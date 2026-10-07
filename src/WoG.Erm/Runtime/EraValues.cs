using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using WoG.Core.State;
using WoG.Erm.Era;
using WoG.Erm.Syntax;

namespace WoG.Erm.Runtime;

/// <summary>VALTYPE_* of Era.</summary>
public enum EraValType { Int = 0, Float = 1, Bool = 2, Str = 3, Error = 999 }

/// <summary>
/// Era value semantics (Erm.pas GetErmParamValue / SetErmParamValue / InterpolateErmStr / CheckFlags and the
/// trigger-local and command-local ERT strings).
/// </summary>
public sealed partial class ErmRuntime
{
    public const int FirstLocalErtIndex = 1_000_000_000;
    public const int LastLocalErtIndex = 2_000_000_000;
    const int EraNzCount = 10;
    const int MaxInterpolationLevel = 5;

    public bool IsEra => Options.Dialect == ErmDialect.Era;
    EraState EraSt => Services.State.Era;

    EraNames? eraNames;
    /// <summary>Era name tables bound to the current game state.</summary>
    public EraNames EraNames
    {
        get
        {
            if (eraNames == null || !ReferenceEquals(eraNames.State, EraSt)) eraNames = new EraNames(EraSt);
            return eraNames;
        }
    }

    /// <summary>Era translations (Lang/*.json); %T(key) and SN:T read it.</summary>
    public EraLang Lang { get; set; } = new();
    /// <summary>Era's cached ini files (SN:F ReadStrFromIni/WriteStrToIni/SaveIni…); the host sets its folders.</summary>
    public WoG.Erm.Era.EraIni Ini { get; set; } = new(System.IO.Directory.GetCurrentDirectory());

    // ---- local ERT strings (never saved) -------------------------------------------------------
    readonly Dictionary<int, string> localErt = new();
    int localErtAutoIndex = FirstLocalErtIndex;
    List<int>? cmdLocalErt;

    int AllocLocalErtIndex()
    {
        int r = localErtAutoIndex++;
        if (localErtAutoIndex >= LastLocalErtIndex) localErtAutoIndex = FirstLocalErtIndex;
        return r;
    }

    /// <summary>CreateCmdLocalErt: a string that lives until the current receiver line finishes.</summary>
    internal int CreateCmdLocalErt(string s)
    {
        int i = AllocLocalErtIndex();
        localErt[i] = s;
        (cmdLocalErt ??= new List<int>()).Add(i);
        return i;
    }

    /// <summary>CreateTriggerLocalErt: a string that lives until the current trigger ends.</summary>
    public int CreateTriggerLocalErtPublic(string s) => CreateTriggerLocalErt(s);

    internal int CreateTriggerLocalErt(string s)
    {
        int i = AllocLocalErtIndex();
        localErt[i] = s;
        if (eraFrame != null) eraFrame.LocalErt.Add(i);
        return i;
    }

    void FreeCmdLocal(List<int>? previous)
    {
        if (cmdLocalErt != null) foreach (int i in cmdLocalErt) localErt.Remove(i);
        cmdLocalErt = previous;
    }

    /// <summary>ZvsGetErtStr (Era storage): ERT file strings and local ERT strings.</summary>
    string ErtString(int index)
    {
        if (localErt.TryGetValue(index, out var s)) return s;
        if (EraSt.Ert.TryGetValue(index, out s)) return s;
        return "STRING NOT FOUND";
    }

    static bool IsLocalErt(int index) => index >= FirstLocalErtIndex && index <= LastLocalErtIndex;

    // ---- z variables ---------------------------------------------------------------------------

    /// <summary>GetZVarAddr (no interpolation).</summary>
    public string EraZRaw(int ind)
    {
        if (ind > WoGVariables.ZCount) return ErtString(ind);
        if (ind >= 1) return Vars.Z[ind - 1];
        if (-ind >= 1 && -ind <= EraNzCount) return lz[-ind - 1];
        Error(null, "Invalid z-var index: " + ind);
        return "STRING NOT FOUND";
    }

