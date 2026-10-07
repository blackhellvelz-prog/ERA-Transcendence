using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WoG.Core.Adapters;
using WoG.Core.Compat;
using WoG.Core.Model;
using WoG.Core.State;
using WoG.Host;

namespace WoG.Debug;

/// <summary>What a self-test case can read: ERM variables and the engine, through the adapter directly.</summary>
public sealed class SelfTestEnv
{
    public SelfTestEnv(WoGHost host) { Host = host; }

    public WoGHost Host { get; }
    public IGameAdapter Game => Host.Game;
    WoGVariables Vars => Host.State.Erm;

    public int V(int i) => Vars.V[i - 1];
    public string Z(int i) => Vars.Z[i - 1];
    public bool F(int i) => Vars.Flags[i - 1];
    public int Assoc(string name) => Host.State.Era.Assoc.TryGetValue(name, out var a) ? a.Int : 0;
    public int CurrentPlayer => Game.Players.CurrentPlayer;

    /// <summary>Values a case saved in Before to restore in After.</summary>
    public Dictionary<string, int> Saved { get; } = new();
}

/// <summary>
/// One check: ERM code run in the host, then a C# check that compares the ERM result with the engine read
/// directly (the oracle). {u} in the code is replaced by a suffix unique to the run, so helper functions
/// loaded by repeated runs never stack up.
/// </summary>
public sealed class SelfTestCase
{
    public string Id { get; init; } = "";
    /// <summary>Receiver commands this case exercises ("OW:R", "VR:S", …), for the coverage table.</summary>
    public string[] Covers { get; init; } = Array.Empty<string>();
    public string Body { get; init; } = "";
    public string Extra { get; init; } = "";
    /// <summary>Changes game state (restored in After).</summary>
    public bool TouchesGame { get; init; }
    /// <summary>Opens a dialog in the game; runs only with "selftest interactive".</summary>
    public bool Interactive { get; init; }
    public Action<SelfTestEnv>? Before { get; init; }
    public Action<SelfTestEnv>? After { get; init; }
    /// <summary>Optional custom run instead of Body (events and the like); returns the ERM run result.</summary>
    public Func<SelfTestEnv, ErmConsole, string, ErmRunResult>? Run { get; init; }
    /// <summary>Null when the result is right, otherwise what is wrong.</summary>
    public Func<SelfTestEnv, string?>? Check { get; init; }
}

public sealed class SelfTestResult
{
    public SelfTestCase Case { get; init; } = new();
    public DebugOutcome Outcome { get; set; }
    public string Detail { get; set; } = "";
}

/// <summary>
/// The WoG Debug self-test: every case of <see cref="Cases"/> plus a coverage table of every receiver
/// command the runtime declares. The same suite runs on the headless engine (tests) and inside the game
/// (debug bridge), so a difference between the two is an adapter problem, not an ERM one.
/// ERM state used by the cases (v9901–v9999, z990–z999, flags 900–909, timer 100, i^wogdebug_*^) is saved
/// before the run and restored after it.
/// </summary>
public static class SelfTest
{
    // OW:R resource numbers 0..6 (WoG.Core WoGLimits.ResourceCount).
    static readonly string[] ResourceNames = { "wood", "mercury", "ore", "sulfur", "crystal", "gems", "gold" };

    public static IReadOnlyList<SelfTestCase> Cases { get; } = BuildCases();

