using System;
using System.IO;
using System.Linq;
using System.Text;
using WoG.Core.H3Data;
using WoG.Core.Save;
using Xunit;

namespace WoG.Tests;

/// <summary>SS — the H3 spell table (spell.cpp ERM_Spell): sptraits.txt and the executable's own table.</summary>
public class SpellReceiverTests
{
    /// <summary>sptraits.txt in SoD's layout: spell i on row i+5, i+8 (battle) or i+11 (creature abilities).</summary>
    public static string Sptraits()
    {
        var rows = Enumerable.Range(0, 92).Select(_ => "").ToArray();
        rows[0] = "\t\t\tSchool";
        rows[1] = "Name\tAbbreviated Name\tLevel\tEarth\tWater\tFire\tAir";
        rows[3] = "Adventure Spells";
        for (int i = 0; i < H3Tables.SpellCount; i++)
        {
            int row = i < 10 ? i + 5 : i < 70 ? i + 8 : i + 11;
            string school(int bit) => (i & bit) != 0 ? "x" : " ";
            var cols = new[] { $"Spell{i}", $"S{i}", (i % 5 + 1).ToString(), school(8), school(4), school(2), school(1) }
                .Concat(Enumerable.Range(0, 4).Select(k => (i + k).ToString()))          // cost
                .Append((i * 10).ToString())                                              // power
                .Concat(Enumerable.Range(0, 4).Select(k => (i * 2 + k).ToString()))      // effect
                .Concat(Enumerable.Range(0, 9).Select(k => k.ToString()))                 // chances
                .Concat(Enumerable.Range(0, 4).Select(k => (100 + k).ToString()))        // AI value
                .Concat(Enumerable.Range(0, 4).Select(k => $"\"{{Spell{i}}}\n\ndescription {k}\""));
            rows[row] = string.Join("\t", cols);
        }
        rows[15] = "Combat Spells";
        rows[78] = "Creature Abilities";
        return string.Join("\r\n", rows) + "\r\n";
    }

    /// <summary>A PE32 file whose .data section holds the spell table at 0x6854A0: target, animation, flags per spell.</summary>
    public static byte[] Executable(Func<int, (int Target, int Def, int Flags)> spell)
    {
        const uint imageBase = 0x400000, sectionRva = 0x285000, rawOffset = 0x400;
        int tableOffset = (int)(ExeSpellTable.TableAddress - imageBase - sectionRva);
        int rawSize = tableOffset + H3Tables.SpellCount * ExeSpellTable.EntrySize;
        var image = new byte[rawOffset + rawSize];
        image[0] = (byte)'M'; image[1] = (byte)'Z';
        const int pe = 0x80;
        BitConverter.GetBytes(pe).CopyTo(image, 0x3C);
        Encoding.ASCII.GetBytes("PE\0\0").CopyTo(image, pe);
        BitConverter.GetBytes((ushort)1).CopyTo(image, pe + 6);       // one section
        BitConverter.GetBytes((ushort)0xE0).CopyTo(image, pe + 20);   // optional header size
        BitConverter.GetBytes((ushort)0x10B).CopyTo(image, pe + 24);  // PE32
        BitConverter.GetBytes(imageBase).CopyTo(image, pe + 24 + 28);
        int s = pe + 24 + 0xE0;
        Encoding.ASCII.GetBytes(".data").CopyTo(image, s);
        BitConverter.GetBytes((uint)rawSize).CopyTo(image, s + 8);
        BitConverter.GetBytes(sectionRva).CopyTo(image, s + 12);
        BitConverter.GetBytes((uint)rawSize).CopyTo(image, s + 16);
        BitConverter.GetBytes(rawOffset).CopyTo(image, s + 20);
        for (int i = 0; i < H3Tables.SpellCount; i++)
        {
            var (target, def, flags) = spell(i);
            int o = (int)rawOffset + tableOffset + i * ExeSpellTable.EntrySize;
            BitConverter.GetBytes(target).CopyTo(image, o);
            BitConverter.GetBytes(def).CopyTo(image, o + 8);
            BitConverter.GetBytes(flags).CopyTo(image, o + 0xC);
        }
        return image;
    }

    /// <summary>H3-like values: Summon Boat an adventure spell, Magic Arrow a battle damage spell at an enemy.</summary>
    static (int, int, int) H3Like(int i) => i switch
    {
        0 => (0, -1, 0x100002),
        15 => (-1, 64, 0x8211),
        41 => (1, 36, 0x40845),
        _ => (0, i, i < 10 ? 2 : 1),
    };

    [Fact]
    public void Sptraits_rows_follow_WoGs_layout()
    {
        var t = H3Tables.ParseSpellTable(Sptraits());
        Assert.Equal(H3Tables.SpellCount, t.Count);
        var s = t[15];
        Assert.Equal(("Spell15", "S15", 1, 15), (s.Name, s.AbbrName, s.Level, s.Schools)); // 15 = all four schools
        Assert.Equal(new[] { 15, 16, 17, 18 }, s.Cost);
        Assert.Equal(150, s.Power);
        Assert.Equal(new[] { 30, 31, 32, 33 }, s.Effect);
        Assert.Equal(8, s.Chance[8]);
        Assert.Equal(103, s.AiValue[3]);
        Assert.Equal("{Spell15}\n\ndescription 2", s.Description[2]);
        Assert.Equal(("Spell70", 0), (t[70].Name, t[70].Schools & 0)); // creature abilities after their header
        Assert.Equal(8, t[8].Schools);                                  // Earth
    }

