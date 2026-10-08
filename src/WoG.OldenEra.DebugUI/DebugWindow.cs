using System;
using System.Collections.Generic;
using System.Text;
using BepInEx.Logging;
using TMPro;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using WoG.Debug;

namespace WoG.OldenEra.DebugUI;

/// <summary>
/// The WoG Debug window: a console over the WoG Debug commands (a line starting with "!" is ERM code, anything
/// else a debug command such as "state" or "vars v 1 10") and buttons for the frequent ones. F9 or the "WoG" button
/// on the left edge of the screen opens and closes it. Built from the game's own interface pieces (GameUi).
/// </summary>
internal sealed class DebugWindow
{
    const int MaxOutput = 14000; // characters kept: a TextMeshPro mesh has a vertex limit

    readonly DebugCommands commands;
    readonly ManualLogSource log;
    readonly List<string> history = new();
    readonly StringBuilder output = new();
    int historyAt;
    int nextTry;
    bool failed;

    GameObject root;
    RectTransform window;
    TMP_InputField input;
    TextMeshProUGUI text;
    ScrollRect scroll;

    public DebugWindow(DebugCommands commands, ManualLogSource log)
    {
        this.commands = commands;
        this.log = log;
    }

    bool Visible => window != null && window.gameObject.activeSelf;

    /// <summary>Every frame: builds the window once the game's interface is loaded, then reads its keys.</summary>
    public void Tick()
    {
        if (failed) return;
        try
        {
            if (root == null)
            {
                if (Time.frameCount < nextTry) return;
                nextTry = Time.frameCount + 120;
                if (!GameUi.Load()) return;
                Build();
            }
            Keys();
        }
        catch (Exception ex)
        {
            failed = true; // one report, not one per frame
            log.LogError("WoG Debug window stopped: " + ex);
        }
    }

    void Build()
    {
        root = new GameObject("WoG Debug window") { layer = 5 };
        UnityEngine.Object.DontDestroyOnLoad(root);
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000; // above the game's tooltips (25001)
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(3840, 2160);
        scaler.matchWidthOrHeight = 0.5f;
        root.AddComponent<GraphicRaycaster>();
        var top = root.GetComponent<RectTransform>();

        // the toggle on the left edge, below the players' banners
        var toggle = GameUi.Button(top, "WoG toggle", "WoG", ToggleWindow, violet: true, size: 30);
        GameUi.Place(toggle.GetComponent<RectTransform>(), 0, 1, 0, 1, 24, -470, -224, 404);

        window = GameUi.Place(GameUi.Rect("Window", top), 1, 0.5f, 1, 0.5f, -1880, -760, 80, -760);
        GameUi.Frame(window);
        var body = GameUi.Place(GameUi.Rect("Body", window), 0, 0, 1, 1, 110, 100, 110, 70);

        var title = GameUi.Place(GameUi.Rect("Title", body), 0, 1, 1, 1, 0, -90, 0, 0);
        GameUi.Text(title, "Header", "WoG Debug", 46, GameUi.Gold, TextAlignmentOptions.Center, header: true);
        Draggable(title, canvas);
        var close = GameUi.Button(body, "Close", "X", ToggleWindow, violet: true, size: 30);
        GameUi.Place(close.GetComponent<RectTransform>(), 1, 1, 1, 1, -110, -82, 0, 8);

        // the frequent commands
        var bar = GameUi.Place(GameUi.Rect("Toolbar", body), 0, 1, 1, 1, 0, -200, 0, 110);
        var layout = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 16;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        foreach (var (label, command) in new[]
                 {
                     ("Состояние", "state"), ("Новый день", "newday"), ("Самопроверка", "selftest"),
                     ("Совместимость", "compat"), ("Справка", "help"),
                 })
            GameUi.Button(bar, label, label, () => Run(command), size: 30);

        // the output
        var outRect = GameUi.Place(GameUi.Rect("Output", body), 0, 0, 1, 1, 0, 190, 0, 230);
        (scroll, text) = GameUi.Output(outRect);

        // the input line
        var line = GameUi.Place(GameUi.Rect("Line", body), 0, 0, 1, 0, 0, 80, 0, -160);
        input = GameUi.Input(line, "!!HE-1:… — ERM, или команда: state, vars v 1 10, help", Submit);
        GameUi.Place(input.GetComponent<RectTransform>(), 0, 0, 1, 1, 0, 0, 340, 0);
        var run = GameUi.Button(line, "Run", "Выполнить", () => Submit(input.text), size: 32);
        GameUi.Place(run.GetComponent<RectTransform>(), 1, 0, 1, 1, -320, 0, 0, 0);

        var hint = GameUi.Place(GameUi.Rect("Hint", body), 0, 0, 1, 0, 0, 0, 0, -64);
        GameUi.Text(hint, "Text", "F9 — открыть/закрыть · Enter — выполнить · стрелки вверх/вниз — история команд", 26, GameUi.Grey,
            TextAlignmentOptions.Center);

        Append("<color=#DDB484>WoG Debug</color> — консоль ERM и команд отладки. Строка, начинающаяся с «!», — код ERM; " +
               "иначе — команда (help — список).\n");
        window.gameObject.SetActive(false);
        log.LogInfo("WoG Debug window built (F9)");
    }

