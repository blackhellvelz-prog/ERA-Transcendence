using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WoG.Core.Visual;
using WoG.Erm.Era;
using WoG.Erm.Runtime;
using WoG.Erm.Syntax;
using WoG.Headless;
using WoG.Host;
using Xunit;

namespace WoG.Tests;

/// <summary>An ERA host over the headless engine with scripts in a temporary mod folder.</summary>
public sealed class EraTestHost : IDisposable
{
    public HeadlessGame Game { get; } = new();
    public WoGHost Host { get; }
    public ErmRuntime Erm => Host.Erm!;
    public string ModDir { get; }

    public EraTestHost(bool legacy = true)
    {
        ModDir = Path.Combine(Path.GetTempPath(), "era-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(ModDir, "Data", "s"));
        Host = new WoGHost(Game, new VisualResolver(new NoAssets()), null,
            new ErmRuntimeOptions { Dialect = ErmDialect.Era, EraLegacySupport = legacy });
        Host.AddEraMods(new[] { ModDir });
    }

    public EraTestHost Script(string name, string text)
    {
        File.WriteAllText(Path.Combine(ModDir, "Data", "s", name), text);
        return this;
    }

    public EraTestHost Lang(string json)
    {
        Directory.CreateDirectory(Path.Combine(ModDir, "Lang", "en"));
        File.WriteAllText(Path.Combine(ModDir, "Lang", "en", "test.json"), json);
        return this;
    }

    public EraTestHost Start()
    {
        Host.StartNewGame();
        return this;
    }

    public int Func(string name) => Erm.EraNames.Functions[name];

    /// <summary>Fires a named function with x1..xN, returns the x vars it ended with.</summary>
    public int[] Call(string name, params int[] args)
    {
        Erm.RaiseEra(Func(name), new ErmEventContext(), args);
        return Erm.RetX;
    }

    public int V(int i) => Host.State.Erm.V[i - 1];
    public string Z(int i) => Host.State.Erm.Z[i - 1];
    public bool F(int i) => Host.State.Erm.Flags[i - 1];
    public int Errors => Erm.Diagnostics.Count(d => d.Severity == ErmSeverity.Error);
    public string ErrorText => string.Join("\n", Erm.Diagnostics.Where(d => d.Severity == ErmSeverity.Error));

    public void Dispose()
    {
        try { Directory.Delete(ModDir, true); } catch (IOException) { }
    }
}

public class EraPreprocessorTests
{
    static string Pp(string text, EraNames? names = null)
    {
        names ??= new EraNames();
        if (names.Functions.Count == 0) names.ResetFunctions();
        var diags = new List<ErmDiagnostic>();
        string r = EraPreprocessor.Process("t.erm", text, names, diags);
        Assert.True(diags.Count == 0, string.Join("\n", diags));
        return r;
    }

    [Fact]
    public void Named_functions_get_ids_from_95000_and_events_keep_era_ids()
    {
        var names = new EraNames();
        names.ResetFunctions();
        string r = Pp("ZVSE2\n!?FU(OnEveryDay);\n!!FU(MyFunc):P;\n!?FU(MyFunc);\n", names);
        Assert.Equal("ZVSE2\n!?FU77018;\n!!FU95000:P;\n!?FU95000;\n", r);
        Assert.Equal(95000, names.State.Assoc["MyFunc"].Int); // NameTrigger also sets i^MyFunc^
    }

    [Fact]
    public void Classic_scripts_only_get_function_names()
    {
        // In a ZVSE (not ZVSE2) script "(name)" is always a function name
        string r = Pp("ZVSE\n!?FU(abc);\n");
        Assert.Equal("ZVSE\n!?FU95000;\n", r);
    }

    [Fact]
    public void Local_variables_arrays_and_addresses()
    {
        string r = Pp("ZVSE2\n!?FU(Fn);\n!!VR(a:y):S5;\n!!VR(b[3]:y):S0;\n!!VR(b[1]):S(a);\n!!VR(b[-1]):S(@a);\n");
        Assert.Equal("ZVSE2\n!?FU95000;\n!!VRy1:S5;\n!!VRy2:S0;\n!!VRy3:Sy1;\n!!VRy4:S1;\n", r);
    }

