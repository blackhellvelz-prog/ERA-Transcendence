using System;
using System.IO;
using WoG.Erm.Era;
using Xunit;

namespace WoG.Tests;

/// <summary>Era's cached ini files (b2 Ini.pas) and the SN:F exports over them.</summary>
public class EraIniTests : IDisposable
{
    readonly string game = Path.Combine(Path.GetTempPath(), "era-ini-game-" + Guid.NewGuid().ToString("N"));
    readonly string write = Path.Combine(Path.GetTempPath(), "era-ini-write-" + Guid.NewGuid().ToString("N"));

    public EraIniTests()
    {
        Directory.CreateDirectory(Path.Combine(game, "Runtime"));
        File.WriteAllText(Path.Combine(game, "Runtime", "mod.ini"),
            "; comment\r\n[Main]\r\nGold = 500 ; trailing comment\r\nName=Orrin\r\n\r\n[Other]\r\nx=1\r\n");
    }

    public void Dispose()
    {
        try { Directory.Delete(game, true); } catch { }
        try { Directory.Delete(write, true); } catch { }
    }

    EraIni Ini() { var i = new EraIni(write); i.ReadRoots.Add(game); return i; }

    [Fact]
    public void Reads_from_the_era_folder_case_insensitively_and_trims()
    {
        var ini = Ini();
        Assert.True(ini.ReadStrFromIni("gold", "main", @"Runtime\mod.ini", out var v));
        Assert.Equal("500", v);
        Assert.True(ini.ReadStrFromIni("Name", "MAIN", "runtime/MOD.INI", out v));
        Assert.Equal("Orrin", v);
        Assert.False(ini.ReadStrFromIni("missing", "Main", @"Runtime\mod.ini", out v));
        Assert.Equal("", v);
    }

    [Fact]
    public void Saving_writes_under_the_write_root_only_sorted()
    {
        var ini = Ini();
        Assert.True(ini.WriteStrToIni("Alpha", "1", "Main", @"Runtime\mod.ini"));
        Assert.False(ini.WriteStrToIni("bad;key", "1", "Main", @"Runtime\mod.ini"));
        Assert.True(ini.SaveIni(@"Runtime\mod.ini"));
        string saved = File.ReadAllText(Path.Combine(write, "Runtime", "mod.ini"));
        Assert.Equal("[Main]\r\nAlpha=1\r\nGold=500\r\nName=Orrin\r\n[Other]\r\nx=1\r\n", saved);
        Assert.DoesNotContain("Alpha", File.ReadAllText(Path.Combine(game, "Runtime", "mod.ini"))); // the ERA folder is untouched

        var again = Ini();                                   // a new process reads the saved copy first
        Assert.True(again.ReadStrFromIni("alpha", "main", @"Runtime\mod.ini", out var v));
        Assert.Equal("1", v);
    }

    [Fact]
    public void A_section_without_closing_bracket_makes_the_file_empty()
    {
        File.WriteAllText(Path.Combine(game, "bad.ini"), "[ok]\r\na=1\r\n[broken\r\nb=2\r\n");
        var ini = Ini();
        Assert.False(ini.LoadIni("bad.ini"));
        Assert.False(ini.ReadStrFromIni("a", "ok", "bad.ini", out _));
    }

    [Fact]
    public void Erm_reads_and_writes_ini_through_sn_f()
    {
        using var t = new EraTestHost().Script("t.erm", "ZVSE2\n!?FU(Go);\n" +
            "!!SN:F^ReadStrFromIni^/^gold^/^Main^/^Runtime\\mod.ini^/?z1;\n" +
            "!!VRv2:Sv1;\n" +
            "!!SN:F^WriteStrToIni^/^Count^/^7^/^New^/^Runtime\\mod.ini^;\n" +
            "!!SN:F^ReadStrFromIni^/^count^/^new^/^Runtime\\mod.ini^/?z2;\n" +
            "!!SN:F^ReadStrFromIni^/^none^/^Main^/^Runtime\\mod.ini^/?z3;\n" +
            "!!VRv3:Sv1;\n").Start();
        t.Host.SetEraFolders(game, write);
        t.Call("Go");
        Assert.Equal("500", t.Z(1));
        Assert.Equal(1, t.V(2));
        Assert.Equal("7", t.Z(2));
        Assert.Equal("", t.Z(3));
        Assert.Equal(0, t.V(3));
        Assert.Equal(0, t.Errors);
    }
}
