using System;
using System.Collections.Generic;
using System.Linq;
using WoG.Core.Adapters;
using WoG.Core.Model;

namespace WoG.Headless;

/// <summary>
/// A minimal in-memory game that behaves like H3 where WoG relies on it. It is the "reference engine"
/// for regression tests: the same scenario run here and on Olden Era must give the same WoG results.
/// </summary>
public sealed class HeadlessGame : IGameAdapter, IHeroAdapter, IPlayerAdapter, ICreatureTypeAdapter, IMapAdapter,
    ITownAdapter, IUiAdapter, IBattleAdapter, IGameClock
{
    public string EngineName => "headless";
    public IHeroAdapter Heroes => this;
    public IPlayerAdapter Players => this;
    public ICreatureTypeAdapter Creatures => this;
    public IMapAdapter Map => this;
    public ITownAdapter Towns => this;
    public IUiAdapter Ui => this;
    public IBattleAdapter Battle => this;
    public IGameClock Clock => this;

    public Dictionary<int, WoGHero> HeroList { get; } = new();
    public Dictionary<int, WoGCreature> CreatureList { get; } = new();
    public Dictionary<int, WoGTown> TownList { get; } = new();
    public Dictionary<int, WoGMapObject> Objects { get; } = new();
    /// <summary>Map width (square) and H3 levels (0 = surface only, 1 = with underground).</summary>
    public int MapSize { get; set; } = 72;
    public int MapLevels { get; set; } = 1;
    /// <summary>Terrain per square (Format TR); missing squares are grass.</summary>
    public Dictionary<int, int> Terrain { get; } = new();
    /// <summary>Squares covered by objects besides their entrance (object position), key = MapPos.Pack().</summary>
    public Dictionary<int, MapPos> Blocked { get; } = new();
    public int[,] Resources { get; } = new int[WoGLimits.PlayerCount, WoGLimits.ResourceCount];
    public bool[] Human { get; } = new bool[WoGLimits.PlayerCount];
    public bool[] Local { get; } = new bool[WoGLimits.PlayerCount];
    public bool[] Alive { get; } = Enumerable.Repeat(true, WoGLimits.PlayerCount).ToArray();
    public int[] ActiveHero { get; } = Enumerable.Repeat(-1, WoGLimits.PlayerCount).ToArray();
    public int CurrentPlayer { get; set; }
    public int AbsoluteDay { get; set; } = 1;
    public int DayOfWeek => (AbsoluteDay - 1) % 7 + 1;
    public int Week => (AbsoluteDay - 1) / 7 % 4 + 1;
    public int Month => (AbsoluteDay - 1) / 28 + 1;
    public List<string> Messages { get; } = new();
    public Queue<bool> Answers { get; } = new();
    public WoGBattle? CurrentBattle { get; set; }

    public HeadlessGame()
    {
        Human[0] = true;
        Local[0] = true;
    }

    public WoGHero AddHero(int id, int owner, int heroClass = 0, string name = "")
    {
        var h = new WoGHero { Id = id, Owner = owner, HeroClass = heroClass, Name = name.Length > 0 ? name : $"Hero{id}" };
        HeroList[id] = h;
        return h;
    }

    public WoGCreature AddCreature(int id, int level, int hp = 10, int attack = 5, int defence = 5)
    {
        var c = new WoGCreature { Id = id, Level = level, HitPoints = hp, Attack = attack, Defence = defence };
        CreatureList[id] = c;
        return c;
    }

    static AdapterResult<T> NoHero<T>(int h) => AdapterResult<T>.Failed($"hero {h} does not exist");

    // ---- heroes -----------------------------------------------------------------------------

    public bool Exists(int hero) => HeroList.ContainsKey(hero);

    public AdapterResult<int> Get(int hero, HeroStat stat)
    {
        if (!HeroList.TryGetValue(hero, out var h)) return NoHero<int>(hero);
        return AdapterResult<int>.Ok(stat switch
        {
            HeroStat.Experience => h.Experience,
            HeroStat.Level => h.Level,
            HeroStat.Attack => h.Primary[0],
            HeroStat.Defence => h.Primary[1],
            HeroStat.Power => h.Primary[2],
            HeroStat.Knowledge => h.Primary[3],
            HeroStat.Mana => h.Mana,
            HeroStat.Movement => h.Movement,
            HeroStat.Owner => h.Owner,
            HeroStat.HeroClass => h.HeroClass,
            _ => 0,
        });
    }

    public AdapterResult Set(int hero, HeroStat stat, int value)
    {
        if (!HeroList.TryGetValue(hero, out var h)) return AdapterResult.Failed($"hero {hero} does not exist");
        switch (stat)
        {
            case HeroStat.Experience: h.Experience = value; break;
            case HeroStat.Level: h.Level = value; break;
            case HeroStat.Attack: h.Primary[0] = value; break;
            case HeroStat.Defence: h.Primary[1] = value; break;
            case HeroStat.Power: h.Primary[2] = value; break;
            case HeroStat.Knowledge: h.Primary[3] = value; break;
            case HeroStat.Mana: h.Mana = value; break;
            case HeroStat.Movement: h.Movement = value; break;
            case HeroStat.Owner: h.Owner = value; break;
            case HeroStat.HeroClass: h.HeroClass = value; break;
        }
        return AdapterResult.Ok;
    }

    public AdapterResult<int> GetBase(int hero, HeroStat stat) => Get(hero, stat);

    public AdapterResult<MapPos> GetPosition(int hero) =>
        HeroList.TryGetValue(hero, out var h) ? AdapterResult<MapPos>.Ok(h.Position) : NoHero<MapPos>(hero);

    public AdapterResult MoveTo(int hero, MapPos pos, bool withEffect)
    {
        if (!HeroList.TryGetValue(hero, out var h)) return AdapterResult.Failed($"hero {hero} does not exist");
        h.Position = pos;
        return AdapterResult.Ok;
    }

    public AdapterResult<int> HeroAt(MapPos pos)
    {
        foreach (var h in HeroList.Values) if (h.Position == pos) return AdapterResult<int>.Ok(h.Id);
        return AdapterResult<int>.Failed($"no hero at {pos}");
    }

    public AdapterResult<int> GetSecondarySkill(int hero, int skill) =>
        HeroList.TryGetValue(hero, out var h) ? AdapterResult<int>.Ok(h.SecondarySkills[skill]) : NoHero<int>(hero);

    public AdapterResult SetSecondarySkill(int hero, int skill, int level)
    {
        if (!HeroList.TryGetValue(hero, out var h)) return AdapterResult.Failed("no hero");
        h.SecondarySkills[skill] = level;
        return AdapterResult.Ok;
    }

    public AdapterResult<bool> HasSpell(int hero, int spell) =>
        HeroList.TryGetValue(hero, out var h) ? AdapterResult<bool>.Ok(h.Spells.Contains(spell)) : NoHero<bool>(hero);

    public AdapterResult SetSpell(int hero, int spell, bool known)
    {
        if (!HeroList.TryGetValue(hero, out var h)) return AdapterResult.Failed("no hero");
        if (known) h.Spells.Add(spell); else h.Spells.Remove(spell);
        return AdapterResult.Ok;
    }

    public AdapterResult<WoGStack> GetStack(int hero, int slot) =>
        HeroList.TryGetValue(hero, out var h) ? AdapterResult<WoGStack>.Ok(h.Army.Slots[slot].Clone()) : NoHero<WoGStack>(hero);

    public AdapterResult SetStack(int hero, int slot, int type, int count)
    {
        if (!HeroList.TryGetValue(hero, out var h)) return AdapterResult.Failed("no hero");
        h.Army.Slots[slot].Type = count <= 0 ? -1 : type;
        h.Army.Slots[slot].Count = type < 0 ? 0 : Math.Max(0, count);
        return AdapterResult.Ok;
    }

    public AdapterResult<int> CountArtifact(int hero, int artifact)
    {
        if (!HeroList.TryGetValue(hero, out var h)) return NoHero<int>(hero);
        return AdapterResult<int>.Ok(h.Equipped.Count(a => a == artifact) + h.Backpack.Count(a => a == artifact));
    }

    public AdapterResult AddArtifact(int hero, int artifact, int slot)
    {
        if (!HeroList.TryGetValue(hero, out var h)) return AdapterResult.Failed("no hero");
        if (slot >= 0 && slot < WoGLimits.ArtifactSlots)
        {
            if (h.Equipped[slot] != -1) return AdapterResult.Failed("slot busy");
            h.Equipped[slot] = artifact;
        }
        else h.Backpack.Add(artifact);
        return AdapterResult.Ok;
    }

    public AdapterResult<int> RemoveArtifact(int hero, int artifact, int count)
    {
        if (!HeroList.TryGetValue(hero, out var h)) return NoHero<int>(hero);
        int removed = 0;
        while (removed < count && h.Backpack.Remove(artifact)) removed++;
        for (int i = 0; i < h.Equipped.Length && removed < count; i++)
            if (h.Equipped[i] == artifact) { h.Equipped[i] = -1; removed++; }
        return AdapterResult<int>.Ok(removed);
    }

    public AdapterResult<string> GetName(int hero) =>
        HeroList.TryGetValue(hero, out var h) ? AdapterResult<string>.Ok(h.Name) : NoHero<string>(hero);

    public AdapterResult SetName(int hero, string name)
    {
        if (!HeroList.TryGetValue(hero, out var h)) return AdapterResult.Failed("no hero");
        h.Name = name;
        return AdapterResult.Ok;
    }

    public AdapterResult Kill(int hero)
    {
        if (!HeroList.TryGetValue(hero, out var h)) return AdapterResult.Failed("no hero");
        h.Alive = false;
        h.Owner = -1;
        return AdapterResult.Ok;
    }

    // ---- players ----------------------------------------------------------------------------

    public AdapterResult<int> GetResource(int player, int resource) => AdapterResult<int>.Ok(Resources[player, resource]);

    public AdapterResult SetResource(int player, int resource, int value)
    {
        Resources[player, resource] = value;
        return AdapterResult.Ok;
    }

    public AdapterResult<bool> IsHuman(int player) => AdapterResult<bool>.Ok(Human[player]);
    public AdapterResult<bool> IsLocal(int player) => AdapterResult<bool>.Ok(Local[player]);
    public AdapterResult<bool> IsAlive(int player) => AdapterResult<bool>.Ok(Alive[player]);
    public AdapterResult<int> GetActiveHero(int player) => AdapterResult<int>.Ok(ActiveHero[player]);

    public AdapterResult<IReadOnlyList<int>> GetHeroes(int player) =>
        AdapterResult<IReadOnlyList<int>>.Ok(HeroList.Values.Where(h => h.Owner == player).Select(h => h.Id).ToList());

    // ---- creature types ---------------------------------------------------------------------

    bool ICreatureTypeAdapter.Exists(int type) => CreatureList.ContainsKey(type);

    public AdapterResult<int> Get(int type, CreatureStat stat)
    {
        if (!CreatureList.TryGetValue(type, out var c)) return AdapterResult<int>.Failed($"creature {type} unknown");
        return AdapterResult<int>.Ok(stat switch
        {
            CreatureStat.Attack => c.Attack, CreatureStat.Defence => c.Defence, CreatureStat.HitPoints => c.HitPoints,
            CreatureStat.Speed => c.Speed, CreatureStat.DamageLow => c.DamageLow, CreatureStat.DamageHigh => c.DamageHigh,
            CreatureStat.Shots => c.Shots, CreatureStat.Casts => c.Casts, CreatureStat.Growth => c.Growth,
            CreatureStat.HordeGrowth => c.HordeGrowth, CreatureStat.FightValue => c.FightValue, CreatureStat.AiValue => c.AiValue,
            CreatureStat.AdvMapLow => c.AdvMapLow, CreatureStat.AdvMapHigh => c.AdvMapHigh, CreatureStat.Level => c.Level,
            CreatureStat.Town => c.Town, CreatureStat.UpgradeTo => c.UpgradeTo, CreatureStat.Flags => unchecked((int)c.Flags),
            _ => 0,
        });
    }

    public AdapterResult Set(int type, CreatureStat stat, int v)
    {
        if (!CreatureList.TryGetValue(type, out var c)) return AdapterResult.Failed($"creature {type} unknown");
        switch (stat)
        {
            case CreatureStat.Attack: c.Attack = v; break;
            case CreatureStat.Defence: c.Defence = v; break;
            case CreatureStat.HitPoints: c.HitPoints = v; break;
            case CreatureStat.Speed: c.Speed = v; break;
            case CreatureStat.DamageLow: c.DamageLow = v; break;
            case CreatureStat.DamageHigh: c.DamageHigh = v; break;
            case CreatureStat.Shots: c.Shots = v; break;
            case CreatureStat.Casts: c.Casts = v; break;
            case CreatureStat.Growth: c.Growth = v; break;
            case CreatureStat.HordeGrowth: c.HordeGrowth = v; break;
            case CreatureStat.FightValue: c.FightValue = v; break;
            case CreatureStat.AiValue: c.AiValue = v; break;
            case CreatureStat.AdvMapLow: c.AdvMapLow = v; break;
            case CreatureStat.AdvMapHigh: c.AdvMapHigh = v; break;
            case CreatureStat.Level: c.Level = v; break;
            case CreatureStat.Town: c.Town = v; break;
            case CreatureStat.UpgradeTo: c.UpgradeTo = v; break;
            case CreatureStat.Flags: c.Flags = unchecked((uint)v); break;
        }
        return AdapterResult.Ok;
    }

    public AdapterResult<int> GetCost(int type, int resource) =>
        CreatureList.TryGetValue(type, out var c) ? AdapterResult<int>.Ok(c.Cost[resource]) : AdapterResult<int>.Failed("unknown creature");

    public AdapterResult SetCost(int type, int resource, int value)
    {
        if (!CreatureList.TryGetValue(type, out var c)) return AdapterResult.Failed("unknown creature");
        c.Cost[resource] = value;
        return AdapterResult.Ok;
    }

    // ---- map / towns ------------------------------------------------------------------------

    public AdapterResult<(int Size, int Levels)> GetSize() => AdapterResult<(int, int)>.Ok((MapSize, MapLevels));

    public AdapterResult<IReadOnlyList<WoGMapObject>> GetObjects() =>
        AdapterResult<IReadOnlyList<WoGMapObject>>.Ok(Objects.Values.OrderBy(o => o.Position.L).ThenBy(o => o.Position.Y).ThenBy(o => o.Position.X).ToList());

    public AdapterResult<MapSquare> GetSquare(MapPos pos)
    {
        if (pos.X < 0 || pos.Y < 0 || pos.X >= MapSize || pos.Y >= MapSize || pos.L < 0 || pos.L > MapLevels)
            return AdapterResult<MapSquare>.Failed($"square {pos} is outside the map");
        int land = Terrain.TryGetValue(pos.Pack(), out int t) ? t : 2;
        if (Objects.TryGetValue(pos.Pack(), out var o))
            return AdapterResult<MapSquare>.Ok(new MapSquare(o.Type, o.SubType, true, true, land, 0, o.Position));
        if (Blocked.TryGetValue(pos.Pack(), out var owner) && Objects.TryGetValue(owner.Pack(), out var b))
            return AdapterResult<MapSquare>.Ok(new MapSquare(b.Type, b.SubType, false, true, land, 0, b.Position));
        return AdapterResult<MapSquare>.Ok(MapSquare.Empty(land));
    }

    public AdapterResult<(int type, int subtype)> GetObjectAt(MapPos pos) =>
        Objects.TryGetValue(pos.Pack(), out var o) ? AdapterResult<(int, int)>.Ok((o.Type, o.SubType)) : AdapterResult<(int, int)>.Failed("no object");

    public AdapterResult<int> GetObjectOwner(MapPos pos) =>
        Objects.TryGetValue(pos.Pack(), out var o) ? AdapterResult<int>.Ok(o.Owner) : AdapterResult<int>.Failed("no object");

    public AdapterResult SetObjectOwner(MapPos pos, int owner)
    {
        if (!Objects.TryGetValue(pos.Pack(), out var o)) return AdapterResult.Failed("no object");
        o.Owner = owner;
        return AdapterResult.Ok;
    }

    public AdapterResult<int> TownAt(MapPos pos)
    {
        foreach (var t in TownList.Values) if (t.Position == pos) return AdapterResult<int>.Ok(t.Id);
        return AdapterResult<int>.Failed("no town");
    }

    public AdapterResult<int> GetMageGuildLevel(int town) =>
        TownList.TryGetValue(town, out var t) ? AdapterResult<int>.Ok(t.MageGuildLevel) : AdapterResult<int>.Failed("no town");

    public AdapterResult<bool> IsBuilt(int town, int building) =>
        TownList.TryGetValue(town, out var t) ? AdapterResult<bool>.Ok(t.Buildings.TryGetValue(building, out var b) && b.Built) : AdapterResult<bool>.Failed("no town");

    public AdapterResult SetBuilt(int town, int building, bool built)
    {
        if (!TownList.TryGetValue(town, out var t)) return AdapterResult.Failed("no town");
        if (!t.Buildings.TryGetValue(building, out var b)) t.Buildings[building] = b = new WoGBuilding { Id = building };
        b.Built = built;
        return AdapterResult.Ok;
    }

    // ---- UI ---------------------------------------------------------------------------------

    public AdapterResult ShowMessage(string text)
    {
        Messages.Add(text);
        return AdapterResult.Ok;
    }

    public AdapterResult<bool> AskYesNo(string text)
    {
        Messages.Add("?" + text);
        return AdapterResult<bool>.Ok(Answers.Count > 0 ? Answers.Dequeue() : true);
    }

    // ---- battle -----------------------------------------------------------------------------

    public bool InBattle => CurrentBattle != null;

    public AdapterResult<int> GetHero(int side) =>
        CurrentBattle != null ? AdapterResult<int>.Ok(CurrentBattle.Heroes[side]) : AdapterResult<int>.Failed("not in battle");

    public AdapterResult<int> StackCount() =>
        CurrentBattle != null ? AdapterResult<int>.Ok(CurrentBattle.Stacks.Count) : AdapterResult<int>.Failed("not in battle");

    public AdapterResult<int> GetStack(int i, BattleStackStat stat)
    {
        if (CurrentBattle == null || i < 0 || i >= CurrentBattle.Stacks.Count) return AdapterResult<int>.Failed("no stack");
        var s = CurrentBattle.Stacks[i];
        return AdapterResult<int>.Ok(stat switch
        {
            BattleStackStat.Type => s.Type, BattleStackStat.Count => s.Count, BattleStackStat.Attack => s.Attack,
            BattleStackStat.Defence => s.Defence, BattleStackStat.HitPoints => s.HitPoints, BattleStackStat.HitPointsLost => s.HitPointsLost,
            BattleStackStat.Speed => s.Speed, BattleStackStat.DamageLow => s.DamageLow, BattleStackStat.DamageHigh => s.DamageHigh,
            BattleStackStat.Shots => s.Shots, BattleStackStat.Casts => s.Casts, BattleStackStat.Retaliations => s.Retaliations,
            BattleStackStat.Flags => unchecked((int)s.Flags), BattleStackStat.Position => s.Position, BattleStackStat.Side => s.Side,
            _ => 0,
        });
    }

    public AdapterResult SetStack(int i, BattleStackStat stat, int v)
    {
        if (CurrentBattle == null || i < 0 || i >= CurrentBattle.Stacks.Count) return AdapterResult.Failed("no stack");
        var s = CurrentBattle.Stacks[i];
        switch (stat)
        {
            case BattleStackStat.Type: s.Type = v; break;
            case BattleStackStat.Count: s.Count = v; break;
            case BattleStackStat.Attack: s.Attack = v; break;
            case BattleStackStat.Defence: s.Defence = v; break;
            case BattleStackStat.HitPoints: s.HitPoints = v; break;
            case BattleStackStat.HitPointsLost: s.HitPointsLost = v; break;
            case BattleStackStat.Speed: s.Speed = v; break;
            case BattleStackStat.DamageLow: s.DamageLow = v; break;
            case BattleStackStat.DamageHigh: s.DamageHigh = v; break;
            case BattleStackStat.Shots: s.Shots = v; break;
            case BattleStackStat.Casts: s.Casts = v; break;
            case BattleStackStat.Retaliations: s.Retaliations = v; break;
            case BattleStackStat.Flags: s.Flags = unchecked((uint)v); break;
            case BattleStackStat.Position: s.Position = v; break;
        }
        return AdapterResult.Ok;
    }

    public AdapterResult ApplyBuff(int stackIndex, string buffId) => AdapterResult.Unsupported("headless engine has no buff system");

    public AdapterResult<int> SummonUnit(int side, string unitKey, int count)
    {
        if (CurrentBattle == null) return AdapterResult<int>.Failed("not in battle");
        CurrentBattle.Stacks.Add(new WoGBattleStack { Index = CurrentBattle.Stacks.Count, Side = side, Count = count, Type = -1 });
        return AdapterResult<int>.Ok(CurrentBattle.Stacks.Count - 1);
    }
}

/// <summary>Visual probe for headless runs: nothing is available, so everything resolves to placeholders.</summary>
public sealed class NoAssets : WoG.Core.Visual.IVisualAssetProbe
{
    public bool IsAvailable(WoG.Core.Visual.VisualCandidate candidate) => false;
}
