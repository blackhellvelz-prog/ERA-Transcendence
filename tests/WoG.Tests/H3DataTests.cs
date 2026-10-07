using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using WoG.Core.H3Data;
using Xunit;

namespace WoG.Tests;

/// <summary>The H3 data layer (LOD/PAC archives, Era's VFS order, text tables) and UN:A/UN:V on top of it.</summary>
public class H3DataTests : IDisposable
{
    readonly string root = Path.Combine(Path.GetTempPath(), "h3data-" + Guid.NewGuid().ToString("N"));

    public H3DataTests() => Directory.CreateDirectory(root);

    public void Dispose()
    {
        try { Directory.Delete(root, true); } catch (IOException) { }
    }

    /// <summary>Writes a LOD archive: "LOD\0", count at 8, 32-byte entries from 0x5C; compressed entries are zlib.</summary>
    static void WriteLod(string path, params (string Name, byte[] Data, bool Compress)[] files)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var blobs = files.Select(f =>
        {
            if (!f.Compress) return f.Data;
            using var o = new MemoryStream();
            using (var z = new ZLibStream(o, CompressionLevel.Optimal, true)) z.Write(f.Data);
            return o.ToArray();
        }).ToList();
        using var w = new BinaryWriter(File.Create(path));
        w.Write(Encoding.ASCII.GetBytes("LOD\0"));
        w.Write(200);
        w.Write(files.Length);
        w.Write(new byte[0x5C - 12]);
        uint offset = (uint)(0x5C + 32 * files.Length);
        for (int i = 0; i < files.Length; i++)
        {
            var name = new byte[16];
            Encoding.ASCII.GetBytes(files[i].Name).CopyTo(name, 0);
            w.Write(name);
            w.Write(offset);
            w.Write((uint)files[i].Data.Length);
            w.Write(2u);
            w.Write(files[i].Compress ? (uint)blobs[i].Length : 0u);
            offset += (uint)blobs[i].Length;
        }
        foreach (var b in blobs) w.Write(b);
    }

    static readonly string[] Classes = { "S", "T", "N", "J", "R" };

    /// <summary>A WoG-sized artraits.txt (171 rows) with known values: cost = 10·id, slot column id % 19, class id % 5.</summary>
    static string Artraits(Func<int, string>? name = null)
    {
        var sb = new StringBuilder();
        sb.Append("\t\tHero Slots\r\n");
        sb.Append("Name\tCost\tSpell Book\tWar Machine 4\tWar Machine 3\tWar Machine 2\tWar Machine 1\tMisc 5\tMisc 4\tMisc 3\tMisc 2\tMisc 1\tFeet\tLeft Ring\tRight Ring\tTorso\tLeft Hand\tRight Hand\tNeck\tShoulders\tHead\tClass\tDescription\r\n");
        for (int id = 0; id < 171; id++)
        {
            var cells = Enumerable.Repeat(" ", 19).ToArray();
            cells[id % 19] = "x";
            string desc = id == 7 ? "\"{Axe}\r\nsecond line\"" : $"Description {id}";
            sb.Append($"{(name ?? (i => $"Art {i}"))(id)}\t{id * 10}\t{string.Join("\t", cells)}\t{Classes[id % 5]}\t{desc}\r\n");
        }
        return sb.ToString();
    }

    [Fact]
    public void Lod_archive_reads_stored_and_zlib_entries_case_insensitively()
    {
        string lod = Path.Combine(root, "test.lod");
        WriteLod(lod, ("plain.txt", Encoding.ASCII.GetBytes("plain"), false), ("PACKED.TXT", Encoding.ASCII.GetBytes("packed text"), true));
        var a = LodArchive.Open(lod)!;
        Assert.Equal("plain", Encoding.ASCII.GetString(a.Read("PLAIN.txt")!));
        Assert.Equal("packed text", Encoding.ASCII.GetString(a.Read("packed.txt")!));
        Assert.Null(a.Read("missing.txt"));
        File.WriteAllText(Path.Combine(root, "not.lod"), "garbage");
        Assert.Null(LodArchive.Open(Path.Combine(root, "not.lod")));
    }

    [Fact]
    public void Vfs_prefers_higher_mods_then_loose_files_then_the_base_game()
    {
        string game = Path.Combine(root, "game"), high = Path.Combine(root, "high"), low = Path.Combine(root, "low");
        WriteLod(Path.Combine(game, "Data", "h3ab_bmp.lod"), ("a.txt", Encoding.ASCII.GetBytes("ab"), false), ("b.txt", Encoding.ASCII.GetBytes("ab"), false));
        WriteLod(Path.Combine(game, "Data", "H3bitmap.lod"), ("a.txt", Encoding.ASCII.GetBytes("sod"), true));
        WriteLod(Path.Combine(low, "Data", "low.pac"), ("c.txt", Encoding.ASCII.GetBytes("low pac"), true), ("d.txt", Encoding.ASCII.GetBytes("low pac"), false));
        File.WriteAllText(Path.Combine(low, "Data", "d.txt"), "low loose");
        WriteLod(Path.Combine(high, "Data", "high.pac"), ("c.txt", Encoding.ASCII.GetBytes("high pac"), false));

        var vfs = new EraVfs(game, new[] { high, low });
        string Text(string n) => Encoding.ASCII.GetString(vfs.Read(n)!);
        Assert.Equal("sod", Text("a.txt"));        // h3bitmap before the Armageddon's Blade archive
        Assert.Equal("ab", Text("b.txt"));
        Assert.Equal("high pac", Text("c.txt"));   // the higher mod wins
        Assert.Equal("low loose", Text("d.txt"));  // a loose file wins over its own mod's archives
        Assert.Null(vfs.Read("e.txt"));
        Assert.True(new EraVfs(null, Array.Empty<string>()).IsEmpty);
    }

    [Fact]
    public void Artraits_rows_give_cost_slot_class_quoted_descriptions_and_sod_combos()
    {
        var arts = H3Tables.ParseArtifacts(Artraits());
        Assert.Equal(171, arts.Count);
        Assert.Equal("Art 5", arts[5].Name);
        Assert.Equal(50, arts[5].Cost);
        Assert.Equal(14, arts[0].Position);   // first slot column = spell book
        Assert.Equal(1, arts[18].Position);   // last slot column = head
        Assert.Equal(8, arts[10].Position);   // feet
        Assert.Equal(7, arts[11].Position);   // left ring = ring
        Assert.Equal(9, arts[5].Position);    // misc 5 = misc
        Assert.Equal(1, arts[0].Type);        // S
        Assert.Equal(16, arts[4].Type);       // R
        Assert.Equal("{Axe}\r\nsecond line", arts[7].Description);
        Assert.Equal(0, arts[129].SuperN);    // Angelic Alliance
        Assert.Equal(0, arts[31].PartOfSuperN);
        Assert.Equal(11, arts[140].SuperN);   // Cornucopia
        Assert.Equal(11, arts[109].PartOfSuperN);
        Assert.Equal(-1, arts[7].SuperN);
        Assert.Equal(-1, arts[7].PartOfSuperN);
    }

    [Fact]
    public void Text_tables_in_cp1251_are_decoded()
    {
        var bytes = Encoding.GetEncoding(1251).GetBytes("Книга\t0\r\n");
        Assert.Equal("Книга", H3Text.Records(H3Text.Decode(bytes))[0][0]);
        Assert.Equal("Book", H3Text.Records(H3Text.Decode(Encoding.UTF8.GetBytes("Book\t0\r\n")))[0][0]);
    }

    static H3DataTests() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    EraTestHost EraWithArtifacts(string script)
    {
        var t = new EraTestHost().Script("t.erm", "ZVSE2\n" + script);
        File.WriteAllText(Path.Combine(t.ModDir, "Data", "artraits.txt"), Artraits());
        t.Start();
        t.Host.SetEraFolders(null, Path.Combine(root, "write"));
        return t;
    }

    [Fact]
    public void Un_a_reads_and_changes_artifact_fields_and_texts()
    {
        using var t = EraWithArtifacts(
            "!?FU(Go);\n" +
            "!!UN:A5/1/?v1;\n" +             // cost
            "!!UN:A5/1/d7;\n" +
            "!!UN:A5/1/?v2;\n" +
            "!!UN:A18/2/?v3;\n" +            // position (head)
            "!!UN:A4/3/?v4;\n" +             // type (relic)
            "!!UN:A129/4/?v5;\n" +           // combo number
            "!!UN:A31/5/?v6;\n" +            // part of combo
            "!!UN:A5/8/1;\n!!UN:A5/8/?v7;\n" +
            "!!UN:A5/9/?z1;\n" +
            "!!UN:A5/9/^Renamed^;\n" +
            "!!UN:A5/9/?z2;\n" +
            "!!VRz3:S^From z3^;\n!!UN:A6/10/3;\n!!UN:A6/10/?z4;\n" +
            "!!UN:A5/9/0;\n!!UN:A5/9/?z5;\n");
        t.Call("Go");
        Assert.Equal("", t.ErrorText);
        Assert.Equal(50, t.V(1));
        Assert.Equal(57, t.V(2));
        Assert.Equal(1, t.V(3));              // head in Format P2
        Assert.Equal(16, t.V(4));
        Assert.Equal(0, t.V(5));
        Assert.Equal(0, t.V(6));
        Assert.Equal(1, t.V(7));
        Assert.Equal("Art 5", t.Z(1));
        Assert.Equal("Renamed", t.Z(2));
        Assert.Equal("From z3", t.Z(4));
        Assert.Equal("Art 5", t.Z(5));        // 0 restores the original
        Assert.Equal(50, t.Host.H3.Artifacts[5].Cost);   // the installation's table itself is never changed
    }

    [Fact]
    public void Un_a_bans_artifacts_and_rebuilds_the_combo_table()
    {
        using var t = EraWithArtifacts(
            "!?FU(Go);\n" +
            "!!UN:A7/?v1;\n!!UN:A7/1;\n!!UN:A7/?v2;\n" +
            "!!UN:A0/?v3/0/0;\n" +                     // entry 0 holds Angelic Alliance (129)
            "!!UN:A20/?v4/0/0;\n" +                    // an empty entry holds 0
            "!!UN:A0/0/0/0;\n!!VRv5:S-2;\n!!VRv5&1:S1;\n!!VRv5&-1:S0;\n" +   // removal → flag 1 is 0
            "!!UN:A129/4/?v6;\n!!UN:A31/5/?v7;\n" +
            "!!UN:A20/150/10/11/12;\n!!VRv8:S-2;\n!!VRv8&1:S1;\n!!VRv8&-1:S0;\n" +
            "!!UN:A150/4/?v9;\n!!UN:A11/5/?v10;\n!!UN:A20/?v11/0/0;\n" +
            "!!UN:A21/150/10/165;\n!!VRv12:S-2;\n!!VRv12&1:S1;\n!!VRv12&-1:S0;\n");  // a part ≥ 160 is refused
        t.Call("Go");
        Assert.Equal("", t.ErrorText);
        Assert.Equal(0, t.V(1));
        Assert.Equal(1, t.V(2));
        Assert.Equal(129, t.V(3));
        Assert.Equal(0, t.V(4));
        Assert.Equal(0, t.V(5));
        Assert.Equal(-1, t.V(6));
        Assert.Equal(-1, t.V(7));
        Assert.Equal(1, t.V(8));
        Assert.Equal(20, t.V(9));
        Assert.Equal(20, t.V(10));
        Assert.Equal(150, t.V(11));
        Assert.Equal(0, t.V(12));
    }

    [Fact]
    public void Un_a_changes_are_saved_with_the_game()
    {
        string save = Path.Combine(root, "game.wog");
        using (var t = EraWithArtifacts("!?FU(Go);\n!!UN:A5/1/999;\n!!UN:A5/9/^Saved^;\n!!UN:A9/1;\n!!UN:A0/0/0/0;\n"))
        {
            t.Call("Go");
            t.Host.SaveTo(save, "test");
        }
        using var u = EraWithArtifacts("!?FU(Go);\n!!UN:A5/1/?v1;\n!!UN:A5/9/?z1;\n!!UN:A9/?v2;\n!!UN:A129/4/?v3;\n");
        u.Host.LoadFrom(save, "test");
        u.Call("Go");
        Assert.Equal("", u.ErrorText);
        Assert.Equal(999, u.V(1));
        Assert.Equal("Saved", u.Z(1));
        Assert.Equal(1, u.V(2));
        Assert.Equal(-1, u.V(3));
    }

    [Fact]
    public void Un_a_without_an_artifact_table_is_reported_unsupported()
    {
        using var t = new EraTestHost().Script("t.erm", "ZVSE2\n!?FU(Go);\n!!UN:A5/1/?v1;\n!!UN:A5/?v2;\n").Start();
        t.Call("Go");
        Assert.Equal(0, t.Errors);
        Assert.Contains(t.Host.Compat.Entries, e => e.Reason.Contains("artraits.txt"));
    }

    [Fact]
    public void Un_v_reports_the_dialect_versions_and_a_single_player_game()
    {
        using var t = new EraTestHost().Script("t.erm", "ZVSE2\n!?FU(Go);\n!!UN:V?v1/?v2/?v3/?v4/?v5/?v6/?v7;\n!!UN:V?v8/2;\n").Start();
        t.Call("Go");
        Assert.Equal(400, t.V(1));
        Assert.Equal(3931, t.V(2));
        Assert.Equal(0, t.V(4));
        Assert.Equal(0, t.V(6));
        Assert.Equal(0, t.V(7));
        Assert.Contains(t.Erm.Diagnostics, d => d.Message.Contains("UN:V"));   // setting a version is an error

        var w = new TestHost().Load("!?FU1;\n!!UN:V?v1/?v2;\n").Start();
        w.Call(1);
        Assert.Equal(358, w.V(1));
        Assert.Equal(281, w.V(2));
    }
}
