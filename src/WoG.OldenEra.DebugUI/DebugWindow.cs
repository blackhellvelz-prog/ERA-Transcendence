using System;
using System.Collections.Generic;
using System.Linq;
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
/// The WoG Debug window. Left: the feature groups as round tabs; middle: the group's features as tiles with round
/// icons (HoMM3, WoG and ERA pictures of the ERA installation, Olden Era sprites), a search over all of them and a line
/// telling what the feature under the mouse does; right: round buttons for the frequent commands, the console (a line
/// starting with "!" is ERM code, anything else a debug command such as "state" or "vars v 1 10") and its input.
/// Its hotkey (F8 by default; F9 is the game's quick load) or the round "WoG" button in the top bar, next to the
/// game's own buttons and away from the players' banners that grow down the left edge, opens and closes it. Built
/// from the game's own interface pieces (GameUi).
/// </summary>
internal sealed class DebugWindow
{
    const int MaxOutput = 14000; // characters kept: a TextMeshPro mesh has a vertex limit
    const string StatusHint = "Наведите курсор на функцию — здесь появится, что она делает; щелчок — выполнить.";

    readonly DebugCommands commands;
    readonly ManualLogSource log;
    readonly Key hotkey;
    readonly List<string> history = new();
    readonly StringBuilder output = new();
    readonly List<(FeatureGroup Group, RoundIcon Icon, TextMeshProUGUI Label)> tabs = new();
    readonly List<(RectTransform Rect, Feature Feature)> shown = new();
    int historyAt;
    int nextTry;
    bool failed;
    bool? gameHotkeys; // the game's hotkey switch before text was typed into the window (null: not taken)
    bool hotkeysFailed; // switching them failed once: not tried again

    GameObject root;
    RectTransform window;
    TMP_InputField input, search;
    TextMeshProUGUI text, groupTitle, status;
    ScrollRect scroll, tilesScroll;
    RectTransform tiles;
    FeatureGroup current;
    Feature hovered;

    public DebugWindow(DebugCommands commands, ManualLogSource log, Key hotkey)
    {
        this.commands = commands;
        this.log = log;
        this.hotkey = hotkey;
        Icons.Log = log;
    }

    bool Visible => window != null && window.gameObject.activeSelf;

