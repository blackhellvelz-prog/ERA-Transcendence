using WoG.Commanders;
using WoG.Core.Model;
using WoG.Core.Random;
using WoG.Core.State;
using Xunit;

namespace WoG.Tests;

public class CommanderTests
{
    static (CommanderService svc, WoGGameState st) New(bool auto = true)
    {
        var st = new WoGGameState();
        return (new CommanderService(st, new MsvcRandom(7), _ => auto), st);
    }

    [Fact]
    public void Init_matches_npc_cpp()
    {
        var (svc, _) = New();
        svc.Init(3, heroClass: 5, name: "Shiva");
        var c = svc.Get(3);
        Assert.Equal(new[] { 5, 5, 40, 12, 1, 4, 5 }, c.Primary);
        Assert.Equal(2, c.Type);          // class 5 (Wizard) / 2 = Tower
        Assert.Equal(0, c.Level);
        Assert.Equal(0, c.Used);
    }

    [Fact]
    public void Level_table_and_hp_damage_formula()
    {
        var (svc, _) = New();
        svc.Init(0, heroClass: 2, name: "x");     // Rampart: normal experience
        int gained = svc.OnHeroExperience(0, 4600, auto: true);
        var c = svc.Get(0);
        Assert.Equal(4, gained);
        Assert.Equal(4, c.Level);
        Assert.Equal(40 + 4 * 20, c.Primary[2]);
        Assert.Equal(12 + 4 * 4, c.Primary[3]);
        Assert.Equal(4600, c.Exp);
    }

    [Fact]
    public void Castle_commander_gets_150_percent()
    {
        var (svc, _) = New();
        svc.Init(0, heroClass: 0, name: "x");
        svc.OnHeroExperience(0, 2000, auto: true);
        Assert.Equal(3000, svc.Get(0).Exp);
        Assert.Equal(2, svc.Get(0).Level);
    }

    [Fact]
    public void Custom_primary_is_not_overwritten()
    {
        var (svc, _) = New();
        svc.Init(0, heroClass: 2, name: "x");
        var c = svc.Get(0);
        c.CustomPrimary = true;
        c.Primary[2] = 999;
        svc.OnHeroExperience(0, 1000, auto: true);
        Assert.Equal(999, c.Primary[2]);
    }

    [Fact]
    public void Skill_gating_follows_MayNextSkill()
    {
        var c = new WoGCommander();
        Assert.True(CommanderService.MayNextSkill(c, 0));          // none → may learn (fewer than 4 skills)
        c.Skills[0] = 1;
        Assert.False(CommanderService.MayNextSkill(c, 0));         // 1→2 needs ≥ 2 skills
        c.Skills[1] = 1;
        Assert.True(CommanderService.MayNextSkill(c, 0));
        c.Skills[2] = 1; c.Skills[3] = 1;
        Assert.False(CommanderService.MayNextSkill(c, 4));         // already 4 skills: no 5th
        c.Skills[0] = 4;
        Assert.False(CommanderService.MayNextSkill(c, 0));         // 4→5 needs 4 skills at ≥ 2
        c.Skills[1] = 2; c.Skills[2] = 2; c.Skills[3] = 2;
        Assert.True(CommanderService.MayNextSkill(c, 0));
        c.Skills[0] = 5;
        Assert.False(CommanderService.MayNextSkill(c, 0));
    }

    [Fact]
    public void Special_bonus_needs_both_skills_at_4()
    {
        var c = new WoGCommander();
        c.Skills[0] = 4; c.Skills[1] = 3;
        Assert.Equal(-1, CommanderService.AvailableSpecialBonus(c, 0));
        c.Skills[1] = 4;
        Assert.Equal(0, CommanderService.AvailableSpecialBonus(c, 0)); // AT+DF
        c.SpecBon[1] = 1;                                               // forbidden
        Assert.Equal(-1, CommanderService.AvailableSpecialBonus(c, 0));
    }

