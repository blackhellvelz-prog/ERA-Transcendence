using System.Collections.Generic;

namespace WoG.Erm.Era;

/// <summary>Era trigger ids (Erm.pas constants) and the names RegisterErmEventNames gives them.</summary>
public static class EraEvents
{
    public const int FU1 = 1, FU29999 = 29999, TM1 = 30000, TM100 = 30099;
    public const int BA0 = 30300, BA1 = 30301, BR = 30302, BG0 = 30303, BG1 = 30304, MW0 = 30305, MW1 = 30306;
    public const int MR0 = 30307, MR1 = 30308, MR2 = 30309;
    public const int CM0 = 30310, CM1 = 30311, CM2 = 30312, CM3 = 30313, CM4 = 30314, AE0 = 30315, AE1 = 30316;
    public const int MM0 = 30317, MM1 = 30318, CM5 = 30319, MP = 30320, SN = 30321, MG0 = 30322, MG1 = 30323;
    public const int TH0 = 30324, TH1 = 30325, IP0 = 30330, IP1 = 30331, IP2 = 30332, IP3 = 30333;
    public const int CO0 = 30340, CO1 = 30341, CO2 = 30342, CO3 = 30343;
    public const int BA50 = 30350, BA51 = 30351, BA52 = 30352, BA53 = 30353, GM0 = 30360, GM1 = 30361;
    public const int PI = 30370, DL = 30371, HM = 30400, HL = 30600, BF = 30800, MF1 = 30801;
    public const int TL0 = 30900, TL1 = 30901, TL2 = 30902, TL3 = 30903, TL4 = 30904;
    public const int ObPos = 0x10000000, LePos = 0x20000000, ObLeave = 0x08000000;

    public const int FirstEraTrigger = 77001;
    public const int SavegameWrite = 77001, SavegameRead = 77002, KeyPress = 77003, OpenHeroScreen = 77004;
    public const int CloseHeroScreen = 77005, StackObtainsTurn = 77006, RegeneratePhase = 77007, AfterSaveGame = 77008;
    public const int BeforeHeroInteract = 77010, AfterHeroInteract = 77011, StackToStackDamage = 77012;
    public const int AICalcStackAttackEffect = 77013, Chat = 77014, GameEnter = 77015, GameLeave = 77016;
    public const int DailyTimer = 77018, BeforeBattlefieldVisible = 77019, BattlefieldVisible = 77020;
    public const int AfterTacticsPhase = 77021, OpenRecruitDlg = 77023, CloseRecruitDlg = 77024;
    public const int RecruitDlgMouseClick = 77025, TownFortMouseClick = 77026, KingdomOverviewMouseClick = 77027;
    public const int RecruitDlgRecalc = 77028, RecruitDlgAction = 77029, LoadHeroScreen = 77030;
    public const int BuildTownBuilding = 77031, OpenTownScreen = 77032, CloseTownScreen = 77033;
    public const int SwitchTownScreen = 77034, PreTownScreen = 77035, PostTownScreen = 77036;
    public const int PreHeroScreen = 77037, PostHeroScreen = 77038, DetermineMonInfoDlgUpgrade = 77039;
    public const int AdvMapTileHint = 77040, BeforeStackTurn = 77041, CalcTownIncome = 77042;
    public const int BattleReplay = 77043, BeforeBattleReplay = 77044, BeforeLocalEvent = 77045;
    public const int AfterLocalEvent = 77046, WinGame = 77047, LoseGame = 77048, TransferHero = 77049;
    public const int AfterHeroGainLevel = 77050, BattleActionEnd = 77051, AfterBuildTownBuilding = 77052;
    public const int KeyReleased = 77053, BeforePlaceBattleObstacles = 77054, AfterPlaceBattleObstacles = 77055;
    public const int BattleStackRegeneration = 77056;
    public const int LastEraTrigger = BattleStackRegeneration;

