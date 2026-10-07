using System.Collections.Generic;
using WoG.Core.State;

namespace WoG.Erm.Syntax;

/// <summary>
/// Era grammar (Erm.pas, Era 3.9.31): Hook_ZvsGetNum (parameters), Hook_ZvsGetFlags (conditions),
/// CustomGetNumAuto (parameter lists of a command) and AdvErm.GetServiceParams (SN/MP/RD parameters).
/// Applied to the output of the Era preprocessor.
/// </summary>
public sealed partial class ErmParser
{
    /// <summary>
    /// Receivers Era adds to WoG's (re/br/co flow control, rewritten SN/MP/VR) and the ones registered by the
    /// plugins shipped with the ERA project (receiver pa.era, receiver qu.era).
    /// </summary>
    public static readonly HashSet<string> KnownReceiversEra = new() { "re", "br", "co", "TL", "DL", "PA", "QU", "RD", "SS" };

    /// <summary>Receivers whose parameters Era parses with GetServiceParams instead of GetNum.</summary>
    static readonly HashSet<string> ServiceReceivers = new() { "SN", "MP", "RD" };

    static bool IsBlank(char c) => c >= '\x01' && c <= ' ';

    char At(int i) => i < len ? s[i] : '\0';

    void SkipBlanksEra(ref int i)
    {
        while (i < len && IsBlank(s[i])) i++;
    }

    /// <summary>Selector/trigger header: CustomGetNumAuto-like list, then Hook_ZvsGetFlags.</summary>
    bool ParseHeaderEra(ref int i, List<ErmParam> into, ErmCondition cond, bool withFlags, out string error)
    {
        error = "";
        for (int k = 0; k < 16; k++)
        {
            if (!ParseParamEra(ref i, service: false, out var p, out error)) return false;
            into.Add(p);
            if (At(i) == '/') { i++; continue; }
            break;
        }
        if (withFlags && !ParseFlagsEra(ref i, cond, out error)) return false;
        SkipWs(ref i);
        return true;
    }

    /// <summary>Hook_ZvsGetFlags.</summary>
    bool ParseFlagsEra(ref int i, ErmCondition cond, out string error)
    {
        error = "";
        SkipBlanksEra(ref i);
        for (int j = 0; j < 2; j++)
        {
            char mark = j == 0 ? '&' : '|';
            var list = j == 0 ? cond.And : cond.Or;
            if (At(i) != mark) continue;
            i++;
            for (int k = 0; k < 16; k++)
            {
                if (!ParseParamEra(ref i, service: false, out var left, out error)) return false;
                if (left.Mode != ErmParamMode.Set)
                    Warn(i, "\"CheckConditions\" - cannot get or compare the first argument in receiver condition.");
                char c = At(i);
                if (c == '<' || c == '>' || c == '=')
                {
                    if (!ParseParamEra(ref i, service: false, out var right, out error)) return false;
                    if (right.Mode != ErmParamMode.Check)
                        Warn(i, "\"CheckConditions\" - cannot set or get the second argument in receiver condition.");
                    list.Add(new ErmCondItem { Left = left, Right = right });
                }
                else if (left.Var == null && left.Macro == null)
                {
                    // Single number: flag (&500 = f500 set, &-500 = f500 clear)
                    int f = left.Number;
                    list.Add(new ErmCondItem { Flag = f >= 0 ? f : -f, FlagSet = f >= 0 });
                }
                else
                {
                    // Any other single value is cast to boolean: <> 0 / non-empty string
                    list.Add(new ErmCondItem { Left = left, Right = null });
                }
                if (At(i) == '/') { i++; continue; }
                break;
            }
        }
        return true;
    }

