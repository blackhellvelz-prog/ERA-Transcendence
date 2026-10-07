using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx.Logging;
using HarmonyLib;
using WoG.Debug;
using WoG.Host;

namespace WoG.OldenEra;

/// <summary>
/// WoG Debug command bridge: a command file dropped into debug/in (one command per file, "erm" may span
/// lines) is executed on the game thread and its output written to debug/out under the same name. An agent
/// or a person can drive the WoG layer in the running game without UI. Every command and result is also
/// appended to debug/session.log; the last self-test report is debug/selftest-latest.md.
/// </summary>
internal sealed class DebugBridge
{
    readonly string dir, inDir, outDir;
    readonly DebugCommands commands;
    readonly ManualLogSource log;

    public DebugBridge(string dir, WoGHost host, IDebugEngine engine, ManualLogSource log)
    {
        this.dir = dir;
        this.log = log;
        inDir = Path.Combine(dir, "in");
        outDir = Path.Combine(dir, "out");
        Directory.CreateDirectory(inDir);
        Directory.CreateDirectory(outDir);
        commands = new DebugCommands(host, engine);
        File.WriteAllText(Path.Combine(dir, "README.txt"),
            "Put a command into a text file in 'in' (for example in/1.txt containing: state).\n" +
            "The result appears in 'out' under the same name.\n\n" + DebugCommands.Help);
    }

    public void Poll()
    {
        foreach (var file in Directory.GetFiles(inDir).OrderBy(f => f, StringComparer.Ordinal))
        {
            string text;
            try { text = File.ReadAllText(file); File.Delete(file); }
            catch (IOException) { continue; } // still being written; next tick
            string name = Path.GetFileName(file);
            string result = commands.Execute(text);
            File.WriteAllText(Path.Combine(outDir, name), result);
            File.AppendAllText(Path.Combine(dir, "session.log"), $"[{DateTime.Now:HH:mm:ss}] > {text.Trim()}\n{result}\n\n");
            if (commands.LastReport != null && text.TrimStart().StartsWith("selftest", StringComparison.OrdinalIgnoreCase))
                File.WriteAllText(Path.Combine(dir, "selftest-latest.md"), commands.LastReport);
            log.LogInfo($"WoG Debug: {name}: {result.Split('\n')[0]}");
        }
    }
}

/// <summary>
/// Debug only: logs every call of chosen game methods (all overloads), to find which method really runs
/// when something happens in game — how symbols such as turn.start are verified.
/// </summary>
internal static class MethodTrace
{
    static readonly Dictionary<string, int> calls = new(StringComparer.Ordinal);
    static ManualLogSource? log;

    public static IReadOnlyDictionary<string, int> Calls => calls;