    /// <summary>RegisterErmEventNames, in its order (aliases first, so the later name wins for id → name).</summary>
    public static readonly (int Id, string Name)[] Names =
    {
        (BA0, "OnBeforeBattle"), (BA1, "OnAfterBattle"), (BR, "OnCombatRound"), (BR, "OnBattleRound"),
        (BG0, "OnBeforeBattleAction"), (BG1, "OnAfterBattleAction"), (MW0, "OnWanderingMonsterReach"),
        (MW1, "OnWanderingMonsterDeath"), (MR0, "OnMagicBasicResistance"), (MR1, "OnMagicCorrectedResistance"),
        (MR2, "OnDwarfMagicResistance"), (CM0, "OnAdventureMapRightMouseClick"), (CM1, "OnTownMouseClick"),
        (CM2, "OnHeroScreenMouseClick"), (CM3, "OnHeroesMeetScreenMouseClick"), (CM4, "OnBattleScreenMouseClick"),
        (CM5, "OnAdventureMapLeftMouseClick"), (AE0, "OnUnequipArt"), (AE1, "OnEquipArt"),
        (MM0, "OnBattleMouseHint"), (MM1, "OnTownMouseHint"), (MP, "OnMp3MusicChange"), (SN, "OnSoundPlay"),
        (MG0, "OnBeforeAdventureMagic"), (MG1, "OnAfterAdventureMagic"), (TH0, "OnEnterTownHall"),
        (TH1, "OnLeaveTownHall"), (IP0, "OnBeforeBattleBeforeDataSend"), (IP1, "OnBeforeBattleAfterDataReceived"),
        (IP2, "OnAfterBattleBeforeDataSend"), (IP3, "OnAfterBattleAfterDataReceived"),
        (CO0, "OnOpenCommanderWindow"), (CO1, "OnCloseCommanderWindow"), (CO2, "OnAfterCommanderBuy"),
        (CO3, "OnAfterCommanderResurrect"), (BA50, "OnBeforeBattleForThisPcDefender"),
        (BA51, "OnAfterBattleForThisPcDefender"), (BA52, "OnBeforeBattleUniversal"),
        (BA53, "OnAfterBattleUniversal"), (GM0, "OnAfterLoadGame"), (GM1, "OnBeforeSaveGame"),
        (PI, "OnAfterErmInstructions"), (DL, "OnCustomDialogEvent"), (HM, "OnHeroMove"), (HL, "OnHeroGainLevel"),
        (BF, "OnSetupBattlefield"), (MF1, "OnMonsterPhysicalDamage"), (TL0, "OnEverySecond"),
        (TL1, "OnEvery2Seconds"), (TL2, "OnEvery5Seconds"), (TL3, "OnEvery10Seconds"), (TL4, "OnEveryMinute"),
        (SavegameWrite, "OnSavegameWrite"), (SavegameRead, "OnSavegameRead"), (KeyPress, "OnKeyPressed"),
        (OpenHeroScreen, "OnOpenHeroScreen"), (CloseHeroScreen, "OnCloseHeroScreen"),
        (StackObtainsTurn, "OnBattleStackObtainsTurn"), (RegeneratePhase, "OnBattleRegeneratePhase"),
        (AfterSaveGame, "OnAfterSaveGame"), (BeforeHeroInteract, "OnBeforeHeroInteraction"),
        (AfterHeroInteract, "OnAfterHeroInteraction"), (StackToStackDamage, "OnStackToStackDamage"),
        (AICalcStackAttackEffect, "OnAICalcStackAttackEffect"), (Chat, "OnChat"), (GameEnter, "OnGameEnter"),
        (GameLeave, "OnGameLeave"), (DailyTimer, "OnEveryDay"),
        (BeforeBattlefieldVisible, "OnBeforeBattlefieldVisible"), (BattlefieldVisible, "OnBattlefieldVisible"),
        (AfterTacticsPhase, "OnAfterTacticsPhase"), (OpenRecruitDlg, "OnOpenRecruitDlg"),
        (CloseRecruitDlg, "OnCloseRecruitDlg"), (RecruitDlgMouseClick, "OnRecruitDlgMouseClick"),
        (TownFortMouseClick, "OnTownFortMouseClick"), (KingdomOverviewMouseClick, "OnKingdomOverviewMouseClick"),
        (RecruitDlgRecalc, "OnRecruitDlgRecalc"), (RecruitDlgAction, "OnRecruitDlgAction"),
        (LoadHeroScreen, "OnLoadHeroScreen"), (BuildTownBuilding, "OnBuildTownBuilding"),
        (OpenTownScreen, "OnOpenTownScreen"), (CloseTownScreen, "OnCloseTownScreen"),
        (SwitchTownScreen, "OnSwitchTownScreen"), (PreTownScreen, "OnPreTownScreen"),
        (PostTownScreen, "OnPostTownScreen"), (PreHeroScreen, "OnPreHeroScreen"),
        (PostHeroScreen, "OnPostHeroScreen"), (DetermineMonInfoDlgUpgrade, "OnDetermineMonInfoDlgUpgrade"),
        (AdvMapTileHint, "OnAdvMapTileHint"), (AdvMapTileHint, "OnAdventureMapTileHint"),
        (BeforeStackTurn, "OnBeforeBattleStackTurn"), (CalcTownIncome, "OnCalculateTownIncome"),
        (BattleReplay, "OnBattleReplay"), (BeforeBattleReplay, "OnBeforeBattleReplay"),
        (BeforeLocalEvent, "OnBeforeLocalEvent"), (AfterLocalEvent, "OnAfterLocalEvent"),
        (WinGame, "OnWinGame"), (LoseGame, "OnLoseGame"), (TransferHero, "OnTransferHero"),
        (AfterHeroGainLevel, "OnAfterHeroGainLevel"), (BattleActionEnd, "OnBattleActionEnd"),
        (AfterBuildTownBuilding, "OnAfterBuildTownBuilding"), (KeyReleased, "OnKeyReleased"),
        (BeforePlaceBattleObstacles, "OnBeforePlaceBattleObstacles"),
        (AfterPlaceBattleObstacles, "OnAfterPlaceBattleObstacles"),
        (BattleStackRegeneration, "OnBattleStackRegeneration"),
    };

    /// <summary>
    /// Function events for the ErmLegacySupport rules of ProcessErm (TRIGGER_FU1..TRIGGER_FU29999 only —
    /// named functions ≥ 95000 count as non-functions there).
    /// </summary>
    public static bool IsClassicFunction(int id) => id >= FU1 && id <= FU29999;

    /// <summary>The "classic WoG non-function trigger" test of ProcessErm (per-section ny reset).</summary>
    public static bool IsClassicNonFunctionTrigger(int id) =>
        id < 0 || (id >= TM1 && id <= TL4) || id >= ObPos;
}
