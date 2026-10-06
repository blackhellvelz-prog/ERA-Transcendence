using System;
using System.Collections.Generic;
using System.Text;
using WoG.Core.State;

namespace WoG.Erm.Syntax;

/// <summary>
/// ERM parser. Mirrors ParseERM / GetNum / GetSubNum / GetFlags / ProcessCmd of erm.cpp so that the
/// same text yields the same structure. Differences, all deliberate:
///  * commands are parsed at load time (WoG re-parses them on every execution); command syntax errors are
///    kept on the line and reported when it executes, as WoG would;
///  * macros ($name$) are stored by name and resolved at execution.
/// </summary>
public sealed class ErmParser
{
    /// <summary>Every receiver id the WoG engine knows (ERM_Addition[] + inline receivers).</summary>
    public static readonly HashSet<string> KnownReceivers358 = new()
    {
        "CD","MA","UN","VR","TM","OB","OW","MN","SC","CH","WT","KT","FR","LN","ST","WG","SK","SP","WM","SW",
        "MT","GD","ML","DW","WH","SY","GR","SR","SG","UR","MC","FU","DO","CA","TR","HO","PO","BA","BF","BM",
        "BH","BU","BG","QW","HL","HT","PM","MW","CM","MM","MP","AI","CO","VC","SN","MR","MF","EX","EA","CB",
        "IP","GE","LE","CE","MO","AR","HE","IF","if","el","en",
    };

    /// <summary>Receivers added in the 3.59 alpha.</summary>
    public static readonly HashSet<string> KnownReceivers359 = new() { "TL", "DL", "SS", "LD", "HD", "UX", "CI", "FC", "DG", "la", "go" };

    public const char StringChar = '^';

    readonly ErmDialect dialect;
    string s = "";
    int len;
    string name = "";
    int[] lineStarts = Array.Empty<int>();
    ErmScript script = new();

    public ErmParser(ErmDialect dialect = ErmDialect.Wog358) { this.dialect = dialect; }

    public static ErmScript ParseText(string name, string text, ErmDialect dialect = ErmDialect.Wog358) =>
        new ErmParser(dialect).Parse(name, text);

    public ErmScript Parse(string scriptName, string text)
    {
        s = text;
        len = text.Length;
        name = scriptName;
        script = new ErmScript { Name = scriptName };
        BuildLines();

        int i = 0;
        SkipWs(ref i);
        if (!(i + 4 <= len && string.CompareOrdinal(s, i, "ZVSE", 0, 4) == 0))
        {
            script.IsErm = false;
            return script; // CheckERM: not an ERM script, ignored
        }
        script.IsErm = true;
        i += 4;

        ErmTriggerSection? current = null;
        while (true)
        {
            int bang = s.IndexOf('!', i);
            if (bang < 0 || bang + 1 >= len - 4) break;   // ParseERM: M.i >= M.m.l-4 → no room for a command
            i = bang + 1;
            char kind = s[i];
            if (kind == '?' || kind == '$')
            {
                int start = bang;
                i++;
                string id = Id(ref i);
                var prm = new List<ErmParam>();
                var cond = new ErmCondition();
                if (!ParseParamList(ref i, prm, cond, withFlags: true, out string err))
                {
                    Error(start, $"!{kind}{id}: {err}. The rest of the file is ignored.");
                    break;
                }
                if (i < len && s[i] == ';') i++;
                var n = new List<int>();
                foreach (var p in prm) n.Add(p.Var != null ? p.Var.Index : p.Number);
                var outcome = ErmEventIds.Compute(id, kind == '$', n, dialect, out int ev, out string msg);
                if (outcome == ErmEventIds.Outcome.Skip)
                {
                    Warn(start, $"!{kind}{id}: {msg}");
                    current = null;
                    continue;
                }
                if (outcome == ErmEventIds.Outcome.Error)
                {
                    Error(start, $"!{kind}{id}: {msg} The rest of the file is ignored.");
                    break;
                }
                current = new ErmTriggerSection { Id = id, Post = kind == '$', Condition = cond, EventId = ev, Loc = Loc(start) };
                current.Params.AddRange(prm);
                script.Items.Add(new ErmItem { Kind = ErmItemKind.Section, Section = current });
            }
            else if (kind == '!' || kind == '#')
            {
                int start = bang;
                bool instr = kind == '#';
                i++;
                string id = Id(ref i);
                if (!IsKnownReceiver(id))
                {
                    Warn(start, $"Unknown receiver or instruction: {id}");
                    continue; // WoG: _next — the rest of the line is ordinary text
                }
                var line = new ErmReceiverLine { Id = id, Instruction = instr, Loc = Loc(start) };
                var cond = new ErmCondition();
                if (!ParseParamList(ref i, line.Selector, cond, withFlags: true, out string err))
                {
                    Error(start, $"!{kind}{id}: {err}. The rest of the file is ignored.");
                    break;
                }
                line.Condition = cond;
                if (i < len && s[i] == ':') i++;
                SkipWs(ref i);
                int cmdStart = i;
                int end = FindTerminator(i);
                if (end < 0)
                {
                    Error(start, $"!{kind}{id}: missing ';'. The rest of the file is ignored.");
                    break;
                }
                line.Raw = s.Substring(start, end + 1 - start);
                ParseCommands(line, cmdStart, end);
                i = end + 1;
                if (instr)
                {
                    script.Items.Add(new ErmItem { Kind = ErmItemKind.Instruction, Line = line });
                }
                else
                {
                    if (current == null)
                    {
                        Error(start, $"!!{id}: receiver outside of a trigger. The rest of the file is ignored.");
                        break;
                    }
                    current.Lines.Add(line);
                }
            }
            else if (kind == '@')
            {
                i++;
                int j = i;
                SkipWs(ref j);
                bool zvse = j + 4 <= len && string.CompareOrdinal(s, j, "ZVSE", 0, 4) == 0;
                script.Items.Add(new ErmItem
                {
                    Kind = ErmItemKind.PostInstructionMarker,
                    Line = new ErmReceiverLine { Id = zvse ? "@ZVSE" : "@", Loc = Loc(bang) },
                });
                if (zvse) i = j + 4;
                current = null;
            }
            else
            {
                i++;
            }
        }
        return script;
    }

