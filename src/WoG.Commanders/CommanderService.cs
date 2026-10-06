using System;
using WoG.Core.Model;
using WoG.Core.Options;
using WoG.Core.Random;
using WoG.Core.Services;
using WoG.Core.State;

namespace WoG.Commanders;

/// <summary>Asks a human player which level-up choice to take (the WoG commander dialog).</summary>
public interface ICommanderLevelUpChooser
{
    /// <summary>
    /// Returns the chosen option: a skill index 0..5 (≥ 0) or a special bonus as -(bit+1).
    /// Return null to fall back to the automatic choice.
    /// </summary>
    int? Choose(WoGCommander c, int[] skillOptions, int[] bonusOptions);
}

/// <summary>
/// WoG commander rules (npc.cpp): init, experience and levels, skill gating, special bonuses,
/// effective stats, battle profile, hire/revive prices. State lives in <see cref="WoGGameState"/>.
/// </summary>
public sealed class CommanderService : ICommanderService
{
    readonly WoGGameState state;
    readonly IWoGRandom random;
    readonly Func<int, bool> isAutomatic;
    readonly Action<int, int>? goldGranted;

    public ICommanderLevelUpChooser? Chooser { get; set; }

    /// <param name="isAutomatic">hero → true when level-ups are decided automatically (AI, remote, start-up).</param>
    /// <param name="goldGranted">(hero, gold) callback for the class-5 experience-to-gold bonus.</param>
    public CommanderService(WoGGameState state, IWoGRandom random, Func<int, bool> isAutomatic, Action<int, int>? goldGranted = null)
    {
        this.state = state;
        this.random = random;
        this.isAutomatic = isAutomatic;
        this.goldGranted = goldGranted;
    }

    public WoGCommander Get(int number)
    {
        if (number == -3) return state.ExtraCommanders[0];
        if (number == -4) return state.ExtraCommanders[1];
        if (!state.Commanders.TryGetValue(number, out var c))
        {
            c = new WoGCommander { Number = number };
            state.Commanders[number] = c;
        }
        return c;
    }

    /// <summary>NPC::Init.</summary>
    public void Init(int hero, int heroClass, string name)
    {
        var c = Get(hero);
        Array.Clear(c.Skills, 0, 7);
        c.Primary[0] = 5; c.Primary[1] = 5; c.Primary[5] = 4; c.Primary[3] = 12; c.Primary[4] = 1; c.Primary[2] = 40; c.Primary[6] = 5;
        c.Name = name.Length > 31 ? name.Substring(0, 31) : name;
        foreach (var a in c.Arts) { a[0] = 0; a[1] = 0; }
        c.Exp = 0; c.Level = 0; c.OldHeroExp = 0;
        c.LastExpoInBattle = 0;
        c.CustomPrimary = false;
        c.SpecBon[0] = c.SpecBon[1] = 0;
        c.HType = heroClass;
        c.Type = heroClass / 2;
        c.Used = 0;
        c.Dead = 0;
    }

    public static int NextLevelExp(WoGCommander c) => c.Level > 73 ? 0 : CommanderTables.Levels[c.Level + 1];

    /// <summary>NPC::CalculateSkills: number of the six choosable skills at level ≥ <paramref name="level"/>.</summary>
    public static int CountSkills(WoGCommander c, int level)
    {
        int n = 0;
        for (int i = 0; i < 6; i++) if (c.Skills[i] >= level) n++;
        return n;
    }

    /// <summary>NPC::MayNextSkill.</summary>
    public static bool MayNextSkill(WoGCommander c, int ind) => c.Skills[ind] switch
    {
        0 => CountSkills(c, 1) < 4,
        1 => CountSkills(c, 1) >= 2,
        2 => CountSkills(c, 1) >= 3,
        3 => CountSkills(c, 1) >= 4,
        4 => CountSkills(c, 1) >= 4 && CountSkills(c, 2) >= 4,
        _ => false,
    };

    static readonly (int a, int b)[] Pairs =
    {
        (0, 1), (0, 2), (0, 3), (0, 4), (0, 5), (1, 2), (1, 3), (1, 4), (1, 5), (2, 3), (2, 4), (2, 5), (3, 4), (3, 5), (4, 5),
    };

    /// <summary>NPC::GetAvailableSpecBon(step): the step-th available special bonus bit, or -1.</summary>
    public static int AvailableSpecialBonus(WoGCommander c, int step)
    {
        for (int i = 0; i < 15; i++)
        {
            var (a, b) = Pairs[i];
            if (c.Skills[a] < 4 || c.Skills[b] < 4) continue;
            uint mask = 1u << i;
            if ((c.SpecBon[0] & mask) != 0) continue;
            if ((c.SpecBon[1] & mask) != 0) continue;
            if (step == 0) return i;
            step--;
        }
        return -1;
    }