    [Fact]
    public void Local_variables_are_per_trigger_in_zvse2()
    {
        string r = Pp("ZVSE2\n!?FU(Fn);\n!!VR(a:y):S1;\n!?FU(Gn);\n!!VR(b:y):S2;\n");
        Assert.Equal("ZVSE2\n!?FU95000;\n!!VRy1:S1;\n!?FU95001;\n!!VRy1:S2;\n", r);
    }

    [Fact]
    public void Z_locals_are_allocated_from_negative_pool()
    {
        string r = Pp("ZVSE2\n!?FU(Fn);\n!!VR(s:z):S^x^;\n!!VR(t[2]:z):S^y^;\n");
        Assert.Equal("ZVSE2\n!?FU95000;\n!!VRz-1:S^x^;\n!!VRz-3:S^y^;\n", r);
    }

    [Fact]
    public void Array_index_by_variable_inserts_helper_command()
    {
        string r = Pp("ZVSE2\n!?FU(Fn);\n!!VR(idx:y):S1;\n!!VR(arr[3]:y):S0;\n!!VR(arr[idx]):S7;\n!!VR(arr[i]):S8;\n");
        // a one-letter f..t index is always the quick variable, even if a local of that name exists
        Assert.Equal("ZVSE2\n!?FU95000;\n!!VRy1:S1;\n!!VRy2:S0;\n!!VRy5:S2 +y1 F2/4/0/0; !!VRyy5:S7;\n!!VRy5:S2 +i F2/4/0/0; !!VRyy5:S8;\n", r);
    }

    [Fact]
    public void Constants_are_declared_and_substituted()
    {
        var names = new EraNames();
        names.ResetFunctions();
        string r = Pp("ZVSE2\n!#DC(MAX_LEVEL) = 74;\n!#DC(ALIAS) = (MAX_LEVEL);\n!?FU(Fn);\n!!VRy1:S(ALIAS) +(TRUE);\n", names);
        Assert.Equal("ZVSE2\n;\n;\n!?FU95000;\n!!VRy1:S74 +1;\n", r);
        Assert.Equal(74, names.Constants["ALIAS"]);
    }

    [Fact]
    public void Labels_become_command_indexes()
    {
        string r = Pp("ZVSE2\n!?FU(Fn);\n!!VRy1:S0;\n[:again]\n!!VRy1:+1;\n!!SN:G[again];\n!!SN:G[later];\n[:later]\n!!VRy2:S1;\n");
        Assert.Equal("ZVSE2\n!?FU95000;\n!!VRy1:S0;\n\n!!VRy1:+1;\n!!SN:G1;\n!!SN:G4;\n\n!!VRy2:S1;\n", r);
    }

    [Fact]
    public void Strings_interpolate_locals_and_keep_era_codes()
    {
        string r = Pp("ZVSE2\n!?FU(Fn);\n!!VR(n:y):S3;\n!!IF:M^n=%(n) %y(n) %s(name) %T(key)^;\n");
        Assert.Equal("ZVSE2\n!?FU95000;\n!!VRy1:S3;\n!!IF:M^n=%y1 %yy1 %s(name) %T(key)^;\n", r);
    }

    [Fact]
    public void Triple_bang_disables_a_command_and_va_is_removed()
    {
        string r = Pp("ZVSE2\n!?FU(Fn);\n!!!VRy1:S1;\n!#VA(x:y);\n!!VR(x):S2;\n");
        Assert.Equal("ZVSE2\n!?FU95000;\n!VRy1:S1;\n\n!!VRy1:S2;\n", r);
    }
}

public class EraParserTests
{
    [Fact]
    public void Era_parameters_types_and_modifiers()
    {
        var s = ErmParser.ParseText("t", "ZVSE\n!?FU95000;\n!!VRy1:S i^cnt^ d* 3;\n!!SN:W^key^/d&^tail^;\n!!VRvy5:Sc2;\n", ErmDialect.Era);
        Assert.DoesNotContain(s.Diagnostics, d => d.Severity == ErmSeverity.Error);
        var lines = s.Sections.Single().Lines;
        var vr = lines[0].Commands[0];
        Assert.Equal(WoG.Core.State.ErmVarKind.AssocI, vr.Params[0].Var!.Kind);
        Assert.Equal("cnt", vr.Params[0].Var!.Name);
        var sn = lines[1].Commands[0];
        Assert.Equal(ErmModifier.Concat, sn.Params[1].Modifier);
        var v = lines[2].Selector[0].Var!;
        Assert.Equal(WoG.Core.State.ErmVarKind.V, v.Kind);
        Assert.Equal(WoG.Core.State.ErmVarKind.Y, v.IndexKind);
        Assert.True(lines[2].Commands[0].Params[0].DayRelative);
    }

