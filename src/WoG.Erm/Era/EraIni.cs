using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace WoG.Erm.Era;

/// <summary>
/// Era's memory-cached ini files (b2 Ini.pas), behind the era.dll exports ReadStrFromIni, WriteStrToIni, SaveIni,
/// ClearIniCache, EmptyIniCache and MergeIniWithDefault. Section names and keys are case-insensitive (the assoc
/// arrays lowercase them); a file is parsed once and kept in memory until it is cleared.
///
/// Paths are relative to the game folder in Era. The port keeps the user's ERA installation read-only: a relative
/// path is read from the write root first, then from the read roots (the ERA folder); saving writes under the
/// write root only.
/// </summary>
public sealed class EraIni
{
    sealed class Section
    {
        public readonly Dictionary<string, (string Key, string Value)> Items = new(StringComparer.OrdinalIgnoreCase);
    }

    sealed class IniFile
    {
        public readonly Dictionary<string, (string Name, Section Section)> Sections = new(StringComparer.OrdinalIgnoreCase);
        public bool Cp1251;

        public IniFile Clone()
        {
            var f = new IniFile { Cp1251 = Cp1251 };
            foreach (var (k, (name, sec)) in Sections)
            {
                var s = new Section();
                foreach (var (ik, iv) in sec.Items) s.Items[ik] = iv;
                f.Sections[k] = (name, s);
            }
            return f;
        }
    }

    readonly Dictionary<string, IniFile> cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Where saved files go (and are read from first).</summary>
    public string WriteRoot { get; set; }
    /// <summary>Folders searched for a relative path after the write root (the ERA installation).</summary>
    public List<string> ReadRoots { get; } = new();

    public EraIni(string writeRoot) { WriteRoot = writeRoot; }

    static string Key(string path) => path.Replace('/', '\\').Trim();

    string? ExistingFile(string path)
    {
        if (Path.IsPathRooted(path)) return File.Exists(path) ? path : null;
        foreach (var root in new[] { WriteRoot }.Concat(ReadRoots))
        {
            string p = Path.Combine(root, path);
            if (File.Exists(p)) return p;
        }
        return null;
    }

    string SavePath(string path) => Path.IsPathRooted(path) ? path : Path.Combine(WriteRoot, path);

    public void ClearIniCache(string path) => cache.Remove(Key(path));
    public void ClearAllIniCache() => cache.Clear();
    public void EmptyIniCache(string path) => cache[Key(path)] = new IniFile();

    IniFile Cached(string path)
    {
        if (!cache.TryGetValue(Key(path), out var f)) { LoadIni(path); f = cache[Key(path)]; }
        return f;
    }

