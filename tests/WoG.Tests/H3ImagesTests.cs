using System;
using System.Collections.Generic;
using System.IO;
using WoG.Core.H3Data;
using Xunit;

namespace WoG.Tests;

/// <summary>The Heroes III picture decoders (DEF frames in all four formats, PCX) and the round icon, on synthetic files.</summary>
public class H3ImagesTests
{
    const int W = 40, H = 2;

    // the pixels of the test frame: index 0 (transparent) first in each row, then colors 8.. (row 1 uses 1, the shadow, too)
    static byte[][] Rows() => new[]
    {
        Row(0, i => (byte)(8 + i)),
        Row(1, i => (byte)(100 + i)),
    };

    static byte[] Row(byte first, Func<int, byte> rest)
    {
        var r = new byte[W];
        r[0] = first;
        for (int i = 1; i < W; i++) r[i] = rest(i);
        return r;
    }

    static byte[] Palette()
    {
        var p = new byte[768];
        p[0] = 0; p[1] = 255; p[2] = 255; // 0: cyan, the transparent key
        p[3] = 255; p[4] = 150; p[5] = 255; // 1: the shadow key
        for (int i = 8; i < 256; i++) { p[i * 3] = (byte)i; p[i * 3 + 1] = (byte)(2 * i); p[i * 3 + 2] = (byte)(3 * i); }
        return p;
    }

    static byte[] FrameData(int format, byte[][] rows)
    {
        var data = new List<byte>();
        switch (format)
        {
            case 0:
                foreach (var r in rows) data.AddRange(r);
                break;
            case 1:
            {
                var table = new byte[4 * H];
                var body = new List<byte>();
                for (int y = 0; y < H; y++)
                {
                    BitConverter.GetBytes(table.Length + body.Count).CopyTo(table, y * 4);
                    body.Add(rows[y][0]); body.Add(0); // a run of one pixel of the first index
                    body.Add(0xFF); body.Add(W - 2); // then raw bytes
                    for (int x = 1; x < W; x++) body.Add(rows[y][x]);
                }
                data.AddRange(table);
                data.AddRange(body);
                break;
            }
            default:
            {
                int per = format == 2 ? 1 : (W + 31) / 32;
                var table = new byte[2 * H * per];
                var body = new List<byte>();
                for (int y = 0; y < H; y++)
                    for (int s = 0; s < per; s++)
                    {
                        BitConverter.GetBytes((ushort)(table.Length + body.Count)).CopyTo(table, (y * per + s) * 2);
                        int from = format == 2 ? 0 : s * 32, to = format == 2 ? W : Math.Min(W, from + 32);
                        int x = from;
                        if (x == 0) { body.Add((byte)(rows[y][0] << 5)); x = 1; } // a run of one pixel (index < 8)
                        while (x < to)
                        {
                            int len = Math.Min(32, to - x);
                            body.Add((byte)(7 << 5 | (len - 1)));
                            for (int k = 0; k < len; k++) body.Add(rows[y][x + k]);
                            x += len;
                        }
                    }
                data.AddRange(table);
                data.AddRange(body);
                break;
            }
        }
        return data.ToArray();
    }

