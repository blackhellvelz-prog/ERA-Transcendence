using System;
using System.Collections.Generic;
using System.Linq;
using WoG.Core.Adapters;
using WoG.Core.Model;
using WoG.Core.Options;
using WoG.Core.Services;
using WoG.Core.State;

namespace WoG.CreatureExperience;

/// <summary>One stat change a rank bonus produces at battle start (input for buff generation).</summary>
public sealed record StackBonusEffect(char Type, char FlagLetter, int Value, bool FlagOn);

/// <summary>WoG stack experience (CrExpoSet / CrExpMod / CrExpBon) over <see cref="WoGGameState"/>.</summary>
public sealed class StackExperienceService : IStackExperienceService
{
    readonly WoGGameState state;
    readonly IGameAdapter game;

    public StackExperienceService(WoGGameState state, IGameAdapter game)
    {
        this.state = state;
        this.game = game;
    }

    StackExperienceState S => state.StackExperience;

    public CreatureExpParams Params(int type) =>
        S.Params.TryGetValue(type, out var p) ? p : LevelDefault(type);

    CreatureExpParams LevelDefault(int type)
    {
        int level = game.Creatures.Get(type, CreatureStat.Level) is { IsOk: true } r ? r.Value : 7;
        if (level < 0 || level > 7) level = 7;
        return S.Params.TryGetValue(-level - 1, out var d) ? d : ExperienceMath.FallbackParams();
    }

    public StackExperienceRecord? Find(StackLocation loc) => S.Records.FirstOrDefault(r => r.Location == loc);

    public StackExperienceRecord GetOrCreate(StackLocation loc, int type, int num)
    {
        var r = Find(loc);
        if (r != null) return r;
        r = new StackExperienceRecord { Location = loc, MType = type, Num = num };
        S.Records.Add(r);
        return r;
    }

    public void Delete(StackLocation loc) => S.Records.RemoveAll(r => r.Location == loc);

    public int GetRank(int creatureType, int exp) => ExperienceMath.GetRank(Params(creatureType), exp);
    public int RankExp(int creatureType, int rank) => ExperienceMath.GetRankExp(Params(creatureType), rank);

    /// <summary>CrExpo::Check4Max.</summary>
    public void ClampToMax(StackExperienceRecord r)
    {
        int max = ExperienceMath.MaxExpo(Params(r.MType));
        if (r.Expo > max) r.Expo = max;
    }

    /// <summary>
    /// CrExpoSet::AddExpo — after a battle won by a human hero: distributes the hero's experience gain
    /// over its stacks according to option 901 (style). Option 900 off or 906 on → nothing.
    /// Dead/empty stacks lose their record; a stack artifact on such a record is reported through
    /// <see cref="ArtifactReturned"/> (WoG gives it back to the hero).
    /// </summary>
    public void OnHeroBattleExperience(int hero, int oldHeroExp, int newHeroExp)
    {
        var opt = state.Options;
        if (opt.Get(WoGOptionIds.CrExpEnable) == 0) return;
        if (opt.Get(WoGOptionIds.ExpGainDis) != 0) return;
        int owner = game.Heroes.Get(hero, HeroStat.Owner) is { IsOk: true } o ? o.Value : -1;
        if (owner < 0 || !(game.Players.IsHuman(owner) is { IsOk: true, Value: true })) return; // "do not support AI experience yet"

        int style = opt.Get(WoGOptionIds.CrExpStyle);
        var stacks = new WoGStack[WoGLimits.ArmySlots];
        var w = new float[WoGLimits.ArmySlots];
        float allW = 0;
        for (int i = 0; i < stacks.Length; i++)
        {
            stacks[i] = game.Heroes.GetStack(hero, i) is { IsOk: true } s ? s.Value : new WoGStack();
            var st = stacks[i];
            if (st.Count == 0 || st.Type == -1) continue;
            var p = Params(st.Type);
            int level = game.Creatures.Get(st.Type, CreatureStat.Level) is { IsOk: true } lv ? lv.Value : 0;
            switch (style)
            {
                case 1: w[i] = 0.9f + (float)st.Count * (7 - level) * p.ExpMul; allW += w[i]; break;
                case 2:
                {
                    int cur = Find(StackLocation.Hero(hero, i))?.Expo ?? 0;
                    w[i] = 0.9f + (float)st.Count * ((level + 1) * 100 + cur + 10) * p.ExpMul / 100;
                    allW += w[i];
                    break;
                }
                case 3: w[i] = st.Count * p.ExpMul; allW += w[i]; break;
                default: w[i] = p.ExpMul; break;
            }
        }
        if (allW == 0) allW = 1;
        for (int i = 0; i < stacks.Length; i++)
        {
            var st = stacks[i];
            var loc = StackLocation.Hero(hero, i);
            if (st.Count == 0 || st.Type == -1)
            {
                var dead = Find(loc);
                if (dead != null && dead.HasArt) ArtifactReturned?.Invoke(hero, dead.ArtCopies + 1);
                Delete(loc);
                continue;
            }
            var cr = Find(loc);
            if (cr == null) cr = GetOrCreate(loc, st.Type, st.Count);
            else Revalidate(cr, st);
            var prm = Params(cr.MType);
            int val;
            if (style == 0)
            {
                // (int)((float)New - Old) is truncated first, then multiplied in float (crexpo.cpp operator order).
                int delta = (int)((float)newHeroExp - oldHeroExp);
                val = ExperienceMath.CapIt(prm, (int)(delta * S.PlayerMult * 1 * w[i] / allW / 100.0f + 0.0001f));
            }
            else
            {
                val = ExperienceMath.CapIt(prm, (int)(((float)newHeroExp - oldHeroExp) * S.PlayerMult * 1 * w[i] / allW / st.Count / 100.0f + 0.0001f));
            }
            if (cr.HasArt && cr.SubArt == 5) val = (int)(val * 1.5);
            cr.Expo += val;
            ClampToMax(cr);
        }
    }

