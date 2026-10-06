using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace WoG.OldenEra.Data;

/// <summary>
/// Reads JSON definitions from the user's Core.zip and builds the WoG overlay zip that is placed next
/// to it (OldenEra_ReverseEngineering/02_Data_Layer_CoreZip.md). Uses the community-verified rule
/// "clone a real definition, mint a new id, ship it"; nothing from Core.zip is copied except the cloned
/// entries themselves, generated on the user's machine.
/// </summary>
public sealed class CoreZipReader : IDisposable
{
    readonly ZipArchive zip;

    public CoreZipReader(string path) : this(File.OpenRead(path)) { }
    public CoreZipReader(Stream s) { zip = new ZipArchive(s, ZipArchiveMode.Read); }

    public IEnumerable<string> FilesUnder(string prefix) =>
        zip.Entries.Where(e => e.FullName.StartsWith(prefix, StringComparison.Ordinal) && e.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .Select(e => e.FullName);

    public JsonNode? ReadJson(string entry)
    {
        var e = zip.GetEntry(entry);
        if (e == null) return null;
        using var r = new StreamReader(e.Open(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return JsonNode.Parse(r.ReadToEnd(), documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
    }

    /// <summary>Every object with an "id" under a DB folder; files may hold an array or a single object.</summary>
    public IEnumerable<(string file, JsonObject entry)> Entries(string prefix)
    {
        foreach (var f in FilesUnder(prefix))
        {
            var node = ReadJson(f);
            if (node is JsonArray arr)
            {
                foreach (var x in arr) if (x is JsonObject o && o["id"] != null) yield return (f, o);
            }
            else if (node is JsonObject o)
            {
                if (o["id"] != null) yield return (f, o);
                else if (o["array"] is JsonArray inner)
                    foreach (var x in inner) if (x is JsonObject io && io["id"] != null) yield return (f, io);
            }
        }
    }

    public JsonObject? Find(string prefix, string id) =>
        Entries(prefix).Select(e => e.entry).FirstOrDefault(e => (string?)e["id"] == id);

    public void Dispose() => zip.Dispose();
}

/// <summary>A cloned definition destined for the overlay.</summary>
public sealed record OverlayEntry(string Path, JsonNode Content);

/// <summary>Builds wog_core.zip: cloned units for commanders/WoG creatures, rank buffs, localisation.</summary>
public sealed class OverlayBuilder
{
    readonly List<OverlayEntry> entries = new();
    readonly Dictionary<string, string> texts = new(StringComparer.Ordinal);

    public IReadOnlyList<OverlayEntry> Entries => entries;

    /// <summary>
    /// Clones a unit definition under a new id with overridden stats. Stat keys are the ones documented
    /// in DB/units/units_logics (hp, offence, defence, damageMin, damageMax, initiative, speed, …).
    /// </summary>
    public JsonObject CloneUnit(CoreZipReader core, string sourceId, string newId, IDictionary<string, int> stats, string? displayName = null)
    {
        var (file, src) = core.Entries("DB/units/units_logics/").FirstOrDefault(e => (string?)e.entry["id"] == sourceId);
        if (src == null) throw new InvalidOperationException($"unit {sourceId} not found in Core.zip");
        var clone = (JsonObject)JsonNode.Parse(src.ToJsonString())!; // DeepClone is .NET 8+, the plugin runs on .NET 6
        clone["id"] = newId;
        if (clone["stats"] is not JsonObject st) clone["stats"] = st = new JsonObject();
        foreach (var (k, v) in stats) st[k] = v;
        // The clone is placed in the source's own folder: for object logic the community verified that a
        // clone must live in its source family's sub-folder; for units this is assumed to be equally safe.
        string dir = file.Substring(0, file.LastIndexOf('/') + 1);
        entries.Add(new OverlayEntry($"{dir}wog_{newId}.json", new JsonArray(clone)));
        if (displayName != null) texts[newId + "_name"] = displayName;
        return clone;
    }

    /// <summary>Creates a stat buff (data.stats) — used for stack-experience ranks and commander bonuses.</summary>
    public JsonObject StatBuff(string id, IDictionary<string, double> stats, string? name = null)
    {
        var data = new JsonObject();
        foreach (var (k, v) in stats) data[k] = v;
        var buff = new JsonObject
        {
            ["id"] = id,
            ["data"] = new JsonObject { ["stats"] = data },
            ["duration"] = new JsonObject { ["infinite"] = true },
        };
        if (name != null) { buff["name_"] = id + "_name"; texts[id + "_name"] = name; }
        entries.Add(new OverlayEntry($"DB/buffs/wog/{id}.json", new JsonArray(buff)));
        return buff;
    }

    /// <summary>
    /// Writes the overlay. Entries are STORED (uncompressed) and localisation is
    /// Lang/english/texts/wog.json = {"tokens":[{"sid","text"}]} with a UTF-8 BOM — both exactly as the
    /// community tool whose zips the game is reported to load (O1 zip-export.ts).
    /// </summary>
    public void Write(string zipPath)
    {
        using var fs = File.Create(zipPath);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);
        var opts = new JsonSerializerOptions { WriteIndented = true };
        foreach (var e in entries)
        {
            var z = zip.CreateEntry(e.Path, CompressionLevel.NoCompression);
            using var w = new StreamWriter(z.Open(), new UTF8Encoding(false));
            w.Write(e.Content.ToJsonString(opts));
        }
        if (texts.Count > 0)
        {
            var z = zip.CreateEntry("Lang/english/texts/wog.json", CompressionLevel.NoCompression);
            using var w = new StreamWriter(z.Open(), new UTF8Encoding(true));
            var tokens = new JsonArray();
            foreach (var (k, v) in texts) tokens.Add(new JsonObject { ["sid"] = k, ["text"] = v });
            w.Write(new JsonObject { ["tokens"] = tokens }.ToJsonString(opts));
        }
    }
}
