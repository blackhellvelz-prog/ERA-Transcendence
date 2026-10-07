using System;
using System.Collections.Generic;
using System.Globalization;
using WoG.Core.Compat;
using WoG.Core.State;
using WoG.Erm.Runtime;
using WoG.Erm.Syntax;

namespace WoG.Erm.Receivers;

/// <summary>!!VR as rewritten by Era (Erm.pas New_VR_Receiver and VR_* functions).</summary>
public sealed class EraVrReceiver : ErmReceiverBase
{
    public EraVrReceiver() : base("VR")
    {
        Declare("S+-*:%&|X~BCFHMURTVZ", CompatLevel.FullySupported);
    }

    protected override void Run(ErmCall c)
    {
        var rt = c.Rt;
        var target = c.Line.Selector.Count > 0 ? c.Rt.ResolveMacro(c.Line.Selector[0]) : throw new ErmRuntimeException("!!VR needs a variable");
        var tt = ErmRuntime.EraTypeOf(target);
        if (!(tt == ErmVarKind.Quick || tt == ErmVarKind.V || tt == ErmVarKind.W || tt == ErmVarKind.X || tt == ErmVarKind.Y
              || tt == ErmVarKind.Z || tt == ErmVarKind.E || tt == ErmVarKind.AssocI || tt == ErmVarKind.AssocS))
            throw new ErmRuntimeException("!!VR parameter does not belong to mutable types");
        switch (c.Letter)
        {
            case '&': case 'X': case '|': case '~': Bits(c, target); break;
            case '%': case '*': case '+': case '-': case ':': Arithmetic(c, target); break;
            case 'B': B(c, target); break;
            case 'C': C(c, target); break;
            case 'F': F(c, target); break;
            case 'H': case 'M': case 'U': Strings(c, target); break;
            case 'R': case 'T': RandomOp(c, target); break;
            case 'S': S(c, target); break;
            case 'V': V(c, target); break;
            case 'Z': Z(c, target); break;
            default: throw new ErmRuntimeException("Unknown ERM command !!VR:" + c.Letter);
        }
    }

    static float Bits2F(int v) => BitConverter.Int32BitsToSingle(v);
    static int F2Bits(float f) => BitConverter.SingleToInt32Bits(f);

    /// <summary>NormalizeErmFloatValue: NaN → 0, ±Inf → ±3.4e38.</summary>
    static float Normalize(float v) =>
        float.IsNaN(v) ? 0f : float.IsPositiveInfinity(v) ? 3.4e38f : float.IsNegativeInfinity(v) ? -3.4e38f : v;

    static float ApplyFloatModifier(float orig, float value, ErmModifier m)
    {
        if (m == ErmModifier.None) return value;
        float r = value;
        switch (m)
        {
            case ErmModifier.Add: r = orig + value; break;
            case ErmModifier.Sub: r = orig - value; break;
            case ErmModifier.Mul: r = orig * value; break;
            case ErmModifier.Div:
                if (value != 0) r = orig / value;
                break;
            case ErmModifier.Mod:
                if (value != 0) { float q = orig / value; r = (q - MathF.Truncate(q)) * value; }
                break;
        }
        return Normalize(r);
    }

    /// <summary>RoundFloat32: 0 = half away from zero, &lt;0 floor, &gt;0 ceil.</summary>
    static int RoundFloat32(float v, int direction)
    {
        if (direction == 0)
        {
            double ip = Math.Truncate(v);
            double frac = v - ip;
            return unchecked((int)(ip + Math.Truncate(frac * 2)));
        }
        return unchecked((int)(direction < 0 ? Math.Floor(v) : Math.Ceiling(v)));
    }

    static int Trunc(float v) => unchecked((int)MathF.Truncate(v));

    static void S(ErmCall c, ErmParam target)
    {
        var rt = c.Rt;
        if (c.Num < 1) throw new ErmRuntimeException("\"!!VR:S\" - value expected");
        var varParam = target;
        var valueParam = c.P(0);
        bool useRounding = c.Num >= 2;
        int second = c.N(0);
        if (valueParam.Mode == ErmParamMode.Get)
        {
            second = rt.EraGetInt(varParam);
            (varParam, valueParam) = (valueParam, varParam);
        }
        else if (valueParam.Mode != ErmParamMode.Set)
        {
            throw new ErmRuntimeException("\"!!VR:S\" - only GET/SET syntax is supported");
        }
        var mod = c.Cmd.Params[0].Modifier;

        if (ErmRuntime.EraIsInt(varParam))
        {
            if (ErmRuntime.EraIsInt(valueParam))
            {
                if (mod == ErmModifier.None) rt.EraSetInt(varParam, second);
                else rt.EraSetInt(varParam, rt.EraModify(rt.EraGetInt(varParam), second, mod));
            }
            else if (ErmRuntime.EraIsFloat(valueParam))
            {
                float v = rt.EraGetInt(varParam);
                v = ApplyFloatModifier(v, Bits2F(second), mod);
                rt.EraSetInt(varParam, useRounding ? RoundFloat32(v, c.N(1)) : Trunc(v));
            }
            else throw new ErmRuntimeException("\"!!VR:S\" - cannot set integer variable to non-numeric value");
        }
        else if (ErmRuntime.EraIsFloat(varParam))
        {
            if (!ErmRuntime.EraIsNumeric(valueParam)) throw new ErmRuntimeException("\"!!VR:S\" - cannot set float variable to non-numeric value");
            float cur = Bits2F(rt.EraGetInt(varParam));
            float sv = ErmRuntime.EraIsFloat(valueParam) ? Bits2F(second) : second;
            rt.EraSetInt(varParam, F2Bits(ApplyFloatModifier(cur, sv, mod)));
        }
        else
        {
            if (!ErmRuntime.EraIsString(valueParam)) throw new ErmRuntimeException("\"!!VR:S\" - cannot set string variable to non-string value");
            rt.EraSetString(varParam, rt.EraZInterpolated(second));
        }
    }

