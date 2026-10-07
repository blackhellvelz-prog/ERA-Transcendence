using System.Collections.Generic;
using WoG.Core.Model;

namespace WoG.Erm.Syntax;

/// <summary>
/// Trigger → internal event id, reproducing InitTrigger / ERM_Triggers[] of erm.cpp.
/// Object/local-event ids use the same flag bits as WoG with the port's own position packing.
/// </summary>
public static class ErmEventIds
{
    public const int FunctionMax = 30000;
    public const int TimerBase = 30000;           // TM1 = 30000
    public const int LocalFunctionBase = 31000;   // FU-1 = 31000 [3.59]
    public const int AutoTimerBase = 31100;       // TM a/b/c/d [3.59]
    public const int ObjectPos = 0x10000000;
    public const int LocalEvent = 0x20000000;
    public const int ObjectType = 0x40000000;
    public const int PostFlag = 0x08000000;
    public const int GlobalEventBase = 0x04000000; // GE#: port-specific (WoG stores an event address)

    sealed record Range(string Id, int Event, int Min, int Max, bool Post, bool Is359);

    /// <summary>ERM_Triggers[] — triggers with one (or no) parameter.</summary>
    static readonly Range[] Table =
    {
        new("IP", 30330, 0, 3, false, false),
        new("BA", 30300, 0, 1, false, false),
        new("BA", 30350, 50, 54, false, false),
        new("BF", 30800, 0, 1, false, false),
        new("BR", 30302, 0, 0, false, false),
        new("MM", 30317, 0, 1, false, false),
        new("AE", 30315, 0, 1, false, false),
        new("BG", 30303, 0, 1, false, false),
        new("CM", 30310, 0, 4, false, false),
        new("CM", 30319, 5, 5, false, false),
        new("MW", 30305, 0, 1, false, false),
        new("TH", 30324, 0, 1, false, false),
        new("HE", 30100, 0, WoGLimits.HeroCount - 1, false, false),
        new("HM", 30400, -1, WoGLimits.HeroCount - 1, false, false),
        new("HL", 30600, -1, WoGLimits.HeroCount - 1, false, false),
        new("CO", 30340, 0, 3, false, false),
        new("MP", 30320, 0, 0, false, false),
        new("MG", 30322, 0, 1, false, false),
        new("SN", 30321, 0, 0, false, false),
        new("MR", 30307, 0, 2, false, false),
        new("MF", 30801, 0, 1, false, false),
        new("GM", 30360, 0, 1, false, false),
        new("PI", 30370, 0, 0, false, false),
        new("TL", 30900, 0, 4, false, true),
        new("DL", 30371, 0, 0, false, true),
        new("HD", 30372, 0, 0, false, true),
        new("CI", 30373, 0, 1, false, true),
        new("FC", 30375, 0, 0, false, true),
        new("DG", 30376, 0, 0, false, true),
        new("HL", 31200, -1, WoGLimits.HeroCount - 1, true, true),
        new("AI", 30377, 0, 0, false, true),
    };

    public enum Outcome { Ok, Skip, Error }

    /// <summary>
    /// Computes the event id. <see cref="Outcome.Skip"/> = "wrong or not yet implemented trigger type"
    /// (WoG skips the header and keeps parsing); <see cref="Outcome.Error"/> = WoG aborts the file.
    /// </summary>
    public static Outcome Compute(string id, bool post, IReadOnlyList<int> n, ErmDialect dialect,
        out int eventId, out string message)
    {
        eventId = 0;
        message = "";
        int num = n.Count;
        switch (id)
        {
            case "LE":
                if (num != 3) return Err("LE needs x/y/l", out message);
                eventId = LocalEvent | new MapPos(n[0], n[1], n[2]).Pack() | (post ? PostFlag : 0);
                return Outcome.Ok;
            case "GE":
                if (num != 1 || n[0] < 0) return Err("wrong global event index", out message);
                eventId = GlobalEventBase | n[0];
                return Outcome.Ok;
            case "OB":
                switch (num)
                {
                    case 1: eventId = ObjectType | (n[0] << 12); break;
                    case 2: eventId = ObjectType | ((n[0] << 12) + (n[1] + 1)); break;
                    case 3: eventId = ObjectPos | new MapPos(n[0], n[1], n[2]).Pack(); break;
                    default: return Err("wrong number of parameters", out message);
                }
                if (post) eventId |= PostFlag;
                return Outcome.Ok;
            case "FU":
                if (num != 1) return Err("wrong number of parameters", out message);
                if (dialect == ErmDialect.Era)
                {
                    // Era patches FindErm "to allow functions with arbitrary positive IDs" (named functions
                    // get ids from 95000, Era events from 77001); the id is the event id.
                    eventId = n[0];
                    return Outcome.Ok;
                }
                if (n[0] < -100 || n[0] == 0 || n[0] > FunctionMax) return Err("wrong function index (-100...30000).", out message);
                if (n[0] < 0 && dialect != ErmDialect.Wog359Alpha) return Err("local functions (FU-#) are WoG 3.59", out message);
                eventId = n[0] < 0 ? -n[0] + LocalFunctionBase - 1 : n[0];
                return Outcome.Ok;
            case "TM":
                if (num == 4)
                {
                    if (dialect != ErmDialect.Wog359Alpha) return Err("auto-timers TM a/b/c/d are WoG 3.59", out message);
                    eventId = AutoTimerBase; // the runtime assigns the slot
                    return Outcome.Ok;
                }
                if (num != 1) return Err("wrong number of parameters", out message);
                if (n[0] < 1 || n[0] > 100) return Err("wrong timer index (1...100).", out message);
                eventId = n[0] - 1 + TimerBase;
                return Outcome.Ok;
        }

        bool found = false;
        foreach (var r in Table)
        {
            if (r.Id != id || r.Post != post) continue;
            if (r.Is359 && dialect == ErmDialect.Wog358) continue;
            // Era's WoG base knows TL and DL (TRIGGER_TL0..4, TRIGGER_DL) but not the other 3.59-alpha triggers.
            if (r.Is359 && dialect == ErmDialect.Era && r.Id != "TL" && r.Id != "DL") continue;
            found = true;
            int v = num >= 1 ? n[0] : 0;
            if (v >= r.Min && v <= r.Max)
            {
                if (num != 1) return Err("wrong number of parameters", out message);
                eventId = r.Event - r.Min + v;
                return Outcome.Ok;
            }
        }
        if (found) return Err($"wrong index for !?{id}", out message);
        message = "wrong or not yet implemented trigger type.";
        return Outcome.Skip;
    }

    static Outcome Err(string m, out string message)
    {
        message = m;
        return Outcome.Error;
    }

    // StoreVars uses Event < 30000: event 30000 is TM1 even though FU accepts index 30000 (WoG collision).
    public static bool IsFunction(int ev) => (ev >= 1 && ev < FunctionMax) || IsLocalFunction(ev);
    public static bool IsLocalFunction(int ev) => ev >= LocalFunctionBase && ev < LocalFunctionBase + 100;
}
