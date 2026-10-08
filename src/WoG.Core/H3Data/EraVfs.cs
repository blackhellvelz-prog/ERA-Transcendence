using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace WoG.Core.H3Data;

/// <summary>
/// The resource files an ERA installation gives the game (Era's VFS over Mods/list.txt + the LOD chain), read only:
/// for a name, the mods are searched from the highest priority down — a loose file in the mod's Data folder, then the
/// mod's .pac/.lod archives — and then the game's own Data folder and its LODs (h3bitmap, then the others). That is
/// why a translation mod (WoG Rus) wins over the mod it translates (WoG), and both over the original game.
/// </summary>
public sealed class EraVfs
{
    readonly List<(string Folder, List<LodArchive> Archives)> layers = new();

    /// <param name="gameFolder">The ERA installation (with Data\h3bitmap.lod); null when there is none.</param>
    /// <param name="modFoldersHighestFirst">Active mods in Era's priority order, highest first.</param>
    public EraVfs(string? gameFolder, IEnumerable<string> modFoldersHighestFirst)
    {
        GameFolder = gameFolder;
        foreach (var mod in modFoldersHighestFirst) AddLayer(DataFolder(mod), baseGame: false);
        if (gameFolder != null) AddLayer(DataFolder(gameFolder), baseGame: true);
    }

    public bool IsEmpty => layers.Count == 0;

    /// <summary>The ERA installation (its executables are read for data the text tables do not have); null when none.</summary>
    public string? GameFolder { get; }

    static string? DataFolder(string root)
    {
        if (!Directory.Exists(root)) return null;
        return Directory.GetDirectories(root).FirstOrDefault(d => string.Equals(System.IO.Path.GetFileName(d), "Data", StringComparison.OrdinalIgnoreCase));
    }

    void AddLayer(string? data, bool baseGame)
    {
        if (data == null) return;
        var archives = Directory.GetFiles(data)
            .Where(f => f.EndsWith(".pac", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".lod", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => baseGame ? BaseRank(f) : 0).ThenBy(f => System.IO.Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
            .Select(LodArchive.Open).Where(a => a != null).Select(a => a!).ToList();
        layers.Add((data, archives));
    }

    /// <summary>The SoD archives before the older Armageddon's Blade ones (h3bitmap before h3ab_bmp).</summary>
    static int BaseRank(string file) => System.IO.Path.GetFileName(file).ToLowerInvariant() switch
    {
        "h3bitmap.lod" => 0,
        "h3sprite.lod" => 1,
        _ => 2,
    };

    /// <summary>The bytes of a resource, or null; <paramref name="from"/> tells which file it came from.</summary>
    public byte[]? Read(string name, out string? from)
    {
        foreach (var (folder, archives) in layers)
        {
            var loose = Directory.GetFiles(folder).FirstOrDefault(f => string.Equals(System.IO.Path.GetFileName(f), name, StringComparison.OrdinalIgnoreCase));
            if (loose != null) { from = loose; return File.ReadAllBytes(loose); }
            foreach (var a in archives)
                if (a.Contains(name)) { from = a.Path + "|" + name; return a.Read(name); }
        }
        from = null;
        return null;
    }

    public byte[]? Read(string name) => Read(name, out _);
}