    static void Arithmetic(ErmCall c, ErmParam target)
    {
        var rt = c.Rt;
        if (c.Num < 1) throw new ErmRuntimeException("\"!!VR\" - value expected");
        var valueParam = c.P(0);
        bool resIsInt = ErmRuntime.EraIsInt(target);
        bool secondIsInt = ErmRuntime.EraIsInt(valueParam);
        bool argsAreFloat = !resIsInt || !secondIsInt;
        int value = rt.EraGetInt(target);
        int second = c.N(0);

        if (c.Letter == '+' && ErmRuntime.EraIsString(target))
        {
            // Era's check "not ValueParamType in STRINGS" is a precedence bug and never fires: any value is
            // taken as a string index here.
            string add = rt.EraZInterpolated(second);
            if (ErmRuntime.EraTypeOf(target) == ErmVarKind.Z)
            {
                rt.EraSetString(target, rt.EraZRaw(value) + add);
            }
            else
            {
                rt.EraSetString(target, rt.EraGetText(target) + add);
            }
            return;
        }
        if (!(ErmRuntime.EraIsNumeric(target) && ErmRuntime.EraIsNumeric(valueParam)))
            throw new ErmRuntimeException("\"!!VR\" - cannot perform arithmetic operations with non-numeric values");

        float fv = 0, fs = 0;
        if (argsAreFloat)
        {
            fv = resIsInt ? value : Bits2F(value);
            fs = secondIsInt ? second : Bits2F(second);
        }
        switch (c.Letter)
        {
            case '+': if (argsAreFloat) fv += fs; else value = unchecked(value + second); break;
            case '-': if (argsAreFloat) fv -= fs; else value = unchecked(value - second); break;
            case '*': if (argsAreFloat) fv *= fs; else value = unchecked(value * second); break;
            case ':':
            case '%':
                if (second == 0 || (argsAreFloat && fs == 0f)) throw new ErmRuntimeException("\"!!VR\" - division by zero");
                if (c.Letter == ':')
                {
                    if (argsAreFloat) fv /= fs;
                    else value = second == -1 ? unchecked(-value) : value / second;
                }
                else
                {
                    if (argsAreFloat) { float q = fv / fs; fv = (q - MathF.Truncate(q)) * fs; }
                    else value = second == -1 ? 0 : value % second;
                }
                break;
        }
        if (argsAreFloat)
        {
            fv = Normalize(fv);
            value = resIsInt ? Trunc(fv) : F2Bits(fv);
        }
        rt.EraSetInt(target, value);
    }

    static void Bits(ErmCall c, ErmParam target)
    {
        var rt = c.Rt;
        if (c.Num < 1) throw new ErmRuntimeException("\"!!VR\" - value expected");
        if (!(ErmRuntime.EraIsInt(target) && ErmRuntime.EraIsInt(c.P(0))))
            throw new ErmRuntimeException("\"!!VR\" - bit operations are supported for integers only");
        int v = rt.EraGetInt(target), s = c.N(0);
        v = c.Letter switch
        {
            '&' => v & s,
            '|' => v | s,
            'X' => v ^ s,
            _ => v & ~s,
        };
        rt.EraSetInt(target, v);
    }

    static void B(ErmCall c, ErmParam target)
    {
        var rt = c.Rt;
        if (!ErmRuntime.EraIsNumeric(target)) throw new ErmRuntimeException("\"!!VR:B\" - only numeric variables can be casted to TRUE/FALSE");
        int v = rt.EraGetInt(target);
        if (ErmRuntime.EraIsInt(target)) { if (v != 0) v = 1; }
        else if (Bits2F(v) != 0f) v = F2Bits(1f);
        rt.EraSetInt(target, v);
    }

