using System;
using WoG.Core.Model;

namespace WoG.Core.Events;

/// <summary>
/// Turns the battle events of an engine into the WoG battle triggers (Monsters.cpp: the battle manager's action
/// before and after it is done, ApplyDamage): !?BR at each round start (v997 = the round, 0 when the battle starts),
/// !?BG0 once per action of a stack and !?BG1 after it, !?BG0/1 around a hero's spell (BG:A 1), !?MF1 when a stack's
/// shot or melee attack (a retaliation too) deals damage — not a spell's damage (WoG hooks ApplyDamage only on the
/// physical attack paths, _B1.cpp).
/// An engine that reports an action only as it happens tells a walk by its movement, which may still end in an attack:
/// a walk is reported at the attack or, without one, when the turn ends.
/// Stacks are WoG stack numbers (0..20 the attacker's, 21..41 the defender's), -1 when unknown.
/// </summary>
public sealed class BattleActionTracker
{
    readonly Func<WoGBattle?> battle;
    readonly Action<WoGEventKind, int> raise;
    int acting = -1;      // the stack whose turn it is
    bool raised;          // !?BG0 of the stack's action was raised this turn
    bool moved;           // the stack moved and has not acted yet
    bool heroCasting;     // between the start and the end of a hero's spell
    bool attacking;       // between the start and the end of a stack's shot or melee attack

    public BattleActionTracker(Func<WoGBattle?> battle, Action<WoGEventKind, int> raise)
    {
        this.battle = battle;
        this.raise = raise;
    }

    /// <summary>A round starts; <paramref name="round"/> is WoG's (0 at the start of the battle).</summary>
    public void RoundStart(int round)
    {
        if (battle() is { } b) b.Round = round;
        raise(WoGEventKind.BattleRound, round);
    }

    /// <summary>The turn of a stack starts (again after it waited).</summary>
    public void TurnStart(int stack)
    {
        acting = stack;
        raised = moved = heroCasting = attacking = false;
        if (battle() is not { } b) return;
        b.ActionStack = stack;
        b.ActionSide = stack < 0 ? -1 : stack / 21;
        b.ActionType = -1;
        b.ActionTarget = -1;
    }

    /// <summary>A stack starts moving.</summary>
    public void Move(int stack)
    {
        if (stack >= 0 && stack == acting && !raised) moved = true;
    }

    /// <summary>
    /// A stack uses an attack or ability: <paramref name="kind"/> is its BG:A (6 melee, 7 shot, 10 an ability),
    /// <paramref name="target"/> the first stack it hits. Retaliations of other stacks are not actions.
    /// </summary>
    public void Attack(int stack, int kind, int target)
    {
        attacking = stack >= 0 && kind is 6 or 7;
        if (stack >= 0 && stack == acting && !raised) Act(kind, target);
    }

    /// <summary>A stack's attack or ability ends.</summary>
    public void AttackEnd() => attacking = false;

    /// <summary>The acting stack defends (skips its turn).</summary>
    public void Defend(int stack)
    {
        if (stack >= 0 && stack == acting && !raised) Act(3, -1);
    }

    /// <summary>The acting stack waits.</summary>
    public void Wait(int stack)
    {
        if (stack >= 0 && stack == acting && !raised) Act(8, -1);
    }

    /// <summary>A hero casts a spell during the turn of a stack of its side (BG:A 1; the stack acts after it).</summary>
    public void HeroCastStart(int target)
    {
        if (heroCasting || battle() is not { } b) return;
        heroCasting = true;
        attacking = false;
        b.ActionStack = acting;
        b.ActionSide = acting < 0 ? -1 : acting / 21;
        b.ActionType = 1;
        b.ActionTarget = target;
        raise(WoGEventKind.BattleActionPre, 0);
    }

    public void HeroCastEnd()
    {
        if (!heroCasting) return;
        heroCasting = false;
        raise(WoGEventKind.BattleActionPost, 0);
        if (battle() is not { } b) return;
        b.ActionType = -1;
        b.ActionTarget = -1;
    }

    /// <summary>A stack took <paramref name="damage"/>; only the damage of a stack's attack raises !?MF1.</summary>
    public void Damage(int stack, int damage)
    {
        if (!attacking || stack < 0 || battle() is not { } b) return;
        b.DamageStack = stack;
        b.Damage = damage;
        b.DamageDealt = true;
        raise(WoGEventKind.BattleDamage, 0);
        b.DamageStack = -1;
    }

    /// <summary>The turn of a stack ends: its action is reported if nothing told it yet (a walk, or no action), then !?BG1.</summary>
    public void TurnEnd(int stack)
    {
        if (stack < 0 || stack != acting) return;
        if (!raised) Act(moved ? 2 : 12, -1);
        raise(WoGEventKind.BattleActionPost, 0);
        acting = -1;
        raised = moved = attacking = false;
    }

    void Act(int kind, int target)
    {
        raised = true;
        moved = false;
        if (battle() is not { } b) return;
        b.ActionStack = acting;
        b.ActionSide = acting / 21;
        b.ActionType = kind;
        b.ActionTarget = target;
        raise(WoGEventKind.BattleActionPre, 0);
    }
}
