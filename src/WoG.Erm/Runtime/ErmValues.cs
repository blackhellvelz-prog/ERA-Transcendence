using System;
using System.Globalization;
using System.Text;
using WoG.Core.State;
using WoG.Erm.Syntax;

namespace WoG.Erm.Runtime;

/// <summary>Variable access, parameter evaluation, conditions and string interpolation.</summary>
public sealed partial class ErmRuntime
{
    // ---- index resolution (CheckVarIndex / GetVarIndex) ----------------------------------------

    static void CheckIndex(ErmVarKind kind, int vi)
    {
        bool ok = kind switch
        {
            ErmVarKind.None => true,
            ErmVarKind.Flag => vi >= 1 && vi <= WoGVariables.FlagCount,
            ErmVarKind.Quick => vi >= 1 && vi <= WoGVariables.QuickCount,
            ErmVarKind.V => vi >= 1 && vi <= WoGVariables.VCount,
            ErmVarKind.W => vi >= 1 && vi <= WoGVariables.WCount,
            ErmVarKind.X => vi >= 1 && vi <= 16,
            ErmVarKind.Y => vi != 0 && vi >= -100 && vi <= 100,
            ErmVarKind.Z => vi != 0 && vi >= -20,
            ErmVarKind.E => vi != 0 && vi >= -100 && vi <= 100,
            _ => false,
        };
        if (!ok) throw new ErmRuntimeException($"Var is out of set ({ErmVarRef.Letter(kind)}{vi}).");
    }

    /// <summary>Final index of a variable reference (applies vy5-style indirection).</summary>
    public int ResolveIndex(ErmVarRef r)
    {
        int vi = r.Index;
        if (r.IndexKind != ErmVarKind.None)
        {
            CheckIndex(r.IndexKind, vi);
            vi = ReadInt(r.IndexKind, vi);
        }
        CheckIndex(r.Kind, vi);
        // z-11..z-20 were added in 3.59 (ChangeLog3: "20 local z vars instead of 10").
        if (r.Kind == ErmVarKind.Z && vi < -10 && Options.Dialect == ErmDialect.Wog358)
            throw new ErmRuntimeException($"Var is out of set (z-10...-1 in WoG 3.58): z{vi}");
        return vi;
    }

    int ReadInt(ErmVarKind kind, int vi) => kind switch
    {
        ErmVarKind.Quick => Vars.Quick[vi - 1],
        ErmVarKind.V => Vars.V[vi - 1],
        ErmVarKind.W => Vars.WFor(Vars.CurrentWHero)[vi - 1],
        ErmVarKind.X => X[vi - 1],
        ErmVarKind.Y => vi < 0 ? yt[-vi - 1] : y[vi - 1],
        _ => throw new ErmRuntimeException("Incorrect index variable"),
    };

    void WriteInt(ErmVarKind kind, int vi, int value)
    {
        switch (kind)
        {
            case ErmVarKind.Quick: Vars.Quick[vi - 1] = value; break;
            case ErmVarKind.V: Vars.V[vi - 1] = value; break;
            case ErmVarKind.W: Vars.WFor(Vars.CurrentWHero)[vi - 1] = value; break;
            case ErmVarKind.X: X[vi - 1] = value; break;
            case ErmVarKind.Y: if (vi < 0) yt[-vi - 1] = value; else y[vi - 1] = value; break;
            case ErmVarKind.E: if (vi < 0) ft[-vi - 1] = value; else f[vi - 1] = value; break;
            default: throw new ErmRuntimeException("wrong var type (f...t,v,w,x,y,e).");
        }
    }

    /// <summary>GetVarVal: ints read the value, z returns its index, e is truncated.</summary>
    public int GetInt(ErmVarRef r)
    {
        int vi = ResolveIndex(r);
        return r.Kind switch
        {
            ErmVarKind.Z => vi,
            ErmVarKind.E => (int)(vi < 0 ? ft[-vi - 1] : f[vi - 1]),
            _ => ReadInt(r.Kind, vi),
        };
    }

