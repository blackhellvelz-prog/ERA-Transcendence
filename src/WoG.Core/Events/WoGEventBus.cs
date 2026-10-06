using System;
using System.Collections.Generic;
using WoG.Core.Model;

namespace WoG.Core.Events;

/// <summary>Engine-neutral WoG events. The ERM runtime maps them to ERM event ids (02_ERM_Triggers.md).</summary>
public enum WoGEventKind
{
    GameStarted,          // new game, after map setup (instructions, then !?PI)
    GameLoaded,           // !?GM0
    GameSaving,           // !?GM1
    PlayerDayStarted,     // timers (!?TM), commanders daily
    HeroVisitPre,         // !?OB… / !?LE (object at Position, type/subtype)
    HeroVisitPost,        // !$OB…
    HeroMeetsHero,        // !?HE#
    HeroStep,             // !?HM
    HeroLevelUp,          // !?HL
    BattleStart,          // !?BA0 / BA52
    BattleFieldSetup,     // !?BF
    BattleRound,          // !?BR
    BattleActionPre,      // !?BG0
    BattleActionPost,     // !?BG1
    BattleEnd,            // !?BA1 / BA53
    ArtifactEquip,        // !?AE1
    ArtifactUnequip,      // !?AE0
    TownHallEnter,        // !?TH0
    TownHallLeave,        // !?TH1
    CommanderDialog,      // !?CO0..3
}

/// <summary>A raised event with the context ERM receivers see (current hero, position, player).</summary>
public sealed class WoGEvent
{
    public WoGEventKind Kind { get; init; }
    public int Player { get; init; } = -1;
    public int Hero { get; init; } = -1;
    public MapPos Position { get; init; } = MapPos.None;
    public int ObjectType { get; init; } = -1;
    public int ObjectSubType { get; init; } = -1;
    /// <summary>Kind-specific integer argument (hero number for HE, round for BR, sub-id for CO/TH…).</summary>
    public int Arg { get; init; }
    /// <summary>Set by handlers to veto the native action (e.g. OB:S disables the standard visit).</summary>
    public bool CancelNative { get; set; }
}

public sealed class WoGEventBus
{
    readonly List<Action<WoGEvent>> handlers = new();

    public void Subscribe(Action<WoGEvent> handler) => handlers.Add(handler);

    public WoGEvent Raise(WoGEvent e)
    {
        foreach (var h in handlers.ToArray()) h(e);
        return e;
    }
}
