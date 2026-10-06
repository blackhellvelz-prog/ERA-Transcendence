using System;
using System.Globalization;
using WoG.Core.Compat;
using WoG.Core.State;
using WoG.Erm.Runtime;
using WoG.Erm.Syntax;

namespace WoG.Erm.Receivers;

/// <summary>!!VR — ERM_Variable (erm.cpp).</summary>
public sealed class VrReceiver : ErmReceiverBase
{
    public VrReceiver() : base("VR")
    {
        Declare("SRT+-*:%&|^XHCUMV", CompatLevel.FullySupported);
    }

    protected override void Run(ErmCall c)
    {
        var rt = c.Rt;
        var target = c.SelectorVar() ?? throw new ErmRuntimeException("!!VR needs a variable");
        int vv;
        switch (c.Letter)
        {
            case 'S':
                if (c.Num != 1) throw new ErmRuntimeException("\"!!VR\"-wrong number of parameters.");
                if (target.Kind == ErmVarKind.Z)
                {
                    if (c.N(0) != 0)
                    {
                        vv = rt.GetInt(target);              // index of the target string
                        if (c.Apply(ref vv, 0)) break;
                        rt.SetInt(target, vv);               // copies string #vv into the target
                    }
                    else
                    {
                        int ind = rt.GetInt(target);
                        if (ind < -20 || ind == 0 || ind > 1000) throw new ErmRuntimeException("\"!!VR:S\"-var is out of set (z-20...-1,1...z1000).");
                        rt.SetString(ind, c.Cmd.Text != null ? rt.Interpolate(c.Cmd.Text) : "");
                    }
                }
                else if (target.Kind == ErmVarKind.E)
                {
                    if (c.P(0).Mode != ErmParamMode.Set) throw new ErmRuntimeException("\"!!VR:S\"-you cannot use get or check syntax for float variable.");
                    rt.SetFloat(target, ParamFloat(c, 0));
                }
                else
                {
                    vv = rt.GetInt(target);
                    if (c.Apply(ref vv, 0)) break;
                    rt.SetInt(target, vv);
                }
                break;
            case 'R':
                if (c.Num > 2) throw new ErmRuntimeException("\"!!VR\"-wrong number of parameters.");
                if (c.Num == 1)
                {
                    vv = 0;
                    c.Apply(ref vv, 0);
                    rt.SetInt(target, unchecked(rt.GetInt(target) + rt.Services.Random.Next(0, vv)));
                }
                else
                {
                    int seed = rt.Services.Random.Seed;
                    if (!c.Apply(ref seed, 1)) rt.Services.Random.Seed = seed;
                }
                break;
            case 'T':
                // TimeRandom: a generator seeded from the clock. Same arithmetic, separate stream.
                if (c.Num != 1) throw new ErmRuntimeException("\"!!VR\"-wrong number of parameters.");
                vv = 0;
                c.Apply(ref vv, 0);
                rt.SetInt(target, unchecked(rt.GetInt(target) + TimeRandom.Next(0, vv + 1)));
                break;
            case '+':
                if (c.Num != 1) throw new ErmRuntimeException("\"!!VR\"-wrong number of parameters.");
                if (target.Kind == ErmVarKind.Z)
                {
                    int ind = rt.GetInt(target);
                    string cur = rt.GetStringRaw(ind);
                    if (c.N(0) != 0)
                    {
                        vv = ind;
                        if (c.Apply(ref vv, 0)) break;
                        rt.SetString(ind, cur + rt.GetStringRaw(vv));
                    }
                    else rt.SetString(ind, cur + (c.Cmd.Text != null ? rt.Interpolate(c.Cmd.Text) : ""));
                }
                else if (target.Kind == ErmVarKind.E) rt.SetFloat(target, rt.GetFloat(target) + ParamFloat(c, 0));
                else { vv = 0; c.Apply(ref vv, 0); rt.SetInt(target, unchecked(rt.GetInt(target) + vv)); }
                break;
            case '-':
                if (c.Num != 1) throw new ErmRuntimeException("\"!!VR\"-wrong number of parameters.");
                if (target.Kind == ErmVarKind.E) { rt.SetFloat(target, rt.GetFloat(target) - ParamFloat(c, 0)); break; }
                vv = 0; c.Apply(ref vv, 0); rt.SetInt(target, unchecked(rt.GetInt(target) - vv));
                break;
            case '*':
                if (c.Num != 1) throw new ErmRuntimeException("\"!!VR\"-wrong number of parameters.");
                if (target.Kind == ErmVarKind.E) { rt.SetFloat(target, rt.GetFloat(target) * ParamFloat(c, 0)); break; }
                vv = 0; c.Apply(ref vv, 0); rt.SetInt(target, unchecked(rt.GetInt(target) * vv));
                break;
            case ':':
                if (c.Num != 1) throw new ErmRuntimeException("\"!!VR\"-wrong number of parameters.");
                if (target.Kind == ErmVarKind.E)
                {
                    float d = ParamFloat(c, 0);
                    if (d == 0f) throw new ErmRuntimeException("Sorry. Division by zero :-)");
                    rt.SetFloat(target, rt.GetFloat(target) / d);
                    break;
                }
                vv = 0; c.Apply(ref vv, 0);
                if (vv == 0) throw new ErmRuntimeException("Sorry. Division by zero :-)");
                rt.SetInt(target, CDiv(rt.GetInt(target), vv));
                break;
            case '%':
                if (c.Num != 1) throw new ErmRuntimeException("\"!!VR\"-wrong number of parameters.");
                vv = 0; c.Apply(ref vv, 0);
                if (vv == 0) throw new ErmRuntimeException("Sorry. Division by zero :-)");
                rt.SetInt(target, CRem(rt.GetInt(target), vv));
                break;
            case '&':
                if (c.Num != 1) throw new ErmRuntimeException("\"!!VR\"-wrong number of parameters.");
                vv = 0; c.Apply(ref vv, 0); rt.SetInt(target, rt.GetInt(target) & vv);
                break;
            case '|':
                if (c.Num != 1) throw new ErmRuntimeException("\"!!VR\"-wrong number of parameters.");
                vv = 0; c.Apply(ref vv, 0); rt.SetInt(target, rt.GetInt(target) | vv);
                break;
            case '^':
                rt.Log?.Invoke("WARNING! The '!!VR:^$' command should be changed to '!!VR:X$' command.");
                goto case 'X';
            case 'X':
                if (c.Num != 1) throw new ErmRuntimeException("\"!!VR\"-wrong number of parameters.");
                vv = 0;
                if (c.Apply(ref vv, 0)) break;
                rt.SetInt(target, rt.GetInt(target) ^ vv);
                break;
            case 'H':
                if (c.Num != 1) throw new ErmRuntimeException("\"!!VR\"-wrong number of parameters.");
                if (target.Kind != ErmVarKind.Z) throw new ErmRuntimeException("\"!!VR:H\"-not a string variable (z#).");
                if (c.N(0) < 1 || c.N(0) > 1000) throw new ErmRuntimeException("\"!!VR:H\"-flag number out of range (1...1000).");
                rt.SetFlag(c.N(0), WoGStrings.HasText(rt.GetStringRaw(rt.GetInt(target))));
                break;
            case 'C':
            {
                if (target.Kind < ErmVarKind.Quick || target.Kind > ErmVarKind.Y)
                    throw new ErmRuntimeException("wrong variable type (must be integer).");
                int ind = rt.ResolveIndex(target);
                for (int i = 0; i < c.Num; i++, ind++)
                {
                    var r = new ErmVarRef { Kind = target.Kind, Index = ind };
                    int v = rt.GetInt(r);
                    if (!c.Apply(ref v, i)) rt.SetInt(r, v);
                }
                break;
            }
            case 'U':
            {
                if (c.Num != 1) throw new ErmRuntimeException("\"!!VR\"-wrong number of parameters.");
                if (target.Kind != ErmVarKind.Z) throw new ErmRuntimeException("\"!!VR:U\"-not Z var.");
                string s = rt.GetStringRaw(rt.GetInt(target));
                string d;
                if (c.N(0) != 0)
                {
                    vv = 0;
                    if (c.Apply(ref vv, 0)) break;
                    d = rt.GetString(vv);
                }
                else d = c.Cmd.Text != null ? rt.Interpolate(c.Cmd.Text) : "";
                rt.SetFlag(1, WoGStrings.Search4Substring(s, d));
                break;
            }
            case 'M':
                StringOp(c, target);
                break;
            case 'V':
            {
                if (c.Num != 1) throw new ErmRuntimeException("\"!!VR\"-wrong number of parameters.");
                vv = 0;
                if (c.Apply(ref vv, 0)) break;
                string s = rt.GetStringRaw(vv);
                if (target.Kind == ErmVarKind.E) rt.SetFloat(target, (float)Atof(s));
                else if (target.Kind >= ErmVarKind.Quick && target.Kind <= ErmVarKind.Y) rt.SetInt(target, Atoi(s));
                else throw new ErmRuntimeException("\"!!VR:V\"-wrong type of var (fl or int only).");
                break;
            }
            default:
                throw ErmCall.WrongCommand(c.Letter);
        }
    }

