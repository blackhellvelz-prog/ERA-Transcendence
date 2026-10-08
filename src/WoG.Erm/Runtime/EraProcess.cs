using System;
using System.Collections.Generic;
using WoG.Core.State;
using WoG.Erm.Era;
using WoG.Erm.Receivers;
using WoG.Erm.Syntax;

namespace WoG.Erm.Runtime;

/// <summary>
/// Era's ProcessErm (Erm.pas): per-event local variables, if/el/en and re/br/co flow control, SN:G jumps,
/// SN:Q, "&lt;event&gt;_Quit" triggers, DO-loop callbacks and FU:P argument passing (ArgXVars / RetXVars).
/// </summary>
public sealed partial class ErmRuntime
{
    /// <summary>One running Era trigger (TTriggerLocalData).</summary>
    internal sealed class EraFrame
    {
        public bool IsQuitTrigger;
        public int CmdIndex;
        public int? JumpTo;
        public readonly List<int> LocalErt = new();
        public readonly List<int> LocalSlots = new();
        public EraFrame? Prev;
    }

    internal EraFrame? eraFrame;

    // ArgXVars / RetXVars / RetStrVars and the argument bookkeeping of FU:P, DO:P, FU:A, FU:S
    internal int[] ArgX = new int[16];
    internal int[] RetX = new int[16];
    /// <summary>x1..x16 as the last ERA event or function left them (the debug console shows them).</summary>
    public int[] LastReturns => (int[])RetX.Clone();
    internal string[] RetStr = new string[17];
    internal int NumFuncArgsPassed, NumFuncArgsReceived;
    internal int FuncArgsGetSyntaxFlagsPassed, FuncArgsGetSyntaxFlagsReceived;
    /// <summary>Erm.FuncArgs: the parameters of the FU:P/DO:P call that has string GET arguments.</summary>
    internal ErmCall? FuncArgs;
    internal bool IsQuitTriggerSignal;
    internal bool QuitTriggerFlag;
    /// <summary>TriggerLoopCallback: makes the next event repeat while it returns true (DO:P).</summary>
    internal Func<bool>? TriggerLoopCallback;

    /// <summary>ErmLegacySupport (heroes3.ini; the ERA project ships 1).</summary>
    public bool EraLegacySupport => Options.EraLegacySupport;

    const int StateFalse = 0, StateTrue = 1, StateInactive = 2;
    const int OperIf = 0, OperRe = 1;

    sealed class FlowOper
    {
        public int State;
        public int OperType;
        public ErmParam? LoopVar;
        public int Stop;
        public int Step;
        public int CmdInd;
    }

    /// <summary>Raised to abandon the whole event ("goto AfterTriggers" on a flow-control error).</summary>
    sealed class EraAbortEvent : Exception { }

    /// <summary>FireErmEventEx: sets x1..xN for an event, then fires it.</summary>
    public void RaiseEra(int eventId, ErmEventContext context, params int[] args) => Guarded("event " + eventId, () =>
    {
        for (int i = 0; i < args.Length && i < 16; i++) ArgX[i] = args[i];
        ProcessEra(eventId, context);
    });

    /// <summary>Library functions implemented natively, by ERA function name (<see cref="WoG.Erm.Era.EraNativeLibrary"/>).</summary>
    public Dictionary<string, WoG.Erm.Era.EraNativeLibrary.Native> EraNatives { get; } = new(StringComparer.Ordinal);

