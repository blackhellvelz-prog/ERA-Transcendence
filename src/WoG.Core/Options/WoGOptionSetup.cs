using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace WoG.Core.Options;

/// <summary>One item of the WoG Options dialog: a row of ZSETUP01.TXT or of a script's .ers file.</summary>
public sealed class WoGSetupItem
{
    public string Comment { get; init; } = "";
    public int Script { get; init; } = -1;
    public int Page { get; init; }
    public int Group { get; init; }
    /// <summary>The place in its group; -1 in the file: the next free one.</summary>
    public int Item { get; set; }
    /// <summary>The state the dialog starts with: 1 checked (or the selected radio button), 0 not.</summary>
    public int Default { get; init; }
    public int Multiplayer { get; init; }
    /// <summary>The WoG option it sets (PL_WoGOptions index), -1 none.</summary>
    public int Option { get; init; } = -1;
    public string Text { get; init; } = "";
    public string Hint { get; init; } = "";
    public string PopUp { get; init; } = "";
}

/// <summary>
/// The WoG Options dialog's items and the option values it starts with (wogsetup.cpp): BuildAll adds every row of
/// ZSETUP01.TXT (from the third) and every row of the scripts' .ers files (Comment, Script, Page, Group, Item, State,
/// MP, ERM option, text, hint, pop-up; a row without text is skipped; Item -1 takes the next place; at most 20 items
/// a group) with its State as the item's state. Without a saved preset (WoGSetupEx.dat) these states are the options:
/// Prepare2Close writes a check box group's items into their options (1..4 inverted: the dialog asks "towers gain
/// experience", the option stores "standard towers"), and a radio group (page 0 group 0 — WoGify, page 0 group 3,
/// page 4 group 0) the number of its selected item into the option of its first item. A new map copies these values
/// into the game's options (ResetWogify). The texts may be ERA language keys (wog_options.724.name).
/// </summary>
public sealed class WoGOptionSetup
{
    public const int Pages = 8, Groups = 4, ItemsPerGroup = 20;

    /// <summary>[page, group] → items by their place (null: empty place).</summary>
    readonly WoGSetupItem?[,][] groups = new WoGSetupItem?[Pages, Groups][];

    public WoGOptionSetup()
    {
        for (int p = 0; p < Pages; p++)
            for (int g = 0; g < Groups; g++) groups[p, g] = new WoGSetupItem?[ItemsPerGroup];
    }

    /// <summary>The radio groups of WoG 3.58 (InitWoGSetup "Dependance": DlgItems[0][0], [0][3], [4][0]).</summary>
    public static bool IsRadio(int page, int group) => (page == 0 && group == 0) || (page == 0 && group == 3) || (page == 4 && group == 0);

    public IReadOnlyList<WoGSetupItem?> Group(int page, int group) => groups[page, group];

    public IEnumerable<WoGSetupItem> Items =>
        Enumerable.Range(0, Pages).SelectMany(p => Enumerable.Range(0, Groups).SelectMany(g => groups[p, g])).Where(i => i != null)!;

    /// <summary>The rows of a ZSETUP01.TXT (first row 2) or .ers (first row 0) text: tab-separated, CRLF records.</summary>
    public static List<WoGSetupItem> Parse(string text, int firstRow)
    {
        var items = new List<WoGSetupItem>();
        var rows = text.Replace("\r\n", "\n").Split('\n');
        for (int r = firstRow; r < rows.Length; r++)
        {
            var c = rows[r].Split('\t');
            string Cell(int i) => i < c.Length ? c[i] : "";
            if (Cell(8).Length == 0) continue; // AddItem: an item without text is not added
            items.Add(new WoGSetupItem
            {
                Comment = Cell(0).Trim(), Script = Int(Cell(1)), Page = Int(Cell(2)), Group = Int(Cell(3)), Item = Int(Cell(4)),
                Default = Int(Cell(5)), Multiplayer = Int(Cell(6)), Option = Int(Cell(7)), Text = Cell(8), Hint = Cell(9), PopUp = Cell(10),
            });
        }
        return items;
    }

    // a2i: the leading integer, 0 when there is none
    static int Int(string s)
    {
        s = s.Trim();
        int end = 0;
        if (end < s.Length && (s[end] == '-' || s[end] == '+')) end++;
        while (end < s.Length && char.IsDigit(s[end])) end++;
        return int.TryParse(s.AsSpan(0, end), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int v) ? v : 0;
    }

    /// <summary>AddItem: puts the items into their pages and groups in order (a later item takes an earlier one's place).</summary>
    public void Add(IEnumerable<WoGSetupItem> items)
    {
        foreach (var item in items)
        {
            if (item.Page < 0 || item.Page >= Pages || item.Group < 0 || item.Group >= Groups) continue;
            var list = groups[item.Page, item.Group];
            int count = Count(list);
            int place = item.Item == -1 ? count : item.Item;
            if (place < 0 || place >= ItemsPerGroup) continue;
            item.Item = place;
            list[place] = item;
        }
    }

    // ItemCount: one past the highest place used
    static int Count(WoGSetupItem?[] list)
    {
        for (int i = list.Length - 1; i >= 0; i--) if (list[i] != null) return i + 1;
        return 0;
    }

    /// <summary>The option values of the dialog's starting states (Prepare2Close over every page and group in order).</summary>
    public WoGOptions Defaults()
    {
        var o = new WoGOptions();
        for (int p = 0; p < Pages; p++)
            for (int g = 0; g < Groups; g++)
            {
                var list = groups[p, g];
                if (Count(list) == 0) continue;
                if (IsRadio(p, g))
                {
                    int option = list[0]?.Option ?? -1;
                    if (option < 0 || option >= WoGOptionIds.Count) continue;
                    int selected = Array.FindIndex(list, i => i != null && i.Default == 1);
                    if (selected >= 0) o.Values[option] = selected;
                    continue;
                }
                foreach (var item in list)
                {
                    if (item == null || item.Option < 0 || item.Option >= WoGOptionIds.Count) continue;
                    o.Values[item.Option] = WoGOptionIds.IsInverted(item.Option) ? (item.Default == 0 ? 1 : 0) : item.Default;
                }
            }
        return o;
    }
}