    static List<SelfTestCase> BuildCases()
    {
        var list = new List<SelfTestCase>
        {
            // ---- ERM runtime (engine-independent) ---------------------------------------------------
            new()
            {
                Id = "core.vr.arithmetic", Covers = new[] { "VR:S", "VR:+", "VR:-", "VR:*", "VR::" },
                Body = "!!VRv9901:S7 +5 *3 -1 :5;",
                Check = e => Expect(e.V(9901), 7),
            },
            new()
            {
                Id = "core.vr.modulo", Covers = new[] { "VR:%" },
                Body = "!!VRv9902:S17 %5;",
                Check = e => Expect(e.V(9902), 2),
            },
            new()
            {
                Id = "core.condition", Covers = new[] { "conditions" },
                Body = "!!VRv9901:S7;\n!!VRv9903:S0;\n!!VRv9903&v9901=7:S1;\n!!VRv9903&v9901=8:S2;",
                Check = e => Expect(e.V(9903), 1),
            },
            new()
            {
                Id = "core.string.concat", Covers = new[] { "VR:S", "VR:+" },
                Body = "!!VRz990:S^Hello^ +^ World^;",
                Check = e => Expect(e.Z(990), "Hello World"),
            },
            new()
            {
                Id = "core.string.interpolation", Covers = new[] { "VR:S" },
                Body = "!!VRi^wogdebug_n^:S42;\n!!VRz991:S^n=%i(wogdebug_n)^;",
                Check = e => Expect(e.Z(991), "n=42"),
            },
            new()
            {
                Id = "core.function.params", Covers = new[] { "FU:P", "VA" },
                Body = "!!FU(WogDebug_Add{u}):P3/4/?v9905;",
                Extra = "!?FU(WogDebug_Add{u});\n!#VA(a:x) (b:x) (result:x);\n!!VR(result):S(a) +(b);\n",
                Check = e => Expect(e.V(9905), 7),
            },
            new()
            {
                Id = "core.do.loop", Covers = new[] { "DO:P" },
                Body = "!!VRv9906:S0;\n!!DO(WogDebug_Sum{u})/1/10/1:P;",
                Extra = "!?FU(WogDebug_Sum{u});\n!!VRv9906:+x16;\n",
                Check = e => Expect(e.V(9906), 55),
            },
            new()
            {
                Id = "core.flags", Covers = new[] { "IF:V" },
                Body = "!!IF:V900/1;\n!!IF:V901/0;",
                Check = e => e.F(900) && !e.F(901) ? null : $"flags 900/901 = {e.F(900)}/{e.F(901)}, expected True/False",
            },
            new()
            {
                Id = "core.assoc", Covers = new[] { "VR:S" },
                Body = "!!VRi^wogdebug_x^:S5 +1;\n!!VRv9907:Si^wogdebug_x^;",
                Check = e => Expect(e.V(9907), 6),
            },
            new()
            {
                Id = "core.sn.array", Covers = new[] { "SN:M" },
                Body = "!!SN:M-1/3/0/0;\n!!VRv9908:Sv1;\n!!SN:Mv9908/1/42;\n!!SN:Mv9908/1/?v9909;",
                Check = e => Expect(e.V(9909), 42),
            },
            new()
            {
                Id = "core.sn.global", Covers = new[] { "SN:W" },
                Body = "!!SN:W^wogdebug_w^/77;\n!!SN:W^wogdebug_w^/?v9910;",
                Check = e => Expect(e.V(9910), 77),
            },
            new()
            {
                Id = "core.function.exit", Covers = new[] { "FU:E" },
                Body = "!!VRv9911:S1;\n!!FU:E;\n!!VRv9911:S2;",
                Check = e => Expect(e.V(9911), 1),
            },
            new()
            {
                Id = "core.timer", Covers = new[] { "TM:S" },
                Before = e => SaveTimer(e, 99),
                After = e => RestoreTimer(e, 99),
                Body = "!!TM100:S1/999/1/255;",
                Check = e =>
                {
                    var t = e.Host.State.Erm.Timers[99];
                    return t.FirstDay == 1 && t.LastDay == 999 && t.Period == 1 && t.OwnerMask == 255
                        ? null : $"timer 100 = {t.FirstDay}/{t.LastDay}/{t.Period}/{t.OwnerMask}";
                },
            },

            // ---- engine: clock and players (ERM result vs. the adapter read directly) ------------------
            new()
            {
                Id = "game.clock.day", Covers = new[] { "VR:S" },
                Body = "!!VRv9913:Sc;",
                Check = e => Expect(e.V(9913), e.Game.Clock.AbsoluteDay),
            },
            new()
            {
                Id = "game.ow.current", Covers = new[] { "OW:C" },
                Body = "!!OW:C?v9914;",
                Check = e => Expect(e.V(9914), e.CurrentPlayer),
            },
            new()
            {
                Id = "game.ow.human", Covers = new[] { "OW:I" },
                Body = "!!OW:I-1/?v9916;",
                Check = e => e.Game.Players.IsHuman(e.CurrentPlayer) is { IsOk: true } h
                    ? Expect(e.V(9916), h.Value ? 0 : 1) : "adapter: " + e.Game.Players.IsHuman(e.CurrentPlayer),
            },
            new()
            {
                Id = "game.ow.local", Covers = new[] { "OW:G" },
                Body = "!!OW:G-1/?v9917;",
                Check = e => e.Game.Players.IsLocal(e.CurrentPlayer) is { IsOk: true } l
                    ? Expect(e.V(9917), l.Value ? 1 : 0) : "adapter: " + e.Game.Players.IsLocal(e.CurrentPlayer),
            },
            new()
            {
                Id = "game.ow.activeHero", Covers = new[] { "OW:A" },
                Body = "!!OW:A-1/?v9918;",
                Check = e => e.Game.Players.GetActiveHero(e.CurrentPlayer) is { IsOk: true } a
                    ? Expect(e.V(9918), a.Value) : "adapter: " + e.Game.Players.GetActiveHero(e.CurrentPlayer),
            },
            new()
            {
                Id = "game.ow.resource.add", Covers = new[] { "OW:R" }, TouchesGame = true,
                Before = e => { if (e.Game.Players.GetResource(e.CurrentPlayer, 6) is { IsOk: true } g) e.Saved["gold"] = g.Value; },
                After = e => { if (e.Saved.TryGetValue("gold", out int g)) e.Game.Players.SetResource(e.CurrentPlayer, 6, g); },
                Body = "!!OW:R-1/6/d100;",
                Check = e => e.Saved.TryGetValue("gold", out int g)
                    ? Expect(e.Game.Players.GetResource(e.CurrentPlayer, 6).Value, g + 100)
                    : "adapter could not read gold",
            },
            new()
            {
                Id = "game.he.experience", Covers = new[] { "HE:E" },
                // HE-1 is the hero of the event (none in a debug call), so the active hero is addressed by
                // number; without an active hero (OW:A unsupported) the case is unsupported, not an HE error.
                Run = (e, c, u) =>
                {
                    var active = c.Run("!!OW:A-1/?v9918;");
                    return active.Outcome != DebugOutcome.Pass ? active : c.Run("!!HEv9918:E?v9919;");
                },
                Check = e => e.Game.Heroes.Get(e.V(9918), HeroStat.Experience) is { IsOk: true } x
                    ? Expect(e.V(9919), x.Value) : "adapter: " + e.Game.Heroes.Get(e.V(9918), HeroStat.Experience),
            },
            new()
            {
                Id = "game.un.option", Covers = new[] { "UN:P" },
                Body = "!!UN:P3/?v9920;",
            },
            new()
            {
                Id = "game.event.everyDay", Covers = new[] { "OnEveryDay" }, TouchesGame = true,
                // The loaded mods' OnEveryDay triggers run too (the WoG Debug slice adds gold): undo their effect.
                Before = SaveResources,
                After = RestoreResources,
                // A trigger on OnEveryDay counts its calls; raising the event once must call it once, for the
                // current player. (The trigger stays loaded; its counter is unique to this run.)
                Run = (e, c, u) =>
                {
                    var load = c.Run("", $"!?FU(OnEveryDay);\n!!VRi^wogdebug_day{u}^:+1;\n!!OW:C?i^wogdebug_owner{u}^;\n");
                    if (load.Outcome != DebugOutcome.Pass) return load;
                    var run = c.Raise("OnEveryDay", e.CurrentPlayer);
                    if (run.Outcome != DebugOutcome.Pass) return run;
                    int calls = e.Assoc("wogdebug_day" + u), owner = e.Assoc("wogdebug_owner" + u);
                    if (calls != 1 || owner != e.CurrentPlayer)
                    {
                        run.Outcome = DebugOutcome.Fail;
                        run.Errors.Add($"OnEveryDay trigger ran {calls} time(s) with OW:C = {owner}; expected 1 and {e.CurrentPlayer}");
                    }
                    return run;
                },
            },
            new()
            {
                Id = "ui.message", Covers = new[] { "IF:M" }, Interactive = true,
                Body = "!!IF:M^WoG Debug: message test^;",
            },
            new()
            {
                Id = "ui.question", Covers = new[] { "IF:Q" }, Interactive = true,
                Body = "!!IF:Q1/0/0/1^WoG Debug: yes or no?^;",
            },
        };

        for (int r = 0; r < ResourceNames.Length; r++)
        {
            int res = r;
            list.Add(new()
            {
                Id = "game.ow.resource.get." + ResourceNames[res], Covers = new[] { "OW:R" },
                Body = $"!!OW:R-1/{res}/?v9915;",
                Check = e => e.Game.Players.GetResource(e.CurrentPlayer, res) is { IsOk: true } v
                    ? Expect(e.V(9915), v.Value) : "adapter: " + e.Game.Players.GetResource(e.CurrentPlayer, res),
            });
        }
        return list;
    }

