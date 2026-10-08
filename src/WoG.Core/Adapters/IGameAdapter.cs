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
    /// <summary>Learned secondary skills in hero-screen order (slot 1 first; H3 shows at most 8).</summary>
    AdapterResult<IReadOnlyList<int>> GetSecondarySkillOrder(int hero);
    AdapterResult SetSecondarySkillOrder(int hero, IReadOnlyList<int> order);
    AdapterResult<bool> HasSpell(int hero, int spell);
    AdapterResult SetSpell(int hero, int spell, bool known);
    AdapterResult<WoGStack> GetStack(int hero, int slot);
    AdapterResult SetStack(int hero, int slot, int type, int count);
    /// <summary>
    /// The hero's artifacts by position, numbered as ERM's HE:A does: 0..18 worn (<see cref="ArtifactSlots"/>),
    /// 19..82 backpack; -1 = empty, 1001 + n = a scroll with spell n.
    /// </summary>
    AdapterResult<int[]> GetArtifacts(int hero);
    /// <summary>Puts an artifact into an empty position (WoG's HE:A1 does not check that it fits).</summary>
    AdapterResult PutArtifact(int hero, int position, int artifact);
    AdapterResult RemoveArtifactAt(int hero, int position);
    /// <summary>The first empty backpack position (HE:A$); nothing happens when the backpack is full.</summary>
    AdapterResult AddToBackpack(int hero, int artifact);
    /// <summary>The game's own "give an artifact" (HE:A4, H3 EquipArtifact): a suitable empty slot, else the backpack.</summary>
    AdapterResult EquipArtifact(int hero, int artifact);
    AdapterResult<string> GetName(int hero);
    AdapterResult SetName(int hero, string name);
    /// <summary>HE:B1 the biography a script gave the hero ("" while it has its own), HE:B3 (original) its own.</summary>
    AdapterResult<string> GetBiography(int hero, bool original);
    AdapterResult SetBiography(int hero, string text);
    /// <summary>
    /// HE:X — the hero's specialty as H3's record of 7 numbers: type (0 skill, 1 creature, 2 resource, 3 spell,
    /// 4 creature with bonuses, 5 speed, 6 upgrade, 7 dragons, 8 WoG), then its subtype and settings.
    /// </summary>
    AdapterResult<int[]> GetSpecialty(int hero);
    AdapterResult SetSpecialty(int hero, int[] record);
    /// <summary>HE:H — the army a hero of this type is hired with, slots 0..2: creature (-1 none), minimum, maximum.</summary>
    AdapterResult<(int Type, int Min, int Max)> GetStartArmy(int hero, int slot);
    AdapterResult SetStartArmy(int hero, int slot, int type, int min, int max);
    AdapterResult Kill(int hero);
    /// <summary>
    /// The total experience a hero needs for a level, from the engine's level table (hero -1: the table of the game's
    /// heroes); 0 for a level below 1, int.MaxValue above the last level the engine has.
    /// </summary>
    AdapterResult<int> ExperienceForLevel(int hero, int level);
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
    /// <summary>OW:N — the town selected in the player's town list; -1 = none.</summary>
    AdapterResult<int> GetActiveTown(int player);
    /// <summary>OW:V — the two heroes the player's tavern offers (left, right); -1 = none.</summary>
    AdapterResult<(int Left, int Right)> GetTavernHeroes(int player);
    AdapterResult SetTavernHeroes(int player, int left, int right);
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
    /// <summary>The game's change of owner of the object at a square (-1 neutral).</summary>
    AdapterResult SetObjectOwner(MapPos pos, int owner);
    /// <summary>The guards an object keeps itself (a mine's, MN:M), slots 0..7.</summary>
    AdapterResult<WoGStack> GetObjectGuard(MapPos pos, int slot);
    AdapterResult SetObjectGuard(MapPos pos, int slot, int type, int count);
}

