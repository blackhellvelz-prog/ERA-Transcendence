using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace WoG.OldenEra.DebugUI;

/// <summary>
/// Builds uGUI elements out of Olden Era's own interface pieces, found by name among the loaded assets: the modal
/// window frame (Window_ModalWindow_*), its buttons (buttom, buttom_violet), the text frame of its input box
/// (TextFrame), the golden scrollbar of the load screen (Gold_Beck, Gold_Top) and the Amrys fonts of its texts
/// (with the game's own materials). Sizes are in the 3840x2160 units the game's windows are made in.
/// </summary>
internal static class GameUi
{
    public static readonly Color Gold = new(0.867f, 0.706f, 0.518f);   // #DDB484, the game's golden headers
    public static readonly Color Light = new(0.937f, 0.937f, 0.937f);  // #EFEFEF, button labels
    public static readonly Color Grey = new(0.65f, 0.65f, 0.65f);      // #A6A6A6, placeholders and hints

    static Dictionary<string, Sprite> sprites;
    public static TMP_FontAsset Font, HeaderFont;
    public static Material FontMaterial, HeaderMaterial;

    public static Sprite SpriteOf(string name)
    {
        if (sprites == null)
        {
            sprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            foreach (var s in Resources.FindObjectsOfTypeAll<Sprite>())
                if (s != null && !sprites.ContainsKey(s.name)) sprites[s.name] = s;
        }
        return sprites.TryGetValue(name, out var sprite) ? sprite : null;
    }

    /// <summary>
    /// The game's fonts as its own visible texts use them (the localized font asset and its material): regular for
    /// text, medium for headers. False until the game has shown such texts and loaded the window sprites.
    /// </summary>
    public static bool Load()
    {
        sprites = null;
        Font = HeaderFont = null;
        foreach (var t in Resources.FindObjectsOfTypeAll<TextMeshProUGUI>())
        {
            if (t == null || !t.gameObject.activeInHierarchy || t.font == null || t.fontSharedMaterial == null) continue;
            string mat = t.fontSharedMaterial.name;
            if (Font == null && mat.StartsWith("AmrysRegular", StringComparison.Ordinal)) { Font = t.font; FontMaterial = t.fontSharedMaterial; }
            if (HeaderFont == null && mat.StartsWith("AmrysMedium", StringComparison.Ordinal)) { HeaderFont = t.font; HeaderMaterial = t.fontSharedMaterial; }
        }
        if (HeaderFont == null) { HeaderFont = Font; HeaderMaterial = FontMaterial; }
        return Font != null && SpriteOf("Window_ModalWindow_Center") != null && SpriteOf("buttom") != null;
    }

