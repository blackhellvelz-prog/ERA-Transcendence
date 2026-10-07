using WoG.Core.Adapters;
using WoG.Core.Compat;
using WoG.Core.Events;
using WoG.Core.Model;
using WoG.Core.Random;
using WoG.Core.State;
using WoG.Core.Visual;

namespace WoG.Core.Services;

/// <summary>
/// Service locator shared by all WoG modules. Optional modules are nullable: when a module is
/// disabled, consumers (e.g. ERM receivers CO/EX) report "module disabled" instead of failing.
/// </summary>
public interface IWoGServices
{
    WoGGameState State { get; }
    IGameAdapter Game { get; }
    IWoGRandom Random { get; }
    WoGEventBus Events { get; }
    CompatibilityReport Compat { get; }
    IVisualResolver Visuals { get; }
    ICommanderService? Commanders { get; }
    IStackExperienceService? StackExperience { get; }
    /// <summary>H3/WoG data tables of the user's ERA installation (empty when there is none).</summary>
    WoG.Core.H3Data.H3Tables H3 { get; }
    /// <summary>Creature types for MA: the engine's units, else the installation's table; changes kept in the state.</summary>
    WoG.Core.H3Data.CreatureTable CreatureTypes { get; }
}

/// <summary>Commander rules (implemented by WoG.Commanders).</summary>
public interface ICommanderService
{
    /// <summary>Commander record of a hero (index -3/-4 = extra attacker/defender commander).</summary>
    WoGCommander Get(int number);
    /// <summary>NPC::Init for a hero (class from the hero's class).</summary>
    void Init(int hero, int heroClass, string name);
    /// <summary>NPC::AddExp: feeds the hero's new total experience; returns levels gained.</summary>
    int OnHeroExperience(int hero, int newHeroExp, bool auto);
    /// <summary>Effective stat (CalcSkill) for stat index 0..6.</summary>
    int CalcSkill(WoGCommander c, int stat);
    CommanderBattleProfile BattleProfile(WoGCommander c);
    /// <summary>Applies option 3/6 (UN:P3, UN:P6): enable/disable all commanders.</summary>
    void ApplyOptions();
}

/// <summary>The commander's stats as a battle unit (SetMonInitPars).</summary>
public sealed class CommanderBattleProfile
{
    public int Attack { get; set; }
    public int Defence { get; set; }
    public int HitPoints { get; set; }
    public int DamageLow { get; set; }
    public int DamageHigh { get; set; }
    public int Speed { get; set; }
    public int Casts { get; set; }
    public int MagicPower { get; set; }
    public int MagicResistancePercent { get; set; }
    public int CastSpell { get; set; }
    public uint MonsterFlags { get; set; }
    public uint SpecialBonuses { get; set; }
}

/// <summary>Stack experience rules (implemented by WoG.CreatureExperience).</summary>
public interface IStackExperienceService
{
    StackExperienceRecord? Find(StackLocation loc);
    StackExperienceRecord GetOrCreate(StackLocation loc, int type, int num);
    void Delete(StackLocation loc);
    int GetRank(int creatureType, int exp);
    int RankExp(int creatureType, int rank);
    /// <summary>CrExpoSet::AddExpo — distributes a human hero's experience gain to its stacks.</summary>
    void OnHeroBattleExperience(int hero, int oldHeroExp, int newHeroExp);
    /// <summary>Effective bonus value of one bonus line at a rank, after the modifier (ApplyMod).</summary>
    int ApplyBonus(CreatureExpBonus bonus, int rank, int baseValue, int artifactSubType);
}