    static void StringOp(ErmCall c, ErmVarRef target)
    {
        var rt = c.Rt;
        if (target.Kind != ErmVarKind.Z) throw new ErmRuntimeException("\"!!VR:M\"-not Z var.");
        c.RequireMin(2);
        int ind = rt.GetInt(target);
        if (ind < -20 || ind == 0 || ind > 1000) throw new ErmRuntimeException("\"!!VR:M\"- z var out of range (-20...-1,1...1000).");
        string d = rt.GetStringRaw(ind);
        int vv, vv2;
        switch (c.N(0))
        {
            case 1:
            {
                if (c.Num < 4) throw new ErmRuntimeException("\"!!VR:M1\"-wrong number of parameters.");
                vv = 0; if (c.Apply(ref vv, 1)) return;
                string src = rt.GetStringRaw(vv);
                vv = 0; if (c.Apply(ref vv, 2)) return;
                vv2 = 0; if (c.Apply(ref vv2, 3)) return;
                if (vv > 511) vv = 511;
                if (vv2 + vv > 511) vv2 = 511 - vv;
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < vv2; i++) sb.Append(vv + i < src.Length ? src[vv + i] : '\0');
                string sub = sb.ToString();
                int nul = sub.IndexOf('\0');
                rt.SetString(ind, nul >= 0 ? sub.Substring(0, nul) : sub);
                return;
            }
            case 2:
            {
                if (c.Num < 3) throw new ErmRuntimeException("\"!!VR:M2\"-wrong number of parameters.");
                vv = 0; if (c.Apply(ref vv, 1)) return;
                string src = rt.GetStringRaw(vv);
                vv = 0; if (c.Apply(ref vv, 2)) return;
                rt.SetString(ind, WoGStrings.Token(src, vv));
                return;
            }
            case 3:
            {
                vv = 0; c.Apply(ref vv, 1);
                int radix = 10;
                if (c.Num > 2) c.Apply(ref radix, 2);
                rt.SetString(ind, Itoa(vv, radix));
                return;
            }
            case 4:
                vv = d.Length; c.Apply(ref vv, 1);
                return;
            case 5:
                vv = WoGStrings.FirstSignificant(d); c.Apply(ref vv, 1);
                return;
            case 6:
                vv = WoGStrings.LastSignificant(d); c.Apply(ref vv, 1);
                return;
            default:
                throw new ErmRuntimeException("\"!!VR:M\"-wrong first parameter.");
        }
    }

    static float ParamFloat(ErmCall c, int i)
    {
        var p = c.P(i);
        if (p.Var != null) return c.Rt.GetFloat(p.Var);
        return c.N(i);
    }

    /// <summary>C division truncates toward zero; int.MinValue / -1 wraps.</summary>
    static int CDiv(int a, int b) => b == -1 ? unchecked(-a) : a / b;
    static int CRem(int a, int b) => b == -1 ? 0 : a % b;

    /// <summary>MSVC itoa: negative numbers only get a '-' in base 10; other bases print the unsigned value.</summary>
    static string Itoa(int v, int radix)
    {
        if (radix < 2 || radix > 36) return "";
        if (radix == 10) return v.ToString(CultureInfo.InvariantCulture);
        uint u = unchecked((uint)v);
        if (u == 0) return "0";
        var sb = new System.Text.StringBuilder();
        while (u > 0) { uint dgt = u % (uint)radix; sb.Insert(0, (char)(dgt < 10 ? '0' + dgt : 'a' + dgt - 10)); u /= (uint)radix; }
        return sb.ToString();
    }

    /// <summary>atoi: leading whitespace, sign, digits; stops at the first other char.</summary>
    internal static int Atoi(string s)
    {
        int i = 0;
        while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        int sign = 1;
        if (i < s.Length && (s[i] == '-' || s[i] == '+')) { if (s[i] == '-') sign = -1; i++; }
        long acc = 0;
        while (i < s.Length && s[i] >= '0' && s[i] <= '9') { acc = acc * 10 + (s[i] - '0'); if (acc > int.MaxValue) break; i++; }
        return unchecked((int)(acc * sign));
    }

    static double Atof(string s)
    {
        int i = 0;
        while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        int j = i;
        if (j < s.Length && (s[j] == '-' || s[j] == '+')) j++;
        while (j < s.Length && (char.IsDigit(s[j]) || s[j] == '.')) j++;
        if (j < s.Length && (s[j] == 'e' || s[j] == 'E'))
        {
            int k = j + 1;
            if (k < s.Length && (s[k] == '-' || s[k] == '+')) k++;
            if (k < s.Length && char.IsDigit(s[k])) { j = k; while (j < s.Length && char.IsDigit(s[j])) j++; }
        }
        return double.TryParse(s.AsSpan(i, j - i), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0;
    }

    static readonly System.Random TimeRandom = new();
}