    bool IsKnownReceiver(string id) =>
        KnownReceivers358.Contains(id) || (dialect == ErmDialect.Wog359Alpha && KnownReceivers359.Contains(id));

    string Id(ref int i)
    {
        string id = i + 2 <= len ? s.Substring(i, 2) : s.Substring(i);
        i += id.Length;
        return id;
    }

    /// <summary>SkipUntil(';') — '^' toggles string mode in which ';' does not terminate.</summary>
    int FindTerminator(int i)
    {
        bool str = false;
        for (; i < len; i++)
        {
            char ch = s[i];
            if (ch == StringChar) str = !str;
            else if (!str && ch == ';') return i;
        }
        return -1;
    }

    /// <summary>GetNumAutoSelf: up to 16 '/'-separated parameters, then optional &amp;/| conditions.</summary>
    bool ParseParamList(ref int i, List<ErmParam> into, ErmCondition cond, bool withFlags, out string error)
    {
        error = "";
        for (int k = 0; k < 16; k++)
        {
            if (!ParseParam(ref i, out var p, out error)) return false;
            into.Add(p);
            if (i < len && s[i] == '/') { i++; continue; }
            break;
        }
        if (withFlags && !ParseFlags(ref i, cond, out error)) return false;
        SkipWs(ref i);
        return true;
    }

    /// <summary>GetFlags: "&amp;a/b|c/d" — flags (±1..1000) or var comparisons.</summary>
    bool ParseFlags(ref int i, ErmCondition cond, out string error)
    {
        error = "";
        if (i < len && s[i] == '&')
        {
            i++;
            if (!ParseFlagList(ref i, cond.And, '&', out error)) return false;
        }
        if (i < len && s[i] == '|')
        {
            i++;
            if (!ParseFlagList(ref i, cond.Or, '|', out error)) return false;
        }
        return true;
    }

