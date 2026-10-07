using System;
using System.Collections.Generic;
using System.Linq;
using WoG.Core.Adapters;
using WoG.Core.Model;
using WoG.Core.State;
using WoG.Erm.Runtime;

namespace WoG.Erm.Era;

/// <summary>
/// Library functions of the ERA mods (Era Erm Framework, WoG Scripts, ERA Scripts) whose ERM bodies read H3 memory or
/// call H3 code (UN:C, SN:E, array addresses), implemented natively with the same parameters and results. A call of
/// such a function by name runs the native code instead of its ERM sections. Each one is a port of the function's
/// documented behaviour (the comments of its ERM source) onto the target engine.
/// </summary>
public static class EraNativeLibrary
{
    /// <summary>x1..x16 of the call (x[0] = x1, changed in place = the results), number of arguments passed.</summary>
    public delegate void Native(ErmRuntime rt, int[] x, int numArgs);

    public static void Register(ErmRuntime rt)
    {
        var n = rt.EraNatives;
        // ---- Era Erm Framework (9999 era - stdlib.erm) ----
        n["GetMaxMonsterId"] = (rt, x, _) => x[0] = MonsterCount(rt) - 1;
        n["GetMaxHeroId"] = (_, x, _) => x[0] = WoGLimits.HeroCount - 1;
        n["GetUpgradedMonster"] = (rt, x, _) =>
            x[1] = rt.Services.CreatureTypes.Get(x[0], CreatureStat.UpgradeTo) is { Status: AdapterStatus.Ok, Value: var u } && u != x[0] ? u : -1;
        n["GetTimeMsec"] = (_, x, _) => x[0] = Environment.TickCount;
        n["Array_CountValue"] = (rt, x, numArgs) => x[2] = Matches(rt, x[0], x[1], numArgs > 3 && x[3] != 0).Count();
        n["Array_IndexOf"] = (rt, x, numArgs) => x[2] = Matches(rt, x[0], x[1], numArgs > 3 && x[3] != 0).DefaultIfEmpty(-1).First();
        n["Array_Merge"] = ArrayMerge;
        n["Array_Slice"] = ArraySlice;
        n["Array_Shuffle"] = ArrayShuffle;
        // ---- WoG Scripts (9000 wog - stdlib.erm) and ERA Scripts (1000 es - stdlib.erm) ----
        foreach (var prefix in new[] { "WOG_", "ES_" })
        {
            n[prefix + "PackedCoords"] = (_, x, _) => x[3] = PackCoords(x[0], x[1], x[2]);
            n[prefix + "UnPackedCoords"] = (_, x, _) => (x[0], x[1], x[2]) = UnpackCoords(x[3]);
            n[prefix + "CheckRandomMap"] = (rt, x, _) =>
                x[0] = rt.Services.Game.Map.IsRandomMap() is { Status: AdapterStatus.Ok, Value: true } ? 1 : 0;
        }
        n["WOG_GameMgr_GetPlayer_Me"] = (rt, x, _) => x[0] = LocalPlayer(rt);
        n["WOG_GameMgr_GetPlayer_Team"] = (rt, x, _) =>
            x[1] = rt.Services.Game.Players.GetTeam(x[0]) is { Status: AdapterStatus.Ok, Value: var t } ? t : x[0];
    }

    /// <summary>ARTNUM-like bound for monsters: the installation's creature table, or WoG 3.58's 197.</summary>
    static int MonsterCount(ErmRuntime rt) => rt.Services.H3.Creatures.Count > 0 ? rt.Services.H3.Creatures.Count : 197;

    /// <summary>WoG's PosMixed: x | y &lt;&lt; 16 | 0x04000000 for the underground.</summary>
    public static int PackCoords(int x, int y, int l) => unchecked((x & 0x3FF) | ((y & 0x3FF) << 16) | (l != 0 ? 0x04000000 : 0));

    public static (int X, int Y, int L) UnpackCoords(int p) => (p & 0x3FF, (p >> 16) & 0x3FF, (uint)p >= 0x04000000 ? 1 : 0);