/// <summary>!!FU — ERM_Function.</summary>
public sealed class FuReceiver : ErmReceiverBase
{
    public FuReceiver() : base("FU")
    {
        Declare("PEXC", CompatLevel.FullySupported);
        Declare("D", CompatLevel.Unsupported, "FU:D is a network call (multiplayer is out of scope)");
    }

    protected override void Run(ErmCall c)
    {
        var rt = c.Rt;
        switch (c.Letter)
        {
            case 'P':
            {
                int fn = c.Selector();
                var args = new int[c.Num];
                for (int i = 0; i < c.Num; i++) args[i] = c.N(i);
                var lastCall = rt.LastFunctionCall;
                rt.LastFunctionCall = c;
                int[] newX;
                try { newX = rt.CallFunction(fn, args); }
                finally { rt.LastFunctionCall = lastCall; }
                for (int i = 0; i < c.Num; i++)
                    if (c.IsGet(i)) { int v = newX[i]; c.Apply(ref v, i); }
                break;
            }
            case 'E':
                rt.TriggerBreak = true;
                rt.TriggerGoTo = 0;
                if (c.Num > 1) throw new ErmRuntimeException("too many parameters");
                if (c.Num == 1 && !c.Cmd.Params[0].Empty) rt.TriggerGoTo = c.N(0);
                if (rt.TriggerGoTo < -256) throw new ErmRuntimeException("cannot jump more than 256 triggers back.");
                break;
            case 'X':
            {
                c.RequireExactly(2);
                int v = c.N(0) - 1;
                if (v < 0 || v >= 16) throw new ErmRuntimeException("x var index out of range (1...16).");
                int check = 0;
                var last = rt.LastFunctionCall;
                if (last != null && v < last.Num)
                {
                    var p = last.P(v);
                    check = p.Mode == ErmParamMode.Get ? 1 : p.Mode == ErmParamMode.Check ? (int)p.Compare : 0;
                    if (check == 0) check = p.Add ? -1 : 0;
                    else if (check == 2 && p.Add) check = -2;
                }
                c.Apply(ref check, 1);
                break;
            }
            case 'C':
                // y-var-outside-function check: a debugging aid of WoG; accepted, no effect on semantics.
                break;
            default:
                throw ErmCall.WrongCommand(c.Letter);
        }
    }
}

