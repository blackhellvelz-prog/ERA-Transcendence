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
            var methods = type?.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Where(m => m.Name == item[(dot + 1)..]).ToList();
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

    static void Postfix(MethodBase __originalMethod)
    {
        try
        {
            string k = Key(__originalMethod);
            calls.TryGetValue(k, out int n);
            calls[k] = ++n;
            int day = WoGPlugin.Host?.Game.Clock.AbsoluteDay ?? -1;
            log?.LogInfo($"WoG trace: {k} call #{n} (WoG day {day})");
        }
        catch (Exception ex) { log?.LogError("WoG trace failed: " + ex.Message); }
    }
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
            sb.Append($" status={Try(() => S.Has("player.alive") ? S.Read("player.alive", p) : "?")}\n");
            if (S.Has("player.resources"))
                sb.Append("      resources: ").Append(Try(() => Resources(S.Read("player.resources", p)))).Append('\n');
        }
        return sb.ToString();
    }

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
