using System.Collections.Generic;
using System.Text.Json.Nodes;
using WoG.Core.Ids;
using WoG.Core.State;
using WoG.CreatureExperience;

namespace WoG.OldenEra.Data;

/// <summary>
/// Turns WoG stack-experience bonus tables into Olden Era buffs: one buff per (creature, rank), id
/// "wog_sx_&lt;wogType&gt;_r&lt;rank&gt;". Each buff carries the *difference* between the stat after
/// CrExpBon::Apply and the Olden Era unit's base stat, so '+', '-', '%' and '=' all end with the value
/// WoG would compute for that unit. Applied at battle start to the stack of matching rank
/// (IBattleAdapter.ApplyBuff).
///
/// Stat mapping (Compatibility_Matrix.md): A→offence, D→defence, H→hp, m→damageMin, M→damageMax,
/// S→speed (+ the same delta to initiative, because H3 speed is also turn order), R→numCounters.
/// O (shots), P (casts) and flags 'f' have no buff equivalent and are reported, not faked.
/// </summary>
public static class StackExperienceBuffs
{
    public sealed record Result(List<JsonObject> Buffs, List<string> NotExpressible);

    static readonly Dictionary<char, string[]> Map = new()
    {
        ['A'] = new[] { "offence" },
        ['D'] = new[] { "defence" },
        ['H'] = new[] { "hp" },
        ['m'] = new[] { "damageMin" },
        ['M'] = new[] { "damageMax" },
        ['S'] = new[] { "speed", "initiative" },
        ['R'] = new[] { "numCounters" },
    };

    public static string BuffId(int wogType, int rank) => $"wog_sx_{wogType}_r{rank}";

    public static Result Generate(CoreZipReader core, OverlayBuilder overlay, StackExperienceState sx, IdMap ids)
    {
        var res = new Result(new List<JsonObject>(), new List<string>());
        foreach (var (type, bonuses) in sx.Bonuses)
        {
            if (!ids.TryGetEngine("creature", type, out var sid)) { res.NotExpressible.Add($"creature {type}: no Olden Era unit mapped"); continue; }
            var unit = core.Find("DB/units/units_logics/", sid);
            if (unit?["stats"] is not JsonObject baseStats) { res.NotExpressible.Add($"creature {type}: unit {sid} not found"); continue; }
            for (int rank = 0; rank <= 10; rank++)
            {
                var stats = new Dictionary<string, double>();
                foreach (var b in bonuses)
                {
                    if (!Map.TryGetValue(b.Type, out var keys))
                    {
                        if (rank == 0) res.NotExpressible.Add($"creature {type}: bonus '{b.Type}{b.Mod}' has no buff equivalent");
                        continue;
                    }
                    int baseVal = (int?)baseStats[keys[0]] ?? 0;
                    int v = (int)ExperienceMath.ApplyMod(baseVal, b.Mod, b.Levels[rank]); // ApplyBonus without stack-artifact doubling
                    int delta = v - baseVal;
                    if (delta == 0) continue;
                    foreach (var k in keys) stats[k] = (stats.TryGetValue(k, out var d) ? d : 0) + delta;
                }
                if (stats.Count == 0) continue;
                res.Buffs.Add(overlay.StatBuff(BuffId(type, rank), stats));
            }
        }
        return res;
    }
}