    /// <summary>GetInterpolatedZVarAddr: ERT strings (except local ones) are interpolated, z1..z1000 / z-1..z-10 are not.</summary>
    public string EraZInterpolated(int ind)
    {
        if (ind > WoGVariables.ZCount)
        {
            string s = ErtString(ind);
            return IsLocalErt(ind) ? s : InterpolateEra(s);
        }
        return EraZRaw(ind);
    }

    static bool IsMutableZ(int ind) => (ind >= 1 && ind <= WoGVariables.ZCount) || (-ind >= 1 && -ind <= EraNzCount);

    void SetEraZ(int ind, string value)
    {
        value = WoGVariables.Clip(value);
        if (ind >= 1 && ind <= WoGVariables.ZCount) Vars.Z[ind - 1] = value;
        else if (-ind >= 1 && -ind <= EraNzCount) lz[-ind - 1] = value;
        else throw new ErmRuntimeException($"Invalid z-var index: {ind}. Expected -10..-1, 1..1000");
    }

    // ---- parameters ----------------------------------------------------------------------------

    static bool IsEraIntType(ErmVarKind k) => k == ErmVarKind.None || k == ErmVarKind.Quick || k == ErmVarKind.V
        || k == ErmVarKind.W || k == ErmVarKind.X || k == ErmVarKind.Y || k == ErmVarKind.AssocI;
    static bool IsEraStrType(ErmVarKind k) => k == ErmVarKind.Z || k == ErmVarKind.AssocS || k == ErmVarKind.Str;

    /// <summary>The Era parameter type (GetType): None = numeric constant.</summary>
    public static ErmVarKind EraTypeOf(ErmParam p) => p.Var?.Kind ?? ErmVarKind.None;

    public static bool EraIsInt(ErmParam p) => IsEraIntType(EraTypeOf(p));
    public static bool EraIsFloat(ErmParam p) => EraTypeOf(p) == ErmVarKind.E;
    public static bool EraIsNumeric(ErmParam p) => EraIsInt(p) || EraIsFloat(p);
    public static bool EraIsString(ErmParam p) => IsEraStrType(EraTypeOf(p));

    string AssocName(ErmVarRef r) => r.NeedsInterpolation ? InterpolateEra(r.Name ?? "") : r.Name ?? "";

    /// <summary>Index part of an indexed parameter (must be an integer).</summary>
    int EraIndex(ErmVarRef r)
    {
        switch (r.IndexKind)
        {
            case ErmVarKind.None: return r.Index;
            case ErmVarKind.Quick:
            case ErmVarKind.V:
            case ErmVarKind.W:
            case ErmVarKind.X:
            case ErmVarKind.Y:
                return EraReadInt(r.IndexKind, r.Index);
            case ErmVarKind.AssocI:
                return EraSt.Assoc.TryGetValue(AssocName(r), out var a) ? a.Int : 0;
            default:
                throw new ErmRuntimeException("Cannot use non-integer variables as indexes for other variables");
        }
    }

    int EraReadInt(ErmVarKind kind, int ind)
    {
        switch (kind)
        {
            case ErmVarKind.Y:
                if (ind >= 1 && ind <= 100) return y[ind - 1];
                if (-ind >= 1 && -ind <= 100) return yt[-ind - 1];
                throw new ErmRuntimeException($"Invalid y-var index: {ind}. Expected -100..-1, 1..100");
            case ErmVarKind.Quick:
                if (ind < 1 || ind > WoGVariables.QuickCount) throw new ErmRuntimeException($"Invalid quick var {ind}. Expected 1..{WoGVariables.QuickCount}");
                return Vars.Quick[ind - 1];
            case ErmVarKind.X:
                if (ind < 1 || ind > 16) throw new ErmRuntimeException($"Invalid x-var index {ind}. Expected 1..16");
                return X[ind - 1];
            case ErmVarKind.V:
                if (ind < 1 || ind > WoGVariables.VCount) throw new ErmRuntimeException($"Invalid v-var index {ind}. Expected 1..{WoGVariables.VCount}");
                return Vars.V[ind - 1];
            case ErmVarKind.W:
                if (ind < 1 || ind > WoGVariables.WCount) throw new ErmRuntimeException($"Invalid v-var index {ind}. Expected 1..{WoGVariables.WCount}");
                return Vars.WFor(Vars.CurrentWHero)[ind - 1];
            default:
                throw new ErmRuntimeException("Unknown variable type");
        }
    }