    [Fact]
    public void The_executable_gives_target_animation_and_flags()
    {
        string dir = Path.Combine(Path.GetTempPath(), "exe-spells-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var spells = H3Tables.ParseSpellTable(Sptraits());
            File.WriteAllBytes(Path.Combine(dir, "h3era.exe"), Executable(i => (0, 0, 0))); // not H3's table
            Assert.Null(ExeSpellTable.Apply(dir, spells));
            Assert.False(spells[15].FromExecutable);
            File.WriteAllBytes(Path.Combine(dir, "h3era.exe"), Executable(H3Like));
            Assert.EndsWith("h3era.exe", ExeSpellTable.Apply(dir, spells));
            Assert.Equal((-1, 64, 0x8211, true), (spells[15].Target, spells[15].DefIndex, spells[15].Flags, spells[15].FromExecutable));
            Assert.Equal(1, spells[41].Target);
        }
        finally { Directory.Delete(dir, true); }
    }

    static EraTestHost Host(string script, bool exe = true)
    {
        var t = new EraTestHost().Script("t.erm", script);
        File.WriteAllText(Path.Combine(t.ModDir, "Data", "sptraits.txt"), Sptraits());
        string game = Path.Combine(t.ModDir, "game");
        Directory.CreateDirectory(game);
        if (exe) File.WriteAllBytes(Path.Combine(game, "h3era.exe"), Executable(H3Like));
        t.Host.SetEraFolders(game, Path.Combine(t.ModDir, "write"));
        return t.Start();
    }

    [Fact]
    public void Ss_reads_the_spell_table()
    {
        using var t = Host("ZVSE2\n!?FU(Go);\n" +
            "!!SS15:L?v1 S?v2 P?v3 O?v4 F?v5 X?v6;\n" +
            "!!SS15:C2/?v7 E3/?v8 H8/?v9 I0/?v10;\n" +
            "!!SS15:N?v11 D1/?v12;\n");
        t.Call("Go");
        Assert.Equal("", t.ErrorText);
        Assert.Equal((1, 15, 150, -1, 0x8211, 64), (t.V(1), t.V(2), t.V(3), t.V(4), t.V(5), t.V(6)));
        Assert.Equal((17, 33, 8, 100), (t.V(7), t.V(8), t.V(9), t.V(10)));
        Assert.Equal((0, 0), (t.V(11), t.V(12)));                    // no text set: 0
        Assert.Empty(t.Host.Compat.Entries);
    }

    [Fact]
    public void Ss_changes_are_kept_saved_and_reported()
    {
        using var t = Host("ZVSE2\n!?FU(Go);\n" +
            "!!re i/66/69;\n  !!SSi:C0/d*10 Fd|2;\n!!en;\n" +
            "!!SS66:C0/?v1 F?v2;\n!!SS67:C0/d:10 C0/?v3;\n" +
            "!!SS15:N5 N?v4;\n");
        t.Call("Go");
        Assert.Equal("", t.ErrorText);
        Assert.Equal((660, 1 | 2, 67), (t.V(1), t.V(2), t.V(3)));   // battle spell made an adventure one too
        Assert.Equal(5, t.V(4));
        Assert.Equal(66, t.Host.H3.SpellTable[66].Cost[0]);           // the installation's table is not changed
        Assert.Contains(t.Host.Compat.Entries, e => e.Reason.Contains("Olden Era's spells do not change"));
        var loaded = WoGSaveSerializer.Deserialize(WoGSaveSerializer.Serialize(t.Host.State, "id"), "id");
        Assert.Equal(660, loaded.SpellOverrides[66].Cost[0]);
        Assert.Equal(5, loaded.SpellOverrides[15].TextVars[0]);
    }

    [Fact]
    public void Ss_without_the_executable_reports_its_flags_unsupported()
    {
        using var t = Host("ZVSE2\n!?FU(Go);\n!!SS15:L?v1 F?v2;\n!!SS81:L?v3;\n", exe: false);
        t.Call("Go");
        Assert.Equal(1, t.V(1));
        Assert.Contains(t.Host.Compat.Entries, e => e.Reason.Contains("h3era.exe"));
        Assert.Contains("incorrect spell index", t.ErrorText);
    }

    /// <summary>The user's ERA installation (ERA_GAME_DIR): sptraits.txt through Era's VFS and h3era.exe's table.</summary>
    [Fact]
    public void The_installation_spell_table()
    {
        string? dir = Environment.GetEnvironmentVariable("ERA_GAME_DIR");
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;
        var mods = new[] { "WoG Rus", "WoG" }.Select(m => Path.Combine(dir, "Mods", m)).ToArray();
        var h3 = H3Tables.Load(new EraVfs(dir, mods));
        Assert.Equal(H3Tables.SpellCount, h3.SpellTable.Count);
        var arrow = h3.SpellTable[15];
        Assert.Equal((1, 15, -1, 0x8211, true), (arrow.Level, arrow.Schools, arrow.Target, arrow.Flags, arrow.FromExecutable));
        Assert.Equal(1, h3.SpellTable[41].Target);                     // Bless: a friendly stack
        Assert.True((h3.SpellTable[0].Flags & 2) != 0);                // Summon Boat: adventure map
        Assert.True((h3.SpellTable[70].Flags & 8) != 0);               // creature abilities from 70
    }
}