    /// <summary>SetVarVal: for a z target, Val is the index of the source string.</summary>
    public void SetInt(ErmVarRef r, int value)
    {
        int vi = ResolveIndex(r);
        if (r.Kind == ErmVarKind.Z)
        {
            SetString(vi, GetStringRaw(value));
            return;
        }
        WriteInt(r.Kind, vi, value);
    }

    public float GetFloat(ErmVarRef r)
    {
        if (r.Kind != ErmVarKind.E) return GetInt(r);
        int vi = ResolveIndex(r);
        return vi < 0 ? ft[-vi - 1] : f[vi - 1];
    }

    public void SetFloat(ErmVarRef r, float v)
    {
        if (r.Kind != ErmVarKind.E) { SetInt(r, (int)v); return; }
        int vi = ResolveIndex(r);
        if (vi < 0) ft[-vi - 1] = v; else f[vi - 1] = v;
    }

    // ---- strings -------------------------------------------------------------------------------

    /// <summary>String variable without interpolation (GetPureErmString).</summary>
    public string GetStringRaw(int index)
    {
        if (index == 0 || index < -20) throw new ErmRuntimeException($"wrong z var index z{index} (-20...-1,1...1000)");
        if (index > WoGVariables.ZCount)
        {
            Services.Compat.Unsupported("erm", "ERT strings", "z>1000 texts come from .ert files (not loaded)");
            return "";
        }
        return index > 0 ? Vars.Z[index - 1] : lz[-index - 1];
    }

    /// <summary>GetErmString: the string with %-codes expanded.</summary>
    public string GetString(int index) => Interpolate(GetStringRaw(index));

    public void SetString(int index, string value)
    {
        if (index == 0 || index < -20 || index > WoGVariables.ZCount)
            throw new ErmRuntimeException($"var is out of set (z-20...-1,1...z1000): z{index}");
        value = WoGVariables.Clip(value);
        if (index > 0) Vars.Z[index - 1] = value; else lz[-index - 1] = value;
    }

    // ---- flags ---------------------------------------------------------------------------------

    public bool GetFlag(int n)
    {
        if (n < 1 || n > WoGVariables.FlagCount) throw new ErmRuntimeException("Flag is out of set (1...1000).");
        return Vars.Flags[n - 1];
    }

    public void SetFlag(int n, bool v)
    {
        if (n < 1 || n > WoGVariables.FlagCount) throw new ErmRuntimeException("Flag is out of set (1...1000).");
        Vars.Flags[n - 1] = v;
    }

    // ---- macros --------------------------------------------------------------------------------

    /// <summary>GetMacro: resolves $name$ to a variable reference or (numeric macro) a constant.</summary>
    public ErmParam ResolveMacro(ErmParam p)
    {
        if (p.Macro == null) return p;
        if (!Vars.Macros.TryGetValue(p.Macro, out var m))
            throw new ErmRuntimeException($"cannot find macro ${p.Macro}$");
        var r = new ErmParam { Mode = p.Mode, Compare = p.Compare, Add = p.Add };
        if (m.Kind == ErmVarKind.None) r.Number = m.Index;
        else r.Var = new ErmVarRef { Kind = m.Kind, Index = m.Index };
        return r;
    }

    // ---- parameter evaluation (GetNum with immed=1) --------------------------------------------

    /// <summary>The value WoG puts in Mes.n[] for this parameter.</summary>
    public int Evaluate(ErmParam p)
    {
        p = ResolveMacro(p);
        if (p.Var == null) return p.Number + (p.DayRelative ? Services.Game.Clock.AbsoluteDay : 0);
        if (p.Mode == ErmParamMode.Get) return ResolveIndex(p.Var);
        return GetInt(p.Var);
    }

    // ---- conditions (CheckFlags) ---------------------------------------------------------------

    public bool CheckCondition(ErmCondition c)
    {
        if (c.IsEmpty) return true;
        try
        {
            if (c.And.Count > 0)
            {
                bool all = true;
                foreach (var item in c.And)
                    if (!Item(item)) { all = false; break; }
                if (all) return true;
            }
            foreach (var item in c.Or)
                if (Item(item)) return true;
            return false;
        }
        catch (ErmRuntimeException ex)
        {
            Error(null, "CheckFlags: " + ex.Message);
            return false;
        }
    }