    /// <summary>NPC::AddExp. Returns the number of levels gained.</summary>
    public int OnHeroExperience(int hero, int newHeroExp, bool auto)
    {
        var c = Get(hero);
        int delExp = newHeroExp - c.OldHeroExp;
        if (c.Type == 0) delExp = delExp * 150 / 100;
        int nExp = c.Exp + delExp;
        int gained = 0;
        bool automatic = auto || isAutomatic(hero);
        while (nExp >= NextLevelExp(c))
        {
            if (c.Level == CommanderTables.MaxLevel) break;
            c.Level++;
            gained++;
            if (!c.CustomPrimary)
            {
                c.Primary[2] = CommanderTables.CalcHp(c.Level);
                c.Primary[3] = CommanderTables.CalcDm(c.Level);
            }
            if (automatic || !ChooseInteractively(c)) AutoLevelUp(c);
        }
        c.Exp = nExp;
        c.OldHeroExp = newHeroExp;
        if (c.Type == 5 && c.LastExpoInBattle == 1)
        {
            // npc.cpp: gold = 50 % of the experience delta for class 5 after a battle
            // (the design comment says "Inferno, 25 %"; the code is followed).
            c.LastExpoInBattle = 0;
            int gold = delExp * 50 / 100;
            if (gold != 0) goldGranted?.Invoke(hero, gold);
        }
        return gained;
    }

    void AutoLevelUp(WoGCommander c)
    {
        int bon = AvailableSpecialBonus(c, 0);
        if (bon != -1)
        {
            uint mask = 1u << bon;
            if ((c.SpecBon[1] & mask) == 0) { c.SpecBon[0] |= mask; return; }
        }
        var w = CommanderTables.AiSkillsChance[Math.Clamp(c.HType, 0, 17)];
        var chance = new int[6];
        int sum = 0;
        for (int i = 0; i < 6; i++) { chance[i] = MayNextSkill(c, i) ? w[i] : 0; sum += chance[i]; }
        if (sum == 0) return;
        int val = random.Next(1, sum);
        for (int i = 0; i < 6; i++)
        {
            if (val <= chance[i]) { RaiseSkill(c, i); break; }
            val -= chance[i];
        }
    }

    static void RaiseSkill(WoGCommander c, int i)
    {
        c.Skills[i]++;
        if (i == 4) c.Skills[6]++; // MP drags MR along
    }

    bool ChooseInteractively(WoGCommander c)
    {
        if (Chooser == null) return false;
        var skills = new System.Collections.Generic.List<int>();
        for (int i = 0; i < 6; i++) if (MayNextSkill(c, i)) skills.Add(i);
        var bonuses = new System.Collections.Generic.List<int>();
        for (int s = 0; ; s++) { int b = AvailableSpecialBonus(c, s); if (b < 0) break; bonuses.Add(b); }
        int? pick = Chooser.Choose(c, skills.ToArray(), bonuses.ToArray());
        if (pick == null) return false;
        if (pick.Value < 0)
        {
            uint mask = 1u << (-pick.Value - 1);
            if ((c.SpecBon[1] & mask) == 0) c.SpecBon[0] |= mask;
        }
        else if (pick.Value < 6) RaiseSkill(c, pick.Value);
        return true;
    }

    /// <summary>NPC::ArtCalcSkill.</summary>
    public static int ArtifactBonus(WoGCommander c, int slot, int skill)
    {
        int art = c.Arts[slot][0], won = c.Arts[slot][1];
        return (art - CommanderTables.ArtBase) switch
        {
            0 when skill == 0 => 5 + won / 6,
            1 when skill == 2 => 12 + won,
            2 when skill == 3 => 12 + won,
            4 when skill == 4 => 1 + won / 10,
            5 when skill == 5 => 1 + won / 10,
            8 when skill == 1 => 5 + won / 6,
            _ => 0,
        };
    }

    /// <summary>NPC::CalcSkill — effective value of stat 0..6.</summary>
    public int CalcSkill(WoGCommander c, int ind) => Calc(c, ind);

    public static int Calc(WoGCommander c, int ind)
    {
        int val = c.Primary[ind];
        bool percent = ind == 2 || ind == 3;
        if (c.Skills[ind] > 0)
        {
            int b = CommanderTables.Bonus[ind][c.Skills[ind] - 1];
            val += percent ? val * b / 100 : b;
        }
        bool ring = false;
        for (int i = 0; i < 10; i++)
        {
            if (c.Arts[i][0] == CommanderTables.ArtBase + 9)
            {
                if (ring) continue;
                if (c.Skills[ind] <= 2)
                {
                    int b = CommanderTables.Bonus[ind][1];
                    val += percent ? val * b / 100 : b;
                }
                ring = true;
                continue;
            }
            int ab = ArtifactBonus(c, i, ind);
            val += percent ? val * ab / 100 : ab;
        }
        return val;
    }