    static string? Expect(int actual, int expected) => actual == expected ? null : $"got {actual}, expected {expected}";
    static string? Expect(string actual, string expected) => actual == expected ? null : $"got \"{actual}\", expected \"{expected}\"";

    static void SaveResources(SelfTestEnv e)
    {
        for (int r = 0; r < WoGLimits.ResourceCount; r++)
            if (e.Game.Players.GetResource(e.CurrentPlayer, r) is { IsOk: true } v) e.Saved["res" + r] = v.Value;
    }

    static void RestoreResources(SelfTestEnv e)
    {
        for (int r = 0; r < WoGLimits.ResourceCount; r++)
            if (e.Saved.TryGetValue("res" + r, out int v)) e.Game.Players.SetResource(e.CurrentPlayer, r, v);
    }

    static void SaveTimer(SelfTestEnv e, int i)
    {
        var t = e.Host.State.Erm.Timers[i];
        e.Saved["t.first"] = t.FirstDay; e.Saved["t.last"] = t.LastDay; e.Saved["t.period"] = t.Period; e.Saved["t.owners"] = t.OwnerMask;
    }

    static void RestoreTimer(SelfTestEnv e, int i)
    {
        var t = e.Host.State.Erm.Timers[i];
        t.FirstDay = e.Saved["t.first"]; t.LastDay = e.Saved["t.last"]; t.Period = e.Saved["t.period"]; t.OwnerMask = e.Saved["t.owners"];
    }