    float EraReadFloat(int ind)
    {
        if (ind >= 1 && ind <= 100) return f[ind - 1];
        if (-ind >= 1 && -ind <= 100) return ft[-ind - 1];
        throw new ErmRuntimeException($"Invalid e-var index: {ind}. Expected -100..-1, 1..100");
    }

    /// <summary>
    /// GetErmParamValue. With <paramref name="strAsText"/> (FLAG_STR_EVALS_TO_ADDR_NOT_INDEX) strings come back
    /// as text in <paramref name="str"/>; otherwise the int is a string index (z index or a command-local
    /// ERT index for literals and s^^). Floats return their bit pattern as the int (like Era).
    /// </summary>
    public int EraGetValue(ErmParam p, out EraValType type, out string? str, bool strAsText = false)
    {
        str = null;
        p = ResolveMacro(p);
        type = EraValType.Int;
        if (p.Var == null) return Day(p, p.Number);
        var r = p.Var;
        switch (r.Kind)
        {
            case ErmVarKind.Str:
            {
                type = EraValType.Str;
                string text = r.NeedsInterpolation ? InterpolateEra(r.Name ?? "") : r.Name ?? "";
                if (strAsText) { str = text; return 0; }
                return CreateCmdLocalErt(text);
            }
            case ErmVarKind.AssocI:
                return Day(p, EraSt.Assoc.TryGetValue(AssocName(r), out var ai) ? ai.Int : 0);
            case ErmVarKind.AssocS:
            {
                type = EraValType.Str;
                string text = EraSt.Assoc.TryGetValue(AssocName(r), out var asv) ? asv.Str : "";
                if (strAsText) { str = text; return 0; }
                return CreateCmdLocalErt(text);
            }
            case ErmVarKind.Quick:
                if (r.IndexKind == ErmVarKind.None) return Day(p, EraReadInt(ErmVarKind.Quick, r.Index));
                break;
        }
        int ind = EraIndex(r);
        switch (r.Kind)
        {
            case ErmVarKind.Y:
            case ErmVarKind.X:
            case ErmVarKind.V:
            case ErmVarKind.W:
            case ErmVarKind.Quick:
                return Day(p, EraReadInt(r.Kind, ind));
            case ErmVarKind.E:
                type = EraValType.Float;
                return BitConverter.SingleToInt32Bits(EraReadFloat(ind));
            case ErmVarKind.Z:
                type = EraValType.Str;
                if (strAsText)
                {
                    if (ind >= 1 && ind <= WoGVariables.ZCount) { str = Vars.Z[ind - 1]; return 0; }
                    if (-ind >= 1 && -ind <= EraNzCount) { str = lz[-ind - 1]; return 0; }
                    if (ind > WoGVariables.ZCount) { str = EraZInterpolated(ind); return 0; }
                    throw new ErmRuntimeException($"Invalid z-var index: {ind}. Expected -10..-1, 1+");
                }
                if (!(ind >= 1 || (-ind >= 1 && -ind <= EraNzCount)))
                    throw new ErmRuntimeException($"Invalid z-var index: {ind}. Expected -10..-1, 1+");
                return ind;
            case ErmVarKind.Flag:
                type = EraValType.Bool;
                if (ind < 1 || ind > WoGVariables.FlagCount) throw new ErmRuntimeException($"Invalid flag index {ind}. Expected 1..1000");
                return Vars.Flags[ind - 1] ? 1 : 0;
            default:
                throw new ErmRuntimeException($"Unknown variable type: {r.Kind}");
        }
    }

    int Day(ErmParam p, int v) => p.DayRelative ? unchecked(v + Services.Game.Clock.AbsoluteDay) : v;

    public int EraGetInt(ErmParam p) => EraGetValue(p, out _, out _);

    /// <summary>Text value of a string parameter (or of an int one, printed), as Era's ApplyString/NewMesMan see it.</summary>
    public string EraGetText(ErmParam p)
    {
        int v = EraGetValue(p, out var t, out var s, strAsText: true);
        return t == EraValType.Str ? s ?? "" : throw new ErmRuntimeException("cannot assign non-string value");
    }

