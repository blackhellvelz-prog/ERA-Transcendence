using System.Collections.Generic;
using WoG.Core.H3Data;
using WoG.Core.Services;

namespace WoG.Erm.Era;

/// <summary>
/// The texts of the H3 tables as ERM sees them: the ERA installation's, or what SN:H put in their place (AdvErm.SN_H
/// writes into Heroes.SecSkillTexts, MonInfos, ArtInfos, Spells and Erm.HeroSpecsTable; the port keeps the change in
/// WoGGameState.Hints and reads it over the installation's table).
/// </summary>
public static class H3Texts
{
    /// <summary>SN:H's code of an object type and subtype (AdvErm CODE_TYPE_SUBTYPE); a square's code has no such bit.</summary>
    public const int CodeTypeSubtype = 0x01000000;

    /// <summary>The sections of SN:H (AdvErm.InitHints).</summary>
    public static readonly string[] Sections = { "object", "spec", "secskill", "monname", "art", "spell" };

    /// <summary>The highest text kind of each text section (Era's range checks).</summary>
    public static int MaxKind(string section) => section switch
    {
        "spec" => 2,      // short, full, description
        "secskill" => 3,  // name, basic, advanced, expert
        "monname" => 2,   // single, plural, description
        "art" => 1,       // name, description
        "spell" => 6,     // name, short name, description, basic, advanced, expert, sound
        _ => -1,
    };

    /// <summary>Era's code of a text in its section (spec and secskill shift the kind by 8, the others by 16).</summary>
    public static int Code(string section, int id, int kind) =>
        section is "spec" or "secskill" ? id | (kind << 8) : id | (kind << 16);

    /// <summary>The text ERM reads: SN:H's replacement, else the installation's; null when the table was not read.</summary>
    public static string? Get(IWoGServices s, string section, int id, int kind) =>
        s.State.Hints.TryGetValue(section, out var own) && own.TryGetValue(Code(section, id, kind), out var text)
            ? text
            : Original(s, section, id, kind);

    /// <summary>The installation's text (hero specialties and spell sounds are not read: null).</summary>
    public static string? Original(IWoGServices s, string section, int id, int kind)
    {
        var h3 = s.H3;
        switch (section)
        {
            case "secskill":
                return id < h3.SecondarySkills.Count && kind < h3.SecondarySkills[id].Length ? h3.SecondarySkills[id][kind] : null;
            case "monname":
                if (id >= h3.Creatures.Count) return null;
                var cr = h3.Creatures[id];
                return kind switch { 0 => cr.Name, 1 => cr.NamePlural, _ => cr.Ability };
            case "art":
                return s.Artifact(id) is { } art ? (kind == 0 ? art.Name : art.Description) : null;
            case "spell":
                if (id >= h3.SpellTable.Count || kind == 6) return null;
                var sp = h3.SpellTable[id];
                return kind switch { 0 => sp.Name, 1 => sp.AbbrName, _ => sp.Description[kind - 2] };
            default:
                return null;
        }
    }

    /// <summary>The section's hints, created on first use.</summary>
    public static Dictionary<int, string> Section(IWoGServices s, string section)
    {
        if (!s.State.Hints.TryGetValue(section, out var d)) s.State.Hints[section] = d = new Dictionary<int, string>();
        return d;
    }
}
