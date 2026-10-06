using System.Linq;
using WoG.Erm.Runtime;
using WoG.Erm.Syntax;
using Xunit;

namespace WoG.Tests;

public class ErmRuntimeTests
{
    [Fact]
    public void Arithmetic_is_32bit_C()
    {
        var t = new TestHost().Load("!?FU1;\n!!VRv1:S-7 :2;\n!!VRv2:S-7 %2;\n!!VRv3:S2147483647 +1;\n!!VRv4:S12 &10;\n!!VRv5:S12 |3;\n!!VRv6:S12 X10;\n").Start();
        t.Call(1);
        Assert.Equal(-3, t.V(1));
        Assert.Equal(-1, t.V(2));
        Assert.Equal(int.MinValue, t.V(3));
        Assert.Equal(8, t.V(4));
        Assert.Equal(15, t.V(5));
        Assert.Equal(6, t.V(6));
    }

    [Fact]
    public void Division_by_zero_aborts_the_rest_of_the_line()
    {
        var t = new TestHost().Load("!?FU1;\n!!VRv1:S10 :0 +5;\n!!VRv2:S1;\n").Start();
        t.Call(1);
        Assert.Equal(10, t.V(1));     // ':' failed, '+5' not executed
        Assert.Equal(1, t.V(2));      // next line still runs
        Assert.Equal(1, t.ErrorCount);
    }

    [Fact]
    public void Apply_set_add_get_check()
    {
        var t = new TestHost().Load("!?FU1;\n!!VRv1:S10;\n!!VRv1:Sd5;\n!!VRv1:S?v2;\n!!VRv1:S=15;\n!!VRv3:S1;\n!!VRv3&1:S99;\n").Start();
        t.Call(1);
        Assert.Equal(15, t.V(1));
        Assert.Equal(15, t.V(2));
        Assert.True(t.F(1));
        Assert.Equal(99, t.V(3));
    }

    [Theory]
    [InlineData(true, true, false, true)]   // &1/2 → true
    [InlineData(true, false, false, false)] // AND fails, no OR true
    [InlineData(true, false, true, true)]   // AND fails, OR (3) true
    [InlineData(false, false, true, true)]
    public void Conditions_and_then_or(bool f1, bool f2, bool f3, bool expected)
    {
        var t = new TestHost().Load("!?FU1;\n!!VRv1&1/2|3:S1;\n").Start();
        var fl = t.Host.State.Erm.Flags;
        fl[0] = f1; fl[1] = f2; fl[2] = f3;
        t.Call(1);
        Assert.Equal(expected ? 1 : 0, t.V(1));
    }

    [Fact]
    public void Only_or_part_requires_one_true()
    {
        var t = new TestHost().Load("!?FU1;\n!!VRv1|2/3:S1;\n").Start();
        t.Call(1);
        Assert.Equal(0, t.V(1));
        t.Host.State.Erm.Flags[2] = true;
        t.Call(1);
        Assert.Equal(1, t.V(1));
    }

    [Fact]
    public void If_else_endif_with_nesting()
    {
        var script = "!?FU1;\n" +
                     "!!if&x1=1:;\n" +
                     "  !!if&x2=1:; !!VRv1:S11; !!el:; !!VRv1:S10; !!en:;\n" +
                     "!!el&x1=2:;\n" +
                     "  !!VRv1:S20;\n" +
                     "!!el:;\n" +
                     "  !!if&x2=1:; !!VRv1:S31; !!en:;\n" +
                     "  !!VRv2:S1;\n" +
                     "!!en:;\n";
        var t = new TestHost().Load(script).Start();
        t.Call(1, 1, 1); Assert.Equal(11, t.V(1));
        t.Call(1, 1, 0); Assert.Equal(10, t.V(1));
        t.Call(1, 2, 1); Assert.Equal(20, t.V(1));
        t.SetV(1, 0);
        t.Call(1, 3, 0); Assert.Equal(0, t.V(1)); Assert.Equal(1, t.V(2));
        Assert.Equal(0, t.ErrorCount);
    }

    [Fact]
    public void Function_args_return_values_and_restored_x()
    {
        var t = new TestHost().Load("!?FU2;\n!!VRx3:Sx1 +x2;\n!?FU1;\n!!VRx1:S100;\n!!FU2:P4/5/?v1;\n!!VRv2:Sx1;\n").Start();
        t.Call(1);
        Assert.Equal(9, t.V(1));
        Assert.Equal(100, t.V(2));   // caller's x1 restored after the call
    }