    bool ParseFlagList(ref int i, List<ErmCondItem> list, char part, out string error)
    {
        error = "";
        for (int k = 0; k < 16; k++)
        {
            if (!ParseParam(ref i, out var a, out error)) return false;
            if (a.Mode != ErmParamMode.Set) { error = $"cannot get or compare first argument in \"{part}...\" part"; return false; }
            if (a.Var != null || a.Macro != null)
            {
                if (!ParseParam(ref i, out var b, out error)) return false;
                if (b.Mode != ErmParamMode.Check) { error = $"second argument in \"{part}...\" part must be a comparison"; return false; }
                list.Add(new ErmCondItem { Left = a, Right = b });
            }
            else
            {
                int f = a.Number;
                bool set = f >= 0;
                if (f < 0) f = -f;
                if (f < 1 || f > 1000) { error = $"wrong index of flag in \"{part}...\" part"; return false; }
                list.Add(new ErmCondItem { Flag = f, FlagSet = set });
            }
            if (i < len && s[i] == '/') { i++; continue; }
            break;
        }
        return true;
    }

    /// <summary>GetNum in "until the first non-parameter character" mode.</summary>
    bool ParseParam(ref int i, out ErmParam p, out string error)
    {
        p = new ErmParam();
        error = "";
        int cmp = 0;
        bool get = false;
        ErmVarKind vf = ErmVarKind.None, ivf = ErmVarKind.None;
        int begin = i;
        while (i < len)
        {
            char ch = s[i];
            switch (ch)
            {
                case 'd': p.Add = true; i++; continue;
                case '<': cmp |= 1; i++; continue;
                case '=': cmp |= 2; i++; continue;
                case '>': cmp |= 4; i++; continue;
                case '?': get = true; i++; continue;
                case 'v': SetKind(ErmVarKind.V); i++; continue;
                case 'w': SetKind(ErmVarKind.W); i++; continue;
                case 'x': SetKind(ErmVarKind.X); i++; continue;
                case 'y': SetKind(ErmVarKind.Y); i++; continue;
                case 'z': SetKind(ErmVarKind.Z); i++; continue;
                case 'e': SetKind(ErmVarKind.E); i++; continue;
                case ' ': case '\r': case '\n': case '\t': i++; continue;
                case '$':
                {
                    int end = s.IndexOf('$', i + 1);
                    if (end < 0) { error = "cannot find macro"; return false; }
                    string m = s.Substring(i + 1, end - i - 1);
                    if (m.Length > 16) { error = "Macro is too long (>16 characters)."; return false; }
                    p.Macro = m;
                    i = end + 1;
                    return Finish(ref p, ref error, ref i);
                }
            }
            if (ch >= 'f' && ch <= 't')
            {
                int q = ch - 'e';
                i++;
                if (vf != ErmVarKind.None)
                    p.Var = new ErmVarRef { Kind = vf, Index = q, IndexKind = ErmVarKind.Quick };
                else
                    p.Var = new ErmVarRef { Kind = ErmVarKind.Quick, Index = q };
                return Finish(ref p, ref error, ref i);
            }
            // GetSubNum
            int start = i;
            if (!SubNum(ref i, out int n, out bool day)) { error = "cannot parse a number"; return false; }
            bool any = i > start;
            if (vf != ErmVarKind.None)
            {
                p.Var = new ErmVarRef { Kind = vf, Index = n, IndexKind = ivf };
            }
            else
            {
                p.Number = n;
                p.DayRelative = day;
                p.Empty = !any && i == begin;
                if (get) { error = "cannot get flag"; return false; }
            }
            return Finish(ref p, ref error, ref i);
        }
        // end of text
        p.Empty = true;
        return Finish(ref p, ref error, ref i);

        void SetKind(ErmVarKind k)
        {
            if (vf != ErmVarKind.None) ivf = k; else vf = k;
        }

        bool Finish(ref ErmParam prm, ref string err, ref int pos)
        {
            if (get && cmp != 0) { err = "cannot set and check both"; return false; }
            if (get) prm.Mode = ErmParamMode.Get;
            else if (cmp != 0)
            {
                prm.Mode = ErmParamMode.Check;
                prm.Compare = cmp switch
                {
                    1 => ErmCompare.Lt, 2 => ErmCompare.Eq, 3 => ErmCompare.Le, 4 => ErmCompare.Gt,
                    5 => ErmCompare.Ne, 6 => ErmCompare.Ge,
                    _ => ErmCompare.None,
                };
                if (prm.Compare == ErmCompare.None) { err = "type of comparison is incorrect"; return false; }
            }
            // An indexed var whose index kind is a string/float is rejected by GetVarIndex.
            if (prm.Var != null && (prm.Var.IndexKind == ErmVarKind.Z || prm.Var.IndexKind == ErmVarKind.E))
            { err = "Incorrect index variable"; return false; }
            SkipWs(ref pos);
            return true;
        }
    }

