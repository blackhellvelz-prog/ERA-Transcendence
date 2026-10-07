using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace WoG.OldenEra;

/// <summary>
/// Where to find a piece of game state: a type (namespace-qualified, possibly obfuscated), and a dotted member
/// path from a static root ("me.cfjl.sides") or from an object of that type ("res"). A method binding has a
/// single-segment path naming the method ("OnStartDay").
/// </summary>
public sealed class SymbolBinding
{
    public string Assembly { get; set; } = "Hex";
    public string Type { get; set; } = "";
    public string Member { get; set; } = "";
    /// <summary>"verified" once confirmed in game; anything else keeps the dependent feature disabled.</summary>
    public string Status { get; set; } = "unverified";
    /// <summary>What the binding points at and how it was found (free text for the person doing the RE).</summary>
    public string Note { get; set; } = "";
}

/// <summary>
/// All Olden Era symbols the adapter needs, loaded from BepInEx/config/wog_symbols.json (produced by
/// the in-game RE step, see OldenEra_ReverseEngineering/07_InGame_RE_Plan.md). Resolution is done once at
/// start-up; every missing symbol is logged (the gme-mod pattern) and only its features are disabled.
/// No obfuscated name is hard-coded in the plugin, so a game update only needs a new JSON file.
/// </summary>
public sealed class OldenEraSymbols
{
    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    public Dictionary<string, SymbolBinding> Bindings { get; set; } = new();

    /// <summary>
    /// Debug mode only (WoG Debug): treat resolved but unverified symbols as usable, so the in-game self-test
    /// can exercise them — that is how a symbol becomes verified. Never on in normal play.
    /// </summary>
    public bool AllowUnverified { get; set; }

    readonly Dictionary<string, (Type type, MemberInfo[] path)> resolved = new();
    readonly List<string> missing = new();

    public IReadOnlyList<string> Missing => missing;

    /// <summary>Symbol keys the adapter understands (documented in 07_InGame_RE_Plan.md §2).</summary>
    public static readonly string[] Known =
    {
        "game.root",                 // static path to the session state object (Hex.Session.Data.Data)
        "game.day",                  // absolute day counter on the root
        "game.dayOfWeek", "game.week", "game.month",
        "player.list",               // root → array/list of players (sides)
        "player.resources",          // player → resource heap; a resource is a member named by IdMap "resource"
        "resource.value",            // resource → amount
        "player.logic",              // static path to the game logic's per-player objects (same order as player.list)
        "player.resourceLogic",      // player logic object → its resource component
        "resource.add",              // method of the resource component: add(resource name, delta) — updates the UI
        "resource.spend",            // method of the resource component: spend(resource name, amount) → bool
        "player.isHuman",            // player → creation type (enum; 0 = human)
        "player.alive",              // player → status (enum; 0 = alive)
        "player.local",              // root → index of the player sitting at this PC
        "player.id",                 // player → the side id heroes refer to (hero.owner)
        "player.current",
        "hero.list", "hero.id", "hero.owner", "hero.experience", "hero.level",
        "hero.offence", "hero.defence", "hero.spellPower", "hero.intelligence", "hero.mana", "hero.movement",
        "hero.status",               // hero → status (enum: on map, dead, in prison, …)
        "hero.config",               // hero → type id (config sid)
        "hero.statsBase",            // hero → primary skills of its type (offence, defence, spellPower, intelligence)
        "hero.army", "stack.unitSid", "stack.count",
        "stack.slot",                // army unit → slot position
        "unit.db", "unit.stats",
        "unit.config",               // session unit → its type config (UnitLogicConfig), resolved from the unit's sid
        "ui.message", "ui.question",
        "turn.start",                // method: start of a day (Harmony postfix → PlayerDayStarted for every player)
        "object.interact",           // method: hero interacts with a map object (prefix/postfix → OB triggers)
        "battle.start", "battle.end", "battle.round", "battle.action",
        "buff.apply",                // method: apply buff by id to a unit
        "save.write", "save.read",   // methods: save/load (side-car WoG state)
        "hero.position", "hero.move", "hero.skills", "hero.spells", "hero.items", "hero.name", "hero.kill",
        "stack.create", "player.activeHero", "player.heroes",
        "map.objects", "town.list", "town.buildings", "battle.stacks", "battle.summon",
        "map.root",                  // static path to the adventure map (Hex.Map.Map)
        "map.sizeX", "map.sizeZ",    // map → width / height in nodes (node = x + z·sizeX)
        "mapobj.id", "mapobj.node",  // map object → its map object id / pivot node
        "mapobj.entrances",          // map object → nodes a hero visits it from
        "mapobj.blocked",            // map object → nodes it occupies
        "map.data", "map.tiles", "map.water", "map.roads", // map → MapData → per-node biome / water / road ids
        "map.generatorChecksum",     // MapData → checksum of the generator (non-empty on a generated map)
        "alliance.list", "alliance.sides", // root → alliances; alliance → side ids
        "game.difficulty",           // static path to the AI difficulty chosen at the start
        "mapobj.sid",                // map object → object config sid (object.json)
        "object.list",               // root → session objects (interactive objects with game data)
        "object.mapId", "object.owner", // session object → map object id / owner side id (-1 neutral)
        "object.interactEnd",        // method: a visit completes (postfix → !$OB)
        "visit.mapObject",           // object logic of a visit → its map object
        "visitor.hero",              // hero logic of a visit → Hex.Session.Data.Hero
        "events.invoke",             // method: the game event bus (EEvent, argument) — battles, steps, level-ups
        "event.battleSide",          // SideStartBattle argument → side id (-1 neutral)
        "event.battleEndSide",       // SideEndBattle argument → side id
        "event.saveOk", "event.savePath", // MapSaved argument → saved / save path (relative to the user folder)
        "session.startInfo",         // static path to how the session started (StartInfo)
        "startInfo.load", "startInfo.hash", // StartInfo → ELoad (1 = LoadSave) / checksum of the loaded save
        "squad.list",                // root → wandering monster squads
        "squad.node", "squad.units", "squad.released", // squad → node / unit stacks / defeated
        "squadunit.sid", "squadunit.amount",           // squad unit → unit sid / count
    };