/// <summary>!!DO — ERM_Do.</summary>
public sealed class DoReceiver : ErmReceiverBase
{
    public DoReceiver() : base("DO")
    {
        Declare("P", CompatLevel.FullySupported);
    }

    protected override void Run(ErmCall c)
    {
        if (c.Letter != 'P') throw ErmCall.WrongCommand(c.Letter);
        if (c.SelectorCount != 4) throw new ErmRuntimeException("wrong syntax for receiver (loop).");
        var rt = c.Rt;
        int fn = c.Selector(0), from = c.Selector(1), to = c.Selector(2), step = c.Selector(3);
        int ev = rt.FunctionEvent(fn);
        rt.StoreVars(true);
        var oldX = (int[])rt.X.Clone();
        Array.Clear(rt.X, 0, 16);
        for (int i = 0; i < c.Num; i++) rt.X[i] = c.N(i);
        bool haveEqual = false;
        for (int i = 0; i < c.Num; i++) if (c.P(i).Mode == ErmParamMode.Check && c.P(i).Compare == ErmCompare.Eq) haveEqual = true;
        var lastCall = rt.LastFunctionCall;
        rt.LastFunctionCall = c;
        int[] newX;
        try
        {
            for (rt.X[15] = from; rt.X[15] <= to; rt.X[15] += step)
            {
                for (int i = 0; i < c.Num; i++)
                    if (c.IsGet(i)) rt.X[i] = c.N(i);
                if (haveEqual)
                    for (int i = 0; i < c.Num; i++)
                    {
                        var p = c.P(i);
                        if (p.Mode == ErmParamMode.Check && p.Compare == ErmCompare.Eq && p.Var != null)
                            rt.X[i] = rt.GetInt(p.Var);
                    }
                rt.Process(ev, rt.Context, needLocals: false);
                if (haveEqual)
                    for (int i = 0; i < c.Num; i++)
                    {
                        var p = c.P(i);
                        if (p.Mode != ErmParamMode.Get || p.Var == null) continue;
                        var k = p.Var.IndexKind != ErmVarKind.None ? p.Var.IndexKind : p.Var.Kind;
                        if ((k == ErmVarKind.Y && c.N(i) > 0) || (k == ErmVarKind.Z && c.N(i) < 0)) continue;
                        int v = rt.X[i];
                        c.Apply(ref v, i);
                    }
                if (step == 0) break; // WoG would loop forever; the port stops after one pass
            }
        }
        finally
        {
            rt.LastFunctionCall = lastCall;
            newX = (int[])rt.X.Clone();
            rt.X = oldX;
            rt.RestoreVars();
        }
        for (int i = 0; i < c.Num; i++)
            if (c.IsGet(i)) { int v = newX[i]; c.Apply(ref v, i); }
    }
}