    [Fact]
    public void Function_y_vars_are_fresh_per_call_and_shared_by_sections()
    {
        var t = new TestHost().Load("!?FU1;\n!!VRy1:+1;\n!?FU1;\n!!VRy1:+1;\n!!VRv1:Sy1;\n").Start();
        t.Call(1);
        Assert.Equal(2, t.V(1));     // both sections of one call share the frame
        t.Call(1);
        Assert.Equal(2, t.V(1));     // a new call starts from 0
    }

    [Fact]
    public void Trigger_local_y_minus_are_zeroed_per_section()
    {
        var t = new TestHost().Load("!?TM1;\n!!VRy-1:+1;\n!!VRv1:Sy-1;\n!?TM1;\n!!VRy-1:+1;\n!!VRv2:Sy-1;\n").Start();
        t.Erm.Raise(30000, new ErmEventContext { Player = 0 });
        Assert.Equal(1, t.V(1));
        Assert.Equal(1, t.V(2));
    }

    [Fact]
    public void FU_E_ends_only_the_current_section()
    {
        var t = new TestHost().Load("!?FU1;\n!!VRv1:S1;\n!!FU:E;\n!!VRv1:S2;\n!?FU1;\n!!VRv2:S3;\n").Start();
        t.Call(1);
        Assert.Equal(1, t.V(1));
        Assert.Equal(3, t.V(2));
    }

    [Fact]
    public void FU_E_positive_skips_following_sections()
    {
        var t = new TestHost().Load("!?FU1;\n!!FU:E1;\n!?FU1;\n!!VRv1:S1;\n!?FU1;\n!!VRv2:S1;\n").Start();
        t.Call(1);
        Assert.Equal(0, t.V(1));
        Assert.Equal(1, t.V(2));
    }

    [Fact]
    public void DO_loop_counts_in_x16_and_keeps_function_locals()
    {
        var t = new TestHost().Load("!?FU2;\n!!VRv1:+x16;\n!!VRy1:+1;\n!!VRv2:Sy1;\n!?FU1;\n!!DO2/1/5/2:P;\n").Start();
        t.Call(1);
        Assert.Equal(1 + 3 + 5, t.V(1));
        Assert.Equal(3, t.V(2));     // y1 survives between iterations
    }

    [Fact]
    public void Strings_interpolate_and_float_prints_three_decimals()
    {
        var t = new TestHost().Load("!?FU1;\n!!VRv7:S42;\n!!VRe1:S3;\n!!VRe1::2;\n!!VRz1:S^v=%V7 f=%F1 e=%E1 100%%^;\n!!VRz2:S^[%Z1]^;\n").Start();
        t.Call(1);
        Assert.Equal("v=42 f=0 e=1.500 100%", t.Z(1));
        Assert.Equal("[v=42 f=0 e=1.500 100%]", t.Z(2));
    }

    [Fact]
    public void Message_text_goes_to_the_ui_adapter()
    {
        var t = new TestHost().Load("!?FU1;\n!!VRv1:S5;\n!!IF:M^You have %V1 gold^;\n").Start();
        t.Call(1);
        Assert.Equal("You have 5 gold", t.Game.Messages.Single());
    }

    [Fact]
    public void Question_sets_flag()
    {
        var t = new TestHost().Load("!?FU1;\n!!IF:Q5^Sure?^;\n").Start();
        t.Game.Answers.Enqueue(false);
        t.Call(1);
        Assert.False(t.F(5));
        t.Game.Answers.Enqueue(true);
        t.Call(1);
        Assert.True(t.F(5));
    }

    [Fact]
    public void String_copy_concat_and_compare()
    {
        var t = new TestHost().Load("!?FU1;\n!!VRz1:S^Hello^;\n!!VRz2:S1;\n!!VRz2:+^ World^;\n!!VRz3:S^  hello   world ^;\n!!VRv1&z2=z3:S1;\n!!VRz2:H5;\n").Start();
        t.Call(1);
        Assert.Equal("Hello World", t.Z(2));
        Assert.Equal(1, t.V(1));     // StrCmpExt: case- and whitespace-insensitive
        Assert.True(t.F(5));
    }

