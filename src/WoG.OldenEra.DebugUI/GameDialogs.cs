using System;
using System.Collections.Generic;
using System.Text;
using BepInEx.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WoG.OldenEra.DebugUI;

/// <summary>
/// WoG's questions in the game's own style (the window frame, fonts and buttons of its message box) over a dimmed
/// screen that keeps the mouse off the game; the game's hotkeys are off while a question waits. Questions come one
/// at a time, in order. The WoGify question of a new map is the first user (WoGPlugin.AskDialog). It runs whether
/// WoG Debug is on or not.
/// </summary>
internal sealed class GameDialogs
{
    readonly ManualLogSource log;
    readonly Queue<(string Text, Action<bool> Answer)> pending = new();
    GameObject root;
    RectTransform window;
    TextMeshProUGUI body;
    Action<bool> answer;
    bool? gameHotkeys;
    bool failed;

    public GameDialogs(ManualLogSource log) => this.log = log;

    /// <summary>Queues a yes/no question (H3 markup: {…} highlighted, {~color}…{~} colored).</summary>
    public void Ask(string text, Action<bool> onAnswer)
    {
        if (failed) { onAnswer(true); return; }
        pending.Enqueue((text, onAnswer));
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
            var (text, onAnswer) = pending.Dequeue();
            answer = onAnswer;
            body.text = H3Markup(text);
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
        GameUi.Text(GameUi.Place(GameUi.Rect("Title", inner), 0, 1, 1, 1, 0, -90, 0, 0), "Header", "WoG'ификация", 46, GameUi.Gold,
            TextAlignmentOptions.Center, header: true);
        body = GameUi.Text(GameUi.Place(GameUi.Rect("Text", inner), 0, 0, 1, 1, 20, 150, 20, 110), "Text", "", 34, GameUi.Light,
            TextAlignmentOptions.Center);
        body.enableAutoSizing = true;
        body.fontSizeMin = 22;
        body.fontSizeMax = 34;
        body.textWrappingMode = TextWrappingModes.Normal;

        var yes = GameUi.Button(inner, "Yes", "Да", () => Answer(true), size: 34);
        GameUi.Place(yes.GetComponent<RectTransform>(), 0.5f, 0, 0.5f, 0, -440, 10, 40, -120);
        var no = GameUi.Button(inner, "No", "Нет", () => Answer(false), violet: true, size: 34);
        GameUi.Place(no.GetComponent<RectTransform>(), 0.5f, 0, 0.5f, 0, 40, 10, -440, -120);
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
