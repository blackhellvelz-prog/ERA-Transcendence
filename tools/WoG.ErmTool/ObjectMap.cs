using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>
/// idmap-objects: fills Compatibility/id-maps/object.json (Olden Era map object sid → H3 object type/subtype, Format
/// OB) from the map object configs of the user's Core.zip (DB/map/objects). Several Olden Era objects may stand for one
/// H3 type, so this map is one-way (sid → type/subtype); placing an H3 type uses its first sid. Rules
/// (Compatibility/id-maps/README.md):
///  * an equivalent is chosen by what the object does (DB/objects_logic), not by its name: mines by "resName",
///    windmill (weekly resources) → Windmill, watchtower (reveals the fog) → Redwood Observatory, …;
///  * "custom_X" (generator variants with other rewards) maps like X;
///  * cities by faction as for creatures (human → Castle, nature → Rampart, undead → Necropolis, dungeon → Dungeon,
///    demon → Inferno, unfrozen → Conflux), dwellings ("barracks_faction_tier") to that town's dwelling of the level
///    (Format CG), portals "portal_N…" to Two-Way Monolith N−1, artifacts to type 5 (subtype: artifact.json, else −1);
///  * every other interactive Olden Era object gets a type from 1000 up (sid order; numbers given before are kept);
///  * scenery (environment, animals, effects, blocks) is not a map object for ERM and is left out.
/// The file holds only sids and numbers (no game data), so it can be committed; rerun it after a game update.
/// </summary>
static class ObjectMap
{
    public const int FirstOldenEraType = 1000;

    static readonly string[] ObjectFiles = { "3_resources", "4_interactables", "6_artifacts", "7_spawns" };

    static readonly Dictionary<string, int> Resource = new()
    {
        ["wood"] = 0, ["mercury"] = 1, ["ore"] = 2, ["crystals"] = 4, ["gemstones"] = 5, ["gold"] = 6,
    };

    static readonly Dictionary<string, int> Town = new()
    {
        ["human"] = 0, ["nature"] = 1, ["demon"] = 3, ["undead"] = 4, ["dungeon"] = 5, ["unfrozen"] = 8,
    };

    /// <summary>Format CG: type 17 subtypes of the town dwellings, levels 1..7 (Castle, Rampart, Inferno, Necropolis, Dungeon, Conflux).</summary>
    static readonly Dictionary<int, int[]> Dwelling = new()
    {
        [0] = new[] { 56, 57, 25, 58, 35, 5, 8 },
        [1] = new[] { 6, 12, 15, 50, 45, 51, 24 },
        [3] = new[] { 29, 22, 27, 37, 40, 14, 10 },
        [4] = new[] { 54, 55, 48, 53, 52, 3, 4 },
        [5] = new[] { 46, 26, 2, 33, 34, 32, 41 },
        [8] = new[] { 59, 7, 47, 16, 13, 60, 61 },
    };

    /// <summary>Objects with an H3 equivalent by what they do (checked against DB/objects_logic).</summary>
    static readonly Dictionary<string, (int Type, int Sub, string Note)> ByFunction = new()
    {
        ["camp_fire"] = (12, 0, "resources + gold, once (Campfire)"),
        ["chest"] = (101, 0, "gold or experience, once (Treasure Chest)"),
        ["pandora_box"] = (6, 0, "Pandora's Box"),
        ["windmill"] = (112, 0, "resources, weekly (Windmill)"),
        ["watchtower"] = (58, 0, "reveals the map around (Redwood Observatory)"),
        ["mana_well"] = (49, 0, "restores mana (Magic Well)"),
        ["market"] = (99, 0, "resource trade (Trading Post)"),
        ["forge"] = (7, 0, "sells items (Black Market)"),
        ["sacrificial_shrine"] = (2, 0, "experience for artifacts and units (Altar of Sacrifice)"),
        ["heros_crypt"] = (84, 0, "guarded, gives items (Crypt)"),
        ["dragon_utopia"] = (25, 0, "Dragon Utopia"),
        ["stables"] = (94, 0, "hero buff, weekly (Stables)"),
        ["learning_stone"] = (100, 0, "experience once per hero (Learning Stone)"),
        ["tree_of_knowledge"] = (102, 0, "a level once per hero (Tree of Knowledge)"),
        ["university"] = (104, 0, "skills for gold (University)"),
        ["prison"] = (62, 0, "Prison"),
        ["tavern"] = (95, 0, "Tavern"),
        ["random-city"] = (77, 0, "Random Town (before the game starts)"),
        ["random-hero"] = (70, 0, "Random Hero (before the game starts)"),
        ["random-squad"] = (71, 0, "Random Monster (before the game starts)"),
        ["random-res"] = (76, 0, "Random Resource (before the game starts)"),
        ["random-item"] = (65, 0, "Random Artifact (before the game starts)"),
        ["random-hire"] = (216, 0, "Random Dwelling (before the game starts)"),
    };

    sealed record Obj(string Id, string Tag, string File);

