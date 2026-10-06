using WoG.Core.Compat;
using WoG.Core.Model;
using WoG.Core.Services;
using WoG.Core.State;
using WoG.Erm.Runtime;

namespace WoG.Erm.Receivers;

/// <summary>!!CO — ERM_NPC (npc.cpp), against the commander module.</summary>
public sealed class CoReceiver : ErmReceiverBase
{
    public CoReceiver() : base("CO")
    {
        Declare("EDTHPSANXB", CompatLevel.Emulated, "commanders are an emulated entity on Olden Era");
    }

    protected override void Run(ErmCall c)
    {
        var svc = c.Rt.Services.Commanders ?? throw new ErmUnsupportedException("commander module disabled");
        var all = c.Rt.Services.State.Commanders;
        int ind = c.Selector(0);
        if (ind < -4 || ind >= WoGLimits.HeroCount) throw new ErmRuntimeException("\"!!CO\"-Commander index is out of range.");
        if (ind == -1)
        {
            if (c.Rt.Context.Hero < 0) throw new ErmRuntimeException("\"!!CO\"-Cannot get the current Commander (no current hero)");
            ind = c.Rt.Context.Hero;
        }
        bool forAll = ind == -2;
        WoGCommander npc = ind switch
        {
            -3 => c.Rt.Services.State.ExtraCommanders[0],
            -4 => c.Rt.Services.State.ExtraCommanders[1],
            -2 => svc.Get(0),
            _ => svc.Get(ind),
        };
        bool bugs = c.Rt.Options.ReproduceKnownBugs;
        int v, v2;

        void ForAll(System.Action<WoGCommander> a)
        {
            for (int i = 0; i < WoGLimits.HeroCount; i++) a(svc.Get(i));
        }

        switch (c.Letter)
        {
            case 'E':
                if (forAll)
                {
                    v = 0;
                    if (c.Apply(ref v, 0)) throw new ErmRuntimeException("\"!!CO:E\"-cannot be got or checked for all Commanders.");
                    ForAll(n => n.Used = v);
                    break;
                }
                v = npc.Used; if (!c.Apply(ref v, 0)) npc.Used = v;
                break;
            case 'D':
                if (forAll)
                {
                    v = 0;
                    if (c.Apply(ref v, 0)) throw new ErmRuntimeException("\"!!CO:D\"-cannot be got or checked for all Commanders.");
                    ForAll(n => n.Dead = v);
                    break;
                }
                v = npc.Dead; if (!c.Apply(ref v, 0)) npc.Dead = v;
                break;
            case 'T':
                if (forAll) throw new ErmRuntimeException("\"!!CO:T\"-cannot be applied to all Commanders.");
                v = npc.Type; if (c.Apply(ref v, 0)) break;
                npc.Type = v < 0 ? 0 : v > 8 ? 8 : v;
                break;
            case 'H':
                if (forAll) throw new ErmRuntimeException("\"!!CO:H\"-cannot be applied to all Commanders.");
                v = npc.HType; if (c.Apply(ref v, 0)) break;
                npc.HType = v < 0 ? 0 : v > 17 ? 17 : v;
                break;
            case 'P':
                if (c.Num == 1)
                {
                    if (forAll)
                    {
                        v = 0;
                        if (c.Apply(ref v, 0)) throw new ErmRuntimeException("\"!!CO:P\"-cannot be got or checked for all Commanders.");
                        ForAll(n => n.CustomPrimary = v != 0);
                        break;
                    }
                    v = npc.CustomPrimary ? 1 : 0;
                    if (!c.Apply(ref v, 0)) npc.CustomPrimary = v != 0;
                    break;
                }
                c.RequireMin(2);
                v = 0;
                if (c.Apply(ref v, 0)) throw new ErmRuntimeException("\"!!CO:P\"-cannot get or check the type of skill.");
                if (v < 0 || v > 6) throw new ErmRuntimeException("\"!!CO:P\"-index of skill out of range (0...6).");
                if (forAll)
                {
                    v2 = 0;
                    if (c.Apply(ref v2, 1)) throw new ErmRuntimeException("\"!!CO:P\"-cannot be got or checked for all Commanders.");
                    int k = v, val = v2;
                    ForAll(n => n.Primary[k] = val);
                    break;
                }
                v2 = npc.Primary[v]; if (!c.Apply(ref v2, 1)) npc.Primary[v] = v2;
                break;
            case 'S':
                c.RequireMin(2);
                v = 0;
                if (c.Apply(ref v, 0)) throw new ErmRuntimeException("\"!!CO:S\"-cannot get or check the type of skill level.");
                if (v < 0 || v > 6) throw new ErmRuntimeException("\"!!CO:S\"-index of skill level out of range (0...6).");
                if (forAll)
                {
                    v2 = 0;
                    if (c.Apply(ref v2, 1)) throw new ErmRuntimeException("\"!!CO:S\"-cannot be got or checked for all Commanders.");
                    int k = v, val = v2;
                    ForAll(n => n.Skills[k] = val);
                    break;
                }
                v2 = npc.Skills[v]; if (!c.Apply(ref v2, 1)) npc.Skills[v] = v2;
                break;
            case 'A':
                if (forAll) throw new ErmRuntimeException("\"!!CO:A\"-cannot be applied to all Commanders.");
                Artifacts(c, npc);
                if (bugs) Name(c, npc); // npc.cpp: missing 'break' — CO:A falls through into CO:N
                break;
            case 'N':
                if (forAll) throw new ErmRuntimeException("\"!!CO:S\"-cannot be got or checked for all Commanders.");
                Name(c, npc);
                break;
            case 'X':
                c.RequireMin(2);
                v = 0;
                if (c.Apply(ref v, 0)) throw new ErmRuntimeException("\"!!CO:X\"-cannot get or check the type of exp");
                if (v < 0 || v > 2) throw new ErmRuntimeException("\"!!CO:X\"-index of exp type out of range (0...2)");
                if (forAll)
                {
                    v2 = 0;
                    if (c.Apply(ref v2, 1)) throw new ErmRuntimeException("\"!!CO:X\"-cannot be got or checked for all Commanders.");
                    int k = v, val = v2;
                    ForAll(n => { if (k == 0) n.OldHeroExp = val; else if (k == 1) n.Exp = val; else n.Level = val - 1; });
                    break;
                }
                switch (v)
                {
                    case 0: v2 = npc.OldHeroExp; if (!c.Apply(ref v2, 1)) npc.OldHeroExp = v2; break;
                    case 1: v2 = npc.Exp; if (!c.Apply(ref v2, 1)) npc.Exp = v2; break;
                    case 2: v2 = npc.Level + 1; if (!c.Apply(ref v2, 1)) npc.Level = v2 - 1; break;
                }
                break;
            case 'B':
                SpecialBonuses(c, npc, forAll, svc, bugs);
                break;
            default:
                throw ErmCall.WrongCommand(c.Letter);
        }
    }

