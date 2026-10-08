using System;
using System.Buffers.Binary;

namespace WoG.Core.H3Data;

/// <summary>An image decoded from a Heroes III resource: RGBA, 4 bytes a pixel, rows from the top.</summary>
public sealed class H3Image
{
    public H3Image(int width, int height, byte[] rgba)
    {
        Width = width;
        Height = height;
        Rgba = rgba;
    }

    public int Width { get; }
    public int Height { get; }
    public byte[] Rgba { get; }

    /// <summary>The share of fully transparent pixels (0..1).</summary>
    public double TransparentShare()
    {
        int n = 0;
        for (int i = 3; i < Rgba.Length; i += 4) if (Rgba[i] == 0) n++;
        return Rgba.Length == 0 ? 1 : n / (Rgba.Length / 4.0);
    }
}

/// <summary>
/// Decoders of the Heroes III picture formats, read from the user's own installation (through <see cref="EraVfs"/>):
/// PCX — {size, width, height}, then width × height palette indices and a 768-byte palette, or BGR pixels when
/// size = 3 × width × height; DEF — {type, width, height, block count}, a 768-byte palette, blocks of frames
/// {id, count, 8 bytes, count × 13-byte names, count × offsets}, each frame {size, format, full width, full height,
/// width, height, left, top} and its pixels: format 0 raw, 1 rows of (index, length − 1) runs with 0xFF = raw bytes,
/// 2 rows (16-bit offsets) and 3 32-pixel blocks (16-bit offsets each) of bytes code &lt;&lt; 5 | (length − 1) with
/// code 7 = raw bytes. The first palette colors of a DEF are keys: 0 transparent, 1 and 4 the shadow, 5..7 the
/// selection and its shadow where they hold a key color (VCMI's CDefFile).
/// </summary>
public static class H3Images
{
    public static H3Image? Pcx(byte[] b)
    {
        if (b.Length < 12) return null;
        int size = I32(b, 0), w = I32(b, 4), h = I32(b, 8);
        if (w <= 0 || h <= 0 || w > 4096 || h > 4096) return null;
        var rgba = new byte[w * h * 4];
        if (size == w * h && b.Length >= 12 + w * h + 768)
        {
            int pal = 12 + w * h;
            for (int i = 0; i < w * h; i++)
            {
                int c = b[12 + i] * 3;
                rgba[i * 4] = b[pal + c];
                rgba[i * 4 + 1] = b[pal + c + 1];
                rgba[i * 4 + 2] = b[pal + c + 2];
                rgba[i * 4 + 3] = 255;
            }
            return new H3Image(w, h, rgba);
        }
        if (size == w * h * 3 && b.Length >= 12 + size)
        {
            for (int i = 0; i < w * h; i++)
            {
                rgba[i * 4] = b[12 + i * 3 + 2];
                rgba[i * 4 + 1] = b[12 + i * 3 + 1];
                rgba[i * 4 + 2] = b[12 + i * 3];
                rgba[i * 4 + 3] = 255;
            }
            return new H3Image(w, h, rgba);
        }
        return null;
    }

    /// <summary>The frame offsets of a DEF, all blocks in order.</summary>
    public static int[] DefFrames(byte[] b)
    {
        if (b.Length < 16 + 768) return Array.Empty<int>();
        int blocks = I32(b, 12), pos = 16 + 768;
        var frames = new System.Collections.Generic.List<int>();
        for (int k = 0; k < blocks && pos + 16 <= b.Length; k++)
        {
            int count = I32(b, pos + 4);
            if (count < 0 || count > 10000) break;
            pos += 16 + 13 * count;
            for (int i = 0; i < count && pos + 4 <= b.Length; i++, pos += 4) frames.Add(I32(b, pos));
        }
        return frames.ToArray();
    }

