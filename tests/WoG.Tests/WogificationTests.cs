using System;
using System.IO;
using System.Linq;
using WoG.Core.Options;
using WoG.Core.Visual;
using WoG.Erm.Era;
using WoG.Erm.Runtime;
using WoG.Erm.Syntax;
using WoG.Headless;
using WoG.Host;
using Xunit;

namespace WoG.Tests;

/// <summary>WoGification (WoG option 5) at a new map: WoG 3.58's CheckWogify and ERA's global scripts switch.</summary>
public class WogificationTests
{
    [Theory]
    // era, option, map scripts, fixed set → WoGify, question
    [InlineData(true, 0, 0, false, false, WogifyQuestion.None)]
    [InlineData(true, 1, 0, false, true, WogifyQuestion.None)]
    [InlineData(true, 2, 0, false, true, WogifyQuestion.None)]
    [InlineData(true, 3, 0, false, true, WogifyQuestion.None)]     // ERA asks only a map with its own scripts
    [InlineData(true, 3, 2, false, true, WogifyQuestion.MapScripts)]
    [InlineData(true, 2, 2, false, true, WogifyQuestion.None)]
    [InlineData(true, 0, 0, true, true, WogifyQuestion.None)]      // a fixed script set loads without asking
    [InlineData(false, 0, 0, false, false, WogifyQuestion.None)]
    [InlineData(false, 1, 0, false, true, WogifyQuestion.None)]
    [InlineData(false, 3, 0, false, true, WogifyQuestion.Map)]
    [InlineData(false, 2, 1, false, true, WogifyQuestion.MapScripts)] // WoG asks for a scripted map whatever the option
    public void The_plan_follows_option_5(bool era, int option, int mapScripts, bool fixedSet, bool wogify, WogifyQuestion question)
    {
        var plan = Wogification.Plan(era, option, mapScripts, fixedSet);
        Assert.Equal(wogify, plan.Wogify);
        Assert.Equal(question, plan.Question);
    }

    const string H = "ZVSE2\n";

    static EraTestHost Era()
    {
        var t = new EraTestHost()
            .Script("global.erm", H + "!?PI;\n!!VRv1:S1;\n")
            .Script("other.erm", H + "!?PI;\n!!VRv3:S1;\n");
        Directory.CreateDirectory(Path.Combine(t.ModDir, "Data", "s", "lib"));
        File.WriteAllText(Path.Combine(t.ModDir, "Data", "s", "lib", "lib.erm"), H + "!?PI;\n!!VRv2:S1;\n");
        return t;
    }

    [Fact]
    public void ERA_without_WoGification_loads_no_global_scripts()
    {
        using var t = Era();
        t.Host.StartNewGame(false);
        Assert.Equal((0, 1, 0), (t.V(1), t.V(2), t.V(3)));
        Assert.Equal(0, t.Host.State.Options.Get(WoGOptionIds.ApplyWoG));
        Assert.False(t.Host.State.Wogified);
        Assert.DoesNotContain(t.Host.EraScripts, s => s.Kind == EraScriptKind.Global);
    }

    [Fact]
    public void ERA_WoGified_loads_them_and_sets_option_5_to_2()
    {
        using var t = Era();
        t.Host.WogifySetting = Wogification.Ask;
        Assert.Equal(WogifyQuestion.None, t.Host.PlanWogify().Question); // an Olden Era map has no scripts of its own
        t.Host.StartNewGame(true);
        Assert.Equal((1, 1, 1), (t.V(1), t.V(2), t.V(3)));
        Assert.Equal(2, t.Host.State.Options.Get(WoGOptionIds.ApplyWoG));
    }

    [Fact]
    public void The_default_setting_WoGifies_without_a_question()
    {
        using var t = Era();
        Assert.Equal(WogifyQuestion.None, t.Host.PlanWogify().Question);
        t.Start();
        Assert.Equal(1, t.V(1));
        Assert.True(t.Host.State.Wogified);
    }

    [Fact]
    public void A_fixed_script_set_loads_only_its_global_scripts_without_asking()
    {
        using var t = Era();
        File.WriteAllText(Path.Combine(t.ModDir, "Data", "s", EraScriptSet.FixedSetFile), "global.erm\r\n");
        t.Host.WogifySetting = Wogification.Ask;
        Assert.Equal(WogifyQuestion.None, t.Host.PlanWogify().Question);
        t.Host.StartNewGame();
        Assert.Equal((1, 1, 0), (t.V(1), t.V(2), t.V(3)));
    }

    [Fact]
    public void A_saved_game_keeps_its_WoGification()
    {
        string dir = Path.Combine(Path.GetTempPath(), "wogify-save-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            using var t = Era();
            t.Host.StartNewGame(false);
            string file = Path.Combine(dir, "s.wog.json");
            t.Host.SaveTo(file, "slot");

            using var t2 = Era();
            t2.Host.LoadFrom(file, "slot");
            Assert.False(t2.Host.State.Wogified);
            Assert.DoesNotContain(t2.Host.EraScripts, s => s.Kind == EraScriptKind.Global);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void WoG_without_WoGification_gets_the_classic_rules_and_no_scripts()
    {
        var game = new HeadlessGame();
        var host = new WoGHost(game, new VisualResolver(new NoAssets()), null, new ErmRuntimeOptions { Dialect = ErmDialect.Wog358 });
        host.AddScript("script00.erm", "ZVSE\n!?PI;\n!!VRv1:S1;\n");
        host.State.Options.Set(WoGOptionIds.CrExpEnable, 1);
        host.State.Options.Set(WoGOptionIds.CrExpStyle, 2);
        host.WogifySetting = Wogification.Ask;
        Assert.Equal(WogifyQuestion.Map, host.PlanWogify().Question);
        host.StartNewGame(false);
        var o = host.State.Options;
        Assert.Equal(0, host.State.Erm.V[0]);
        Assert.Equal(new[] { 0, 1, 1, 1, 1, 3, 0 }, Enumerable.Range(0, 7).Select(o.Get).ToArray());
        Assert.Equal(0, o.Get(WoGOptionIds.CrExpEnable));
        Assert.Equal(2, o.Get(WoGOptionIds.CrExpStyle));
        Assert.False(o.CommandersEnabled);
    }

    [Fact]
    public void The_question_falls_back_to_English_without_an_installation()
    {
        using var t = Era();
        Assert.Contains("WoGify", t.Host.WogifyText(WogifyQuestion.Map));
        Assert.Contains("global", t.Host.WogifyText(WogifyQuestion.MapScripts));
    }
}