    static void C(ErmCall c, ErmParam target)
    {
        var rt = c.Rt;
        var kind = ErmRuntime.EraTypeOf(target);
        if (!(kind == ErmVarKind.V || kind == ErmVarKind.W || kind == ErmVarKind.X || kind == ErmVarKind.Y || kind == ErmVarKind.E))
            throw new ErmRuntimeException("\"!!VR:C\" - only x, y, v, w, e variables are supported for mass assignment");
        // ZvsGetVarValIndex: the resolved index of the target
        int start = rt.EraResolvedIndex(target);
        int n = c.Num;
        int end = start >= 0 ? start + n - 1 : start - n + 1;
        bool ok = kind switch
        {
            ErmVarKind.V => start >= 1 && end <= WoGVariables.VCount,
            ErmVarKind.X => start >= 1 && end <= 16,
            ErmVarKind.W => start >= 1 && end <= WoGVariables.WCount,
            _ => (start >= 1 && end <= 100) || (-start >= 1 && -end <= 100),
        };
        if (!ok) throw new ErmRuntimeException($"\"!!VR:C\" first/last index is out of range: {start}..{start + n - 1}");
        for (int i = 0; i < n; i++)
        {
            int ind = start >= 0 ? start + i : start - i;
            var p = new ErmParam { Var = new ErmVarRef { Kind = kind, Index = ind } };
            int cur = rt.EraGetInt(p);
            if (!c.Apply(ref cur, i)) rt.EraSetInt(p, cur);
        }
    }

    static void F(ErmCall c, ErmParam target)
    {
        var rt = c.Rt;
        if (c.Num < 2 || c.Num > 4) throw new ErmRuntimeException("\"!!VR:F\" - expected 2-4 parameters");
        bool varInt = ErmRuntime.EraIsInt(target), varFloat = ErmRuntime.EraIsFloat(target);
        bool minNum = ErmRuntime.EraIsNumeric(c.P(0)), maxNum = ErmRuntime.EraIsNumeric(c.P(1));
        bool defNum = c.Num < 4 || ErmRuntime.EraIsNumeric(c.P(3));
        if (!((varInt || varFloat) && minNum && maxNum && defNum))
            throw new ErmRuntimeException("\"!!VR:F\" - only numeric variables and values are supported");
        int value = rt.EraGetInt(target);
        bool showErrors = c.Num >= 3 && c.N(2) != 0;
        if (varInt)
        {
            int min = ErmRuntime.EraIsFloat(c.P(0)) ? Trunc(Bits2F(c.N(0))) : c.N(0);
            int max = ErmRuntime.EraIsFloat(c.P(1)) ? Trunc(Bits2F(c.N(1))) : c.N(1);
            bool outOfBounds = false;
            if (value > max)
            {
                if (showErrors) rt.ReportError($"\"SN:F\" - value {value} is out of allowed range [{min}..{max}]. Forced value to range.");
                outOfBounds = true;
                value = max;
            }
            if (value < min)
            {
                if (showErrors) rt.ReportError($"\"SN:F\" - value {value} is out of allowed range [{min}..{max}]. Forced value to range.");
                outOfBounds = true;
                value = min;
            }
            if (outOfBounds && c.Num >= 4) value = c.N(3);
            rt.EraSetInt(target, value);
        }
        else
        {
            float fv = Bits2F(value);
            float min = ErmRuntime.EraIsInt(c.P(0)) ? c.N(0) : Bits2F(c.N(0));
            float max = ErmRuntime.EraIsInt(c.P(1)) ? c.N(1) : Bits2F(c.N(1));
            if (fv > max) { if (showErrors) rt.ReportError("\"SN:F\" - value is out of allowed range"); fv = max; }
            if (fv < min) { if (showErrors) rt.ReportError("\"SN:F\" - value is out of allowed range"); fv = min; }
            rt.EraSetInt(target, F2Bits(fv));
        }
    }

    static void Z(ErmCall c, ErmParam target)
    {
        var rt = c.Rt;
        if (!ErmRuntime.EraIsInt(target)) throw new ErmRuntimeException("\"!!VR:Z\" - base variable must be integer to store trigger local z-variable index");
        if (c.Num < 1 || !ErmRuntime.EraIsString(c.P(0))) throw new ErmRuntimeException("\"!!VR:Z\" - value must be string");
        rt.EraSetInt(target, rt.CreateTriggerLocalErtPublic(rt.EraZInterpolated(c.N(0))));
    }

    static void V(ErmCall c, ErmParam target)
    {
        var rt = c.Rt;
        if (!ErmRuntime.EraIsNumeric(target)) throw new ErmRuntimeException("\"!!VR:V\" - target variable type must be number (integer or float)");
        if (c.Num < 1 || !ErmRuntime.EraIsString(c.P(0))) throw new ErmRuntimeException("\"!!VR:V\" - value to convert to number must be of string type");
        string s = rt.EraZInterpolated(c.N(0));
        if (ErmRuntime.EraIsFloat(target)) rt.EraSetFloat(target, (float)VrReceiverHelpers.Atof(s));
        else rt.EraSetInt(target, VrReceiver.Atoi(s));
    }