    public static H3Image? Def(byte[] b, int frame)
    {
        var frames = DefFrames(b);
        if (frame < 0 || frame >= frames.Length) return null;
        int off = frames[frame];
        if (off < 0 || off + 32 > b.Length) return null;
        int fmt = I32(b, off + 4), fw = I32(b, off + 8), fh = I32(b, off + 12);
        int w = I32(b, off + 16), h = I32(b, off + 20), left = I32(b, off + 24), top = I32(b, off + 28);
        if (fw <= 0 || fh <= 0 || w < 0 || h < 0 || left < 0 || top < 0 || left + w > 4096 || top + h > 4096) return null;
        // some frames are wider than their full size (WoG's flame.def, zobj018.def): the picture grows to hold them
        fw = Math.Max(fw, left + w);
        fh = Math.Max(fh, top + h);
        var idx = new byte[fw * fh];
        int data = off + 32;
        void Put(int y, int x, byte v) => idx[(top + y) * fw + left + x] = v;
        switch (fmt)
        {
            case 0:
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++) Put(y, x, b[data + y * w + x]);
                break;
            case 1:
                for (int y = 0; y < h; y++)
                {
                    int p = data + I32(b, data + y * 4);
                    for (int x = 0; x < w;)
                    {
                        byte code = b[p];
                        int len = b[p + 1] + 1;
                        p += 2;
                        for (int k = 0; k < len && x + k < w; k++) Put(y, x + k, code == 0xFF ? b[p + k] : code);
                        if (code == 0xFF) p += len;
                        x += len;
                    }
                }
                break;
            case 2:
            case 3:
                int perRow = fmt == 2 ? 1 : (w + 31) / 32;
                for (int y = 0; y < h; y++)
                {
                    int x = 0;
                    for (int s = 0; s < perRow; s++)
                    {
                        int p = data + BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(data + (y * perRow + s) * 2));
                        int end = fmt == 2 ? w : Math.Min(w, x + 32);
                        while (x < end)
                        {
                            byte c = b[p++];
                            int code = c >> 5, len = (c & 31) + 1;
                            for (int k = 0; k < len && x + k < w; k++) Put(y, x + k, code == 7 ? b[p + k] : (byte)code);
                            if (code == 7) p += len;
                            x += len;
                        }
                    }
                }
                break;
            default:
                return null;
        }
        var rgba = new byte[fw * fh * 4];
        for (int i = 0; i < idx.Length; i++)
        {
            int c = idx[i];
            int r = b[16 + c * 3], g = b[16 + c * 3 + 1], bl = b[16 + c * 3 + 2];
            byte a = 255;
            if (c is 0 or 1 or 4 || (c < 8 && IsKey(r, g, bl)))
            {
                a = c switch { 1 or 7 => 64, 4 or 6 => 128, _ => 0 };
                r = g = bl = 0;
            }
            rgba[i * 4] = (byte)r;
            rgba[i * 4 + 1] = (byte)g;
            rgba[i * 4 + 2] = (byte)bl;
            rgba[i * 4 + 3] = a;
        }
        return new H3Image(fw, fh, rgba);
    }

    // the key colors of the first palette entries besides 0, 1 and 4 (always keys): cyan, magenta and its shades, yellow
    static bool IsKey(int r, int g, int b) => (r == 0 && g == 255 && b == 255) || (r == 255 && b == 255) || (r == 255 && g == 255 && b == 0);

    /// <summary>"name.def#frame" (frame 0 without it) or "name.pcx" from the installation, or null.</summary>
    public static H3Image? Load(EraVfs vfs, string spec)
    {
        int hash = spec.IndexOf('#');
        string name = hash < 0 ? spec : spec[..hash];
        int frame = hash < 0 ? 0 : int.TryParse(spec[(hash + 1)..], out int f) ? f : -1;
        var bytes = vfs.Read(name);
        if (bytes == null) return null;
        try
        {
            return name.EndsWith(".pcx", StringComparison.OrdinalIgnoreCase) ? Pcx(bytes) : Def(bytes, frame);
        }
        catch (IndexOutOfRangeException) { return null; } // a damaged file
        catch (ArgumentOutOfRangeException) { return null; }
    }

    /// <summary>
    /// A round icon <paramref name="size"/> pixels wide: a picture (an opaque square icon) is zoomed past its frame
    /// and cut to a circle; a glyph (an icon on a transparent background) is fitted inside the circle.
    /// </summary>
    public static H3Image Round(H3Image src, int size)
    {
        bool glyph = src.TransparentShare() > 0.3; // a spell's oval (about a fifth transparent) still fills the circle
        double side = Math.Min(src.Width, src.Height) * (glyph ? 1.0 : 0.86); // the source square shown
        double inset = glyph ? size * 0.16 : 0; // a glyph keeps a margin to the circle
        double scale = side / (size - 2 * inset);
        double cx = src.Width / 2.0, cy = src.Height / 2.0, r = size / 2.0;
        var rgba = new byte[size * size * 4];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                double dx = x + 0.5 - r, dy = y + 0.5 - r;
                double edge = Math.Clamp(r - Math.Sqrt(dx * dx + dy * dy), 0, 1); // anti-aliased circle
                if (edge <= 0) continue;
                double sx = cx + (x + 0.5 - r) * scale - 0.5, sy = cy + (y + 0.5 - r) * scale - 0.5;
                Sample(src, sx, sy, out double cr, out double cg, out double cb, out double ca);
                int o = (y * size + x) * 4;
                rgba[o] = (byte)Math.Round(cr);
                rgba[o + 1] = (byte)Math.Round(cg);
                rgba[o + 2] = (byte)Math.Round(cb);
                rgba[o + 3] = (byte)Math.Round(ca * edge);
            }
        return new H3Image(size, size, rgba);
    }

    // bilinear, premultiplied by alpha so transparent pixels do not darken the edges
    static void Sample(H3Image img, double x, double y, out double r, out double g, out double b, out double a)
    {
        int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
        double fx = x - x0, fy = y - y0;
        r = g = b = a = 0;
        for (int j = 0; j < 2; j++)
            for (int i = 0; i < 2; i++)
            {
                int px = x0 + i, py = y0 + j;
                if (px < 0 || py < 0 || px >= img.Width || py >= img.Height) continue;
                double wgt = (i == 0 ? 1 - fx : fx) * (j == 0 ? 1 - fy : fy);
                int o = (py * img.Width + px) * 4;
                double pa = img.Rgba[o + 3] / 255.0 * wgt;
                r += img.Rgba[o] * pa;
                g += img.Rgba[o + 1] * pa;
                b += img.Rgba[o + 2] * pa;
                a += pa;
            }
        if (a > 0) { r /= a; g /= a; b /= a; }
        a *= 255;
    }

    static int I32(byte[] b, int at) => BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(at));
}
