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
        "player.tavern",             // player → the Olden Era ids of the heroes its tavern offers
        "hero.list", "hero.id", "hero.owner", "hero.experience", "hero.level",
        "hero.offence", "hero.defence", "hero.spellPower", "hero.intelligence", "hero.mana", "hero.movement",
        "hero.status",               // hero → status (enum: on map, dead, in prison, …)
        "hero.config",               // hero → type id (config sid)
        "hero.statsBase",            // hero → primary skills of its type (offence, defence, spellPower, intelligence)
        "hero.army", "stack.unitSid", "stack.count",
        "stack.slot",                // army unit → slot position
        "unit.db", "unit.stats",
        "unit.config",               // session unit → its type config (UnitLogicConfig), resolved from the unit's sid
        "hero.skillsHolder",         // hero → its skills (HeroSkills)
        "hero.skillsAdd",            // method of HeroSkills: give a skill (sid, level)
        "skill.sid", "skill.level",  // hero skill → sid / level 1..3
        "world.heroLogics",          // static path to the logic objects of the heroes on the map
        "herologic.skills",          // hero logic → its skills logic (one skill logic per applied skill)
        "herologic.stats",           // hero logic → the hero's total stats (type, growth, items, skills)
        "herologic.logic", "herologic.recalc", // hero logic → Logic.Hero; its method that recalculates the totals
        "herologic.experience", "experience.add", // hero logic → experience logic; its method that adds experience
        "hero.levelUpPool", "levelups.add",     // hero → its pending level-ups; the pool's method that adds one
        "herologic.levelUps", "levelups.register", "levelups.show", // hero logic → level-up logic; register one; show
        "skilllogic.pickSub",        // method of a skill logic: activate one of its sub-skills
        "skilllogic.list", "skilllogic.sid", // skills logic → skill logics; skill logic → sid
        "skills.learn",              // method of the skills logic: build and apply the logic of a skill entry
        "skilllogic.levelUp",        // method of a skill logic: raise the skill by one level
        "hero.magics", "magic.sid", "magic.learned", // hero → its spells; spell → sid / learned
        "herologic.magics",          // hero logic → its magic logic (one spell logic per spell)
        "magiclogic.list", "magiclogic.sid", // magic logic → spell logics; spell logic → sid
        "magics.learn", "magics.forget", // methods of the magic logic: learn a spell by sid / remove a spell logic
        "magics.variant",            // method of the magic logic: the hero's "_special" variant of a spell sid
        "hero.doll", "hero.backpack", // hero → its worn items / backpack (item containers)
        "container.slots", "slot.type", "slot.items", // container → slots; slot → type / item ids (-1 empty)
        "item.list", "item.id", "item.sid", "item.slotType", // root → items; item → id / sid / the slot type it is worn on
        "herologic.doll", "herologic.backpack", // hero logic → item logic of the doll / backpack
        "items.add", "items.move", "items.remove", // item logic: add by sid (backpack), move to a slot, remove at a slot
        "ui.message", "ui.question",
        "turn.start",                // method: start of a day (Harmony postfix → PlayerDayStarted for every player)
        "object.interact",           // method: hero interacts with a map object (prefix/postfix → OB triggers)
        "battle.start", "battle.end", "battle.round", "battle.action",
        "buff.apply",                // method: apply buff by id to a unit
        "save.write", "save.read",   // methods: save/load (side-car WoG state)
        "hero.position", "hero.move", "hero.skills", "hero.kill",
        "stack.create", "player.activeHero", "player.heroes",
        "map.objects", "battle.stacks", "battle.summon",
        "town.class",                // the session object type of a city (ObjCity)
        "object.sid",                // session object → its config sid ("human_city")
        "town.name",                 // city → its name (a localization key)
        "town.garrisonHero", "town.visitorHero", // city → Olden Era hero id in the garrison / visiting (-1 none)
        "town.buildings",            // city → all its buildings (BuildingData)
        "building.sid", "building.level", "building.built", "building.bans", // building → sid / level / constructed / bans per level
        "town.hires", "hire.sets",   // city → its dwellings; dwelling → its unit sets
        "unitset.amount", "unitset.growth", // unit set → creatures to hire / weekly growth
        "town.garrison",             // city → the units of its own garrison
        "town.builtToday",           // city → constructions made today
        "world.objectLogics",        // static path to the logic objects of the map objects, by map object id
        "object.setOwner",           // method of a map object's logic: change its owner (side id, -1 neutral)
        "citylogic.buildings",       // city logic → its buildings logic
        "citylogic.income",          // method of the city logic: daily income by resource name
        "citylogic.guildSpells",     // method of the city logic: the spell sids of a mage guild level (1..5)
        "buildings.construct",       // method of the buildings logic: construct (sid, level)
        "loc.text",                  // static method: the localized text of a key
        "loc.entries",               // static path to the localization table (key → entry)
        "locentry.key", "locentry.text", "locentry.args", // localization entry → key / text / format arguments
        "hero.typeConfig",           // hero → its type config (HeroConfig)
        "heroconfig.nameKey", "heroconfig.bioKey", // hero type config → localization keys of its name / biography
        "heroconfig.fraction", "heroconfig.classType", // hero type config → faction / might or magic
        "hero.specialization",       // hero → its specialization config
        "heroconfig.startSquad",     // hero type config → the army it is hired with
        "squadslot.sid", "squadslot.min", "squadslot.max", // start squad entry → unit sid / minimum / maximum
        "spec.bonuses", "bonus.type", "bonus.parameters", // specialization → bonuses; bonus → type / parameters
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
        "battle.logic",              // static path to the logic of the battle being fought
        "battle.objects", "battle.current", // battle logic → its field objects (units, obstacles) / the unit whose turn it is
        "bunit.class",               // the type of a battle unit
        "bunit.sid", "bunit.side",   // battle unit → unit sid / side (0 attacker, 1 defender)
        "bunit.data", "bunit.army",  // battle unit → its battle data (count, top hit points) / the army stack it came from
        "bunit.stats", "bunit.mods", // battle unit → its total stats / its battle modifier
        "bunit.config",              // battle unit → its type config (abilities, attacks)
        "battle.events",             // battle logic → its event bus (fields of EventHandler<BattleEventArgs>)
        "battleevent.roundStart", "battleevent.turnStart", "battleevent.turnEnd", // events: a round / a unit's turn starts, a turn ends
        "battleevent.move", "battleevent.cast", "battleevent.castEnd", // events: a unit starts moving, uses an ability or attack, it ends
        "battleevent.defend", "battleevent.wait", // events: a unit skips its turn (H3's defend), waits
        "battleevent.magicStart", "battleevent.magicEnd", // events: a hero's spell starts, ends
        "battleevent.damage",        // event: a unit took damage
        "input.hotkeys",             // static: whether the game reacts to its hotkeys (off while text is typed)
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
        if (Pairs(collection) is { } pairs) return pairs.Select(p => p.Value).ToList();
        var t = collection.GetType();
        var count = t.GetProperty("Count");
        var item = t.GetMethod("get_Item", new[] { typeof(int) });
        if (count == null || item == null) throw new InvalidOperationException($"{t.FullName} is not a collection");
        int n = Convert.ToInt32(count.GetValue(collection));
        var list = new List<object?>(n);
        for (int i = 0; i < n; i++) list.Add(item.Invoke(collection, new object[] { i }));
        return list;
    }

    /// <summary>
    /// The key/value pairs of an IL2CPP dictionary (its enumerator's KeyValuePairs), or null when the object is not
    /// one. Its get_Item takes a key, not an index, so <see cref="Items"/> gives its values.
    /// </summary>
    public static List<(object? Key, object? Value)>? Pairs(object collection)
    {
        var t = collection.GetType();
        if (t.GetMethod("ContainsKey") == null || t.GetMethod("GetEnumerator", Type.EmptyTypes) is not { } get) return null;
        var en = get.Invoke(collection, null)!;
        var move = en.GetType().GetMethod("MoveNext", Type.EmptyTypes)!;
        var current = en.GetType().GetProperty("Current")!;
        var pairs = new List<(object?, object?)>();
        while ((bool)move.Invoke(en, null)!)
        {
            var kv = current.GetValue(en)!;
            pairs.Add((kv.GetType().GetProperty("Key")!.GetValue(kv), kv.GetType().GetProperty("Value")!.GetValue(kv)));
        }
        return pairs;
    }
}