    static int LocalPlayer(ErmRuntime rt)
    {
        var players = rt.Services.Game.Players;
        for (int p = 0; p < WoGLimits.PlayerCount; p++)
            if (players.IsLocal(p) is { Status: AdapterStatus.Ok, Value: true }) return p;
        return players.CurrentPlayer;
    }

    static EraSlot Slot(ErmRuntime rt, int id) =>
        rt.Services.State.Era.Slots.TryGetValue(id, out var s) ? s : throw new ErmRuntimeException($"Slot #{id} does not exist");

    /// <summary>Indexes of the items equal to the value (a z index for string arrays, compared ordinally or ignoring case).</summary>
    static IEnumerable<int> Matches(ErmRuntime rt, int list, int value, bool ignoreCase)
    {
        var s = Slot(rt, list);
        if (!s.IsString) return Enumerable.Range(0, s.Count).Where(i => s.Ints[i] == value);
        string text = rt.EraZRaw(value);
        var cmp = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return Enumerable.Range(0, s.Count).Where(i => string.Equals(s.Strs[i], text, cmp));
    }

    /// <summary>Array_Merge(dst, src…): appends the items of every source array (all of the destination's type).</summary>
    static void ArrayMerge(ErmRuntime rt, int[] x, int numArgs)
    {
        if (numArgs < 2) throw new ErmRuntimeException("Array_Merge: invalid arguments number. Expected at least 2 arguments");
        var dst = Slot(rt, x[0]);
        var sources = Enumerable.Range(1, numArgs - 1).Select(i => (Index: i + 1, Slot: Slot(rt, x[i]))).ToList();
        var wrong = sources.FirstOrDefault(s => s.Slot.IsString != dst.IsString);
        if (wrong.Slot != null) throw new ErmRuntimeException($"Array_Merge: cannot merge arrays of different types. Invalid argument #{wrong.Index}");
        foreach (var (_, s) in sources)
        {
            if (dst.IsString) dst.Strs.AddRange(s.Strs.ToList());
            else dst.Ints.AddRange(s.Ints.ToList());
        }
    }

    /// <summary>Array_Slice(list, start, count, ?result, storage = trigger-local): part of an array as a new array.</summary>
    static void ArraySlice(ErmRuntime rt, int[] x, int numArgs)
    {
        if (numArgs < 4) throw new ErmRuntimeException("Array_Slice: invalid arguments number. Expected at least 4 arguments");
        x[3] = 0;
        var list = Slot(rt, x[0]);
        if (list.Count <= 0) return;
        int storage = numArgs > 4 ? x[4] : (int)EraSlotStorage.TriggerLocal;
        var st = rt.Services.State.Era;
        var result = new EraSlot { IsString = list.IsString, Storage = (EraSlotStorage)storage };
        while (st.Slots.ContainsKey(st.FreeSlotN)) st.FreeSlotN--;
        int id = st.FreeSlotN--;
        st.Slots[id] = result;
        // The new array belongs to the calling trigger (ExtendArrayLifetime): a native call has no frame of its own.
        if (storage == (int)EraSlotStorage.TriggerLocal) rt.RegisterTriggerLocalSlot(id);
        x[3] = id;
        int size = list.Count, start = x[1], count = x[2];
        if (start < 0) start = Math.Clamp(start + size, 0, int.MaxValue);
        if (start >= size) return;
        int max = size - start;
        if (count < 0) count = max + count;
        count = Math.Clamp(count, 0, max);
        if (count <= 0) return;
        if (list.IsString) result.Strs.AddRange(list.Strs.GetRange(start, count));
        else result.Ints.AddRange(list.Ints.GetRange(start, count));
    }

    /// <summary>Array_Shuffle(list): for each index i, swaps the item with a random one from i to the end.</summary>
    static void ArrayShuffle(ErmRuntime rt, int[] x, int _)
    {
        var s = Slot(rt, x[0]);
        int n = s.Count;
        if (n <= 1) return;
        for (int i = 0; i < n; i++)
        {
            int r = rt.Services.Random.Next(i, n - 1);
            if (s.IsString) (s.Strs[i], s.Strs[r]) = (s.Strs[r], s.Strs[i]);
            else (s.Ints[i], s.Ints[r]) = (s.Ints[r], s.Ints[i]);
        }
    }
}
