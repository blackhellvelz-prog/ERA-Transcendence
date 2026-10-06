using System.Linq;
using WoG.Core.Model;
using WoG.Core.State;
using WoG.Erm.Syntax;
using Xunit;

namespace WoG.Tests;

public class ErmParserTests
{
    static ErmScript P(string text, ErmDialect d = ErmDialect.Wog358) => ErmParser.ParseText("t.erm", text, d);

    [Fact]
    public void File_without_ZVSE_is_not_ERM()
    {
        var s = P("Hello !?FU1; !!VRv1:S1;");
        Assert.False(s.IsErm);
        Assert.Empty(s.Items);
    }

    [Fact]
    public void Everything_outside_commands_is_comment()
    {
        var s = P("ZVSE\nSome text! with bangs [and brackets]\n!?FU1;\n** comment\n!!VRv1:S5; trailing words\n");
        var sec = Assert.Single(s.Sections);
        Assert.Equal("FU", sec.Id);
        Assert.Equal(1, sec.EventId);
        var line = Assert.Single(sec.Lines);
        Assert.Equal("VR", line.Id);
        Assert.Equal(ErmVarKind.V, line.Selector[0].Var!.Kind);
        Assert.Equal('S', line.Commands[0].Letter);
        Assert.Equal(5, line.Commands[0].Params[0].Number);
    }

    [Fact]
    public void Parameter_prefixes_follow_GetNum()
    {
        var s = P("ZVSE\n!?FU1;\n!!HE-1:Ed500 F?v1/d-2/>=y-3/vy5;\n");
        var cmds = s.Sections.First().Lines[0].Commands;
        Assert.Equal(2, cmds.Count);
        var e = cmds[0].Params[0];
        Assert.True(e.Add);
        Assert.Equal(500, e.Number);
        var f = cmds[1].Params;
        Assert.Equal(ErmParamMode.Get, f[0].Mode);
        Assert.Equal(ErmVarKind.V, f[0].Var!.Kind);
        Assert.True(f[1].Add);
        Assert.Equal(-2, f[1].Number);
        Assert.Equal(ErmParamMode.Check, f[2].Mode);
        Assert.Equal(ErmCompare.Ge, f[2].Compare);
        Assert.Equal(-3, f[2].Var!.Index);
        Assert.Equal(ErmVarKind.Y, f[3].Var!.IndexKind);
        Assert.Equal(5, f[3].Var!.Index);
    }

    [Theory]
    [InlineData("<", ErmCompare.Lt)]
    [InlineData("=", ErmCompare.Eq)]
    [InlineData("<=", ErmCompare.Le)]
    [InlineData(">", ErmCompare.Gt)]
    [InlineData("<>", ErmCompare.Ne)]
    [InlineData("><", ErmCompare.Ne)]
    [InlineData(">=", ErmCompare.Ge)]
    [InlineData("=>", ErmCompare.Ge)]
    public void Comparison_codes_match_GetCmpCode(string op, ErmCompare expected)
    {
        var s = P($"ZVSE\n!?FU1;\n!!VRv1:S{op}5;\n");
        Assert.Equal(expected, s.Sections.First().Lines[0].Commands[0].Params[0].Compare);
    }

    [Fact]
    public void Quick_vars_and_indirection()
    {
        var s = P("ZVSE\n!?FU1;\n!!VRf:Svf;\n");
        var line = s.Sections.First().Lines[0];
        Assert.Equal(ErmVarKind.Quick, line.Selector[0].Var!.Kind);
        Assert.Equal(1, line.Selector[0].Var!.Index);
        var p = line.Commands[0].Params[0];
        Assert.Equal(ErmVarKind.V, p.Var!.Kind);
        Assert.Equal(ErmVarKind.Quick, p.Var.IndexKind);
    }

    [Fact]
    public void Strings_and_semicolons_inside_strings()
    {
        var s = P("ZVSE\n!?FU1;\n!!IF:M^Hello; world^;\n!!VRv1:S2;\n");
        var lines = s.Sections.First().Lines;
        Assert.Equal(2, lines.Count);
        Assert.Equal("Hello; world", lines[0].Commands[0].Text);
        Assert.True(lines[0].Commands[0].Params[0].Empty);
    }