    static byte[] Def(int format)
    {
        var ms = new MemoryStream();
        var w = new BinaryWriter(ms);
        w.Write(0x47); w.Write(W + 2); w.Write(H + 2); w.Write(1);
        w.Write(Palette());
        w.Write(0); w.Write(1); w.Write(0L); w.Write(new byte[13]);
        int offset = 16 + 768 + 16 + 13 + 4;
        w.Write(offset);
        var data = FrameData(format, Rows());
        w.Write(32 + data.Length); w.Write(format); w.Write(W + 2); w.Write(H + 2); w.Write(W); w.Write(H); w.Write(1); w.Write(1);
        w.Write(data);
        return ms.ToArray();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void A_DEF_frame_decodes_in_every_format(int format)
    {
        var img = H3Images.Def(Def(format), 0);
        Assert.NotNull(img);
        Assert.Equal((W + 2, H + 2), (img!.Width, img.Height));
        byte[] Px(int x, int y) => img.Rgba.AsSpan(((y * img.Width) + x) * 4, 4).ToArray();
        Assert.Equal(0, Px(0, 0)[3]);              // the margin is transparent
        Assert.Equal(0, Px(1, 1)[3]);              // index 0
        Assert.Equal(new byte[] { 9, 18, 27, 255 }, Px(2, 1));   // index 9 (x = 1 of row 0)
        Assert.Equal(new byte[] { 47, 94, 141, 255 }, Px(40, 1)); // index 47 (x = 39, in the second 32-pixel block)
        Assert.Equal(new byte[] { 0, 0, 0, 64 }, Px(1, 2));       // index 1: the shadow
        Assert.Equal(new byte[] { 139, 22, 161, 255 }, Px(40, 2)); // index 139 = (139, 278 & 255, 417 & 255)
        Assert.Null(H3Images.Def(Def(format), 1));
    }

    [Fact]
    public void A_PCX_decodes_with_its_palette_or_as_BGR()
    {
        var ms = new MemoryStream();
        var w = new BinaryWriter(ms);
        w.Write(4); w.Write(2); w.Write(2); w.Write(new byte[] { 0, 1, 2, 3 });
        var pal = new byte[768];
        pal[3] = 10; pal[4] = 20; pal[5] = 30;
        w.Write(pal);
        var img = H3Images.Pcx(ms.ToArray())!;
        Assert.Equal(new byte[] { 10, 20, 30, 255 }, img.Rgba.AsSpan(4, 4).ToArray());

        ms = new MemoryStream();
        w = new BinaryWriter(ms);
        w.Write(3); w.Write(1); w.Write(1); w.Write(new byte[] { 1, 2, 3 }); // B G R
        Assert.Equal(new byte[] { 3, 2, 1, 255 }, H3Images.Pcx(ms.ToArray())!.Rgba);
    }

    /// <summary>Every frame of every DEF and every PCX of an ERA installation decodes (set ERA_GAME_DIR to it).</summary>
    [Fact]
    public void The_pictures_of_an_ERA_installation_decode()
    {
        string? dir = Environment.GetEnvironmentVariable("ERA_GAME_DIR");
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;
        var archives = new List<string> { Path.Combine(dir, "Data", "H3sprite.lod"), Path.Combine(dir, "Data", "H3bitmap.lod") };
        archives.AddRange(Directory.GetFiles(Path.Combine(dir, "Mods", "WoG", "Data"), "*.pac"));
        var failed = new List<string>();
        int frames = 0;
        foreach (var path in archives)
        {
            var lod = LodArchive.Open(path);
            if (lod == null) continue;
            foreach (var name in lod.Names)
            {
                if (name.StartsWith("SGTWMT", StringComparison.OrdinalIgnoreCase)) continue; // SoD's own broken frame headers (VCMI fixes them up)
                var bytes = lod.Read(name)!;
                if (name.EndsWith(".pcx", StringComparison.OrdinalIgnoreCase))
                {
                    frames++;
                    if (H3Images.Pcx(bytes) == null) failed.Add(name);
                }
                else if (name.EndsWith(".def", StringComparison.OrdinalIgnoreCase))
                    for (int f = 0; f < H3Images.DefFrames(bytes).Length; f++, frames++)
                        if (H3Images.Def(bytes, f) == null) { failed.Add($"{Path.GetFileName(path)}|{name}#{f}"); break; }
            }
        }
        Assert.True(failed.Count == 0, $"{failed.Count} of {frames}: " + string.Join(", ", failed.GetRange(0, Math.Min(30, failed.Count))));
        Assert.True(frames > 10000);
    }

    [Fact]
    public void A_round_icon_is_cut_to_a_circle()
    {
        var opaque = new H3Image(8, 8, new byte[8 * 8 * 4]);
        for (int i = 0; i < opaque.Rgba.Length; i += 4) { opaque.Rgba[i] = 200; opaque.Rgba[i + 3] = 255; }
        var round = H3Images.Round(opaque, 32);
        Assert.Equal(0, round.Rgba[3]);                           // a corner is outside the circle
        Assert.Equal(255, round.Rgba[(16 * 32 + 16) * 4 + 3]);    // the middle is opaque
        Assert.Equal(200, round.Rgba[(16 * 32 + 16) * 4]);
    }
}
