using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using WoG.Core.Services;
using WoG.Core.State;
using WoG.Erm.Receivers;
using WoG.Erm.Syntax;

namespace WoG.Erm.Runtime;

/// <summary>Runtime switches.</summary>
public sealed class ErmRuntimeOptions
{
    public ErmDialect Dialect { get; set; } = ErmDialect.Wog358;
    /// <summary>Reproduce documented WoG bugs (CO:A fall-through, CO:B3 wrong mask, …).</summary>
    public bool ReproduceKnownBugs { get; set; }
    /// <summary>Guard against runaway scripts: maximum nested event depth.</summary>
    public int MaxDepth { get; set; } = 256;
    /// <summary>Era: ErmLegacySupport option of heroes3.ini ("default heroes3.ini" of the ERA project sets 1).</summary>
    public bool EraLegacySupport { get; set; } = true;
    /// <summary>
    /// Milliseconds one engine call into ERM (an event, a function call, loading a script) may run before it is
    /// abandoned with an error; 0 = no limit (as in Era). A host inside a running game sets it: a loop that never ends
    /// because an engine command is unsupported must not freeze the game.
    /// </summary>
    public int TimeLimitMs { get; set; }
    /// <summary>How many times the same unsupported command or error at one script line is written to the log.</summary>
    public int LogRepeatLimit { get; set; } = 3;
}

/// <summary>Thrown when an engine call into ERM runs longer than <see cref="ErmRuntimeOptions.TimeLimitMs"/>.</summary>
public sealed class ErmTimeLimitException : Exception
{
    public ErmTimeLimitException(ErmSourceLoc? loc, long ms) : base($"ran longer than {ms} ms") { Loc = loc; }
    public ErmSourceLoc? Loc { get; }
}

/// <summary>
/// The ERM interpreter: owns the loaded trigger sections, the local-variable frames and the execution
/// rules of ProcessERM / ProcessCmd / CheckConditions (erm.cpp). Persistent variables live in
/// <see cref="WoGGameState.Erm"/>.
/// </summary>
public sealed partial class ErmRuntime
{
    public IWoGServices Services { get; }
    public ErmRuntimeOptions Options { get; }
    public ReceiverRegistry Receivers { get; }
    public List<ErmDiagnostic> Diagnostics { get; } = new();
    /// <summary>Optional log sink (WOGERMLOG.TXT equivalent).</summary>
    public Action<string>? Log { get; set; }

    WoGVariables Vars => Services.State.Erm;

    readonly List<ErmTriggerSection> sections = new();
    readonly Dictionary<int, List<ErmTriggerSection>> byEvent = new();
    int scopeCounter;
    int currentScope;
    int depth;

    // ---- local frames (never saved) -----------------------------------------------------------
    internal int[] X = new int[16];
    int[] y = new int[100];
    int[] yt = new int[100];
    float[] f = new float[100];
    float[] ft = new float[100];
    string[] lz = NewLz();
    readonly Stack<(int[] y, int[] yt, float[] f, float[] ft, string[] lz)> frames = new();

    // ---- section control (TriggerBreak / TriggerGoTo) ----------------------------------------
    internal bool TriggerBreak;
    internal int TriggerGoTo;

    /// <summary>The FU:P / DO:P call whose function is running (FU:X inspects it).</summary>
    internal ErmCall? LastFunctionCall { get; set; }

    /// <summary>Context of the innermost running event.</summary>
    public ErmEventContext Context { get; private set; } = new();

    public ErmRuntime(IWoGServices services, ErmRuntimeOptions? options = null)
    {
        Services = services;
        Options = options ?? new ErmRuntimeOptions();
        Receivers = Options.Dialect == ErmDialect.Era ? ReceiverRegistry.CreateEra(this) : ReceiverRegistry.CreateDefault(this);
    }

    static string[] NewLz()
    {
        var a = new string[20];
        for (int i = 0; i < a.Length; i++) a[i] = "";
        return a;
    }