    [Fact]
    public void VR_U_is_an_ends_with_test_as_in_WoG()
    {
        var t = new TestHost().Load("!?FU1;\n!!VRz1:S^Dragon Utopia^;\n!!VRz1:U^utopia^;\n!!VRv1&1:S1;\n!!VRz1:U^dragon^;\n!!VRv2&-1:S1;\n").Start();
        t.Call(1);
        Assert.Equal(1, t.V(1));
        Assert.Equal(1, t.V(2));     // "dragon" is a prefix, not a suffix → no match
    }

    [Fact]
    public void Macros_resolve_at_execution()
    {
        var t = new TestHost().Load("!#MCv20:S@gold@;\n!?FU1;\n!!VR$gold$:S77;\n!!VRv1:S$gold$;\n").Start();
        t.Call(1);
        Assert.Equal(77, t.V(20));
        Assert.Equal(77, t.V(1));
    }

    [Fact]
    public void Instructions_run_for_new_game_only()
    {
        var a = new TestHost().Load("!#VRv1:S5;\n!?FU1;\n").Start();
        Assert.Equal(5, a.V(1));
        var b = new TestHost();
        b.Host.AddScript("t.erm", "ZVSE\n!#VRv1:S5;\n!?FU1;\n");
        b.Erm.Load(ErmParser.ParseText("t.erm", "ZVSE\n!#VRv1:S5;\n!?FU1;\n"), newGame: false);
        Assert.Equal(0, b.V(1));
    }

    [Fact]
    public void PostInstruction_trigger_fires_after_instructions()
    {
        var t = new TestHost().Load("!#VRv1:S1;\n!?PI;\n!!VRv2:Sv1 +1;\n").Start();
        Assert.Equal(2, t.V(2));
    }

    [Fact]
    public void Context_sets_flag1000_and_position_vars()
    {
        var t = new TestHost().Load("!?TM1;\n!!VRv1:Sv998;\n!!VRv2&1000:S1;\n").Start();
        t.Erm.Raise(30000, new ErmEventContext { Player = 0, Position = new WoG.Core.Model.MapPos(3, 4, 1) });
        Assert.Equal(3, t.V(1));
        Assert.Equal(1, t.V(2));     // player 0 is human in the headless game
    }

    [Fact]
    public void Timers_fire_on_due_days()
    {
        var t = new TestHost().Load("!#TM1:S2/10/3/1;\n!?TM1;\n!!VRv1:+1;\n").Start();
        for (int day = 1; day <= 12; day++) t.Erm.RunTimers(0, day);
        Assert.Equal(3, t.V(1));     // days 2, 5, 8
        t.Erm.RunTimers(1, 5);       // player 1 not in owner mask
        Assert.Equal(3, t.V(1));
    }

    [Fact]
    public void Unsupported_commands_are_reported_not_faked()
    {
        var t = new TestHost().Load("!?FU1;\n!!UN:C1/2/3;\n!!VRv1:S1;\n").Start();
        t.Call(1);
        Assert.Equal(1, t.V(1));
        Assert.Contains(t.Host.Compat.Entries, e => e.Item == "!!UN:C");
        Assert.Equal(0, t.ErrorCount);
    }

    [Fact]
    public void Runaway_recursion_is_stopped()
    {
        var t = new TestHost().Load("!?FU1;\n!!FU1:P;\n").Start();
        t.Call(1);
        Assert.True(t.ErrorCount > 0);
    }

    [Fact]
    public void Goto_labels_in_359_dialect()
    {
        var t = new TestHost(ErmDialect.Wog359Alpha)
            .Load("!?FU1;\n!!la1;\n!!VRv1:+1;\n!!go1&v1<3;\n!!VRv2:S9;\n").Start();
        t.Call(1);
        Assert.Equal(3, t.V(1));
        Assert.Equal(9, t.V(2));
    }

    [Fact]
    public void Local_z_range_depends_on_dialect()
    {
        var t358 = new TestHost().Load("!?FU1;\n!!VRz-11:S^x^;\n").Start();
        t358.Call(1);
        Assert.Equal(1, t358.ErrorCount);
        var t359 = new TestHost(ErmDialect.Wog359Alpha).Load("!?FU1;\n!!VRz-11:S^x^;\n").Start();
        t359.Call(1);
        Assert.Equal(0, t359.ErrorCount);
    }
}
