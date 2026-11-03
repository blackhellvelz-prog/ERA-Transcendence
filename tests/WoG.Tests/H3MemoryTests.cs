using System;
using System.IO;
using System.Linq;
using System.Text;
using WoG.Core.H3Data;
using WoG.Core.Save;
using WoG.Erm.Era;
using Xunit;

namespace WoG.Tests;

/// <summary>UN:C — the H3 addresses scripts use, by what they mean (H3Memory).</summary>
public class H3MemoryTests
{
    const uint ImageBase = 0x400000, SectionRva = 0x1000, RawOffset = 0x400, End = 0x690000;

    /// <summary>
    /// A PE32 file with one section from 0x401000 holding H3's bytes the tests need: the spell table at 0x6854A0
    /// (SpellReceiverTests.H3Like), the spell table variable and the operand that names it, SoD's Resistance table
    /// (0, 5, 10, 20 %), the object deletion code WoG patches and the screen height.
    /// </summary>
    static byte[] Executable()
    {
        int rawSize = (int)(End - ImageBase - SectionRva);
        var image = new byte[RawOffset + rawSize];
        image[0] = (byte)'M'; image[1] = (byte)'Z';
        const int pe = 0x80;
        BitConverter.GetBytes(pe).CopyTo(image, 0x3C);
        Encoding.ASCII.GetBytes("PE\0\0").CopyTo(image, pe);
        BitConverter.GetBytes((ushort)1).CopyTo(image, pe + 6);
        BitConverter.GetBytes((ushort)0xE0).CopyTo(image, pe + 20);
        BitConverter.GetBytes((ushort)0x10B).CopyTo(image, pe + 24);
        BitConverter.GetBytes(ImageBase).CopyTo(image, pe + 24 + 28);
        int s = pe + 24 + 0xE0;
        Encoding.ASCII.GetBytes(".text").CopyTo(image, s);
        BitConverter.GetBytes((uint)rawSize).CopyTo(image, s + 8);
        BitConverter.GetBytes(SectionRva).CopyTo(image, s + 12);
        BitConverter.GetBytes((uint)rawSize).CopyTo(image, s + 16);
        BitConverter.GetBytes(RawOffset).CopyTo(image, s + 20);

        void Put(uint address, byte[] bytes) => bytes.CopyTo(image, (int)(RawOffset + address - ImageBase - SectionRva));
        for (int i = 0; i < H3Tables.SpellCount; i++)
        {
            var (target, def, flags) = SpellReceiverTests.H3Like(i);
            uint o = ExeSpellTable.TableAddress + (uint)(i * ExeSpellTable.EntrySize);
            Put(o, BitConverter.GetBytes(target));
            Put(o + 8, BitConverter.GetBytes(def));
            Put(o + 0xC, BitConverter.GetBytes(flags));
        }
        Put(H3Memory.SpellTableVariable, BitConverter.GetBytes(ExeSpellTable.TableAddress));
        Put(0x4446DB, BitConverter.GetBytes(H3Memory.SpellTableVariable));
        Put(0x63EA48, new[] { 0f, 0.05f, 0.1f, 0.2f }.SelectMany(BitConverter.GetBytes).ToArray());
        Put(0x4AA9EF, new byte[] { 0x66, 0x8B });
        Put(0x40144F, BitConverter.GetBytes((short)600));
        return image;
    }

    static EraTestHost Host(string script, bool exe = true)
    {
        var t = new EraTestHost().Script("t.erm", script);
        File.WriteAllText(Path.Combine(t.ModDir, "Data", "sptraits.txt"), SpellReceiverTests.Sptraits());
        string game = Path.Combine(t.ModDir, "game");
        Directory.CreateDirectory(game);
        if (exe) File.WriteAllBytes(Path.Combine(game, "h3era.exe"), Executable());
        t.Host.SetEraFolders(game, Path.Combine(t.ModDir, "write"));
        return t.Start();
    }

    static int Bits(float f) => BitConverter.SingleToInt32Bits(f);

    [Fact]
    public void Wogify_reads_a_spell_school_through_the_spell_table_variable()
    {
        // 78 wog - wogify.erm FU7779 (igrik): the operand → the variable → the table → spell * 136 + 28
        using var t = Host("ZVSE2\n!?FU(Go);\n" +
            "!!UN:C4474587/4/?v1 Cv1/4/?v2;\n" +
            "!!VRv3:S6 *136 +v2 +28;\n" +
            "!!UN:Cv3/4/?v4;\n" +
            "!!UN:Cv2/840/4/?v5;\n" +          // the four-parameter form: spell 6's level (+0x18)
            "!!UN:Cv2/2072/4/?v6;\n" +         // spell 15's first cost (15 * 136 + 0x20)
            "!!UN:Cv2/2052/-1/?v7;\n");        // spell 15's flags, lowest byte, unsigned (15 * 136 + 0xC)
        t.Call("Go");
        Assert.Equal("", t.ErrorText);
        Assert.Equal((H3Memory.SpellTableVariable, H3Memory.VirtualSpellTable), (t.V(1), t.V(2)));
        Assert.Equal((6, 2, 15, 0x11), (t.V(4), t.V(5), t.V(6), t.V(7))); // Fire + Water, level 2; flags 0x8211
        Assert.Empty(t.Host.Compat.Entries);
    }