    /// <summary>SetMonInitPars + GetNPCMagicPower + NPC_Resist inputs.</summary>
    public CommanderBattleProfile BattleProfile(WoGCommander c)
    {
        var p = new CommanderBattleProfile
        {
            Attack = Calc(c, 0),
            Defence = Calc(c, 1),
            HitPoints = Calc(c, 2),
            DamageHigh = Calc(c, 3),
            Speed = Calc(c, 5),
            Casts = c.Skills[4] + 1,
            MagicPower = Calc(c, 4),
            MagicResistancePercent = Calc(c, 6),
            CastSpell = CommanderTables.ClassSpell[Math.Clamp(c.Type, 0, 8)],
            SpecialBonuses = c.SpecBon[0],
        };
        p.DamageLow = (c.SpecBon[0] & CommanderTables.AT_DM) != 0 ? p.DamageHigh : p.DamageHigh / 2;
        uint fl = 0;
        if ((c.SpecBon[0] & CommanderTables.AT_SP) != 0) fl |= 0x00001004;
        int slot152 = Array.FindIndex(c.Arts, a => a[0] == 152);
        if (slot152 >= 0 && slot152 < 6 && c.Arts[slot152][1] >= 5) fl |= 0x00001004;
        if ((c.SpecBon[0] & CommanderTables.AT_MP) != 0) fl |= 0x00010000;
        if ((c.SpecBon[0] & CommanderTables.HP_DM) != 0) fl |= 0x00008000;
        if ((c.SpecBon[0] & CommanderTables.DF_DM) != 0) fl |= 0x00080000;
        if ((c.SpecBon[0] & CommanderTables.MP_SP) != 0) fl |= 0x00000002;
        if (Array.Exists(c.Arts, a => a[0] == 153)) fl |= 0x80000008;
        p.MonsterFlags = fl;
        // Necropolis commander (creature types 178/187): magic power / 4, min 1 — "3.58 reduction".
        if (c.Type == 4) p.MagicPower = Math.Max(1, p.MagicPower / 4);
        return p;
    }

    /// <summary>Class 7 (Fortress): the hero's contribution to attack/defence is increased by 50 %.</summary>
    public static int FortressBoost(int valueWithHero, int commanderOwn) => valueWithHero + (valueWithHero - commanderOwn) * 50 / 100;

    /// <summary>NPC2Castle: resurrection price in gold, or -1 when the town's mage guild is too low.</summary>
    public static int ResurrectionPrice(WoGCommander c, int mageGuildLevel)
    {
        if (c.Level >= 30 && mageGuildLevel < 3) return -1;
        if (c.Level >= 20 && mageGuildLevel < 2) return -1;
        if (c.Level >= 10 && mageGuildLevel < 1) return -1;
        return (c.Level * c.Level + c.Level % 2) * 50;
    }

    /// <summary>NPC2Castle hire branch (Used == 0): Init + ResetNPCExp + EnableNPC(hero, 1). Caller deducts the gold.</summary>
    public void Hire(int hero, int heroClass, string name, int heroExp)
    {
        Init(hero, heroClass, name);
        var c = Get(hero);
        c.OldHeroExp = heroExp;
        c.Used = 1;
        c.Dead = 0;
    }

    /// <summary>NPC2Castle revive branch: ResetNPCExp + EnableNPC(hero, 1).</summary>
    public void Revive(int hero, int heroExp)
    {
        var c = Get(hero);
        c.OldHeroExp = heroExp;
        c.Used = 1;
        c.Dead = 0;
    }

    /// <summary>
    /// ResetNPC (new game): every hero's commander is initialised and fed the hero's current experience
    /// (AddExp with auto = 0), then all are enabled/disabled according to options 3 and 6.
    /// </summary>
    public void ResetAll(Func<int, (int heroClass, string name, int exp)> hero)
    {
        for (int i = 0; i < WoGLimits.HeroCount; i++)
        {
            var (cls, name, exp) = hero(i);
            Init(i, cls, name);
            OnHeroExperience(i, exp, auto: false);
        }
        ApplyOptions();
    }

    /// <summary>
    /// UN:P3 / UN:P6 side effect: DisableNPC(-1) (Used = -1) when option 3 is set, otherwise
    /// EnableNPC(-1, !option6) (Used = 1, or 0 when commanders must be hired); Dead = 0 for all heroes.
    /// </summary>
    public void ApplyOptions()
    {
        var o = state.Options;
        bool noNpc = o.Get(WoGOptionIds.NoNPC) != 0;
        int used = noNpc ? -1 : (o.Get(WoGOptionIds.NPC2Hire) != 0 ? 0 : 1);
        for (int i = 0; i < WoGLimits.HeroCount; i++)
        {
            var c = Get(i);
            c.Used = used;
            c.Dead = 0;
        }
    }
}