    /// <summary>SetErmParamValue for integers (and float bit patterns for e-vars).</summary>
    public void EraSetInt(ErmParam p, int value)
    {
        p = ResolveMacro(p);
        if (p.Var == null) throw new ErmRuntimeException($"Cannot use GET syntax with number: ?{p.Number}");
        var r = p.Var;
        switch (r.Kind)
        {
            case ErmVarKind.AssocI:
                EraSt.GetOrCreateAssoc(AssocName(r)).Int = value;
                return;
            case ErmVarKind.Str:
            case ErmVarKind.AssocS:
            case ErmVarKind.Z:
                throw new ErmRuntimeException($"SetErmParamValue: Unsupported variable type: {r.Kind}");
            case ErmVarKind.Flag:
                throw new ErmRuntimeException("Cannot use GET syntax with flags");
        }
        int ind = EraIndex(r);
        switch (r.Kind)
        {
            case ErmVarKind.Y:
                if (ind >= 1 && ind <= 100) y[ind - 1] = value;
                else if (-ind >= 1 && -ind <= 100) yt[-ind - 1] = value;
                else throw new ErmRuntimeException($"Invalid y-var index: {ind}. Expected -100..-1, 1..100");
                return;
            case ErmVarKind.Quick:
                if (ind < 1 || ind > WoGVariables.QuickCount) throw new ErmRuntimeException($"Invalid quick var {ind}");
                Vars.Quick[ind - 1] = value;
                return;
            case ErmVarKind.X:
                if (ind < 1 || ind > 16) throw new ErmRuntimeException($"Invalid x-var index {ind}. Expected 1..16");
                X[ind - 1] = value;
                return;
            case ErmVarKind.V:
                if (ind < 1 || ind > WoGVariables.VCount) throw new ErmRuntimeException($"Invalid v-var index {ind}");
                Vars.V[ind - 1] = value;
                return;
            case ErmVarKind.W:
                if (ind < 1 || ind > WoGVariables.WCount) throw new ErmRuntimeException($"Invalid w-var index {ind}");
                Vars.WFor(Vars.CurrentWHero)[ind - 1] = value;
                return;
            case ErmVarKind.E:
                float fv = BitConverter.Int32BitsToSingle(value);
                if (ind >= 1 && ind <= 100) f[ind - 1] = fv;
                else if (-ind >= 1 && -ind <= 100) ft[-ind - 1] = fv;
                else throw new ErmRuntimeException($"Invalid e-var index: {ind}. Expected -100..-1, 1..100");
                return;
            default:
                throw new ErmRuntimeException($"SetErmParamValue: Unsupported value type: {r.Kind}");
        }
    }

    public void EraSetFloat(ErmParam p, float value) => EraSetInt(p, BitConverter.SingleToInt32Bits(value));

    /// <summary>ZvsGetVarValIndex: the final index of a variable parameter (index part resolved).</summary>
    public int EraResolvedIndex(ErmParam p)
    {
        p = ResolveMacro(p);
        if (p.Var == null) return p.Number;
        return EraIndex(p.Var);
    }

    /// <summary>ShowErmError equivalent for receivers.</summary>
    public void ReportError(string message) => Error(null, message);

    /// <summary>ExtendArrayLifetime: hands a trigger-local array over to the calling trigger.</summary>
    public void ExtendArrayLifetime(int id)
    {
        if (eraFrame?.Prev == null) return;
        if (eraFrame.LocalSlots.Remove(id)) eraFrame.Prev.LocalSlots.Add(id);
    }

    /// <summary>SN:M ... /-1 storage: the array is deleted when the current trigger ends (TSlotReleaser).</summary>
    public void RegisterTriggerLocalSlot(int id)
    {
        if (eraFrame != null) eraFrame.LocalSlots.Add(id);
    }

