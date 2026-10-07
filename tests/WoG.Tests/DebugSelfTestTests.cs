using System.Linq;
using WoG.Debug;
using WoG.Erm.Syntax;
using Xunit;

namespace WoG.Tests;

/// <summary>
/// The WoG Debug self-test on the headless reference engine: every case must pass here, so a case that fails
/// in game points at the Olden Era adapter, not at the ERM runtime.
/// </summary>
public class DebugSelfTestTests
{
    static (EraTestHost t, DebugCommands cmd) Setup()
    {
        var t = new EraTestHost();
        t.Host.StartNewGame();
        t.Game.Resources[0, 6] = 1500;
        t.Game.AddHero(0, owner: 0);
        t.Game.ActiveHero[0] = 0;
        t.Game.SetStack(0, 0, 0, 10); // ten pikemen in slot 0 for the army cases
        return (t, new DebugCommands(t.Host, null));
    }

    [Fact]
    public void Every_case_passes_on_the_headless_engine()
    {
        var (t, cmd) = Setup();
        using var _ = t;
        var results = SelfTest.Run(t.Host, cmd.Console);
        var bad = results.Where(r => r.Outcome is not (DebugOutcome.Pass or DebugOutcome.Skipped)).ToList();
        Assert.True(bad.Count == 0, string.Join("\n", bad.Select(r => $"{r.Case.Id}: {r.Outcome} {r.Detail}")));
        Assert.Contains(results, r => r.Case.Interactive && r.Outcome == DebugOutcome.Skipped);
    }

    [Fact]
    public void Self_test_restores_the_state_it_touches()
    {
        var (t, cmd) = Setup();
        using var _ = t;
        t.Host.State.Erm.V[9900] = 123;   // v9901
        t.Host.State.Erm.Z[989] = "keep";  // z990
        SelfTest.Run(t.Host, cmd.Console);
        Assert.Equal(123, t.Host.State.Erm.V[9900]);
        Assert.Equal("keep", t.Host.State.Erm.Z[989]);
        Assert.Equal(1500, t.Game.Resources[0, 6]);
        Assert.DoesNotContain(t.Host.State.Era.Assoc.Keys, k => k.StartsWith("wogdebug_"));
    }

    [Fact]
    public void Console_runs_era_code_and_reports_unsupported_commands()
    {
        var (t, cmd) = Setup();
        using var _ = t;
        Assert.StartsWith("Pass", cmd.Execute("erm !!VRv1:S2 +3"));
        Assert.Equal(5, t.Host.State.Erm.V[0]);
        Assert.StartsWith("Unsupported", cmd.Execute("erm !!UN:C1/2/3"));
        Assert.StartsWith("Error", cmd.Execute("erm !!VRv1:Q"));
    }

    /// <summary>The repository's mods/WoG Debug: the ERA script of the in-game vertical slice.</summary>
    static string DebugModDir()
    {
        var d = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
        while (d != null && !System.IO.Directory.Exists(System.IO.Path.Combine(d.FullName, "mods", "WoG Debug"))) d = d.Parent;
        Assert.NotNull(d);
        return System.IO.Path.Combine(d!.FullName, "mods", "WoG Debug");
    }

    [Fact]
    public void Slice_script_gives_human_players_gold_every_day()
    {
        var game = new WoG.Headless.HeadlessGame();
        var host = new WoG.Host.WoGHost(game, new WoG.Core.Visual.VisualResolver(new WoG.Headless.NoAssets()), null,
            new WoG.Erm.Runtime.ErmRuntimeOptions { Dialect = ErmDialect.Era });
        host.AddEraMods(new[] { DebugModDir() });
        host.StartNewGame();
        game.Resources[0, 6] = 500;
        game.Resources[1, 6] = 500;
        game.Human[1] = false;
        foreach (int p in new[] { 0, 1 })
        {
            game.CurrentPlayer = p; // the engine makes the player whose day starts the current one (OW:C)
            host.Events.Raise(new WoG.Core.Events.WoGEvent { Kind = WoG.Core.Events.WoGEventKind.PlayerDayStarted, Player = p });
        }
        Assert.Equal(1500, game.Resources[0, 6]);
        Assert.Equal(500, game.Resources[1, 6]);
        Assert.DoesNotContain(host.Erm!.Diagnostics, d => d.Severity == ErmSeverity.Error);
    }

    [Fact]
    public void A_second_new_game_starts_clean_without_duplicate_sections()
    {
        var game = new WoG.Headless.HeadlessGame();
        var host = new WoG.Host.WoGHost(game, new WoG.Core.Visual.VisualResolver(new WoG.Headless.NoAssets()), null,
            new WoG.Erm.Runtime.ErmRuntimeOptions { Dialect = ErmDialect.Era });
        host.AddEraMods(new[] { DebugModDir() });
        var log = new System.Collections.Generic.List<string>();
        host.ErmLog = log.Add;
        game.Resources[0, 6] = 0;
        for (int session = 1; session <= 2; session++)
        {
            host.StartNewGame();
            host.State.Erm.V[0] += 1; // v1 survives only within one game
            host.Events.Raise(new WoG.Core.Events.WoGEvent { Kind = WoG.Core.Events.WoGEventKind.PlayerDayStarted, Player = 0 });
            Assert.Equal(1, host.State.Erm.V[0]);
            Assert.Equal(1000 * session, game.Resources[0, 6]); // the slice trigger ran once per day, not twice
        }
        new DebugCommands(host, null).Execute("erm !!VRv2:Q"); // the runtime was rebuilt: the log still arrives
        Assert.NotEmpty(log);
    }

    [Fact]
    public void Report_lists_every_receiver_command()
    {
        var (t, cmd) = Setup();
        using var _ = t;
        string md = cmd.Execute("selftest");
        Assert.Contains("| OW:R |", md);
        Assert.Contains("| UN:C |", md);
        Assert.Contains("not tested", md);
    }
}