    /// <summary>
    /// Hook_ZvsGetNum (and, with <paramref name="service"/>, GetServiceParams): leading blanks; one of
    /// ? / d-modifier / comparison; base type v y x z e w (indexable) or c (current day); then the value or
    /// index: ^string^, i^name^, s^name^, quick var f..t, $macro$, a number, or a variable letter and a
    /// number; trailing blanks.
    /// </summary>
    bool ParseParamEra(ref int i, bool service, out ErmParam p, out string error)
    {
        p = new ErmParam();
        error = "";
        SkipBlanksEra(ref i);
        int start = i;
        bool get = false;
        switch (At(i))
        {
            case '?':
                get = true;
                p.Mode = ErmParamMode.Get;
                i++;
                break;
            case 'd':
                p.Modifier = ErmModifier.Add;
                i++;
                switch (At(i))
                {
                    case '+': i++; break;
                    case '-': p.Modifier = ErmModifier.Sub; i++; break;
                    case '*': p.Modifier = ErmModifier.Mul; i++; break;
                    case ':': p.Modifier = ErmModifier.Div; i++; break;
                    case '%': p.Modifier = ErmModifier.Mod; i++; break;
                    case '|': p.Modifier = ErmModifier.Or; i++; break;
                    case '~': p.Modifier = ErmModifier.AndNot; i++; break;
                    case '&': if (service) { p.Modifier = ErmModifier.Concat; i++; } break;
                    case '<': if (At(i + 1) == '<') { p.Modifier = ErmModifier.Shl; i += 2; } break;
                    case '>': if (At(i + 1) == '>') { p.Modifier = ErmModifier.Shr; i += 2; } break;
                }
                p.Add = p.Modifier == ErmModifier.Add;
                break;
            case '=' when !service:
                i++;
                p.Mode = ErmParamMode.Check;
                if (At(i) == '>') { p.Compare = ErmCompare.Ge; i++; }
                else if (At(i) == '<') { p.Compare = ErmCompare.Le; i++; }
                else p.Compare = ErmCompare.Eq;
                break;
            case '<' when !service:
                i++;
                p.Mode = ErmParamMode.Check;
                if (At(i) == '=') { p.Compare = ErmCompare.Le; i++; }
                else if (At(i) == '>') { p.Compare = ErmCompare.Ne; i++; }
                else p.Compare = ErmCompare.Lt;
                break;
            case '>' when !service:
                i++;
                p.Mode = ErmParamMode.Check;
                if (At(i) == '=') { p.Compare = ErmCompare.Ge; i++; }
                else p.Compare = ErmCompare.Gt;
                break;
        }
        SkipBlanksEra(ref i);

        char baseChar = At(i);
        bool indexed = baseChar == 'v' || baseChar == 'y' || baseChar == 'x' || baseChar == 'z' || baseChar == 'e' || baseChar == 'w';
        // i^..^ / s^..^ are assoc vars, not the indexable bases s/i
        if (indexed) i++;
        else if (baseChar == 'c' && !service)
        {
            if (get) { error = "*GetNum: GET-syntax is not compatible with \"c\" modifier"; return Fail(ref i); }
            p.DayRelative = true;
            i++;
        }

        char idx = At(i);
        ErmVarKind idxKind = ErmVarKind.None;
        int value = 0;
        string? name = null;
        bool interp = false;
        if (idx == '^' || ((idx == 'i' || idx == 's') && At(i + 1) == '^'))
        {
            if (idx == '^') { i++; idxKind = ErmVarKind.Str; }
            else { i += 2; idxKind = idx == 'i' ? ErmVarKind.AssocI : ErmVarKind.AssocS; }
            int nameStart = i;
            while (i < len && s[i] != '^')
            {
                if (s[i] == '%') interp = true;
                i++;
            }
            if (At(i) != '^') { error = "*GetNum: string end marker (^) not found"; return Fail(ref i); }
            name = s.Substring(nameStart, i - nameStart);
            i++;
        }
        else if (idx >= 'f' && idx <= 't')
        {
            idxKind = ErmVarKind.Quick;
            value = idx - 'e';
            i++;
        }
        else if (idx == '$' && !service)
        {
            int end = s.IndexOf('$', i + 1);
            if (end < 0) { error = "*GetNum: unknown macro name $...$"; return Fail(ref i); }
            p.Macro = s.Substring(i + 1, end - i - 1);
            i = end + 1;
            if (indexed) { error = "*GetNum: macro as index is not supported by the port"; return Fail(ref i); }
            SkipBlanksEra(ref i);
            return true;
        }
        else
        {
            if (idx == '+' || idx == '-' || (idx >= '0' && idx <= '9'))
            {
                if (!indexed && get) { error = "*GetNum: GET-syntax cannot be applied to constants"; return Fail(ref i); }
            }
            else if (idx == 'v' || idx == 'y' || idx == 'x' || idx == 'z' || idx == 'e' || idx == 'w')
            {
                idxKind = KindOf(idx);
                i++;
            }
            bool digits = ParseIntEra(ref i, out int n);
            if (digits) value = n;
            else if (idx == '+' || idx == '-')
            {
                error = "*GetNum: expected digit after number sign (+/-). Got: " + At(i);
                return Fail(ref i);
            }
        }

        if (indexed)
        {
            p.Var = new ErmVarRef { Kind = KindOf(baseChar), IndexKind = idxKind, Index = value, Name = name, NeedsInterpolation = interp };
        }
        else if (idxKind == ErmVarKind.None)
        {
            p.Number = value;
            p.Empty = i == start;
        }
        else
        {
            p.Var = new ErmVarRef { Kind = idxKind, Index = value, Name = name, NeedsInterpolation = interp };
        }
        SkipBlanksEra(ref i);
        return true;

        bool Fail(ref int pos)
        {
            // Error: skip to the end of the command
            while (pos < len && s[pos] != ';') pos++;
            return false;
        }
    }