    static void Name(ErmCall c, WoGCommander npc)
    {
        var rt = c.Rt;
        if (c.IsGet(0))
        {
            int sind = rt.ResolveIndex(c.P(0).Var!);
            if (sind < -20 || sind == 0 || sind > 1000) throw new ErmRuntimeException("\"CO:N\"-wrong z var index (-20...-1,1...1000).");
            rt.SetString(sind, npc.Name.Length > 31 ? npc.Name.Substring(0, 31) : npc.Name);
        }
        else
        {
            int sind = c.N(0);
            if (sind < -20 || sind == 0 || sind > 1000) throw new ErmRuntimeException("\"CO:N\"-wrong z var index (-20...-1,1...1000).");
            string s = rt.GetStringRaw(sind);
            npc.Name = s.Length > 31 ? s.Substring(0, 31) : s;
        }
    }

    static void Artifacts(ErmCall c, WoGCommander npc)
    {
        var rt = c.Rt;
        int v = 0, v2 = 0, v3 = 0;
        if (c.Apply(ref v, 0)) throw new ErmRuntimeException("\"!!CO:A\"-cannot get or check the command type.");
        rt.Services.State.Erm.V[0] = 0;
        switch (v)
        {
            case 1:
            {
                if (c.Num < 3) throw new ErmRuntimeException("\"!!CO:A1\"-wrong syntax");
                if (c.Apply(ref v2, 1)) throw new ErmRuntimeException("\"!!CO:A1\"-cannot get or check art type.");
                if (c.Apply(ref v3, 2)) throw new ErmRuntimeException("\"!!CO:A1\"-cannot get or check number of battles.");
                if (v2 < WoGLimits.CommanderArtifactFirst || v2 >= WoGLimits.CommanderArtifactFirst + 10) { rt.Services.State.Erm.V[0] = 1; break; }
                int slot = -1;
                bool has = false;
                for (int i = 0; i < 6; i++) { if (npc.Arts[i][0] == v2) has = true; }
                if (has) { rt.Services.State.Erm.V[0] = 3; break; }
                for (int i = 0; i < 6; i++) if (npc.Arts[i][0] <= 0) { slot = i; break; }
                if (slot == -1) { rt.Services.State.Erm.V[0] = 4; break; }
                npc.Arts[slot][0] = v2;
                npc.Arts[slot][1] = v3;
                break;
            }
            case 2:
            {
                if (c.Num < 2) throw new ErmRuntimeException("\"!!CO:A2\"-wrong syntax");
                if (c.Apply(ref v2, 1)) throw new ErmRuntimeException("\"!!CO:A2\"-cannot get or check art type.");
                int slot = -1;
                for (int i = 0; i < 6; i++) if (npc.Arts[i][0] == v2) { slot = i; break; }
                if (slot == -1) { rt.Services.State.Erm.V[0] = 1; break; }
                npc.Arts[slot][0] = 0;
                npc.Arts[slot][1] = 0;
                break;
            }
            case 3:
            {
                if (c.Num < 4) throw new ErmRuntimeException("\"!!CO:A3\"-wrong syntax");
                int slot = 0;
                if (c.Apply(ref slot, 1)) throw new ErmRuntimeException("\"!!CO:A3\"-cannot get or check art slot.");
                if (slot < 0 || slot > 5) throw new ErmRuntimeException("\"!!CO:A3\"-incorrect art slot (0...5).");
                v2 = npc.Arts[slot][0]; v3 = npc.Arts[slot][1];
                if (!c.Apply(ref v2, 2)) npc.Arts[slot][0] = v2;
                if (!c.Apply(ref v3, 3)) npc.Arts[slot][1] = v3;
                break;
            }
            case 4:
                if (c.Num < 13) throw new ErmRuntimeException("\"!!CO:A4\"-wrong syntax");
                for (int i = 0; i < 6; i++)
                {
                    v2 = npc.Arts[i][0]; v3 = npc.Arts[i][1];
                    if (!c.Apply(ref v2, 1 + i * 2)) npc.Arts[i][0] = v2;
                    if (!c.Apply(ref v3, 2 + i * 2)) npc.Arts[i][1] = v3;
                }
                break;
        }
    }

