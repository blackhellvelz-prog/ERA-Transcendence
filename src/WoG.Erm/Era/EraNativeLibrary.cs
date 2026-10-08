using System;
using System.Collections.Generic;
using System.Linq;
using WoG.Core.Adapters;
using WoG.Core.H3Data;
using WoG.Core.Model;
using WoG.Core.State;
using WoG.Erm.Receivers;
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
        // artifacts: ERA reads and changes H3's hero structure (HE:Z + UN:C, SN:E to H3's own functions); here the
        // hero adapter does it, with ERA's artifact numbers (NO_ART -1, a spell scroll is 1 with its spell as modifier)
        n["GetArtAtSlot"] = GetArtAtSlot;
        n["ChangeArtModAtSlot"] = ChangeArtModAtSlot;
        n["AddArtToHero"] = AddArtToHero;
        n["EquipArtToSlot"] = EquipArtToSlot;
        n["UnequipArtFromSlot"] = UnequipArtFromSlot;
        n["GetHeroPrimarySkillsWithoutArts"] = (rt, x, _) =>
        {
            int hero = Hero(rt, x[0]);
            var stats = new[] { HeroStat.Attack, HeroStat.Defence, HeroStat.Power, HeroStat.Knowledge };
            for (int i = 0; i < 4; i++) x[i + 1] = Need(rt.Services.Game.Heroes.GetBase(hero, stats[i]));
        };
        // ---- WoG Scripts (9000 wog - stdlib.erm) and ERA Scripts (1000 es - stdlib.erm) ----
        foreach (var prefix in new[] { "WOG_", "ES_" })
        {
            n[prefix + "PackedCoords"] = (_, x, _) => x[3] = PackCoords(x[0], x[1], x[2]);
            n[prefix + "UnPackedCoords"] = (_, x, _) => (x[0], x[1], x[2]) = UnpackCoords(x[3]);
            n[prefix + "CheckRandomMap"] = (rt, x, _) =>
                x[0] = rt.Services.Game.Map.IsRandomMap() is { Status: AdapterStatus.Ok, Value: true } ? 1 : 0;
        }
        n["WOG_GameMgr_GetPlayer_Me"] = (rt, x, _) => x[0] = LocalPlayer(rt);
        // H3's experience of a level (SN:E to 0x4DA690): the engine's level table; int.MaxValue past its last level
        n["WOG_GetExpRequirementOfLevel"] = (rt, x, _) => x[1] = x[0] < 1 ? 0 : Need(rt.Services.Game.Heroes.ExperienceForLevel(-1, x[0]));
        n["WOG_GameMgr_GetPlayer_Team"] = (rt, x, _) =>
            x[1] = rt.Services.Game.Players.GetTeam(x[0]) is { Status: AdapterStatus.Ok, Value: var t } ? t : x[0];
    }

    // ---- artifacts ------------------------------------------------------------------------------

    // Era Erm Framework's constants: NO_ART, NO_ART_MOD, ART_SPELL_SCROLL, NO_ART_SLOT; slots 0..18 worn, 19..82 backpack
    const int NoArt = -1, NoArtMod = -1, SpellScroll = 1, NoArtSlot = -1;

    /// <summary>A hero argument: (CURRENT_HERO) = -1 is the hero of the trigger.</summary>
    static int Hero(ErmRuntime rt, int hero)
    {
        if (hero == -1)
        {
            if (rt.Context.Hero < 0) throw new ErmRuntimeException("no current hero");
            return rt.Context.Hero;
        }
        if (hero < 0 || hero >= WoGLimits.HeroCount || !rt.Services.Game.Heroes.Exists(hero))
            throw new ErmRuntimeException($"hero {hero} does not exist");
        return hero;
    }

    static T Need<T>(AdapterResult<T> r) => r.Status switch
    {
        AdapterStatus.Ok => r.Value,
        AdapterStatus.Unsupported => throw new ErmUnsupportedException(r.Reason ?? "unsupported"),
        _ => throw new ErmRuntimeException(r.Reason ?? "failed"),
    };

    static bool Done(AdapterResult r) => r.Status switch
    {
        AdapterStatus.Ok => true,
        AdapterStatus.Unsupported => throw new ErmUnsupportedException(r.Reason ?? "unsupported"),
        _ => false,
    };

    /// <summary>A position of HE:A (-1 empty, 1001 + spell = a scroll) as ERA's artifact and modifier.</summary>
    static (int Art, int Mod) Decode(int position) =>
        position < 0 ? (NoArt, NoArtMod)
        : position >= ArtifactSlots.ScrollBase ? (SpellScroll, position - ArtifactSlots.ScrollBase)
        : (position, NoArtMod);

    static int Encode(int art, int mod) => art == SpellScroll && mod >= 0 ? ArtifactSlots.ScrollBase + mod : art;

    static int CheckSlot(string fn, int slot) =>
        slot >= 0 && slot < ArtifactSlots.Positions ? slot : throw new ErmRuntimeException($"{fn}: invalid artifact slot ID: {slot}");

    /// <summary>GetArtAtSlot(hero, slot, ?art, ?artMod): the artifact at a worn or backpack slot.</summary>
    static void GetArtAtSlot(ErmRuntime rt, int[] x, int _)
    {
        int hero = Hero(rt, x[0]), slot = CheckSlot("GetArtAtSlot", x[1]);
        (x[2], x[3]) = Decode(Need(rt.Services.Game.Heroes.GetArtifacts(hero))[slot]);
    }

    /// <summary>ChangeArtModAtSlot(hero, slot, artMod): the spell of a scroll; other artifacts have no modifier here.</summary>
    static void ChangeArtModAtSlot(ErmRuntime rt, int[] x, int _)
    {
        int hero = Hero(rt, x[0]), slot = CheckSlot("GetArtAtSlot", x[1]), mod = x[2];
        var heroes = rt.Services.Game.Heroes;
        var (art, old) = Decode(Need(heroes.GetArtifacts(hero))[slot]);
        if (art == NoArt || mod == old) return;
        if (art != SpellScroll || mod < 0)
            throw new ErmUnsupportedException("ChangeArtModAtSlot: a custom artifact modifier (other than a scroll's spell) has no Olden Era equivalent");
        if (Done(heroes.RemoveArtifactAt(hero, slot))) Done(heroes.PutArtifact(hero, slot, Encode(art, mod)));
    }

    /// <summary>AddArtToHero(hero, art, artMod, ?result): the game's own "give an artifact" (a fitting slot, else the backpack).</summary>
    static void AddArtToHero(ErmRuntime rt, int[] x, int _)
    {
        int hero = Hero(rt, x[0]);
        x[3] = Done(rt.Services.Game.Heroes.EquipArtifact(hero, Encode(x[1], x[2]))) ? 1 : 0;
    }

    /// <summary>
    /// EquipArtToSlot(hero, art, artMod, slot, ?result): puts the artifact into an empty worn slot it fits (slot
    /// NO_ART_SLOT: the first one); FALSE when the slot is wrong, taken or does not fit it.
    /// </summary>
    static void EquipArtToSlot(ErmRuntime rt, int[] x, int _)
    {
        x[4] = 0;
        int slot = x[3];
        if (slot < NoArtSlot || slot > ArtifactSlots.Misc5) return;
        int hero = Hero(rt, x[0]), art = x[1];
        var heroes = rt.Services.Game.Heroes;
        int p2 = art == SpellScroll ? 9 : rt.Services.Artifact(art)?.Position ?? 0;
        var arts = Need(heroes.GetArtifacts(hero));
        foreach (int s in ArtifactSlots.ForPosition(p2))
        {
            if (slot != NoArtSlot && s != slot || arts[s] >= 0) continue;
            x[4] = Done(heroes.PutArtifact(hero, s, Encode(art, x[2]))) ? 1 : 0;
            return;
        }
    }

    /// <summary>UnequipArtFromSlot(hero, slot): takes the artifact off a worn slot (it is gone, as in H3).</summary>
    static void UnequipArtFromSlot(ErmRuntime rt, int[] x, int _)
    {
        int hero = Hero(rt, x[0]), slot = x[1];
        if (slot < 0 || slot >= ArtifactSlots.Worn) return;
        var heroes = rt.Services.Game.Heroes;
        if (Need(heroes.GetArtifacts(hero))[slot] >= 0) Done(heroes.RemoveArtifactAt(hero, slot));
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
