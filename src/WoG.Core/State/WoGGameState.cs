using System.Linq;
using System.Collections.Generic;
using WoG.Core.Ids;
using WoG.Core.Model;
using WoG.Core.Options;

namespace WoG.Core.State;

/// <summary>Per-type stack experience parameters (CrExpMod) — may be changed by ERM, so it is state.</summary>
public sealed class CreatureExpParams
{
    public float ExpMul { get; set; } = 1f;
    public float UpgrMul { get; set; } = 0.5f;
    public int Limit { get; set; }
    public int Lvl11Exp { get; set; }
    /// <summary>Per-battle cap in percent of Limit.</summary>
    public int Cap { get; set; } = 100;
}

/// <summary>One stack-experience bonus line (CrExpBonStr): type char, modifier char, 11 rank values.</summary>
public sealed class CreatureExpBonus
{
    public char Type { get; set; }
    public char Mod { get; set; }
    /// <summary>Numeric modifier when the file used "#n" (Mod is then '#').</summary>
    public int ModNumber { get; set; }
    public int[] Levels { get; set; } = new int[11];
}

/// <summary>Everything the stack-experience subsystem persists.</summary>
public sealed class StackExperienceState
{
    public List<StackExperienceRecord> Records { get; set; } = new();
    /// <summary>CrExpoSet::PlayerMult (percent, default 100).</summary>
    public int PlayerMult { get; set; } = 100;
    public Dictionary<int, CreatureExpParams> Params { get; set; } = new();
    public Dictionary<int, List<CreatureExpBonus>> Bonuses { get; set; } = new();
}

/// <summary>Per-object ERM data (ERM_Object): hint override and native-behaviour switches.</summary>
public sealed class ErmObjectState
{
    public string? Hint { get; set; }
    public int DisabledMask { get; set; }
}

/// <summary>
/// The single root of all WoG state. Serialised by <see cref="WoG.Core.Save.WoGSaveSerializer"/>
/// next to every native save. Nothing gameplay-critical may live only in process memory.
/// </summary>
public sealed class WoGGameState
{
    public const int SchemaVersion = 1;

    public WoGOptions Options { get; set; } = new();
    /// <summary>The map was WoGified when it started (WoG option 5): its global scripts load again with a saved game.</summary>
    public bool Wogified { get; set; } = true;
    public WoGVariables Erm { get; set; } = new();
    /// <summary>ERA (HoMM3 ERA) additions to the ERM state; empty for classic WoG scripts.</summary>
    public EraState Era { get; set; } = new();
    public Dictionary<int, WoGCommander> Commanders { get; set; } = new();
    /// <summary>Extra "additional" commanders for battles without a commander-owning hero (NPCsa).</summary>
    public WoGCommander[] ExtraCommanders { get; set; } = { new() { Number = -3 }, new() { Number = -4 } };
    public StackExperienceState StackExperience { get; set; } = new();
    /// <summary>Key = MapPos.Pack().</summary>
    public Dictionary<int, ErmObjectState> Objects { get; set; } = new();
    /// <summary>Creature type values changed by MA: type → stat name (or "Cost0".."Cost6") → value.</summary>
    public Dictionary<int, Dictionary<string, int>> CreatureChanges { get; set; } = new();
    /// <summary>Artifact types changed by UN:A (only changed ones; the rest come from the H3 tables).</summary>
    public Dictionary<int, WoGArtifact> ArtifactOverrides { get; set; } = new();
    /// <summary>PO: WoG's own data of map squares (key MapPos.Pack()); only squares that differ from the start values.</summary>
    public Dictionary<int, PoSquare> Squares { get; set; } = new();
    /// <summary>Spells banned from mage guilds by UN:J0.</summary>
    public HashSet<int> DisabledSpells { get; set; } = new();
    /// <summary>UN:J11: the right-click creature info dialog stays open (0) or works as usual (1).</summary>
    public int MonInfoDlgPopUp { get; set; }
    /// <summary>Artifacts banned from the map by UN:A#/1 (ArtDisabled).</summary>
    public HashSet<int> BannedArtifacts { get; set; } = new();
    /// <summary>Combination table entries changed by UN:A (index 0..31 → [combo artifact, parts…]; 0 = empty entry).</summary>
    public Dictionary<int, int[]> ComboOverrides { get; set; } = new();
    public IdMap Ids { get; set; } = new();
    /// <summary>WoG's mithril per player (erm.cpp MithrillVal: OW:R resource 7, mithril piles); saved with the game.</summary>
    public int[] Mithril { get; set; } = new int[WoGLimits.PlayerCount];
    /// <summary>H3 pool heroes of numbers the engine has no hero for (<see cref="WoG.Core.Adapters.PoolHeroAdapter"/>).</summary>
    public Dictionary<int, WoGHero> PoolHeroes { get; set; } = new();
    /// <summary>Free-form module data (module name → JSON).</summary>
    public Dictionary<string, string> Modules { get; set; } = new();
    /// <summary>The last day whose day start (OnEveryDay, timers) ran: a loaded game does not run it again.</summary>
    public int DayStarted { get; set; }
    /// <summary>True once instructions (!#) and !?PI ran for this game.</summary>
    public bool InstructionsDone { get; set; }
}

/// <summary>One map square's PO data: erm.cpp's _Square_ bit fields and _Square2_ (the values are kept in range).</summary>
public sealed class PoSquare
{
    /// <summary>"The last hero" (8 bits); scripts set it, WoG itself does not.</summary>
    public int Hero { get; set; } = 255;
    /// <summary>Owner (4 bits, signed: -8..7).</summary>
    public int Owner { get; set; } = -1;
    public int Number { get; set; }  // 4 bits
    public int NumberT { get; set; } // 8 bits
    public int NumberS { get; set; } // 8 bits
    public short[] S { get; set; } = new short[4];
    public int[] L { get; set; } = new int[2];

    public bool IsStart() => Hero == 255 && Owner == -1 && Number == 0 && NumberT == 0 && NumberS == 0
        && S.All(x => x == 0) && L.All(x => x == 0);
}
