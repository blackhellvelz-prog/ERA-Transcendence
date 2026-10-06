using System;
using System.Collections.Generic;
using System.Linq;

namespace WoG.Core.Visual;

/// <summary>
/// Where a visual comes from, in the project's mandatory priority order (lower = preferred).
/// Gameplay code never sees these — it only holds <see cref="VisualRef"/> keys.
/// </summary>
public enum VisualSource
{
    OldenEraAsset = 0,
    OldenEraRecolor = 1,
    OldenEraMaterial = 2,
    OldenEraTexture = 3,
    OldenEraVfx = 4,
    ImportedWoGAsset = 5,
    Placeholder = 6,
    NewAsset = 7,
}

/// <summary>Logical visual key, e.g. "creature:wog.commander.castle", "icon:artifact:146".</summary>
public readonly record struct VisualRef(string Key)
{
    public override string ToString() => Key;
}

/// <summary>One way to realise a visual.</summary>
public sealed class VisualCandidate
{
    public VisualSource Source { get; init; }
    /// <summary>Engine asset id / file name / placeholder id.</summary>
    public string Asset { get; init; } = "";
    /// <summary>Recolour (hue shift degrees, saturation, value) or material/texture parameters.</summary>
    public IReadOnlyDictionary<string, string> Parameters { get; init; } = new Dictionary<string, string>();
}

public sealed class ResolvedVisual
{
    public VisualRef Ref { get; init; }
    public VisualCandidate Candidate { get; init; } = new();
    public bool IsPlaceholder => Candidate.Source == VisualSource.Placeholder;
}

/// <summary>Answers "does this asset exist in the running engine?".</summary>
public interface IVisualAssetProbe
{
    bool IsAvailable(VisualCandidate candidate);
}

public interface IVisualResolver
{
    ResolvedVisual Resolve(VisualRef r);
}

/// <summary>
/// Picks, for each logical visual, the best available candidate by <see cref="VisualSource"/> priority.
/// Always succeeds: when nothing is available a placeholder is returned, so missing art can never block
/// gameplay.
/// </summary>
public sealed class VisualResolver : IVisualResolver
{
    readonly Dictionary<string, List<VisualCandidate>> catalog = new(StringComparer.Ordinal);
    readonly IVisualAssetProbe probe;

    public VisualResolver(IVisualAssetProbe probe) { this.probe = probe; }

    public void Register(string key, VisualCandidate candidate)
    {
        if (!catalog.TryGetValue(key, out var list)) catalog[key] = list = new();
        list.Add(candidate);
    }

    public ResolvedVisual Resolve(VisualRef r)
    {
        if (catalog.TryGetValue(r.Key, out var list))
        {
            foreach (var c in list.OrderBy(c => (int)c.Source))
                if (c.Source == VisualSource.Placeholder || probe.IsAvailable(c))
                    return new ResolvedVisual { Ref = r, Candidate = c };
        }
        return new ResolvedVisual
        {
            Ref = r,
            Candidate = new VisualCandidate { Source = VisualSource.Placeholder, Asset = "placeholder:" + r.Key },
        };
    }
}