    static void SpecialBonuses(ErmCall c, WoGCommander npc, bool forAll, ICommanderService svc, bool bugs)
    {
        c.RequireMin(2);
        int v = 0;
        if (c.Apply(ref v, 0)) throw new ErmRuntimeException("\"!!CO:B\"-cannot get or check the type of spec bonus");
        if (v < 0 || v > 3) throw new ErmRuntimeException("\"!!CO:B\"-index of spec bonus type out of range (0...3)");
        int mask = v >= 2 ? 1 : 0; // 0/1 → owned mask, 2/3 → forbidden mask
        if (forAll)
        {
            int v2 = 0;
            if (c.Apply(ref v2, 1)) throw new ErmRuntimeException("\"!!CO:B\"-cannot be got or checked for all Commanders.");
            if (v == 0 || v == 2)
            {
                for (int i = 0; i < WoGLimits.HeroCount; i++) svc.Get(i).SpecBon[mask] = unchecked((uint)v2);
                return;
            }
            if (c.Num < 3) throw new ErmRuntimeException("\"!!CO:B\"-wrong syntax");
            if (v2 < 0 || v2 > 14) throw new ErmRuntimeException("\"!!CO:B\"-index of spec bonus out of range (0...14)");
            int v3 = 0;
            if (c.Apply(ref v3, 2)) throw new ErmRuntimeException("\"!!CO:B\"-cannot be got or checked for all Commanders.");
            uint bit = 1u << v2;
            for (int i = 0; i < WoGLimits.HeroCount; i++)
            {
                var n = svc.Get(i);
                n.SpecBon[mask] &= ~bit;
                if (v3 != 0) n.SpecBon[mask] |= bit;
            }
            return;
        }
        if (v == 0 || v == 2)
        {
            int m = unchecked((int)npc.SpecBon[mask]);
            if (!c.Apply(ref m, 1)) npc.SpecBon[mask] = unchecked((uint)m);
            return;
        }
        c.RequireMin(3);
        int idx = 0;
        if (c.Apply(ref idx, 1)) throw new ErmRuntimeException("\"!!CO:B\"-cannot get or check spec bonus index.");
        if (idx < 0 || idx > 14) throw new ErmRuntimeException("\"!!CO:B\"-index of spec bonus out of range (0...14)");
        uint b = 1u << idx;
        int has = (npc.SpecBon[mask] & b) != 0 ? 1 : 0;
        if (c.Apply(ref has, 2)) return;
        npc.SpecBon[mask] &= ~b;
        // npc.cpp bug: CO:B3 clears the bit in the forbidden mask but sets it in the owned mask.
        int setMask = (bugs && v == 3) ? 0 : mask;
        if (has != 0) npc.SpecBon[setMask] |= b;
    }
}

