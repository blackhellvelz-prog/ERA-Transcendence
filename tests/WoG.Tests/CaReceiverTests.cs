using WoG.Core.Model;
using Xunit;

namespace WoG.Tests;

/// <summary>!!CA — a town (casdem.cpp ERM_Castle) on the headless engine (H3's _CastleSetup_).</summary>
public class CaReceiverTests
{
    static TestHost Towns(string body)
    {
        var t = new TestHost().Load(body);
        t.Game.AddTown(0, 4, new MapPos(5, 6, 0), owner: 1).Name = "Тест";
        t.Game.AddTown(1, 0, new MapPos(10, 3, 1));
        return t.Start();
    }

    [Fact]
    public void Selectors_number_position_v_index_and_current_town()
    {
        var t = Towns(
            "!?FU1;\n" +
            "!!CA0/1:T?v1 U?v2;\n" +
            "!!CA5/6/0:T?v3 U?v4 P?v5/?v6/?v7;\n" +
            "!!VRv20:S10; !!VRv21:S3; !!VRv22:S1;\n" +
            "!!CA20:U?v8;\n" +
            "!!CA-1:U?v9;\n");
        t.Game.CurrentTownId = 1;
        t.Call(1);
        Assert.Equal(0, t.ErrorCount);
        Assert.Equal(new[] { 0, 1, 4, 0, 5, 6, 0, 1, 1 }, new[] { t.V(1), t.V(2), t.V(3), t.V(4), t.V(5), t.V(6), t.V(7), t.V(8), t.V(9) });
    }

    [Theory]
    [InlineData("!!CA0/2:U?v1;")]   // no town 2
    [InlineData("!!CA1/0:U?v1;")]   // the first number must be 0
    [InlineData("!!CA1/1/0:U?v1;")] // not a town
    [InlineData("!!CA0/0:B3/44;")]  // buildings 0..43
    [InlineData("!!CA0/0:M1/7/?v1/?v2;")]
    public void Wrong_selectors_and_numbers_are_errors(string line)
    {
        var t = Towns("!?FU1;\n" + line + "\n");
        t.Call(1);
        Assert.Equal(1, t.ErrorCount);
    }

    [Fact]
    public void B1_builds_B2_destroys_down_to_what_it_upgraded_B3_checks()
    {
        var t = Towns(
            "!?FU1;\n" +
            "!!CA0/0:B1/8 B3/8; !!VRv1&1:S1;\n" +
            "!!CA0/0:B3/7; !!VRv2&-1:S1;\n" +        // B1 sets only its own bit
            "!!CA0/0:B2/8 B3/7; !!VRv3&1:S1;\n" +   // destroying the citadel leaves a fort
            "!!CA0/0:B3/8; !!VRv4&-1:S1;\n" +
            "!!CA0/0:B3/7/1; !!VRv5&1:S1;\n" +      // with its bonus
            "!!CA0/0:B2/30 B3/30; !!VRv6&-1:S1;\n");
        t.Call(1);
        Assert.Equal(0, t.ErrorCount);
        Assert.Equal(new[] { 1, 1, 1, 1, 1, 1 }, new[] { t.V(1), t.V(2), t.V(3), t.V(4), t.V(5), t.V(6) });
    }

    [Fact]
    public void B4_B5_allow_and_forbid_B6_builds_an_upgrade_with_what_it_needs_and_keeps_R()
    {
        var t = Towns(
            "!?FU1;\n" +
            "!!CA0/0:B5/16 B3/16/2; !!VRv1&-1:S1;\n" +
            "!!CA0/0:B4/16 B3/16/2; !!VRv2&1:S1;\n" +
            "!!CA0/0:R1 B6/9 R?v3 G?v4;\n" +
            "!!CA0/0:B3/7; !!VRv5&1:S1;\n" +
            "!!CA0/0:B3/8; !!VRv6&1:S1;\n" +
            "!!CA0/0:B6/2 G?v7;\n");
        t.Call(1);
        Assert.Equal(0, t.ErrorCount);
        Assert.Equal(new[] { 1, 1, 1, 0, 1, 1, 3 }, new[] { t.V(1), t.V(2), t.V(3), t.V(4), t.V(5), t.V(6), t.V(7) });
    }

    [Fact]
    public void M1_rows_M2_garrison_M4_growth_is_read_only()
    {
        var t = Towns(
            "!?FU1;\n" +
            "!!CA0/0:M1/3/7/2 M1/3/d3/d M1/3/?v1/?v2;\n" +
            "!!CA0/0:M2/0/13/20 M2/0/?v3/?v4 M2/0/d/d5 M2/0/?v5/?v6;\n" +
            "!!CA0/0:M2/0/-1/0 M2/0/?v7/?v8;\n" +
            "!!CA0/0:M4/2/99 M4/2/?v9;\n");
        t.Game.TownList[0].Growth[2] = 4;
        t.Call(1);
        Assert.Equal(0, t.ErrorCount);
        Assert.Equal(new[] { 10, 2, 13, 20, 13, 25, -1, 0, 4 },
            new[] { t.V(1), t.V(2), t.V(3), t.V(4), t.V(5), t.V(6), t.V(7), t.V(8), t.V(9) });
    }

    [Fact]
    public void Owner_name_heroes_guild_spells_and_income()
    {
        var t = Towns(
            "!?FU1;\n" +
            "!!CA0/0:O?v1 O3 O?v2;\n" +
            "!!CA0/0:N?z1 N^Новый^ N?z2;\n" +
            "!!CA0/0:H0/?v3 H1/7 H1/?v4;\n" +
            "!!CA0/0:G3 G?v5 G1/4/2 G1/4/?v6 G1/2/17 G1/2/?v7 G2/5 G2/?v10;\n" +
            "!!CA0/0:S?v8 S500 S?v9;\n");
        t.Game.AddHero(7, 3);
        t.Game.TownList[0].Income = 1500;
        t.Call(1);
        Assert.Equal(0, t.ErrorCount);
        Assert.Equal(new[] { 1, 3, -1, 7, 3, 2, 17, 1500, 1500 },
            new[] { t.V(1), t.V(2), t.V(3), t.V(4), t.V(5), t.V(6), t.V(7), t.V(8), t.V(9) });
        Assert.Equal(5, t.V(10));
        Assert.Equal("Тест", t.Z(1));
        Assert.Equal("Новый", t.Z(2));
        Assert.Equal(new MapPos(5, 6, 0), t.Game.HeroList[7].Position);
        Assert.Equal(3, t.Game.Objects[new MapPos(5, 6, 0).Pack()].Owner);
    }

    [Fact]
    public void Moving_a_town_changing_its_type_and_the_portal_are_reported_not_faked()
    {
        var t = Towns("!?FU1;\n!!CA0/0:T5;\n!!CA0/0:P1/1/0;\n!!CA0/0:M3/1/1;\n!!CA0/0:T4 Td I0;\n!!VRv1:S1;\n");
        t.Call(1);
        Assert.Equal(0, t.ErrorCount);
        Assert.Equal(1, t.V(1));
        Assert.Equal(4, t.Game.TownList[0].Type);
        Assert.Contains(t.Host.Compat.Entries, e => e.Item == "!!CA:T");
        Assert.Contains(t.Host.Compat.Entries, e => e.Item == "!!CA:P");
        Assert.Contains(t.Host.Compat.Entries, e => e.Item == "!!CA:M");
    }
}