    /// <summary>SetErmParamValue with FLAG_ASSIGNABLE_STRINGS for z / s^^ targets.</summary>
    public void EraSetString(ErmParam p, string value)
    {
        p = ResolveMacro(p);
        var r = p.Var ?? throw new ErmRuntimeException($"Cannot use GET syntax with number: ?{p.Number}");
        if (r.Kind == ErmVarKind.AssocS)
        {
            EraSt.GetOrCreateAssoc(AssocName(r)).Str = value;
            return;
        }
        if (r.Kind == ErmVarKind.Z)
        {
            SetEraZ(EraIndex(r), value);
            return;
        }
        throw new ErmRuntimeException($"SetErmParamValue: Unsupported value type: {r.Kind}");
    }

    /// <summary>PutVal: applies an Era d-modifier.</summary>
    public int EraModify(int orig, int value, ErmModifier m)
    {
        switch (m)
        {
            case ErmModifier.None: return value;
            case ErmModifier.Add: return unchecked(orig + value);
            case ErmModifier.Sub: return unchecked(orig - value);
            case ErmModifier.Mul: return unchecked(orig * value);
            case ErmModifier.Or: return orig | value;
            case ErmModifier.AndNot: return orig & ~value;
            case ErmModifier.Shl: return orig << Math.Clamp(value, 0, 32);
            case ErmModifier.Shr: return (int)((uint)orig >> Math.Clamp(value, 0, 32));
            case ErmModifier.Div:
                if (value == 0) { Error(null, "Division by zero in d-modifier"); return value; }
                return value == -1 ? unchecked(-orig) : orig / value;
            case ErmModifier.Mod:
                if (value == 0) { Error(null, "Division by zero in d-modifier"); return value; }
                return value == -1 ? 0 : orig % value;
            default: return value;
        }
    }

    // ---- conditions (Hook_ZvsCheckFlags) -------------------------------------------------------

    /// <summary>True when the condition passes (the command/section runs).</summary>
    public bool CheckConditionEra(ErmCondition c)
    {
        if (c.IsEmpty) return true;
        try
        {
            bool andRes = c.And.Count > 0;
            foreach (var item in c.And)
                if (!ItemEra(item)) { andRes = false; break; }
            bool orRes = false;
            foreach (var item in c.Or)
                if (ItemEra(item)) { orRes = true; break; }
            return andRes || orRes;
        }
        catch (ErmRuntimeException ex)
        {
            Error(null, "CheckFlags: " + ex.Message);
            return false;
        }
    }

    bool ItemEra(ErmCondItem item)
    {
        if (item.Flag is int fl)
        {
            if (fl < 1 || fl > WoGVariables.FlagCount) throw new ErmRuntimeException($"Invalid flag index {fl}. Expected 1..1000");
            return Vars.Flags[fl - 1] == item.FlagSet;
        }
        var left = item.Left!;
        int v1 = EraGetValue(left, out var t1, out var s1, strAsText: true);
        int v2;
        EraValType t2;
        string? s2;
        ErmCompare cmp;
        if (item.Right == null)
        {
            // single value cast to boolean: <> 0 / <> ""
            cmp = ErmCompare.Ne;
            if (t1 == EraValType.Str) { t2 = EraValType.Str; s2 = ""; v2 = 0; }
            else { t2 = EraValType.Int; s2 = null; v2 = 0; }
        }
        else
        {
            v2 = EraGetValue(item.Right, out t2, out s2, strAsText: true);
            cmp = item.Right.Compare;
        }

        int res;
        bool num1 = t1 == EraValType.Int || t1 == EraValType.Float;
        bool num2 = t2 == EraValType.Int || t2 == EraValType.Float;
        if (t1 == EraValType.Bool)
        {
            return v1 != 0; // flags only come as single values
        }
        if (num1 || num2)
        {
            if (t1 == EraValType.Float || t2 == EraValType.Float)
            {
                float a = t1 == EraValType.Int ? v1 : t1 == EraValType.Float ? BitConverter.Int32BitsToSingle(v1)
                    : throw new ErmRuntimeException("CheckFlags: Cannot compare float variable to non-numeric value");
                float b = t2 == EraValType.Int ? v2 : t2 == EraValType.Float ? BitConverter.Int32BitsToSingle(v2)
                    : throw new ErmRuntimeException("CheckFlags: Cannot compare float variable to non-numeric value");
                res = a > b ? 1 : a < b ? -1 : 0;
            }
            else
            {
                if (t1 != EraValType.Int || t2 != EraValType.Int)
                    throw new ErmRuntimeException("CheckFlags: Cannot compare integer variable to non-numeric value");
                res = v1 > v2 ? 1 : v1 < v2 ? -1 : 0;
            }
        }
        else if (t1 == EraValType.Str && t2 == EraValType.Str)
        {
            res = Math.Sign(string.CompareOrdinal(s1 ?? "", s2 ?? ""));
        }
        else throw new ErmRuntimeException("CheckFlags: Cannot compare values of incompatible types");
        return Compare(res, cmp);
    }