    /// <summary>GetSubNum (c==1): optional spaces, one sign, one 'c' (day), digits.</summary>
    bool SubNum(ref int i, out int n, out bool day)
    {
        n = 0;
        day = false;
        int sign = 0;
        int dayState = 0;
        long acc = 0;
        while (i < len)
        {
            char ch = s[i];
            if (ch == ' ')
            {
                if (sign == 0) { i++; continue; }
                while (i < len && s[i] == ' ') i++;
                break;
            }
            if (ch == 'c') { if (dayState == 0) { dayState = 1; i++; continue; } break; }
            if (ch == '-') { if (sign == 0) { sign = -1; i++; continue; } break; }
            if (ch == '+') { if (sign == 0) { sign = 1; i++; continue; } break; }
            if (ch < '0' || ch > '9') break;
            if (sign == 0) sign = 1;
            if (dayState == 0) dayState = -1;
            acc = unchecked(acc * 10 + (ch - '0'));
            i++;
        }
        n = unchecked((int)(acc * sign));
        day = dayState == 1;
        return true;
    }

    /// <summary>ProcessCmd: letter + GetNumAuto, repeated until ';'. Attaches ^text^ and @macro@.</summary>
    void ParseCommands(ErmReceiverLine line, int i, int end)
    {
        while (i < end)
        {
            SkipWs(ref i);
            if (i >= end) break;
            var cmd = new ErmCommand { Letter = s[i] };
            i++;
            while (true)
            {
                if (!ParseParam(ref i, out var p, out string err))
                {
                    line.Commands.Add(new ErmCommand { Letter = '\0', Text = $"syntax error in command {cmd.Letter}: {err}" });
                    Warn(line.Loc, $"!!{line.Id}:{cmd.Letter} — {err}");
                    return;
                }
                cmd.Params.Add(p);
                if (i < end && s[i] == StringChar)
                {
                    int close = s.IndexOf(StringChar, i + 1);
                    if (close < 0 || close > end) close = end;
                    cmd.Text = s.Substring(i + 1, Math.Max(0, close - i - 1));
                    i = Math.Min(close + 1, end);
                }
                else if (i < end && s[i] == '@')
                {
                    int close = s.IndexOf('@', i + 1);
                    if (close < 0 || close > end) close = end;
                    cmd.MacroName = s.Substring(i + 1, Math.Max(0, close - i - 1));
                    i = Math.Min(close + 1, end);
                }
                if (i < end && s[i] == '/') { i++; continue; }
                break;
            }
            line.Commands.Add(cmd);
            SkipWs(ref i);
        }
    }

    void SkipWs(ref int i)
    {
        while (i < len && (s[i] == ' ' || s[i] == '\r' || s[i] == '\n' || s[i] == '\t')) i++;
    }

    void BuildLines()
    {
        var starts = new List<int> { 0 };
        for (int k = 0; k < len; k++) if (s[k] == '\n') starts.Add(k + 1);
        lineStarts = starts.ToArray();
    }

    ErmSourceLoc Loc(int offset)
    {
        int lo = 0, hi = lineStarts.Length - 1;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            if (lineStarts[mid] <= offset) lo = mid; else hi = mid - 1;
        }
        return new ErmSourceLoc(name, lo + 1, offset - lineStarts[lo] + 1);
    }

    void Warn(int offset, string m) => Warn(Loc(offset), m);
    void Warn(ErmSourceLoc loc, string m) => script.Diagnostics.Add(new ErmDiagnostic { Severity = ErmSeverity.Warning, Loc = loc, Message = m });
    void Error(int offset, string m) => script.Diagnostics.Add(new ErmDiagnostic { Severity = ErmSeverity.Error, Loc = Loc(offset), Message = m });

    /// <summary>Reads an ERM file in its original code page (cp1251) when available.</summary>
    public static string DecodeFile(byte[] bytes)
    {
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(1251).GetString(bytes);
        }
        catch
        {
            return Encoding.Latin1.GetString(bytes);
        }
    }
}
