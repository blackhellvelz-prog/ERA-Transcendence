using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;

namespace WoG.OldenEra;

/// <summary>
/// WoG Debug "battleevents": subscribes to every event of the battle's event buses (symbol battle.eventBuses — in
/// 0.81.04 eor.clgz → ena with the buses cksd enb and ckse enc, fields of EventHandler&lt;BattleEventArgs&gt;) and logs
/// which fire, with the round, the target and the arguments — to tell the round start, a unit's turn, damage… apart
/// while reverse engineering. Debug only.
/// </summary>
internal static class BattleEventProbe
{
    static readonly List<string> Log = new();

    public static string Run(string args)
    {
        var a = args.Trim();
        if (a == "show") return Log.Count == 0 ? "no battle events logged" : string.Join("\n", Log.TakeLast(120));
        if (a == "clear") { Log.Clear(); return "cleared"; }
        var sym = WoGPlugin.Symbols!;
        if (!sym.Has("battle.logic") || sym.Read("battle.logic", null) is not { } logic) return "not in battle";
        if (!sym.Has("battle.eventBuses")) return "symbol battle.eventBuses is not verified";
        var buses = MethodTrace.Real(sym.Read("battle.eventBuses", MethodTrace.Real(logic))!);
        int n = 0;
        // every bus of the container: the objects whose properties are EventHandler<…>
        foreach (var busProp in buses.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(p => p.PropertyType.GetProperties().Any(x => x.PropertyType.Name.StartsWith("EventHandler`1", StringComparison.Ordinal))))
        {
            if (busProp.GetValue(buses) is not { } busObj) continue;
            var bus = MethodTrace.Real(busObj);
            foreach (var prop in bus.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                         .Where(p => p.PropertyType.Name.StartsWith("EventHandler`1", StringComparison.Ordinal) && p.CanWrite))
            {
                string label = $"{bus.GetType().Name}.{prop.Name}";
                var handler = Il2CppHandler(prop.PropertyType, label);
                var current = prop.GetValue(bus);
                prop.SetValue(bus, current == null ? handler : Combine(current, handler, prop.PropertyType));
                n++;
            }
        }
        return $"subscribed to {n} battle events; act in the battle, then: battleevents show";
    }

    sealed class Sink
    {
        public string Label = "";

        public void Handle(object sender, object e)
        {
            try
            {
                var real = MethodTrace.Real(e);
                string target = "";
                if (real.GetType().GetProperty("target")?.GetValue(real) is { } t)
                {
                    var rt = MethodTrace.Real(t);
                    var sym = WoGPlugin.Symbols!;
                    target = (sym.Has("bunit.sid") && sym.TypeOf("bunit.sid").IsInstanceOfType(rt) ? sym.Read("bunit.sid", rt) as string : null)
                             ?? rt.GetType().Name;
                }
                string line = $"{Label}: {real.GetType().Name} round {real.GetType().GetProperty("roundNumber")?.GetValue(real)} target {target} | {MethodTrace.Describe(real)}";
                Log.Add(line);
                if (Log.Count > 2000) Log.RemoveRange(0, 500);
                WoGPlugin.L?.LogInfo("WoG battle event " + line);
            }
            catch (Exception ex) { WoGPlugin.L?.LogError("WoG battle event probe: " + ex.Message); }
        }
    }

    /// <summary>An Il2Cpp EventHandler&lt;T&gt; calling back a managed sink.</summary>
    static object Il2CppHandler(Type handlerType, string label)
    {
        var invoke = handlerType.GetMethod("Invoke")!;
        var ps = invoke.GetParameters().Select(p => p.ParameterType).ToArray();
        var actionType = typeof(Action<,>).MakeGenericType(ps);
        var sink = new Sink { Label = label };
        var managed = Delegate.CreateDelegate(actionType, sink, typeof(Sink).GetMethod(nameof(Sink.Handle))!);
        return typeof(DelegateSupport).GetMethods().First(m => m.Name == "ConvertDelegate" && m.IsGenericMethod)
            .MakeGenericMethod(handlerType).Invoke(null, new object[] { managed })!;
    }

    static object Combine(object a, object b, Type handlerType)
    {
        var delegateType = AppDomain.CurrentDomain.GetAssemblies().Select(x => x.GetType("Il2CppSystem.Delegate")).First(t => t != null)!;
        var combined = delegateType.GetMethod("Combine", new[] { delegateType, delegateType })!.Invoke(null, new[] { a, b })!;
        return typeof(Il2CppObjectBase).GetMethod("Cast")!.MakeGenericMethod(handlerType).Invoke(combined, null)!;
    }
}