    // ---- interpolation (InterpolateErmStr) -----------------------------------------------------

    int interpolationLevel;

    public string InterpolateEra(string str)
    {
        if (interpolationLevel >= MaxInterpolationLevel) return str;
        interpolationLevel++;
        try
        {
            var res = new StringBuilder(str.Length + 16);
            int i = 0;
            while (i < str.Length)
            {
                int pct = str.IndexOf('%', i);
                if (pct < 0) { res.Append(str, i, str.Length - i); break; }
                res.Append(str, i, pct - i);
                i = pct;
                int chunkStart = i;
                char c1 = At(str, i + 1);
                if (c1 == '%') { res.Append('%'); i += 2; continue; }
                if (c1 == '\\')
                {
                    char c2 = At(str, i + 2);
                    if (c2 == ':') { res.Append(';'); i += 3; }
                    else if (c2 == '"') { res.Append('^'); i += 3; }
                    else { res.Append("%\\"); i += 2; }
                    continue;
                }
                i++;
                char c = At(str, i);
                if (c == 'V' || c == 'Y' || c == 'X' || c == 'Z' || c == 'E' || c == 'W' || c == 'S' || c == 'I')
                    c = char.ToLowerInvariant(c);
                if (c == 'D')
                {
                    var clock = Services.Game.Clock;
                    switch (At(str, i + 1))
                    {
                        case 'd': res.Append(clock.DayOfWeek); break;
                        case 'w': res.Append(clock.Week); break;
                        case 'm': res.Append(clock.Month); break;
                        case 'a': res.Append(clock.AbsoluteDay); break;
                        default:
                            Error(null, "*InterpolateErmStr: invalid %Dx syntax");
                            res.Append(str, chunkStart, i - chunkStart);
                            continue;
                    }
                    i += 2;
                    continue;
                }
                if (c == 'G' && At(str, i + 1) == 'c')
                {
                    i += 2;
                    res.Append(PlayerColorName(Services.Game.Players.CurrentPlayer));
                    continue;
                }
                if (c == 'T' && At(str, i + 1) == '(')
                {
                    int close = str.IndexOf(')', i + 2);
                    if (close < 0)
                    {
                        Error(null, "*InterpolateErmStr: missing %T closing parenthesis \")\"");
                        i = str.Length;
                        res.Append(str, chunkStart, i - chunkStart);
                        continue;
                    }
                    res.Append(Lang.Tr(str.Substring(i + 2, close - i - 2)));
                    i = close + 1;
                    continue;
                }
                if (At(str, i) == 'V' && At(str, i + 1) >= 'f' && At(str, i + 1) <= 't')
                {
                    res.Append(Vars.Quick[At(str, i + 1) - 'e' - 1]);
                    i += 2;
                    continue;
                }
                if (IsSupportedInterpType(c))
                {
                    if (!InterpolateParam(str, ref i, c, res)) res.Append(str, chunkStart, i - chunkStart);
                    continue;
                }
                res.Append('%');
            }
            return res.ToString();
        }
        finally
        {
            interpolationLevel--;
        }
    }

    static char At(string s, int i) => i >= 0 && i < s.Length ? s[i] : '\0';

    static bool IsSupportedInterpType(char c) =>
        c == 'v' || c == 'y' || c == 'x' || c == 'z' || c == 'e' || c == 'w' || (c >= 'f' && c <= 't') || c == 'F' || c == 's' || c == 'i' || c == '$';

    static ErmVarKind InterpKind(char c) => c switch
    {
        'v' => ErmVarKind.V, 'y' => ErmVarKind.Y, 'x' => ErmVarKind.X, 'z' => ErmVarKind.Z,
        'e' => ErmVarKind.E, 'w' => ErmVarKind.W, 'F' => ErmVarKind.Flag, _ => ErmVarKind.None,
    };

