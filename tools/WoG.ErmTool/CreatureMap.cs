using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>
/// idmap-creatures: fills Compatibility/id-maps/creature.json (WoG/H3 creature number ↔ Olden Era unit sid) from the
/// units of the user's Core.zip. Rules (Compatibility/id-maps/README.md):
///  * H3 town → Olden Era faction by theme: castle → human (Temple), rampart → nature (Grove), necropolis → undead,
///    dungeon → dungeon, inferno → demon (Hive), conflux → unfrozen (Schism); tower, stronghold and fortress have no
///    Olden Era faction and stay unmapped;
///  * H3 level L: the base creature → the faction's tier-L base unit, the upgrade → its "_upg" unit;
///  * neutrals with the same creature: halfling, peasant, fairie dragon;
///  * every Olden Era unit left without an H3 number (alternative upgrades "_upg_alt", Olden Era neutrals) gets a
///    number from 1000 up, in faction/tier/sid order; numbers given before are kept, new units are appended.
/// The file holds only sids (no game data), so it can be committed; rerun it after a game update.
/// </summary>
static class CreatureMap
{
    static readonly Dictionary<string, string> TownToFaction = new()
    {
        ["castle"] = "human", ["rampart"] = "nature", ["necropolis"] = "undead",
        ["dungeon"] = "dungeon", ["inferno"] = "demon", ["conflux"] = "unfrozen",
    };

    static readonly Dictionary<string, string> NeutralByName = new()
    {
        ["halfling"] = "halfling", ["peasant"] = "peasant", ["fairieDragon"] = "fairy_dragon",
    };

    static readonly string[] FactionOrder = { "human", "nature", "undead", "dungeon", "demon", "unfrozen", "neutral" };

    public const int FirstOldenEraNumber = 1000;

    sealed record Unit(string Id, string Faction, int Tier);

    public static int Run(string coreZip, string jsonPath)
    {
        var units = ReadUnits(coreZip);
        Console.WriteLine($"{units.Count} Olden Era units in {Path.GetFileName(coreZip)}");
        var byId = units.ToDictionary(u => u.Id);
        var rows = JsonNode.Parse(File.ReadAllText(jsonPath))!.AsArray();

        // Olden Era numbers given before stay; their rows are rebuilt below.
        var kept = rows.Where(r => (int)r!["wog"]! >= FirstOldenEraNumber && r!["engine"] is JsonValue)
            .ToDictionary(r => (string)r!["engine"]!, r => (int)r!["wog"]!);
        var h3Rows = rows.Where(r => (int)r!["wog"]! < FirstOldenEraNumber).Select(r => r!.AsObject()).ToList();

        // H3 pairs per town and level: the lower number is the base creature, the higher one its upgrade.
        foreach (var group in h3Rows.Where(r => r["faction"] is JsonValue && r["level"] is JsonValue)
                     .GroupBy(r => ((string)r["faction"]!, (int)r["level"]!)))
        {
            var (town, level) = group.Key;
            var pair = group.OrderBy(r => (int)r["wog"]!).ToList();
            for (int i = 0; i < pair.Count; i++)
            {
                var row = pair[i];
                string? sid = null;
                string note;
                if (TownToFaction.TryGetValue(town, out var faction) && pair.Count == 2 && level is >= 1 and <= 7)
                {
                    var b = BaseUnit(units, faction, level);
                    sid = b == null ? null : i == 0 ? b.Id : byId.ContainsKey(b.Id + "_upg") ? b.Id + "_upg" : null;
                    note = sid == null ? $"no {faction} tier {level} unit" : $"{faction} tier {level} {(i == 0 ? "base" : "upgrade")}";
                }
                else if (town == "neutral" && NeutralByName.TryGetValue((string)row["h3"]!, out var n) && byId.ContainsKey(n))
                {
                    sid = n;
                    note = "same neutral creature";
                }
                else note = TownToFaction.ContainsKey(town) || town == "neutral" || town == "special"
                    ? "no Olden Era counterpart" : $"no Olden Era faction for {town}";
                row["engine"] = sid;
                row["note"] = note;
            }
        }
        foreach (var row in h3Rows.Where(r => r["level"] is null || (string?)r["faction"] == "special"))
            if (row["engine"] is null) row["note"] = "no Olden Era counterpart";

        // Olden Era units without an H3 number.
        var mapped = h3Rows.Select(r => (string?)r["engine"]).Where(s => s != null).ToHashSet();
        var rest = units.Where(u => !mapped.Contains(u.Id))
            .OrderBy(u => Array.IndexOf(FactionOrder, u.Faction) is int k && k >= 0 ? k : FactionOrder.Length)
            .ThenBy(u => u.Tier).ThenBy(u => u.Id, StringComparer.Ordinal).ToList();
        int next = Math.Max(FirstOldenEraNumber, kept.Values.DefaultIfEmpty(FirstOldenEraNumber - 1).Max() + 1);
        var extra = new List<JsonObject>();
        foreach (var u in rest)
        {
            int wog = kept.TryGetValue(u.Id, out int k) ? k : next++;
            extra.Add(new JsonObject
            {
                ["wog"] = wog, ["h3"] = null, ["faction"] = u.Faction, ["level"] = u.Tier,
                ["engine"] = u.Id, ["visual"] = null, ["note"] = "Olden Era unit without an H3 creature",
            });
        }

        var result = new JsonArray();
        foreach (var r in h3Rows.OrderBy(r => (int)r["wog"]!)) result.Add(r.DeepClone());
        foreach (var r in extra.OrderBy(r => (int)r["wog"]!)) result.Add(r);
        File.WriteAllText(jsonPath, result.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "\n");
        Console.WriteLine($"H3 creatures mapped: {h3Rows.Count(r => r["engine"] != null)} of {h3Rows.Count}; Olden Era-only units: {extra.Count}");
        return 0;
    }

    /// <summary>The tier's base unit: not an upgrade, and the one that has an upgrade (a faction may have extras).</summary>
    static Unit? BaseUnit(List<Unit> units, string faction, int tier)
    {
        var bases = units.Where(u => u.Faction == faction && u.Tier == tier && !u.Id.Contains("_upg")).ToList();
        return bases.FirstOrDefault(b => units.Any(u => u.Id == b.Id + "_upg")) ?? bases.FirstOrDefault();
    }

    static List<Unit> ReadUnits(string coreZip)
    {
        var list = new List<Unit>();
        var options = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
        using var zip = ZipFile.OpenRead(coreZip);
        foreach (var e in zip.Entries.Where(e => e.FullName.StartsWith("DB/units/units_logics/", StringComparison.Ordinal) && e.FullName.EndsWith(".json")))
        {
            using var s = e.Open();
            using var doc = JsonDocument.Parse(s, options);
            var root = doc.RootElement;
            IEnumerable<JsonElement> items = root.ValueKind == JsonValueKind.Array ? root.EnumerateArray()
                : root.TryGetProperty("array", out var a) ? a.EnumerateArray() : new[] { root };
            foreach (var u in items)
                if (u.TryGetProperty("id", out var id) && u.TryGetProperty("fraction", out var f) && u.TryGetProperty("tier", out var t))
                    list.Add(new Unit(id.GetString()!, f.GetString()!, t.GetInt32()));
        }
        return list.GroupBy(u => u.Id).Select(g => g.First()).ToList();
    }
}