    bool Item(ErmCondItem item)
    {
        if (item.Flag is int fl) return GetFlag(fl) == item.FlagSet;
        var left = ResolveMacro(item.Left!);
        var right = ResolveMacro(item.Right!);
        var cmp = item.Right!.Compare;
        if (left.Var!.Kind == ErmVarKind.Z && right.Var?.Kind == ErmVarKind.Z)
        {
            bool eq = WoGStrings.StrCmpExt(GetStringRaw(ResolveIndex(left.Var)), GetStringRaw(ResolveIndex(right.Var)));
            return cmp switch
            {
                ErmCompare.Eq => eq,
                ErmCompare.Ne => !eq,
                _ => throw new ErmRuntimeException("Z vars may be compared for = or <> only"),
            };
        }
        bool fl1 = left.Var.Kind == ErmVarKind.E || right.Var?.Kind == ErmVarKind.E;
        if (fl1)
        {
            float a = left.Var.Kind == ErmVarKind.E ? GetFloat(left.Var) : GetInt(left.Var);
            float b = right.Var == null ? right.Number : right.Var.Kind == ErmVarKind.E ? GetFloat(right.Var) : GetInt(right.Var);
            return Compare(a.CompareTo(b), cmp);
        }
        int x = GetInt(left.Var);
        // WoG quirk: a z on the right side compares against the *left* value's index (n2=zind bug) — not
        // reproduced: comparing an int with a string index has no meaningful use in the corpus.
        int yv = right.Var == null ? right.Number : GetInt(right.Var);
        return Compare(x.CompareTo(yv), cmp);
    }

    internal static bool Compare(int sign, ErmCompare c) => c switch
    {
        ErmCompare.Eq => sign == 0,
        ErmCompare.Ne => sign != 0,
        ErmCompare.Gt => sign > 0,
        ErmCompare.Lt => sign < 0,
        ErmCompare.Ge => sign >= 0,
        ErmCompare.Le => sign <= 0,
        _ => false,
    };

    // ---- %-interpolation (_Message2ERM, 5 passes) ----------------------------------------------

    public string Interpolate(string text)
    {
        string s = text;
        for (int pass = 0; pass < 5; pass++)
        {
            if (s.IndexOf('%') < 0) break;
            s = InterpolateOnce(s);
        }
        return s;
    }

