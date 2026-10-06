using System;
using WoG.Core.State;

namespace WoG.CreatureExperience;

/// <summary>Pure formulas of crexpo.h / crexpo.cpp (CrExpMod, ApplyExpo, ApplyMod).</summary>
public static class ExperienceMath
{
    /// <summary>CrExpMod::Ranks — [0] is the scale base, [1..10] experience per rank step.</summary>
    public static readonly int[] Ranks = { 17500, 1000, 1000, 1200, 1400, 1600, 1800, 2000, 2200, 2500, 2800 };

    /// <summary>
    /// Fallback per-type parameters used only when no CREXPMOD.TXT is available. They are the intended
    /// hard-coded defaults of CrExpMod::Clear (Limit 79400, Cap 50 %, Lvl11 55580). Note: the original
    /// assigns index 1 twice (a bug), which a real install never exposes because the file overrides them.
    /// </summary>
    public static CreatureExpParams FallbackParams() => new() { ExpMul = 1f, UpgrMul = 0.5f, Limit = 79400, Cap = 50, Lvl11Exp = 55580 };

    static float Scale(CreatureExpParams p) => (float)p.Limit / Ranks[0];

    /// <summary>CrExpMod::GetRank: rank 0..10 for an experience value.</summary>
    public static int GetRank(CreatureExpParams p, int exp)
    {
        float scale = Scale(p);
        if (scale < 0.000001f) return 0;
        float expnorm = (float)((exp + 0.00001) / scale);
        for (int i = 1; i < 11; i++)
        {
            expnorm -= Ranks[i];
            if (expnorm < 0) return i - 1;
        }
        return 10;
    }

    /// <summary>CrExpMod::GetRankExp: experience between rank0 and rank (rank 11 adds Lvl11Exp).</summary>
    public static int GetRankExp(CreatureExpParams p, int rank, int rank0 = 0)
    {
        float scale = Scale(p);
        if (scale < 0.000001f) return 0;
        rank = Math.Clamp(rank, 0, 11);
        rank0 = Math.Clamp(rank0, 0, 11);
        if (rank <= rank0) return 0;
        int exp = 0;
        for (int i = rank0; i < rank; i++) exp += i != 10 ? Ranks[i + 1] : p.Lvl11Exp;
        return (int)((exp + 0.01) * scale);
    }

    public static int MaxExpo(CreatureExpParams p) => p.Limit + p.Lvl11Exp;

    /// <summary>CrExpMod::CapIt: at most Limit·Cap/100 (≥ 1) experience per battle.</summary>
    public static int CapIt(CreatureExpParams p, int exp)
    {
        int v = p.Limit * p.Cap / 100;
        if (v < 1) v = 1;
        return exp > v ? v : exp;
    }

    /// <summary>CrExpMod::Exp4Level: experience after moving <paramref name="lvl"/> ranks up from exp0.</summary>
    public static int Exp4Level(CreatureExpParams p, int lvl, int exp0 = 0)
    {
        if (lvl < 0) return 0;
        if (lvl > 10) return MaxExpo(p);
        int lvl0 = GetRank(p, exp0);
        int dexp = exp0 - GetRankExp(p, lvl0);
        lvl0 = Math.Clamp(lvl0 + lvl, 0, 11);
        return GetRankExp(p, lvl0) + dexp;
    }

    /// <summary>
    /// ApplyExpo (crexpo.cpp): new per-creature experience when a stack of type T with N creatures gets
    /// (N - Num) creatures of experience/level E. <paramref name="type"/> = current stack type.
    /// </summary>
    public static int Apply(int exp, int e, int mode, CreatureExpParams typeP, CreatureExpParams tP,
        int type, int t, int num, int n)
    {
        switch (mode)
        {
            case 0:
                if (n == 0) return 0;
                if (type != t) return e;
                return (exp * num + e * (n - num)) / n;
            case 1:
                if (n == 0) return 0;
                return (exp * num + e * (n - num)) / n;
            case 2: return e;
            case 3: return exp + e;
            case 4: return Exp4Level(tP, e, exp);
            case 5: return (int)(exp * typeP.UpgrMul + e);
            case 10:
                if (num + n == 0) return 0;
                if (type != t) return Exp4Level(tP, e, 0);
                return (exp * num + Exp4Level(tP, e, 0) * (n - num)) / n;
            case 11:
                if (n == 0) return 0;
                return (exp * num + Exp4Level(tP, e, 0) * (n - num)) / n;
            case 12: return Exp4Level(tP, e, 0);
            case 13: return typeP.Limit == 0 ? e : exp * tP.Limit / typeP.Limit + e;
            case 14:
            {
                int x = typeP.Limit == 0 ? exp : exp * tP.Limit / typeP.Limit;
                return Exp4Level(tP, e, x);
            }
            default: return exp;
        }
    }

    /// <summary>ApplyMod: '+' add, '-' subtract, '%' val += val·p/100 + 0.5, '=' set. Float as in WoG.</summary>
    public static float ApplyMod(float val, char mod, int perc) => mod switch
    {
        '+' => val + perc,
        '-' => val - perc,
        '%' => val + val * perc / 100.0f + 0.5f,
        '=' => perc,
        _ => val,
    };
}