    public static OldenEraSymbols Load(string path)
    {
        if (!File.Exists(path)) return new OldenEraSymbols();
        var s = JsonSerializer.Deserialize<OldenEraSymbols>(File.ReadAllText(path)) ?? new OldenEraSymbols();
        return s;
    }

    public void Save(string path) =>
        File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));

    /// <summary>Writes a template listing every known key, for the person doing the in-game RE.</summary>
    public static void WriteTemplate(string path)
    {
        var t = new OldenEraSymbols();
        foreach (var k in Known) t.Bindings[k] = new SymbolBinding();
        t.Save(path);
    }

    /// <summary>
    /// Resolves every binding against the loaded (interop) assemblies. A member path is walked through the
    /// declared field/property types, so a stale segment anywhere in it is reported at start-up.
    /// </summary>
    public void Resolve(Action<string> log)
    {
        resolved.Clear();
        missing.Clear();
        foreach (var key in Known)
        {
            if (!Bindings.TryGetValue(key, out var b) || string.IsNullOrEmpty(b.Type))
            {
                missing.Add($"{key}: not bound");
                continue;
            }
            var asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == b.Assembly);
            var type = asm?.GetType(b.Type);
            if (type == null) { missing.Add($"{key}: type {b.Type} ({b.Assembly}) not found"); continue; }
            var path = new List<MemberInfo>();
            string? error = null;
            var current = type;
            foreach (var name in string.IsNullOrEmpty(b.Member) ? Array.Empty<string>() : b.Member.Split('.'))
            {
                var m = current.GetMember(name, All).FirstOrDefault(x => x is FieldInfo or PropertyInfo or MethodInfo);
                if (m == null) { error = $"member {current.FullName}.{name} not found"; break; }
                path.Add(m);
                if (m is MethodInfo) break;
                current = m is FieldInfo f ? f.FieldType : ((PropertyInfo)m).PropertyType;
            }
            if (error != null) { missing.Add($"{key}: {error} (path {b.Type}.{b.Member})"); continue; }
            if (b.Status != "verified")
                log(AllowUnverified
                    ? $"WoG symbol {key} resolved, NOT verified — enabled because WoG Debug allows unverified symbols"
                    : $"WoG symbol {key} resolved but not verified in game — feature stays disabled");
            resolved[key] = (type, path.ToArray());
        }
        foreach (var m in missing) log("Game API not found: " + m);
    }

    /// <summary>True for resolved and in-game-verified symbols (or any resolved one in debug mode).</summary>
    public bool Has(string key) =>
        resolved.ContainsKey(key) && Bindings.TryGetValue(key, out var b) && (b.Status == "verified" || AllowUnverified);

    public bool IsResolved(string key) => resolved.ContainsKey(key);
    public bool IsVerified(string key) => Bindings.TryGetValue(key, out var b) && b.Status == "verified";

    public Type TypeOf(string key) => resolved[key].type;
    /// <summary>The last member of the path (the method for method bindings).</summary>
    public MemberInfo? MemberOf(string key) => resolved[key].path.LastOrDefault();

    /// <summary>
    /// Reads the value at the end of the member path (the first member is static when target is null). A null
    /// anywhere on the way — no game session yet, an instance member without an object — reads as null.
    /// </summary>
    public object? Read(string key, object? target)
    {
        object? o = target;
        foreach (var m in resolved[key].path)
        {
            if (o == null && !IsStatic(m)) return null;
            o = Get(m, o);
            if (o == null) return null;
        }
        return o;
    }

    static bool IsStatic(MemberInfo m) => m switch
    {
        FieldInfo f => f.IsStatic,
        PropertyInfo p => (p.GetMethod ?? p.SetMethod)?.IsStatic ?? false,
        MethodInfo mi => mi.IsStatic,
        _ => false,
    };

    public void Write(string key, object? target, object? value)
    {
        var path = resolved[key].path;
        if (path.Length == 0) throw new InvalidOperationException($"{key} has no member path");
        object? o = target;
        for (int i = 0; i < path.Length - 1; i++)
        {
            o = Get(path[i], o);
            if (o == null) throw new InvalidOperationException($"{key}: {path[i].Name} is null");
        }
        Set(path[^1], o, value);
    }

    static object? Get(MemberInfo m, object? o) => m switch
    {
        FieldInfo f => f.GetValue(o),
        PropertyInfo p => p.GetValue(o),
        _ => throw new InvalidOperationException($"{m.Name} is not a field or property"),
    };

    static void Set(MemberInfo m, object? o, object? value)
    {
        switch (m)
        {
            case FieldInfo f: f.SetValue(o, Convert.ChangeType(value, f.FieldType)); break;
            case PropertyInfo p: p.SetValue(o, Convert.ChangeType(value, p.PropertyType)); break;
            default: throw new InvalidOperationException($"{m.Name} is not a field or property");
        }
    }

    /// <summary>Reads a member of an object by name (for members chosen at run time, e.g. a resource by IdMap sid).</summary>
    public static object? ReadMember(object target, string name)
    {
        var m = target.GetType().GetMember(name, All).FirstOrDefault(x => x is FieldInfo or PropertyInfo)
                ?? throw new InvalidOperationException($"{target.GetType().FullName}.{name} not found");
        return Get(m, target);
    }

    /// <summary>Writes a member of an object by name (for config values chosen at run time, e.g. a unit stat).</summary>
    public static void WriteMember(object target, string name, object value)
    {
        var m = target.GetType().GetMember(name, All).FirstOrDefault(x => x is FieldInfo or PropertyInfo)
                ?? throw new InvalidOperationException($"{target.GetType().FullName}.{name} not found");
        Set(m, target, value);
    }

    /// <summary>Calls a public instance method of a game object by name with arguments of matching count.</summary>
    public static object? Call(object target, string method, params object?[] args)
    {
        var m = target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .FirstOrDefault(x => x.Name == method && x.GetParameters().Length == args.Length)
            ?? throw new InvalidOperationException($"{target.GetType().FullName}.{method}/{args.Length} not found");
        return m.Invoke(target, args);
    }

    /// <summary>
    /// Items of a game collection: .NET enumerables (Il2CppArrayBase) and IL2CPP lists, which are not
    /// IEnumerable for .NET reflection but expose Count and get_Item(int).
    /// </summary>
    public static IReadOnlyList<object?> Items(object? collection)
    {
        if (collection == null) return Array.Empty<object?>();
        if (collection is IEnumerable e) return e.Cast<object?>().ToList();
        var t = collection.GetType();
        var count = t.GetProperty("Count");
        var item = t.GetMethod("get_Item", new[] { typeof(int) });
        if (count == null || item == null) throw new InvalidOperationException($"{t.FullName} is not a collection");
        int n = Convert.ToInt32(count.GetValue(collection));
        var list = new List<object?>(n);
        for (int i = 0; i < n; i++) list.Add(item.Invoke(collection, new object[] { i }));
        return list;
    }
}