    public IReadOnlyList<ErmTriggerSection> Sections => sections;

    // ============================================================================================
    // Loading
    // ============================================================================================

    /// <summary>
    /// Loads a parsed script: registers its sections in order and runs its instructions (!#) the way
    /// ParseERM does — during loading, interleaved with section registration, only for a new game
    /// (or, in a loaded game, after a "!@ZVSE" marker).
    /// </summary>
    public void Load(ErmScript script, bool newGame) => Guarded("loading " + script.Name, () => LoadUnguarded(script, newGame));

    void LoadUnguarded(ErmScript script, bool newGame)
    {
        Diagnostics.AddRange(script.Diagnostics);
        if (!script.IsErm) return;
        int scope = ++scopeCounter;
        int oldScope = currentScope;
        currentScope = scope;
        bool postInst = false;
        var ifState = new IfState();
        try
        {
            foreach (var item in script.Items)
            {
                switch (item.Kind)
                {
                    case ErmItemKind.Section:
                        var sec = item.Section!;
                        sec.Scope = scope;
                        if (sec.Id == "TM" && sec.EventId == ErmEventIds.AutoTimerBase)
                            sec.EventId = AddAutoTimer(sec);
                        sections.Add(sec);
                        if (!byEvent.TryGetValue(sec.EventId, out var list)) byEvent[sec.EventId] = list = new();
                        list.Add(sec);
                        break;
                    case ErmItemKind.PostInstructionMarker:
                        // ParseERM: '@' stops the file for a new game; for a loaded game, "!@ZVSE"
                        // switches on instructions from here on.
                        if (newGame || item.Line!.Id != "@ZVSE") return;
                        postInst = true;
                        break;
                    case ErmItemKind.Instruction:
                        if (!newGame && !postInst) break;
                        var line = item.Line!;
                        if (IsEra ? !CheckConditionEra(line.Condition) : CheckConditions(line, ifState)) break;
                        TriggerBreak = false;
                        ExecuteLine(line);
                        if (TriggerBreak) { TriggerBreak = false; return; } // FU:E in an instruction ends the file
                        break;
                }
            }
            if (ifState.Total != 0) Error(null, "no ENDIF for IF");
        }
        finally
        {
            currentScope = oldScope;
        }
    }

    int AddAutoTimer(ErmTriggerSection sec)
    {
        // AddTimer: auto timers live in ERMTimer[100..199]; identical setups share a slot.
        var n = sec.Params.Select(p => p.Number).ToArray();
        for (int i = 0; i < autoTimers.Count; i++)
            if (autoTimers[i].SequenceEqual(n)) return ErmEventIds.AutoTimerBase + i;
        autoTimers.Add(n);
        return ErmEventIds.AutoTimerBase + autoTimers.Count - 1;
    }

    readonly List<int[]> autoTimers = new();

    // ============================================================================================
    // Firing events (ProcessERM)
    // ============================================================================================

    /// <summary>Raises an ERM event with a fresh context (engine entry point).</summary>
    public void Raise(int eventId, ErmEventContext context) => Guarded("event " + eventId, () =>
    {
        if (IsEra) ProcessEra(eventId, context);
        else Process(eventId, context, needLocals: true);
    });

    // ---- time limit (ErmRuntimeOptions.TimeLimitMs) --------------------------------------------

    readonly System.Diagnostics.Stopwatch watch = new();
    int guardDepth;
    long steps;

    /// <summary>
    /// Runs an engine call into ERM under the time limit. Nested calls share the outer call's clock; when the limit
    /// is hit, the whole outer call is abandoned (frames are restored by the finally blocks it unwinds through).
    /// </summary>
    internal void Guarded(string what, Action action)
    {
        if (guardDepth++ == 0) { watch.Restart(); steps = 0; }
        try { action(); }
        catch (ErmTimeLimitException ex) when (guardDepth == 1)
        {
            Error(ex.Loc, $"{what} {ex.Message} and was abandoned (an endless loop? see the line)");
        }
        finally { guardDepth--; }
    }