    public static void Install(Harmony harmony, string spec, ManualLogSource logSource)
    {
        log = logSource;
        var asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "Hex");
        foreach (var item in spec.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int dot = item.LastIndexOf('.');
            if (dot <= 0) { log.LogWarning("WoG trace: expected Type.Method, got " + item); continue; }
            var type = asm?.GetType(item[..dot]);
            string name = item[(dot + 1)..];
            // "Type.*": every method the class declares itself (not property accessors), to find which one runs.
            var methods = type?.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(m => name == "*" ? !m.IsSpecialName : m.Name == name).ToList();
            if (methods == null || methods.Count == 0) { log.LogWarning("WoG trace: not found " + item); continue; }
            foreach (var m in methods)
            {
                harmony.Patch(m, postfix: new HarmonyMethod(typeof(MethodTrace), nameof(Postfix)));
                calls[Key(m)] = 0;
                log.LogInfo("WoG trace: watching " + Key(m));
            }
        }
    }

    static string Key(MethodBase m) =>
        $"{m.DeclaringType?.FullName}.{m.Name}({string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name))})";

    static readonly HashSet<string> seenArgs = new(StringComparer.Ordinal);

    /// <summary>
    /// Logs a call with its argument values (enums, numbers, strings) and the real IL2CPP class of object arguments
    /// — each distinct argument signature once, so busy methods (an event bus) do not flood the log.
    /// </summary>
    static void Postfix(MethodBase __originalMethod, object[] __args)
    {
        try
        {
            string k = Key(__originalMethod);
            calls.TryGetValue(k, out int n);
            calls[k] = ++n;
            string args = string.Join(", ", (__args ?? Array.Empty<object>()).Select(Describe));
            if (!seenArgs.Add(k + "|" + args) && n > 3) return;
            // No game state is read here: a traced method may run while that state is half built.
            log?.LogInfo($"WoG trace: {k} call #{n} args: {args}");
        }
        catch (Exception ex) { log?.LogError("WoG trace failed: " + ex.Message); }
    }

    /// <summary>A value as text; an IL2CPP object as its runtime class (the declared type is often a base class).</summary>
    internal static string Describe(object? a)
    {
        if (a == null) return "null";
        var t = a.GetType();
        if (t.IsPrimitive || t.IsEnum || a is string) return a.ToString() ?? "";
        try
        {
            var il2cppType = t.GetMethod("GetIl2CppType", Type.EmptyTypes)?.Invoke(a, null);
            var name = il2cppType?.GetType().GetProperty("FullName")?.GetValue(il2cppType) as string;
            if (name != null)
            {
                // Re-wrap the object as its real class to show that class's own fields (event argument data).
                var real = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(x => x.GetName().Name == "Hex")?.GetType(name);
                if (real != null && real != t && a is Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase o)
                {
                    var wrapped = Activator.CreateInstance(real, o.Pointer);
                    var fields = real.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                        .Where(p => p.GetIndexParameters().Length == 0)
                        .Select(p => { try { return $"{p.Name}={Short(p.GetValue(wrapped))}"; } catch { return p.Name + "=?"; } });
                    return $"<{name} {string.Join(" ", fields)}>";
                }
                return "<" + name + ">";
            }
        }
        catch { /* not an IL2CPP object */ }
        return "<" + t.FullName + ">";
    }

    static string Short(object? v) => v == null ? "null" : v.GetType().IsPrimitive || v is string || v.GetType().IsEnum ? v.ToString()! : v.GetType().Name;
}

/// <summary>The Olden Era half of WoG Debug: raw state read through the symbols, without ERM.</summary>
internal sealed class OldenEraDebugEngine : IDebugEngine
{
    public string Name => "Olden Era";

    static OldenEraSymbols S => WoGPlugin.Symbols!;
    static OldenEraGameAdapter A => WoGPlugin.Adapter!;

