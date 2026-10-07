using System;
using System.Collections.Generic;
using WoG.Core.State;

namespace WoG.Erm.Era;

/// <summary>
/// Era's name tables (Erm.pas): ERM function names → ids (FuncNames / FuncIdToNameMap / FuncAutoId) and
/// global constants (GlobalConsts). Shared by every script of a game, filled while scripts are
/// preprocessed in load order, saved with the game (stored in <see cref="EraState"/>).
/// </summary>
public sealed class EraNames
{
    public const int InitialFuncAutoId = EraState.InitialFuncAutoId;

    public EraState State { get; }
    readonly Dictionary<int, string> byId = new();

    public EraNames(EraState? state = null)
    {
        State = state ?? new EraState();
        foreach (var kv in State.Functions) byId[kv.Value] = kv.Key;
        if (State.Constants.Count == 0) ResetConstants();
    }

    public Dictionary<string, int> Functions => State.Functions;
    public Dictionary<string, int> Constants => State.Constants;
    public int FuncAutoId { get => State.FuncAutoId; set => State.FuncAutoId = value; }

    /// <summary>Hook_FindErm_BeforeMainLoop (new game): FuncNames.Clear; FuncAutoId := 95000; RegisterErmEventNames.</summary>
    public void ResetFunctions()
    {
        State.Functions.Clear();
        byId.Clear();
        State.FuncAutoId = InitialFuncAutoId;
        foreach (var (id, name) in EraEvents.Names) NameTrigger(id, name);
    }

    /// <summary>TScriptMan.ClearScripts: GlobalConsts.Clear; RegisterStdGlobalConsts.</summary>
    public void ResetConstants()
    {
        var c = State.Constants;
        c.Clear();
        c["FLOAT_INF"] = BitConverter.SingleToInt32Bits(float.PositiveInfinity);
        c["FLOAT_NEG_INF"] = BitConverter.SingleToInt32Bits(float.NegativeInfinity);
        c["TRUE"] = 1;
        c["FALSE"] = 0;
    }

    /// <summary>NameTrigger: also creates the associative variable i^name^ = id (AdvErm.GetOrCreateAssocVar).</summary>
    public void NameTrigger(int id, string name)
    {
        State.Functions[name] = id;
        byId[id] = name;
        State.GetOrCreateAssoc(name).Int = id;
    }

    /// <summary>AllocErmFunc: true if a new id was allocated, false if the name already had one.</summary>
    public bool AllocFunction(string name, out int id)
    {
        if (State.Functions.TryGetValue(name, out id) && id != 0) return false;
        id = State.FuncAutoId++;
        NameTrigger(id, name);
        return true;
    }

    public bool TryGetFunctionName(int id, out string name) => byId.TryGetValue(id, out name!);

    /// <summary>GetTriggerReadableName: the name used for "&lt;name&gt;_Quit" lookups and plugin events.</summary>
    public string ReadableName(int eventId)
    {
        if (TryGetFunctionName(eventId, out var name)) return name;
        if (eventId >= EraEvents.FU1 && eventId <= EraEvents.FU29999) return "OnErmFunction " + eventId;
        if (eventId >= EraEvents.TM1 && eventId <= EraEvents.TM100) return "OnErmTimer " + (eventId - EraEvents.TM1 + 1);
        if (eventId >= 30100 && eventId <= 30298) return "OnHeroInteraction " + (eventId - 30100);
        if (eventId >= 30401 && eventId <= 30599) return "OnHeroMove " + (eventId - 30401);
        if (eventId >= 30601 && eventId <= 30799) return "OnHeroGainLevel " + (eventId - 30601);
        return "OnErmFunction " + eventId;
    }
}
