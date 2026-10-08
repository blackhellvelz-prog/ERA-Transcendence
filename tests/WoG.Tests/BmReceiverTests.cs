using WoG.Core.Model;
using Xunit;

namespace WoG.Tests;

/// <summary>!!BM — a stack in battle (Monsters.cpp ERM_BRound).</summary>
public class BmReceiverTests
{
    static TestHost Battle(string body)
    {
        var t = new TestHost().Load(body);
        t.Game.CurrentBattle = new WoGBattle
        {
            Stacks =
            {
                new WoGBattleStack { Index = 0, Side = 0, Type = 4, Count = 7, CountAtStart = 7, Attack = 9, Defence = 9, HitPoints = 30, Speed = 5, DamageLow = 5, DamageHigh = 9, ArmySlot = 2 },
                new WoGBattleStack { Index = 21, Side = 1, Type = 1, Count = 9, CountAtStart = 9, Attack = 6, Defence = 5, HitPoints = 12, HitPointsLost = 4, ArmySlot = 0 },
            },
        };
        t.Game.ActiveStack = 21;
        return t.Start();
    }

    [Fact]
    public void Reads_and_sets_by_stack_number()
    {
        var t = Battle("!?FU1;\n!!BM0:T?v1 N?v2 B?v3 A?v4 D?v5 H?v6 S?v7 I?v8 O?v9 U1/?v10 U2/?v11;\n" +
                       "!!BM0:N20 Ad11 U2/12 N?v12 A?v13 U2/?v14;\n!!BM21:L?v15 I?v16;\n!!BM-1:N?v17;\n");
        t.Call(1);
        Assert.Equal(0, t.ErrorCount);
        Assert.Equal(new[] { 4, 7, 7, 9, 9, 30, 5, 0, 2, 5, 9 },
            new[] { t.V(1), t.V(2), t.V(3), t.V(4), t.V(5), t.V(6), t.V(7), t.V(8), t.V(9), t.V(10), t.V(11) });
        Assert.Equal(new[] { 20, 20, 12, 4, 1, 9 }, new[] { t.V(12), t.V(13), t.V(14), t.V(15), t.V(16), t.V(17) });
    }

    [Theory]
    [InlineData("!!BM42:N?v1;")]   // stacks -1, 0..41
    [InlineData("!!BM5:N?v1;")]    // no such stack
    [InlineData("!!BM0:U6/?v1;")]  // U1..U5
    public void Wrong_stacks_and_commands_are_errors(string line)
    {
        var t = Battle("!?FU1;\n" + line + "\n");
        t.Call(1);
        Assert.Equal(1, t.ErrorCount);
    }

    [Fact]
    public void Spells_damage_and_obstacles_are_reported_not_faked()
    {
        var t = Battle("!?FU1;\n!!BM0:K10;\n!!BM0:M53/3/2;\n!!VRv1:S1;\n");
        t.Call(1);
        Assert.Equal(0, t.ErrorCount);
        Assert.Equal(1, t.V(1));
        Assert.Contains(t.Host.Compat.Entries, e => e.Item == "!!BM:K");
        Assert.Contains(t.Host.Compat.Entries, e => e.Item == "!!BM:M");
    }
}