    internal void ProcessEra(int eventId, ErmEventContext context)
    {
        if (EraNatives.Count > 0 && EraNames.TryGetFunctionName(eventId, out var fname) && EraNatives.TryGetValue(fname, out var native))
        {
            // The native replaces the function's ERM sections: x1..x16 in, x1..x16 out, in the caller's trigger.
            TriggerLoopCallback = null;
            FuncArgs = null;
            IsQuitTriggerSignal = false;
            var x = (int[])ArgX.Clone();
            native(this, x, NumFuncArgsPassed);
            RetX = x;
            return;
        }
        var loopCallback = TriggerLoopCallback;
        TriggerLoopCallback = null;
        var funcArgs = FuncArgs;
        FuncArgs = null;
        var frame = new EraFrame { IsQuitTrigger = IsQuitTriggerSignal };
        IsQuitTriggerSignal = false;

        byEvent.TryGetValue(eventId, out var list);
        bool has = list != null && list.Count > 0;
        if (!has)
        {
            RetX = (int[])ArgX.Clone();
            return;
        }
        if (++depth > Options.MaxDepth)
        {
            depth--;
            Error(null, $"ERM recursion deeper than {Options.MaxDepth} events — event {eventId} ignored");
            RetX = (int[])ArgX.Clone();
            return;
        }

        bool legacy = EraLegacySupport;
        bool classicFunc = EraEvents.IsClassicFunction(eventId);
        var outerContext = Context;
        Context = context;

        // SaveVars
        var savedY = (int[])y.Clone();
        int[]? savedNY = legacy && !classicFunc ? (int[])yt.Clone() : null;
        var savedE = (float[])f.Clone();
        var savedX = X;
        X = (int[])ArgX.Clone();
        var savedQuick = (int[])Vars.Quick.Clone();
        int savedNumArgs = NumFuncArgsReceived;
        NumFuncArgsReceived = NumFuncArgsPassed;
        int savedSyntax = FuncArgsGetSyntaxFlagsReceived;
        FuncArgsGetSyntaxFlagsReceived = FuncArgsGetSyntaxFlagsPassed;
        var savedNZ = new string[EraNzCount];
        Array.Copy(lz, savedNZ, EraNzCount);
        var savedF = new bool[5];
        Array.Copy(Vars.Flags, 995, savedF, 0, 5);        // f996..f1000
        var savedV = new int[4];
        Array.Copy(Vars.V, 996, savedV, 0, 4);             // v997..v1000

        // ResetLocalVars
        Array.Clear(y, 0, y.Length);
        Array.Clear(f, 0, f.Length);
        if (!legacy || !classicFunc)
            for (int i = 0; i < EraNzCount; i++) lz[i] = "";

        SetContextVariables(context);

        frame.Prev = eraFrame;
        eraFrame = frame;
        int quitId = 0;
        if (!frame.IsQuitTrigger && EraNames.Functions.TryGetValue(EraNames.ReadableName(eventId) + "_Quit", out int q)) quitId = q;

        try
        {
            while (true)
            {
                bool stopAll = false;
                foreach (var sec in list!.ToArray())
                {
                    if (sec.Lines.Count == 0) continue;
                    if (!CheckConditionEra(sec.Condition)) continue;
                    // For classic WoG non-function triggers only
                    if (legacy && EraEvents.IsClassicNonFunctionTrigger(eventId)) Array.Clear(yt, 0, yt.Length);
                    var outcome = RunSectionEra(sec, frame);
                    if (outcome == SectionOutcome.Quit)
                    {
                        if (!frame.IsQuitTrigger) { stopAll = true; break; }
                    }
                }
                _ = stopAll;

                // TriggersProcessed
                if (quitId != 0)
                {
                    ArgX = (int[])X.Clone();
                    IsQuitTriggerSignal = true;
                    ProcessEra(quitId, context);
                    X = (int[])RetX.Clone();
                }
                if (loopCallback == null || !loopCallback()) break;
            }
        }
        catch (EraAbortEvent)
        {
            // AfterTriggers
        }
        finally
        {
            depth--;
            // It's a function call: save result string variables
            if (funcArgs != null)
            {
                for (int j = 0; j < NumFuncArgsReceived && j < funcArgs.Num; j++)
                {
                    var p = funcArgs.Cmd.Params[j];
                    if (p.Mode == ErmParamMode.Get && EraIsString(p))
                    {
                        try { RetStr[j + 1] = EraZInterpolated(X[j]); }
                        catch (ErmRuntimeException) { RetStr[j + 1] = ""; }
                    }
                }
            }

            // RestoreVars
            y = savedY;
            if (savedNY != null) yt = savedNY;
            f = savedE;
            RetX = X;
            X = savedX;
            Vars.Quick = savedQuick;
            NumFuncArgsReceived = savedNumArgs;
            FuncArgsGetSyntaxFlagsReceived = savedSyntax;
            Array.Copy(savedNZ, lz, EraNzCount);
            Array.Copy(savedF, 0, Vars.Flags, 995, 5);
            Array.Copy(savedV, 0, Vars.V, 996, 4);

            foreach (int i in frame.LocalErt) localErt.Remove(i);
            foreach (int s in frame.LocalSlots) EraSt.Slots.Remove(s);
            eraFrame = frame.Prev;
            Context = outerContext;
        }
    }

