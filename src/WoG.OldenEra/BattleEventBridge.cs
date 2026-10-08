using System;
using System.Linq;
using System.Reflection;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using WoG.Core.Events;

namespace WoG.OldenEra;

/// <summary>Subscribing managed code to the events of a game object (fields of Il2Cpp EventHandler&lt;T&gt;).</summary>
internal static class Il2CppEvents
{
    sealed class Sink
    {
        public Action<object> Handler = _ => { };
        public void Handle(object sender, object e) => Handler(e);
    }

    /// <summary>Adds <paramref name="handler"/> (called with the event's arguments) to the event field <paramref name="field"/>.</summary>
    public static void Subscribe(object owner, string field, Action<object> handler)
    {
        var prop = owner.GetType().GetProperty(field, BindingFlags.Public | BindingFlags.Instance)
                   ?? throw new InvalidOperationException($"{owner.GetType().Name}.{field} not found");
        var handlerType = prop.PropertyType;
        var ps = handlerType.GetMethod("Invoke")!.GetParameters().Select(p => p.ParameterType).ToArray();
        var sink = new Sink { Handler = handler };
        var managed = Delegate.CreateDelegate(typeof(Action<,>).MakeGenericType(ps), sink, typeof(Sink).GetMethod(nameof(Sink.Handle))!);
        var il2cpp = typeof(DelegateSupport).GetMethods().First(m => m.Name == "ConvertDelegate" && m.IsGenericMethod)
            .MakeGenericMethod(handlerType).Invoke(null, new object[] { managed })!;
        var current = prop.GetValue(owner);
        if (current != null)
        {
            var delegateType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Il2CppSystem.Delegate")).First(t => t != null)!;
            var combined = delegateType.GetMethod("Combine", new[] { delegateType, delegateType })!.Invoke(null, new[] { current, il2cpp })!;
            il2cpp = typeof(Il2CppObjectBase).GetMethod("Cast")!.MakeGenericMethod(handlerType).Invoke(combined, null)!;
        }
        prop.SetValue(owner, il2cpp);
    }
}

/// <summary>
/// The battle triggers. Olden Era's battle logic raises its events on a bus (eor.clgz.cksd: fields of
/// EventHandler&lt;BattleEventArgs&gt;) [V-game, WoG Debug "battleevents" in a manual battle]: a round starts (ckso,
/// roundNumber = 1, 2…), a unit's turn starts (cksj — every turn, also the one after waiting; cksi only a fresh one)
/// and ends (cksn), it starts moving (cktd), uses an ability or attack (ckts … cktt, CastAbilityEventArgs: caster,
/// abilityID, attackedObjects; a retaliation too), waits (cksw), skips its turn (cksv — Olden Era's "skip turn" is
/// H3's defend), a hero casts a spell during a unit's turn (cktn … cktm, CastMagicEventArgs: casterSide,
/// attackedObjects), a unit took damage (cksq, ObjectTookDamageEventArgs: damage, stacksDestroyed, target).
/// Each new battle is subscribed to when its logic appears (checked every frame, before its first round);
/// <see cref="BattleActionTracker"/> turns the events into !?BR, !?BG0/1 and !?MF1.
/// </summary>
internal static class BattleEventBridge
{
    static IntPtr subscribed;
    static BattleActionTracker? tracker;

    static readonly (string Key, Action<object> Handler)[] Events =
    {
        ("battleevent.roundStart", RoundStart),
        ("battleevent.turnStart", e => T.TurnStart(Stack(Target(e)))),
        ("battleevent.turnEnd", e => T.TurnEnd(Stack(Target(e)))),
        ("battleevent.move", e => T.Move(Stack(Target(e)))),
        ("battleevent.cast", Cast),
        ("battleevent.castEnd", _ => T.AttackEnd()),
        ("battleevent.defend", e => T.Defend(Stack(Target(e)))),
        ("battleevent.wait", e => T.Wait(Stack(Target(e)))),
        ("battleevent.magicStart", e => T.HeroCastStart(FirstAttacked(e))),
        ("battleevent.magicEnd", _ => T.HeroCastEnd()),
        ("battleevent.damage", e => T.Damage(Stack(Target(e)), Convert.ToInt32(OldenEraSymbols.ReadMember(e, "damage")))),
    };