    static void RandomOp(ErmCall c, ErmParam target)
    {
        var rt = c.Rt;
        if (!ErmRuntime.EraIsInt(target)) throw new ErmRuntimeException("\"!!VR\" - random functions can operate on integers only");
        if (c.Letter == 'R')
        {
            if (c.Num >= 4) rt.EraSetInt(target, rt.Services.Random.Next(c.N(1), c.N(2)));       // RandomRangeWithFreeParam
            else if (c.Num >= 3) rt.EraSetInt(target, rt.Services.Random.Next(c.N(1), c.N(2)));
            else if (c.Num >= 2) rt.Services.Random.Seed = c.N(1);
            else rt.EraSetInt(target, unchecked(rt.EraGetInt(target) + rt.Services.Random.Next(0, c.N(0))));
        }
        else
        {
            if (c.Num >= 3) rt.EraSetInt(target, UniqueRng.Next(c.N(1), c.N(2) + 1));
            else if (c.Num >= 2) throw new ErmRuntimeException("\"!!VR:T\" - it's forbidden to seed the unique generator");
            else rt.EraSetInt(target, unchecked(rt.EraGetInt(target) + UniqueRng.Next(0, c.N(0) + 1)));
        }
    }

    static readonly Random UniqueRng = new();

    static void Strings(ErmCall c, ErmParam target)
    {
        var rt = c.Rt;
        if (!ErmRuntime.EraIsString(target)) throw new ErmRuntimeException("\"!!VR\" - string functions work only with string variables");
        switch (c.Letter)
        {
            case 'H':
            {
                int fl = c.N(0);
                if (fl < 1 || fl > WoGVariables.FlagCount) throw new ErmRuntimeException("\"!!VR:H\" - invalid flag index: " + fl);
                rt.SetFlag(fl, !IsBlank(rt.EraGetText(target)));
                break;
            }
            case 'M':
                StringM(c, target);
                break;
            case 'U':
            {
                if (c.Num < 1 || !ErmRuntime.EraIsString(c.P(0))) throw new ErmRuntimeException("\"!!VR:U\" - expected substring, not a number");
                string hay = rt.EraGetText(target).Trim().ToLowerInvariant();
                string needle = rt.EraGetText(c.P(0)).Trim().ToLowerInvariant();
                rt.SetFlag(1, hay.Contains(needle, StringComparison.Ordinal));
                break;
            }
        }
    }

    static bool IsBlank(string s)
    {
        foreach (char ch in s) if (!(ch >= '\x01' && ch <= ' ')) return false;
        return true;
    }

    static void StringM(ErmCall c, ErmParam target)
    {
        var rt = c.Rt;
        switch (c.N(0))
        {
            case 1:
            {
                if (c.Num < 4) throw new ErmRuntimeException("\"!!VR:M1\" - insufficient parameters");
                rt.EraSetString(target, Substr(rt.EraZInterpolated(c.N(1)), c.N(2), c.N(3)));
                break;
            }
            case 2:
            {
                if (c.Num < 3) throw new ErmRuntimeException("\"!!VR:M2\" - insufficient parameters");
                rt.EraSetString(target, NthToken(rt.EraZInterpolated(c.N(1)), c.N(2)));
                break;
            }
            case 3:
            {
                if (c.Num < 2) throw new ErmRuntimeException("\"!!VR:M3\" - insufficient parameters");
                int radix = c.Num > 2 ? Math.Clamp(c.N(2), 2, 16) : 10;
                rt.EraSetString(target, VrReceiverHelpers.Itoa(c.N(1), radix));
                break;
            }
            case 4:
            {
                if (c.Num < 2) throw new ErmRuntimeException("\"!!VR:M4\" - insufficient parameters");
                rt.EraSetInt(c.P(1), rt.EraGetText(target).Length);
                break;
            }
            case 5:
            {
                if (c.Num < 2) throw new ErmRuntimeException("\"!!VR:M5\" - insufficient parameters");
                string s = rt.EraGetText(target);
                int i = 0;
                while (i < s.Length && s[i] >= '\x01' && s[i] <= ' ') i++;
                rt.EraSetInt(c.P(1), i >= s.Length ? -1 : i);
                break;
            }
            case 6:
            {
                if (c.Num < 2) throw new ErmRuntimeException("\"!!VR:M6\" - insufficient parameters");
                string s = rt.EraGetText(target);
                int trailing = 0;
                foreach (char ch in s) trailing = ch >= '\x01' && ch <= ' ' ? trailing + 1 : 0;
                rt.EraSetInt(c.P(1), s.Length - trailing - 1);
                break;
            }
        }
    }

    /// <summary>StrLib.Substr: 0-based start clamped to the string, count &lt; 0 = to the end.</summary>
    internal static string Substr(string s, int start, int count)
    {
        if (count == 0 || s.Length == 0) return "";
        start = Math.Max(0, Math.Min(start, s.Length));
        if (start >= s.Length) return "";
        count = count < 0 ? s.Length - start : Math.Min(s.Length - start, count);
        return s.Substring(start, count);
    }

