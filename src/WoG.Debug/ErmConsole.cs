using System;
using System.Collections.Generic;
using System.Linq;
using WoG.Erm.Era;
using WoG.Erm.Runtime;
using WoG.Erm.Syntax;
using WoG.Host;

namespace WoG.Debug;

public enum DebugOutcome
{
    Pass,
    Fail,
    /// <summary>The engine cannot do it (an adapter/receiver reported Unsupported).</summary>
    Unsupported,
    /// <summary>ERM error or exception.</summary>
    Error,
    Skipped,
}

/// <summary>What one ad-hoc ERM run did: diagnostics, unsupported commands and log lines it produced.</summary>
public sealed class ErmRunResult
{
    public DebugOutcome Outcome { get; set; } = DebugOutcome.Pass;
    public List<string> Errors { get; } = new();
    public List<string> Unsupported { get; } = new();
    public List<string> Log { get; } = new();
    public int[] Returns { get; set; } = Array.Empty<int>();

    public string Summary()
    {
        var parts = new List<string> { Outcome.ToString() };
        if (Errors.Count > 0) parts.Add("errors: " + string.Join(" | ", Errors));
        if (Unsupported.Count > 0) parts.Add("unsupported: " + string.Join(" | ", Unsupported));
        return string.Join("; ", parts);
    }
}

/// <summary>
/// Runs ERM text typed by a person or a test inside the running WoG host. The text becomes the body of a
/// one-off function (ERA: <c>!?FU(WogDebug_Exec_N)</c>, preprocessed with the game's shared names so it can
/// call the loaded scripts' functions and constants; WoG: a classic <c>!?FU</c> number) which is then called.
/// </summary>
public sealed class ErmConsole
{
    readonly WoGHost host;
    int counter;

    public ErmConsole(WoGHost host) { this.host = host; }

    ErmRuntime Erm => host.Erm ?? throw new InvalidOperationException("ERM is disabled in this host");

    /// <param name="body">Lines executed in the function body.</param>
    /// <param name="extra">More triggers appended after the body (helper functions used by the body).</param>
    public ErmRunResult Run(string body, string extra = "", params int[] args) => Tracked(result =>
    {
        var erm = Erm;
        int n = ++counter;
        if (erm.IsEra)
        {
            string name = "WogDebug_Exec_" + n;
            string text = "ZVSE2\n!?FU(" + name + ");\n" + body.TrimEnd() + "\n" + extra;
            var diags = new List<ErmDiagnostic>();
            string pp = EraPreprocessor.Process("wogdebug-" + n + ".erm", text, erm.EraNames, diags);
            var script = ErmParser.ParseText("wogdebug-" + n + ".erm", pp, ErmDialect.Era);
            script.Diagnostics.InsertRange(0, diags);
            erm.Load(script, newGame: true);
            // FireErmEventEx: an ERA function (95000+) is called like any event, with x1..x16 = args.
            erm.RaiseEra(erm.EraNames.Functions[name], new ErmEventContext { Player = host.Game.Players.CurrentPlayer }, args);
        }
        else
        {
            int fn = 29000 + n % 1000; // classic function numbers near the top of the FU range
            string text = "ZVSE\n!?FU" + fn + ";\n" + body.TrimEnd() + "\n" + extra;
            erm.Load(ErmParser.ParseText("wogdebug-" + n + ".erm", text, erm.Options.Dialect), newGame: true);
            result.Returns = erm.CallFunction(fn, args);
        }
    });

    /// <summary>Raises an ERM/ERA event by number or ERA name (OnEveryDay, OnGameEnter, …).</summary>
    public ErmRunResult Raise(string eventName, int player) => Tracked(result =>
    {
        var erm = Erm;
        if (!int.TryParse(eventName, out int id) && !(erm.IsEra && erm.EraNames.Functions.TryGetValue(eventName, out id)))
            throw new ArgumentException("unknown event " + eventName);
        bool? human = host.Game.Players.IsHuman(player) is { IsOk: true } h ? h.Value : null;
        erm.Raise(id, new ErmEventContext { Player = player, IsHuman = human });
    });

    /// <summary>Runs an action and collects the ERM errors, unsupported commands and log lines it caused.</summary>
    ErmRunResult Tracked(Action<ErmRunResult> action)
    {
        var result = new ErmRunResult();
        var erm = Erm;
        int diagnosticsBefore = erm.Diagnostics.Count;
        var compatBefore = host.Compat.Entries.ToDictionary(e => (e.Area, e.Item, e.Reason), e => e.Count);
        var oldLog = erm.Log;
        erm.Log = m => { result.Log.Add(m); oldLog?.Invoke(m); };
        try { action(result); }
        catch (Exception ex) { result.Errors.Add(ex.GetType().Name + ": " + ex.Message); }
        finally { erm.Log = oldLog; }
        foreach (var d in erm.Diagnostics.Skip(diagnosticsBefore))
            if (d.Severity == ErmSeverity.Error) result.Errors.Add(d.ToString());
        foreach (var e in host.Compat.Entries)
        {
            compatBefore.TryGetValue((e.Area, e.Item, e.Reason), out int before);
            if (e.Count > before) result.Unsupported.Add($"{e.Item} — {e.Reason}");
        }
        result.Outcome = result.Errors.Count > 0 ? DebugOutcome.Error
            : result.Unsupported.Count > 0 ? DebugOutcome.Unsupported
            : DebugOutcome.Pass;
        return result;
    }
}
