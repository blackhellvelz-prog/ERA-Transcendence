using System;
using System.Collections.Generic;
using WoG.Core.Adapters;
using WoG.Core.Model;
using WoG.Core.State;

namespace WoG.Core.H3Data;

/// <summary>
/// Creature types as MA sees them. A creature the engine has (through the id map) is read and changed in the engine's
/// own unit data; an H3/WoG creature the engine lacks reads the ERA installation's creature table (zcrtrait.txt) —
/// changes to it are only kept, nothing in the game uses it. Every change is recorded in the WoG state, so it is saved
/// with the game and applied to the engine again after loading; the engine's original values come back for a new game
/// (the engine's unit data lives as long as the game process, not as long as one game).
/// </summary>
public sealed class CreatureTable
{
    readonly Func<IGameAdapter> game;
    readonly Func<WoGGameState> state;
    readonly Func<H3Tables> h3;
    readonly Dictionary<(int Type, string Key), int> originals = new();

    public CreatureTable(Func<IGameAdapter> game, Func<WoGGameState> state, Func<H3Tables> h3)
    {
        this.game = game;
        this.state = state;
        this.h3 = h3;
    }

    static string CostKey(int resource) => "Cost" + resource;

    ICreatureTypeAdapter Engine => game().Creatures;
    Dictionary<int, Dictionary<string, int>> Changes => state().CreatureChanges;

    /// <summary>The engine has the creature (MA changes act on the game).</summary>
    public bool InEngine(int type) => Engine.Exists(type);

    public bool Exists(int type) => InEngine(type) || (type >= 0 && type < h3().Creatures.Count);

    bool Changed(int type, string key, out int value)
    {
        value = 0;
        return Changes.TryGetValue(type, out var c) && c.TryGetValue(key, out value);
    }

    void Record(int type, string key, int value)
    {
        if (!Changes.TryGetValue(type, out var c)) Changes[type] = c = new Dictionary<string, int>();
        c[key] = value;
    }

    public AdapterResult<int> Get(int type, CreatureStat stat)
    {
        if (InEngine(type)) return Engine.Get(type, stat);
        if (Changed(type, stat.ToString(), out int v)) return AdapterResult<int>.Ok(v);
        var t = TableRow(type);
        if (t == null) return AdapterResult<int>.Failed($"wrong monster type {type}");
        int? value = stat switch
        {
            CreatureStat.Attack => t.Attack, CreatureStat.Defence => t.Defence, CreatureStat.HitPoints => t.HitPoints,
            CreatureStat.Speed => t.Speed, CreatureStat.DamageLow => t.DamageLow, CreatureStat.DamageHigh => t.DamageHigh,
            CreatureStat.Shots => t.Shots, CreatureStat.Casts => t.Casts, CreatureStat.Growth => t.Growth,
            CreatureStat.HordeGrowth => t.HordeGrowth, CreatureStat.FightValue => t.FightValue, CreatureStat.AiValue => t.AiValue,
            CreatureStat.AdvMapLow => t.AdvMapLow, CreatureStat.AdvMapHigh => t.AdvMapHigh,
            _ => null,
        };
        return value is int x ? AdapterResult<int>.Ok(x)
            : AdapterResult<int>.Unsupported($"creature {type} is not in the engine, and its {stat} is not in the text tables");
    }

    public AdapterResult Set(int type, CreatureStat stat, int value)
    {
        string key = stat.ToString();
        if (InEngine(type))
        {
            var before = Engine.Get(type, stat);
            var r = Engine.Set(type, stat, value);
            if (r.Status != AdapterStatus.Ok) return r;
            if (before.Status == AdapterStatus.Ok) originals.TryAdd((type, key), before.Value);
            Record(type, key, value);
            return r;
        }
        var g = Get(type, stat);
        if (g.Status != AdapterStatus.Ok) return g.AsPlain();
        Record(type, key, value);
        return AdapterResult.Ok;
    }

    public AdapterResult<int> GetCost(int type, int resource)
    {
        if (InEngine(type)) return Engine.GetCost(type, resource);
        if (Changed(type, CostKey(resource), out int v)) return AdapterResult<int>.Ok(v);
        var t = TableRow(type);
        return t == null ? AdapterResult<int>.Failed($"wrong monster type {type}") : AdapterResult<int>.Ok(t.Cost[resource]);
    }

    public AdapterResult SetCost(int type, int resource, int value)
    {
        string key = CostKey(resource);
        if (InEngine(type))
        {
            var before = Engine.GetCost(type, resource);
            var r = Engine.SetCost(type, resource, value);
            if (r.Status != AdapterStatus.Ok) return r;
            if (before.Status == AdapterStatus.Ok) originals.TryAdd((type, key), before.Value);
            Record(type, key, value);
            return r;
        }
        if (TableRow(type) == null) return AdapterResult.Failed($"wrong monster type {type}");
        Record(type, key, value);
        return AdapterResult.Ok;
    }

    WoGCreature? TableRow(int type) => type >= 0 && type < h3().Creatures.Count ? h3().Creatures[type] : null;

    /// <summary>Puts the engine's original values back (before a new game or a loaded one).</summary>
    public void Restore()
    {
        foreach (var ((type, key), value) in originals) Apply(type, key, value);
        originals.Clear();
    }

    /// <summary>Applies the recorded changes of the current WoG state to the engine (after loading a game).</summary>
    public void Reapply()
    {
        foreach (var (type, changes) in Changes)
        {
            if (!InEngine(type)) continue;
            foreach (var (key, value) in changes)
            {
                int? before = Read(type, key);
                if (before is int b) originals.TryAdd((type, key), b);
                Apply(type, key, value);
            }
        }
    }

    int? Read(int type, string key)
    {
        var r = key.StartsWith("Cost") ? Engine.GetCost(type, int.Parse(key[4..]))
            : Enum.TryParse<CreatureStat>(key, out var s) ? Engine.Get(type, s) : AdapterResult<int>.Failed("");
        return r.Status == AdapterStatus.Ok ? r.Value : null;
    }

    void Apply(int type, string key, int value)
    {
        if (key.StartsWith("Cost")) Engine.SetCost(type, int.Parse(key[4..]), value);
        else if (Enum.TryParse<CreatureStat>(key, out var s)) Engine.Set(type, s, value);
    }
}
