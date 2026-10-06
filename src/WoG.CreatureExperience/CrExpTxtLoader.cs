using System;
using System.Collections.Generic;
using System.Globalization;
using WoG.Core.State;

namespace WoG.CreatureExperience;

/// <summary>
/// Reads the stack-experience tables from the user's own WoG install (bring-your-own-files):
/// CREXPMOD.TXT and CREXPBON.TXT, tab-separated with one header line, as loaded by
/// CrExpMod::Clear and CrExpBon::Clear. The files themselves are never shipped with the port.
/// </summary>
public static class CrExpTxtLoader
{
    static List<string[]> Rows(string text)
    {
        var rows = new List<string[]>();
        var lines = text.Replace("\r\n", "\n").Split('\n');
        for (int i = 1; i < lines.Length; i++) // TxtFile rows start at 1 (row 0 = header)
            rows.Add(lines[i].Split('\t'));
        return rows;
    }

    static string Col(string[] r, int i) => i < r.Length ? r[i] : "";

    /// <summary>WoG a2i/Atoi: leading integer, 0 when absent.</summary>
    static int Atoi(string s)
    {
        s = s.Trim();
        int j = 0;
        if (j < s.Length && (s[j] == '-' || s[j] == '+')) j++;
        while (j < s.Length && char.IsDigit(s[j])) j++;
        return int.TryParse(s.AsSpan(0, j), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int v) ? v : 0;
    }

    static float Atof(string s) =>
        float.TryParse(s.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : 0f;

    /// <summary>CrExpMod::Clear. Rows with ids -1..-8 are per-level defaults (stored under the same negative keys).</summary>
    public static void LoadMod(string text, StackExperienceState into, int monsterCount, Func<int, int> levelOf)
    {
        var defaults = new CreatureExpParams[8];
        for (int j = 0; j < 8; j++) defaults[j] = ExperienceMath.FallbackParams();
        var explicitRows = new Dictionary<int, CreatureExpParams>();
        foreach (var r in Rows(text))
        {
            int n = Atoi(Col(r, 0));
            var p = new CreatureExpParams
            {
                ExpMul = Atof(Col(r, 1)), UpgrMul = Atof(Col(r, 2)), Limit = Atoi(Col(r, 3)), Cap = Atoi(Col(r, 4)), Lvl11Exp = Atoi(Col(r, 5)),
            };
            if (n < 0) { n = -n - 1; if (n > 7) continue; defaults[n] = p; continue; }
            if (n >= 256) continue;
            explicitRows[n] = p;
        }
        for (int j = 0; j < 8; j++) into.Params[-j - 1] = defaults[j];
        for (int i = 0; i < 256; i++)
        {
            if (explicitRows.TryGetValue(i, out var p) && p.Limit != 0) { into.Params[i] = p; continue; }
            int lvl = i >= monsterCount ? 7 : levelOf(i);
            if (lvl < 0 || lvl > 7) lvl = 7;
            var d = defaults[lvl];
            into.Params[i] = new CreatureExpParams { ExpMul = d.ExpMul, UpgrMul = d.UpgrMul, Limit = d.Limit, Cap = d.Cap, Lvl11Exp = d.Lvl11Exp };
        }
    }

    static CreatureExpBonus ParseBonus(string[] r)
    {
        var b = new CreatureExpBonus { Type = Col(r, 1).Length > 0 ? Col(r, 1)[0] : '\0' };
        string mod = Col(r, 2);
        if (mod.StartsWith("#", StringComparison.Ordinal)) { b.Mod = '#'; b.ModNumber = (byte)Atoi(mod.Substring(1)); }
        else b.Mod = mod.Length > 0 ? mod[0] : '\0';
        for (int k = 0; k < 11; k++) b.Levels[k] = (byte)Atoi(Col(r, 3 + k));
        return b;
    }

    static bool Skip(char c) => c == ' ' || c == '\t' || c == '\0';

    /// <summary>CrExpBon::Clear: level defaults merged per creature, then explicit lines (A D H m M S replace).</summary>
    public static void LoadBon(string text, StackExperienceState into, int monsterCount, Func<int, int> levelOf)
    {
        var rows = Rows(text);
        var defaults = new List<CreatureExpBonus>[8];
        for (int j = 0; j < 8; j++) defaults[j] = new();
        foreach (var r in rows)
        {
            int n = Atoi(Col(r, 0));
            if (n < -8 || n > -1) continue;
            var b = ParseBonus(r);
            if (Skip(b.Type)) continue;
            if (defaults[-n - 1].Count >= 20) continue;
            defaults[-n - 1].Add(b);
        }
        into.Bonuses.Clear();
        for (int i = 0; i < monsterCount; i++)
        {
            var list = new List<CreatureExpBonus>();
            int l = levelOf(i);
            void Merge(List<CreatureExpBonus> src)
            {
                for (int j = 0; j < src.Count && j < 14; j++)
                {
                    if (list.Exists(x => x.Type == src[j].Type)) continue; // FindBon(type) → already present
                    if (list.Count >= 20) continue;
                    list.Add(Clone(src[j]));
                }
            }
            if (l >= 0 && l < 8) Merge(defaults[l]);
            Merge(defaults[7]);
            into.Bonuses[i] = list;
        }
        foreach (var r in rows)
        {
            int n = Atoi(Col(r, 0));
            if (n < 0 || n >= 256) continue;
            var b = ParseBonus(r);
            if (Skip(b.Type)) continue;
            if (!into.Bonuses.TryGetValue(n, out var list)) into.Bonuses[n] = list = new();
            int idx = "ADHmMS".IndexOf(b.Type) >= 0 ? list.FindIndex(x => x.Type == b.Type) : -1;
            if (idx >= 0) list[idx] = b;
            else if (list.Count < 20) list.Add(b);
        }
    }

    static CreatureExpBonus Clone(CreatureExpBonus b) =>
        new() { Type = b.Type, Mod = b.Mod, ModNumber = b.ModNumber, Levels = (int[])b.Levels.Clone() };
}
