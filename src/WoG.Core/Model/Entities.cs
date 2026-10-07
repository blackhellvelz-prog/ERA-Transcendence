using System.Collections.Generic;

namespace WoG.Core.Model;

/// <summary>Adventure map position in WoG terms (x, y, level 0 = surface, 1 = underground).</summary>
public readonly record struct MapPos(int X, int Y, int L)
{
    public static readonly MapPos None = new(-1, -1, -1);
    public bool IsNone => X < 0;

    /// <summary>Packs a position into one int (internal to the port; not H3's MixPos).</summary>
    public int Pack() => (X & 0x3FF) | ((Y & 0x3FF) << 10) | ((L & 1) << 20);

    public static MapPos Unpack(int v) => new(v & 0x3FF, (v >> 10) & 0x3FF, (v >> 20) & 1);

    public override string ToString() => $"{X}/{Y}/{L}";
}

/// <summary>Constants of the H3/WoG id domain that ERM scripts use.</summary>
public static class WoGLimits
{
    public const int HeroCount = 156;          // HERNUM
    public const int PlayerCount = 8;
    public const int ArmySlots = 7;
    public const int ResourceCount = 7;        // wood, mercury, ore, sulfur, crystal, gems, gold
    public const int Gold = 6;
    public const int SecondarySkillCount = 28;
    public const int SpellCount = 70;
    public const int ArtifactSlots = 19;       // equipped slots 0..18, backpack starts at 19
    public const int CommanderArtifactFirst = 146;
    public const int StackArtifact = 156;
    public const int ArtifactCount = 171;      // ARTNUM of WoG 3.58 (used when no artifact table is loaded)
}

/// <summary>One army slot: creature type (-1 = empty) and count.</summary>
public sealed class WoGStack
{
    public int Type { get; set; } = -1;
    public int Count { get; set; }
    public bool IsEmpty => Type < 0 || Count <= 0;
    public WoGStack Clone() => new() { Type = Type, Count = Count };
}

/// <summary>A 7-slot army (hero, town garrison, garrison object, mine guards).</summary>
public sealed class WoGArmy
{
    public WoGStack[] Slots { get; set; } = NewSlots();

    static WoGStack[] NewSlots()
    {
        var s = new WoGStack[WoGLimits.ArmySlots];
        for (int i = 0; i < s.Length; i++) s[i] = new WoGStack();
        return s;
    }

    public int FirstEmptySlot()
    {
        for (int i = 0; i < Slots.Length; i++) if (Slots[i].IsEmpty) return i;
        return -1;
    }
}

/// <summary>Reference-model hero (used by the headless engine and by WoG-side bookkeeping).</summary>
public sealed class WoGHero
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int Owner { get; set; } = -1;
    public int HeroClass { get; set; }
    public MapPos Position { get; set; } = MapPos.None;
    public int Experience { get; set; }
    public int Level { get; set; } = 1;
    /// <summary>Attack, Defence, Power, Knowledge.</summary>
    public int[] Primary { get; set; } = new int[4];
    public int Mana { get; set; }
    public int Movement { get; set; }
    public int[] SecondarySkills { get; set; } = new int[WoGLimits.SecondarySkillCount];
    /// <summary>Shown secondary skills in hero-screen order (H3 SShow/SSNum as a list).</summary>
    public List<int> SkillOrder { get; set; } = new();
    public HashSet<int> Spells { get; set; } = new();
    /// <summary>Equipped slots 0..18 (artifact id or -1).</summary>
    public int[] Equipped { get; set; } = NewEquipped();
    /// <summary>Backpack positions 19.. as a list; -1 = an empty position left by a removed artifact.</summary>
    public List<int> Backpack { get; set; } = new();
    public WoGArmy Army { get; set; } = new();
    public bool Alive { get; set; } = true;

    static int[] NewEquipped()
    {
        var a = new int[WoGLimits.ArtifactSlots];
        for (int i = 0; i < a.Length; i++) a[i] = -1;
        return a;
    }
}

