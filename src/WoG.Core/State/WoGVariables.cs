using System;
using System.Collections.Generic;

namespace WoG.Core.State;

/// <summary>ERM variable kinds (numbering follows PARAM_VARTYPE / GetNum vf codes in erm.cpp).</summary>
public enum ErmVarKind
{
    None = 0,
    Flag = 1,
    Quick = 2,   // f..t
    V = 3,
    W = 4,
    X = 5,
    Y = 6,
    Z = 7,
    E = 8,
}

/// <summary>A macro binding created by MC:S (macro name → variable).</summary>
public sealed class ErmMacro
{
    public string Name { get; set; } = "";
    public ErmVarKind Kind { get; set; }
    public int Index { get; set; }
}

/// <summary>TM timer setup (ERMTimer[]).</summary>
public sealed class ErmTimer
{
    public int FirstDay { get; set; }
    public int LastDay { get; set; }
    public int Period { get; set; }
    /// <summary>Bit per player colour (bit 0 = red).</summary>
    public int OwnerMask { get; set; }
}

/// <summary>
/// The persistent ERM variable space. Exactly the set that WoG's SaveERM writes: flags, f..t, v, w,
/// z1..z1000, macros, timers. Function/trigger locals (x, y, e, z-1..z-20) are not here — they are
/// frames owned by the running interpreter and are never saved.
/// </summary>
public sealed class WoGVariables
{
    public const int FlagCount = 1000;
    public const int QuickCount = 15;
    public const int VCount = 10000;
    public const int WCount = 200;
    public const int ZCount = 1000;
    public const int StringMax = 511;
    public const int TimerCount = 100;

    public bool[] Flags { get; set; } = new bool[FlagCount];
    public int[] Quick { get; set; } = new int[QuickCount];
    public int[] V { get; set; } = new int[VCount];
    /// <summary>w-vars per hero (lazily allocated).</summary>
    public Dictionary<int, int[]> W { get; set; } = new();
    /// <summary>Hero whose w-vars are addressed (ERMW, set by IF:W).</summary>
    public int CurrentWHero { get; set; }
    public string[] Z { get; set; } = NewStrings(ZCount);
    public Dictionary<string, ErmMacro> Macros { get; set; } = new(StringComparer.Ordinal);
    public ErmTimer[] Timers { get; set; } = NewTimers();

    public int[] WFor(int hero)
    {
        if (!W.TryGetValue(hero, out var arr))
        {
            arr = new int[WCount];
            W[hero] = arr;
        }
        return arr;
    }

    static string[] NewStrings(int n)
    {
        var a = new string[n];
        for (int i = 0; i < n; i++) a[i] = "";
        return a;
    }

    static ErmTimer[] NewTimers()
    {
        var t = new ErmTimer[TimerCount];
        for (int i = 0; i < t.Length; i++) t[i] = new ErmTimer();
        return t;
    }

    /// <summary>Truncates to WoG's 512-byte buffers (511 characters + terminator).</summary>
    public static string Clip(string s) => s.Length > StringMax ? s.Substring(0, StringMax) : s;
}