    /// <summary>VrGetNthToken: tokens separated by blanks, ',' and '.'.</summary>
    internal static string NthToken(string s, int n)
    {
        static bool Delim(char ch) => (ch >= '\x01' && ch <= ' ') || ch == ',' || ch == '.';
        if (s.Length == 0 || n < 0) return "";
        int i = 0, cur = 0;
        while (i < s.Length && cur < n)
        {
            while (i < s.Length && !Delim(s[i])) i++;
            while (i < s.Length && Delim(s[i])) i++;
            cur++;
        }
        if (i >= s.Length) return "";
        int start = i;
        while (i < s.Length && !Delim(s[i])) i++;
        return s.Substring(start, i - start);
    }
}

/// <summary>Shared string helpers.</summary>
public static class VrReceiverHelpers
{
    public static string Itoa(int v, int radix)
    {
        if (radix == 10) return v.ToString(CultureInfo.InvariantCulture);
        uint u = unchecked((uint)v);
        if (u == 0) return "0";
        var sb = new System.Text.StringBuilder();
        while (u > 0) { uint d = u % (uint)radix; sb.Insert(0, (char)(d < 10 ? '0' + d : 'a' + d - 10)); u /= (uint)radix; }
        return sb.ToString();
    }

    public static double Atof(string s)
    {
        int i = 0;
        while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        int j = i;
        if (j < s.Length && (s[j] == '-' || s[j] == '+')) j++;
        while (j < s.Length && (char.IsDigit(s[j]) || s[j] == '.')) j++;
        return double.TryParse(s.AsSpan(i, j - i), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0;
    }
}

/// <summary>!!FU in Era: P (rewritten, Hook_FU_P), E, A and S (Hook_FU_EXT); D is network-only.</summary>
public sealed class EraFuReceiver : ErmReceiverBase
{
    public EraFuReceiver() : base("FU")
    {
        Declare("PEAS", CompatLevel.FullySupported);
        Declare("D", CompatLevel.Unsupported, "FU:D — network call (multiplayer is out of scope)");
    }

    protected override void Run(ErmCall c)
    {
        var rt = c.Rt;
        switch (c.Letter)
        {
            case 'P':
            {
                int fn = c.Selector();
                rt.EraSetupArgs(c, 16, 16);
                rt.ProcessEra(fn, rt.Context);
                rt.EraApplyFuncResults(c, c.Num);
                break;
            }
            case 'E':
                rt.TriggerBreak = true;
                break;
            case 'A':
                if (c.Num == 1)
                {
                    if (c.P(0).Mode != ErmParamMode.Get)
                    {
                        if (rt.NumFuncArgsReceived == 0) rt.X[0] = c.N(0);
                    }
                    else
                    {
                        int n = rt.NumFuncArgsReceived;
                        c.Apply(ref n, 0);
                        rt.NumFuncArgsReceived = n;
                    }
                }
                else
                {
                    for (int i = rt.NumFuncArgsReceived; i < c.Num && i < 16; i++) rt.X[i] = c.N(i);
                }
                break;
            case 'S':
            {
                if (c.Num != 2 || c.P(0).Mode == ErmParamMode.Get || c.P(1).Mode != ErmParamMode.Get || c.N(0) < 1 || c.N(0) > 16)
                    throw new ErmRuntimeException("Invalid !!FU:S syntax");
                int shift = (c.N(0) - 1) << 1;
                int res = (rt.FuncArgsGetSyntaxFlagsReceived & (3 << shift)) >> shift;
                c.Apply(ref res, 1);
                break;
            }
            default:
                throw ErmCall.WrongCommand(c.Letter);
        }
    }
}

/// <summary>!!DO in Era (DO_P): the whole event repeats through TriggerLoopCallback, x16 is the counter.</summary>
public sealed class EraDoReceiver : ErmReceiverBase
{
    public EraDoReceiver() : base("DO")
    {
        Declare("P", CompatLevel.FullySupported);
    }

    protected override void Run(ErmCall c)
    {
        if (c.Letter != 'P') throw new ErmRuntimeException("!!DO - wrong command");
        var rt = c.Rt;
        int fn = c.Selector(0);
        rt.ArgX[15] = c.Selector(1);
        int end = c.Selector(2);
        int step = c.Selector(3);
        rt.EraSetupArgs(c, 15, 15);
        int start = rt.ArgX[15];
        if ((step >= 0 && start <= end) || (step < 0 && start >= end))
        {
            rt.TriggerLoopCallback = () =>
            {
                rt.X[15] = unchecked(rt.X[15] + step);
                return (step >= 0 && rt.X[15] <= end) || (step < 0 && rt.X[15] >= end);
            };
            rt.ProcessEra(fn, rt.Context);
        }
        rt.EraApplyFuncResults(c, Math.Min(c.Num, 15));
    }
}

/// <summary>!!SN — Era service receiver (AdvErm.SN_Receiver).</summary>
public sealed class SnReceiver : ErmReceiverBase
{
    public SnReceiver() : base("SN")
    {
        Declare("WMVKXTCGQID", CompatLevel.FullySupported);
        Declare("H", CompatLevel.Unsupported, "SN:H — object/monster hints: needs an Olden Era UI adapter");
        Declare("O", CompatLevel.Unsupported, "SN:O — object entrance tile: needs a map adapter");
        Declare("P", CompatLevel.Unsupported, "SN:P — H3 sound playback: Olden Era sounds are different");
        Declare("S", CompatLevel.Unsupported, "SN:S — sound name in !?SN: the sound trigger is not ported");
        Declare("R", CompatLevel.Unsupported, "SN:R — H3 resource redirection (lod/def): Olden Era resources are different");
        Declare("F", CompatLevel.PartiallySupported, "SN:F — Era API functions: those used by the ERA Project scripts are ported (see EraApi); DLL/Win32 functions are not");
        Declare("E", CompatLevel.Unsupported, "SN:E — calling a function by address in the H3 exe: different engine");
        Declare("LAB", CompatLevel.Unsupported, "SN:L/A/B — DLL loading, addresses and H3 process memory: different engine");
    }