    /// <summary>The window follows the mouse while the title is dragged.</summary>
    void Draggable(RectTransform handle, Canvas canvas)
    {
        GameUi.Img(handle, null, color: new Color(0, 0, 0, 0)); // invisible, catches the mouse
        var trigger = handle.gameObject.AddComponent<EventTrigger>();
        var drag = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
        drag.callback.AddListener(DelegateSupport.ConvertDelegate<UnityAction<BaseEventData>>(new Action<BaseEventData>(e =>
        {
            var pointer = e.TryCast<PointerEventData>();
            if (pointer != null) window.anchoredPosition += pointer.delta / canvas.scaleFactor;
        })));
        trigger.triggers.Add(drag);
    }

    void Keys()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.f9Key.wasPressedThisFrame) ToggleWindow();
        if (!Visible || input == null || !input.isFocused || history.Count == 0) return;
        if (kb.upArrowKey.wasPressedThisFrame) Recall(-1);
        else if (kb.downArrowKey.wasPressedThisFrame) Recall(+1);
    }

    void Recall(int step)
    {
        historyAt = Math.Clamp(historyAt + step, 0, history.Count);
        input.text = historyAt < history.Count ? history[historyAt] : "";
        input.caretPosition = input.text.Length;
    }

    void ToggleWindow()
    {
        if (window == null) return;
        bool show = !window.gameObject.activeSelf;
        window.gameObject.SetActive(show);
        if (show) input.ActivateInputField();
    }

    void Submit(string line)
    {
        line = line.Trim();
        if (line.Length == 0) return;
        if (history.Count == 0 || history[^1] != line) history.Add(line);
        historyAt = history.Count;
        input.text = "";
        Run(line.StartsWith("!") ? "erm " + line : line, line);
        input.ActivateInputField();
    }

    void Run(string command) => Run(command, command);

    void Run(string command, string shown)
    {
        string result;
        try { result = commands.Execute(command); }
        catch (Exception ex) { result = "error: " + ex.Message; }
        string first = result.TrimStart().Split('\n')[0];
        string color = first.StartsWith("Error") || first.StartsWith("error") ? "#FF7A6E"
            : first.StartsWith("Unsupported") ? "#FFC85A"
            : first.StartsWith("Pass") ? "#9BE08C" : "#E6E6E6";
        Append($"<color=#DDB484>› {Escape(shown)}</color>\n<color={color}>{Escape(result.TrimEnd())}</color>\n\n");
    }

    static string Escape(string s) => "<noparse>" + s.Replace("</noparse>", "</ noparse>") + "</noparse>";

    void Append(string rich)
    {
        output.Append(rich);
        if (output.Length > MaxOutput)
        {
            // drop whole entries from the start (an entry ends with an empty line)
            int cut = output.Length - MaxOutput;
            string s = output.ToString();
            int at = s.IndexOf("\n\n", cut, StringComparison.Ordinal);
            output.Clear().Append(at > 0 ? s[(at + 2)..] : s[cut..]);
        }
        text.text = output.ToString();
        Canvas.ForceUpdateCanvases();
        scroll.verticalNormalizedPosition = 0; // the newest at the bottom
    }
}
