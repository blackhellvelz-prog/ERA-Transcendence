using System.IO;
using System.Linq;
using WoG.Core.Model;
using WoG.Core.Save;
using WoG.Core.State;
using WoG.Core.Visual;
using Xunit;

namespace WoG.Tests;

public class SaveAndOptionsTests
{
    [Fact]
    public void Round_trip_preserves_all_wog_state()
    {
        var st = new WoGGameState();
        st.Options.Set(900, 1);
        st.Options.Set(250, 7);
        st.Erm.V[9999] = 42;
        st.Erm.Flags[999] = true;
        st.Erm.Z[0] = "привет, мир";
        st.Erm.WFor(12)[199] = 5;
        st.Erm.Macros["gold"] = new ErmMacro { Name = "gold", Kind = ErmVarKind.V, Index = 20 };
        st.Erm.Timers[3].Period = 7;
        st.Commanders[4] = new WoGCommander { Number = 4, Level = 12, Exp = 28784, Name = "Elmore" };
        st.Commanders[4].Arts[2][0] = 150;
        st.StackExperience.Records.Add(new StackExperienceRecord { Location = StackLocation.Hero(1, 3), Expo = 900, MType = 13, Num = 7 });
        st.Squares[new MapPos(5, 6, 1).Pack()] = new[] { 1, 2, 3, 4 };
        st.Ids.Set("creature", 13, "griffin_upg");

        string blob = WoGSaveSerializer.Serialize(st, "save-1");
        var back = WoGSaveSerializer.Deserialize(blob, "save-1");

        Assert.Equal(1, back.Options.Get(900));
        Assert.Equal(7, back.Options.Get(250));
        Assert.Equal(42, back.Erm.V[9999]);
        Assert.True(back.Erm.Flags[999]);
        Assert.Equal("привет, мир", back.Erm.Z[0]);
        Assert.Equal(5, back.Erm.WFor(12)[199]);
        Assert.Equal(20, back.Erm.Macros["gold"].Index);
        Assert.Equal(7, back.Erm.Timers[3].Period);
        Assert.Equal(28784, back.Commanders[4].Exp);
        Assert.Equal(150, back.Commanders[4].Arts[2][0]);
        Assert.Equal(StackLocation.Hero(1, 3), back.StackExperience.Records[0].Location);
        Assert.Equal(new[] { 1, 2, 3, 4 }, back.Squares[new MapPos(5, 6, 1).Pack()]);
        Assert.True(back.Ids.TryGetWoG("creature", "griffin_upg", out int id) && id == 13);
    }

    [Fact]
    public void Tampered_or_foreign_saves_are_rejected()
    {
        string blob = WoGSaveSerializer.Serialize(new WoGGameState(), "a");
        Assert.Throws<WoGSaveException>(() => WoGSaveSerializer.Deserialize(blob, "b"));
        string tampered = blob.Replace("\\u0022Values\\u0022", "\\u0022Valuez\\u0022");
        if (tampered == blob) tampered = blob.Replace("Values", "Valuez");
        Assert.Throws<WoGSaveException>(() => WoGSaveSerializer.Deserialize(tampered));
    }