    protected override void Run(ErmCall c)
    {
        var rt = c.Rt;
        switch (c.Letter)
        {
            case 'W': W(c); break;
            case 'M': M(c); break;
            case 'V': V(c); break;
            case 'K': K(c); break;
            case 'X': Xc(c); break;
            case 'T': T(c); break;
            case 'C':
            {
                if (c.Num < 2 || !IsStr(c, 0) || c.IsGet(0) || IsStr(c, 1) || !c.IsGet(1)) throw Invalid();
                bool exists = rt.EraNames.Constants.TryGetValue(Text(c, 0), out int value);
                if (c.Num >= 3) RetInt(c, 2, exists ? 1 : 0);
                RetInt(c, 1, value);
                break;
            }
            case 'G':
                if (c.Num != 1 || c.IsGet(0) || IsStr(c, 0) || c.N(0) < 0 || c.N(0) == int.MaxValue) throw Invalid();
                rt.EraGoto(c.N(0));
                break;
            case 'Q':
                rt.QuitTriggerFlag = true;
                break;
            case 'I':
                if (c.Num < 2 || !IsStr(c, 0) || c.IsGet(0) || !IsStr(c, 1) || !c.IsGet(1)) throw new ErmRuntimeException("Invalid command syntax. Valid syntax is !!SN:Iz#/?z#");
                rt.EraSetString(c.P(1), rt.InterpolateEra(Text(c, 0)));
                break;
            case 'D':
                // redraw of the current H3 screen: Olden Era redraws by itself
                break;
            case 'F':
                EraApi.Call(c);
                break;
            default:
                throw new ErmRuntimeException($"Unknown command \"!!SN:{c.Letter}\"");
        }
    }

    static ErmRuntimeException Invalid() => new("Invalid command parameters");

    // ---- TServiceParam view -------------------------------------------------------------------

    static bool IsStr(ErmCall c, int i) => ErmRuntime.EraIsString(c.P(i));

    /// <summary>Service string value: z vars are read raw (ErmVarToServiceParam), literals/s^^ interpolated as parsed.</summary>
    static string Text(ErmCall c, int i)
    {
        var p = c.P(i);
        if (ErmRuntime.EraTypeOf(p) == ErmVarKind.Z) return c.Rt.EraZRaw(c.Rt.EraResolvedIndex(p));
        return c.Rt.EraGetText(p);
    }

    static void RetInt(ErmCall c, int i, int v) => c.Rt.EraSetInt(c.P(i), v);
    static void RetStr(ErmCall c, int i, string s) => c.Rt.EraSetString(c.P(i), s);

    /// <summary>ApplyIntParam — including Era's bug: d| performs AND; shifts clamp to 0..100.</summary>
    static int ApplyInt(ErmCall c, int i, int dest)
    {
        var p = c.P(i);
        if (p.Mode == ErmParamMode.Get) { RetInt(c, i, dest); return dest; }
        int v = c.N(i);
        switch (p.Modifier)
        {
            case ErmModifier.None: return v;
            case ErmModifier.Add: return unchecked(dest + v);
            case ErmModifier.Sub: return unchecked(dest - v);
            case ErmModifier.Mul: return unchecked(dest * v);
            case ErmModifier.Div:
                if (v == 0) { c.Rt.ReportError("Division by zero in d-modifier"); return dest; }
                return v == -1 ? unchecked(-dest) : dest / v;
            case ErmModifier.Mod:
                if (v == 0) { c.Rt.ReportError("Division by zero in d-modifier"); return dest; }
                return v == -1 ? 0 : dest % v;
            case ErmModifier.Or: return dest & v;
            case ErmModifier.AndNot: return dest & ~v;
            case ErmModifier.Shl: return dest << Math.Clamp(v, 0, 100);
            case ErmModifier.Shr: return (int)((uint)dest >> Math.Clamp(v, 0, 100));
            default: return dest;
        }
    }

    /// <summary>AssignStrFromParam: d&amp; concatenates.</summary>
    static string AssignStr(ErmCall c, int i, string dest) =>
        c.P(i).Modifier == ErmModifier.Concat ? dest + Text(c, i) : Text(c, i);

    // ---- SN:W -----------------------------------------------------------------------------------

