using WoG.Core.Events;
using WoG.Core.Model;
using Xunit;

namespace WoG.Tests;

/// <summary>The battle triggers !?BR, !?BG0/1, !?MF1 and the receivers BG and MF.</summary>
public class BattleTriggerTests
{
    static TestHost Battle(string body)
    {
        var t = new TestHost().Load(body);
        t.Game.CurrentBattle = new WoGBattle
        {
            Heroes = new[] { 5, -1 },
            Stacks =
            {
                new WoGBattleStack { Index = 1, Side = 0, Type = 2, Count = 10 },
                new WoGBattleStack { Index = 22, Side = 1, Type = 1000, Count = 8 },
            },
        };
        return t.Start();
    }

    [Fact]
    public void BR_gets_the_round_in_v997()
    {
        var t = Battle("!?BR;\n!!VRv1:+1;\n!!VRv2:Sv997;\n");
        t.Host.Events.Raise(new WoGEvent { Kind = WoGEventKind.BattleRound, Arg = 0 });
        t.Host.Events.Raise(new WoGEvent { Kind = WoGEventKind.BattleRound, Arg = 1 });
        Assert.Equal(0, t.ErrorCount);
        Assert.Equal(2, t.V(1));
        Assert.Equal(1, t.V(2));
    }

    [Fact]
    public void BG_reads_the_action_of_the_stack()
    {
        var t = Battle("!?BG0;\n!!BG:A?v1 N?v2 Q?v3 H?v4 E?v5;\n!?BG1;\n!!VRv6:S1;\n");
        var b = t.Game.CurrentBattle!;
        b.ActionStack = 1; b.ActionType = 7; b.ActionTarget = 22;
        t.Host.Events.Raise(new WoGEvent { Kind = WoGEventKind.BattleActionPre });
        t.Host.Events.Raise(new WoGEvent { Kind = WoGEventKind.BattleActionPost });
        Assert.Equal(0, t.ErrorCount);
        Assert.Equal(new[] { 7, 1, 0, 5, 22, 1 }, new[] { t.V(1), t.V(2), t.V(3), t.V(4), t.V(5), t.V(6) });
    }

    [Fact]
    public void MF_reads_the_damage_and_reports_changing_it()
    {
        var t = Battle("!?MF1;\n!!MF:D?v1 N?v2 F?v3 E?v4;\n!!MF:F0;\n");
        var b = t.Game.CurrentBattle!;
        b.DamageStack = 22; b.Damage = 16; b.DamageDealt = true;
        t.Host.Events.Raise(new WoGEvent { Kind = WoGEventKind.BattleDamage });
        Assert.Equal(0, t.ErrorCount);
        Assert.Equal(new[] { 16, 22, 16, 1 }, new[] { t.V(1), t.V(2), t.V(3), t.V(4) });
        Assert.Contains(t.Host.Compat.Entries, e => e.Item == "!!MF:F");
    }

    // v1 = !?BG0 count, v2 = BG:A, v3 = BG:N, v4 = BG:E, v5 = BG:Q, v6 = !?BG1 count, v7 = !?MF1 count, v8 = MF:D, v9 = MF:N
    const string Recorder =
        "!?BG0;\n!!VRv1:+1;\n!!BG:A?v2 N?v3 E?v4 Q?v5;\n!?BG1;\n!!VRv6:+1;\n!?MF1;\n!!VRv7:+1;\n!!MF:D?v8 N?v9;\n";

    static (TestHost T, BattleActionTracker A) Tracked()
    {
        var t = Battle(Recorder);
        var a = new BattleActionTracker(() => t.Game.CurrentBattle, (k, arg) => t.Host.Events.Raise(new WoGEvent { Kind = k, Arg = arg }));
        return (t, a);
    }

    static int[] Seen(TestHost t) => new[] { t.V(1), t.V(2), t.V(3), t.V(4), t.V(5), t.V(6) };

