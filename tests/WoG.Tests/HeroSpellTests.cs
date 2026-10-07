using Xunit;

namespace WoG.Tests;

/// <summary>HE:M — the spells a hero knows (WoG erm.cpp, HE Cmd=='M').</summary>
public class HeroSpellTests
{
    [Fact]
    public void Spells_are_learned_read_and_forgotten()
    {
        var t = new TestHost().Load(
            "!?FU1;\n" +
            "!!HE0:M17/1 M53/1;\n" +       // Lightning Bolt, Haste
            "!!HE0:M17/?v1 M21/?v2;\n" +
            "!!HE0:M17/0;\n" +
            "!!HE0:M17/?v3 M53/?v4;\n" +
            "!?FU2;\n!!HE0:M70/1;\n");      // out of range
        t.Game.AddHero(0, owner: 0);
        t.Start();
        t.Call(1);
        Assert.Equal(0, t.ErrorCount);
        Assert.Equal(new[] { 1, 0, 0, 1 }, new[] { t.V(1), t.V(2), t.V(3), t.V(4) });
        t.Call(2);
        Assert.Equal(1, t.ErrorCount);
    }
}