    /// <summary>One %&lt;type&gt;&lt;index&gt; item; i points at the type character. Returns false on error.</summary>
    bool InterpolateParam(string str, ref int i, char baseChar, StringBuilder res)
    {
        bool indexed = baseChar == 'v' || baseChar == 'y' || baseChar == 'x' || baseChar == 'z' || baseChar == 'e' || baseChar == 'w' || baseChar == 'F';
        char idx;
        if (indexed) { i++; idx = At(str, i); }
        else idx = baseChar;

        var r = new ErmVarRef();
        ErmParam p;
        if ((idx == 'i' || idx == 's') && At(str, i + 1) == '(')
        {
            int start = i + 2;
            int close = str.IndexOf(')', start);
            if (close < 0)
            {
                Error(null, "*InterpolateErmStr: missing s/i-var closing parenthesis \")\"");
                i = str.Length;
                return false;
            }
            string name = str.Substring(start, close - start);
            i = close + 1;
            var kind = idx == 'i' ? ErmVarKind.AssocI : ErmVarKind.AssocS;
            if (indexed) { r.Kind = InterpKind(baseChar); r.IndexKind = kind; }
            else r.Kind = kind;
            r.Name = name;
            r.NeedsInterpolation = name.IndexOf('%') >= 0;
        }
        else if (idx >= 'f' && idx <= 't')
        {
            if (indexed) { r.Kind = InterpKind(baseChar); r.IndexKind = ErmVarKind.Quick; }
            else r.Kind = ErmVarKind.Quick;
            r.Index = idx - 'e';
            i++;
        }
        else if (idx == '$')
        {
            int close = str.IndexOf('$', i + 1);
            if (close < 0 || !Vars.Macros.TryGetValue(str.Substring(i + 1, close - i - 1), out var m))
            {
                Error(null, "*GetNum: unknown macro name $...$");
                i = close < 0 ? str.Length : close + 1;
                return false;
            }
            i = close + 1;
            if (m.Kind == ErmVarKind.None)
            {
                if (indexed) { r.Kind = InterpKind(baseChar); r.Index = m.Index; }
                else { res.Append(m.Index.ToString(CultureInfo.InvariantCulture)); return true; }
            }
            else if (indexed) { r.Kind = InterpKind(baseChar); r.IndexKind = m.Kind; r.Index = m.Index; }
            else { r.Kind = m.Kind; r.Index = m.Index; }
        }
        else
        {
            ErmVarKind idxKind = ErmVarKind.None;
            if (idx == '+' || idx == '-' || (idx >= '0' && idx <= '9')) { }
            else if (InterpKind(idx) != ErmVarKind.None) { idxKind = InterpKind(idx); i++; }
            int numStart = i;
            bool neg = At(str, i) == '-';
            if (neg || At(str, i) == '+') i++;
            int v = 0;
            bool digits = false;
            while (At(str, i) >= '0' && At(str, i) <= '9') { v = unchecked(v * 10 + (str[i] - '0')); i++; digits = true; }
            if (!digits && (idx == '+' || idx == '-'))
            {
                Error(null, "*InterpolateErmStr: expected digit after number sign (+/-). Got: " + At(str, i));
                return false;
            }
            if (neg) v = -v;
            if (indexed) { r.Kind = InterpKind(baseChar); r.IndexKind = idxKind; r.Index = v; }
            else if (idxKind != ErmVarKind.None) { r.Kind = idxKind; r.Index = v; }
            else { res.Append(v.ToString(CultureInfo.InvariantCulture)); return true; }
        }

        p = new ErmParam { Var = r };
        try
        {
            int value = EraGetValue(p, out var t, out var s, strAsText: true);
            switch (t)
            {
                case EraValType.Int: res.Append(value.ToString(CultureInfo.InvariantCulture)); break;
                case EraValType.Str: res.Append(s); break;
                case EraValType.Float: res.Append(BitConverter.Int32BitsToSingle(value).ToString("0.000", CultureInfo.InvariantCulture)); break;
                case EraValType.Bool: res.Append(value != 0 ? '1' : '0'); break;
            }
            return true;
        }
        catch (ErmRuntimeException ex)
        {
            Error(null, ex.Message);
            return false;
        }
    }
}

