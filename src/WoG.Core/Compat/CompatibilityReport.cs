using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WoG.Core.Compat;

/// <summary>Status vocabulary of the project brief.</summary>
public enum CompatLevel
{
    FullySupported,
    PartiallySupported,
    Emulated,
    Workaround,
    Unsupported,
}

/// <summary>
/// Runtime record of every place where the target engine could not do what WoG asked. Aggregated per
/// area/item so that a play session produces the evidence for the compatibility matrix.
/// </summary>
public sealed class CompatibilityReport
{
    public sealed class Entry
    {
        public string Area { get; init; } = "";
        public string Item { get; init; } = "";
        public string Reason { get; init; } = "";
        public int Count { get; set; }
    }

    readonly Dictionary<(string, string, string), Entry> entries = new();

    public void Unsupported(string area, string item, string reason)
    {
        var key = (area, item, reason);
        if (!entries.TryGetValue(key, out var e))
            entries[key] = e = new Entry { Area = area, Item = item, Reason = reason };
        e.Count++;
    }

    public IReadOnlyCollection<Entry> Entries => entries.Values;

    public string ToMarkdown()
    {
        var sb = new StringBuilder("| Area | Item | Reason | Count |\n|---|---|---|---|\n");
        foreach (var e in entries.Values.OrderBy(e => e.Area).ThenBy(e => e.Item))
            sb.Append($"| {e.Area} | {e.Item} | {e.Reason} | {e.Count} |\n");
        return sb.ToString();
    }
}