    static int runCounter;

    /// <param name="filter">Case id prefix; null runs every non-interactive case.</param>
    public static IReadOnlyList<SelfTestResult> Run(WoGHost host, ErmConsole console, string? filter = null, bool interactive = false)
    {
        var results = new List<SelfTestResult>();
        var vars = host.State.Erm;
        var savedV = vars.V.Skip(9899).Take(100).ToArray();
        var savedZ = vars.Z.Skip(989).Take(11).ToArray();
        var savedF = vars.Flags.Skip(899).Take(10).ToArray();
        string u = "_" + (++runCounter);
        try
        {
            foreach (var c in Cases)
            {
                var r = new SelfTestResult { Case = c };
                results.Add(r);
                if (filter != null && !c.Id.StartsWith(filter, StringComparison.OrdinalIgnoreCase)) { r.Outcome = DebugOutcome.Skipped; r.Detail = "filtered out"; continue; }
                if (c.Interactive && !interactive) { r.Outcome = DebugOutcome.Skipped; r.Detail = "interactive (run \"selftest interactive\")"; continue; }
                if (host.Erm == null || !host.Erm.IsEra) { r.Outcome = DebugOutcome.Skipped; r.Detail = "the suite uses ERA syntax; ERM runs in WoG mode"; continue; }
                var env = new SelfTestEnv(host);
                try
                {
                    c.Before?.Invoke(env);
                    var run = c.Run != null ? c.Run(env, console, u) : console.Run(c.Body.Replace("{u}", u), c.Extra.Replace("{u}", u));
                    r.Outcome = run.Outcome;
                    if (run.Outcome == DebugOutcome.Pass)
                    {
                        string? fail = c.Check?.Invoke(env);
                        if (fail != null) { r.Outcome = DebugOutcome.Fail; r.Detail = fail; }
                    }
                    else r.Detail = run.Summary();
                }
                catch (Exception ex) { r.Outcome = DebugOutcome.Error; r.Detail = ex.GetType().Name + ": " + ex.Message; }
                finally
                {
                    try { c.After?.Invoke(env); }
                    catch (Exception ex) { r.Detail += " | restore failed: " + ex.Message; }
                }
            }
        }
        finally
        {
            Array.Copy(savedV, 0, vars.V, 9899, savedV.Length);
            Array.Copy(savedZ, 0, vars.Z, 989, savedZ.Length);
            Array.Copy(savedF, 0, vars.Flags, 899, savedF.Length);
            foreach (var k in host.State.Era.Assoc.Keys.Where(k => k.StartsWith("wogdebug_", StringComparison.Ordinal)).ToList())
                host.State.Era.Assoc.Remove(k);
        }
        return results;
    }

