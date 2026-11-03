using System.Linq;
using WoG.Core.Adapters;
using Xunit;

namespace WoG.Tests;

/// <summary>IF:G — check boxes and radio buttons (erm.cpp; WoG's CheckBoxesDialog).</summary>
public class ChoiceDialogTests
{
    [Fact]
    public void The_dialog_starts_with_the_bits_of_shown_items()
    {
        string?[] items = { "a", null, "c", "", "e" };
        Assert.Equal(0b10101, Choice.Initial(items, radio: false, 0b11111)); // hidden items are off
        Assert.Equal(0b10000, Choice.Initial(items, radio: true, 0b10101));  // a radio keeps the last item on
        Assert.Equal(0, Choice.Initial(items, radio: true, 0b01010));
        Assert.Equal(0b00110, Choice.Click(0b00010, 2, radio: false));
        Assert.Equal(0b00100, Choice.Click(0b00010, 2, radio: true));
    }

    [Fact]
    public void Map_rules_ask_with_a_string_header_and_get_the_bits()
    {
        // 77 wog - map rules.erm: IF:G0/2/v2/^title^/1/2/3…; the choice goes into v2
        using var t = new EraTestHost().Script("t.erm", "ZVSE2\n!?FU(Go);\n" +
            "!!VRz1:S^One^;\n!!VRz2:S^Two^;\n!!VRz3:S^Three^;\n!!VRv2:S6;\n" +
            "!!IF:G0/2/v2/^Rules^/1/2/3/0;\n" +
            "!!VRv3:Sv2;\n" +
            "!!IF:G1/2/7/^Pick^/1/2/3;\n").Start();
        t.Game.Choices.Enqueue(0b011);                                   // the player's check boxes
        t.Call("Go");
        Assert.Equal("", t.ErrorText);
        Assert.Equal(0b011, t.V(3));
        Assert.Equal(0b100, t.V(2));                                     // no answer queued: closed as it opened
        Assert.Contains("[Rules] One | Two | Three | ", t.Game.Messages);
    }

    /// <summary>
    /// IF:Q with pictures (Request3Pic): type 1 a message (the flag stays), 2 a question; map rules announce their
    /// rules with no picture (type -1); a picture is not shown yet, a choice of pictures is not supported yet.
    /// </summary>
    [Fact]
    public void If_q_with_pictures_shows_messages_and_questions()
    {
        using var t = new EraTestHost().Script("t.erm", "ZVSE2\n!?FU(Go);\n" +
            "!!IF:V2/1;\n!!IF:Q2/-1/-1/1/^Rules^;\n" +                     // a message: flag 2 stays on
            "!!IF:Q3/-1/-1/2/^Agree?^;\n" +
            "!!VRz5:S^Prize^;\n!!IF:Q4/8/7/2/5;\n" +                       // a picture: shown without it
            "!!IF:Q5/8/125/8/110/7/^Which?^;\n").Start();                 // a choice of pictures
        t.Game.Answers.Enqueue(false);
        t.Game.Answers.Enqueue(true);
        t.Call("Go");
        Assert.Equal("", t.ErrorText);
        Assert.Equal((true, false, true), (t.F(2), t.F(3), t.F(4)));
        Assert.Equal(new[] { "Rules", "?Agree?", "?Prize" }, t.Game.Messages.Take(3));
        Assert.Contains(t.Host.Compat.Entries, e => e.Reason.Contains("its pictures not yet"));
        Assert.Contains(t.Host.Compat.Entries, e => e.Reason.Contains("dialog type 7"));
    }

    [Fact]
    public void Wog_dialect_takes_z_numbers_and_checks_its_parameters()
    {
        var t = new TestHost().Load("!?FU1;\n!!VRz9:S^Teams^;\n!!VRz1:S^Red^;\n!!VRz2:S^Blue^;\n" +
            "!!IF:G1/777/1/9/1/2;\n" +
            "!?FU2;\n!!IF:G1/0/1/9/1;\n" +
            "!?FU3;\n!!IF:G1/777/1/9/1/2/3/4/5/6/7/8/9/10/11/12/13;\n" +
            "!?FU4;\n!!IF:G1/777/?v1/9/1;\n").Start();
        t.Game.Choices.Enqueue(2);
        t.Call(1);
        Assert.Equal(0, t.ErrorCount);
        Assert.Equal(2, t.V(777));
        Assert.Contains(t.Game.Messages, m => m == "[Teams] Red | Blue");
        t.Call(2);
        t.Call(3);
        t.Call(4);
        Assert.Equal(3, t.ErrorCount);                                    // v index, too many items, get syntax
    }
}
