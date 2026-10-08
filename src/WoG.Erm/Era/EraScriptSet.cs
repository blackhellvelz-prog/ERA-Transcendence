using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace WoG.Erm.Era;

/// <summary>One script as Era would load it: name used for dedup/save ("lib\9999 era - stdlib.erm"), source path.</summary>
public sealed record EraScriptFile(string Name, string Path, int Priority, EraScriptKind Kind);

public enum EraScriptKind { Lib, Map, Global, LibEnd }

/// <summary>
/// The script set Era loads for a new game (TScriptMan.LoadScriptsFromDisk): lib scripts (Data\s\lib),
/// map scripts, global scripts (Data\s) and end-lib scripts (Data\s\lib_end). Era sees mods through its
/// virtual file system: a file present in several mods comes from the highest-priority mod. Inside each
/// folder files are ordered by GetOrderedPrioritizedFileList: numeric prefix ("906 name.erm") descending,
/// then by name (AnsiCompareText).
/// </summary>
public static class EraScriptSet
{
    public const string ErmLibDirName = "lib";
    public const string ErmEndLibDirName = "lib_end";

    /// <summary>The file naming the only global scripts to load (TScriptMan.LoadFixedScriptSet).</summary>
    public const string FixedSetFile = "load only these scripts.txt";

    /// <param name="modRoots">Mod folders, highest priority first (each contains Data\s).</param>
    /// <param name="mapScriptsDir">Optional Maps\&lt;map&gt;\Data\s folder.</param>
    /// <param name="globalScripts">False: no global scripts (Data\s), as ERA with WoG option 5 = 0; lib, map and
    /// end-lib scripts load anyway.</param>
    public static List<EraScriptFile> Collect(IReadOnlyList<string> modRoots, string? mapScriptsDir = null, bool globalScripts = true)
    {
        var fixedSet = FixedScriptSet(modRoots);
        var all = new List<EraScriptFile>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddDir(string sub, string prefix, EraScriptKind kind, IEnumerable<string> roots)
        {
            var dirs = roots.Select(r => Path.Combine(r, sub)).Where(Directory.Exists).ToList();
            foreach (var f in OrderedPrioritizedFileList(dirs))
            {
                if (kind == EraScriptKind.Global && fixedSet != null && !fixedSet.Contains(f.FileName)) continue;
                // TScriptMan.LoadScript: an already loaded script name is skipped
                string name = prefix + f.FileName;
                if (!names.Add(name)) continue;
                all.Add(new EraScriptFile(name, f.Path, f.Priority, kind));
            }
        }

        var dataS = Path.Combine("Data", "s");
        AddDir(Path.Combine(dataS, ErmLibDirName), ErmLibDirName + "\\", EraScriptKind.Lib, modRoots);
        if (mapScriptsDir != null && Directory.Exists(mapScriptsDir))
            AddDir("", Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(mapScriptsDir)!)!) + "\\", EraScriptKind.Map, new[] { mapScriptsDir });
        if (globalScripts) AddDir(dataS, "", EraScriptKind.Global, modRoots);
        AddDir(Path.Combine(dataS, ErmEndLibDirName), ErmEndLibDirName + "\\", EraScriptKind.LibEnd, modRoots);
        return all;
    }

    /// <summary>
    /// The fixed global script set (TScriptMan.LoadFixedScriptSet): the names in Data\s\load only these scripts.txt
    /// (one a line) as Era's virtual file system finds it — the highest-priority mod that has it — or null.
    /// </summary>
    public static HashSet<string>? FixedScriptSet(IReadOnlyList<string> modRoots)
    {
        foreach (var root in modRoots)
        {
            string path = Path.Combine(root, "Data", "s", FixedSetFile);
            if (!File.Exists(path)) continue;
            return new HashSet<string>(
                EraText.Decode(File.ReadAllBytes(path)).Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0),
                StringComparer.OrdinalIgnoreCase);
        }
        return null;
    }

    public sealed record PrioritizedFile(string FileName, string Path, int Priority);

    /// <summary>
    /// GetOrderedPrioritizedFileList: *.erm of all locations (first location wins for a name), sorted by
    /// name, then a stable insertion sort by priority (higher first). The priority is the first
    /// space-separated token when it is an integer, else 0.
    /// </summary>
    public static List<PrioritizedFile> OrderedPrioritizedFileList(IEnumerable<string> dirs)
    {
        var byName = new Dictionary<string, PrioritizedFile>(StringComparer.OrdinalIgnoreCase);
        foreach (var dir in dirs)
        {
            foreach (var path in Directory.EnumerateFiles(dir, "*.erm", SearchOption.TopDirectoryOnly))
            {
                string fileName = Path.GetFileName(path);
                if (byName.ContainsKey(fileName)) continue;
                byName[fileName] = new PrioritizedFile(fileName, path, PriorityOf(fileName));
            }
        }
        var list = byName.Values.ToList();
        list.Sort((a, b) => AnsiCompareText(a.FileName, b.FileName));
        // insertion sort by priority, exactly as Era does it (stable)
        for (int i = 1; i < list.Count; i++)
        {
            var item = list[i];
            int j = i - 1;
            while (j >= 0 && item.Priority > list[j].Priority) j--;
            list.RemoveAt(i);
            list.Insert(j + 1, item);
        }
        return list;
    }

    public static int PriorityOf(string fileName)
    {
        int sp = fileName.IndexOf(' ');
        if (sp < 0) return 0;
        return int.TryParse(fileName.AsSpan(0, sp), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int p) ? p : 0;
    }

    /// <summary>
    /// Approximation of Windows AnsiCompareText (CompareString, NORM_IGNORECASE, "word sort"): hyphens and
    /// apostrophes are ignored at the first level and only break ties.
    /// </summary>
    public static int AnsiCompareText(string a, string b)
    {
        static string Strip(string s) => new string(s.Where(c => c != '-' && c != '\'').ToArray());
        int r = string.Compare(Strip(a), Strip(b), StringComparison.OrdinalIgnoreCase);
        if (r != 0) return r;
        return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
    }
}