    [Fact]
    public void Question_with_flag_then_text()
    {
        var s = P("ZVSE\n!?FU1;\n!!IF:Q2^Sure?^;\n");
        var c = s.Sections.First().Lines[0].Commands[0];
        Assert.Single(c.Params);
        Assert.Equal(2, c.Params[0].Number);
        Assert.Equal("Sure?", c.Text);
    }

    [Fact]
    public void Conditions_and_or()
    {
        var s = P("ZVSE\n!?FU1&1/-2/v5>3|v6=z1;\n");
        var c = s.Sections.First().Condition;
        Assert.Equal(3, c.And.Count);
        Assert.Equal(1, c.And[0].Flag);
        Assert.True(c.And[0].FlagSet);
        Assert.Equal(2, c.And[1].Flag);
        Assert.False(c.And[1].FlagSet);
        Assert.Equal(ErmCompare.Gt, c.And[2].Right!.Compare);
        Assert.Single(c.Or);
    }

    [Fact]
    public void Macro_names_are_kept_for_execution()
    {
        var s = P("ZVSE\n!#MCv5:S@gold@;\n!?FU1;\n!!VR$gold$:S7;\n");
        var mc = s.Items.First(i => i.Kind == ErmItemKind.Instruction).Line!;
        Assert.Equal("gold", mc.Commands[0].MacroName);
        Assert.Equal("gold", s.Sections.First().Lines[0].Selector[0].Macro);
    }

    [Fact]
    public void Unknown_receiver_is_skipped_with_warning()
    {
        var s = P("ZVSE\n!?FU1;\n!!QQ:X;\n!!VRv1:S1;\n");
        Assert.Contains(s.Diagnostics, d => d.Severity == ErmSeverity.Warning && d.Message.Contains("QQ"));
        Assert.Single(s.Sections.First().Lines);
    }

    [Fact]
    public void Receiver_before_any_trigger_stops_the_file()
    {
        var s = P("ZVSE\n!!VRv1:S1;\n!?FU1;\n!!VRv2:S2;\n");
        Assert.Contains(s.Diagnostics, d => d.Severity == ErmSeverity.Error);
        Assert.Empty(s.Sections);
    }

    [Fact]
    public void Event_ids_follow_InitTrigger()
    {
        int Ev(string t) => P("ZVSE\n" + t + "\n").Sections.First().EventId;
        Assert.Equal(5, Ev("!?FU5;"));
        Assert.Equal(30002, Ev("!?TM3;"));
        Assert.Equal(30107, Ev("!?HE7;"));
        Assert.Equal(30600, Ev("!?HL-1;"));
        Assert.Equal(30601 + 3, Ev("!?HL3;"));
        Assert.Equal(30300, Ev("!?BA0;"));
        Assert.Equal(30352, Ev("!?BA52;"));
        Assert.Equal(30302, Ev("!?BR;"));
        Assert.Equal(30370, Ev("!?PI;"));
        Assert.Equal(30340 + 2, Ev("!?CO2;"));
        Assert.Equal(ErmEventIds.ObjectPos | new MapPos(10, 20, 1).Pack(), Ev("!?OB10/20/1;"));
        Assert.Equal(ErmEventIds.ObjectPos | new MapPos(10, 20, 1).Pack() | ErmEventIds.PostFlag, Ev("!$OB10/20/1;"));
        Assert.Equal(ErmEventIds.ObjectType | ((63 << 12) + 3), Ev("!?OB63/2;"));
        Assert.Equal(ErmEventIds.ObjectType | (63 << 12), Ev("!?OB63;"));
    }

    [Fact]
    public void Dialect_gates_359_features()
    {
        Assert.Contains(P("ZVSE\n!?FU-1;\n").Diagnostics, d => d.Severity == ErmSeverity.Error);
        Assert.Empty(P("ZVSE\n!?FU-1;\n", ErmDialect.Wog359Alpha).Diagnostics);
        Assert.Contains(P("ZVSE\n!?FU1;\n!!la1;\n").Diagnostics, d => d.Message.Contains("la"));
    }

    [Fact]
    public void Post_trigger_on_table_trigger_without_post_variant_is_skipped()
    {
        var s = P("ZVSE\n!$BA0;\n!?FU1;\n!!VRv1:S1;\n");
        Assert.Single(s.Sections);
        Assert.Contains(s.Diagnostics, d => d.Message.Contains("not yet implemented"));
    }
}
