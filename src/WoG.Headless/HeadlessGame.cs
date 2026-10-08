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
    public bool RandomMap { get; set; }
    /// <summary>Team per player (default: each player is its own team).</summary>
    public int[] Teams { get; } = Enumerable.Range(0, WoGLimits.PlayerCount).ToArray();
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
        // WoG HE:S#/$: a learned skill takes the next display slot (while fewer than 8 are shown); a removed one
        // gives its slot to the skill in the last slot.
        var order = h.SkillOrder;
        int at = order.IndexOf(skill);
        if (level == 0 && at >= 0)
        {
            order[at] = order[^1];
            order.RemoveAt(order.Count - 1);
        }
        else if (level != 0 && at < 0 && order.Count < 8) order.Add(skill);
        return AdapterResult.Ok;
    }

    public AdapterResult<IReadOnlyList<int>> GetSecondarySkillOrder(int hero) =>
        HeroList.TryGetValue(hero, out var h) ? AdapterResult<IReadOnlyList<int>>.Ok(h.SkillOrder.ToList()) : NoHero<IReadOnlyList<int>>(hero);

    public AdapterResult SetSecondarySkillOrder(int hero, IReadOnlyList<int> order)
    {
        if (!HeroList.TryGetValue(hero, out var h)) return AdapterResult.Failed("no hero");
        h.SkillOrder = order.ToList();
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

    /// <summary>Where an artifact is worn ("Format P2"), for HE:A4; null = everything goes to the backpack.</summary>
    public Func<int, int>? ArtifactPosition { get; set; }

    public AdapterResult<int[]> GetArtifacts(int hero)
    {
        if (!HeroList.TryGetValue(hero, out var h)) return NoHero<int[]>(hero);
        var all = Enumerable.Repeat(-1, ArtifactSlots.Positions).ToArray();
        Array.Copy(h.Equipped, all, ArtifactSlots.Worn);
        for (int i = 0; i < h.Backpack.Count && i < ArtifactSlots.Backpack; i++) all[ArtifactSlots.Worn + i] = h.Backpack[i];
        return AdapterResult<int[]>.Ok(all);
    }

    public AdapterResult PutArtifact(int hero, int position, int artifact)
    {
        if (!HeroList.TryGetValue(hero, out var h)) return AdapterResult.Failed("no hero");
        if (position < ArtifactSlots.Worn)
        {
            if (h.Equipped[position] != -1) return AdapterResult.Failed("position taken");
            h.Equipped[position] = artifact;
            return AdapterResult.Ok;
        }
        int k = position - ArtifactSlots.Worn;
        while (h.Backpack.Count <= k) h.Backpack.Add(-1);
        if (h.Backpack[k] != -1) return AdapterResult.Failed("position taken");
        h.Backpack[k] = artifact;
        return AdapterResult.Ok;
    }

    public AdapterResult RemoveArtifactAt(int hero, int position)
    {
        if (!HeroList.TryGetValue(hero, out var h)) return AdapterResult.Failed("no hero");
        if (position < ArtifactSlots.Worn) h.Equipped[position] = -1;
        else if (position - ArtifactSlots.Worn < h.Backpack.Count) h.Backpack[position - ArtifactSlots.Worn] = -1;
        return AdapterResult.Ok;
    }

    public AdapterResult AddToBackpack(int hero, int artifact)
    {
        if (!HeroList.TryGetValue(hero, out var h)) return AdapterResult.Failed("no hero");
        int k = h.Backpack.IndexOf(-1);
        if (k >= 0) h.Backpack[k] = artifact;
        else if (h.Backpack.Count < ArtifactSlots.Backpack) h.Backpack.Add(artifact);
        return AdapterResult.Ok;
    }

    public AdapterResult EquipArtifact(int hero, int artifact)
    {
        if (!HeroList.TryGetValue(hero, out var h)) return AdapterResult.Failed("no hero");
        int p2 = artifact >= ArtifactSlots.ScrollBase ? 9 : ArtifactPosition?.Invoke(artifact) ?? 0;
        foreach (int slot in ArtifactSlots.ForPosition(p2))
            if (h.Equipped[slot] == -1) { h.Equipped[slot] = artifact; return AdapterResult.Ok; }
        return AddToBackpack(hero, artifact);
    }

    public AdapterResult<string> GetName(int hero) =>
        HeroList.TryGetValue(hero, out var h) ? AdapterResult<string>.Ok(h.Name) : NoHero<string>(hero);

    public AdapterResult SetName(int hero, string name)
    {
        if (!HeroList.TryGetValue(hero, out var h)) return AdapterResult.Failed("no hero");
        h.Name = name;
        return AdapterResult.Ok;
    }

    public AdapterResult<string> GetBiography(int hero, bool original) =>
        HeroList.TryGetValue(hero, out var h) ? AdapterResult<string>.Ok(original ? h.DefaultBiography : h.Biography ?? "") : NoHero<string>(hero);

    public AdapterResult<int[]> GetSpecialty(int hero) =>
        HeroList.TryGetValue(hero, out var h) ? AdapterResult<int[]>.Ok((int[])h.Specialty.Clone()) : NoHero<int[]>(hero);

    public AdapterResult SetSpecialty(int hero, int[] record)
    {
        if (!HeroList.TryGetValue(hero, out var h)) return AdapterResult.Failed("no hero");
        h.Specialty = (int[])record.Clone();
        return AdapterResult.Ok;
    }

    public AdapterResult<(int Type, int Min, int Max)> GetStartArmy(int hero, int slot) =>
        HeroList.TryGetValue(hero, out var h)
            ? AdapterResult<(int, int, int)>.Ok((h.StartArmy[slot][0], h.StartArmy[slot][1], h.StartArmy[slot][2])) : NoHero<(int, int, int)>(hero);

    public AdapterResult SetStartArmy(int hero, int slot, int type, int min, int max)
    {
        if (!HeroList.TryGetValue(hero, out var h)) return AdapterResult.Failed("no hero");
        h.StartArmy[slot] = new[] { type, min, max };
        return AdapterResult.Ok;
    }

    public AdapterResult SetBiography(int hero, string text)
    {
        if (!HeroList.TryGetValue(hero, out var h)) return AdapterResult.Failed("no hero");
        h.Biography = text;
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

    public AdapterResult<bool> IsRandomMap() => AdapterResult<bool>.Ok(RandomMap);
    public int Difficulty { get; set; } = 1;
    public AdapterResult<int> GetDifficulty() => AdapterResult<int>.Ok(Difficulty);
    public AdapterResult SetDifficulty(int level) { Difficulty = level; return AdapterResult.Ok; }

    public AdapterResult<int> GetTeam(int player) =>
        player is >= 0 and < WoGLimits.PlayerCount ? AdapterResult<int>.Ok(Teams[player]) : AdapterResult<int>.Failed("wrong player");

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

    // ---- towns (H3's _CastleSetup_, erm.cpp/casdem.cpp ERM_Castle) ----------------------------

    /// <summary>The town whose screen is open (CA-1); -1 = none.</summary>
    public int CurrentTownId { get; set; } = -1;

    public WoGTown AddTown(int id, int type, MapPos pos, int owner = -1)
    {
        var t = new WoGTown { Id = id, Type = type, Position = pos, Owner = owner };
        TownList[id] = t;
        Objects[pos.Pack()] = new WoGMapObject { Position = pos, Type = 98, SubType = type, Owner = owner };
        return t;
    }

    AdapterResult<T> WithTown<T>(int town, Func<WoGTown, T> read) =>
        TownList.TryGetValue(town, out var t) ? AdapterResult<T>.Ok(read(t)) : AdapterResult<T>.Failed($"town {town} does not exist");

    AdapterResult ChangeTown(int town, Action<WoGTown> write)
    {
        if (!TownList.TryGetValue(town, out var t)) return AdapterResult.Failed($"town {town} does not exist");
        write(t);
        return AdapterResult.Ok;
    }

    public AdapterResult<int> TownCount() => AdapterResult<int>.Ok(TownList.Count);

    public AdapterResult<int> TownAt(MapPos pos)
    {
        foreach (var t in TownList.Values) if (t.Position == pos) return AdapterResult<int>.Ok(t.Id);
        return AdapterResult<int>.Failed("no town");
    }

    public AdapterResult<int> CurrentTown() =>
        CurrentTownId >= 0 ? AdapterResult<int>.Ok(CurrentTownId) : AdapterResult<int>.Failed("no town screen is open");

    public AdapterResult<MapPos> GetTownPosition(int town) => WithTown(town, t => t.Position);
    public AdapterResult<int> GetTownOwner(int town) => WithTown(town, t => t.Owner);

    public AdapterResult SetTownOwner(int town, int owner) => ChangeTown(town, t =>
    {
        t.Owner = owner;
        if (Objects.TryGetValue(t.Position.Pack(), out var o)) o.Owner = owner;
    });

    public AdapterResult<int> GetTownType(int town) => WithTown(town, t => t.Type);
    public AdapterResult<string> GetTownName(int town) => WithTown(town, t => t.Name);
    public AdapterResult SetTownName(int town, string name) => ChangeTown(town, t => t.Name = name);
    public AdapterResult<int> GetTownHero(int town, bool visitor) => WithTown(town, t => visitor ? t.VisitorHero : t.GarrisonHero);

    public AdapterResult SetTownHero(int town, bool visitor, int hero) => ChangeTown(town, t =>
    {
        if (visitor) t.VisitorHero = hero; else t.GarrisonHero = hero;
        if (HeroList.TryGetValue(hero, out var h)) h.Position = t.Position;
    });

    public AdapterResult<int> GetMageGuildLevel(int town) => WithTown(town, t => t.MageGuildLevel);
    public AdapterResult SetMageGuildLevel(int town, int level) => ChangeTown(town, t => t.MageGuildLevel = level);
    public AdapterResult<int> GetGuildSpellCount(int town, int level) => WithTown(town, t => t.GuildSpellCount[level]);
    public AdapterResult SetGuildSpellCount(int town, int level, int count) => ChangeTown(town, t => t.GuildSpellCount[level] = count);
    public AdapterResult<int> GetGuildSpell(int town, int level, int slot) => WithTown(town, t => t.GuildSpells[level][slot]);
    public AdapterResult SetGuildSpell(int town, int level, int slot, int spell) => ChangeTown(town, t => t.GuildSpells[level][slot] = spell);

    public AdapterResult<bool> GetBuildingFlag(int town, int building, int check) =>
        WithTown(town, t => t.Has(building, check switch { 0 => t.Built, 1 => t.Bonus, _ => t.Allowed }));

    // CSCheckERM: what destroying a building leaves built (a guild level, a fort, a hall, a basic dwelling)
    static int Predecessor(int building) => building switch
    {
        >= 1 and <= 4 => building - 1,
        8 or 9 or 11 or 12 or 13 => building - 1,
        >= 37 and <= 43 => building - 7,
        _ => -1,
    };

    public AdapterResult SetBuilt(int town, int building, bool built) => ChangeTown(town, t =>
    {
        ulong bit = 1UL << building;
        if (built)
        {
            t.Built |= bit;
            t.Bonus |= bit;
            return;
        }
        t.Built &= ~bit;
        t.Bonus &= ~bit;
        if (Predecessor(building) is var p and >= 0)
        {
            t.Built |= 1UL << p;
            t.Bonus |= 1UL << p;
        }
    });

    public AdapterResult SetAllowed(int town, int building, bool allowed) =>
        ChangeTown(town, t => t.Allowed = allowed ? t.Allowed | 1UL << building : t.Allowed & ~(1UL << building));

    // The game's construction builds what an upgrade needs first and fills the mage guild.
    public AdapterResult Build(int town, int building) => ChangeTown(town, t =>
    {
        for (int b = building; b >= 0; b = Predecessor(b))
        {
            t.Built |= 1UL << b;
            t.Bonus |= 1UL << b;
        }
        if (building <= 4) t.MageGuildLevel = Math.Max(t.MageGuildLevel, building + 1);
    });

    public AdapterResult<int> GetBuiltThisTurn(int town) => WithTown(town, t => t.BuiltThisTurn);
    public AdapterResult SetBuiltThisTurn(int town, int value) => ChangeTown(town, t => t.BuiltThisTurn = value);
    public AdapterResult<int> GetAvailable(int town, int level, int row) => WithTown(town, t => t.Available[row][level]);
    public AdapterResult SetAvailable(int town, int level, int row, int count) => ChangeTown(town, t => t.Available[row][level] = count);
    public AdapterResult<int> GetGrowth(int town, int level) => WithTown(town, t => t.Growth[level]);
    public AdapterResult<WoGStack> GetGuard(int town, int slot) => WithTown(town, t => t.Garrison.Slots[slot].Clone());

    public AdapterResult SetGuard(int town, int slot, int type, int count) =>
        ChangeTown(town, t => t.Garrison.Slots[slot] = new WoGStack { Type = type, Count = count });

    public AdapterResult<int> GetIncome(int town) => WithTown(town, t => t.Income);

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

    public AdapterResult<WoGBattle> GetBattle() =>
        CurrentBattle != null ? AdapterResult<WoGBattle>.Ok(CurrentBattle) : AdapterResult<WoGBattle>.Failed("not in battle");

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
