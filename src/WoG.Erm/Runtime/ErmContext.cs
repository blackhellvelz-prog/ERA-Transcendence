using WoG.Core.Model;

namespace WoG.Erm.Runtime;

/// <summary>
/// What the engine tells ERM about the moment an event fires (ERM_HeroStr, ERM_PosX/Y/L, GM_ai,
/// CurrentUser). Functions called from a section inherit the caller's context.
/// </summary>
public sealed class ErmEventContext
{
    /// <summary>Current hero (HE-1, CO-1, EX-1); -1 = none.</summary>
    public int Hero { get; set; } = -1;
    public MapPos Position { get; set; } = new(0, 0, 0);
    /// <summary>Player whose action raised the event (CurrentUser).</summary>
    public int Player { get; set; } = -1;
    /// <summary>GM_ai override for flag 1000 (null = ask the adapter whether the player is human).</summary>
    public bool? IsHuman { get; set; }
    /// <summary>Flag 999: the player sits at this PC.</summary>
    public bool? IsLocal { get; set; }
    /// <summary>Attacker/defender heroes during battle (HE-10 / HE-20).</summary>
    public int BattleAttacker { get; set; } = -1;
    public int BattleDefender { get; set; } = -1;
    /// <summary>Set by OB:S-style receivers to veto the native action.</summary>
    public bool CancelNative { get; set; }

    public ErmEventContext Clone() => (ErmEventContext)MemberwiseClone();
}

/// <summary>Aborts the current receiver line (WoG: MError → ProcessCmd l_exit).</summary>
public sealed class ErmRuntimeException : System.Exception
{
    public ErmRuntimeException(string message) : base(message) { }
}