/// <summary>!!EX — ERM_StackExperience, against the stack-experience module.</summary>
public sealed class ExReceiver : ErmReceiverBase
{
    public ExReceiver() : base("EX")
    {
        Declare("ENTAR", CompatLevel.Emulated, "experience is external WoG state applied to OE stacks");
        Declare("C", CompatLevel.Unsupported, "stack combining not implemented yet");
    }

    protected override void Run(ErmCall c)
    {
        var sx = c.Rt.Services.StackExperience ?? throw new ErmUnsupportedException("stack experience module disabled");
        var heroes = c.Rt.Services.Game.Heroes;
        StackLocation loc;
        int mtype, mnum;
        if (c.SelectorCount == 2)
        {
            int h = c.Selector(0);
            if (h < -1 || h >= WoGLimits.HeroCount) throw new ErmRuntimeException("\"!!EX\"- incorrect hero index");
            if (h == -1)
            {
                if (c.Rt.Context.Hero < 0) throw new ErmRuntimeException("\"!!EX\"- default hero is not available here");
                h = c.Rt.Context.Hero;
            }
            int slot = c.Selector(1);
            if (slot < 0 || slot >= 7) throw new ErmRuntimeException("\"!!EX\"- incorrect index");
            var st = c.Need(heroes.GetStack(h, slot));
            loc = StackLocation.Hero(h, slot);
            mtype = st.Type;
            mnum = st.Count;
        }
        else
        {
            throw new ErmUnsupportedException("EX by map position (towns, mines, garrisons) not mapped yet");
        }

        var existing = sx.Find(loc);
        var rec = existing ?? new StackExperienceRecord { Location = loc, MType = mtype, Num = mnum };
        void Store() { if (existing == null) { var r = sx.GetOrCreate(loc, rec.MType, rec.Num); r.Expo = rec.Expo; r.HasArt = rec.HasArt; r.Art = rec.Art; r.SubArt = rec.SubArt; r.ArtCopies = rec.ArtCopies; } }
        int v;
        switch (c.Letter)
        {
            case 'E': v = rec.Expo; if (c.Apply(ref v, 0)) break; rec.Expo = v; Store(); break;
            case 'N': v = rec.Num; if (c.Apply(ref v, 0)) break; rec.Num = v; Store(); break;
            case 'T': v = rec.MType; if (c.Apply(ref v, 0)) break; rec.MType = (ushort)v; Store(); break;
            case 'A':
            {
                c.RequireMin(3);
                int m = rec.MType, n = rec.Num, e = rec.Expo, fl = 0;
                if (c.Apply(ref m, 0)) fl++;
                if (c.Apply(ref n, 1)) fl++;
                if (c.Apply(ref e, 2)) fl++;
                if (fl == 3) break;
                rec.MType = (ushort)m; rec.Num = n; rec.Expo = e;
                Store();
                break;
            }
            case 'R':
            {
                c.RequireMin(2);
                if (c.Num == 4)
                {
                    int h = rec.HasArt ? 1 : 0, a = rec.Art, s = rec.SubArt, cp = rec.ArtCopies, fl = 4;
                    if (c.Apply(ref h, 0)) fl--;
                    if (c.Apply(ref a, 1)) fl--;
                    if (c.Apply(ref s, 2)) fl--;
                    if (c.Apply(ref cp, 3)) fl--;
                    if (fl == 0) break;
                    rec.HasArt = h != 0; rec.Art = a; rec.SubArt = s; rec.ArtCopies = cp < 0 ? 0 : cp > 3 ? 3 : cp;
                    Store();
                    break;
                }
                int art = rec.HasArt ? rec.Art : -1, opt = rec.HasArt ? rec.SubArt : 0;
                bool g = c.Apply(ref art, 0) | c.Apply(ref opt, 1);
                if (g) break;
                if (art == -1) { rec.HasArt = false; rec.SubArt = 0; rec.ArtCopies = 0; }
                else { rec.HasArt = true; rec.Art = art; rec.SubArt = opt; rec.ArtCopies = 0; }
                Store();
                break;
            }
            default:
                throw ErmCall.WrongCommand(c.Letter);
        }
    }
}