    static ErmVarKind KindOf(char c) => c switch
    {
        'v' => ErmVarKind.V,
        'y' => ErmVarKind.Y,
        'x' => ErmVarKind.X,
        'z' => ErmVarKind.Z,
        'e' => ErmVarKind.E,
        'w' => ErmVarKind.W,
        _ => ErmVarKind.None,
    };

    /// <summary>StrLib.ParseIntFromPchar: optional sign (consumed even on failure), digits, wrap-around.</summary>
    bool ParseIntEra(ref int i, out int value)
    {
        value = 0;
        bool neg = At(i) == '-';
        if (neg || At(i) == '+') i++;
        if (!(At(i) >= '0' && At(i) <= '9')) return false;
        int v = 0;
        while (At(i) >= '0' && At(i) <= '9')
        {
            v = unchecked(v * 10 + (s[i] - '0'));
            i++;
        }
        value = neg ? unchecked(-v) : v;
        return true;
    }

    /// <summary>
    /// Commands of an Era receiver line: blanks before the command letter are skipped (Era's ProcessCmd
    /// patch). Parameters: CustomGetNumAuto (a ^string^ right after a parameter starts the next parameter;
    /// FU/DO may have zero parameters), or GetServiceParams for SN/MP/RD.
    /// </summary>
    void ParseCommandsEra(ErmReceiverLine line, int i, int end)
    {
        bool service = ServiceReceivers.Contains(line.Id);
        bool fuDo = line.Id == "FU" || line.Id == "DO";
        while (i < end)
        {
            SkipBlanksEra(ref i);
            if (i >= end) break;
            var cmd = new ErmCommand { Letter = s[i] };
            i++;
            string err;
            bool ok = service ? ServiceParams(cmd, ref i, end, out err) : CommandParams(cmd, ref i, end, fuDo, out err);
            if (!ok)
            {
                line.Commands.Add(new ErmCommand { Letter = '\0', Text = $"syntax error in command {cmd.Letter}: {err}" });
                Warn(line.Loc, $"!!{line.Id}:{cmd.Letter} — {err}");
                return;
            }
            foreach (var prm in cmd.Params)
            {
                if (prm.Var?.Kind == ErmVarKind.Str) { cmd.Text ??= prm.Var.Name; break; }
            }
            // WoG's MC:S reads "@name@" itself after the parameters
            SkipBlanksEra(ref i);
            if (i < end && s[i] == '@')
            {
                int close = s.IndexOf('@', i + 1);
                if (close < 0 || close > end) close = end;
                cmd.MacroName = s.Substring(i + 1, System.Math.Max(0, close - i - 1));
                i = System.Math.Min(close + 1, end);
            }
            line.Commands.Add(cmd);
        }
    }

    bool CommandParams(ErmCommand cmd, ref int i, int end, bool fuDo, out string error)
    {
        error = "";
        for (int k = 0; k < 16; k++)
        {
            int prev = i;
            if (!ParseParamEra(ref i, service: false, out var p, out error)) return false;
            // Allow FU/DO receivers to handle zero number of parameters
            if (i != prev || !fuDo || At(i) == '/') cmd.Params.Add(p);
            if (i >= end) break;
            // Support old-style IF:Q#^...^ syntax: # and ^...^ are two arguments
            if (s[i] == '^') continue;
            if (s[i] == '/') { i++; continue; }
            break;
        }
        return true;
    }

    static bool IsServiceParamStart(char c) =>
        c == 'x' || c == 'y' || c == 'z' || c == 'w' || c == 'v' || c == 'e' || (c >= 'f' && c <= 't')
        || c == 'd' || c == '+' || c == '-' || (c >= '0' && c <= '9') || c == '?' || c == '^';

    bool ServiceParams(ErmCommand cmd, ref int i, int end, out string error)
    {
        error = "";
        while (true)
        {
            SkipBlanksEra(ref i);
            if (i >= end || !IsServiceParamStart(s[i])) break;
            if (!ParseParamEra(ref i, service: true, out var p, out error)) return false;
            // Strings are not valid indexes; d& only for strings — checked at execution
            cmd.Params.Add(p);
            SkipBlanksEra(ref i);
            if (At(i) == '/') i++;
        }
        return true;
    }
}
