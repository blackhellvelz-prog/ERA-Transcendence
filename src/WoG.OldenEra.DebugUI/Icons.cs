using System;
using System.Collections.Generic;
using UnityEngine;
using WoG.Core.H3Data;

namespace WoG.OldenEra.DebugUI;

/// <summary>
/// The pictures of the window's round icons, by a spec of alternatives separated by '|', the first found wins:
/// "h3:Secskill.def#32" — a picture of the ERA installation as Era's VFS gives it (HoMM3's LODs, WoG's and ERA's
/// .pac archives and mod folders; read at run time, never copied), cut to a circle; "oe:Icon_Stats_Attack" — an
/// Olden Era sprite loaded by the game, fitted inside the circle. Also the procedural disc, ring and glow the icons
/// are drawn with.
/// </summary>
internal static class Icons
{
    const int Size = 128; // pixels of a round H3 icon

    public static BepInEx.Logging.ManualLogSource Log;
    /// <summary>An Olden Era sprite was not loaded yet: the window looks the sprites up again before its next page.</summary>
    public static bool OeMissed;

    static readonly Dictionary<string, Sprite> h3 = new(StringComparer.OrdinalIgnoreCase);
    static readonly HashSet<string> missing = new(StringComparer.OrdinalIgnoreCase);
    static Sprite disc, ring, glow;

    /// <summary>The sprite of a spec and whether it already fills the circle (an H3 picture cut round).</summary>
    public static (Sprite Sprite, bool Round) Get(string spec)
    {
        if (string.IsNullOrEmpty(spec)) return (null, false);
        foreach (var alt in spec.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (alt.StartsWith("oe:", StringComparison.Ordinal))
            {
                var s = GameUi.SpriteOf(alt[3..]);
                if (s != null) return (s, false);
                OeMissed = true;
            }
            else if (alt.StartsWith("h3:", StringComparison.Ordinal))
            {
                var s = H3(alt[3..]);
                if (s != null) return (s, true);
            }
        }
        return (null, false);
    }

    static Sprite H3(string name)
    {
        if (h3.TryGetValue(name, out var cached)) return cached;
        if (missing.Contains(name)) return null;
        var vfs = WoGPlugin.EraFiles;
        H3Image img = null;
        try { if (vfs != null && !vfs.IsEmpty) img = H3Images.Load(vfs, name); }
        catch (Exception ex) { Log?.LogWarning($"WoG Debug window: picture {name}: {ex.Message}"); }
        if (img == null) { missing.Add(name); return null; }
        return h3[name] = Make(H3Images.Round(img, Size));
    }

    /// <summary>A dark disc under an icon.</summary>
    public static Sprite Disc => disc ??= Make(Shape(256, (d, y) =>
    {
        double a = Edge(120 - d);
        double t = Math.Clamp(1 - d / 120, 0, 1); // lighter in the middle
        return (0.07 + 0.08 * t, 0.09 + 0.10 * t, 0.15 + 0.14 * t, a);
    }));

    /// <summary>A golden ring with a bevel, lighter at the top (the game's gold, #DDB484).</summary>
    public static Sprite Ring => ring ??= Make(Shape(256, (d, y) =>
    {
        const double inner = 108, outer = 124;
        double a = Math.Min(Edge(d - inner), Edge(outer - d));
        if (a <= 0) return (0.05, 0.03, 0.02, Edge(127 - d) * Edge(d - 103) * 0.55); // a dark rim on both sides
        double t = (d - inner) / (outer - inner);
        double k = (0.72 + 0.28 * Math.Cos((t - 0.35) * Math.PI)) * (0.78 + 0.22 * (y + 1) / 2);
        return (0.95 * k, 0.78 * k, 0.50 * k, a);
    }));

    /// <summary>A soft golden glow behind the selected icon.</summary>
    public static Sprite Glow => glow ??= Make(Shape(256, (d, y) =>
    {
        double a = d > 127 ? 0 : Math.Pow(Math.Clamp((127 - d) / 30, 0, 1), 1.5) * Math.Clamp((d - 70) / 30, 0, 1);
        return (1.0, 0.85, 0.55, a * 0.9);
    }));

    static double Edge(double v) => Math.Clamp(v + 0.5, 0, 1);

    // a square RGBA picture from (distance from the middle, height -1 bottom .. 1 top) → color
    static H3Image Shape(int size, Func<double, double, (double R, double G, double B, double A)> f)
    {
        var rgba = new byte[size * size * 4];
        double c = size / 2.0;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                double dx = x + 0.5 - c, dy = y + 0.5 - c;
                var (r, g, b, a) = f(Math.Sqrt(dx * dx + dy * dy) * 128 / c, -dy / c);
                int o = (y * size + x) * 4;
                rgba[o] = Byte(r);
                rgba[o + 1] = Byte(g);
                rgba[o + 2] = Byte(b);
                rgba[o + 3] = Byte(a);
            }
        return new H3Image(size, size, rgba);
    }

    static byte Byte(double v) => (byte)Math.Round(Math.Clamp(v, 0, 1) * 255);

    /// <summary>A sprite of an RGBA picture (rows from the top; Unity's go from the bottom), kept while the game runs.</summary>
    static Sprite Make(H3Image img)
    {
        var flipped = new byte[img.Rgba.Length];
        int row = img.Width * 4;
        for (int y = 0; y < img.Height; y++) Buffer.BlockCopy(img.Rgba, y * row, flipped, (img.Height - 1 - y) * row, row);
        var tex = new Texture2D(img.Width, img.Height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontUnloadUnusedAsset,
        };
        tex.LoadRawTextureData(flipped);
        tex.Apply(false, false);
        var sprite = Sprite.Create(tex, new Rect(0, 0, img.Width, img.Height), new Vector2(0.5f, 0.5f), 100);
        sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
        return sprite;
    }
}
