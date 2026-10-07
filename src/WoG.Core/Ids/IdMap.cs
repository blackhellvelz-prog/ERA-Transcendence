using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace WoG.Core.Ids;

/// <summary>
/// Bidirectional mapping between WoG/H3 numeric ids (what ERM scripts use) and engine ids (Olden Era
/// string sids). Domains: "hero", "creature", "artifact", "spell", "skill", "resource", "town", "building".
/// An unmapped id is reported as unsupported by the adapter — never silently substituted.
/// </summary>
public sealed class IdMap
{
    public Dictionary<string, Dictionary<int, string>> Forward { get; set; } = new();

    readonly Dictionary<string, Dictionary<string, int>> reverse = new();
    // Domains loaded from Compatibility/id-maps files (not saved: a field, and the save serializer skips fields).
    readonly HashSet<string> fileDomains = new();
    // The reverse table is built on first use: Forward may be filled by deserialization without Set.
    bool reverseBuilt;

    public void Set(string domain, int wogId, string engineId)
    {
        if (!Forward.TryGetValue(domain, out var f)) Forward[domain] = f = new();
        f[wogId] = engineId;
        if (!reverseBuilt) return;
        if (!reverse.TryGetValue(domain, out var r)) reverse[domain] = r = new();
        r[engineId] = wogId;
    }

    public bool TryGetEngine(string domain, int wogId, out string engineId)
    {
        engineId = "";
        return Forward.TryGetValue(domain, out var f) && f.TryGetValue(wogId, out engineId!);
    }

    public bool TryGetWoG(string domain, string engineId, out int wogId)
    {
        wogId = -1;
        if (!reverseBuilt) RebuildReverse();
        return reverse.TryGetValue(domain, out var r) && r.TryGetValue(engineId, out wogId);
    }

    public void RebuildReverse()
    {
        reverse.Clear();
        foreach (var (domain, f) in Forward)
        {
            var r = reverse[domain] = new Dictionary<string, int>();
            foreach (var (k, v) in f) r[v] = k;
        }
        reverseBuilt = true;
    }

    sealed class Entry
    {
        public int wog { get; set; }
        public string? engine { get; set; }
    }

    /// <summary>Loads Compatibility/id-maps/&lt;domain&gt;.json: [{"wog":0,"engine":"sid"|null,…}].</summary>
    public void LoadDomainFile(string domain, string path)
    {
        var list = JsonSerializer.Deserialize<List<Entry>>(File.ReadAllText(path)) ?? new();
        foreach (var e in list)
            if (!string.IsNullOrEmpty(e.engine)) Set(domain, e.wog, e.engine!);
        fileDomains.Add(domain);
    }

    /// <summary>
    /// After loading a saved game: the domains <paramref name="current"/> loaded from files replace the copies the
    /// save carries (the files may be newer than the save); numbers given during the game (other domains) stay.
    /// </summary>
    public void AdoptFileDomains(IdMap current)
    {
        foreach (var domain in current.fileDomains)
        {
            Forward[domain] = current.Forward.TryGetValue(domain, out var f) ? new Dictionary<int, string>(f) : new();
            fileDomains.Add(domain);
        }
        reverseBuilt = false;
    }
}
