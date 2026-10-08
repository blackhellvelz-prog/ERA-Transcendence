using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WoG.OldenEra.DebugUI;

/// <summary>
/// "ui" bridge command: reads the game's own interface (canvases, the object tree with its images and texts, the
/// loaded sprites and TextMeshPro fonts), to find the pieces the debug window borrows.
/// </summary>
internal static class UiProbe
{
    public static string Run(string args)
    {
        var w = args.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (w.Length == 0) return "usage: ui canvases | ui tree <name> [depth] | ui sprites <filter> [max] | ui fonts | ui keys [filter]";
        int Int(int i, int def) => w.Length > i && int.TryParse(w[i], out int v) ? v : def;
        return w[0] switch
        {
            "canvases" => Canvases(),
            "tree" => w.Length > 1 ? Tree(w[1], Int(2, 4)) : "usage: ui tree <name> [depth]",
            "sprites" => Sprites(w.Length > 1 ? w[1] : "", Int(2, 100)),
            "fonts" => Fonts(),
            "keys" => Keys(w.Length > 1 ? w[1] : ""),
            _ => "unknown: " + w[0],
        };
    }

    /// <summary>
    /// The game's input actions (every loaded InputActionAsset: map/action [on|off]: bound paths), to find keys the
    /// game does not use for the window's hotkey.
    /// </summary>
    static string Keys(string filter)
    {
        var sb = new StringBuilder();
        foreach (var asset in Resources.FindObjectsOfTypeAll<UnityEngine.InputSystem.InputActionAsset>())
        {
            if (asset == null) continue;
            var maps = asset.actionMaps;
            for (int m = 0; m < maps.Count; m++)
            {
                var map = maps[m];
                var actions = map.actions;
                for (int a = 0; a < actions.Count; a++)
                {
                    var action = actions[a];
                    var paths = new List<string>();
                    var bindings = action.bindings;
                    for (int b = 0; b < bindings.Count; b++)
                    {
                        var binding = bindings[b];
                        if (!binding.isComposite) paths.Add(binding.effectivePath);
                    }
                    string line = $"{asset.name}/{map.name}/{action.name} [{(action.enabled ? "on" : "off")}]: {string.Join(", ", paths)}";
                    if (filter.Length == 0 || line.Contains(filter, StringComparison.OrdinalIgnoreCase)) sb.Append(line).Append('\n');
                }
            }
        }
        return sb.Length == 0 ? "no input actions" + (filter.Length > 0 ? " with " + filter : "") : sb.ToString();
    }

    static bool InScene(Component c) => c != null && c.gameObject.scene.IsValid();

    static string PathOf(Transform t)
    {
        var names = new List<string>();
        for (var x = t; x != null; x = x.parent) names.Add(x.name);
        names.Reverse();
        return string.Join("/", names);
    }

    static string Color32Hex(Color c) => $"#{(int)(c.r * 255):X2}{(int)(c.g * 255):X2}{(int)(c.b * 255):X2}{(int)(c.a * 255):X2}";

    static string Short(string s) => s == null ? "" : (s.Length > 40 ? s[..40] + "…" : s).Replace("\n", "\\n");

    static string Canvases()
    {
        var sb = new StringBuilder();
        foreach (var c in Resources.FindObjectsOfTypeAll<Canvas>())
        {
            if (!InScene(c) || !c.isRootCanvas) continue;
            sb.Append($"{PathOf(c.transform)} active={c.gameObject.activeInHierarchy} mode={c.renderMode} order={c.sortingOrder} children={c.transform.childCount}\n");
        }
        return sb.ToString();
    }