    [Fact]
    public void CalcSkill_flat_and_percent_with_artifacts()
    {
        var c = new WoGCommander();
        c.Primary[0] = 10; c.Primary[2] = 100;
        c.Skills[0] = 3;   // AT +9
        c.Skills[2] = 2;   // HP +25 %
        Assert.Equal(19, CommanderService.Calc(c, 0));
        Assert.Equal(125, CommanderService.Calc(c, 2));
        c.Arts[0][0] = 146; c.Arts[0][1] = 12;   // AT +5 + 12/6
        Assert.Equal(26, CommanderService.Calc(c, 0));
        c.Arts[1][0] = 147; c.Arts[1][1] = 3;    // HP +15 %
        Assert.Equal(125 + 125 * 15 / 100, CommanderService.Calc(c, 2));
    }

    [Fact]
    public void Battle_profile_flags()
    {
        var (svc, _) = New();
        var c = new WoGCommander { Type = 4 };
        c.Primary = new[] { 5, 5, 40, 12, 8, 4, 5 };
        c.SpecBon[0] = CommanderTables.AT_DM | CommanderTables.MP_SP;
        var p = svc.BattleProfile(c);
        Assert.Equal(12, p.DamageLow);           // AT+DM: always max damage
        Assert.Equal(0x2u, p.MonsterFlags & 0x2u); // MP+SP: flying
        Assert.Equal(2, p.MagicPower);           // necropolis: power / 4
        Assert.Equal(39, p.CastSpell);           // animate dead
    }

    [Theory]
    [InlineData(5, 0, 1300)]
    [InlineData(9, 0, 4100)]
    [InlineData(10, 0, -1)]
    [InlineData(10, 1, 5000)]
    [InlineData(31, 2, -1)]
    public void Resurrection_price(int level, int guild, int expected)
    {
        var c = new WoGCommander { Level = level };
        Assert.Equal(expected, CommanderService.ResurrectionPrice(c, guild));
    }

    [Fact]
    public void Options_enable_disable_all()
    {
        var (svc, st) = New();
        st.Options.Set(3, 1);
        svc.ApplyOptions();
        Assert.Equal(-1, svc.Get(10).Used);
        st.Options.Set(3, 0);
        st.Options.Set(6, 1);
        svc.ApplyOptions();
        Assert.Equal(0, svc.Get(10).Used);
        st.Options.Set(6, 0);
        svc.ApplyOptions();
        Assert.Equal(1, svc.Get(10).Used);
    }

    [Fact]
    public void ERM_CO_reads_and_writes_commanders()
    {
        var t = new TestHost().Load("!?FU1;\n!!CO5:P0/d3 S0/2 X2/?v1;\n!!CO5:N^ignored^;\n").Start();
        var c = t.Host.Commanders!.Get(5);
        c.Primary[0] = 5;
        c.Level = 6;
        t.Call(1);
        Assert.Equal(8, c.Primary[0]);
        Assert.Equal(2, c.Skills[0]);
        Assert.Equal(7, t.V(1));                 // X2 returns level + 1
    }

    [Fact]
    public void ERM_CO_A_fall_through_bug_is_opt_in()
    {
        const string s = "!?FU1;\n!!VRz1:S^Bug^;\n!!CO5:A1/146/0;\n";
        var plain = new TestHost().Load(s).Start();
        plain.Call(1);
        Assert.Equal("", plain.Host.Commanders!.Get(5).Name);
        Assert.Equal(146, plain.Host.Commanders!.Get(5).Arts[0][0]);
        var bug = new TestHost(bugs: true).Load(s).Start();
        bug.Call(1);
        Assert.Equal("Bug", bug.Host.Commanders!.Get(5).Name); // z1 copied into the name, like WoG
    }

    [Fact]
    public void Disabled_module_reports_unsupported()
    {
        var t = new TestHost(modules: new WoG.Host.WoGModules { Commanders = false }).Load("!?FU1;\n!!CO0:E1;\n!!VRv1:S1;\n").Start();
        t.Call(1);
        Assert.Equal(1, t.V(1));
        Assert.Contains(t.Host.Compat.Entries, e => e.Reason.Contains("commander module disabled"));
    }
}
