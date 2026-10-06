using System.Collections.Generic;
using WoG.Core.State;

namespace WoG.Erm.Syntax;

/// <summary>Language dialect. 3.58 is the reference; 3.59 additions are opt-in.</summary>
public enum ErmDialect
{
    Wog358,
    Wog359Alpha,
}

/// <summary>Comparison codes exactly as GetCmpCode returns them (VarNum.Check 2..7).</summary>
public enum ErmCompare
{
    None = 0,
    Eq = 2,
    Ne = 3,
    Gt = 4,
    Lt = 5,
    Ge = 6,
    Le = 7,
}

public enum ErmParamMode
{
    /// <summary>Plain value (with <see cref="ErmParam.Add"/> = 'd' prefix it is a delta).</summary>
    Set,
    /// <summary>'?' — write the current value into the variable.</summary>
    Get,
    /// <summary>&lt; = &gt; — compare the current value, result into flag 1.</summary>
    Check,
}

/// <summary>Reference to a variable, possibly indexed by another variable (vy5 = v[y5]).</summary>
public sealed class ErmVarRef
{
    public ErmVarKind Kind { get; set; }
    /// <summary>Literal index, or index of the indexing variable when <see cref="IndexKind"/> != None.</summary>
    public int Index { get; set; }
    public ErmVarKind IndexKind { get; set; } = ErmVarKind.None;

    public override string ToString() =>
        IndexKind == ErmVarKind.None ? $"{Letter(Kind)}{Index}" : $"{Letter(Kind)}{Letter(IndexKind)}{Index}";

    internal static string Letter(ErmVarKind k) => k switch
    {
        ErmVarKind.Flag => "flag",
        ErmVarKind.Quick => "q",
        ErmVarKind.V => "v",
        ErmVarKind.W => "w",
        ErmVarKind.X => "x",
        ErmVarKind.Y => "y",
        ErmVarKind.Z => "z",
        ErmVarKind.E => "e",
        _ => "?",
    };
}

/// <summary>One parameter of a trigger, selector or command (the VarNum of erm.cpp).</summary>
public sealed class ErmParam
{
    public ErmParamMode Mode { get; set; }
    public ErmCompare Compare { get; set; }
    /// <summary>'d' prefix.</summary>
    public bool Add { get; set; }
    /// <summary>Variable reference, or null for a constant.</summary>
    public ErmVarRef? Var { get; set; }
    /// <summary>Constant value (when <see cref="Var"/> is null).</summary>
    public int Number { get; set; }
    /// <summary>'c' in the number: add the current absolute day at evaluation.</summary>
    public bool DayRelative { get; set; }
    /// <summary>Unresolved $macro$ (resolved at execution, after MC:S instructions ran).</summary>
    public string? Macro { get; set; }
    /// <summary>No characters at all (e.g. the empty parameter in front of ^text^).</summary>
    public bool Empty { get; set; }

    public override string ToString()
    {
        string p = Mode == ErmParamMode.Get ? "?" : Mode == ErmParamMode.Check ? Compare.ToString() + ":" : "";
        if (Add) p += "d";
        if (Macro != null) return p + "$" + Macro + "$";
        if (Var != null) return p + Var;
        return Empty ? p + "∅" : p + Number + (DayRelative ? "c" : "");
    }
}

/// <summary>One item of a &amp;/| condition list.</summary>
public sealed class ErmCondItem
{
    /// <summary>Flag test when not null: flag number (1..1000).</summary>
    public int? Flag { get; set; }
    /// <summary>For flag tests: true = must be set, false = must be clear (negative number).</summary>
    public bool FlagSet { get; set; } = true;
    public ErmParam? Left { get; set; }
    public ErmParam? Right { get; set; }
}

public sealed class ErmCondition
{
    public List<ErmCondItem> And { get; } = new();
    public List<ErmCondItem> Or { get; } = new();
    public bool IsEmpty => And.Count == 0 && Or.Count == 0;
}

public readonly record struct ErmSourceLoc(string Script, int Line, int Column)
{
    public override string ToString() => $"{Script}:{Line}:{Column}";
}

/// <summary>A command letter with its parameters, e.g. "Fd1/d1/0/0" or "M^text^".</summary>
public sealed class ErmCommand
{
    public char Letter { get; set; }
    public List<ErmParam> Params { get; } = new();
    /// <summary>^text^ attached to the command (raw, interpolated when used).</summary>
    public string? Text { get; set; }
    /// <summary>@name@ attached to the command (MC:S).</summary>
    public string? MacroName { get; set; }
    public int Count => Params.Count;
}

/// <summary>A receiver (!!) or instruction (!#) line.</summary>
public sealed class ErmReceiverLine
{
    public string Id { get; set; } = "";
    public bool Instruction { get; set; }
    public List<ErmParam> Selector { get; } = new();
    public ErmCondition Condition { get; set; } = new();
    public List<ErmCommand> Commands { get; } = new();
    public ErmSourceLoc Loc { get; set; }
    public string Raw { get; set; } = "";
}

/// <summary>A trigger header with the receiver lines that follow it.</summary>
public sealed class ErmTriggerSection
{
    public string Id { get; set; } = "";
    public bool Post { get; set; }
    public List<ErmParam> Params { get; } = new();
    public ErmCondition Condition { get; set; } = new();
    /// <summary>Internal event id (InitTrigger). Computed by <see cref="ErmEventIds"/>.</summary>
    public int EventId { get; set; }
    public List<ErmReceiverLine> Lines { get; } = new();
    /// <summary>Per-file scope number (local functions FU-1..-100 only fire inside their scope).</summary>
    public int Scope { get; set; }
    public ErmSourceLoc Loc { get; set; }
}

public enum ErmItemKind { Section, Instruction, PostInstructionMarker }

/// <summary>Script contents in source order (instructions run while loading, interleaved with sections).</summary>
public sealed class ErmItem
{
    public ErmItemKind Kind { get; init; }
    public ErmTriggerSection? Section { get; init; }
    public ErmReceiverLine? Line { get; init; }
}

public enum ErmSeverity { Info, Warning, Error }

public sealed class ErmDiagnostic
{
    public ErmSeverity Severity { get; init; }
    public ErmSourceLoc Loc { get; init; }
    public string Message { get; init; } = "";
    public override string ToString() => $"{Severity} {Loc}: {Message}";
}

public sealed class ErmScript
{
    public string Name { get; set; } = "";
    /// <summary>False if the file does not start with ZVSE (it is then ignored, as in WoG).</summary>
    public bool IsErm { get; set; }
    public List<ErmItem> Items { get; } = new();
    public List<ErmDiagnostic> Diagnostics { get; } = new();

    public IEnumerable<ErmTriggerSection> Sections
    {
        get { foreach (var i in Items) if (i.Section != null) yield return i.Section; }
    }
}
