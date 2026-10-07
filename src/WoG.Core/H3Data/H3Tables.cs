using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WoG.Core.Model;

namespace WoG.Core.H3Data;

/// <summary>
/// The Heroes III / WoG data tables an ERA installation provides (text tables in its LODs and mod archives), read
/// with <see cref="EraVfs"/>. They describe the H3/WoG ids ERM scripts use; whether an id exists in Olden Era is a
/// separate question answered by the IdMap.
/// </summary>
public sealed class H3Tables
{
    public IReadOnlyList<WoGArtifact> Artifacts { get; private set; } = Array.Empty<WoGArtifact>();
    /// <summary>Where each table came from (for the log).</summary>
    public Dictionary<string, string> Sources { get; } = new(StringComparer.OrdinalIgnoreCase);

    public static H3Tables Load(EraVfs vfs)
    {
        var t = new H3Tables();
        var art = vfs.Read("artraits.txt", out var from);
        if (art != null) { t.Artifacts = ParseArtifacts(H3Text.Decode(art)); t.Sources["artraits.txt"] = from!; }
        return t;
    }

    // ---- artifacts --------------------------------------------------------------------------------

    /// <summary>
    /// artraits.txt slot columns, in file order (Spell Book, War Machine 4…1, Misc 5…1, Feet, Left Ring, Right Ring,
    /// Torso, Left Hand, Right Hand, Neck, Shoulders, Head), as the "Format P2" position UN:A reports. ERM's own
    /// documentation and scripts (AMER_HumanAI "artiSlotP2") use these codes; 0 = no column = backpack only.
    /// </summary>
    static readonly int[] SlotColumns = { 14, 13, 12, 11, 10, 9, 9, 9, 9, 9, 8, 7, 7, 6, 5, 4, 3, 2, 1 };

    /// <summary>
    /// Combination artifacts of SoD (fixed in the H3 executable, not in the text tables): the combo's artifact number
    /// is 129 + its index; SuperN = index for the combo, PartOfSuperN = index for each part, -1 elsewhere.
    /// </summary>
    public static readonly int[][] Combos =
    {
        new[] { 31, 32, 33, 34, 35, 36 },             // 129 Angelic Alliance
        new[] { 54, 55, 56 },                         // 130 Cloak of the Undead King
        new[] { 94, 95, 96 },                         // 131 Elixir of Life
        new[] { 8, 14, 20, 26 },                      // 132 Armor of the Damned
        new[] { 118, 119, 120, 121, 122 },            // 133 Statue of Legion
        new[] { 37, 38, 39, 40, 41, 42, 43, 44, 45 }, // 134 Power of the Dragon Father
        new[] { 12, 18, 24, 30 },                     // 135 Titan's Thunder
        new[] { 71, 123 },                            // 136 Admiral's Hat
        new[] { 60, 61, 62 },                         // 137 Bow of the Sharpshooter
        new[] { 73, 74, 75 },                         // 138 Wizard's Well
        new[] { 76, 77, 78 },                         // 139 Ring of the Magi
        new[] { 109, 110, 111, 113 },                 // 140 Cornucopia
    };

    public const int FirstCombo = 129;

    /// <summary>H3 artifact class bits: S special 1, T treasure 2, N minor 4, J major 8, R relic 16.</summary>
    public static int ClassBits(string letter) => letter.Trim().ToUpperInvariant() switch
    {
        "S" => 1, "T" => 2, "N" => 4, "J" => 8, "R" => 16, _ => 0,
    };

    public static List<WoGArtifact> ParseArtifacts(string text)
    {
        var rows = H3Text.Records(text);
        var list = new List<WoGArtifact>();
        // two header rows: "Hero Slots", then the column names
        foreach (var r in rows.Skip(2))
        {
            if (r.Length < 2 || r[0].Length == 0) continue;
            int id = list.Count;
            int position = 0;
            for (int col = 0; col < SlotColumns.Length && position == 0; col++)
                if (2 + col < r.Length && r[2 + col].Trim().Length > 0) position = SlotColumns[col];
            var a = new WoGArtifact
            {
                Id = id,
                Name = r[0],
                Cost = H3Text.Int(r, 1),
                Position = position,
                Type = ClassBits(r.Length > 21 ? r[21] : ""),
                Description = r.Length > 22 ? r[22] : "",
                SuperN = -1,
                PartOfSuperN = -1,
                NewSpell = -1,
            };
            list.Add(a);
        }
        for (int combo = 0; combo < Combos.Length; combo++)
        {
            if (FirstCombo + combo < list.Count) list[FirstCombo + combo].SuperN = combo;
            foreach (int part in Combos[combo])
                if (part < list.Count) list[part].PartOfSuperN = combo;
        }
        return list;
    }
}

/// <summary>Heroes III text tables: tab-separated fields, CRLF records; a quoted field may hold line breaks.</summary>
public static class H3Text
{
    /// <summary>ERA's files are cp1251 (classic) or UTF-8 (modern mods): strict UTF-8 first, else cp1251.</summary>
    public static string Decode(byte[] bytes)
    {
        int offset = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
        try { return new UTF8Encoding(false, true).GetString(bytes, offset, bytes.Length - offset); }
        catch (DecoderFallbackException)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(1251).GetString(bytes, offset, bytes.Length - offset);
        }
    }

    public static List<string[]> Records(string text)
    {
        var records = new List<string[]>();
        var fields = new List<string>();
        var sb = new StringBuilder();
        bool quoted = false;
        for (int i = 0; i < text.Length; i++)
        {
            char ch = text[i];
            if (quoted)
            {
                if (ch == '"') quoted = false;
                else sb.Append(ch);
                continue;
            }
            switch (ch)
            {
                case '"' when sb.Length == 0:
                    quoted = true;
                    break;
                case '\t':
                    fields.Add(sb.ToString()); sb.Clear();
                    break;
                case '\r':
                    break;
                case '\n':
                    fields.Add(sb.ToString()); sb.Clear();
                    records.Add(fields.ToArray()); fields.Clear();
                    break;
                default:
                    sb.Append(ch);
                    break;
            }
        }
        if (sb.Length > 0 || fields.Count > 0) { fields.Add(sb.ToString()); records.Add(fields.ToArray()); }
        return records;
    }

    public static int Int(string[] r, int i) =>
        i < r.Length && int.TryParse(r[i].Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int v) ? v : 0;
}