    /// <summary>Every frame: builds the window once the game's interface is loaded, then reads its keys and the mouse.</summary>
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
            Hover();
            GuardHotkeys();
        }
        catch (Exception ex)
        {
            failed = true; // one report, not one per frame
            log.LogError("WoG Debug window stopped: " + ex);
        }
    }

    // window layout, in the 3840x2160 units of the game's windows
    const float WindowW = 3000, WindowH = 1680;
    const float TabsW = 190, MidL = 220, MidR = 1500, RightL = 1530, Top = 100;

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

        // the toggle in the top bar, right of the game's two buttons (adventure map) and of the hero's portrait, left of
        // the turn queue (battle); the players' banners grow down the left edge with the number of players
        var (toggle, _, _) = RoundUi.IconButton(top, "WoG toggle", null, "", ToggleWindow, 84, 1, "WoG");
        Pin(toggle.GetComponent<RectTransform>(), 0, 1, new Vector2(420, -4));

        window = GameUi.Place(GameUi.Rect("Window", top), 0.5f, 0.5f, 0.5f, 0.5f, -WindowW / 2, -WindowH / 2, -WindowW / 2, -WindowH / 2);
        GameUi.Frame(window);
        var body = GameUi.Place(GameUi.Rect("Body", window), 0, 0, 1, 1, 110, 100, 110, 70);

        var title = GameUi.Place(GameUi.Rect("Title", body), 0, 1, 1, 1, 0, -90, 0, 0);
        GameUi.Text(title, "Header", "ERA:Transcendence · WoG Debug", 46, GameUi.Gold, TextAlignmentOptions.Center, header: true);
        Draggable(title, canvas);
        var (close, _, _) = RoundUi.IconButton(body, "Close", "h3:iCANCEL.def#0|h3:wogbttn.def#3", "", ToggleWindow, 76, 1, "X");
        Pin(close.GetComponent<RectTransform>(), 1, 1, new Vector2(10, 4));

        // left: a round tab for each feature group
        var tabColumn = GameUi.Place(GameUi.Rect("Tabs", body), 0, 0, 0, 1, 0, 0, -TabsW, Top - 10);
        var tabLayout = tabColumn.gameObject.AddComponent<VerticalLayoutGroup>();
        tabLayout.spacing = 4;
        tabLayout.childAlignment = TextAnchor.UpperCenter;
        tabLayout.childControlWidth = tabLayout.childControlHeight = true;
        tabLayout.childForceExpandWidth = tabLayout.childForceExpandHeight = false;
        float tabSize = FeatureCatalog.Groups.Count > 8 ? 88 : 108;
        foreach (var g in FeatureCatalog.Groups)
        {
            var group = g;
            var (_, icon, label) = RoundUi.IconButton(tabColumn, "Tab " + g.Tab, g.Icon, g.Tab, () => ShowGroup(group), tabSize, 30, g.Tab[..1]);
            tabs.Add((g, icon, label));
        }

        // middle: the group's title, the search and the tiles; under them what the hovered feature does
        var head = GameUi.Place(GameUi.Rect("Group", body), 0, 1, 0, 1, MidL, -(Top + 96), -MidR, Top);
        groupTitle = GameUi.Text(GameUi.Place(GameUi.Rect("Title", head), 0, 0, 1, 1, 0, 0, 540, 0), "Text", "", 42, GameUi.Gold,
            TextAlignmentOptions.MidlineLeft, header: true);
        groupTitle.richText = true;
        search = GameUi.Input(GameUi.Place(GameUi.Rect("Search", head), 1, 0, 1, 1, -520, 10, 0, 10), "Поиск по всем функциям…", _ => { });
        GameUi.Stretch(search.GetComponent<RectTransform>());
        search.onValueChanged.AddListener(DelegateSupport.ConvertDelegate<UnityAction<string>>(new Action<string>(Search)));

        var tileArea = GameUi.Place(GameUi.Rect("Features", body), 0, 0, 0, 1, MidL, 84, -MidR, Top + 106);
        (tilesScroll, tiles) = GameUi.Grid(tileArea, new Vector2(578, 116), new Vector2(12, 12), 2);
        status = GameUi.Text(GameUi.Place(GameUi.Rect("Status", body), 0, 0, 0, 0, MidL + 10, 0, -MidR, -80), "Text", StatusHint, 28,
            GameUi.Grey, TextAlignmentOptions.MidlineLeft);
        status.richText = true;
        status.enableAutoSizing = true;
        status.fontSizeMin = 20;
        status.fontSizeMax = 28;

        // right: round buttons for the frequent commands, the console and its input line
        var bar = GameUi.Place(GameUi.Rect("Toolbar", body), 0, 1, 1, 1, RightL, -(Top + 190), 0, Top);
        var layout = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        foreach (var t in FeatureCatalog.Tools)
        {
            var tool = t;
            RoundUi.IconButton(bar, t.Label, t.Icon, t.Label, () => RunTool(tool), 100, 28, t.Label[..1]);
        }

        var outRect = GameUi.Place(GameUi.Rect("Output", body), 0, 0, 1, 1, RightL, 190, 0, Top + 200);
        (scroll, text) = GameUi.Output(outRect);

        var line = GameUi.Place(GameUi.Rect("Line", body), 0, 0, 1, 0, RightL, 80, 0, -160);
        input = GameUi.Input(line, "!!HE-1:… — ERM, или команда: state, hero, vars v 1 10, help", Submit);
        GameUi.Place(input.GetComponent<RectTransform>(), 0, 0, 1, 1, 0, 0, 340, 0);
        var run = GameUi.Button(line, "Run", "Выполнить", () => Submit(input.text), size: 32);
        GameUi.Place(run.GetComponent<RectTransform>(), 1, 0, 1, 1, -320, 0, 0, 0);

        var hint = GameUi.Place(GameUi.Rect("Hint", body), 0, 0, 1, 0, RightL, 0, 0, -64);
        GameUi.Text(hint, "Text", $"{hotkey} — открыть/закрыть · Enter — выполнить · стрелки вверх/вниз — история · заголовок — перетащить окно",
            24, GameUi.Grey, TextAlignmentOptions.Center);

        Append("<color=#DDB484>WoG Debug</color> — консоль ERM и команд отладки. Строка, начинающаяся с «!», — код ERM; " +
               "иначе — команда (help — список). Слева — функции порта по группам: щелчок по плитке выполняет её.\n\n");
        ShowGroup(FeatureCatalog.Groups[0]);
        window.gameObject.SetActive(false);
        log.LogInfo($"WoG Debug window built ({hotkey})");
    }

    /// <summary>A fixed-size widget at a corner of its parent (anchor 0/1 on each axis), <paramref name="offset"/> inward.</summary>
    static void Pin(RectTransform rt, float ax, float ay, Vector2 offset)
    {
        var size = rt.GetComponent<LayoutElement>() is { } e ? new Vector2(e.preferredWidth, e.preferredHeight) : rt.sizeDelta;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(ax, ay);
        rt.sizeDelta = size;
        rt.anchoredPosition = new Vector2(ax == 0 ? offset.x : -offset.x, ay == 0 ? offset.y : offset.y);
    }

    void ShowGroup(FeatureGroup g)
    {
        current = g;
        if (search.text.Length > 0) search.text = ""; // calls Search, which fills the tiles
        else Fill(FeatureCatalog.All.Where(f => f.Group == g.Name), g.Name);
        foreach (var (group, icon, label) in tabs)
        {
            icon.Select(group == g);
            label.color = group == g ? GameUi.Gold : GameUi.Grey;
        }
    }

    void Search(string query)
    {
        query = query.Trim();
        if (query.Length == 0)
        {
            Fill(FeatureCatalog.All.Where(f => f.Group == current.Name), current.Name);
            return;
        }
        bool Has(string s) => s != null && s.Contains(query, StringComparison.OrdinalIgnoreCase);
        Fill(FeatureCatalog.All.Where(f => Has(f.Label) || Has(f.Note) || Has(f.Command) || Has(f.Group)), "Поиск: " + query);
    }

    void Fill(IEnumerable<Feature> features, string title)
    {
        if (Icons.OeMissed) { GameUi.ResetSprites(); Icons.OeMissed = false; } // the game may have loaded them by now
        for (int i = tiles.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(tiles.GetChild(i).gameObject);
        shown.Clear();
        hovered = null;
        status.text = StatusHint;
        int n = 0;
        foreach (var f in features)
        {
            var feature = f;
            var tile = RoundUi.Tile(tiles, "Feature " + n++, f.Icon, f.Label, () => RunFeature(feature));
            shown.Add((tile.GetComponent<RectTransform>(), f));
        }
        groupTitle.text = $"{Escape(title)}  <color=#A6A6A6><size=70%>{n}</size></color>";
        tilesScroll.verticalNormalizedPosition = 1;
    }

    /// <summary>
    /// While text is typed into the window the game's hotkeys are off (it binds most letters, Enter and Space: typing
    /// "hero" would act on the map), and back as they were when the typing ends.
    /// </summary>
    void GuardHotkeys()
    {
        if (hotkeysFailed) return;
        bool typing = Visible && (input.isFocused || search.isFocused);
        try
        {
            if (typing && gameHotkeys == null)
            {
                gameHotkeys = WoGPlugin.GameHotkeys ?? true;
                WoGPlugin.GameHotkeys = false;
            }
            else if (!typing && gameHotkeys != null)
            {
                WoGPlugin.GameHotkeys = gameHotkeys;
                gameHotkeys = null;
            }
        }
        catch (Exception ex)
        {
            hotkeysFailed = true;
            log.LogWarning("WoG Debug window: the game's hotkeys could not be switched: " + ex.Message);
        }
    }

    /// <summary>The line under the tiles tells what the feature under the mouse does.</summary>
    void Hover()
    {
        if (!Visible || Mouse.current == null) return;
        var pos = Mouse.current.position.ReadValue();
        Feature over = null;
        if (RectTransformUtility.RectangleContainsScreenPoint(tilesScroll.viewport, pos, null))
            foreach (var (rt, f) in shown)
                if (rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, pos, null)) { over = f; break; }
        if (over == hovered) return;
        hovered = over;
        status.text = over == null ? StatusHint
            : $"<color=#DDB484>{Escape(over.Label)}</color>" + (over.Note != null ? " — " + Escape(over.Note) : "") +
              $"  <color=#7F7F7F>{Escape(Shorten(over.Command, 90))}</color>";
    }

    static string Shorten(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";

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
        if (kb[hotkey].wasPressedThisFrame) ToggleWindow();
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
        else EventSystem.current?.SetSelectedGameObject(null); // give the keyboard back to the game
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

    void RunTool(ToolButton t)
    {
        if (t.Command == "clear")
        {
            output.Clear();
            text.text = "";
            return;
        }
        Run(t.Command);
    }

    void Run(string command) => Run(command, command);

    void RunFeature(Feature f)
    {
        Run(f.Command, f.Label + (f.Note != null ? "  (" + f.Note + ")" : ""));
        if (f.Then != null) Run(f.Then);
    }

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
