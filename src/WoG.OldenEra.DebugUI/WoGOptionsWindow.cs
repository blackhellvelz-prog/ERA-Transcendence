using System;
using System.Collections.Generic;
using BepInEx.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using WoG.Core.Options;

namespace WoG.OldenEra.DebugUI;

/// <summary>
/// The WoG Options window (wogsetup.cpp's dialog) in the game's own style: the eight pages as tabs, each with its four
/// groups (upper left, lower left, upper right, lower right, as ZSETUP01 numbers them 0..3), the items of the
/// installation's ZSETUP01.TXT and the mods' .ers files with their ERA translations (wog_options.N.name), check boxes
/// and the radio groups (WoGify, AI stack experience, map rules). The item under the mouse shows its hint and its
/// full text. "Save" makes the marks the options of every new map and keeps them (as WoG's WoGSetupEx.dat), "Default"
/// shows the installation's defaults again. As in WoG, a running game keeps the options it started with. Its hotkey
/// (F6 by default) or the round button in the top bar opens it, with or without WoG Debug.
/// </summary>
internal sealed class WoGOptionsWindow
{
    const string StatusHint = "Наведите курсор на пункт — здесь появится его описание; щелчок — отметить.";

    // ZSETUP00.TXT's page and group names (English there), in Russian; groups by ZSETUP01's numbers 0..3
    static readonly (string Tab, string Title)[] PageNames =
    {
        ("WoGификация", "Настройки WoGификации, улучшения игры и встроенные опции"),
        ("Карта", "Опции и улучшения карты"),
        ("Герои и монстры", "Навыки, монстры и герои: улучшения и баланс"),
        ("Объекты", "Объекты и артефакты"),
        ("Правила карты", "Правила карты"),
        ("Запреты", "Запрет артефактов и заклинаний"),
        ("WoG Team", "Новые скрипты команды WoG"),
        ("Свои скрипты", "Пользовательские скрипты"),
    };

    static readonly string[,] GroupNames =
    {
        { "Настройки WoGификации", "Улучшения игры", "Встроенные опции", "Сложность опыта отрядов ИИ" },
        { "Опции карты", "Улучшения карты", "Улучшения карты", "Улучшения карты" },
        { "Герои: улучшения и баланс", "Улучшения навыков", "Монстры: улучшения и баланс", "Улучшения навыков" },
        { "Сундуки и артефакты", "Новые объекты", "Улучшенные объекты", "Новые объекты" },
        { "Опции правил карты", "Правила карты", "", "Правила карты" },
        { "Запрет заклинаний", "Запрещённые артефакты WoG", "Запрещённые артефакты", "Запрещённые артефакты WoG" },
        { "Скрипты команды WoG", "Скрипты команды WoG", "Скрипты команды WoG", "Скрипты команды WoG" },
        { "Пользовательские скрипты", "Пользовательские скрипты", "Пользовательские скрипты", "Пользовательские скрипты" },
    };

    static readonly Color Unchecked = new(0.22f, 0.20f, 0.18f);

    readonly ManualLogSource log;
    readonly Key hotkey;
    readonly List<(Button Button, TextMeshProUGUI Label)> tabs = new();
    readonly RectTransform[] lists = new RectTransform[WoGOptionSetup.Groups];
    readonly TextMeshProUGUI[] groupTitles = new TextMeshProUGUI[WoGOptionSetup.Groups];
    readonly List<(RectTransform Row, Image Mark, WoGSetupItem Item)> rows = new();
    GameObject root;
    RectTransform window;
    TextMeshProUGUI pageTitle, status, details;
    WoGOptionDialog dialog;
    WoGSetupItem hovered;
    int page;
    int nextTry;
    bool failed;
    bool? gameHotkeys;

    public WoGOptionsWindow(ManualLogSource log, Key hotkey)
    {
        this.log = log;
        this.hotkey = hotkey;
    }

    bool Visible => window != null && window.gameObject.activeSelf;