/// <summary>!!MC — ERM_Macro.</summary>
public sealed class McReceiver : ErmReceiverBase
{
    public McReceiver() : base("MC")
    {
        Declare("S", CompatLevel.FullySupported);
    }

    protected override void Run(ErmCall c)
    {
        if (c.Letter != 'S') throw ErmCall.WrongCommand(c.Letter);
        string? name = c.Cmd.MacroName;
        if (string.IsNullOrEmpty(name)) throw new ErmRuntimeException("Macro defined incorrectly.");
        if (name!.Length > 16) throw new ErmRuntimeException("Macro is too long (>16 characters).");
        var sel = c.Rt.ResolveMacro(c.Line.Selector[0]);
        int vi = c.SelectorCount > 1 && !c.Line.Selector[1].Empty ? c.Selector(1) : (sel.Var?.Index ?? sel.Number);
        var kind = sel.Var?.Kind ?? ErmVarKind.None;
        switch (kind)
        {
            case ErmVarKind.None: break;
            case ErmVarKind.Quick: if (vi < 1 || vi > 15) throw new ErmRuntimeException("\"!!MC:S\"-var is out of set (f...t)."); break;
            case ErmVarKind.V: if (vi < 1 || vi > WoGVariables.VCount) throw new ErmRuntimeException("\"!!MC:S\"-var is out of set (v1...v10000)."); break;
            case ErmVarKind.W: if (vi < 1 || vi > 200) throw new ErmRuntimeException("\"!!MC:S\"-var is out of set (w1...w200)."); break;
            case ErmVarKind.Z: if (vi < 1 || vi > 1000) throw new ErmRuntimeException("\"!!MC:S\"-var is out of set (z1...z1000)."); break;
            default: throw new ErmRuntimeException("\"!!MC:S\"-Wrong var type.");
        }
        if (kind == ErmVarKind.None && c.Rt.Options.Dialect == ErmDialect.Wog358)
            throw new ErmRuntimeException("numeric macros are WoG 3.59");
        c.Rt.Services.State.Erm.Macros[name] = new ErmMacro { Name = name, Kind = kind, Index = vi };
    }
}

/// <summary>!!TM — ERM_Timer.</summary>
public sealed class TmReceiver : ErmReceiverBase
{
    public TmReceiver() : base("TM")
    {
        Declare("SED", CompatLevel.FullySupported);
    }

    protected override void Run(ErmCall c)
    {
        int ti = c.Selector();
        if (ti < 1 || ti > 100) throw new ErmRuntimeException($"\"!!TM:{c.Letter}\"-timer number is out of range (1...100).");
        var t = c.Rt.Services.State.Erm.Timers[ti - 1];
        switch (c.Letter)
        {
            case 'S':
                c.RequireMin(4);
                int v = t.FirstDay; c.Apply(ref v, 0); t.FirstDay = (ushort)v;
                v = t.LastDay; c.Apply(ref v, 1); t.LastDay = (ushort)v;
                v = t.Period; c.Apply(ref v, 2); t.Period = (ushort)v;
                v = t.OwnerMask; c.Apply(ref v, 3); t.OwnerMask = (ushort)v;
                break;
            case 'E':
                c.RequireExactly(1);
                t.OwnerMask = (ushort)(t.OwnerMask | (1 << c.N(0)));
                break;
            case 'D':
                c.RequireExactly(1);
                t.OwnerMask = (ushort)(t.OwnerMask & ~(1 << c.N(0)));
                break;
            default:
                throw ErmCall.WrongCommand(c.Letter);
        }
    }
}