    /// <summary>Called for every executed line and loop turn: throws when the current engine call is over time.</summary>
    internal void CheckTime(ErmSourceLoc? loc)
    {
        if (guardDepth == 0 || Options.TimeLimitMs <= 0 || (++steps & 1023) != 0) return;
        if (watch.ElapsedMilliseconds > Options.TimeLimitMs) throw new ErmTimeLimitException(loc, Options.TimeLimitMs);
    }

    internal void Process(int eventId, ErmEventContext context, bool needLocals)
    {
        if (!byEvent.TryGetValue(eventId, out var list) || list.Count == 0) return;
        if (++depth > Options.MaxDepth)
        {
            depth--;
            Error(null, $"ERM recursion deeper than {Options.MaxDepth} events — event {eventId} ignored");
            return;
        }
        var outerContext = Context;
        Context = context;
        bool localsStored = false;
        bool fu = ErmEventIds.IsFunction(eventId);
        int prevScope = currentScope;
        int callerScope = currentScope;
        TriggerGoTo = 0;
        try
        {
            int idx = 0;
            var done = new List<int>();     // indices of sections reached, for FU:E -n
            while (idx < list.Count)
            {
                var sec = list[idx];
                int me = idx;
                idx++;
                if (ErmEventIds.IsLocalFunction(eventId) && sec.Scope != callerScope) continue;
                if (--TriggerGoTo >= 0) continue;
                TriggerGoTo = 0;
                TriggerBreak = false;
                done.Add(me);

                SetContextVariables(context);
                if (!localsStored && needLocals)
                {
                    localsStored = true;
                    StoreVars(fu);
                }
                if (CheckCondition(sec.Condition))
                {
                    currentScope = sec.Scope;
                    if (!fu)
                    {
                        Array.Clear(yt, 0, yt.Length);
                        Array.Clear(ft, 0, ft.Length);
                    }
                    RunSection(sec);
                    currentScope = prevScope;
                }
                if (TriggerGoTo < -done.Count) TriggerGoTo = -done.Count;
                if (TriggerGoTo < 0)
                {
                    idx = done[done.Count + TriggerGoTo];
                    done.RemoveRange(done.Count + TriggerGoTo, -TriggerGoTo);
                    TriggerGoTo = 0;
                }
            }
        }
        finally
        {
            if (localsStored) RestoreVars();
            TriggerBreak = false;
            TriggerGoTo = 0;
            currentScope = prevScope;
            Context = outerContext;
            depth--;
        }
    }

    void SetContextVariables(ErmEventContext c)
    {
        bool human = c.IsHuman ?? (c.Player >= 0 && Services.Game.Players.IsHuman(c.Player) is { IsOk: true, Value: true });
        bool local = c.IsLocal ?? (c.Player >= 0 && Services.Game.Players.IsLocal(c.Player) is { IsOk: true, Value: true });
        Vars.Flags[999] = human;
        Vars.Flags[998] = local;
        Vars.V[997] = c.Position.X;
        Vars.V[998] = c.Position.Y;
        Vars.V[999] = c.Position.L;
    }

    sealed class IfState
    {
        public int Ghost, Total;
        public bool IsFalse;
    }