    public static string Summary(IReadOnlyList<SelfTestResult> results) =>
        "selftest: " + string.Join(", ", Enum.GetValues<DebugOutcome>()
            .Select(o => $"{results.Count(r => r.Outcome == o)} {o.ToString().ToLowerInvariant()}"));

    /// <summary>Markdown report: case results, then every receiver command with its declared level and what tested it.</summary>
    public static string ToMarkdown(WoGHost host, IReadOnlyList<SelfTestResult> results, string engineName)
    {
        var sb = new StringBuilder();
        sb.Append("# WoG Debug self-test\n\n");
        sb.Append($"Engine: {engineName}. {Summary(results)}.\n\n");
        sb.Append("| Case | Covers | Result | Detail |\n|---|---|---|---|\n");
        foreach (var r in results)
            sb.Append($"| {r.Case.Id} | {string.Join(", ", r.Case.Covers)} | {r.Outcome} | {Cell(r.Detail)} |\n");

        sb.Append("\n## Receiver coverage\n\n");
        sb.Append("| Command | Declared | Tested by | Observed |\n|---|---|---|---|\n");
        var byCommand = results.Where(r => r.Outcome != DebugOutcome.Skipped)
            .SelectMany(r => r.Case.Covers.Select(c => (c, r)))
            .GroupBy(x => x.c).ToDictionary(g => g.Key, g => g.Select(x => x.r).ToList());
        foreach (var rec in host.Erm!.Receivers.All.OrderBy(r => r.Id, StringComparer.Ordinal))
        {
            if (rec.Support.Count == 0)
            {
                sb.Append($"| {rec.Id} | {(rec is WoG.Erm.Receivers.UnsupportedReceiver u ? "Unsupported: " + Cell(u.Reason) : "—")} | — | — |\n");
                continue;
            }
            foreach (var (letter, support) in rec.Support.OrderBy(x => x.Key))
            {
                string key = rec.Id + ":" + letter;
                byCommand.TryGetValue(key, out var tested);
                string by = tested == null ? "not tested" : string.Join(", ", tested.Select(t => t.Case.Id));
                string observed = tested == null ? "—" : Worst(tested).ToString();
                sb.Append($"| {key} | {support.Level} | {by} | {observed} |\n");
            }
        }
        return sb.ToString();
    }

    static DebugOutcome Worst(List<SelfTestResult> rs) =>
        rs.Any(r => r.Outcome == DebugOutcome.Error) ? DebugOutcome.Error
        : rs.Any(r => r.Outcome == DebugOutcome.Fail) ? DebugOutcome.Fail
        : rs.Any(r => r.Outcome == DebugOutcome.Unsupported) ? DebugOutcome.Unsupported
        : DebugOutcome.Pass;

    static string Cell(string s) => s.Replace("|", "\\|").Replace("\n", " ");
}