/// <summary>
/// Era translations (Trans.pas): Lang\&lt;language&gt;\*.json then Lang\*.json of every mod (higher priority
/// first, existing keys are not overridden), nested objects flattened with '.', lists by index.
/// </summary>
public sealed class EraLang
{
    public const char TemplChar = '@';
    readonly Dictionary<string, string> dict = new(StringComparer.Ordinal);

    public int Count => dict.Count;

    public void LoadJson(string json, bool overrideKeys = false)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(json, new System.Text.Json.JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = System.Text.Json.JsonCommentHandling.Skip,
        });
        Walk(doc.RootElement, "", overrideKeys);
    }

    void Walk(System.Text.Json.JsonElement e, string prefix, bool overrideKeys)
    {
        switch (e.ValueKind)
        {
            case System.Text.Json.JsonValueKind.Object:
                foreach (var prop in e.EnumerateObject()) Walk(prop.Value, prefix + prop.Name, overrideKeys, nested: true);
                break;
            case System.Text.Json.JsonValueKind.Array:
                int k = 0;
                foreach (var item in e.EnumerateArray()) Walk(item, prefix + k++.ToString(CultureInfo.InvariantCulture), overrideKeys, nested: true);
                break;
        }
    }

    void Walk(System.Text.Json.JsonElement v, string key, bool overrideKeys, bool nested)
    {
        switch (v.ValueKind)
        {
            case System.Text.Json.JsonValueKind.Object:
            case System.Text.Json.JsonValueKind.Array:
                Walk(v, key + ".", overrideKeys);
                return;
            case System.Text.Json.JsonValueKind.Null:
                return;
        }
        if (!overrideKeys && dict.ContainsKey(key)) return;
        dict[key] = v.ValueKind switch
        {
            System.Text.Json.JsonValueKind.String => v.GetString() ?? "",
            System.Text.Json.JsonValueKind.Number => v.GetDouble().ToString(CultureInfo.InvariantCulture),
            System.Text.Json.JsonValueKind.True => "1",
            System.Text.Json.JsonValueKind.False => "0",
            _ => v.GetRawText(),
        };
    }

    /// <summary>Loads Lang\&lt;language&gt;\*.json then Lang\*.json of each mod (highest priority first).</summary>
    public void LoadMods(IEnumerable<string> modRoots, string language)
    {
        var roots = new List<string>(modRoots);
        foreach (var dir in new[] { System.IO.Path.Combine("Lang", language), "Lang" })
            foreach (var root in roots)
            {
                string d = System.IO.Path.Combine(root, dir);
                if (!System.IO.Directory.Exists(d)) continue;
                foreach (var file in System.IO.Directory.GetFiles(d, "*.json"))
                {
                    try { LoadJson(System.IO.File.ReadAllText(file)); }
                    catch (System.Text.Json.JsonException) { /* Era: "Invalid language json file" */ }
                }
            }
    }

    public bool TryGet(string key, out string value) => dict.TryGetValue(key, out value!);

    /// <summary>Trans.tr: the translation with @name@ placeholders filled (BuildStr); the key itself if missing.</summary>
    public string Tr(string key, IReadOnlyList<string>? args = null) =>
        dict.TryGetValue(key, out var t) ? BuildStr(t!, args) : key;

    /// <summary>StrLib.BuildStr: odd '@'-separated tokens are parameter names.</summary>
    public static string BuildStr(string template, IReadOnlyList<string>? args)
    {
        if (args == null || args.Count == 0) return template;
        var tokens = template.Split(TemplChar);
        if ((tokens.Length - 1) / 2 == 0) return template;
        for (int i = 1; i < tokens.Length; i += 2)
        {
            string name = tokens[i];
            string? val = null;
            for (int j = 0; j + 1 < args.Count; j += 2)
                if (args[j] == name) { val = args[j + 1]; break; }
            tokens[i] = val ?? TemplChar + name + TemplChar;
        }
        return string.Concat(tokens);
    }
}
