using System;
using System.IO;
using System.Linq;
using WoG.Core.Options;
using WoG.Core.Visual;
using WoG.Erm.Runtime;
using WoG.Erm.Syntax;
using WoG.Headless;
using WoG.Host;
using Xunit;

namespace WoG.Tests;

/// <summary>The WoG Options dialog's items and starting option values (wogsetup.cpp BuildAll, AddItem, Prepare2Close).</summary>
public class WoGOptionSetupTests
{
    const string Zsetup =
        "WoG Setup Dialog Information and Hint Text\r\n" +
        "Comment\tScript\tPage\tGroup\tItem\tState\tMP\tERM\r\n" +
        "random\t0\t0\t0\t0\t0\t0\t5\tWoGify random maps\thint\tpopup\r\n" +
        "all\t0\t0\t0\t1\t0\t0\t5\tWoGify all\t\t\r\n" +
        "ask\t0\t0\t0\t3\t1\t1\t5\tWoGify all but ask\t\t\r\n" +
        "towers\t0\t0\t2\t0\t1\t1\t1\tArrow towers gain experience\t\t\r\n" +
        "commanders\t0\t0\t2\t1\t1\t1\t3\tCommanders are enabled\t\t\r\n" +
        "dwellings\t0\t0\t2\t2\t0\t0\t7\tDwellings accumulate\t\t\r\n" +
        "stack exp\t0\t0\t2\t3\t1\t1\t900\tStack Experience\t\t\r\n" +
        "no text\t0\t0\t2\t4\t1\t1\t901\t\t\t\r\n" +
        "script\t-1\t1\t0\t-1\t1\t0\t56\tA script option\t\t\r\n";

    const string Ers =
        "Bank\t-1\t1\t0\t-1\t0\t0\t724\twog_options.724.name\twog_options.724.hint\twog_options.724.text\r\n" +
        "Peons\t-1\t1\t0\t-1\t1\t0\t785\twog_options.785.name\t\t\r\n";

    static WoGOptionSetup Setup()
    {
        var s = new WoGOptionSetup();
        s.Add(WoGOptionSetup.Parse(Zsetup, 2));
        s.Add(WoGOptionSetup.Parse(Ers, 0));
        return s;
    }

    [Fact]
    public void Items_take_their_places_and_rows_without_text_are_skipped()
    {
        var s = Setup();
        Assert.Equal(10, s.Items.Count());
        Assert.Null(s.Group(0, 0)[2]); // the radio group's place 2 is empty
        Assert.Equal(new[] { 56, 724, 785 }, s.Group(1, 0).Where(i => i != null).Select(i => i!.Option).ToArray()); // Item -1: the next place
        Assert.Equal("wog_options.724.name", s.Group(1, 0)[1]!.Text);
    }

    [Fact]
    public void The_defaults_are_the_dialog_states()
    {
        var o = Setup().Defaults();
        Assert.Equal(3, o.Get(WoGOptionIds.ApplyWoG));     // the radio group: the selected item's number
        Assert.Equal(0, o.Get(WoGOptionIds.TowerStd));     // checked "towers gain experience" → standard towers off
        Assert.Equal(0, o.Get(WoGOptionIds.NoNPC));        // checked "commanders are enabled" → no-commanders off
        Assert.True(o.CommandersEnabled);
        Assert.Equal(0, o.Get(WoGOptionIds.DwellAccum));
        Assert.Equal(1, o.Get(WoGOptionIds.CrExpEnable));
        Assert.Equal(0, o.Get(WoGOptionIds.CrExpStyle));   // its row has no text
        Assert.Equal((1, 0, 1), (o.Get(56), o.Get(724), o.Get(785)));
    }

    [Fact]
    public void A_new_ERA_map_starts_with_the_installation_options()
    {
        using var t = new EraTestHost();
        File.WriteAllText(Path.Combine(t.ModDir, "Data", "s", "zsetup01.txt"), Zsetup);
        File.WriteAllText(Path.Combine(t.ModDir, "Data", "s", "script60.ers"), Ers);
        File.WriteAllText(Path.Combine(t.ModDir, "Data", "s", "t.erm"), "ZVSE2\n!?PI;\n!!UN:P56/?v1;\n!!UN:P5/?v2;\n");
        // a loose file in a mod's Data folder is found by Era's VFS: put ZSETUP01 there
        File.Move(Path.Combine(t.ModDir, "Data", "s", "zsetup01.txt"), Path.Combine(t.ModDir, "Data", "zsetup01.txt"));
        t.Host.LoadH3Tables();
        Assert.Equal(Wogification.Ask, t.Host.WogifySetting);
        Assert.Equal(WogifyQuestion.None, t.Host.PlanWogify().Question);
        t.Start();
        Assert.Equal((1, 2), (t.V(1), t.V(2))); // the script option on; option 5 = 2: WoGified
        Assert.True(t.Host.State.Options.CommandersEnabled);
    }

