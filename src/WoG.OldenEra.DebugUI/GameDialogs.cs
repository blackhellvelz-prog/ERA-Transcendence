using System;
using System.Collections.Generic;
using System.Text;
using BepInEx.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WoG.OldenEra.DebugUI;

/// <summary>
/// WoG's questions and messages in the game's own style (the window frame, fonts and buttons of its message box) over
/// a dimmed screen that keeps the mouse off the game; the game's hotkeys are off while one waits. They come one at a
/// time, in order. Users: the WoGify question of a new map and ERM's IF:Q (WoGPlugin.AskDialog), ERM's IF:M messages
/// (WoGPlugin.MessageDialog). It runs whether WoG Debug is on or not.
/// </summary>
internal sealed class GameDialogs
{
    /// <summary>An IF:G dialog: its items (null = not shown), radio buttons or check boxes, the starting bits.</summary>
    sealed class Choice
    {
        public IReadOnlyList<string> Items;
        public bool Radio;
        public int State;
        public Action<int> OnBits;
    }

    readonly ManualLogSource log;
    readonly Queue<(string Text, string Title, Action<bool> Answer, bool Message, Choice Choice)> pending = new();
    GameObject root;
    RectTransform window, inner;
    TextMeshProUGUI body, title;
    GameObject yes, no, ok;
    Action<bool> answer;
    bool? gameHotkeys;
    bool failed;
    // the shown IF:G dialog: its rows (item number, mark) and the bits on
    Choice choice;
    readonly List<(int Item, Image Mark, GameObject Row)> rows = new();
    int bits;

    public GameDialogs(ManualLogSource log) => this.log = log;

    /// <summary>Queues a yes/no question (H3 markup: {…} highlighted, {~color}…{~} colored; no heading when "").</summary>
    public void Ask(string text, string heading, Action<bool> onAnswer)
    {
        if (failed) { onAnswer(true); return; }
        pending.Enqueue((text, heading, onAnswer, false, null));
    }

    /// <summary>
    /// Queues a message with one button (H3's IF:M); <paramref name="onClose"/> runs when the player closes it (a
    /// script on the scripts' worker waits for that, as H3's modal message box held the script).
    /// </summary>
    public void Message(string text, Action onClose)
    {
        if (failed) { onClose?.Invoke(); return; }
        pending.Enqueue((text, "", _ => onClose?.Invoke(), true, null));
    }

    /// <summary>
    /// Queues check boxes or radio buttons under a header (ERM's IF:G); <paramref name="onBits"/> gets the bits the
    /// player leaves on when they press the button.
    /// </summary>
    public void Choose(string header, IReadOnlyList<string> items, bool radio, int state, Action<int> onBits)
    {
        var c = new Choice { Items = items, Radio = radio, State = state, OnBits = onBits };
        if (failed) { onBits(Core.Adapters.Choice.Initial(items, radio, state)); return; }
        pending.Enqueue((header, "", null, true, c));
    }

    /// <summary>Every frame: shows the next question once the game's interface is loaded.</summary>
    public void Tick()
    {
        if (failed || answer != null || pending.Count == 0) return;
        try
        {
            if (root == null)
            {
                if (!GameUi.Load()) return;
                Build();
            }
            var (text, heading, onAnswer, message, c) = pending.Dequeue();
            choice = c;
            answer = c == null ? onAnswer : _ => c.OnBits(bits);
            if (c != null)
            {
                // the header as the window's title, the items under it
                heading = text;
                text = "";
                ShowRows(c);
            }
            else ClearRows();
            body.text = H3Markup(text);
            body.gameObject.SetActive(c == null);
            title.text = H3Markup(heading);
            title.gameObject.SetActive(heading.Length > 0);
            yes.SetActive(!message);
            no.SetActive(!message);
            ok.SetActive(message);
            root.SetActive(true);
            gameHotkeys = WoGPlugin.GameHotkeys;
            WoGPlugin.GameHotkeys = false;
        }
        catch (Exception ex)
        {
            failed = true; // nobody can answer: every question is taken as "yes", a choice as it opened; the game goes on
            log.LogError("WoG dialogs stopped: " + ex);
            Answer(true);
            while (pending.Count > 0)
            {
                var p = pending.Dequeue();
                if (p.Choice != null) p.Choice.OnBits(Core.Adapters.Choice.Initial(p.Choice.Items, p.Choice.Radio, p.Choice.State));
                else p.Answer(true);
            }
        }
    }

    // ---- IF:G -----------------------------------------------------------------------------------

    const float RowHeight = 76, MessageHeight = 940;

    /// <summary>A row a shown item (mark and text, a click marks it), the window as tall as they need.</summary>
    void ShowRows(Choice c)
    {
        ClearRows();
        bits = Core.Adapters.Choice.Initial(c.Items, c.Radio, c.State);
        var shown = new List<int>();
        for (int i = 0; i < c.Items.Count; i++)
            if (Core.Adapters.Choice.Shown(c.Items, i)) shown.Add(i);
        float height = Math.Max(MessageHeight, 470 + RowHeight * shown.Count);
        GameUi.Place(window, 0.5f, 0.5f, 0.5f, 0.5f, -900, -height / 2, -900, -height / 2);
        for (int k = 0; k < shown.Count; k++)
        {
            int item = shown[k];
            var rt = GameUi.Place(GameUi.Rect("Item", inner), 0, 1, 1, 1, 60, -150 - RowHeight * (k + 1), 60, 150 + RowHeight * k);
            var hit = GameUi.Img(rt, null, color: new Color(1, 1, 1, 0)); // catches the mouse for the whole row
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = hit;
            b.onClick.AddListener(Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<UnityEngine.Events.UnityAction>(new Action(() =>
            {
                bits = Core.Adapters.Choice.Click(bits, item, choice.Radio);
                Marks();
            })));
            var markRect = GameUi.Place(GameUi.Rect("Mark", rt), 0, 0.5f, 0, 0.5f, 10, -20, -50, -20);
            var mark = markRect.gameObject.AddComponent<Image>();
            if (c.Radio) mark.sprite = Icons.Dot;
            mark.raycastTarget = false;
            var text = GameUi.Text(GameUi.Place(GameUi.Rect("Text", rt), 0, 0, 1, 1, 72, 0, 8, 0), "Text", H3Markup(c.Items[item]), 32,
                GameUi.Light, TextAlignmentOptions.MidlineLeft);
            text.enableAutoSizing = true;
            text.fontSizeMin = 20;
            text.fontSizeMax = 32;
            text.raycastTarget = false;
            rows.Add((item, mark, rt.gameObject));
        }
        Marks();
    }

