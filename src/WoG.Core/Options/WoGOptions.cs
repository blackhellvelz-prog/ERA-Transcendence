using System;
using System.Collections.Generic;

namespace WoG.Core.Options;

/// <summary>Option indices hard-coded in the WoG engine (erm.h PL_* macros).</summary>
public static class WoGOptionIds
{
    public const int Count = 1000;               // PL_WONUM
    public const int ExtDwellStd = 0;
    public const int TowerStd = 1;               // stored inverted
    public const int MLeaveStd = 2;              // stored inverted
    public const int NoNPC = 3;                  // stored inverted: 0 = commanders enabled
    public const int NoTownDem = 4;              // stored inverted
    public const int ApplyWoG = 5;
    public const int NPC2Hire = 6;
    public const int DwellAccum = 7;
    public const int GuardAccum = 8;
    public const int CentElf = 9;
    public const int MLeaveStyle = 10;
    public const int CrExpEnable = 900;
    public const int CrExpStyle = 901;
    public const int LeaveArt = 902;
    public const int CheatDis = 903;
    public const int ErmErrDis = 904;
    public const int ErmError = 905;
    public const int ExpGainDis = 906;
    public const int NewHero = 907;

    /// <summary>Indices whose stored value is the negation of the dialog check box (wogsetup.cpp).</summary>
    public static bool IsInverted(int index) => index > 0 && index < 5;
}

/// <summary>Metadata of one option, used by the options UI and the compatibility report.</summary>
public sealed class WoGOptionDefinition
{
    public int Index { get; init; }
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string[] AffectedSystems { get; init; } = Array.Empty<string>();
    public int Default { get; init; }
    /// <summary>True when the default was read from WoG data, false when it is our documented assumption.</summary>
    public bool DefaultVerified { get; init; }
}

/// <summary>
/// PL_WoGOptions row 0: 1000 independent integer options, saved with the game. Every index is kept
/// separately — the project forbids collapsing unrelated options into one toggle.
/// </summary>
public sealed class WoGOptions
{
    public int[] Values { get; set; } = new int[WoGOptionIds.Count];

    /// <summary>Raised after a value changes (index, old, new). Not serialised.</summary>
    public event Action<int, int, int>? Changed;

    public int Get(int index)
    {
        Check(index);
        return Values[index];
    }

    public void Set(int index, int value)
    {
        Check(index);
        int old = Values[index];
        Values[index] = value;
        if (old != value) Changed?.Invoke(index, old, value);
    }

    /// <summary>Value as seen by the options dialog check box (undoes the inversion of 1..4).</summary>
    public bool IsChecked(int index) => WoGOptionIds.IsInverted(index) ? Get(index) == 0 : Get(index) != 0;

    public void SetChecked(int index, bool on) => Set(index, WoGOptionIds.IsInverted(index) ? (on ? 0 : 1) : (on ? 1 : 0));

    public bool CommandersEnabled => Get(WoGOptionIds.NoNPC) == 0;
    public bool StackExperienceEnabled => Get(WoGOptionIds.CrExpEnable) != 0;

    public void ApplyDefaults(IEnumerable<WoGOptionDefinition> defs)
    {
        foreach (var d in defs) Values[d.Index] = d.Default;
    }

    static void Check(int index)
    {
        if (index < 0 || index >= WoGOptionIds.Count)
            throw new ArgumentOutOfRangeException(nameof(index), index, "WoG option index must be 0..999");
    }
}
