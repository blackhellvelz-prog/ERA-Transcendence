using System;
using System.Collections.Generic;
using System.Linq;
using WoG.Core.Model;
using WoG.Core.Services;

namespace WoG.Core.H3Data;

/// <summary>
/// The artifact setup table (ArtSetUp) and the combination table (CArtSetup) as WoG's UN:A sees them: the H3 tables
/// of the ERA installation plus the changes ERM made in this game. Changes live in the WoG state (copy on write), so
/// they are saved with it and a new game starts from the installation's tables again.
/// </summary>
public static class ArtifactTable
{
    /// <summary>CArtSetup has 32 entries; the first 12 are the SoD combinations.</summary>
    public const int ComboSlots = 32;
    /// <summary>Only artifacts below 160 may be parts of a combination (BuildUpCombo).</summary>
    public const int MaxComboPart = 160;
    /// <summary>A combination has 1..14 parts (BuildUpCombo).</summary>
    public const int MaxComboParts = 14;

    public static bool HasArtifactTable(this IWoGServices s) => s.H3.Artifacts.Count > 0;

    /// <summary>ARTNUM: the size of the installation's table, or WoG 3.58's 171 when there is none.</summary>
    public static int ArtifactCount(this IWoGServices s) => s.H3.Artifacts.Count > 0 ? s.H3.Artifacts.Count : WoGLimits.ArtifactCount;

    /// <summary>An artifact type, null when the id is not in the table.</summary>
    public static WoGArtifact? Artifact(this IWoGServices s, int id) =>
        s.State.ArtifactOverrides.TryGetValue(id, out var a) ? a
        : id >= 0 && id < s.H3.Artifacts.Count ? s.H3.Artifacts[id] : null;

    /// <summary>The game's own copy of an artifact type, to change it (the installation's table stays as it is).</summary>
    public static WoGArtifact EditArtifact(this IWoGServices s, int id)
    {
        if (s.State.ArtifactOverrides.TryGetValue(id, out var a)) return a;
        if (id < 0 || id >= s.H3.Artifacts.Count) throw new ArgumentOutOfRangeException(nameof(id));
        a = s.H3.Artifacts[id].Clone();
        s.State.ArtifactOverrides[id] = a;
        return a;
    }

    /// <summary>A combination table entry: [combo artifact, parts…]; combo artifact 0 means an empty entry.</summary>
    public static int[] Combo(this IWoGServices s, int index)
    {
        if (s.State.ComboOverrides.TryGetValue(index, out var c)) return c;
        if (index >= 0 && index < H3Tables.Combos.Length && H3Tables.FirstCombo + index < s.H3.Artifacts.Count)
            return new[] { H3Tables.FirstCombo + index }.Concat(H3Tables.Combos[index]).ToArray();
        return new[] { 0 };
    }

    /// <summary>
    /// BuildUpCombo (common.cpp): checks the arguments, clears the entry (CleanUpCombo: the SuperN/PartOfSuperN marks
    /// of that index go back to -1) and, unless the combo artifact is 0, fills it and marks the artifacts.
    /// Returns WoG's result: false for wrong arguments and for a removal.
    /// </summary>
    public static bool BuildUpCombo(this IWoGServices s, int comboArt, int index, IReadOnlyList<int> parts)
    {
        if (index < 0 || index >= ComboSlots) return false;
        if (comboArt < 0 || comboArt >= s.ArtifactCount()) return false;
        if (parts.Count < 1 || parts.Count > MaxComboParts) return false;
        if (parts.Any(p => p < 0 || p >= MaxComboPart)) return false;
        // CleanUpCombo
        for (int i = 0; i < s.H3.Artifacts.Count; i++)
        {
            var a = s.Artifact(i)!;
            if (a.PartOfSuperN == index) s.EditArtifact(i).PartOfSuperN = -1;
            if (a.SuperN == index) s.EditArtifact(i).SuperN = -1;
        }
        s.State.ComboOverrides[index] = new[] { 0 };
        if (comboArt == 0) return false;
        s.State.ComboOverrides[index] = new[] { comboArt }.Concat(parts).ToArray();
        if (comboArt < s.H3.Artifacts.Count) s.EditArtifact(comboArt).SuperN = index;
        foreach (int p in parts)
            if (p < s.H3.Artifacts.Count) s.EditArtifact(p).PartOfSuperN = index;
        return true;
    }
}