    public static int Run(string coreZip, string jsonPath, string? artifactJson)
    {
        var objects = Read(coreZip);
        Console.WriteLine($"{objects.Count} interactive Olden Era map objects in {Path.GetFileName(coreZip)}");
        var artifacts = new Dictionary<string, int>();
        if (artifactJson != null && File.Exists(artifactJson))
            foreach (var r in JsonNode.Parse(File.ReadAllText(artifactJson))!.AsArray())
                if (r!["engine"] is JsonValue) artifacts[(string)r["engine"]!] = (int)r["wog"]!;

        var kept = new Dictionary<string, int>();
        if (File.Exists(jsonPath))
            foreach (var r in JsonNode.Parse(File.ReadAllText(jsonPath))!.AsArray())
                if ((int)r!["type"]! >= FirstOldenEraType) kept[(string)r["engine"]!] = (int)r["type"]!;
        int next = Math.Max(FirstOldenEraType, kept.Count == 0 ? 0 : kept.Values.Max() + 1);

        var rows = new JsonArray();
        foreach (var o in objects.OrderBy(o => o.Id, StringComparer.Ordinal))
        {
            var (type, sub, h3, note) = Classify(o, artifacts);
            if (type < 0)
            {
                if (!kept.TryGetValue(o.Id, out type)) type = next++;
                sub = 0;
                h3 = null;
                note = "Olden Era only";
            }
            rows.Add(new JsonObject
            {
                ["engine"] = o.Id, ["tag"] = o.Tag, ["type"] = type, ["subtype"] = sub, ["h3"] = h3, ["note"] = note,
            });
        }
        File.WriteAllText(jsonPath, rows.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
        int mapped = rows.Count(r => (int)r!["type"]! < FirstOldenEraType);
        Console.WriteLine($"{mapped} mapped to H3 types, {rows.Count - mapped} Olden Era only → {jsonPath}");
        return 0;
    }

    static (int Type, int Sub, string? H3, string Note) Classify(Obj o, Dictionary<string, int> artifacts)
    {
        string id = o.Id;
        string baseId = id.StartsWith("custom_") ? id["custom_".Length..] : id;
        string variant = baseId == id ? "" : " — generator variant";
        if (ByFunction.TryGetValue(baseId, out var f)) return (f.Type, f.Sub, H3Name(f.Type), f.Note + variant);
        if (baseId.StartsWith("mine_") && Resource.TryGetValue(baseId[5..], out int mr))
            return (53, mr, "Mine", $"produces {baseId[5..]} (Format MI {mr})");
        if (baseId.StartsWith("resource_") && Resource.TryGetValue(baseId[9..], out int rr))
            return (79, rr, "Resource", $"{baseId[9..]} (Format R {rr})");
        if (baseId.EndsWith("_city") && Town.TryGetValue(baseId[..^5], out int town))
            return (98, town, "Town", $"{baseId[..^5]} city (Format T {town})");
        if (baseId.StartsWith("barracks_"))
        {
            var p = baseId.Split('_');
            if (p.Length == 3 && Town.TryGetValue(p[1], out int t) && int.TryParse(p[2], out int tier) && tier is >= 1 and <= 7)
                return (17, Dwelling[t][tier - 1], "Creature Generator 1", $"{p[1]} tier {tier} dwelling (Format CG)");
        }
        if (baseId.StartsWith("portal_") && int.TryParse(baseId.Split('_')[1], out int portal) && portal is >= 1 and <= 8)
            return (45, portal - 1, "Monolith Two Way", $"portal group {portal}");
        if (o.Tag == "Artifact" && baseId.EndsWith("_artifact"))
            return artifacts.TryGetValue(baseId, out int art)
                ? (5, art, "Artifact", "artifact (artifact.json)")
                : (5, -1, "Artifact", "artifact not linked to an H3 number yet");
        return (-1, 0, null, "");
    }

    static string? H3Name(int type) => type switch
    {
        2 => "Altar of Sacrifice", 6 => "Pandora's Box", 7 => "Black Market", 12 => "Campfire", 25 => "Dragon Utopia",
        45 => "Monolith Two Way", 49 => "Magic Well", 58 => "Redwood Observatory", 62 => "Prison", 65 => "Random Artifact",
        70 => "Random Hero", 71 => "Random Monster", 76 => "Random Resource", 77 => "Random Town", 84 => "Crypt",
        94 => "Stables", 95 => "Tavern", 99 => "Trading Post", 100 => "Learning Stone", 101 => "Treasure Chest",
        102 => "Tree of Knowledge", 104 => "University", 112 => "Windmill", 216 => "Random Dwelling",
        _ => null,
    };

    static List<Obj> Read(string coreZip)
    {
        using var zip = ZipFile.OpenRead(coreZip);
        var list = new List<Obj>();
        foreach (var file in ObjectFiles)
        {
            var e = zip.GetEntry($"DB/map/objects/{file}.json") ?? throw new FileNotFoundException("DB/map/objects/" + file + ".json");
            using var s = e.Open();
            var root = JsonNode.Parse(s)!;
            var arr = root is JsonObject ? root["array"]!.AsArray() : root.AsArray();
            foreach (var o in arr)
            {
                string? id = (string?)o!["id"];
                if (id == null) continue;
                if (file != "7_spawns" && o["isInteractable"] is JsonValue v && !(bool)v) continue;
                list.Add(new Obj(id, (string?)o["tag"] ?? "", file));
            }
        }
        return list.GroupBy(o => o.Id).Select(g => g.First()).ToList();
    }
}