    /// <summary>LoadIni: parses the file into the cache; an invalid file leaves an empty entry. True if it was read.</summary>
    public bool LoadIni(string path)
    {
        var file = new IniFile();
        cache[Key(path)] = file;
        string? real = ExistingFile(path);
        if (real == null) return false;
        byte[] bytes;
        try { bytes = File.ReadAllBytes(real); }
        catch (IOException) { return false; }
        string text = EraText.Decode(bytes);
        file.Cp1251 = bytes.Any(b => b >= 0x80) && !IsUtf8(bytes);
        if (text.Length == 0) return true;

        // TextScanner rules of LoadIni: blanks #0..#32 skipped; ';' comments to the end of the line; "[name]" opens a
        // section (a missing ']' makes the whole file invalid); "key=value" with ';' or the line end closing both.
        Section? current = null;
        int pos = 0;
        bool ok = true;
        while (ok && pos < text.Length)
        {
            while (pos < text.Length && text[pos] <= ' ') pos++;
            if (pos >= text.Length) break;
            char c = text[pos];
            if (c == ';') { SkipLine(); continue; }
            if (c == '[')
            {
                pos++;
                int start = pos;
                while (pos < text.Length && text[pos] != ']' && text[pos] != ';' && text[pos] != '\n' && text[pos] != '\r') pos++;
                if (pos >= text.Length || text[pos] != ']') { ok = false; break; }
                string name = text[start..pos].Trim();
                SkipLine();
                if (!file.Sections.TryGetValue(name, out var s)) file.Sections[name] = s = (name, new Section());
                current = s.Section;
                continue;
            }
            int ks = pos;
            while (pos < text.Length && text[pos] != '=' && text[pos] != ';' && text[pos] != '\n' && text[pos] != '\r') pos++;
            string key = text[ks..pos].Trim();
            string value = "";
            if (pos < text.Length && text[pos] == '=')
            {
                pos++;
                int vs = pos;
                while (pos < text.Length && text[pos] != ';' && text[pos] != '\n' && text[pos] != '\r') pos++;
                value = text[vs..pos].Trim();
            }
            if (current == null)
            {
                if (!file.Sections.TryGetValue("", out var s)) file.Sections[""] = s = ("", new Section());
                current = s.Section;
            }
            current.Items[key] = (key, value);
        }
        if (!ok) file.Sections.Clear(); // Era keeps no half-parsed data
        return ok;

        void SkipLine()
        {
            while (pos < text.Length && text[pos] != '\n') pos++;
            if (pos < text.Length) pos++;
        }
    }

    static bool IsUtf8(byte[] b)
    {
        try { new UTF8Encoding(false, true).GetString(b); return true; }
        catch (DecoderFallbackException) { return false; }
    }

    public bool ReadStrFromIni(string key, string section, string path, out string value)
    {
        value = "";
        var f = Cached(path);
        if (!f.Sections.TryGetValue(section, out var s) || !s.Section.Items.TryGetValue(key, out var item)) return false;
        value = item.Value;
        return true;
    }

    public bool WriteStrToIni(string key, string value, string section, string path)
    {
        var f = Cached(path);
        if (section.IndexOfAny(new[] { ';', '\n', '\r', ']' }) >= 0) return false;
        if (key.IndexOfAny(new[] { ';', '\n', '\r', '=' }) >= 0) return false;
        if (value.IndexOfAny(new[] { ';', '\n', '\r' }) >= 0) return false;
        if (!f.Sections.TryGetValue(section, out var s)) f.Sections[section] = s = (section, new Section());
        s.Section.Items[key] = (key, value);
        return true;
    }

    /// <summary>SaveIni: sections and keys sorted (case-insensitively, as TStringList), CRLF lines, under the write root.</summary>
    public bool SaveIni(string path)
    {
        var f = Cached(path);
        var sb = new StringBuilder();
        foreach (var (name, section) in f.Sections.Values.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (name != "") sb.Append('[').Append(name).Append("]\r\n");
            foreach (var (key, value) in section.Items.Values.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
                sb.Append(key).Append('=').Append(value).Append("\r\n");
        }
        try
        {
            string target = SavePath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(target))!);
            var enc = f.Cp1251 ? Cp1251() : new UTF8Encoding(false);
            File.WriteAllText(target, sb.ToString(), enc);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }

    /// <summary>MergeIniWithDefault: adds the source's entries that the target lacks (both must be loaded already).</summary>
    public void MergeIniWithDefault(string targetPath, string sourcePath)
    {
        if (!cache.TryGetValue(Key(sourcePath), out var source)) return;
        if (!cache.TryGetValue(Key(targetPath), out var target)) { cache[Key(targetPath)] = source.Clone(); return; }
        foreach (var (name, sec) in source.Sections.Values)
        {
            if (!target.Sections.TryGetValue(name, out var t)) { target.Sections[name] = (name, source.Clone().Sections[name].Section); continue; }
            foreach (var (k, item) in sec.Items)
                if (!t.Section.Items.ContainsKey(k)) t.Section.Items[k] = item;
        }
    }

    static Encoding Cp1251()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1251);
    }
}
