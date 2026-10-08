using System;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace WoG.OldenEra.DebugUI;

/// <summary>A round icon: a dark disc, the picture (<see cref="Icons"/>), a golden ring and a glow when selected.</summary>
internal sealed class RoundIcon
{
    public RectTransform Rect;
    public Image Ring;
    Image glow;

    public static RoundIcon Create(RectTransform parent, string name, string spec, float size, string fallback = null)
    {
        var icon = new RoundIcon { Rect = GameUi.Rect(name, parent) };
        icon.Rect.sizeDelta = new Vector2(size, size);
        float g = size * 0.22f;
        icon.glow = Sprite(GameUi.Place(GameUi.Rect("Glow", icon.Rect), 0, 0, 1, 1, -g, -g, -g, -g), Icons.Glow, new Color(1, 0.85f, 0.55f));
        icon.glow.enabled = false;
        Sprite(GameUi.Stretch(GameUi.Rect("Disc", icon.Rect), size * 0.03f), Icons.Disc, Color.white);
        var (sprite, round) = Icons.Get(spec);
        if (sprite != null)
        {
            var img = Sprite(GameUi.Stretch(GameUi.Rect("Picture", icon.Rect), size * (round ? 0.07f : 0.2f)), sprite, Color.white);
            img.preserveAspect = true;
        }
        else
        {
            var t = GameUi.Text(GameUi.Stretch(GameUi.Rect("Letter", icon.Rect), size * 0.12f), "Text", fallback ?? "?", size * 0.36f,
                GameUi.Gold, TextAlignmentOptions.Center, header: true);
            t.enableAutoSizing = true;
            t.fontSizeMin = 10;
            t.fontSizeMax = size * 0.36f;
        }
        icon.Ring = Sprite(GameUi.Stretch(GameUi.Rect("Ring", icon.Rect)), Icons.Ring, Color.white);
        icon.Ring.raycastTarget = true; // the ring catches the mouse for the whole icon
        return icon;
    }

    public void Select(bool on) => glow.enabled = on;

    static Image Sprite(RectTransform rt, Sprite sprite, Color color)
    {
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }
}

/// <summary>The window's round buttons and feature tiles.</summary>
internal static class RoundUi
{
    static readonly Color Dim = new(0.82f, 0.82f, 0.82f);

    /// <summary>A round icon with a label under it (tabs and the toolbar); the ring brightens under the mouse.</summary>
    public static (Button Button, RoundIcon Icon, TextMeshProUGUI Label) IconButton(RectTransform parent, string name, string spec,
        string label, Action onClick, float size, float labelSize, string fallback = null)
    {
        var rt = GameUi.Rect(name, parent);
        var e = rt.gameObject.AddComponent<LayoutElement>();
        e.preferredWidth = e.minWidth = size + 60;
        e.preferredHeight = e.minHeight = size + labelSize * 1.9f;
        var icon = RoundIcon.Create(rt, "Icon", spec, size, fallback);
        icon.Rect.anchorMin = icon.Rect.anchorMax = new Vector2(0.5f, 1);
        icon.Rect.pivot = new Vector2(0.5f, 1);
        icon.Rect.anchoredPosition = Vector2.zero;
        var text = GameUi.Text(GameUi.Place(GameUi.Rect("Label", rt), 0, 0, 1, 0, -14, 0, -14, -labelSize * 1.9f), "Text", label, labelSize,
            GameUi.Grey, TextAlignmentOptions.Bottom);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        var b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = icon.Ring;
        b.transition = Selectable.Transition.ColorTint;
        var colors = b.colors;
        colors.normalColor = Dim;
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.65f, 0.65f, 0.65f);
        colors.selectedColor = Dim;
        colors.fadeDuration = 0.08f;
        b.colors = colors;
        b.onClick.AddListener(DelegateSupport.ConvertDelegate<UnityAction>(onClick));
        return (b, icon, text);
    }

    /// <summary>
    /// A feature: the game's button stretched to a tile, its round icon on the left, the label and, smaller and golden,
    /// the ERM code it tries (the label's part in parentheses).
    /// </summary>
    public static Button Tile(RectTransform parent, string name, string spec, string label, Action onClick)
    {
        string title = label, code = null;
        int open = label.LastIndexOf(" (", StringComparison.Ordinal);
        if (open > 0 && label.EndsWith(")", StringComparison.Ordinal))
        {
            title = label[..open];
            code = label[(open + 2)..^1];
        }
        var b = GameUi.Button(parent, name, null, onClick, size: 28);
        var rt = b.GetComponent<RectTransform>();
        const float Icon = 92;
        var icon = RoundIcon.Create(rt, "Icon", spec, Icon, title.Length > 0 ? title[..1] : "?");
        icon.Rect.anchorMin = icon.Rect.anchorMax = new Vector2(0, 0.5f);
        icon.Rect.pivot = new Vector2(0, 0.5f);
        icon.Rect.anchoredPosition = new Vector2(14, 0);
        icon.Ring.raycastTarget = false;
        var texts = GameUi.Place(GameUi.Rect("Texts", rt), 0, 0, 1, 1, Icon + 30, 10, 22, 10);
        var main = GameUi.Text(GameUi.Place(GameUi.Rect("Title", texts), 0, code != null ? 0.44f : 0, 1, 1), "Text", title, 28, GameUi.Light,
            code != null ? TextAlignmentOptions.BottomLeft : TextAlignmentOptions.MidlineLeft);
        main.enableAutoSizing = true;
        main.fontSizeMin = 20;
        main.fontSizeMax = 30;
        if (code != null)
        {
            // no ellipsis: TextMeshPro hides a whole line that does not fit the box's height (the Amrys line is tall)
            var sub = GameUi.Text(GameUi.Place(GameUi.Rect("Code", texts), 0, 0, 1, 0.44f), "Text", code, 24, GameUi.Gold, TextAlignmentOptions.TopLeft);
            sub.textWrappingMode = TextWrappingModes.NoWrap;
            sub.overflowMode = TextOverflowModes.Overflow;
        }
        return b;
    }
}