    public static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name) { layer = 5 }; // UI
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        return rt;
    }

    /// <summary>Anchors (fractions of the parent) and offsets from them.</summary>
    public static RectTransform Place(RectTransform rt, float minX, float minY, float maxX, float maxY,
        float left = 0, float bottom = 0, float right = 0, float top = 0)
    {
        rt.anchorMin = new Vector2(minX, minY);
        rt.anchorMax = new Vector2(maxX, maxY);
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
        return rt;
    }

    public static RectTransform Stretch(RectTransform rt, float inset = 0) => Place(rt, 0, 0, 1, 1, inset, inset, inset, inset);

    public static Image Img(RectTransform rt, string sprite, Image.Type type = Image.Type.Simple, Color? color = null)
    {
        var img = rt.gameObject.AddComponent<Image>();
        if (sprite != null) img.sprite = SpriteOf(sprite);
        img.type = type;
        img.color = color ?? Color.white;
        return img;
    }

    public static TextMeshProUGUI Text(RectTransform parent, string name, string text, float size, Color color,
        TextAlignmentOptions align = TextAlignmentOptions.Left, bool header = false)
    {
        var rt = Stretch(Rect(name, parent));
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = header ? HeaderFont : Font;
        t.fontSharedMaterial = header ? HeaderMaterial : FontMaterial;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.richText = true;
        t.raycastTarget = false;
        t.text = text;
        return t;
    }

    /// <summary>The game's modal window frame: four corners, four edges and the middle, as its message box has them.</summary>
    public static void Frame(RectTransform parent)
    {
        const float W = 440, H = 100; // corner size
        Img(Place(Rect("cornerTopLeft", parent), 0, 1, 0, 1, 0, -H, -W, 0), "Window_ModalWindow_LeftUP");
        Img(Place(Rect("cornerTopRight", parent), 1, 1, 1, 1, -W, -H, 0, 0), "Window_ModalWindow_RightUP");
        Img(Place(Rect("cornerDownLeft", parent), 0, 0, 0, 0, 0, 0, -W, -H), "Window_ModalWindow_LeftDown");
        Img(Place(Rect("cornerDownRight", parent), 1, 0, 1, 0, -W, 0, 0, -H), "Window_ModalWindow_RightDown");
        Img(Place(Rect("lineTop", parent), 0, 1, 1, 1, W, -H, W, 0), "Window_ModalWindow_MiddleUp");
        Img(Place(Rect("lineDown", parent), 0, 0, 1, 0, W, 0, W, -H), "Window_ModalWindow_MiddleDown");
        Img(Place(Rect("lineLeft", parent), 0, 0, 0, 1, 0, H, -W, H), "Window_ModalWindow_LeftMiddle");
        Img(Place(Rect("lineRight", parent), 1, 0, 1, 1, -W, H, 0, H), "Window_ModalWindow_RightMiddle");
        Img(Place(Rect("fon", parent), 0, 0, 1, 1, W, H, W, H), "Window_ModalWindow_Center");
    }

    /// <summary>A button of the game's message box: blue (main) or violet, glowing under the mouse.</summary>
    public static Button Button(RectTransform parent, string name, string label, Action onClick, bool violet = false, float size = 34)
    {
        string normal = violet ? "buttom_violet" : "buttom", glow = violet ? "buttom_violet_MouseOver" : "buttom_MouseOver";
        var rt = Rect(name, parent);
        var img = Img(rt, normal);
        var b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        b.transition = Selectable.Transition.SpriteSwap;
        var states = b.spriteState;
        states.highlightedSprite = SpriteOf(glow);
        states.pressedSprite = SpriteOf(glow);
        states.selectedSprite = SpriteOf(normal);
        b.spriteState = states;
        b.onClick.AddListener(DelegateSupport.ConvertDelegate<UnityAction>(onClick));
        var t = Text(rt, "label", label, size, Light, TextAlignmentOptions.Center);
        t.enableAutoSizing = true;
        t.fontSizeMin = 18;
        t.fontSizeMax = size;
        t.margin = new Vector4(24, 4, 24, 4);
        return b;
    }

    /// <summary>A one-line input in the text frame of the game's input box.</summary>
    public static TMP_InputField Input(RectTransform parent, string placeholder, Action<string> onSubmit)
    {
        var rt = Rect("Input", parent);
        Img(rt, "TextFrame", Image.Type.Sliced);
        var area = Stretch(Rect("Text Area", rt), 0);
        area.offsetMin = new Vector2(20, 6);
        area.offsetMax = new Vector2(-20, -6);
        area.gameObject.AddComponent<RectMask2D>();
        var ph = Text(area, "Placeholder", placeholder, 32, Grey, TextAlignmentOptions.MidlineLeft);
        ph.fontStyle = FontStyles.Italic;
        var text = Text(area, "Text", "", 32, Color.white, TextAlignmentOptions.MidlineLeft);
        text.richText = false;
        var input = rt.gameObject.AddComponent<TMP_InputField>();
        input.textViewport = area;
        input.textComponent = text;
        input.placeholder = ph;
        input.fontAsset = Font;
        input.pointSize = 32;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.richText = false;
        input.customCaretColor = true;
        input.caretColor = Gold;
        input.caretWidth = 3;
        input.selectionColor = new Color(0.867f, 0.706f, 0.518f, 0.35f);
        input.onSubmit.AddListener(DelegateSupport.ConvertDelegate<UnityAction<string>>(onSubmit));
        return input;
    }

    /// <summary>A scrolling text area with the golden scrollbar of the game's load screen.</summary>
    public static (ScrollRect scroll, TextMeshProUGUI text) Output(RectTransform parent)
    {
        Img(parent, "text_background", Image.Type.Sliced, new Color(1, 1, 1, 0.85f));
        var scroll = parent.gameObject.AddComponent<ScrollRect>();
        var viewport = Place(Rect("Viewport", parent), 0, 0, 1, 1, 24, 16, 60, 16);
        viewport.gameObject.AddComponent<RectMask2D>();
        var content = Rect("Content", viewport);
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(0.5f, 1);
        content.offsetMin = new Vector2(0, 0);
        content.offsetMax = new Vector2(0, 0);
        var text = content.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = Font;
        text.fontSharedMaterial = FontMaterial;
        text.fontSize = 32;
        text.lineSpacing = -55; // the Amrys fonts have a tall line; the game's lists use them tighter too
        text.color = new Color(0.9f, 0.9f, 0.9f);
        text.alignment = TextAlignmentOptions.TopLeft;
        text.richText = true;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        var fit = content.gameObject.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var bar = Place(Rect("Scrollbar", parent), 1, 0, 1, 1, -48, 16, 16, 16);
        Img(bar, "Gold_Beck", Image.Type.Sliced);
        var sb = bar.gameObject.AddComponent<Scrollbar>();
        var area = Stretch(Rect("Sliding Area", bar), 2);
        var handle = Stretch(Rect("Handle", area));
        sb.targetGraphic = Img(handle, "Gold_Top", Image.Type.Sliced);
        sb.handleRect = handle;
        sb.direction = Scrollbar.Direction.BottomToTop;

        scroll.content = content;
        scroll.viewport = viewport;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 60;
        scroll.verticalScrollbar = sb;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        return (scroll, text);
    }
}
