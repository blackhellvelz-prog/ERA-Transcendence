using WoG.Core.Options;

namespace WoG.Host;

/// <summary>The question asked before a map is WoGified, if any.</summary>
public enum WogifyQuestion
{
    None,
    /// <summary>A map without its own scripts (every Olden Era map), with WoG option 5 = 3: ZMESS00.TXT line 226.</summary>
    Map,
    /// <summary>A map with its own scripts. ERA: era.global_scripts_vs_map_scripts_warning; WoG: ZMESS00.TXT line 197.</summary>
    MapScripts,
}

/// <summary>Whether a new map is WoGified when nothing is asked, and the question to ask first.</summary>
public sealed class WogifyPlan
{
    public WogifyPlan(bool wogify, WogifyQuestion question = WogifyQuestion.None)
    {
        Wogify = wogify;
        Question = question;
    }

    /// <summary>The decision without a question; with one, the decision when nobody answers it (as "yes").</summary>
    public bool Wogify { get; }
    public WogifyQuestion Question { get; }
}

/// <summary>
/// WoGification: whether a map starts as a WoG map (WoG option 5, PL_ApplyWoG: 0 never, 1 WoG maps, 2 all, 3 ask).
/// <para>
/// WoG 3.58 (erm.cpp CheckWogify): a map without scripts is WoGified unless the option is 0, and with 3 the player
/// is asked first (ZMESS00 226); a map with its own scripts is not WoGified with 0 and otherwise only when the player
/// agrees (197, 198 for an external .erm). A map that is not WoGified gets the classic rules (ResetNoWoG) and none of
/// the global WoGify scripts.
/// </para>
/// <para>
/// ERA (Erm.pas Hook_FindErm_AfterMapScripts, changelog "WoG Option 5 meaning was changed"): every map is a WoG map
/// (the hard-coded checks read a frozen "WoGify all"), and the option only decides whether the global scripts load:
/// 0 never, 1 and 2 always, 3 always but a map with its own scripts asks first; a fixed script set ("load only these
/// scripts.txt") loads without asking. The answer sets the option to 2 or 0. ERA does not ask for a map without
/// scripts; the port asks there as WoG 3.58 did for the classic RoE/AB/SoD maps, since every Olden Era map is such
/// a map — the question the player knows from WoG.
/// </para>
/// </summary>
public static class Wogification
{
    public const int Never = 0, WogMapsOnly = 1, All = 2, Ask = 3;

    public static WogifyPlan Plan(bool era, int option, int mapScripts, bool fixedScriptSet)
    {
        if (era && fixedScriptSet) return new WogifyPlan(true);
        if (option == Never) return new WogifyPlan(false);
        if (mapScripts > 0)
            return era && option != Ask ? new WogifyPlan(true) : new WogifyPlan(true, WogifyQuestion.MapScripts);
        return option == Ask ? new WogifyPlan(true, WogifyQuestion.Map) : new WogifyPlan(true);
    }

    /// <summary>
    /// ResetNoWoG for a map without scripts: the hard-coded options 0..10 and 900..907 back to the values FindERM keeps
    /// (PL_OptionReset: commanders off, option 5 and "commanders hired in town" as they were; PL_OptionReset2: the stack
    /// experience style and cheats as they were, everything else off), then standard towers, monsters that leave, no
    /// town demolition and no new heroes setup.
    /// </summary>
    public static void ResetNoWoG(WoGOptions o)
    {
        int applyWoG = o.Get(WoGOptionIds.ApplyWoG), npc2Hire = o.Get(WoGOptionIds.NPC2Hire);
        int crExpStyle = o.Get(WoGOptionIds.CrExpStyle), cheatDis = o.Get(WoGOptionIds.CheatDis);
        for (int i = 0; i <= 10; i++) o.Set(i, 0);
        o.Set(WoGOptionIds.NoNPC, 1);
        o.Set(WoGOptionIds.ApplyWoG, applyWoG);
        o.Set(WoGOptionIds.NPC2Hire, npc2Hire);
        for (int i = 900; i < 908; i++) o.Set(i, 0);
        o.Set(WoGOptionIds.CrExpStyle, crExpStyle);
        o.Set(WoGOptionIds.CheatDis, cheatDis);
        o.Set(WoGOptionIds.TowerStd, 1);
        o.Set(WoGOptionIds.MLeaveStd, 1);
        o.Set(WoGOptionIds.NoTownDem, 1);
        o.Set(WoGOptionIds.NewHero, 0);
    }
}
