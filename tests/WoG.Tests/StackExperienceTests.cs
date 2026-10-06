using WoG.Core.Model;
using WoG.Core.State;
using WoG.CreatureExperience;
using Xunit;

namespace WoG.Tests;

public class StackExperienceTests
{
    static readonly CreatureExpParams Unit = new() { ExpMul = 1, UpgrMul = 0.5f, Limit = 17500, Cap = 100, Lvl11Exp = 5000 };

    [Theory]
    [InlineData(0, 0)]
    [InlineData(999, 0)]
    [InlineData(1000, 1)]
    [InlineData(2000, 2)]
    [InlineData(3199, 2)]
    [InlineData(3200, 3)]
    [InlineData(14699, 8)]
    [InlineData(17500, 10)]
    [InlineData(99999, 10)]
    public void Ranks_with_unit_scale(int exp, int rank) => Assert.Equal(rank, ExperienceMath.GetRank(Unit, exp));

    [Fact]
    public void Rank_experience_is_cumulative_and_scaled()
    {
        Assert.Equal(3200, ExperienceMath.GetRankExp(Unit, 3));
        Assert.Equal(17500, ExperienceMath.GetRankExp(Unit, 10));
        Assert.Equal(22500, ExperienceMath.GetRankExp(Unit, 11));
        var big = new CreatureExpParams { Limit = 35000, Lvl11Exp = 0 };
        Assert.Equal(6400, ExperienceMath.GetRankExp(big, 3));
        Assert.Equal(3, ExperienceMath.GetRank(big, 6400));
    }

    [Fact]
    public void Cap_per_battle()
    {
        var p = new CreatureExpParams { Limit = 10000, Cap = 50 };
        Assert.Equal(5000, ExperienceMath.CapIt(p, 9000));
        Assert.Equal(100, ExperienceMath.CapIt(p, 100));
    }

    [Theory]
    [InlineData('+', 10, 3, 13)]
    [InlineData('-', 10, 3, 7)]
    [InlineData('=', 10, 3, 3)]
    [InlineData('%', 10, 25, 13)] // 10 + 2.5 + 0.5 → 13 (truncated)
    [InlineData('%', 7, 10, 8)]   // 7 + 0.7 + 0.5 = 8.2
    public void ApplyMod_matches_WoG(char mod, int val, int perc, int expected) =>
        Assert.Equal(expected, (int)ExperienceMath.ApplyMod(val, mod, perc));

    [Fact]
    public void Merge_modes()
    {
        // 10 creatures with 1000 exp + 10 new with 0 → average 500 (mode 1)
        Assert.Equal(500, ExperienceMath.Apply(1000, 0, 1, Unit, Unit, 1, 1, 10, 20));
        // mode 5 upgrade: exp × UpgrMul + E
        Assert.Equal(600, ExperienceMath.Apply(1000, 100, 5, Unit, Unit, 1, 2, 10, 10));
    }

    [Fact]
    public void Revalidate_follows_RecalcExp2RealNum()
    {
        var r = new StackExperienceRecord { MType = 5, Num = 10, Expo = 1000 };
        StackExperienceService.Revalidate(r, new WoGStack { Type = 5, Count = 20 });
        Assert.Equal(500, r.Expo);
        StackExperienceService.Revalidate(r, new WoGStack { Type = 5, Count = 5 });
        Assert.Equal(500, r.Expo);                // fewer creatures keep their experience
        StackExperienceService.Revalidate(r, new WoGStack { Type = 6, Count = 5 });
        Assert.Equal(0, r.Expo);                  // different creature → reset
        r.Expo = 700;
        StackExperienceService.Revalidate(r, new WoGStack { Type = 194, Count = 5 });
        Assert.Equal(700, r.Expo);                // werewolf exception
    }

    [Fact]
    public void Battle_experience_style0_gives_each_stack_the_hero_gain_times_ExpMul()
    {
        var t = new TestHost().Start();
        t.Game.AddCreature(1, level: 0);
        t.Game.AddCreature(2, level: 6);
        var h = t.Game.AddHero(0, owner: 0);
        h.Army.Slots[0] = new WoGStack { Type = 1, Count = 50 };
        h.Army.Slots[3] = new WoGStack { Type = 2, Count = 2 };
        t.Host.State.Options.Set(900, 1);
        t.Host.State.StackExperience.Params[1] = new CreatureExpParams { ExpMul = 1f, Limit = 17500, Cap = 100, Lvl11Exp = 0 };
        t.Host.State.StackExperience.Params[2] = new CreatureExpParams { ExpMul = 0.5f, Limit = 17500, Cap = 100, Lvl11Exp = 0 };
        t.Host.AfterBattleExperience(0, 1000, 3000);
        var sx = t.Host.StackExperience!;
        Assert.Equal(2000, sx.Find(StackLocation.Hero(0, 0))!.Expo);
        Assert.Equal(1000, sx.Find(StackLocation.Hero(0, 3))!.Expo);
    }

    [Fact]
    public void AI_heroes_do_not_gain_stack_experience()
    {
        var t = new TestHost().Start();
        t.Game.AddCreature(1, level: 0);
        var h = t.Game.AddHero(0, owner: 1);     // player 1 is AI in the headless game
        h.Army.Slots[0] = new WoGStack { Type = 1, Count = 5 };
        t.Host.State.Options.Set(900, 1);
        t.Host.AfterBattleExperience(0, 0, 1000);
        Assert.Null(t.Host.StackExperience!.Find(StackLocation.Hero(0, 0)));
    }

    [Fact]
    public void Txt_loader_reads_defaults_and_overrides()
    {
        string mod = "id\tmul\tupg\tlimit\tcap\tlvl11\n-1\t1.0\t0.5\t17500\t50\t3000\n2\t2.0\t0.25\t35000\t40\t6000\n";
        string bon = "id\ttype\tmod\tr0\tr1\tr2\tr3\tr4\tr5\tr6\tr7\tr8\tr9\tr10\n" +
                     "-1\tA\t+\t0\t1\t1\t1\t2\t2\t2\t3\t3\t3\t4\n" +
                     "1\tA\t+\t0\t2\t2\t2\t4\t4\t4\t6\t6\t6\t8\n" +
                     "1\tf\tF\t2\t2\t2\t2\t2\t1\t1\t1\t1\t1\t1\n";
        var st = new StackExperienceState();
        CrExpTxtLoader.LoadMod(mod, st, 3, _ => 0);
        CrExpTxtLoader.LoadBon(bon, st, 3, _ => 0);
        Assert.Equal(17500, st.Params[0].Limit);
        Assert.Equal(35000, st.Params[2].Limit);
        Assert.Equal(1, st.Bonuses[0][0].Levels[1]);     // level default
        Assert.Equal(2, st.Bonuses[1][0].Levels[1]);     // explicit 'A' replaced the default
        Assert.Equal(2, st.Bonuses[1].Count);            // plus the flag line
    }

    [Fact]
    public void ERM_EX_reads_and_writes_records()
    {
        var t = new TestHost().Load("!?FU1;\n!!EX0/2:E1500;\n!!EX0/2:E?v1 T?v2;\n").Start();
        t.Game.AddCreature(4, level: 1);
        var h = t.Game.AddHero(0, owner: 0);
        h.Army.Slots[2] = new WoGStack { Type = 4, Count = 10 };
        t.Call(1);
        Assert.Equal(1500, t.V(1));
        Assert.Equal(4, t.V(2));
    }
}