    [Fact]
    public void Conditions_cast_single_values_and_flags()
    {
        var s = ErmParser.ParseText("t", "ZVSE\n!?FU95000;\n!!if&y1/-5|z1;\n!!en;\n", ErmDialect.Era);
        var c = s.Sections.Single().Lines[0].Condition;
        Assert.Null(c.And[0].Right);      // &y1 → y1 <> 0
        Assert.Equal(5, c.And[1].Flag);   // &-5 → flag 5 clear
        Assert.False(c.And[1].FlagSet);
        Assert.Null(c.Or[0].Right);       // |z1 → z1 <> ""
    }
}

public class EraRuntimeTests
{
    const string H = "ZVSE2\n";

    [Fact]
    public void Re_loops_with_break_and_continue()
    {
        using var t = new EraTestHost().Script("t.erm", H + @"
!?FU(Sum);
!!VRv1:S0;
!!re i/1/10;
  !!co&i=3;
  !!br&i=8;
  !!VRv1:+i;
!!en;
").Start();
        t.Call("Sum");
        Assert.Equal(1 + 2 + 4 + 5 + 6 + 7, t.V(1));
        Assert.Equal(0, t.Errors);
    }

    [Fact]
    public void El_with_condition_works_as_else_if()
    {
        using var t = new EraTestHost().Script("t.erm", H + @"
!?FU(Pick);
!!if&x1=1;
  !!VRv2:S10;
!!el&x1=2;
  !!VRv2:S20;
!!el;
  !!VRv2:S30;
!!en;
").Start();
        t.Call("Pick", 2);
        Assert.Equal(20, t.V(2));
        t.Call("Pick", 5);
        Assert.Equal(30, t.V(2));
    }

    [Fact]
    public void Y_vars_are_shared_by_all_sections_of_one_event_and_reset_for_the_next()
    {
        using var t = new EraTestHost().Script("t.erm", H + @"
!?FU(Ev);
!!VRy1:+1;
!?FU(Ev);
!!VRy1:+1;
!!VRv3:Sy1;
").Start();
        t.Call("Ev");
        Assert.Equal(2, t.V(3));
        t.Call("Ev");
        Assert.Equal(2, t.V(3));
    }

    [Fact]
    public void Quick_vars_are_restored_after_a_trigger()
    {
        using var t = new EraTestHost().Script("t.erm", H + @"
!?FU(Outer);
!!VRi:S5;
!!FU(Inner):P;
!!VRv4:Si;
!?FU(Inner);
!!VRi:S99;
").Start();
        t.Call("Outer");
        Assert.Equal(5, t.V(4));
    }

    [Fact]
    public void Functions_return_strings_and_use_default_arguments()
    {
        using var t = new EraTestHost().Script("t.erm", H + @"
!?FU(Caller);
!!FU(Greet):P?z5/7;
!!FU(Defaults):P1;
!?FU(Greet);
!!VRx1:Z^hello %x2^;
!?FU(Defaults);
!!FU:A5/6;
!!FU:A?v6;
!!VRv7:Sx1;
!!VRv8:Sx2;
").Start();
        t.Call("Caller");
        Assert.Equal("hello 7", t.Z(5));
        Assert.Equal(1, t.V(6));
        Assert.Equal(1, t.V(7));
        Assert.Equal(6, t.V(8));
        Assert.Equal(0, t.Errors);
    }

    [Fact]
    public void Get_parameters_pass_numbers_by_reference()
    {
        using var t = new EraTestHost().Script("t.erm", H + @"
!?FU(Caller);
!!VRv9:S4;
!!FU(Double):P?v9;
!?FU(Double);
!!VRx1:*2;
").Start();
        t.Call("Caller");
        Assert.Equal(8, t.V(9));
    }

    [Fact]
    public void Associative_variables_arrays_and_interpolation()
    {
        using var t = new EraTestHost().Script("t.erm", H + @"
!?FU(Go);
!!VRi^count^:S5 +1;
!!SN:W^count^/?v10;
!!VRz1:S^c=%i(count)^;
!!SN:M-1/3/0/1;
!!VRv11:Sv1;
!!SN:Mv11/0/10 Mv11/1/20 Mv11/2/d5;
!!SN:Mv11/2/?v12 Mv11/?v13;
!!SN:Vv11/0/?v14/?v15;
!!VRs^name^:S^Era^;
!!VRz2:S^Hi, %s(name)!^;
").Start();
        t.Call("Go");
        Assert.Equal(6, t.V(10));
        Assert.Equal("c=6", t.Z(1));
        Assert.Equal(5, t.V(12));
        Assert.Equal(3, t.V(13));
        Assert.Equal(10, t.V(14));
        Assert.Equal(20, t.V(15));
        Assert.Equal("Hi, Era!", t.Z(2));
        Assert.Equal(0, t.Errors);
    }

    [Fact]
    public void Trigger_local_arrays_are_freed_unless_lifetime_is_extended()
    {
        using var t = new EraTestHost().Script("t.erm", H + @"
!?FU(Make);
!!SN:M-1/2/0/-1/?x1;
!?FU(MakeKeep);
!!SN:M-1/2/0/-1/?x1;
!!SN:F^ExtendArrayLifetime^/x1;
!?FU(Caller);
!!FU(Make):P?v20;
!!SN:Mv20/?v21;
!!FU(MakeKeep):P?v22;
!!SN:Mv22/?v23;
").Start();
        t.Call("Caller");
        Assert.Equal(-1, t.V(21));  // no such array any more
        Assert.Equal(2, t.V(23));   // handed over to Caller
    }

    [Fact]
    public void Vr_strings_floats_and_contains()
    {
        using var t = new EraTestHost().Script("t.erm", H + @"
!?FU(Go);
!!VRz1:S^Hello^ +^ World^;
!!VRz2:S^  wor  ^;
!!VRz1:Uz2;
!!VRe1:S5 :2;
!!VRv30:Se1/0;
!!VRv31:Se1;
!!VRv32:Se1/-1;
!!VRz3:M3/255/16;
!!VRz4:S^a,b c^;
!!VRz5:M2/z4/2;
").Start();
        t.Call("Go");
        Assert.Equal("Hello World", t.Z(1));
        Assert.True(t.F(1));           // Era VR:U = case-insensitive "contains" of the trimmed text
        Assert.Equal(3, t.V(30));      // 2.5 rounded half away from zero
        Assert.Equal(2, t.V(31));      // truncated
        Assert.Equal(2, t.V(32));      // floor
        Assert.Equal("ff", t.Z(3));
        Assert.Equal("c", t.Z(5));
    }

    [Fact]
    public void D_modifiers_and_string_conditions()
    {
        using var t = new EraTestHost().Script("t.erm", H + @"
!?FU(Go);
!!VRv40:S12;
!!OW:R0/6/d-2;
!!VRv41:S5 *3 :2 %4;
!!VRz1:S^abc^;
!!if&z1=^abc^;
  !!VRv42:S1;
!!en;
!!if&z1;
  !!VRv43:S1;
!!en;
").Start();
        t.Call("Go");
        Assert.Equal(7 % 4, t.V(41));
        Assert.Equal(1, t.V(42));
        Assert.Equal(1, t.V(43));
    }

    [Fact]
    public void Sn_g_jumps_and_sn_q_stops_remaining_sections()
    {
        using var t = new EraTestHost().Script("t.erm", H + @"
!?FU(Loop);
!!VRv50:S0;
[:again]
!!VRv50:+1;
!!SN&v50<5:G[again];
!?FU(Stop);
!!VRv51:S1;
!!SN:Q;
!!VRv51:S2;
!?FU(Stop);
!!VRv51:S3;
").Start();
        t.Call("Loop");
        Assert.Equal(5, t.V(50));
        t.Call("Stop");
        Assert.Equal(1, t.V(51));
    }

    [Fact]
    public void Quit_trigger_runs_after_the_event()
    {
        using var t = new EraTestHost().Script("t.erm", H + @"
!?FU(Ev);
!!VRv60:S1;
!?FU(Ev_Quit);
!!VRv60:*10;
").Start();
        t.Call("Ev");
        Assert.Equal(10, t.V(60));
    }

    [Fact]
    public void Do_loop_repeats_the_function_with_x16()
    {
        using var t = new EraTestHost().Script("t.erm", H + @"
!?FU(Caller);
!!VRv70:S0;
!!DO(Body)/1/5/1:P;
!?FU(Body);
!!VRv70:+x16;
").Start();
        t.Call("Caller");
        Assert.Equal(15, t.V(70));
    }

    [Fact]
    public void Translations_and_sn_t()
    {
        using var t = new EraTestHost()
            .Lang("{\"my\": {\"hello\": \"Hello, @name@!\", \"plain\": \"Plain\"}}")
            .Script("t.erm", H + @"
!?FU(Go);
!!VRz1:S^%T(my.plain)^;
!!SN:T^my.hello^/?z2/^name^/^Era^;
!!SN:T^missing.key^/?z3;
").Start();
        t.Call("Go");
        Assert.Equal("Plain", t.Z(1));
        Assert.Equal("Hello, Era!", t.Z(2));
        Assert.Equal("missing.key", t.Z(3));
    }

    [Fact]
    public void Every_day_event_fires_before_timers()
    {
        using var t = new EraTestHost().Script("t.erm", H + @"
!?FU(OnEveryDay);
!!VRv80:+1;
!!VRv81:Sv82;
!?TM1;
!!VRv82:+1;
!#TM1:S1/999/1/255;
").Start();
        t.Erm.RunTimers(0, 1);
        Assert.Equal(1, t.V(80));
        Assert.Equal(0, t.V(81)); // OnEveryDay ran before TM1
        Assert.Equal(1, t.V(82));
    }

    [Fact]
    public void Legacy_support_resets_negative_y_per_section_of_classic_triggers()
    {
        using var t = new EraTestHost(legacy: true).Script("t.erm", "ZVSE\n" + @"
!?TM2;
!!VRy-1:+1;
!?TM2;
!!VRy-1:+1;
!!VRv90:Sy-1;
!#TM2:S1/999/1/255;
").Start();
        t.Erm.RunTimers(0, 1);
        Assert.Equal(1, t.V(90));
    }

    [Fact]
    public void State_survives_save_and_load()
    {
        string dir = Path.Combine(Path.GetTempPath(), "era-save-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string script = H + "!?FU(Setup);\n!!VRi^keep^:S42;\n!!SN:M5/2/0/1;\n!!SN:M5/1/77;\n!?FU(Read);\n!!SN:M5/1/?v1;\n!!VRv2:Si^keep^;\n";
            using var t = new EraTestHost().Script("t.erm", script).Start();
            t.Call("Setup");
            int setupId = t.Func("Setup");
            string file = Path.Combine(dir, "s.wog.json");
            t.Host.SaveTo(file, "slot");

            using var t2 = new EraTestHost().Script("t.erm", script);
            t2.Host.LoadFrom(file, "slot");
            Assert.Equal(setupId, t2.Func("Setup"));
            t2.Call("Read");
            Assert.Equal(77, t2.V(1));
            Assert.Equal(42, t2.V(2));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}

/// <summary>Runs the real ERA project scripts (set ERA_MODS_DIR to the era-project Mods folder).</summary>
public class EraCorpusTests
{
    [Fact]
    public void Era_project_scripts_load_as_a_new_game_without_errors()
    {
        string? mods = Environment.GetEnvironmentVariable("ERA_MODS_DIR");
        if (string.IsNullOrEmpty(mods) || !Directory.Exists(mods)) return;
        var game = new HeadlessGame();
        var host = new WoGHost(game, new VisualResolver(new NoAssets()), null, new ErmRuntimeOptions { Dialect = ErmDialect.Era });
        host.AddEraMods(new[] { "Era Erm Framework", "ERA Scripts", "WoG Scripts", "WoG" }.Select(m => Path.Combine(mods, m)));
        host.StartNewGame();
        for (int day = 1; day <= 7; day++)
        {
            game.AbsoluteDay = day;
            host.Erm!.RunTimers(0, day);
        }
        var errors = host.Erm!.Diagnostics.Where(d => d.Severity == ErmSeverity.Error).ToList();
        Assert.True(errors.Count == 0, string.Join("\n", errors.Take(20)));
        Assert.True(host.EraScripts.Count > 150);
    }
}