    [Fact]
    public void A_walk_that_ends_in_an_attack_is_an_attack()
    {
        var (t, a) = Tracked();
        a.TurnStart(1);
        a.Move(1);
        Assert.Equal(0, t.V(1));
        a.Attack(1, 6, 22);
        a.Damage(22, 40);
        a.AttackEnd();
        a.Attack(22, 6, 1); // the retaliation is not an action, its damage is physical
        a.Damage(1, 9);
        a.AttackEnd();
        a.TurnEnd(1);
        Assert.Equal(0, t.ErrorCount);
        Assert.Equal(new[] { 1, 6, 1, 22, 0, 1 }, Seen(t));
        Assert.Equal(new[] { 2, 9, 1 }, new[] { t.V(7), t.V(8), t.V(9) });
    }

    [Fact]
    public void A_walk_is_reported_when_the_turn_ends()
    {
        var (t, a) = Tracked();
        a.TurnStart(22);
        a.Move(22);
        a.TurnEnd(22);
        Assert.Equal(0, t.ErrorCount);
        Assert.Equal(new[] { 1, 2, 22, -1, 1, 1 }, Seen(t));
    }

    [Fact]
    public void Waiting_defending_and_a_turn_without_action()
    {
        var (t, a) = Tracked();
        a.TurnStart(1);
        a.Wait(1);
        a.TurnEnd(1);
        Assert.Equal(new[] { 1, 8, 1, -1, 0, 1 }, Seen(t));
        a.TurnStart(22);
        a.Defend(22);
        a.TurnEnd(22);
        Assert.Equal(new[] { 2, 3, 22, -1, 1, 2 }, Seen(t));
        a.TurnStart(1); // after waiting
        a.TurnEnd(1);
        Assert.Equal(0, t.ErrorCount);
        Assert.Equal(new[] { 3, 12, 1, -1, 0, 3 }, Seen(t));
    }

    [Fact]
    public void A_hero_spell_is_its_own_action_and_not_physical_damage()
    {
        var (t, a) = Tracked();
        a.TurnStart(1);
        a.HeroCastStart(22);
        a.Damage(22, 30);
        Assert.Equal(new[] { 1, 1, 1, 22, 0, 0 }, Seen(t));
        a.HeroCastEnd();
        Assert.Equal(1, t.V(6));
        a.Attack(1, 7, 22);
        a.Damage(22, 12);
        a.AttackEnd();
        a.TurnEnd(1);
        Assert.Equal(0, t.ErrorCount);
        Assert.Equal(new[] { 2, 7, 1, 22, 0, 2 }, Seen(t));
        Assert.Equal(new[] { 1, 12, 22 }, new[] { t.V(7), t.V(8), t.V(9) });
    }

    [Fact]
    public void An_ability_damage_is_not_physical()
    {
        var (t, a) = Tracked();
        a.TurnStart(1);
        a.Attack(1, 10, 22);
        a.Damage(22, 30);
        a.AttackEnd();
        a.TurnEnd(1);
        Assert.Equal(0, t.ErrorCount);
        Assert.Equal(new[] { 1, 10, 1, 22, 0, 1 }, Seen(t));
        Assert.Equal(0, t.V(7));
    }

    [Fact]
    public void The_round_goes_to_v997_and_the_battle()
    {
        var t = Battle("!?BR;\n!!VRv1:Sv997;\n");
        var a = new BattleActionTracker(() => t.Game.CurrentBattle, (k, arg) => t.Host.Events.Raise(new WoGEvent { Kind = k, Arg = arg }));
        a.RoundStart(0);
        a.RoundStart(1);
        Assert.Equal(1, t.V(1));
        Assert.Equal(1, t.Game.CurrentBattle!.Round);
    }

    [Fact]
    public void BG_outside_an_action_is_an_error()
    {
        var t = Battle("!?FU1;\n!!BG:A?v1;\n");
        t.Call(1);
        Assert.Equal(1, t.ErrorCount);
    }
}