    string InterpolateOnce(string str)
    {
        var o = new StringBuilder(str.Length + 16);
        for (int i = 0; i < str.Length; i++)
        {
            char ch = str[i];
            if (ch != '%') { o.Append(ch); continue; }
            if (i + 1 >= str.Length) { o.Append('%'); break; }
            ch = str[++i];
            switch (ch)
            {
                case '%': o.Append('%'); break;
                case '$':
                {
                    int end = str.IndexOf('$', i + 1);
                    if (end < 0) { Error(null, "\"String 2ERM\"-wrong macro $...$."); break; }
                    string name = str.Substring(i + 1, end - i - 1);
                    i = end;
                    if (!Vars.Macros.TryGetValue(name, out var m)) { Error(null, "\"String 2ERM\"-wrong macro $...$."); break; }
                    if (m.Kind == ErmVarKind.Z) o.Append(Vars.Z[m.Index - 1]);
                    else if (m.Kind == ErmVarKind.None) o.Append(m.Index);
                    else o.Append(ReadInt(m.Kind, m.Index));
                    break;
                }
                case 'V':
                {
                    if (i + 1 < str.Length && str[i + 1] >= 'f' && str[i + 1] <= 't')
                    {
                        o.Append(Vars.Quick[str[++i] - 'e' - 1]);
                        break;
                    }
                    int v = ReadNumber(str, ref i);
                    if (v >= 1 && v <= WoGVariables.VCount) o.Append(Vars.V[v - 1]);
                    else Error(null, "\"String 2ERM\"-wrong %V# number (1...10000).");
                    break;
                }
                case 'W':
                {
                    int v = ReadNumber(str, ref i);
                    if (v >= 1 && v <= WoGVariables.WCount) o.Append(Vars.WFor(Vars.CurrentWHero)[v - 1]);
                    else Error(null, "\"String 2ERM\"-wrong %W# number (1...200).");
                    break;
                }
                case 'X':
                {
                    int v = ReadNumber(str, ref i);
                    if (v >= 1 && v <= 16) o.Append(X[v - 1]);
                    else Error(null, "\"String 2ERM\"-wrong %X# number (1...16).");
                    break;
                }
                case 'Y':
                {
                    int v = ReadNumber(str, ref i);
                    if (v >= 1 && v <= 100) o.Append(y[v - 1]);
                    else if (v <= -1 && v >= -100) o.Append(yt[-v - 1]);
                    else Error(null, "\"String 2ERM\"-wrong %Y# number (-100...-1,1...100).");
                    break;
                }
                case 'E':
                {
                    int v = ReadNumber(str, ref i);
                    if (v >= 1 && v <= 100) o.Append(F2A(f[v - 1]));
                    else if (v <= -1 && v >= -100) o.Append(F2A(ft[-v - 1]));
                    else Error(null, "\"String 2ERM\"-wrong %E# number (-100...-1,1...100).");
                    break;
                }
                case 'Z':
                {
                    int v = ReadNumber(str, ref i);
                    if (v < -20 || v == 0) { Error(null, "\"String 2ERM\"-wrong %Z# number (-20...-1,1...1000)."); break; }
                    if (v > WoGVariables.ZCount) { Services.Compat.Unsupported("erm", "%Z ERT", "ERT strings not loaded"); break; }
                    o.Append(v > 0 ? Vars.Z[v - 1] : lz[-v - 1]);
                    break;
                }
                case 'F':
                {
                    int v = ReadNumber(str, ref i);
                    if (v >= 1 && v <= 1000) o.Append(Vars.Flags[v - 1] ? 1 : 0);
                    else Error(null, "\"String 2ERM\"-wrong %F# number (1...1000).");
                    break;
                }
                case 'D':
                {
                    var clock = Services.Game.Clock;
                    char k = i + 1 < str.Length ? str[++i] : '\0';
                    switch (k)
                    {
                        case 'd': o.Append(clock.DayOfWeek); break;
                        case 'w': o.Append(clock.Week); break;
                        case 'm': o.Append(clock.Month); break;
                        case 'a': o.Append(clock.AbsoluteDay); break;
                        default: Error(null, "\"String 2ERM\"-wrong %D$ syntax."); break;
                    }
                    break;
                }
                case 'G':
                {
                    char k = i + 1 < str.Length ? str[++i] : '\0';
                    if (k == 'c') o.Append(PlayerColorName(Services.Game.Players.CurrentPlayer));
                    else Error(null, "\"String 2ERM\"-wrong %G# syntax.");
                    break;
                }
                default:
                    o.Append('%').Append(ch);
                    break;
            }
        }
        return o.ToString();
    }

    /// <summary>a2i + SkipNumbers: optional sign and digits right after the code letter.</summary>
    static int ReadNumber(string s, ref int i)
    {
        int j = i + 1;
        int start = j;
        if (j < s.Length && (s[j] == '-' || s[j] == '+')) j++;
        while (j < s.Length && char.IsDigit(s[j])) j++;
        int.TryParse(s.AsSpan(start, j - start), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int v);
        i = j - 1;
        return v;
    }

    /// <summary>f2a: always three decimals, half-up rounding on |v|·1000.</summary>
    public static string F2A(float v)
    {
        bool neg = v < 0;
        long n = (long)(Math.Abs((double)v) * 1000.0 + 0.5);
        string body = (n / 1000).ToString(CultureInfo.InvariantCulture) + "." + (n % 1000).ToString("000", CultureInfo.InvariantCulture);
        return neg ? "-" + body : body;
    }

    static readonly string[] Colors = { "Red", "Blue", "Tan", "Green", "Orange", "Purple", "Teal", "Pink" };

    /// <summary>%Gc prints the colour name from the game's text table; English names are used here.</summary>
    static string PlayerColorName(int p) => p >= 0 && p < Colors.Length ? Colors[p] : "";
}
