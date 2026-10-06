using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using WoG.Core.Ids;
using WoG.Core.State;
using WoG.OldenEra.Data;
using Xunit;

namespace WoG.Tests;

public class OverlayTests
{
    /// <summary>A synthetic Core.zip with one unit in the layout O1 documents (no game data involved).</summary>
    static MemoryStream FakeCore()
    {
        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var e = zip.CreateEntry("DB/units/units_logics/temple/squire.json");
            using var w = new StreamWriter(e.Open(), new UTF8Encoding(true));
            w.Write("[{\"id\":\"squire\",\"fraction\":\"temple\",\"tier\":1,\"stats\":{\"hp\":10,\"offence\":4,\"defence\":5,\"damageMin\":1,\"damageMax\":3,\"initiative\":8,\"speed\":4,\"numCounters\":1}}]");
        }
        ms.Position = 0;
        return ms;
    }

    [Fact]
    public void Clone_unit_keeps_source_folder_and_overrides_stats()
    {
        using var core = new CoreZipReader(FakeCore());
        var ov = new OverlayBuilder();
        var c = ov.CloneUnit(core, "squire", "wog_commander_castle", new System.Collections.Generic.Dictionary<string, int> { ["hp"] = 40, ["offence"] = 5 }, "Paladin");
        Assert.Equal("wog_commander_castle", (string?)c["id"]);
        Assert.Equal(40, (int?)c["stats"]!["hp"]);
        Assert.Equal(5, (int?)c["stats"]!["defence"]);       // untouched stats come from the source
        Assert.StartsWith("DB/units/units_logics/temple/", ov.Entries.Single().Path);
    }

    [Fact]
    public void Stack_experience_buffs_are_deltas_from_the_oe_base()
    {
        using var core = new CoreZipReader(FakeCore());
        var ov = new OverlayBuilder();
        var sx = new StackExperienceState();
        var lv = new int[11];
        for (int i = 0; i < 11; i++) lv[i] = i;
        sx.Bonuses[0] = new() { new CreatureExpBonus { Type = 'A', Mod = '+', Levels = lv }, new CreatureExpBonus { Type = 'H', Mod = '%', Levels = (int[])lv.Clone() }, new CreatureExpBonus { Type = 'f', Mod = 'F', Levels = new int[11] } };
        var ids = new IdMap();
        ids.Set("creature", 0, "squire");
        var r = StackExperienceBuffs.Generate(core, ov, sx, ids);
        var r5 = r.Buffs.Single(b => (string?)b["id"] == "wog_sx_0_r5");
        Assert.Equal(5, (double)r5["data"]!["stats"]!["offence"]!);
        // H 10 % at rank 10 of hp 10 → 10 + 1 + 0.5 = 11.5 → 11 → delta 1
        var r10 = r.Buffs.Single(b => (string?)b["id"] == "wog_sx_0_r10");
        Assert.Equal(1, (double)r10["data"]!["stats"]!["hp"]!);
        Assert.Contains(r.NotExpressible, m => m.Contains("'fF'"));
    }

    [Fact]
    public void Overlay_zip_uses_store_and_token_localisation()
    {
        using var core = new CoreZipReader(FakeCore());
        var ov = new OverlayBuilder();
        ov.StatBuff("wog_test", new System.Collections.Generic.Dictionary<string, double> { ["offence"] = 2 }, "Test buff");
        string path = Path.Combine(Path.GetTempPath(), "wog-ov-" + System.Guid.NewGuid().ToString("N") + ".zip");
        try
        {
            ov.Write(path);
            using var z = ZipFile.OpenRead(path);
            var loc = z.GetEntry("Lang/english/texts/wog.json")!;
            Assert.Equal(loc.Length, loc.CompressedLength);   // stored
            using var s = loc.Open();
            var bytes = new MemoryStream();
            s.CopyTo(bytes);
            var b = bytes.ToArray();
            Assert.True(b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF);  // BOM
            var json = JsonNode.Parse(Encoding.UTF8.GetString(b, 3, b.Length - 3))!;
            Assert.Equal("wog_test_name", (string?)json["tokens"]![0]!["sid"]);
        }
        finally { File.Delete(path); }
    }
}
