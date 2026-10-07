using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace WoG.Core.H3Data;

/// <summary>
/// A Heroes III resource archive (.lod; ERA's .pac has the same layout): "LOD\0", a file count at offset 8 and, from
/// offset 0x5C, 32-byte entries {name[16], offset, size, type, compressed size}; a compressed entry is zlib data.
/// Read only, from the user's own installation.
/// </summary>
public sealed class LodArchive
{
    readonly string path;
    readonly Dictionary<string, (uint Offset, uint Size, uint CSize)> entries = new(StringComparer.OrdinalIgnoreCase);

    public string Path => path;
    public IEnumerable<string> Names => entries.Keys;

    LodArchive(string path) { this.path = path; }

    public static LodArchive? Open(string path)
    {
        try
        {
            using var f = File.OpenRead(path);
            using var r = new BinaryReader(f);
            if (f.Length < 0x5C || Encoding.ASCII.GetString(r.ReadBytes(4)) != "LOD\0") return null;
            f.Position = 8;
            uint count = r.ReadUInt32();
            if (count > 100000 || 0x5C + 32L * count > f.Length) return null;
            var lod = new LodArchive(path);
            f.Position = 0x5C;
            for (uint i = 0; i < count; i++)
            {
                var name = r.ReadBytes(16);
                int len = Array.IndexOf(name, (byte)0);
                string n = Encoding.Latin1.GetString(name, 0, len < 0 ? 16 : len);
                uint offset = r.ReadUInt32(), size = r.ReadUInt32();
                r.ReadUInt32(); // type
                uint csize = r.ReadUInt32();
                if (n.Length > 0) lod.entries.TryAdd(n, (offset, size, csize));
            }
            return lod;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
    }

    public bool Contains(string name) => entries.ContainsKey(name);

    public byte[]? Read(string name)
    {
        if (!entries.TryGetValue(name, out var e)) return null;
        using var f = File.OpenRead(path);
        f.Position = e.Offset;
        var raw = new byte[e.CSize != 0 ? e.CSize : e.Size];
        for (int got = 0, n; got < raw.Length; got += n)
            if ((n = f.Read(raw, got, raw.Length - got)) == 0) throw new EndOfStreamException(path + ": " + name);
        if (e.CSize == 0) return raw;
        using var z = new ZLibStream(new MemoryStream(raw), CompressionMode.Decompress);
        using var o = new MemoryStream((int)e.Size);
        z.CopyTo(o);
        return o.ToArray();
    }
}