    static void W(ErmCall c)
    {
        var st = c.Rt.Services.State.Era;
        string Name() => IsStr(c, 0) ? Text(c, 0) : c.N(0).ToString(CultureInfo.InvariantCulture);
        switch (c.Num)
        {
            case 0:
                st.Assoc.Clear();
                break;
            case 1:
                if (c.IsGet(0)) throw Invalid();
                st.Assoc.Remove(Name());
                break;
            case 2:
            {
                if (c.IsGet(0)) throw Invalid();
                string name = Name();
                st.Assoc.TryGetValue(name, out var av);
                if (c.IsGet(1))
                {
                    if (IsStr(c, 1)) RetStr(c, 1, av?.Str ?? "");
                    else RetInt(c, 1, av?.Int ?? 0);
                }
                else
                {
                    av ??= st.GetOrCreateAssoc(name);
                    if (IsStr(c, 1)) av.Str = AssignStr(c, 1, av.Str);
                    else av.Int = ApplyInt(c, 1, av.Int);
                }
                break;
            }
            default:
                throw new ErmRuntimeException("Invalid number of command parameters");
        }
    }

    // ---- SN:M / SN:V ----------------------------------------------------------------------------

    static void M(ErmCall c)
    {
        var rt = c.Rt;
        var st = rt.Services.State.Era;
        switch (c.Num)
        {
            case 0:
                st.Slots.Clear();
                break;
            case 1:
                if (IsStr(c, 0) || c.IsGet(0) || c.N(0) == -1) throw Invalid();
                st.Slots.Remove(c.N(0));
                break;
            case 2:
            {
                if (IsStr(c, 0) || c.IsGet(0) || IsStr(c, 1) || !(c.IsGet(1) || c.N(1) >= 0)) throw Invalid();
                if (c.IsGet(1))
                {
                    RetInt(c, 1, st.Slots.TryGetValue(c.N(0), out var s) ? s.Count : -1);
                }
                else
                {
                    var slot = GetSlot(st, c.N(0));
                    int count = ApplyInt(c, 1, slot.Count);
                    if (count < 0) throw new ErmRuntimeException("Negative array size");
                    slot.Resize(count);
                }
                break;
            }
            case 3:
            {
                if (IsStr(c, 0) || c.IsGet(0) || IsStr(c, 1)) throw Invalid();
                var slot = GetSlot(st, c.N(0));
                if (c.IsGet(1)) throw new ErmUnsupportedException("SN:M#/?addr/# — address of an array element in H3 memory");
                int ind = c.N(1);
                if (ind < 0) ind += slot.Count;
                if (c.IsGet(1) || ind < 0 || ind >= slot.Count) throw new ErmRuntimeException($"Invalid array index {c.N(1)} for array {c.N(0)} of length {slot.Count}");
                if (c.IsGet(2))
                {
                    if (slot.IsString) RetStr(c, 2, slot.Strs[ind]);
                    else RetInt(c, 2, slot.Ints[ind]);
                }
                else
                {
                    if (slot.IsString) slot.Strs[ind] = AssignStr(c, 2, slot.Strs[ind]);
                    else slot.Ints[ind] = ApplyInt(c, 2, slot.Ints[ind]);
                }
                break;
            }
            case 4:
            case 5:
            {
                if (c.IsGet(1) || c.IsGet(2) || c.IsGet(3))
                {
                    if (IsStr(c, 0) || c.IsGet(0)) throw Invalid();
                    if (!st.Slots.TryGetValue(c.N(0), out var s)) throw new ErmRuntimeException($"Slot #{c.N(0)} does not exist");
                    if (c.IsGet(1)) RetInt(c, 1, s.Count);
                    if (c.IsGet(2)) RetInt(c, 2, s.IsString ? 1 : 0);
                    if (c.IsGet(3)) RetInt(c, 3, (int)s.Storage);
                    if (c.Num >= 5 && c.IsGet(4)) throw new ErmUnsupportedException("SN:M — address of an array in H3 memory");
                    break;
                }
                int id = c.N(0), count = c.N(1), type = c.N(2), storage = c.N(3);
                bool ok = !IsStr(c, 0) && !IsStr(c, 1) && !IsStr(c, 2) && !IsStr(c, 3)
                          && (c.Num < 5 || (!IsStr(c, 4) && c.IsGet(4)))
                          && id >= -1 && count >= 0 && type >= 0 && type <= 1
                          && (storage == 0 || storage == 1 || (storage == -1 && id == -1));
                if (!ok) throw Invalid();
                var slot = new EraSlot { IsString = type == 1, Storage = (EraSlotStorage)storage };
                slot.Resize(count);
                if (id == -1)
                {
                    while (st.Slots.ContainsKey(st.FreeSlotN)) st.FreeSlotN--;
                    id = st.FreeSlotN;
                    st.FreeSlotN--;
                }
                st.Slots[id] = slot;
                if (c.Num >= 5) RetInt(c, 4, id);
                else rt.Services.State.Erm.V[0] = id;
                if (storage == -1) rt.RegisterTriggerLocalSlot(id);
                break;
            }
            default:
                throw new ErmRuntimeException("Invalid number of command parameters");
        }
    }