/// <summary>Hero artifact positions as ERM's HE:A numbers them (H3 ART_SLOT_*), and where an artifact can be worn.</summary>
public static class ArtifactSlots
{
    public const int Head = 0, Shoulders = 1, Neck = 2, RightHand = 3, LeftHand = 4, Torso = 5, RightRing = 6,
        LeftRing = 7, Feet = 8, Misc1 = 9, Misc4 = 12, Ballista = 13, AmmoCart = 14, FirstAidTent = 15, Catapult = 16,
        SpellBook = 17, Misc5 = 18;
    public const int Worn = 19, Backpack = 64, Positions = Worn + Backpack;
    /// <summary>ERM's scroll numbers: 1001 + spell.</summary>
    public const int ScrollBase = 1001;

    /// <summary>Worn positions for an artifact position of "Format P2" (<see cref="WoGArtifact.Position"/>).</summary>
    public static int[] ForPosition(int formatP2) => formatP2 switch
    {
        1 => new[] { Head }, 2 => new[] { Shoulders }, 3 => new[] { Neck }, 4 => new[] { RightHand },
        5 => new[] { LeftHand }, 6 => new[] { Torso }, 7 => new[] { RightRing, LeftRing }, 8 => new[] { Feet },
        9 => new[] { Misc1, Misc1 + 1, Misc1 + 2, Misc4, Misc5 }, 10 => new[] { Ballista }, 11 => new[] { AmmoCart },
        12 => new[] { FirstAidTent }, 13 => new[] { Catapult }, 14 => new[] { SpellBook },
        _ => System.Array.Empty<int>(),
    };
}

/// <summary>Creature type definition (the fields ERM's MA receiver can read/write).</summary>
public sealed class WoGCreature
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string NamePlural { get; set; } = "";
    /// <summary>The ability text of the creature info window.</summary>
    public string Ability { get; set; } = "";
    public int Town { get; set; } = -1;
    /// <summary>0-based level ("SubGroup"): 0 = level 1 … 6 = level 7 (8th level also 6).</summary>
    public int Level { get; set; }
    public int Attack { get; set; }
    public int Defence { get; set; }
    public int HitPoints { get; set; }
    public int Speed { get; set; }
    public int DamageLow { get; set; }
    public int DamageHigh { get; set; }
    public int Shots { get; set; }
    public int Casts { get; set; }
    public int Growth { get; set; }
    public int HordeGrowth { get; set; }
    public int FightValue { get; set; }
    public int AiValue { get; set; }
    public int AdvMapLow { get; set; }
    public int AdvMapHigh { get; set; }
    public int UpgradeTo { get; set; } = -1;
    public uint Flags { get; set; }
    public int[] Cost { get; set; } = new int[WoGLimits.ResourceCount];
}

/// <summary>An artifact type as WoG's artifact setup table (ArtSetUp) holds it — what UN:A reads and writes.</summary>
public sealed class WoGArtifact
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    /// <summary>Message shown when the artifact is picked up (UN:A…/11).</summary>
    public string PickUpText { get; set; } = "";
    public int Cost { get; set; }
    /// <summary>
    /// Where the artifact is worn, in ERM's "Format P2": 0 none (backpack only), 1 head, 2 shoulders, 3 neck,
    /// 4 right hand, 5 left hand, 6 torso, 7 ring, 8 feet, 9 misc, 10 ballista, 11 ammo cart, 12 first aid tent,
    /// 13 catapult, 14 spell book. (Hero slots — ART_SLOT_* — are numbered differently.)
    /// </summary>
    public int Position { get; set; }
    /// <summary>Class bits: 1 special, 2 treasure, 4 minor, 8 major, 16 relic.</summary>
    public int Type { get; set; }
    /// <summary>Index of the combination this artifact is (SuperN), -1 if it is not one.</summary>
    public int SuperN { get; set; } = -1;
    /// <summary>Index of the combination this artifact is a part of, -1 if none.</summary>
    public int PartOfSuperN { get; set; } = -1;
    public int Disable { get; set; }
    /// <summary>"Gives spells" byte (UN:A…/8): 1 for tomes, Spellbinder's Hat…; WoG has no default table for it.</summary>
    public int NewSpell { get; set; }

    public WoGArtifact Clone() => (WoGArtifact)MemberwiseClone();
}

