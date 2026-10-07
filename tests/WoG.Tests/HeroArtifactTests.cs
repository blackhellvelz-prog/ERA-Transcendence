using Xunit;

namespace WoG.Tests;

/// <summary>HE:A — hero artifacts by position: 0..18 worn, 19..82 backpack (WoG erm.cpp, HE Cmd=='A').</summary>
public class HeroArtifactTests
{
    static TestHost WithHero(string body)
    {
        var t = new TestHost().Load(body);
        t.Game.AddHero(0, owner: 0);
        // Format P2 for HE:A4: rings (7), misc (9), head (1)
        t.Game.ArtifactPosition = a => a switch { 37 or 45 => 7, 53 => 9, 22 => 1, _ => 0 };
        return t.Start();
    }

    [Fact]
    public void Backpack_worn_positions_counting_and_checking()
    {
        var t = WithHero(
            "!?FU1;\n" +
            "!!HE0:A53 A53;\n" +                 // two spyglasses into the backpack (19, 20)
            "!!HE0:A1/53/9;\n" +                 // a third one worn in misc 1
            "!!VRv1:S0; !!HE0:A=53; !!VRv1&1:S1;\n" +
            "!!VRv2:S0; !!HE0:A=22; !!VRv2&1:S1;\n" +
            "!!HE0:A2/53/?v3/?v4;\n" +           // 3 in all, 1 worn
            "!!HE0:A1/?v5/9 A1/?v6/20 A1/?v7/21;\n" +
            "!!HE0:A1/22/9; !!VRv8:S0; !!VRv8&1:S1;\n" + // misc 1 is taken: flag 0
            "!!HE0:A3/53/1/0 A2/53/?v9/?v10;\n" + // the backpack goes first
            "!!HE0:A-53 A2/53/?v11;\n");
        t.Call(1);
        Assert.Equal(0, t.ErrorCount);
        Assert.Equal(new[] { 1, 0, 3, 1 }, new[] { t.V(1), t.V(2), t.V(3), t.V(4) });
        Assert.Equal(new[] { 53, 53, -1 }, new[] { t.V(5), t.V(6), t.V(7) });
        Assert.Equal(0, t.V(8));
        Assert.Equal(new[] { 2, 1, 0 }, new[] { t.V(9), t.V(10), t.V(11) });
    }

    [Fact]
    public void A3_with_worn_set_removes_worn_copies_first()
    {
        var t = WithHero(
            "!?FU1;\n" +
            "!!HE0:A53 A1/53/9 A1/53/10;\n" +
            "!!HE0:A3/53/2/1 A2/53/?v1/?v2;\n");
        t.Call(1);
        Assert.Equal(0, t.ErrorCount);
        Assert.Equal(1, t.V(1));
        Assert.Equal(0, t.V(2));
    }

    [Fact]
    public void A4_wears_in_the_first_suitable_slot_then_the_backpack()
    {
        var t = WithHero(
            "!?FU1;\n" +
            "!!HE0:A4/37 A4/45 A4/37;\n" +       // right ring, left ring, then the backpack
            "!!HE0:A1/?v1/6 A1/?v2/7 A1/?v3/19;\n" +
            "!!HE0:A4/1000 A1/?v4/17;\n");       // the spellbook (artifact 0) reads as 1000
        t.Game.ArtifactPosition = a => a switch { 37 or 45 => 7, 0 => 14, _ => 0 };
        t.Call(1);
        Assert.Equal(0, t.ErrorCount);
        Assert.Equal(new[] { 37, 45, 37, 1000 }, new[] { t.V(1), t.V(2), t.V(3), t.V(4) });
    }

    [Fact]
    public void Scrolls_are_1001_plus_the_spell_and_A_minus_1_removes_all_of_them()
    {
        var t = WithHero(
            "!?FU1;\n" +
            "!!HE0:A1018 A1054 A53;\n" +         // Lightning Bolt and Slow scrolls, a spyglass
            "!!HE0:A1/?v1/19;\n" +
            "!!HE0:A-1 A1/?v2/19 A1/?v3/20 A1/?v4/21;\n");
        t.Call(1);
        Assert.Equal(0, t.ErrorCount);
        Assert.Equal(1018, t.V(1));
        Assert.Equal(new[] { -1, -1, 53 }, new[] { t.V(2), t.V(3), t.V(4) });
    }

    [Fact]
    public void Wrong_positions_and_commands_are_errors()
    {
        var t = WithHero(
            "!?FU1;\n!!HE0:A1/53/83;\n" +
            "!?FU2;\n!!HE0:A9/53;\n" +
            "!?FU3;\n!!HE0:A?v1;\n");
        t.Call(1);
        Assert.Equal(1, t.ErrorCount);
        t.Call(2);
        Assert.Equal(2, t.ErrorCount);
        t.Call(3);
        Assert.Equal(3, t.ErrorCount);
    }
}
