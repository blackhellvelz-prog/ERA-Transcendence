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
    public DebugCommands Commands => commands;
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
            "Put a command into a .txt file in 'in' (for example in/1.txt containing: state); write it under another\n" +
            "extension first and rename it, so a half-written file is never read.\n" +
            "The result appears in 'out' under the same name.\n\n" + DebugCommands.Help);
    }

    public void Poll()
    {
        foreach (var file in Directory.GetFiles(inDir, "*.txt").OrderBy(f => f, StringComparer.Ordinal))
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
        var real = Real(a);
        if (real.GetType().Assembly.GetName().Name != "Hex") return "<" + RuntimeName(a) + ">";
        // The real class's own fields (event argument data, session objects).
        var fields = real.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(p => p.GetIndexParameters().Length == 0)
            .Select(p => { try { return $"{p.Name}={Short(p.GetValue(real))}"; } catch { return p.Name + "=?"; } });
        return $"<{RuntimeName(a)} {string.Join(" ", fields)}>";
    }

    /// <summary>The IL2CPP runtime class name of an object (the declared .NET type is often a base class).</summary>
    static string RuntimeName(object a)
    {
        try
        {
            var il2cppType = a.GetType().GetMethod("GetIl2CppType", Type.EmptyTypes)?.Invoke(a, null);
            if (il2cppType?.GetType().GetProperty("FullName")?.GetValue(il2cppType) is string name) return name;
        }
        catch { /* not an IL2CPP object */ }
        return a.GetType().FullName ?? a.GetType().Name;
    }

    /// <summary>An IL2CPP object re-wrapped as its runtime class of the game assembly, so that class's members resolve.</summary>
    internal static object Real(object a)
    {
        try
        {
            string name = RuntimeName(a);
            var real = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(x => x.GetName().Name == "Hex")?.GetType(name.Replace('/', '+'));
            if (real != null && real != a.GetType() && a is Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase o)
                return Activator.CreateInstance(real, o.Pointer)!;
        }
        catch { /* keep the declared type */ }
        return a;
    }

    internal static string Short(object? v) => v == null ? "null" : v.GetType().IsPrimitive || v is string || v.GetType().IsEnum ? v.ToString()! : v.GetType().Name;
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

    const BindingFlags Members = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;

    /// <summary>
    /// peek root.heroes.list[0].node | peek Type.staticMember.member[2] | peek Type — walks the path with
    /// reflection; every object is re-wrapped as its runtime class first, so members of derived classes resolve.
    /// </summary>
    public string Peek(string args)
    {
        var words = args.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return "usage: peek <root|Type>.member[index].member... [max items]";
        int max = words.Length > 1 && int.TryParse(words[1], out int m) ? m : 10;
        string[]? only = words.Length > 2 ? words[2].Split(',', StringSplitOptions.RemoveEmptyEntries) : null;
        var o = Resolve(words[0], out var error);
        return error ?? Show(o, max, only);
    }

    /// <summary>The object at a peek path; error is set (with the members that exist) when the path breaks.</summary>
    object? Resolve(string path, out string? error)
    {
        error = null;
        var segs = Segments(path);
        object? o;
        int i;
        if (segs[0] == "root")
        {
            o = A.Root();
            i = 1;
        }
        else
        {
            var asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "Hex");
            Type? t = null;
            for (i = segs.Count; i >= 1 && t == null; i--)
                t = asm?.GetType(string.Join(".", segs.Take(i)));
            if (t == null) { error = "type not found: " + path; return null; }
            i++; // the loop stepped one past the matching prefix
            if (i >= segs.Count) { error = TypeMembers(t, statics: true); return null; }
            var sm = t.GetMember(segs[i], Members).FirstOrDefault(x => x is FieldInfo or PropertyInfo);
            if (sm == null) { error = $"{t.FullName}.{segs[i]} not found\n" + TypeMembers(t, statics: true); return null; }
            o = sm is FieldInfo f ? f.GetValue(null) : ((PropertyInfo)sm).GetValue(null);
            i++;
        }
        for (; i < segs.Count; i++)
        {
            if (o == null) { error = $"null at {string.Join(".", segs.Take(i))}"; return null; }
            string s = segs[i];
            if (s.StartsWith("["))
            {
                var items = OldenEraSymbols.Items(o);
                int idx = int.Parse(s[1..^1]);
                if (idx < 0 || idx >= items.Count) { error = $"index {idx} out of range (count {items.Count})"; return null; }
                o = items[idx];
                continue;
            }
            o = MethodTrace.Real(o);
            var mem = o.GetType().GetMember(s, Members).FirstOrDefault(x => x is FieldInfo or PropertyInfo);
            if (mem == null) { error = $"{o.GetType().FullName}.{s} not found\n" + TypeMembers(o.GetType(), statics: false); return null; }
            o = mem is FieldInfo fi ? fi.GetValue(o) : ((PropertyInfo)mem).GetValue(o);
        }
        return o;
    }

    /// <summary>
    /// invoke &lt;path&gt; &lt;method&gt; [arg...] — calls a public method of the object at a peek path (the overload with
    /// that many parameters whose types accept the arguments). Arguments: numbers, "text", true/false, null,
    /// @&lt;path&gt; for a game object. Reverse engineering only: it runs game code with whatever it is given.
    /// </summary>
    public string Invoke(string args)
    {
        var words = SplitArgs(args);
        if (words.Count < 2) return "usage: invoke <path> <method> [arg...]";
        var target = Resolve(words[0], out var error);
        if (error != null) return error;
        if (target == null) return "null target";
        target = MethodTrace.Real(target);
        var values = new List<object?>();
        foreach (var w in words.Skip(2))
        {
            if (w.StartsWith("@"))
            {
                var v = Resolve(w[1..], out var e);
                if (e != null) return e;
                values.Add(v == null ? null : MethodTrace.Real(v));
            }
            else values.Add(w);
        }
        foreach (var m in target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static)
                     .Where(x => x.Name == words[1] && x.GetParameters().Length == values.Count))
        {
            var ps = m.GetParameters();
            var call = new object?[values.Count];
            bool fits = true;
            for (int k = 0; k < ps.Length && fits; k++)
                fits = TryConvert(values[k], ps[k].ParameterType, out call[k]);
            if (!fits) continue;
            var result = m.Invoke(m.IsStatic ? null : target, call);
            return $"{m.DeclaringType?.Name}.{m.Name}({string.Join(", ", ps.Select(x => x.ParameterType.Name))}) → " +
                   (m.ReturnType == typeof(void) ? "void" : Show(result, 10));
        }
        return $"no method {words[1]}/{values.Count} accepting these arguments\n" + TypeMembers(target.GetType(), statics: false);
    }

    /// <summary>Words of a command line; "quoted text" stays one word (without the quotes, marked by a leading \0).</summary>
    static List<string> SplitArgs(string text)
    {
        var list = new List<string>();
        int i = 0;
        while (i < text.Length)
        {
            if (char.IsWhiteSpace(text[i])) { i++; continue; }
            if (text[i] == '"')
            {
                int end = text.IndexOf('"', i + 1);
                if (end < 0) end = text.Length;
                list.Add("\0" + text[(i + 1)..end]);
                i = end + 1;
                continue;
            }
            int start = i;
            while (i < text.Length && !char.IsWhiteSpace(text[i])) i++;
            list.Add(text[start..i]);
        }
        return list;
    }

    static bool TryConvert(object? value, Type type, out object? result)
    {
        result = null;
        if (value is not string w) // a game object from @path
        {
            if (value == null) return !type.IsValueType;
            if (type.IsInstanceOfType(value)) { result = value; return true; }
            // an IL2CPP object of a base class: re-wrap it as the parameter type
            if (value is Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase ob && typeof(Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase).IsAssignableFrom(type))
            {
                result = Activator.CreateInstance(type, ob.Pointer);
                return true;
            }
            return false;
        }
        // ordinal check: culture-aware StartsWith("\0") is true for every string ("\0" is ignorable)
        if (w.Length > 0 && w[0] == '\0') { if (type != typeof(string)) return false; result = w[1..]; return true; }
        if (w == "null") return !type.IsValueType;
        try
        {
            if (type == typeof(bool) && bool.TryParse(w, out var b)) { result = b; return true; }
            if (type.IsEnum) { result = Enum.Parse(type, w, ignoreCase: true); return true; }
            if (type == typeof(string)) { result = w; return true; }
            if (type.IsPrimitive) { result = Convert.ChangeType(w, type, System.Globalization.CultureInfo.InvariantCulture); return true; }
        }
        catch (Exception) { }
        return false;
    }

    /// <summary>Chosen members of an object ("node,sideId" or a path "party.units"), on one line.</summary>
    static string Project(object? item, string[] members)
    {
        if (item == null) return "null";
        return string.Join(" ", members.Select(path =>
        {
            object? v = item;
            try
            {
                foreach (var name in path.Split('.'))
                {
                    if (v == null) break;
                    v = MethodTrace.Real(v);
                    var mem = v.GetType().GetMember(name, Members).FirstOrDefault(x => x is FieldInfo or PropertyInfo);
                    v = mem is FieldInfo fi ? fi.GetValue(v) : mem is PropertyInfo pi ? pi.GetValue(v) : "?";
                }
                return $"{path}={MethodTrace.Short(v)}";
            }
            catch (Exception ex) { return $"{path}=error {ex.GetType().Name}"; }
        }));
    }

    static List<string> Segments(string path)
    {
        var list = new List<string>();
        foreach (var part in path.Split('.'))
        {
            int b = part.IndexOf('[');
            if (b < 0) { list.Add(part); continue; }
            if (b > 0) list.Add(part[..b]);
            foreach (var ix in part[b..].Split('[', StringSplitOptions.RemoveEmptyEntries)) list.Add("[" + ix);
        }
        return list;
    }

    static string TypeMembers(Type t, bool statics)
    {
        var flags = BindingFlags.Public | BindingFlags.DeclaredOnly | (statics ? BindingFlags.Static : BindingFlags.Instance);
        var sb = new StringBuilder($"{t.FullName} : {t.BaseType?.FullName}\n");
        foreach (var p in t.GetProperties(flags).Where(p => p.GetIndexParameters().Length == 0))
            sb.Append($"  {p.PropertyType.Name} {p.Name}\n");
        foreach (var mi in t.GetMethods(flags).Where(x => !x.IsSpecialName))
            sb.Append($"  {mi.ReturnType.Name} {mi.Name}({string.Join(", ", mi.GetParameters().Select(x => x.ParameterType.Name))})\n");
        return sb.ToString();
    }

    static string Show(object? o, int max, string[]? only = null)
    {
        if (o == null) return "null";
        var t = o.GetType();
        if (t.IsPrimitive || t.IsEnum || o is string) return o.ToString() ?? "";
        IReadOnlyList<object?>? items = null;
        try { items = OldenEraSymbols.Items(o); } catch (InvalidOperationException) { }
        if (items == null) return MethodTrace.Describe(o);
        var sb = new StringBuilder($"{MethodTrace.Describe(o)}\ncount {items.Count}\n");
        for (int k = 0; k < Math.Min(max, items.Count); k++)
            sb.Append($"[{k}] {(only == null ? MethodTrace.Describe(items[k]) : Project(items[k], only))}\n");
        return sb.ToString();
    }
}