    static EraSlot GetSlot(EraState st, int id) =>
        st.Slots.TryGetValue(id, out var s) ? s : throw new ErmRuntimeException($"Slot #{id} does not exist.");

    static void V(ErmCall c)
    {
        var st = c.Rt.Services.State.Era;
        if (c.Num < 2 || IsStr(c, 0) || c.IsGet(0) || IsStr(c, 1) || c.IsGet(1)) throw Invalid();
        if (!st.Slots.TryGetValue(c.N(0), out var slot)) throw new ErmRuntimeException($"Failed to find SN:M array with ID {c.N(0)}");
        int start = c.N(1);
        if (start < 0) start += slot.Count;
        if (start < 0 || start >= slot.Count)
            throw new ErmRuntimeException($"Invalid starting dynamical array index: {start} for array with ID {c.N(0)} and length {slot.Count}");
        for (int i = 2; i < c.Num; i++)
        {
            int ind = start + i - 2;
            if (ind >= slot.Count) throw new ErmRuntimeException($"Index {ind} is out of bounds for dynamical array with ID {c.N(0)} and length {slot.Count}");
            if (IsStr(c, i) != slot.IsString) throw new ErmRuntimeException("Cannot get INTEGER/STRING item into variable of not appropriate type");
            if (c.IsGet(i))
            {
                if (slot.IsString) RetStr(c, i, slot.Strs[ind]);
                else RetInt(c, i, slot.Ints[ind]);
            }
            else
            {
                if (slot.IsString) slot.Strs[ind] = AssignStr(c, i, slot.Strs[ind]);
                else slot.Ints[ind] = ApplyInt(c, i, slot.Ints[ind]);
            }
        }
    }

    // ---- SN:K / SN:X / SN:T ----------------------------------------------------------------------

    static void K(ErmCall c)
    {
        switch (c.Num)
        {
            case 2:
                if (!IsStr(c, 0) || c.IsGet(0) || IsStr(c, 1) || !c.IsGet(1)) throw Invalid();
                RetInt(c, 1, Text(c, 0).Length);
                break;
            case 3:
            {
                if (c.IsGet(0) || IsStr(c, 1) || c.IsGet(1) || c.N(1) < 0) throw Invalid();
                string s = Text(c, 0);
                int ind = c.N(1);
                if (c.IsGet(2))
                {
                    if (IsStr(c, 2)) RetStr(c, 2, ind < s.Length ? s[ind].ToString() : "");
                    // Era returns the code of the *first* character here (it reads Params[0].Value.pc^)
                    else RetInt(c, 2, s.Length > 0 ? s[0] : 0);
                }
                else
                {
                    if (!IsStr(c, 2)) throw new ErmUnsupportedException("SN:K#/#/# — writing a character to an address given as a number");
                    string ch = Text(c, 2);
                    var chars = s.ToCharArray();
                    if (ind < chars.Length) chars[ind] = ch.Length > 0 ? ch[0] : '\0';
                    string res = new string(chars);
                    int nul = res.IndexOf('\0');
                    if (nul >= 0) res = res.Substring(0, nul);
                    c.Rt.EraSetString(c.P(0), res);
                }
                break;
            }
            case 4:
                throw new ErmUnsupportedException("SN:K#/#/#/# — copying H3 process memory");
            default:
                throw new ErmRuntimeException("Invalid number of command parameters");
        }
    }

    static void Xc(ErmCall c)
    {
        var rt = c.Rt;
        if (c.Num > 16) throw Invalid();
        for (int i = 0; i < c.Num; i++)
        {
            if (c.IsGet(i))
            {
                if (IsStr(c, i)) RetStr(c, i, rt.EraZInterpolated(rt.X[i]));
                else RetInt(c, i, rt.X[i]);
            }
            else
            {
                // Era stores a pointer to the string; the port stores a trigger-local string index instead
                if (IsStr(c, i)) rt.X[i] = rt.CreateTriggerLocalErtPublic(Text(c, i));
                else rt.X[i] = ApplyInt(c, i, rt.X[i]);
            }
        }
    }

    static void T(ErmCall c)
    {
        if (c.Num < 2) throw new ErmRuntimeException("Invalid number of command parameters");
        if (!IsStr(c, 0) || c.IsGet(0) || !IsStr(c, 1) || !c.IsGet(1))
            throw new ErmRuntimeException("Valid syntax is !!SN:T^key^/?(str result)/...parameters...");
        int numTr = (c.Num - 2) / 2;
        var args = new List<string>();
        for (int i = 2; i < 2 + numTr * 2; i++)
        {
            if (c.IsGet(i)) throw new ErmRuntimeException("Arguments for translation must use set syntax");
            args.Add(IsStr(c, i) ? Text(c, i) : c.N(i).ToString(CultureInfo.InvariantCulture));
        }
        args.Add("");
        args.Add(EraTemplateEscape);
        RetStr(c, 1, c.Rt.Lang.Tr(Text(c, 0), args));
    }

    const string EraTemplateEscape = "@";
}
