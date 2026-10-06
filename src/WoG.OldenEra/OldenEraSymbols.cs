using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace WoG.OldenEra;

/// <summary>
/// Where to find a piece of game state: a type (namespace-qualified, possibly obfuscated), and a member
/// path from a static root ("Instance.heroes") or from an object of that type ("offence").
/// </summary>
public sealed class SymbolBinding
{
    public string Assembly { get; set; } = "Hex";
    public string Type { get; set; } = "";
    public string Member { get; set; } = "";
    /// <summary>"verified" once confirmed in game; anything else keeps the dependent feature disabled.</summary>
    public string Status { get; set; } = "unverified";
}

/// <summary>
/// All Olden Era symbols the adapter needs, loaded from BepInEx/config/wog_symbols.json (produced by
/// the in-game RE step, see OldenEra_ReverseEngineering/07_InGame_RE_Plan.md). Resolution is done once at
/// start-up; every missing symbol is logged (the gme-mod pattern) and only its features are disabled.
/// No obfuscated name is hard-coded in the plugin, so a game update only needs a new JSON file.
/// </summary>
public sealed class OldenEraSymbols
{
    public Dictionary<string, SymbolBinding> Bindings { get; set; } = new();

    readonly Dictionary<string, (Type type, MemberInfo? member)> resolved = new();
    readonly List<string> missing = new();

    public IReadOnlyList<string> Missing => missing;

    /// <summary>Symbol keys the adapter understands (documented in 07_InGame_RE_Plan.md §2).</summary>
    public static readonly string[] Known =
    {
        "game.root",                 // static instance holding the game state
        "game.day",                  // absolute day counter on the root
        "player.list", "player.resources", "player.isHuman", "player.current",
        "hero.list", "hero.id", "hero.owner", "hero.experience", "hero.level",
        "hero.offence", "hero.defence", "hero.spellPower", "hero.intelligence", "hero.mana", "hero.movement",
        "hero.army", "stack.unitSid", "stack.count",
        "unit.db", "unit.stats",
        "ui.message", "ui.question",
        "turn.start",                // method: start of a player's turn (Harmony postfix → PlayerDayStarted)
        "object.interact",           // method: hero interacts with a map object (prefix/postfix → OB triggers)
        "battle.start", "battle.end", "battle.round", "battle.action",
        "buff.apply",                // method: apply buff by id to a unit
        "save.write", "save.read",   // methods: save/load (side-car WoG state)
        "hero.position", "hero.move", "hero.skills", "hero.spells", "hero.items", "hero.name", "hero.kill",
        "stack.create", "player.alive", "player.activeHero", "player.heroes",
        "map.objects", "town.list", "town.buildings", "battle.stacks", "battle.summon",
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

    /// <summary>Resolves every binding against the loaded (interop) assemblies.</summary>
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
            MemberInfo? member = null;
            if (!string.IsNullOrEmpty(b.Member))
            {
                const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
                member = type.GetMember(b.Member, all).FirstOrDefault();
                if (member == null) { missing.Add($"{key}: member {b.Type}.{b.Member} not found"); continue; }
            }
            if (b.Status != "verified") log($"WoG symbol {key} resolved but not verified in game — feature stays disabled");
            resolved[key] = (type, member);
        }
        foreach (var m in missing) log("Game API not found: " + m);
    }

    /// <summary>True only for resolved and in-game-verified symbols.</summary>
    public bool Has(string key) =>
        resolved.ContainsKey(key) && Bindings.TryGetValue(key, out var b) && b.Status == "verified";

    public Type TypeOf(string key) => resolved[key].type;
    public MemberInfo? MemberOf(string key) => resolved[key].member;

    /// <summary>Reads a field/property value (static when target is null).</summary>
    public object? Read(string key, object? target)
    {
        var m = MemberOf(key);
        return m switch
        {
            FieldInfo f => f.GetValue(target),
            PropertyInfo p => p.GetValue(target),
            _ => throw new InvalidOperationException($"{key} is not a field or property"),
        };
    }

    public void Write(string key, object? target, object? value)
    {
        switch (MemberOf(key))
        {
            case FieldInfo f: f.SetValue(target, Convert.ChangeType(value, f.FieldType)); break;
            case PropertyInfo p: p.SetValue(target, Convert.ChangeType(value, p.PropertyType)); break;
            default: throw new InvalidOperationException($"{key} is not a field or property");
        }
    }
}