    [Fact]
    public void A_spell_table_write_is_the_spell_change_of_SS()
    {
        using var t = Host("ZVSE2\n!?FU(Go);\n" +
            "!!UN:C4474587/4/?v1 Cv1/4/?v2;\n" +
            "!!UN:Cv2/844/4/1;\n" +            // spell 6's schools := Air
            "!!SS6:S?v3;\n" +
            "!!UN:Cv2/845/1/?v4;\n" +          // a byte inside the field
            "!!UN:Cv2/840/4/d1;\n" +           // level + 1 (d)
            "!!SS6:L?v5;\n" +
            "!!UN:Cv2/836/4/?v6;\n");          // the short name pointer (+0x14)
        t.Call("Go");
        Assert.Equal("", t.ErrorText);
        Assert.Equal((1, 0, 3), (t.V(3), t.V(4), t.V(5)));
        Assert.Equal(6, t.Host.H3.SpellTable[6].Schools);             // the installation's table is not changed
        Assert.Equal(1, t.Host.State.SpellOverrides[6].Schools);
        Assert.Contains(t.Host.Compat.Entries, e => e.Reason == H3Memory.SpellsNotInGame);
        Assert.Contains(t.Host.Compat.Entries, e => e.Reason.Contains("text pointer of H3's spell table"));
    }

    [Fact]
    public void Enhanced_resistance_is_kept_saved_and_put_back()
    {
        // 30 wog - enhanced secondary skills.erm: OnGameEnter keeps the old values and sets WoG's; OnGameLeave restores
        using var t = Host("ZVSE2\n!?FU(Enter);\n" +
            "!!UN:C6548044/4/?v1 C6548048/4/?v2 C6548052/4/?v3;\n" +
            "!!UN:C6548044/4/1036831949 C6548048/4/1045220556 C6548052/4/1050253721;\n" +
            "!!UN:C6548048/4/?v4;\n" +
            "!?FU(Leave);\n" +
            "!!UN:C6548044/4/v1 C6548048/4/v2 C6548052/4/v3;\n");
        t.Call("Enter");
        Assert.Equal("", t.ErrorText);
        Assert.Equal((Bits(0.05f), Bits(0.1f), Bits(0.2f)), (t.V(1), t.V(2), t.V(3)));
        Assert.Equal(1045220556, t.V(4));
        Assert.Equal(new[] { 0, 1036831949, 1045220556, 1050253721 }, t.Host.State.SkillEffects[26]);
        Assert.Contains(t.Host.Compat.Entries, e => e.Reason == H3Memory.SkillsNotInGame);
        var loaded = WoGSaveSerializer.Deserialize(WoGSaveSerializer.Serialize(t.Host.State, "id"), "id");
        Assert.Equal(1050253721, loaded.SkillEffects[26][3]);
        t.Call("Leave");
        Assert.Empty(t.Host.State.SkillEffects);                       // the original values leave no change behind
    }

    [Fact]
    public void A_code_patch_reads_the_executable_and_keeps_the_write()
    {
        // 9000 wog - stdlib.erm: jump over the deletion sound while walking the map, then put the code back
        using var t = Host("ZVSE2\n!?FU(Go);\n" +
            "!!UN:C4893167/2/?v1;\n!!UN:C4893167/2/24555;\n!!UN:C4893167/2/?v2;\n" +
            "!!UN:C4893167/2/v1;\n!!UN:C4893167/-2/?v3;\n" +
            "!!UN:C4199503/2/?v4;\n");
        t.Call("Go");
        Assert.Equal("", t.ErrorText);
        Assert.Equal((-29850, 24555, 0x8B66, 600), (t.V(1), t.V(2), t.V(3), t.V(4))); // signed 0x8B66, then unsigned
        Assert.Empty(t.Host.Compat.Entries);
    }

    [Fact]
    public void Other_addresses_and_a_wrong_size_are_reported()
    {
        using var t = Host("ZVSE2\n!?FU(Go);\n!!UN:C6919480/4/?v1;\n!?FU(Size);\n!!UN:C4893167/3/?v2;\n");
        t.Call("Go");
        Assert.Contains(t.Host.Compat.Entries, e => e.Reason.Contains("6919480 (0x699538): no Olden Era meaning"));
        t.Call("Size");
        Assert.Contains("wrong Size parameter: 3", t.ErrorText);
    }

    [Fact]
    public void Without_the_executable_its_bytes_are_unsupported()
    {
        using var t = Host("ZVSE2\n!?FU(Go);\n!!UN:C6548044/4/?v1;\n", exe: false);
        t.Call("Go");
        Assert.Contains(t.Host.Compat.Entries, e => e.Reason.Contains("h3era.exe) was not read"));
    }

    /// <summary>The user's ERA installation (ERA_GAME_DIR): the addresses in its own h3era.exe.</summary>
    [Fact]
    public void The_installation_executable_has_these_meanings()
    {
        string? dir = Environment.GetEnvironmentVariable("ERA_GAME_DIR");
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;
        var h3 = H3Tables.Load(new EraVfs(dir, new[] { "WoG Rus", "WoG" }.Select(m => Path.Combine(dir, "Mods", m)).ToArray()));
        int Int(int address, int size = 4)
        {
            var b = h3.ReadExecutable(address, size)!;
            return size == 2 ? BitConverter.ToInt16(b, 0) : BitConverter.ToInt32(b, 0);
        }
        Assert.Equal(H3Memory.SpellTableVariable, Int(0x4446DB));
        Assert.Equal((int)ExeSpellTable.TableAddress, Int(H3Memory.SpellTableVariable));
        foreach (var (address, _, _) in H3Memory.SkillTables)            // SoD: 5 %, 10 %, then 15 % or 20 %
            Assert.Equal((0, Bits(0.05f), Bits(0.1f)), (Int(address), Int(address + 4), Int(address + 8)));
        Assert.Equal(Bits(0.2f), Int(0x63EA48 + 12));                    // expert Resistance
        Assert.Equal((800, 600), (Int(0x401448, 2), Int(0x40144F, 2)));
        Assert.Equal(555, Int(0x71286D));                                // the town hint's y
    }
}
