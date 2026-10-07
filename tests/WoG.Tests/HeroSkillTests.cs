using Xunit;

namespace WoG.Tests;

/// <summary>HE:S — secondary skill levels and hero-screen slots (WoG erm.cpp, Cmd=='S').</summary>
public class HeroSkillTests
{
    static TestHost WithHero(string body)
    {
        var t = new TestHost().Load(body);
        t.Game.AddHero(0, owner: 0);
        return t.Start();
    }

    [Fact]
    public void Learned_skills_take_the_next_slot_and_a_forgotten_one_gives_its_slot_to_the_last()
    {
        var t = WithHero(
            "!?FU1;\n" +
            "!!HE0:S2/1 S9/2 S6/3;\n" +   // logistics, luck, leadership: slots 1, 2, 3
            "!!HE0:S?v1;\n" +             // shown skills
            "!!HE0:S?v2/9/1;\n" +         // the slot of luck
            "!!HE0:S3/?v3/1;\n" +         // the skill in slot 3
            "!!HE0:S2/0;\n" +             // forget logistics: leadership moves from slot 3 to slot 1
            "!!HE0:S1/?v4/1 S?v5 S9/?v6 S2/?v7;\n");
        t.Call(1);
        Assert.Equal(0, t.ErrorCount);
        Assert.Equal(3, t.V(1));
        Assert.Equal(2, t.V(2));
        Assert.Equal(6, t.V(3));
        Assert.Equal(6, t.V(4));
        Assert.Equal(2, t.V(5));
        Assert.Equal(2, t.V(6));
        Assert.Equal(0, t.V(7));
    }

    [Fact]
    public void Setting_a_slot_moves_the_skill_and_hides_the_one_that_held_it()
    {
        var t = WithHero(
            "!?FU1;\n" +
            "!!HE0:S2/1 S9/2 S6/3;\n" +
            "!!HE0:S1/6/1;\n" +           // leadership into slot 1; logistics loses it
            "!!HE0:S?v1/6/1 S?v2/2/1 S?v3/9/1 S?v4;\n");
        t.Call(1);
        Assert.Equal(0, t.ErrorCount);
        Assert.Equal(1, t.V(1));
        Assert.Equal(0, t.V(2));
        Assert.Equal(2, t.V(3)); // luck keeps slot 2
        Assert.Equal(2, t.V(4));
    }

    [Fact]
    public void Only_eight_skills_are_shown()
    {
        var t = WithHero(
            "!?FU1;\n" +
            "!!DO2/0/8/1:P;\n" +          // nine skills: 0..8
            "!!HE0:S?v1 S?v2/8/1 S?v3/0/1;\n" +
            "!?FU2;\n" +
            "!!HE0:Sx16/1;\n");
        t.Call(1);
        Assert.Equal(0, t.ErrorCount);
        Assert.Equal(8, t.V(1));
        Assert.Equal(0, t.V(2));  // the ninth is learned but not shown
        Assert.Equal(1, t.V(3));
    }

    [Fact]
    public void Wrong_skill_or_slot_numbers_are_errors()
    {
        var t = WithHero(
            "!?FU1;\n!!HE0:S28/1;\n" +
            "!?FU2;\n!!HE0:S9/?v1/1;\n" +
            "!?FU3;\n!!HE0:S?v1/?v2/1;\n");
        t.Call(1);
        Assert.Equal(1, t.ErrorCount);
        t.Call(2);
        Assert.Equal(2, t.ErrorCount);
        t.Call(3);
        Assert.Equal(3, t.ErrorCount);
    }
}