    enum SectionOutcome { Done, Quit }

    SectionOutcome RunSectionEra(ErmTriggerSection sec, EraFrame frame)
    {
        var opers = new FlowOper[16];
        for (int k = 0; k < opers.Length; k++) opers[k] = new FlowOper();
        int level = -1;
        TriggerBreak = false;
        QuitTriggerFlag = false;
        var lines = sec.Lines;
        int i = 0;
        while (i < lines.Count)
        {
            CheckTime(lines[i].Loc);
            var line = lines[i];
            frame.CmdIndex = i;
            switch (line.Id)
            {
                case "if":
                {
                    if (++level >= opers.Length) Abort(line, "\"if\" - too many IF/REs (>16)");
                    var o = opers[level];
                    o.OperType = OperIf;
                    o.State = level == 0 || opers[level - 1].State == StateTrue
                        ? (CheckConditionEra(line.Condition) ? StateTrue : StateFalse)
                        : StateInactive;
                    break;
                }
                case "el":
                {
                    if (level < 0 || opers[level].OperType != OperIf) Abort(line, "\"el\" - no IF for ELSE");
                    var o = opers[level];
                    if (o.State == StateTrue) o.State = StateInactive;
                    else if (o.State == StateFalse) o.State = CheckConditionEra(line.Condition) ? StateTrue : StateFalse;
                    break;
                }
                case "en":
                {
                    if (level < 0) Abort(line, "\"en\" - no IF/RE for ENDIF");
                    var o = opers[level];
                    if (o.State != StateTrue || o.OperType == OperIf) level--;
                    else
                    {
                        int value = unchecked(EraGetInt(o.LoopVar!) + o.Step);
                        if (o.Step != 0) EraSetInt(o.LoopVar!, value);
                        if ((o.Step >= 0 && value > o.Stop) || (o.Step < 0 && value < o.Stop)) level--;
                        else i = o.CmdInd - 1;
                    }
                    break;
                }
                case "re":
                {
                    if (++level >= opers.Length) Abort(line, "\"re\" - too many IF/REs (>16)");
                    var o = opers[level];
                    o.OperType = OperRe;
                    if (level == 0 || opers[level - 1].State == StateTrue)
                    {
                        o.State = StateTrue;
                        o.LoopVar = line.Selector[0];
                        o.Stop = int.MaxValue;
                        o.Step = 1;
                        o.CmdInd = i + 1;
                        int n = line.Selector.Count;
                        int value;
                        try
                        {
                            if (n >= 2)
                            {
                                value = EraGetInt(line.Selector[1]);
                                EraSetInt(o.LoopVar, value);
                            }
                            else value = EraGetInt(o.LoopVar);
                            if (n >= 3) o.Stop = EraGetInt(line.Selector[2]);
                            else o.Step = 0;
                            if (n >= 4) o.Step = EraGetInt(line.Selector[3]);
                            if (n >= 5) o.Stop = unchecked(o.Stop + EraGetInt(line.Selector[4]));
                        }
                        catch (ErmRuntimeException ex)
                        {
                            Error(line.Loc, "!!re — " + ex.Message);
                            value = 0;
                        }
                        if (o.Step >= 0 ? value > o.Stop : value < o.Stop) o.State = StateInactive;
                    }
                    else o.State = StateInactive;
                    break;
                }
                case "br":
                case "co":
                {
                    if ((level < 0 || opers[level].State == StateTrue) && CheckConditionEra(line.Condition))
                    {
                        if (level < 0) Abort(line, "\"br/co\" - no loop to break/continue");
                        var t = EraValType.Int;
                        int target = line.Selector.Count > 0 ? EraGetValue(line.Selector[0], out t, out _, strAsText: true) : 0;
                        if (t != EraValType.Int)
                            Error(line.Loc, "\"br/co\" - loop index must be positive number. Given: non-integer");
                        if (target < 0) Abort(line, "\"br/co\" - loop index must be positive number. Given: " + target);
                        if (target == 0) target = 1;
                        int j = level;
                        while (j >= 0 && target > 0)
                        {
                            if (opers[j].OperType != OperRe) j--;
                            else
                            {
                                target--;
                                if (target > 0) j--;
                            }
                        }
                        if (j < 0) Abort(line, "\"br/co\" - no loop to break/continue");
                        level = j;
                        var o = opers[j];
                        i = o.CmdInd - 1;
                        if (line.Id == "br") o.State = StateInactive;
                        else
                        {
                            int value = unchecked(EraGetInt(o.LoopVar!) + o.Step);
                            if (o.Step != 0) EraSetInt(o.LoopVar!, value);
                            if ((o.Step >= 0 && value > o.Stop) || (o.Step < 0 && value < o.Stop)) o.State = StateInactive;
                        }
                    }
                    break;
                }
                default:
                    if ((level < 0 || opers[level].State == StateTrue) && CheckConditionEra(line.Condition))
                    {
                        ExecuteLine(line);
                        if (TriggerBreak)
                        {
                            TriggerBreak = false;
                            return SectionOutcome.Done;
                        }
                        if (QuitTriggerFlag)
                        {
                            QuitTriggerFlag = false;
                            return SectionOutcome.Quit;
                        }
                        if (frame.JumpTo is int jump)
                        {
                            frame.JumpTo = null;
                            i = jump - 1;
                        }
                    }
                    break;
            }
            i++;
        }
        return SectionOutcome.Done;
    }