    /// <summary>The dialog (wogsetup.cpp): clicks on its items, Prepare2Close of the marks, a preset shown back, Restore.</summary>
    [Fact]
    public void The_dialog_clicks_give_the_options_and_show_them_back()
    {
        var s = Setup();
        var d = new WoGOptionDialog(s, s.Defaults());
        Assert.Equal(s.Defaults().Values, d.Result().Values);
        d.Click(s.Group(0, 0)[1]!);                             // the radio button "WoGify all"
        d.Click(s.Group(0, 2)[0]!);                             // clear "arrow towers gain experience" (option 1, inverted)
        d.Click(s.Group(1, 0)[1]!);                             // check the bank (724)
        var o = d.Result();
        Assert.Equal(1, o.Get(WoGOptionIds.ApplyWoG));
        Assert.False(d.IsChecked(s.Group(0, 0)[3]!));           // the other radio button is cleared
        Assert.Equal(1, o.Get(WoGOptionIds.TowerStd));          // standard towers
        Assert.Equal(1, o.Get(724));
        var shown = new WoGOptionDialog(s, o);                  // a preset shown again: the same marks
        Assert.Equal(o.Values, shown.Result().Values);
        Assert.True(shown.IsChecked(s.Group(0, 0)[1]!));
        shown.Restore();
        Assert.Equal(s.Defaults().Values, shown.Result().Values);
    }

    /// <summary>The player's choice is kept (WoGSetupEx.dat in WoG) and every new map starts with it.</summary>
    [Fact]
    public void A_saved_choice_is_the_options_of_new_maps()
    {
        using var t = new EraTestHost();
        File.WriteAllText(Path.Combine(t.ModDir, "Data", "zsetup01.txt"), Zsetup);
        File.WriteAllText(Path.Combine(t.ModDir, "Data", "s", "script60.ers"), Ers);
        File.WriteAllText(Path.Combine(t.ModDir, "Data", "s", "t.erm"), "ZVSE2\n!?PI;\n!!UN:P724/?v1;\n!!UN:P1/?v2;\n");
        t.Host.LoadH3Tables();
        var d = t.Host.OptionDialog()!;
        d.Click(t.Host.OptionSetup!.Group(1, 0)[1]!);           // the bank on
        d.Click(t.Host.OptionSetup!.Group(0, 2)[0]!);           // standard towers
        t.Host.SetSetupOptions(d.Result(t.Host.SetupOptions));
        string preset = Path.Combine(t.ModDir, "wog_options.json");
        t.Host.SaveOptionPreset(preset);
        t.Host.SetSetupOptions(t.Host.OptionSetup!.Defaults()); // forget it; the file brings it back
        Assert.True(t.Host.LoadOptionPreset(preset));
        Assert.False(t.Host.LoadOptionPreset(preset + ".missing"));
        t.Start();
        Assert.Equal((1, 1), (t.V(1), t.V(2)));
    }

    /// <summary>The user's ERA installation (ERA_GAME_DIR): WoG Scripts' ZSETUP01 turns 93 options on, WoGify "ask".</summary>
    [Fact]
    public void The_installation_defaults_are_WoGs()
    {
        string? dir = Environment.GetEnvironmentVariable("ERA_GAME_DIR");
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;
        var mods = new[] { "ERA Scripts", "WoG Fix Lite", "WoG Scripts Rus", "WoG Scripts", "WoG Rus", "Era Erm Framework", "WoG" }
            .Select(m => Path.Combine(dir, "Mods", m)).ToArray();
        var host = new WoGHost(new HeadlessGame(), new VisualResolver(new NoAssets()), null, new ErmRuntimeOptions { Dialect = ErmDialect.Era });
        host.AddEraMods(mods);
        var o = host.SetupOptions!;
        Assert.Equal(3, o.Get(WoGOptionIds.ApplyWoG));
        Assert.True(o.CommandersEnabled);
        Assert.Equal(1, o.Get(WoGOptionIds.CrExpEnable));
        Assert.Equal(0, o.Get(WoGOptionIds.TowerStd));
        Assert.Equal(1, o.Get(36)); // Mithril Enhancements
        Assert.InRange(o.Values.Count(v => v != 0), 80, 120);
    }
}