    /// <summary>Every frame: subscribes to the events of a battle that has just begun.</summary>
    public static void Poll()
    {
        var sym = WoGPlugin.Symbols;
        if (sym == null || WoGPlugin.Adapter == null || !sym.Has("battle.logic") || !sym.Has("battle.events")) return;
        if (sym.Read("battle.logic", null) is not Il2CppObjectBase logic) { subscribed = IntPtr.Zero; return; }
        if (logic.Pointer == subscribed) return;
        subscribed = logic.Pointer;
        tracker = new BattleActionTracker(() => A.CurrentBattle, Raise);
        try
        {
            var bus = MethodTrace.Real(sym.Read("battle.events", MethodTrace.Real(logic))!);
            int n = 0;
            foreach (var (key, handler) in Events)
                if (sym.Has(key))
                {
                    Il2CppEvents.Subscribe(bus, sym.MemberOf(key)!.Name, e => Safe(key, handler, e));
                    n++;
                }
            WoGPlugin.L?.LogInfo($"WoG: battle triggers on ({n} battle events)");
        }
        catch (Exception ex) { WoGPlugin.L?.LogError("WoG: battle triggers failed: " + ex); }
    }

    static void Safe(string key, Action<object> handler, object e)
    {
        try { handler(MethodTrace.Real(e)); }
        catch (Exception ex) { WoGPlugin.L?.LogError($"WoG: battle event {key} failed: " + ex); }
    }

    static OldenEraGameAdapter A => WoGPlugin.Adapter!;
    static BattleActionTracker T => tracker!;
    static object? Target(object e) => OldenEraSymbols.ReadMember(e, "target") is { } t ? MethodTrace.Real(t) : null;
    static int Stack(object? unit) => A.StackNumberOf(unit);

    static int FirstAttacked(object e)
    {
        var attacked = OldenEraSymbols.Items(OldenEraSymbols.ReadMember(e, "attackedObjects")).FirstOrDefault(x => x != null);
        return attacked == null ? -1 : Stack(MethodTrace.Real(attacked));
    }

    static void Raise(WoGEventKind kind, int arg)
    {
        var b = A.CurrentBattle;
        if (kind == WoGEventKind.BattleRound) WoGPlugin.L?.LogInfo($"WoG: battle round {arg}");
        else if (kind == WoGEventKind.BattleActionPre && b != null)
            WoGPlugin.L?.LogInfo($"WoG: battle action {b.ActionType} of stack {b.ActionStack} (side {b.ActionSide}), target {b.ActionTarget}");
        else if (kind == WoGEventKind.BattleDamage && b != null)
            WoGPlugin.L?.LogInfo($"WoG: battle damage {b.Damage} to stack {b.DamageStack}");
        WoGPlugin.Host?.Events.Raise(new WoGEvent
        {
            Kind = kind, Arg = arg, Player = b?.Owners[0] ?? -1, Hero = b?.Heroes[0] ?? -1, Position = b?.Position ?? Core.Model.MapPos.None,
        });
    }

    // OE's round number is 1 at the first round; WoG's v997 is 0 when the battle starts, then 1, 2…
    static void RoundStart(object e) => T.RoundStart(Convert.ToInt32(OldenEraSymbols.ReadMember(e, "roundNumber")) - 1);

    // a unit's attack or ability: shot 7, melee 6 (a retaliation too), anything else 10
    static void Cast(object e)
    {
        var caster = OldenEraSymbols.ReadMember(e, "caster") is { } c ? MethodTrace.Real(c) : null;
        int stack = Stack(caster);
        T.Attack(stack, stack < 0 ? -1 : A.AttackKind(caster, OldenEraSymbols.ReadMember(e, "abilityID")), FirstAttacked(e));
    }
}