    /// <summary>Every frame: builds the window once the game's interface is loaded, then its key and the mouse.</summary>
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
            if (Keyboard.current != null && Keyboard.current[hotkey].wasPressedThisFrame) Toggle();
            if (Visible) Hover();
        }
        catch (Exception ex)
        {
            failed = true;
            log.LogError("WoG Options window stopped: " + ex);
        }
    }

    // layout, in the 3840x2160 units of the game's windows
    const float WindowW = 3300, WindowH = 1880;

    void Build()
    {
        root = new GameObject("WoG Options window") { layer = 5 };
        UnityEngine.Object.DontDestroyOnLoad(root);
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30500; // above the WoG Debug window, under WoG's questions
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(3840, 2160);
        scaler.matchWidthOrHeight = 0.5f;
        root.AddComponent<GraphicRaycaster>();
        var top = root.GetComponent<RectTransform>();

        // the round button in the top bar, right of WoG Debug's (which shows only with WoG Debug on)
        var (open, _, _) = RoundUi.IconButton(top, "WoG Options toggle", "h3:wogbttn.def#0", "", Toggle, 84, 1, "O");
        var openRect = open.GetComponent<RectTransform>();
        openRect.anchorMin = openRect.anchorMax = openRect.pivot = new Vector2(0, 1);
        openRect.sizeDelta = new Vector2(144, 86);
        openRect.anchoredPosition = new Vector2(530, -4);

        var dim = GameUi.Img(GameUi.Stretch(GameUi.Rect("Dim", top)), null, color: new Color(0, 0, 0, 0.45f)); // keeps the mouse off the game
        window = GameUi.Place(GameUi.Rect("Window", top), 0.5f, 0.5f, 0.5f, 0.5f, -WindowW / 2, -WindowH / 2, -WindowW / 2, -WindowH / 2);
        dim.transform.SetParent(window, false);
        GameUi.Place(dim.rectTransform, 0.5f, 0.5f, 0.5f, 0.5f, -4000, -3000, -4000, -3000);
        dim.transform.SetAsFirstSibling();
        GameUi.Frame(window);
        var body = GameUi.Place(GameUi.Rect("Body", window), 0, 0, 1, 1, 110, 80, 110, 70);

        GameUi.Text(GameUi.Place(GameUi.Rect("Title", body), 0, 1, 1, 1, 0, -90, 0, 0), "Header", "WoG Options · ERA:Transcendence", 46,
            GameUi.Gold, TextAlignmentOptions.Center, header: true);
        var (close, _, _) = RoundUi.IconButton(body, "Close", "h3:iCANCEL.def#0|h3:wogbttn.def#3", "", Toggle, 76, 1, "X");
        var closeRect = close.GetComponent<RectTransform>();
        closeRect.anchorMin = closeRect.anchorMax = closeRect.pivot = new Vector2(1, 1);
        closeRect.sizeDelta = new Vector2(136, 78);
        closeRect.anchoredPosition = new Vector2(-10, 4);

        // the pages as tabs
        var tabRow = GameUi.Place(GameUi.Rect("Pages", body), 0, 1, 1, 1, 0, -200, 0, 104);
        var tabLayout = tabRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        tabLayout.spacing = 10;
        tabLayout.childControlWidth = tabLayout.childControlHeight = true;
        tabLayout.childForceExpandWidth = tabLayout.childForceExpandHeight = true;
        for (int p = 0; p < WoGOptionSetup.Pages; p++)
        {
            int index = p;
            var b = GameUi.Button(tabRow, "Page " + p, PageNames[p].Tab, () => ShowPage(index), size: 30);
            tabs.Add((b, b.GetComponentInChildren<TextMeshProUGUI>()));
        }
        pageTitle = GameUi.Text(GameUi.Place(GameUi.Rect("Page title", body), 0, 1, 1, 1, 0, -270, 0, 206), "Text", "", 38, GameUi.Gold,
            TextAlignmentOptions.Center, header: true);

        // the four groups: upper left (0), lower left (1), upper right (2), lower right (3)
        var area = GameUi.Place(GameUi.Rect("Groups", body), 0, 0, 1, 1, 0, 470, 0, 280);
        var places = new (float X0, float Y0, float X1, float Y1)[] { (0, 0.5f, 0.5f, 1), (0, 0, 0.5f, 0.5f), (0.5f, 0.5f, 1, 1), (0.5f, 0, 1, 0.5f) };
        for (int g = 0; g < WoGOptionSetup.Groups; g++)
        {
            var (x0, y0, x1, y1) = places[g];
            var panel = GameUi.Place(GameUi.Rect("Group " + g, area), x0, y0, x1, y1, x0 == 0 ? 0 : 24, y0 == 0 ? 0 : 14, x1 == 1 ? 0 : 24, y1 == 1 ? 0 : 14);
            GameUi.Img(panel, null, color: new Color(0, 0, 0, 0.28f));
            groupTitles[g] = GameUi.Text(GameUi.Place(GameUi.Rect("Title", panel), 0, 1, 1, 1, 20, -64, 20, 6), "Text", "", 32, GameUi.Gold,
                TextAlignmentOptions.MidlineLeft, header: true);
            lists[g] = GameUi.List(GameUi.Place(GameUi.Rect("Items", panel), 0, 0, 1, 1, 6, 6, 6, 70));
        }

        // what the item under the mouse does, and the buttons
        status = GameUi.Text(GameUi.Place(GameUi.Rect("Status", body), 0, 0, 1, 0, 10, 380, 10, -456), "Text", StatusHint, 30, GameUi.Light,
            TextAlignmentOptions.MidlineLeft);
        status.enableAutoSizing = true;
        status.fontSizeMin = 20;
        status.fontSizeMax = 30;
        details = GameUi.Text(GameUi.Place(GameUi.Rect("Details", body), 0, 0, 1, 0, 10, 110, 10, -372), "Text", "", 24, GameUi.Grey,
            TextAlignmentOptions.TopLeft);
        details.enableAutoSizing = true;
        details.fontSizeMin = 16;
        details.fontSizeMax = 24;
        details.textWrappingMode = TextWrappingModes.Normal;

        var buttons = GameUi.Place(GameUi.Rect("Buttons", body), 0, 0, 1, 0, 0, 0, 0, -96);
        var save = GameUi.Button(buttons, "Save", "Сохранить для новых карт", Save, size: 32);
        GameUi.Place(save.GetComponent<RectTransform>(), 0.5f, 0, 0.5f, 1, -1010, 0, 330, 0);
        var restore = GameUi.Button(buttons, "Default", "По умолчанию", Restore, violet: true, size: 32);
        GameUi.Place(restore.GetComponent<RectTransform>(), 0.5f, 0, 0.5f, 1, -300, 0, -300, 0);
        var cancel = GameUi.Button(buttons, "Cancel", "Закрыть без сохранения", Toggle, violet: true, size: 32);
        GameUi.Place(cancel.GetComponent<RectTransform>(), 0.5f, 0, 0.5f, 1, 330, 0, -1010, 0);
        GameUi.Text(GameUi.Place(GameUi.Rect("Note", body), 0, 0, 1, 0, 0, -60, 0, 4), "Text",
            $"Как в WoG: выбор действует на новые карты; идущая игра сохраняет опции, с которыми началась · {hotkey} — открыть/закрыть",
            22, GameUi.Grey, TextAlignmentOptions.Center);

        window.gameObject.SetActive(false);
        log.LogInfo($"WoG Options window built ({hotkey})");
    }

    void Toggle()
    {
        if (Visible) { Close(); return; }
        var host = WoGPlugin.Host;
        dialog = host?.OptionDialog();
        if (dialog == null)
        {
            WoGPlugin.MessageDialog?.Invoke("WoG Options: нет установки ERA (пункты окна берутся из её ZSETUP01.TXT и файлов .ers модов).", null);
            return;
        }
        window.gameObject.SetActive(true);
        gameHotkeys = WoGPlugin.GameHotkeys;
        WoGPlugin.GameHotkeys = false;
        ShowPage(page);
    }

    void Close()
    {
        window.gameObject.SetActive(false);
        dialog = null;
        if (gameHotkeys == null) return;
        try { WoGPlugin.GameHotkeys = gameHotkeys; }
        catch (Exception ex) { log.LogWarning("WoG Options: the game's hotkeys could not be switched back: " + ex.Message); }
        gameHotkeys = null;
    }

    void Save()
    {
        var host = WoGPlugin.Host;
        if (host == null || dialog == null) return;
        host.SetSetupOptions(dialog.Result(host.SetupOptions));
        try
        {
            host.SaveOptionPreset(WoGPlugin.OptionPresetPath);
            log.LogInfo("WoG Options saved for new maps: " + WoGPlugin.OptionPresetPath);
        }
        catch (Exception ex) { log.LogError("WoG Options: saving failed: " + ex.Message); }
        Close();
    }

    void Restore()
    {
        dialog?.Restore();
        Marks();
    }

    void ShowPage(int p)
    {
        page = p;
        pageTitle.text = PageNames[p].Title;
        for (int i = 0; i < tabs.Count; i++) tabs[i].Label.color = i == p ? GameUi.Gold : GameUi.Light;
        rows.Clear();
        hovered = null;
        status.text = StatusHint;
        details.text = "";
        for (int g = 0; g < WoGOptionSetup.Groups; g++)
        {
            var list = lists[g];
            for (int c = list.childCount - 1; c >= 0; c--) UnityEngine.Object.Destroy(list.GetChild(c).gameObject);
            groupTitles[g].text = GroupNames[p, g];
            if (dialog == null) continue;
            foreach (var item in dialog.Setup.Group(p, g))
                if (item != null) Row(list, item);
        }
        Marks();
    }

    /// <summary>An item: its mark (a square for a check box, a disc for a radio button) and its translated name.</summary>
    void Row(RectTransform list, WoGSetupItem item)
    {
        var rt = GameUi.Rect("Item", list);
        GameUi.Height(rt, 58);
        var hit = GameUi.Img(rt, null, color: new Color(1, 1, 1, 0)); // catches the mouse for the whole row
        var b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = hit;
        b.onClick.AddListener(Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<UnityEngine.Events.UnityAction>(new Action(() =>
        {
            dialog?.Click(item);
            Marks();
        })));
        bool radio = WoGOptionSetup.IsRadio(item.Page, item.Group);
        var markRect = GameUi.Place(GameUi.Rect("Mark", rt), 0, 0.5f, 0, 0.5f, 10, -17, -44, -17);
        var mark = markRect.gameObject.AddComponent<Image>();
        if (radio) mark.sprite = Icons.Dot;
        mark.raycastTarget = false;
        var text = GameUi.Text(GameUi.Place(GameUi.Rect("Text", rt), 0, 0, 1, 1, 64, 0, 8, 0), "Text", Name(item), 28, GameUi.Light,
            TextAlignmentOptions.MidlineLeft);
        text.enableAutoSizing = true;
        text.fontSizeMin = 18;
        text.fontSizeMax = 28;
        text.raycastTarget = false;
        rows.Add((rt, mark, item));
    }

    void Marks()
    {
        foreach (var (_, mark, item) in rows)
            if (mark != null) mark.color = dialog != null && dialog.IsChecked(item) ? GameUi.Gold : Unchecked;
    }

    void Hover()
    {
        if (Mouse.current == null) return;
        var pos = Mouse.current.position.ReadValue();
        WoGSetupItem over = null;
        foreach (var (rt, _, item) in rows)
            if (rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, pos, null) && Within(rt, pos)) { over = item; break; }
        if (over == hovered) return;
        hovered = over;
        status.text = over == null ? StatusHint : $"<color=#DDB484>{Plain(Name(over))}</color> — {Plain(Tr(over.Hint))}";
        details.text = over == null ? "" : Plain(Tr(over.PopUp));
    }

    /// <summary>The row is in the visible part of its scrolling list (a hidden row must not take the hover).</summary>
    static bool Within(RectTransform row, Vector2 pos)
    {
        var viewport = row.parent?.parent as RectTransform;
        return viewport == null || RectTransformUtility.RectangleContainsScreenPoint(viewport, pos, null);
    }

    static string Name(WoGSetupItem item) => Tr(item.Text).Trim('*', ' ');

    static string Tr(string key) => key.Length == 0 || WoGPlugin.Host is not { IsEra: true } host ? key : host.Lang.Tr(key);

    /// <summary>A text as plain TextMeshPro text (no tags of its own).</summary>
    static string Plain(string s) => s.Replace("<", "<noparse><</noparse>");
}