    [Fact]
    public void Host_save_load_keeps_erm_state_and_fires_GM_triggers()
    {
        string dir = Path.Combine(Path.GetTempPath(), "wog-save-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            const string script = "!#VRv1:S1;\n!?GM1;\n!!VRv2:+1;\n!?GM0;\n!!VRv3:+1;\n";
            var t = new TestHost().Load(script).Start();
            t.SetV(10, 123);
            t.Host.Commanders!.Get(2).Level = 9;
            string file = Path.Combine(dir, "slot1.wog.json");
            t.Host.SaveTo(file, "slot1");
            Assert.Equal(1, t.V(2));                  // GM1 ran before saving

            var t2 = new TestHost();
            t2.Host.AddScript("test.erm", "ZVSE\n" + script);
            t2.Host.LoadFrom(file, "slot1");
            Assert.Equal(123, t2.V(10));
            Assert.Equal(1, t2.V(1));                 // instruction value came from the save, not re-run
            Assert.Equal(1, t2.V(3));                 // GM0 ran after loading
            Assert.Equal(9, t2.Host.Commanders!.Get(2).Level);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Options_are_independent_and_inverted_1_to_4()
    {
        var o = new WoG.Core.Options.WoGOptions();
        o.SetChecked(3, true);                        // "commanders" checked → stored 0 (PL_NoNPC)
        Assert.Equal(0, o.Get(3));
        Assert.True(o.CommandersEnabled);
        o.SetChecked(900, true);
        Assert.Equal(1, o.Get(900));
        o.Set(901, 2);
        Assert.Equal(2, o.Get(901));
        Assert.Equal(0, o.Get(902));
    }

    [Fact]
    public void ERM_UN_P_reads_writes_options_and_drives_commanders()
    {
        var t = new TestHost().Load("!?FU1;\n!!UN:P901/2;\n!!UN:P901/?v1;\n!!UN:P3/1;\n").Start();
        t.Call(1);
        Assert.Equal(2, t.V(1));
        Assert.Equal(-1, t.Host.Commanders!.Get(0).Used);
    }

    [Fact]
    public void Visual_resolver_prefers_engine_assets_and_never_fails()
    {
        var probe = new SetProbe("oe:griffin");
        var r = new VisualResolver(probe);
        r.Register("creature:wog.commander.castle", new VisualCandidate { Source = VisualSource.ImportedWoGAsset, Asset = "h3:cmdr0" });
        r.Register("creature:wog.commander.castle", new VisualCandidate { Source = VisualSource.OldenEraRecolor, Asset = "oe:griffin" });
        Assert.Equal("oe:griffin", r.Resolve(new VisualRef("creature:wog.commander.castle")).Candidate.Asset);
        var missing = r.Resolve(new VisualRef("icon:artifact:146"));
        Assert.True(missing.IsPlaceholder);
    }

    sealed class SetProbe : IVisualAssetProbe
    {
        readonly string available;
        public SetProbe(string a) { available = a; }
        public bool IsAvailable(VisualCandidate c) => c.Asset == available;
    }
}

public class RepositoryDataTests
{
    static string Repo([System.Runtime.CompilerServices.CallerFilePath] string here = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", ".."));

    [Fact]
    public void Options_catalogue_loads_and_keeps_options_separate()
    {
        var defs = WoG.Core.Options.WoGOptionCatalog.LoadJson(Path.Combine(Repo(), "Compatibility", "options-defaults.json"));
        Assert.Contains(defs, d => d.Index == 900);
        Assert.Equal(defs.Count, defs.Select(d => d.Index).Distinct().Count());
        Assert.All(defs, d => Assert.False(d.DefaultVerified)); // nothing here is claimed as verified
        var o = new WoG.Core.Options.WoGOptions();
        o.ApplyDefaults(defs);
        Assert.Equal(1, o.Get(900));
    }

    [Fact]
    public void Creature_id_map_follows_the_documented_rules()
    {
        // Rules: tools/WoG.ErmTool/CreatureMap.cs and Compatibility/id-maps/README.md.
        var map = new WoG.Core.Ids.IdMap();
        map.LoadDomainFile("creature", Path.Combine(Repo(), "Compatibility", "id-maps", "creature.json"));
        Assert.True(map.TryGetEngine("creature", 0, out var pikeman));     // castle level 1 base → Temple tier 1 base
        Assert.Equal("esquire", pikeman);
        Assert.True(map.TryGetEngine("creature", 13, out var archangel));  // castle level 7 upgrade → "_upg"
        Assert.Equal("angel_upg", archangel);
        Assert.False(map.TryGetEngine("creature", 28, out _));             // tower: no Olden Era faction
        Assert.True(map.TryGetWoG("creature", "esquire_upg_alt", out int alt));
        Assert.True(alt >= 1000);                                          // Olden Era-only units: 1000 and up
        var engines = map.Forward["creature"].Values.ToList();
        Assert.Equal(engines.Count, engines.Distinct().Count());           // one number per unit
    }
    [Fact]
    public void Loading_a_save_keeps_the_id_tables_of_the_files_and_the_numbers_given_in_the_game()
    {
        var dir = Path.Combine(Path.GetTempPath(), "wog-idmap-" + Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        try
        {
            var t = new TestHost().Load("!?FU1;\n").Start();
            t.Host.State.Ids.Set("heroslot", 3, "id:7");        // given during the game
            t.Host.State.Ids.Set("spell", 17, "old_sid");       // a table the save carries
            var save = Path.Combine(dir, "s.wogsave");
            t.Host.SaveTo(save, "slot");
            var file = Path.Combine(dir, "spell.json");          // a newer id-maps file present at start
            File.WriteAllText(file, "[{\"wog\": 17, \"engine\": \"primal_1_magic_thunderbolt\"}]");
            t.Host.State.Ids.LoadDomainFile("spell", file);
            t.Host.State.Ids.Set("heroslot", 3, "id:9");         // the session moved on
            t.Host.LoadFrom(save, "slot");
            Assert.True(t.Host.State.Ids.TryGetEngine("spell", 17, out var sid));
            Assert.Equal("primal_1_magic_thunderbolt", sid);
            Assert.True(t.Host.State.Ids.TryGetWoG("spell", "primal_1_magic_thunderbolt", out int n));
            Assert.Equal(17, n);
            Assert.True(t.Host.State.Ids.TryGetEngine("heroslot", 3, out var hero));
            Assert.Equal("id:7", hero);
        }
        finally { Directory.Delete(dir, true); }
    }
}
