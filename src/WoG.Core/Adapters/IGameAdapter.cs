using System.Collections.Generic;
using WoG.Core.Model;

namespace WoG.Core.Adapters;

/// <summary>
/// Everything the WoG Core needs from a game engine. All ids are in the WoG/H3 domain (hero 0..155,
/// creature type numbers, artifact numbers, player colours 0..7); translating them to engine ids is the
/// adapter's job (see <see cref="WoG.Core.Ids.IdMap"/>).
/// </summary>
public interface IGameAdapter
{
    string EngineName { get; }
    IHeroAdapter Heroes { get; }
    IPlayerAdapter Players { get; }
    ICreatureTypeAdapter Creatures { get; }
    IMapAdapter Map { get; }
    ITownAdapter Towns { get; }
    IUiAdapter Ui { get; }
    IBattleAdapter Battle { get; }
    IGameClock Clock { get; }
}

public enum HeroStat
{
    Experience,
    Level,
    Attack,
    Defence,
    Power,
    Knowledge,
    Mana,
    Movement,
    Owner,
    HeroClass,
}

public interface IHeroAdapter
{
    bool Exists(int hero);
    AdapterResult<int> Get(int hero, HeroStat stat);
    AdapterResult Set(int hero, HeroStat stat, int value);
    /// <summary>Primary skills without artifact bonuses (HE:F…/1).</summary>
    AdapterResult<int> GetBase(int hero, HeroStat stat);
    AdapterResult<MapPos> GetPosition(int hero);
    AdapterResult MoveTo(int hero, MapPos pos, bool withEffect);
    AdapterResult<int> HeroAt(MapPos pos);
    AdapterResult<int> GetSecondarySkill(int hero, int skill);
    AdapterResult SetSecondarySkill(int hero, int skill, int level);
    AdapterResult<bool> HasSpell(int hero, int spell);
    AdapterResult SetSpell(int hero, int spell, bool known);
    AdapterResult<WoGStack> GetStack(int hero, int slot);
    AdapterResult SetStack(int hero, int slot, int type, int count);
    AdapterResult<int> CountArtifact(int hero, int artifact);
    /// <summary>Gives an artifact; slot -1 = first suitable slot or backpack.</summary>
    AdapterResult AddArtifact(int hero, int artifact, int slot);
    /// <summary>Removes up to <paramref name="count"/> copies; returns how many were removed.</summary>
    AdapterResult<int> RemoveArtifact(int hero, int artifact, int count);
    AdapterResult<string> GetName(int hero);
    AdapterResult SetName(int hero, string name);
    AdapterResult Kill(int hero);
}

public interface IPlayerAdapter
{
    AdapterResult<int> GetResource(int player, int resource);
    AdapterResult SetResource(int player, int resource, int value);
    /// <summary>Player whose turn it is.</summary>
    int CurrentPlayer { get; }
    AdapterResult<bool> IsHuman(int player);
    /// <summary>True when the player sits at this PC (flag 999 / OW:G).</summary>
    AdapterResult<bool> IsLocal(int player);
    AdapterResult<bool> IsAlive(int player);
    AdapterResult<int> GetActiveHero(int player);
    AdapterResult<IReadOnlyList<int>> GetHeroes(int player);
    /// <summary>The player's team: players of one alliance share it (the lowest player number among them).</summary>
    AdapterResult<int> GetTeam(int player);
}

public enum CreatureStat
{
    Attack, Defence, HitPoints, Speed, DamageLow, DamageHigh, Shots, Casts, Growth, HordeGrowth,
    FightValue, AiValue, AdvMapLow, AdvMapHigh, Level, Town, UpgradeTo, Flags,
}

public interface ICreatureTypeAdapter
{
    bool Exists(int type);
    AdapterResult<int> Get(int type, CreatureStat stat);
    AdapterResult Set(int type, CreatureStat stat, int value);
    AdapterResult<int> GetCost(int type, int resource);
    AdapterResult SetCost(int type, int resource, int value);
}

public interface IMapAdapter
{
    /// <summary>Map width (H3 maps are square) and levels as H3 counts them: 0 = surface only, 1 = with underground.</summary>
    AdapterResult<(int Size, int Levels)> GetSize();
    /// <summary>
    /// The objects ERM can see, each at its H3 position (its entrance square), in the order H3 scans the map: left to
    /// right, top to bottom, surface before underground.
    /// </summary>
    AdapterResult<IReadOnlyList<WoGMapObject>> GetObjects();
    /// <summary>A square: the object on it, its entrance/blocked bits and its terrain.</summary>
    AdapterResult<MapSquare> GetSquare(MapPos pos);
    /// <summary>The map was generated (a random map), not made in an editor.</summary>
    AdapterResult<bool> IsRandomMap();
    /// <summary>The difficulty chosen for the game (H3: 0 easy … 4 impossible).</summary>
    AdapterResult<int> GetDifficulty();
    AdapterResult SetDifficulty(int level);
    AdapterResult<(int type, int subtype)> GetObjectAt(MapPos pos);
    AdapterResult<int> GetObjectOwner(MapPos pos);
    AdapterResult SetObjectOwner(MapPos pos, int owner);
}

public interface ITownAdapter
{
    AdapterResult<int> TownAt(MapPos pos);
    AdapterResult<int> GetMageGuildLevel(int town);
    AdapterResult<bool> IsBuilt(int town, int building);
    AdapterResult SetBuilt(int town, int building, bool built);
}

public interface IUiAdapter
{
    AdapterResult ShowMessage(string text);
    AdapterResult<bool> AskYesNo(string text);
}

public interface IGameClock
{
    /// <summary>Absolute day, 1-based, as WoG's GetCurDate: ((month-1)*4 + week-1)*7 + day.</summary>
    int AbsoluteDay { get; }
    int DayOfWeek { get; }
    int Week { get; }
    int Month { get; }
}

public enum BattleStackStat
{
    Type, Count, Attack, Defence, HitPoints, HitPointsLost, Speed, DamageLow, DamageHigh, Shots, Casts,
    Retaliations, Flags, Position, Side,
}

public interface IBattleAdapter
{
    bool InBattle { get; }
    /// <summary>The battle being set up or fought: heroes, owners, position, flags (what BA reads).</summary>
    AdapterResult<WoGBattle> GetBattle();
    AdapterResult<int> GetHero(int side);
    AdapterResult<int> StackCount();
    AdapterResult<int> GetStack(int stackIndex, BattleStackStat stat);
    AdapterResult SetStack(int stackIndex, BattleStackStat stat, int value);
    /// <summary>Applies a generated buff (overlay data) to a battle stack.</summary>
    AdapterResult ApplyBuff(int stackIndex, string buffId);
    /// <summary>Adds a unit to the battle (commanders); returns its stack index.</summary>
    AdapterResult<int> SummonUnit(int side, string unitKey, int count);
}