    static string Describe(GameObject go)
    {
        var parts = new List<string>();
        var rt = go.GetComponent<RectTransform>();
        if (rt != null) parts.Add($"{rt.rect.width:0}x{rt.rect.height:0}");
        var img = go.GetComponent<Image>();
        if (img != null)
            parts.Add($"Image[{(img.sprite != null ? img.sprite.name : "-")} {img.type} {Color32Hex(img.color)}{(img.material != null && img.material.name != "Default UI Material" ? " mat=" + img.material.name : "")}]");
        var raw = go.GetComponent<RawImage>();
        if (raw != null) parts.Add($"Raw[{(raw.texture != null ? raw.texture.name : "-")}]");
        var tmp = go.GetComponent<TextMeshProUGUI>();
        if (tmp != null)
            parts.Add($"Text[{(tmp.font != null ? tmp.font.name : "-")} {tmp.fontSize:0.#} {Color32Hex(tmp.color)} mat={(tmp.fontSharedMaterial != null ? tmp.fontSharedMaterial.name : "-")} \"{Short(tmp.text)}\"]");
        if (go.GetComponent<Button>() != null) parts.Add("Button");
        if (go.GetComponent<TMP_InputField>() != null) parts.Add("Input");
        if (go.GetComponent<ScrollRect>() != null) parts.Add("Scroll");
        if (go.GetComponent<Scrollbar>() != null) parts.Add("Scrollbar");
        if (go.GetComponent<Toggle>() != null) parts.Add("Toggle");
        if (go.GetComponent<Mask>() != null || go.GetComponent<RectMask2D>() != null) parts.Add("Mask");
        if (go.GetComponent<VerticalLayoutGroup>() != null) parts.Add("VLayout");
        if (go.GetComponent<HorizontalLayoutGroup>() != null) parts.Add("HLayout");
        return string.Join(" ", parts);
    }

    static void Dump(StringBuilder sb, Transform t, int depth, int max, string indent)
    {
        if (sb.Length > 60000) return;
        sb.Append(indent).Append(t.name).Append(t.gameObject.activeSelf ? "" : " (off)").Append("  ").Append(Describe(t.gameObject)).Append('\n');
        if (depth >= max) { if (t.childCount > 0) sb.Append(indent).Append("  … ").Append(t.childCount).Append(" children\n"); return; }
        for (int i = 0; i < t.childCount; i++) Dump(sb, t.GetChild(i), depth + 1, max, indent + "  ");
    }

    /// <summary>The objects whose name contains <paramref name="name"/> (active ones first), each with its subtree.</summary>
    static string Tree(string name, int depth)
    {
        var found = Resources.FindObjectsOfTypeAll<RectTransform>()
            .Where(t => InScene(t) && t.name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
            .OrderByDescending(t => t.gameObject.activeInHierarchy).Take(3).ToList();
        if (found.Count == 0) return "not found: " + name;
        var sb = new StringBuilder();
        foreach (var t in found)
        {
            sb.Append("== ").Append(PathOf(t)).Append(t.gameObject.activeInHierarchy ? "" : " (inactive)").Append('\n');
            Dump(sb, t, 0, depth, "");
        }
        return sb.ToString();
    }

    static string Sprites(string filter, int max)
    {
        var sb = new StringBuilder();
        int n = 0;
        foreach (var s in Resources.FindObjectsOfTypeAll<Sprite>().OrderBy(s => s.name))
        {
            if (s == null || (filter.Length > 0 && s.name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)) continue;
            if (++n > max) { sb.Append("…\n"); break; }
            var b = s.border;
            sb.Append($"{s.name}  {s.rect.width:0}x{s.rect.height:0} border={b.x:0},{b.y:0},{b.z:0},{b.w:0} tex={(s.texture != null ? s.texture.name : "-")}\n");
        }
        return sb.Append(n).Append(" sprite(s)\n").ToString();
    }

    static string Fonts()
    {
        var sb = new StringBuilder();
        foreach (var f in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
            if (f != null) sb.Append("font ").Append(f.name).Append('\n');
        foreach (var m in Resources.FindObjectsOfTypeAll<Material>())
            if (m != null && m.shader != null && m.shader.name.Contains("TextMeshPro")) sb.Append("material ").Append(m.name).Append("  ").Append(m.shader.name).Append('\n');
        return sb.ToString();
    }
}