    /// <summary>(hero, count) — stack artifacts returned to the hero when their stack died.</summary>
    public Action<int, int>? ArtifactReturned { get; set; }

    /// <summary>CrExpo::RecalcExp2RealNum(-1, type) — exact port.</summary>
    public static void Revalidate(StackExperienceRecord r, WoGStack st)
    {
        int type = st.Type;
        if (type != -1)
        {
            if (type != 194) // werewolf keeps its experience across form changes
            {
                if (r.MType != type) { r.Expo = 0; r.MType = type; }
            }
            else r.MType = type;
        }
        int n = st.Count;
        if (n == 0) { r.Expo = 0; return; }
        if (n <= r.Num) { r.Num = n; return; }
        r.Expo = unchecked(r.Expo * r.Num / n); // 32-bit int arithmetic as in the original
        r.Num = n;
    }

    /// <summary>ApplyMod + stack-artifact doubling for one bonus line (CrExpBon::Apply).</summary>
    public int ApplyBonus(CreatureExpBonus b, int rank, int baseValue, int artifactSubType)
    {
        int lv = b.Levels[Math.Clamp(rank, 0, 10)];
        (int sub, int add) art = b.Type switch
        {
            'H' => (0, 2),
            'A' => (1, 2),
            'D' => (2, 2),
            'm' => (3, 1),
            'M' => (3, 1),
            'S' => (4, 1),
            _ => (-1, 0),
        };
        float val = baseValue;
        if (art.sub >= 0 && artifactSubType == art.sub)
            val = ExperienceMath.ApplyMod(val, b.Mod, lv * 2) + art.add;
        else
            val = ExperienceMath.ApplyMod(val, b.Mod, lv);
        return (int)val;
    }

    public IReadOnlyList<CreatureExpBonus> Bonuses(int type) =>
        S.Bonuses.TryGetValue(type, out var l) ? l : (IReadOnlyList<CreatureExpBonus>)Array.Empty<CreatureExpBonus>();

    /// <summary>
    /// Battle-start effects for a stack (stats A D H m M S O P R and flags 'f'), as absolute values
    /// computed from the stack's base stats — what CrExpBon::Apply writes into the battle stack.
    /// </summary>
    public List<StackBonusEffect> BattleEffects(int type, int exp, int artifactSubType, Func<char, int> baseStat)
    {
        var res = new List<StackBonusEffect>();
        int rank = GetRank(type, exp);
        foreach (var b in Bonuses(type))
        {
            switch (b.Type)
            {
                case 'A': case 'D': case 'H': case 'm': case 'M': case 'S': case 'O': case 'P':
                    res.Add(new StackBonusEffect(b.Type, '\0', ApplyBonus(b, rank, baseStat(b.Type), artifactSubType), false));
                    break;
                case 'R':
                {
                    int v = (int)ExperienceMath.ApplyMod(baseStat('R'), b.Mod, b.Levels[rank]);
                    if (artifactSubType == 8) v += 2;
                    res.Add(new StackBonusEffect('R', '\0', v, false));
                    break;
                }
                case 'f':
                {
                    int lv = b.Levels[rank];
                    if (lv == 2) break; // unchanged
                    res.Add(new StackBonusEffect('f', b.Mod, 0, lv == 1));
                    break;
                }
            }
        }
        return res;
    }
}