    void Abort(ErmReceiverLine line, string message)
    {
        Error(line.Loc, message);
        throw new EraAbortEvent();
    }

    /// <summary>
    /// FU:P / DO:P argument setup (Hook_FU_P / DO_P): x1..xN from the parameters; ?string params pass 0 and
    /// get the result string back; ?number params pass their current value (by reference); local z
    /// strings are copied into command-local ERT strings; d- negates; syntax flags (2 bits per argument:
    /// 0 = GET, 1 = value, 2 = d-modified).
    /// </summary>
    internal void EraSetupArgs(ErmCall c, int maxArgs, int clearUpTo)
    {
        FuncArgsGetSyntaxFlagsPassed = 0;
        int n = Math.Min(c.Num, maxArgs);
        for (int i = 0; i < n; i++)
        {
            var p = c.P(i);
            ArgX[i] = c.N(i);
            if (p.Mode == ErmParamMode.Get)
            {
                if (EraIsString(p))
                {
                    ArgX[i] = 0;
                    FuncArgs = c;
                }
                else
                {
                    ArgX[i] = EraGetInt(p);
                }
            }
            else if (EraTypeOf(p) == ErmVarKind.Z && ArgX[i] < 0)
            {
                ArgX[i] = CreateCmdLocalErt(EraZInterpolated(ArgX[i]));
            }
            if (p.Modifier == ErmModifier.Sub) ArgX[i] = unchecked(-ArgX[i]);
            int flags = p.Modifier != ErmModifier.None ? 2 : p.Mode == ErmParamMode.Get ? 0 : 1;
            FuncArgsGetSyntaxFlagsPassed |= flags << (i << 1);
        }
        for (int i = n; i < clearUpTo; i++)
        {
            ArgX[i] = 0;
            FuncArgsGetSyntaxFlagsPassed |= 1 << (i << 1);
        }
        NumFuncArgsPassed = n;
    }

    /// <summary>ApplyFuncByRefRes: copies results back into the ?-parameters of the call.</summary>
    internal void EraApplyFuncResults(ErmCall c, int numParams)
    {
        for (int i = 0; i < numParams && i < c.Num; i++)
        {
            var p = c.P(i);
            if (p.Mode != ErmParamMode.Get) continue;
            try
            {
                if (EraIsString(p)) EraSetString(p, RetStr[i + 1] ?? "");
                else EraSetInt(p, RetX[i]);
            }
            catch (ErmRuntimeException ex)
            {
                Error(c.Line.Loc, ex.Message);
            }
        }
    }

    /// <summary>SN:G: continue the current trigger at command number <paramref name="cmd"/>.</summary>
    internal void EraGoto(int cmd)
    {
        if (eraFrame == null) throw new ErmRuntimeException("SN:G outside of a trigger");
        eraFrame.JumpTo = cmd;
    }
}
