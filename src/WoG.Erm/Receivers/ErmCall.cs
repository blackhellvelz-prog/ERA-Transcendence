using System;
using WoG.Core.Adapters;
using WoG.Erm.Runtime;
using WoG.Erm.Syntax;
using WoG.Core.State;

namespace WoG.Erm.Receivers;

/// <summary>The target engine cannot perform the command; logged in the compatibility report.</summary>
public sealed class ErmUnsupportedException : Exception
{
    public ErmUnsupportedException(string message) : base(message) { }
}

/// <summary>
/// One command being executed: gives receivers WoG's Mes/VarNum view — evaluated parameter values
/// (Mes.n[]), the get/set/check modes and the Apply() primitive.
/// </summary>
public sealed class ErmCall
{
    public ErmRuntime Rt { get; }
    public ErmReceiverLine Line { get; }
    public ErmCommand Cmd { get; }
    readonly int[] n;

    public ErmCall(ErmRuntime rt, ErmReceiverLine line, ErmCommand cmd)
    {
        Rt = rt;
        Line = line;
        Cmd = cmd;
        n = new int[cmd.Params.Count];
        for (int i = 0; i < n.Length; i++) n[i] = rt.Evaluate(cmd.Params[i]);
    }

    public char Letter => Cmd.Letter;
    /// <summary>Number of parameters (WoG "Num").</summary>
    public int Num => Cmd.Params.Count;
    public int N(int i) => i < n.Length ? n[i] : 0;
    public ErmParam P(int i) => Rt.ResolveMacro(Cmd.Params[i]);
    public bool IsGet(int i) => i < Num && P(i).Mode == ErmParamMode.Get;
    public bool IsCheck(int i) => i < Num && P(i).Mode == ErmParamMode.Check;
    public bool IsGetOrCheck(int i) => i < Num && P(i).Mode != ErmParamMode.Set;

    // ---- selector (the part before ':') --------------------------------------------------------

    public int SelectorCount => Line.Selector.Count;
    public int Selector(int i = 0) => i < Line.Selector.Count ? Rt.Evaluate(Line.Selector[i]) : 0;
    public ErmVarRef? SelectorVar(int i = 0) =>
        i < Line.Selector.Count ? Rt.ResolveMacro(Line.Selector[i]).Var : null;
    public bool SelectorIsEmpty => Line.Selector.Count == 1 && Line.Selector[0].Empty;

    // ---- Apply (erm.cpp) -----------------------------------------------------------------------

    /// <summary>
    /// Apply(): set (or add with 'd') <paramref name="v"/> from parameter i, or write v into the '?'
    /// variable, or compare v and store the result in flag 1. Returns true for get/check (the caller
    /// then must not perform its "set" side effect).
    /// </summary>
    public bool Apply(ref int v, int i)
    {
        if (i >= Num) throw new ErmRuntimeException("wrong number of parameters");
        var p = P(i);
        switch (p.Mode)
        {
            case ErmParamMode.Get:
                if (p.Var == null) throw new ErmRuntimeException("cannot get flag");
                if (p.Var.Kind == ErmVarKind.Z)
                {
                    // 3.58 z vars: '?zN' behaves like PutVal with the index as value.
                    v = p.Add ? v + N(i) : N(i);
                }
                else if (p.Var.Kind == ErmVarKind.E) Rt.SetFloat(p.Var, v);
                else Rt.SetInt(p.Var, v);
                return true;
            case ErmParamMode.Check:
                Rt.SetFlag(1, ErmRuntime.Compare(v.CompareTo(N(i)), p.Compare));
                return true;
            default:
                v = p.Add ? unchecked(v + N(i)) : N(i);
                return false;
        }
    }

    /// <summary>Apply for a value that is stored elsewhere: reads it, applies, writes it back on set.</summary>
    public bool ApplyTo(Func<int> read, Action<int> write, int i)
    {
        int v = read();
        if (Apply(ref v, i)) return true;
        write(v);
        return false;
    }

    /// <summary>GetErmText: a z-var parameter, or the ^text^ attached to the command (interpolated).</summary>
    public string Text(int i)
    {
        if (i < Num)
        {
            var p = P(i);
            if (p.Mode != ErmParamMode.Set) throw new ErmRuntimeException("cannot use get or check syntax.");
            if (p.Var?.Kind == ErmVarKind.Z) return Rt.GetString(Rt.ResolveIndex(p.Var));
        }
        if (Cmd.Text != null) return Rt.Interpolate(Cmd.Text);
        throw new ErmRuntimeException("string expected.");
    }

    public void RequireMin(int min)
    {
        if (Num < min) throw new ErmRuntimeException($"wrong number of parameters (at least {min})");
    }

    public void RequireExactly(int count)
    {
        if (Num != count) throw new ErmRuntimeException($"wrong number of parameters ({count} expected)");
    }

    public static ErmRuntimeException WrongCommand(char c) => new($"wrong command '{c}'");

    // ---- adapter results ------------------------------------------------------------------------

    public T Need<T>(AdapterResult<T> r)
    {
        if (r.Status == AdapterStatus.Ok) return r.Value;
        if (r.Status == AdapterStatus.Unsupported) throw new ErmUnsupportedException(r.Reason ?? "unsupported");
        throw new ErmRuntimeException(r.Reason ?? "failed");
    }

    public void Need(AdapterResult r)
    {
        if (r.Status == AdapterStatus.Ok) return;
        if (r.Status == AdapterStatus.Unsupported) throw new ErmUnsupportedException(r.Reason ?? "unsupported");
        throw new ErmRuntimeException(r.Reason ?? "failed");
    }

    /// <summary>Apply() against an adapter-backed value.</summary>
    public bool ApplyAdapter(AdapterResult<int> current, Func<int, AdapterResult> write, int i)
    {
        int v = Need(current);
        if (Apply(ref v, i)) return true;
        Need(write(v));
        return false;
    }
}