public sealed class WoGSkill
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

public sealed class WoGSpell
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int Level { get; set; }
}

public sealed class WoGBuilding
{
    public int Id { get; set; }
    public bool Built { get; set; }
    public bool Enabled { get; set; } = true;
}

public sealed class WoGTown
{
    public int Id { get; set; }
    public int Type { get; set; }
    public string Name { get; set; } = "";
    public int Owner { get; set; } = -1;
    public MapPos Position { get; set; } = MapPos.None;
    public int MageGuildLevel { get; set; }
    public Dictionary<int, WoGBuilding> Buildings { get; set; } = new();
    public WoGArmy Garrison { get; set; } = new();
}

/// <summary>
/// One map square as H3's MapItem shows it to ERM (OB:T/U, TR): the object on it (type 0 = none), the yellow
/// (entrance) and red (blocked) bits, terrain type (Format TR: 0 dirt … 8 water, 9 rock) and road type (0 none).
/// </summary>
public readonly record struct MapSquare(int ObjectType, int ObjectSubtype, bool Entrance, bool Blocked, int Land, int Road,
    MapPos ObjectEntrance)
{
    public static MapSquare Empty(int land) => new(0, 0, false, false, land, 0, MapPos.None);
}

public sealed class WoGMapObject
{
    public MapPos Position { get; set; } = MapPos.None;
    /// <summary>The engine's object id (sid) behind it, for diagnostics; null on the headless engine.</summary>
    public string? Sid { get; set; }
    public int Type { get; set; }
    public int SubType { get; set; }
    public int Owner { get; set; } = -1;
    /// <summary>Script-defined hint override (ERM OB:H / HT).</summary>
    public string? Hint { get; set; }
    /// <summary>Native behaviour disabled for some players (OB:S/R).</summary>
    public int DisabledMask { get; set; }
    public Dictionary<string, int> Data { get; set; } = new();
}

/// <summary>A creature stack in battle (42 per battle in H3: 21 per side).</summary>
public sealed class WoGBattleStack
{
    public int Index { get; set; }
    public int Side { get; set; }
    public int Type { get; set; }
    public int Count { get; set; }
    public int CountAtStart { get; set; }
    public int Attack { get; set; }
    public int Defence { get; set; }
    public int HitPoints { get; set; }
    public int HitPointsLost { get; set; }
    public int Speed { get; set; }
    public int DamageLow { get; set; }
    public int DamageHigh { get; set; }
    public int Shots { get; set; }
    public int Casts { get; set; }
    public int Retaliations { get; set; }
    public uint Flags { get; set; }
    public int Position { get; set; }
    /// <summary>Army slot the stack came from, -1 for summoned/commander.</summary>
    public int ArmySlot { get; set; } = -1;
    public bool IsCommander { get; set; }
}

public sealed class WoGBattle
{
    public int[] Heroes { get; set; } = { -1, -1 };
    public int[] Owners { get; set; } = { -1, -1 };
    public MapPos Position { get; set; } = MapPos.None;
    public int Round { get; set; } = -1;
    public List<WoGBattleStack> Stacks { get; set; } = new();
    public int Winner { get; set; } = -1;
    /// <summary>Quick battle (BA:Q): the result is computed, not fought on the field.</summary>
    public bool Quick { get; set; }
    /// <summary>A town siege (BA:S).</summary>
    public bool Siege { get; set; }
    /// <summary>Both sides are run by the computer (BA:A).</summary>
    public bool CompleteAi { get; set; }
}

/// <summary>A loaded ERM script (text and origin).</summary>
public sealed class WoGScript
{
    public string Name { get; set; } = "";
    public string Text { get; set; } = "";
    /// <summary>True for script text embedded in the map (events/objects), false for Data\s files.</summary>
    public bool FromMap { get; set; }
}
