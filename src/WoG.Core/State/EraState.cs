using System;
using System.Collections.Generic;

namespace WoG.Core.State;

/// <summary>Associative ERM variable (AdvErm.TAssocVar): i^name^ uses Int, s^name^ uses Str.</summary>
public sealed class EraAssocVar
{
    public int Int { get; set; }
    public string Str { get; set; } = "";
}

/// <summary>TSlotStorageType of SN:M dynamic arrays.</summary>
public enum EraSlotStorage
{
    /// <summary>Freed when the trigger that created it ends.</summary>
    TriggerLocal = -1,
    /// <summary>Lives until deleted; saved without its items (they read as 0 / "" after loading).</summary>
    Temp = 0,
    /// <summary>Saved with its items.</summary>
    Stored = 1,
}

/// <summary>SN:M dynamic array (AdvErm.TSlot).</summary>
public sealed class EraSlot
{
    public bool IsString { get; set; }
    public EraSlotStorage Storage { get; set; }
    public List<int> Ints { get; set; } = new();
    public List<string> Strs { get; set; } = new();

    public int Count => IsString ? Strs.Count : Ints.Count;

    /// <summary>SetSlotItemsCount: growing adds zeros / empty strings, shrinking drops the tail.</summary>
    public void Resize(int n)
    {
        if (n < 0) throw new ArgumentOutOfRangeException(nameof(n));
        if (IsString)
        {
            if (n < Strs.Count) Strs.RemoveRange(n, Strs.Count - n);
            while (Strs.Count < n) Strs.Add("");
        }
        else
        {
            if (n < Ints.Count) Ints.RemoveRange(n, Ints.Count - n);
            while (Ints.Count < n) Ints.Add(0);
        }
    }
}

/// <summary>
/// Everything Era adds to the saved ERM state: named functions and constants (Erm.pas FuncNames,
/// FuncAutoId, GlobalConsts), associative variables (SN:W, i^^, s^^), SN:M arrays and ERT strings.
/// </summary>
public sealed class EraState
{
    public const int InitialFuncAutoId = 95000;
    /// <summary>AUTO_ALLOC_SLOT - 1: first id SN:M -1/... hands out (then -3, -4, ...).</summary>
    public const int FirstAutoSlot = -2;

    public Dictionary<string, int> Functions { get; set; } = new(StringComparer.Ordinal);
    public int FuncAutoId { get; set; } = InitialFuncAutoId;
    public Dictionary<string, int> Constants { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, EraAssocVar> Assoc { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<int, EraSlot> Slots { get; set; } = new();
    public int FreeSlotN { get; set; } = FirstAutoSlot;
    /// <summary>ERT strings (z &gt; 1000) loaded from the .ert files that accompany scripts.</summary>
    public Dictionary<int, string> Ert { get; set; } = new();

    public EraAssocVar GetOrCreateAssoc(string name)
    {
        if (!Assoc.TryGetValue(name, out var v))
        {
            v = new EraAssocVar();
            Assoc[name] = v;
        }
        return v;
    }

    /// <summary>AdvErm.ResetMemory (new game): slots and associative variables are cleared.</summary>
    public void ResetMemory()
    {
        Slots.Clear();
        FreeSlotN = FirstAutoSlot;
        Assoc.Clear();
    }

    /// <summary>
    /// Era's save/load keeps every slot but only the items of SLOT_STORED ones, and drops associative
    /// variables that are 0 / "" (LoadAssocMem). Applied after loading.
    /// </summary>
    public void NormalizeAfterLoad()
    {
        foreach (var s in Slots.Values)
        {
            if (s.Storage == EraSlotStorage.Stored) continue;
            int n = s.Count;
            s.Ints.Clear();
            s.Strs.Clear();
            s.Resize(n);
        }
        var empty = new List<string>();
        foreach (var kv in Assoc) if (kv.Value.Int == 0 && kv.Value.Str.Length == 0) empty.Add(kv.Key);
        foreach (var k in empty) Assoc.Remove(k);
    }
}