    public string DumpState()
    {
        var sb = new StringBuilder();
        var root = A.Root();
        sb.Append($"game.root: {(root == null ? "null (no session / unbound)" : root.GetType().FullName)}\n");
        if (root == null) return sb.ToString();
        foreach (var key in new[] { "game.day", "game.dayOfWeek", "game.week", "game.month", "player.local" })
            sb.Append($"{key}: {Try(() => S.Has(key) ? S.Read(key, root) : "(not bound)")}\n");
        var players = A.PlayerObjects();
        sb.Append($"players: {players.Count}\n");
        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            if (p == null) { sb.Append($"  [{i}] null\n"); continue; }
            sb.Append($"  [{i}] name={Try(() => OldenEraSymbols.ReadMember(p, "name"))}");
            sb.Append($" type={Try(() => S.Has("player.isHuman") ? S.Read("player.isHuman", p) : "?")}");
            sb.Append($" status={Try(() => S.Has("player.alive") ? S.Read("player.alive", p) : "?")}");
            sb.Append($" id={Try(() => S.Has("player.id") ? S.Read("player.id", p) : "?")}");
            sb.Append($" activeHero(raw)={Try(() => S.Has("player.activeHero") ? S.Read("player.activeHero", p) : "?")}");
            sb.Append($" heroes(raw)=[{Try(() => S.Has("player.heroes") ? string.Join(",", OldenEraSymbols.Items(S.Read("player.heroes", p))) : "?")}]\n");
            if (S.Has("player.resources"))
                sb.Append("      resources: ").Append(Try(() => Resources(S.Read("player.resources", p)))).Append('\n');
        }
        if (S.Has("hero.list"))
        {
            var heroes = OldenEraSymbols.Items(S.Read("hero.list", root));
            sb.Append($"heroes: {heroes.Count}\n");
            foreach (var h in heroes)
            {
                if (h == null) continue;
                int id = Convert.ToInt32(S.Read("hero.id", h));
                sb.Append($"  hero id={id} wog#={A.HeroNumber(id)} owner(side)={Try(() => S.Read("hero.owner", h))}");
                sb.Append($" type={Try(() => S.Has("hero.config") ? S.Read("hero.config", h) : "?")}");
                sb.Append($" status={Try(() => S.Has("hero.status") ? S.Read("hero.status", h) : "?")}");
                sb.Append($" level={Try(() => S.Read("hero.level", h))} exp={Try(() => S.Read("hero.experience", h))}");
                sb.Append($" mana={Try(() => S.Read("hero.mana", h))} move={Try(() => S.Read("hero.movement", h))}\n");
                sb.Append($"      type base: {Try(() => S.Has("hero.statsBase") ? Stats(S.Read("hero.statsBase", h)) : "?")}\n");
                sb.Append($"      statsByLevel: {Try(() => Stats(OldenEraSymbols.ReadMember(h, "statsByLevel")))}\n");
                sb.Append($"      additionalStats: {Try(() => Stats(OldenEraSymbols.ReadMember(h, "additionalStats")))}\n");
                if (S.Has("hero.army"))
                    sb.Append("      army: ").Append(Try(() => string.Join(", ", OldenEraSymbols.Items(S.Read("hero.army", h)).Where(u => u != null)
                        .Select(u => $"[{S.Read("stack.slot", u)}] {S.Read("stack.unitSid", u)} x{S.Read("stack.count", u)}")))).Append('\n');
            }
        }
        return sb.ToString();
    }

    /// <summary>The primary stats of a hero stat block (offence, defence, spell power, intelligence, luck, moral).</summary>
    static string Stats(object? block) => block == null ? "null" :
        string.Join(" ", new[] { "offence", "defence", "spellPower", "intelligence", "luck", "moral" }
            .Select(n => $"{n}={OldenEraSymbols.ReadMember(block, n)}"));

    /// <summary>Every member of the resource heap that has a "value" (name=value), whatever the IdMap says.</summary>
    static string Resources(object? heap)
    {
        if (heap == null) return "null";
        var parts = new List<string>();
        foreach (var p in heap.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
        {
            if (p.GetIndexParameters().Length > 0 || p.PropertyType.GetProperty("value") == null) continue;
            var r = p.GetValue(heap);
            parts.Add($"{p.Name}={(r == null ? "null" : OldenEraSymbols.ReadMember(r, "value"))}");
        }
        return string.Join(", ", parts);
    }

    public string NewDay() => WoGSession.StartDay();

    public string Symbols()
    {
        var sb = new StringBuilder("| key | binding | resolved | verified |\n|---|---|---|---|\n");
        foreach (var key in OldenEraSymbols.Known)
        {
            S.Bindings.TryGetValue(key, out var b);
            string binding = b == null || b.Type.Length == 0 ? "—" : $"{b.Type}.{b.Member}";
            sb.Append($"| {key} | {binding} | {(S.IsResolved(key) ? "yes" : "no")} | {(S.IsVerified(key) ? "yes" : "no")} |\n");
        }
        sb.Append($"\nAllowUnverified: {S.AllowUnverified}\n");
        sb.Append($"day starts: {WoGSession.DayStarts}, last: {WoGSession.LastDayStart}\n");
        foreach (var (k, n) in MethodTrace.Calls) sb.Append($"trace {k}: {n} call(s)\n");
        return sb.ToString();
    }

    static string Try(Func<object?> f)
    {
        try { return f()?.ToString() ?? "null"; }
        catch (Exception ex) { return "error: " + ex.Message; }
    }
}