    void RunSection(ErmTriggerSection sec)
    {
        var ifs = new IfState();
        var labels = new int[50];
        var labelIf = new int[50];
        for (int k = 0; k < 50; k++) labels[k] = -1;
        int needLabel = -1;
        var lines = sec.Lines;
        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            CheckTime(line.Loc);
            if (line.Id == "la")
            {
                int j = LabelId(line);
                if (j < 0) return;
                labels[j] = i;
                labelIf[j] = ifs.Total;
                if (j == needLabel) needLabel = -1;
                continue;
            }
            if (needLabel >= 0 || CheckConditions(line, ifs)) continue;
            if (line.Id == "go")
            {
                int j = LabelId(line);
                if (j < 0) return;
                if (labels[j] >= 0)
                {
                    i = labels[j];
                    ifs.Total = labelIf[j];
                    ifs.Ghost = 0;
                    ifs.IsFalse = false;
                }
                else needLabel = j;
                continue;
            }
            ExecuteLine(line);
            if (TriggerBreak) return;
        }
        if (ifs.Total != 0) Error(sec.Loc, "no ENDIF for IF");
    }

    int LabelId(ErmReceiverLine line)
    {
        int j = line.Selector.Count > 0 ? line.Selector[0].Number : -1;
        if (j < 0 || j > 49) { Error(line.Loc, "wrong goto label (0...49)"); return -1; }
        return j;
    }

    /// <summary>CheckConditions: if/el/en bookkeeping; returns true when the line must be skipped.</summary>
    bool CheckConditions(ErmReceiverLine line, IfState a)
    {
        switch (line.Id)
        {
            case "if":
                a.Total++;
                if (a.Ghost > 0 || a.IsFalse) a.Ghost++;
                else a.IsFalse = !CheckCondition(line.Condition);
                return true;
            case "el":
                if (a.Ghost == 0)
                {
                    if (!a.IsFalse)
                    {
                        if (a.Total == 0) { Error(line.Loc, "\"el\" - no IF for ELSE"); return true; }
                        a.Ghost++;
                        a.IsFalse = false;
                    }
                    else a.IsFalse = !CheckCondition(line.Condition);
                }
                return true;
            case "en":
                if (--a.Total < 0) { Error(line.Loc, "\"en\" - no IF for ENDIF"); a.Total = 0; return true; }
                if (a.Ghost > 0) a.Ghost--; else a.IsFalse = false;
                return true;
        }
        if (a.Ghost > 0 || a.IsFalse) return true;
        return !CheckCondition(line.Condition);
    }

    /// <summary>ProcessCmd: runs the commands of one line; an error abandons the rest of the line.</summary>
    internal void ExecuteLine(ErmReceiverLine line)
    {
        CheckTime(line.Loc);
        var receiver = Receivers.Get(line.Id);
        if (receiver == null)
        {
            Services.Compat.Unsupported("erm", "!!" + line.Id, "receiver not implemented");
            return;
        }
        // Era: command-local strings (^literals^, s^^ values) live until the line is done (Hook_ProcessCmd)
        var prevCmdLocal = cmdLocalErt;
        cmdLocalErt = null;
        try
        {
            ExecuteCommands(line, receiver);
        }
        finally
        {
            FreeCmdLocal(prevCmdLocal);
        }
    }

    void ExecuteCommands(ErmReceiverLine line, IErmReceiver receiver)
    {
        foreach (var cmd in line.Commands)
        {
            if (cmd.Letter == '\0') { Error(line.Loc, cmd.Text ?? "syntax error"); return; }
            try
            {
                var call = new ErmCall(this, line, cmd);
                receiver.Execute(call);
            }
            catch (ErmRuntimeException ex)
            {
                Error(line.Loc, $"!!{line.Id}:{cmd.Letter} — {ex.Message}");
                return;
            }
            catch (ErmUnsupportedException ex)
            {
                Services.Compat.Unsupported("erm", $"!!{line.Id}:{cmd.Letter}", ex.Message);
                if (ShouldLog(line.Loc + ":" + cmd.Letter))
                    Log?.Invoke($"[unsupported] {line.Loc} !!{line.Id}:{cmd.Letter} — {ex.Message}");
            }
            if (TriggerBreak) return;
        }
    }

    // ============================================================================================
    // Functions and loops
    // ============================================================================================

    /// <summary>FU:P semantics: x1..x16 = args, call, restore x; returns the callee's final x values.</summary>
    public int[] CallFunction(int number, IReadOnlyList<int> args, bool needLocals = true)
    {
        int ev = FunctionEvent(number);
        var oldX = (int[])X.Clone();
        Array.Clear(X, 0, 16);
        for (int i = 0; i < args.Count && i < 16; i++) X[i] = args[i];
        Guarded("function " + number, () => Process(ev, Context, needLocals));
        var newX = (int[])X.Clone();
        X = oldX;
        return newX;
    }

    internal int FunctionEvent(int n)
    {
        if (n > ErmEventIds.FunctionMax) throw new ErmRuntimeException("Function Index out of range (1...30000)");
        if (n < 0)
        {
            if (n < -100) throw new ErmRuntimeException("Local Function Index out of range (-100...-1)");
            return -n + ErmEventIds.LocalFunctionBase - 1;
        }
        return n;
    }

    internal void StoreVars(bool fu)
    {
        frames.Push((y, yt, f, ft, lz));
        var newLz = NewLz();
        if (fu)
        {
            y = new int[100];
            f = new float[100];
            // backward compatibility: z-1..z-10 are inherited by functions
            Array.Copy(lz, newLz, 10);
        }
        else
        {
            yt = new int[100];
            ft = new float[100];
        }
        lz = newLz;
    }

    internal void RestoreVars() => (y, yt, f, ft, lz) = frames.Pop();

    // ============================================================================================
    // Timers (RunTimer)
    // ============================================================================================

    /// <summary>Start of a player's day: fires every due TM timer (and auto-timer) for that owner.</summary>
    public void RunTimers(int owner, int absoluteDay)
    {
        int msk = 1 << owner;
        bool human = Services.Game.Players.IsHuman(owner) is { IsOk: true, Value: true };
        // Era Hook_RunTimer: universal !?FU(OnEveryDay) for every colour before the !?TM triggers
        if (IsEra) Raise(Era.EraEvents.DailyTimer, new ErmEventContext { Player = owner, IsHuman = human });
        void Fire(int ev) => Raise(ev, new ErmEventContext { Player = owner, IsHuman = human });
        for (int i = 0; i < WoGVariables.TimerCount; i++)
        {
            var t = Vars.Timers[i];
            if (Due(t.FirstDay, t.LastDay, t.Period, t.OwnerMask)) Fire(ErmEventIds.TimerBase + i);
        }
        for (int i = 0; i < autoTimers.Count; i++)
        {
            var a = autoTimers[i];
            if (Due(a[0], a[1], a[2], a[3])) Fire(ErmEventIds.AutoTimerBase + i);
        }

        bool Due(int first, int last, int period, int owners)
        {
            if (owners == 0 || (owners & msk) == 0) return false;
            if (first > absoluteDay || last < absoluteDay) return false;
            // WoG divides by Period without a check (crash on 0); a zero period never fires here.
            if (period == 0) return false;
            return (absoluteDay - first) % period == 0;
        }
    }

    // ============================================================================================
    // Diagnostics
    // ============================================================================================

    internal void Error(ErmSourceLoc? loc, string message)
    {
        var d = new ErmDiagnostic { Severity = ErmSeverity.Error, Loc = loc ?? default, Message = message };
        Diagnostics.Add(d);
        if (ShouldLog(d.Loc + "|" + message)) Log?.Invoke(d.ToString());
    }

    readonly Dictionary<string, int> logged = new(StringComparer.Ordinal);

    /// <summary>
    /// The log gets the first few occurrences of the same message from the same line (a loop repeats it thousands
    /// of times); every occurrence is still counted in Diagnostics and the compatibility report.
    /// </summary>
    bool ShouldLog(string key)
    {
        logged.TryGetValue(key, out int n);
        logged[key] = ++n;
        if (n == Options.LogRepeatLimit + 1) Log?.Invoke($"[log] {key}: repeated — further occurrences are not logged");
        return n <= Options.LogRepeatLimit;
    }
}
