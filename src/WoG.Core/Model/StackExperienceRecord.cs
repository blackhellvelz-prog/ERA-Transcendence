namespace WoG.Core.Model;

/// <summary>Where an experienced stack lives (CE_* constants of crexpo.h).</summary>
public enum StackLocationKind
{
    Hero = 1,
    Map = 2,
    Town = 3,
    Mine = 4,
    Garrison = 5,
}

/// <summary>Key of a stack-experience record: location + slot (hero index or x/y/l).</summary>
public readonly record struct StackLocation(StackLocationKind Kind, int A, int B, int C, int Slot)
{
    public static StackLocation Hero(int hero, int slot) => new(StackLocationKind.Hero, hero, 0, 0, slot);
    public static StackLocation At(StackLocationKind kind, MapPos p, int slot) => new(kind, p.X, p.Y, p.L, slot);
    public override string ToString() => Kind == StackLocationKind.Hero ? $"hero{A}:{Slot}" : $"{Kind}@{A}/{B}/{C}:{Slot}";
}

/// <summary>One CrExpo record: experience per creature and the stack it was computed for.</summary>
public sealed class StackExperienceRecord
{
    public StackLocation Location { get; set; }
    /// <summary>Experience per creature.</summary>
    public int Expo { get; set; }
    public int Num { get; set; }
    public int MType { get; set; } = -1;
    public bool HasArt { get; set; }
    public int Art { get; set; } = WoGLimits.StackArtifact;
    public int SubArt { get; set; }
    /// <summary>Additional copies of the stack artifact (0..3).</summary>
    public int ArtCopies { get; set; }
}
