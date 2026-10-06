using System;

namespace WoG.Erm.Runtime;

/// <summary>Exact ports of WoG's string helpers (common.cpp, service.cpp) used by ERM.</summary>
public static class WoGStrings
{
    static bool IsWs(char c) => c == ' ' || c == '\r' || c == '\n' || c == '\t';
    static char Up(char c) => c >= 'a' && c <= 'z' ? (char)(c - ('a' - 'A')) : c;

    static int SkipLead(string s, int i)
    {
        for (; i < s.Length; i++) if (!IsWs(s[i])) return i;
        return -1;
    }

    static int WordEnd(string s, int i)
    {
        for (; i < s.Length; i++) if (IsWs(s[i])) return i;
        return s.Length;
    }

    /// <summary>
    /// StrCmpExt: word-by-word comparison, ASCII case-insensitive, any amount of whitespace between words.
    /// Quirk kept: an empty <paramref name="src"/> never equals a non-empty <paramref name="dst"/> even if
    /// <paramref name="dst"/> is only whitespace, while the reverse is "equal".
    /// </summary>
    public static bool StrCmpExt(string src, string dst)
    {
        if (src.Length == 0 && dst.Length != 0) return false;
        int i = 0, j = 0;
        while (true)
        {
            i = SkipLead(src, i);
            j = SkipLead(dst, j);
            if (i == -1 && j != -1) return false;
            if (j == -1 && i != -1) return false;
            if (i == -1 && j == -1) return true;
            int ie = WordEnd(src, i), je = WordEnd(dst, j);
            if (je - j != ie - i) return false;
            for (; i < ie; i++, j++) if (Up(src[i]) != Up(dst[j])) return false;
        }
    }

    /// <summary>
    /// Search4Substring as implemented: true when the upper-cased <paramref name="s"/> has a suffix equal
    /// to the trimmed, upper-cased <paramref name="d"/> (StrCmp is a full-string equality, so this is an
    /// "ends with" test, not "contains"). Quirk kept: a one-character <paramref name="d"/> never matches.
    /// Strings are limited to 999 characters as in the original buffers.
    /// </summary>
    public static bool Search4Substring(string s, string d)
    {
        if (s.Length > 999) s = s.Substring(0, 999);
        if (d.Length > 999) d = d.Substring(0, 999);
        var S = s.ToCharArray();
        var D = d.ToCharArray();
        for (int k = 0; k < S.Length; k++) S[k] = Up(S[k]);
        for (int k = 0; k < D.Length; k++) D[k] = Up(D[k]);
        string su = new(S), du = new(D);
        int i = SkipLead(su, 0), j = SkipLead(du, 0);
        if (i == -1 || j == -1) return false;
        int j2 = du.Length - 1;
        while (j2 >= 0 && IsWs(du[j2])) j2--;
        if (j2 <= j) return false;
        string needle = du.Substring(j, j2 - j + 1);
        for (int i2 = i; i2 < su.Length && i2 < 998; i2++)
            if (string.CompareOrdinal(su, i2, needle, 0, int.MaxValue) == 0 && su.Length - i2 == needle.Length)
                return true;
        return false;
    }

    /// <summary>HasText: contains a character other than space, CR, LF, TAB.</summary>
    public static bool HasText(string s)
    {
        foreach (var c in s) if (!IsWs(c)) return true;
        return false;
    }

    /// <summary>Strtok with WoG's delimiters " ,.\t\n\a": returns token #index (0-based) or "".</summary>
    public static string Token(string s, int index)
    {
        var parts = s.Split(new[] { ' ', ',', '.', '\t', '\n', '\a' }, StringSplitOptions.RemoveEmptyEntries);
        return index >= 0 && index < parts.Length ? parts[index] : "";
    }

    /// <summary>StrSkipLead: index of the first significant character, -1 if none.</summary>
    public static int FirstSignificant(string s) => SkipLead(s, 0);

    /// <summary>StrSkipTrailer(str, strlen(str)): index of the last significant character, -1 if none.</summary>
    public static int LastSignificant(string s)
    {
        for (int i = s.Length - 1; i >= 0; i--) if (!IsWs(s[i])) return i;
        return -1;
    }
}