/// <summary>
/// Towns, numbered as H3 keeps them (CA0/n, CA:U): 0..TownCount-1. Buildings are H3's numbers 0..43 (ERM help,
/// building list); town types are H3's (Format T: 0 Castle … 8 Conflux).
/// </summary>
public interface ITownAdapter
{
    AdapterResult<int> TownCount();
    /// <summary>The town whose ERM position (its entrance) is <paramref name="pos"/>.</summary>
    AdapterResult<int> TownAt(MapPos pos);
    /// <summary>The town whose screen is open (CA-1).</summary>
    AdapterResult<int> CurrentTown();
    AdapterResult<MapPos> GetTownPosition(int town);
    AdapterResult<int> GetTownOwner(int town);
    /// <summary>The game's own change of owner (H3 0x4C5EA0); -1 = neutral.</summary>
    AdapterResult SetTownOwner(int town, int owner);
    AdapterResult<int> GetTownType(int town);
    AdapterResult<string> GetTownName(int town);
    AdapterResult SetTownName(int town, string name);
    /// <summary>The hero in the garrison (<paramref name="visitor"/> false) or visiting the town; -1 = none.</summary>
    AdapterResult<int> GetTownHero(int town, bool visitor);
    AdapterResult SetTownHero(int town, bool visitor, int hero);
    AdapterResult<int> GetMageGuildLevel(int town);
    AdapterResult SetMageGuildLevel(int town, int level);
    /// <summary>G2/G3: the spells of mage guild level 0..4: how many it offers, and which (slot 0..5).</summary>
    AdapterResult<int> GetGuildSpellCount(int town, int level);
    AdapterResult SetGuildSpellCount(int town, int level, int count);
    AdapterResult<int> GetGuildSpell(int town, int level, int slot);
    AdapterResult SetGuildSpell(int town, int level, int slot, int spell);
    /// <summary>B3: built (<paramref name="check"/> 0), its bonus taken (1), allowed to be built (2).</summary>
    AdapterResult<bool> GetBuildingFlag(int town, int building, int check);
    /// <summary>B1 (built) and B2 (destroyed; an upgrade leaves what it upgraded, WoG CSCheckERM).</summary>
    AdapterResult SetBuilt(int town, int building, bool built);
    /// <summary>B4/B5: allow or forbid building it.</summary>
    AdapterResult SetAllowed(int town, int building, bool allowed);
    /// <summary>B6: the game's own construction (H3 0x5BF1E0): no cost, the day's construction is kept.</summary>
    AdapterResult Build(int town, int building);
    /// <summary>R: 1 when the town has built today.</summary>
    AdapterResult<int> GetBuiltThisTurn(int town);
    AdapterResult SetBuiltThisTurn(int town, int value);
    /// <summary>M1: creatures to hire at a dwelling of level 0..6; row 0 its basic, 1 its upgraded building.</summary>
    AdapterResult<int> GetAvailable(int town, int level, int row);
    AdapterResult SetAvailable(int town, int level, int row, int count);
    /// <summary>M4: weekly growth of a dwelling of level 0..6.</summary>
    AdapterResult<int> GetGrowth(int town, int level);
    /// <summary>M2: the town's own garrison, slots 0..6.</summary>
    AdapterResult<WoGStack> GetGuard(int town, int slot);
    AdapterResult SetGuard(int town, int slot, int type, int count);
    /// <summary>S: the town's gold income per day.</summary>
    AdapterResult<int> GetIncome(int town);
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
    Retaliations, Flags, Position, Side, CountAtStart, ArmySlot,
}

public interface IBattleAdapter
{
    bool InBattle { get; }
    /// <summary>The battle being set up or fought: heroes, owners, position, flags (what BA reads).</summary>
    AdapterResult<WoGBattle> GetBattle();
    AdapterResult<int> GetHero(int side);
    AdapterResult<int> StackCount();
    /// <summary>The stack whose turn it is (BM-1); -1 = none.</summary>
    AdapterResult<int> CurrentStack();
    /// <summary>A battle stack by WoG's number: side * 21 + index (0..20 attacker, 21..41 defender).</summary>
    AdapterResult<int> GetStack(int stackIndex, BattleStackStat stat);
    AdapterResult SetStack(int stackIndex, BattleStackStat stat, int value);
    /// <summary>Applies a generated buff (overlay data) to a battle stack.</summary>
    AdapterResult ApplyBuff(int stackIndex, string buffId);
    /// <summary>Adds a unit to the battle (commanders); returns its stack index.</summary>
    AdapterResult<int> SummonUnit(int side, string unitKey, int count);
}