    void Marks()
    {
        foreach (var (item, mark, _) in rows) mark.color = (bits & (1 << item)) != 0 ? GameUi.Gold : Unchecked;
    }

    static readonly Color Unchecked = new(0.22f, 0.20f, 0.18f);

    void ClearRows()
    {
        foreach (var (_, _, row) in rows) UnityEngine.Object.Destroy(row);
        rows.Clear();
        if (window != null) GameUi.Place(window, 0.5f, 0.5f, 0.5f, 0.5f, -900, -MessageHeight / 2, -900, -MessageHeight / 2);
    }

    void Build()
    {
        root = new GameObject("WoG dialogs") { layer = 5 };
        UnityEngine.Object.DontDestroyOnLoad(root);
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 31000; // above the WoG Debug window
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(3840, 2160);
        scaler.matchWidthOrHeight = 0.5f;
        root.AddComponent<GraphicRaycaster>();
        var top = root.GetComponent<RectTransform>();

        GameUi.Img(GameUi.Stretch(GameUi.Rect("Dim", top)), null, color: new Color(0, 0, 0, 0.55f)); // catches the mouse

        window = GameUi.Place(GameUi.Rect("Window", top), 0.5f, 0.5f, 0.5f, 0.5f, -900, -470, -900, -470);
        GameUi.Frame(window);
        inner = GameUi.Place(GameUi.Rect("Body", window), 0, 0, 1, 1, 130, 110, 130, 90);
        title = GameUi.Text(GameUi.Place(GameUi.Rect("Title", inner), 0, 1, 1, 1, 0, -90, 0, 0), "Header", "", 46, GameUi.Gold,
            TextAlignmentOptions.Center, header: true);
        body = GameUi.Text(GameUi.Place(GameUi.Rect("Text", inner), 0, 0, 1, 1, 20, 150, 20, 110), "Text", "", 34, GameUi.Light,
            TextAlignmentOptions.Center);
        body.enableAutoSizing = true;
        body.fontSizeMin = 22;
        body.fontSizeMax = 34;
        body.textWrappingMode = TextWrappingModes.Normal;

        var yesButton = GameUi.Button(inner, "Yes", "Да", () => Answer(true), size: 34);
        GameUi.Place(yesButton.GetComponent<RectTransform>(), 0.5f, 0, 0.5f, 0, -440, 10, 40, -120);
        var noButton = GameUi.Button(inner, "No", "Нет", () => Answer(false), violet: true, size: 34);
        GameUi.Place(noButton.GetComponent<RectTransform>(), 0.5f, 0, 0.5f, 0, 40, 10, -440, -120);
        var okButton = GameUi.Button(inner, "Ok", "Принять", () => Answer(true), size: 34);
        GameUi.Place(okButton.GetComponent<RectTransform>(), 0.5f, 0, 0.5f, 0, -200, 10, -200, -120);
        (yes, no, ok) = (yesButton.gameObject, noButton.gameObject, okButton.gameObject);
        root.SetActive(false);
    }

    void Answer(bool yes)
    {
        var a = answer;
        answer = null;
        if (root != null) root.SetActive(false);
        if (gameHotkeys != null)
        {
            try { WoGPlugin.GameHotkeys = gameHotkeys; }
            catch (Exception ex) { log.LogWarning("WoG dialogs: the game's hotkeys could not be switched back: " + ex.Message); }
            gameHotkeys = null;
        }
        if (a == null) return;
        try { a(yes); }
        catch (Exception ex) { log.LogError("WoG dialogs: the answer failed: " + ex); }
    }

    /// <summary>H3 text markup as TextMeshPro rich text: {…} in the game's gold, {~color}…{~} (ERA) in that color.</summary>
    static string H3Markup(string s)
    {
        var sb = new StringBuilder();
        var open = new Stack<bool>(); // true: a color tag was opened
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '<') { sb.Append("<noparse><</noparse>"); continue; }
            if (c == '{')
            {
                if (i + 1 < s.Length && s[i + 1] == '~')
                {
                    int end = s.IndexOf('}', i);
                    if (end < 0) { sb.Append(c); continue; }
                    string color = s.Substring(i + 2, end - i - 2).Trim();
                    i = end;
                    if (color.Length == 0) { if (open.Count > 0) { open.Pop(); sb.Append("</color>"); } continue; }
                    sb.Append("<color=").Append(color.StartsWith("#") ? color : color.ToLowerInvariant()).Append('>');
                    open.Push(true);
                    continue;
                }
                sb.Append("<color=#DDB484>");
                open.Push(true);
                continue;
            }
            if (c == '}' && open.Count > 0) { open.Pop(); sb.Append("</color>"); continue; }
            sb.Append(c);
        }
        while (open.Count > 0) { open.Pop(); sb.Append("</color>"); }
        return sb.ToString();
    }
}
