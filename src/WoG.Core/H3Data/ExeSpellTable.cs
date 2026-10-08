using System;
using System.Collections.Generic;
using System.IO;
using WoG.Core.Model;

namespace WoG.Core.H3Data;

/// <summary>
/// The part of H3's spell table that is not in sptraits.txt — the target, the animation and the flags — read from the
/// initialised data of the installation's SoD-based executable. WoG's ResetSpells copies the table from 0x6854A0
/// (SPELLNUM_0 = 81 entries of _Spell_, 0x88 bytes: +0 target, +8 animation, +0xC flags; the texts and numbers are
/// filled from sptraits.txt at run time). Read only; nothing of it is stored by the port.
/// </summary>
public static class ExeSpellTable
{
    public const uint TableAddress = 0x6854A0;
    public const int EntrySize = 0x88;

    /// <summary>The executables tried, in order (ERA's own first).</summary>
    public static readonly string[] Executables = { "h3era.exe", "heroes3.exe" };

    /// <summary>Fills Target, DefIndex and Flags of the spells; returns the executable used, or null.</summary>
    public static string? Apply(string gameFolder, IReadOnlyList<WoGSpell> spells)
    {
        if (spells.Count == 0) return null;
        foreach (var name in Executables)
        {
            string path = Path.Combine(gameFolder, name);
            if (!File.Exists(path)) continue;
            byte[]? table;
            try { table = PeImage.ReadVirtual(File.ReadAllBytes(path), TableAddress, spells.Count * EntrySize); }
            catch (Exception) { continue; }
            if (table == null || !Plausible(table)) continue;
            for (int i = 0; i < spells.Count; i++)
            {
                int o = i * EntrySize;
                spells[i].Target = BitConverter.ToInt32(table, o);
                spells[i].DefIndex = BitConverter.ToInt32(table, o + 8);
                spells[i].Flags = BitConverter.ToInt32(table, o + 0xC);
                spells[i].FromExecutable = true;
            }
            return path;
        }
        return null;
    }

    /// <summary>
    /// The table is H3's when Summon Boat (0) is an adventure-map spell and Magic Arrow (15) a battle spell aimed at an
    /// enemy stack that deals damage — another build keeps something else at that address.
    /// </summary>
    static bool Plausible(byte[] t)
    {
        if (t.Length < 16 * EntrySize) return false;
        int boatFlags = BitConverter.ToInt32(t, 0xC);
        int arrowTarget = BitConverter.ToInt32(t, 15 * EntrySize), arrowFlags = BitConverter.ToInt32(t, 15 * EntrySize + 0xC);
        return (boatFlags & 2) != 0 && arrowTarget == -1 && (arrowFlags & 0x201) == 0x201;
    }
}

/// <summary>Reads initialised data of a 32-bit Windows executable by its virtual address (the PE section table).</summary>
public static class PeImage
{
    /// <summary>The bytes at a virtual address, or null when no section holds all of them in the file.</summary>
    public static byte[]? ReadVirtual(byte[] image, uint address, int size)
    {
        if (image.Length < 0x40 || image[0] != 'M' || image[1] != 'Z') return null;
        int pe = BitConverter.ToInt32(image, 0x3C);
        if (pe <= 0 || pe + 24 > image.Length || BitConverter.ToUInt32(image, pe) != 0x00004550) return null; // "PE\0\0"
        int sections = BitConverter.ToUInt16(image, pe + 6);
        int optional = BitConverter.ToUInt16(image, pe + 20);
        if (BitConverter.ToUInt16(image, pe + 24) != 0x10B) return null; // PE32
        uint imageBase = BitConverter.ToUInt32(image, pe + 24 + 28);
        if (address < imageBase) return null;
        uint rva = address - imageBase;
        int table = pe + 24 + optional;
        for (int i = 0; i < sections; i++)
        {
            int s = table + 40 * i;
            if (s + 40 > image.Length) return null;
            uint virtualSize = BitConverter.ToUInt32(image, s + 8), virtualAddress = BitConverter.ToUInt32(image, s + 12);
            uint rawSize = BitConverter.ToUInt32(image, s + 16), rawOffset = BitConverter.ToUInt32(image, s + 20);
            if (rva < virtualAddress || rva >= virtualAddress + Math.Max(virtualSize, rawSize)) continue;
            uint offset = rva - virtualAddress;
            if (offset + (uint)size > rawSize || rawOffset + offset + (uint)size > (uint)image.Length) return null;
            var bytes = new byte[size];
            Array.Copy(image, (int)(rawOffset + offset), bytes, 0, size);
            return bytes;
        }
        return null;
    }
}
