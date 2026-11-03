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
    readonly ManualLogSource log;
    readonly Queue<(string Text, string Title, Action<bool> Answer, bool Message)> pending = new();
    GameObject root;
    RectTransform window;
    TextMeshProUGUI body, title;
    GameObject yes, no, ok;
    Action<bool> answer;
    bool? gameHotkeys;
    bool failed;

    public GameDialogs(ManualLogSource log) => this.log = log;

    /// <summary>Queues a yes/no question (H3 markup: {…} highlighted, {~color}…{~} colored; no heading when "").</summary>
    public void Ask(string text, string heading, Action<bool> onAnswer)
    {
        if (failed) { onAnswer(true); return; }
        pending.Enqueue((text, heading, onAnswer, false));
    }

    /// <summary>
    /// Queues a message with one button (H3's IF:M); <paramref name="onClose"/> runs when the player closes it (a
    /// script on the scripts' worker waits for that, as H3's modal message box held the script).
    /// </summary>
    public void Message(string text, Action onClose)
    {
        if (failed) { onClose?.Invoke(); return; }
        pending.Enqueue((text, "", _ => onClose?.Invoke(), true));
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
            var (text, heading, onAnswer, message) = pending.Dequeue();
            answer = onAnswer;
            body.text = H3Markup(text);
            title.text = heading;
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
            failed = true; // nobody can answer: every question is taken as "yes", the game goes on
            log.LogError("WoG dialogs stopped: " + ex);
            Answer(true);
            while (pending.Count > 0) pending.Dequeue().Answer(true);
        }
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
        var inner = GameUi.Place(GameUi.Rect("Body", window), 0, 0, 1, 1, 130, 110, 130, 90);
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
