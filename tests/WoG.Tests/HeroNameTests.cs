using Xunit;

namespace WoG.Tests;

/// <summary>HE:B — name, biography and class (WoG erm.cpp, HE Cmd=='B').</summary>
public class HeroNameTests
{
    static TestHost WithHero(string body)
    {
        var t = new TestHost().Load(body);
        var h = t.Game.AddHero(0, owner: 0, heroClass: 9, name: "Сандро");
        h.DefaultBiography = "Некромант.";
        return t.Start();
    }

    [Fact]
    public void B0_reads_and_sets_the_name_up_to_12_characters()
    {
        var t = WithHero("!?FU1;\n!!HE0:B0/?z1;\n!!VRz5:S^Очень длинное имя героя^;\n!!HE0:B0/z5 B0/?z2;\n");
        t.Call(1);
        Assert.Equal(0, t.ErrorCount);
        Assert.Equal("Сандро", t.Z(1));
        Assert.Equal("Очень длинно", t.Z(2));
        Assert.Equal("Очень длинно", t.Game.HeroList[0].Name);
    }

    [Fact]
    public void B1_is_empty_until_a_script_sets_it_B3_is_the_own_biography()
    {
        var t = WithHero("!?FU1;\n!!HE0:B1/?z1 B3/?z2;\n!!VRz5:S^Новая.^;\n!!HE0:B1/z5 B1/?z3 B3/?z4;\n");
        t.Call(1);
        Assert.Equal(0, t.ErrorCount);
        Assert.Equal("", t.Z(1));
        Assert.Equal("Некромант.", t.Z(2));
        Assert.Equal("Новая.", t.Z(3));
        Assert.Equal("Некромант.", t.Z(4));
    }

    [Fact]
    public void Era_takes_the_text_as_a_parameter()
    {
        using var t = new EraTestHost().Script("t.erm", "ZVSE2\n!?FU(Go);\n!!HE0:B0/^Сэр WoG^ B0/?z1 B1/^Новая.^ B1/?z2;\n");
        t.Game.AddHero(0, owner: 0, name: "Сандро");
        t.Start();
        t.Call("Go");
        Assert.Equal(0, t.Errors);
        Assert.Equal("Сэр WoG", t.Z(1));
        Assert.Equal("Новая.", t.Z(2));
    }

    [Fact]
    public void X_in_wog_sets_the_type_and_its_settings_like_MakeHeroSpec()
    {
        var t = WithHero("!?FU1;\n!!HE0:X4/24/3/2/1;\n!!HE0:X?v1/?v2/?v3/?v4/?v5/?v6/?v7;\n!!HE0:X6/16/16/17;\n!!HE0:X?v8/?v9/?v10/?v11/?v12/?v13/?v14;\n");
        t.Call(1);
        Assert.Equal(0, t.ErrorCount);
        Assert.Equal(new[] { 4, 24, 3, 2, 1, 0, 0 }, new[] { t.V(1), t.V(2), t.V(3), t.V(4), t.V(5), t.V(6), t.V(7) });
        Assert.Equal(new[] { 6, 16, 3, 2, 1, 16, 17 }, new[] { t.V(8), t.V(9), t.V(10), t.V(11), t.V(12), t.V(13), t.V(14) });
    }

    [Fact]
    public void X_in_era_applies_each_parameter_except_upgrades_and_dragons()
    {
        using var t = new EraTestHost().Script("t.erm",
            "ZVSE2\n!?FU(Go);\n!!HE0:X1/13;\n!!HE0:X?y1/?y2 X?v1/?v2/?v3/?v4/?v5/?v6/?v7;\n" +
            "!!HE0:X6/58/58/59 X?v8/?v9/d/d/d/?v10/?v11;\n!!HE0:X7/4/5 X?v12/d/?v13/?v14;\n!!VRv15:Sy1; !!VRv16:Sy2;\n");
        t.Game.AddHero(0, owner: 0);
        t.Start();
        t.Call("Go");
        Assert.Equal(0, t.Errors);
        Assert.Equal(new[] { 1, 13 }, new[] { t.V(15), t.V(16) });
        Assert.Equal(new[] { 6, 58, 58, 59 }, new[] { t.V(8), t.V(9), t.V(10), t.V(11) });
        Assert.Equal(new[] { 7, 4, 5 }, new[] { t.V(12), t.V(13), t.V(14) });
    }

    [Fact]
    public void H_sets_and_reads_the_army_of_a_newly_hired_hero()
    {
        var t = WithHero("!?FU1;\n!!HE0:H0/99/3/3 H1/-1/2/2 H2/-1/0/0;\n!!HE0:H0/?v1/?v2/?v3 H1/?v4/?v5/?v6;\n");
        t.Call(1);
        Assert.Equal(0, t.ErrorCount);
        Assert.Equal(new[] { 99, 3, 3, -1, 2, 2 }, new[] { t.V(1), t.V(2), t.V(3), t.V(4), t.V(5), t.V(6) });

        var bad = WithHero("!?FU1;\n!!HE0:H3/1/1/1;\n");
        bad.Call(1);
        Assert.Equal(1, bad.ErrorCount);
    }

    [Fact]
    public void B2_is_the_class_and_B3_takes_only_get_syntax()
    {
        var t = WithHero("!?FU1;\n!!HE0:B2/?v1 B2/8 B2/?v2;\n");
        t.Call(1);
        Assert.Equal(0, t.ErrorCount);
        Assert.Equal(9, t.V(1));
        Assert.Equal(8, t.V(2));

        var bad = WithHero("!?FU1;\n!!HE0:B3/1;\n");
        bad.Call(1);
        Assert.Equal(1, bad.ErrorCount);
    }
}
