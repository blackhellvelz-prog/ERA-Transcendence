using System.Collections.Generic;

namespace WoG.OldenEra.DebugUI;

/// <summary>A button of the window's feature list: a debug command (often "erm ..."), then optionally a second one
/// that shows the result (such as "hero"). Note tells what the x1.. values printed after an ERM snippet mean.</summary>
internal sealed class Feature
{
    public Feature(string group, string label, string command, string then = null, string note = null)
    {
        Group = group;
        Label = label;
        Command = command;
        Then = then;
        Note = note;
    }

    public string Group { get; }
    public string Label { get; }
    public string Command { get; }
    public string Then { get; }
    public string Note { get; }
}

/// <summary>
/// What the port can do, as buttons, so the user can click through every feature in the game. Each new feature gets
/// its buttons here when it is implemented. ERM snippets act on the active hero of the current player (y1) and
/// return values in x1..x16, which the console prints.
/// </summary>
internal static class FeatureCatalog
{
    const string ActiveHero = "!!OW:A-1/?y1;";
    const string HeroSquare = ActiveHero + " !!HEy1:P?y2/?y3/?y4;";

    static Feature Hero(string label, string erm, string note = null) =>
        new("Герой (активный)", label, "erm " + ActiveHero + " " + erm, "hero", note);

    // {town}: the town the active hero visits, else the current player's first town (WoG Debug fills it in)
    static Feature Town(string label, string erm, string note = null) =>
        new("Город", label, "erm " + erm.Replace("CA:", "CA0/{town}:"), "town {town}", note);

    public static readonly IReadOnlyList<Feature> All = new List<Feature>
    {
        new("Герой (активный)", "Сводка героя", "hero"),
        new("Герой (активный)", "Поднавыки навыков", "subskills"),
        Hero("Основные навыки (HE:F, F…/1)", "!!HEy1:F?x1/?x2/?x3/?x4; !!HEy1:F?x5/?x6/?x7/?x8/1;",
            "x1..x4 — атака, защита, сила магии, знание с артефактами; x5..x8 — без них"),
        Hero("Атака +1 (HE:F)", "!!HEy1:F?y2/?y3/?y4/?y5; !!VRy2:+1; !!HEy1:Fy2/y3/y4/y5;"),
        Hero("Опыт +500 (HE:E)", "!!HEy1:E?y2; !!VRy2:+500; !!HEy1:Ey2;", "опыт идёт через игру: повышение уровня с её окном выбора"),
        Hero("Мана = 50 (HE:I)", "!!HEy1:I50;"),
        Hero("Очки движения +500 (HE:W)", "!!HEy1:W?y2; !!VRy2:+500; !!HEy1:Wy2;"),
        Hero("Удача: +1 уровень (HE:S9)", "!!HEy1:S9/?y2; !!VRy2:+1; !!HEy1:S9/y2;", "на уровнях 2 и 3 игра предложит выбрать поднавык"),
        Hero("Логистика: +1 уровень (HE:S2)", "!!HEy1:S2/?y2; !!VRy2:+1; !!HEy1:S2/y2;"),
        Hero("Разведка: изучить (HE:S3)", "!!HEy1:S3/1;"),
        Hero("Мудрость: нет в Olden Era (HE:S7)", "!!HEy1:S7/1;", "ожидается Unsupported: аналога нет"),
        Hero("Слоты навыков (HE:S$, S#/?/1)", "!!HEy1:S?x1 S1/?x2/1 S2/?x3/1;", "x1 — показано навыков, x2/x3 — навыки в слотах 1 и 2"),
        Hero("Выучить Молнию (HE:M17)", "!!HEy1:M17/1;"),
        Hero("Забыть Молнию (HE:M17)", "!!HEy1:M17/0;"),
        Hero("Выучить Ускорение (HE:M53)", "!!HEy1:M53/1;"),
        Hero("Знает ли Замедление (HE:M54)", "!!HEy1:M54/?x1;", "x1 — 1, если знает (у специалиста — «Паутина»)"),
        Hero("Корона мага на голову (HE:A1)", "!!HEy1:A1/22/0;"),
        Hero("Драконья броня: надеть (HE:A4)", "!!HEy1:A4/40;"),
        Hero("Подзорная труба в рюкзак (HE:A)", "!!HEy1:A53;"),
        Hero("Свиток Молнии в рюкзак (HE:A1018)", "!!HEy1:A1018;"),
        Hero("Корона в слот разного (HE:A1)", "!!HEy1:A1/22/10;", "ожидается Unsupported: предмет не подходит к слоту"),
        Hero("Сколько труб (HE:A2)", "!!HEy1:A2/53/?x1/?x2;", "x1 — всего, x2 — надето"),
        Hero("Убрать трубы и свитки (HE:A-)", "!!HEy1:A-53 A-1;"),
        Hero("Снять корону и броню (HE:A-)", "!!HEy1:A-22 A-40;"),

        new("Город", "Список городов (CA0/n)", "town"),
        new("Город", "Сводка города", "town {town}"),
        Town("Владелец, тип, позиция, герои (CA:O/T/P/H)", "!!CA:O?x1 T?x2 P?x3/?x4/?x5 H0/?x6 H1/?x7;",
            "x1 — владелец, x2 — тип (0 Замок … 8 Сопряжение), x3..x5 — позиция, x6/x7 — герой в гарнизоне / гость"),
        Town("Ратуша, форт, жилище 7+ (CA:B3)", "!!VRx1:S0; !!VRx2:S0; !!VRx3:S0; !!CA:B3/11; !!VRx1&1:S1; !!CA:B3/7; !!VRx2&1:S1; !!CA:B3/43; !!VRx3&1:S1;",
            "1 — построено: x1 ратуша, x2 форт, x3 улучшенное жилище 7"),
        Town("Построить форт (CA:B1/7)", "!!CA:B1/7;", "строит игра, без цены и без траты стройки дня"),
        Town("Построить цитадель (CA:B6/8)", "!!CA:B6/8;"),
        Town("Гильдия магов 2 (CA:B6/1)", "!!CA:B6/1;"),
        Town("Запретить гильдию 3 (CA:B5/2)", "!!CA:B5/2 B3/2/2; !!VRx1:S0; !!VRx1&1:S1;", "x1 — 1, если разрешено (после запрета 0)"),
        Town("Разрешить гильдию 3 (CA:B4/2)", "!!CA:B4/2 B3/2/2; !!VRx1:S0; !!VRx1&1:S1;", "x1 — 1, если разрешено"),
        Town("Снести форт (CA:B2/7)", "!!CA:B2/7;", "ожидается Unsupported: в Olden Era сноса нет"),
        Town("Существа 1 уровня +10 (CA:M1)", "!!CA:M1/0/d10/d10 M1/0/?x1/?x2;", "x1/x2 — в простом / улучшенном жилище (одно число в Olden Era)"),
        Town("Прирост 7 уровня (CA:M4)", "!!CA:M4/6/?x1;", "x1 — недельный прирост"),
        Town("Гарнизон: 5 копейщиков в слот 0 (CA:M2)", "!!CA:M2/0/0/5 M2/0/?x1/?x2;", "x1/x2 — тип и число в слоте 0"),
        Town("Гарнизон: очистить слот 0 (CA:M2)", "!!CA:M2/0/-1/0;"),
        Town("Строился сегодня → 0 (CA:R)", "!!CA:R?x1 R0 R?x2;", "x1 — до, x2 — после (0 — можно строить)"),
        Town("Уровень гильдии, доход (CA:G, S)", "!!CA:G?x1 S?x2;", "x1 — уровень гильдии магов, x2 — доход золота"),
        Town("Переименовать (CA:N)", "!!CA:N^Твердыня WoG^;"),

        new("Игрок", "Ресурсы (OW:R)", "erm !!OW:R-1/0/?x1 R-1/1/?x2 R-1/2/?x3 R-1/3/?x4 R-1/4/?x5 R-1/5/?x6 R-1/6/?x7;",
            note: "x1..x7 — дерево, ртуть, руда, сера, кристаллы, самоцветы, золото"),
        new("Игрок", "Золото +1000 (OW:R)", "erm !!OW:R-1/6/d1000 R-1/6/?x1;", note: "x1 — золото после"),
        new("Игрок", "Сложность (UN:J2)", "erm !!UN:J2/?x1;", note: "x1 — сложность ИИ"),

        new("Карта", "Размер карты (UN:X)", "erm !!UN:X?x1/?x2;", note: "x1 — размер, x2 — есть ли подземелье"),
        new("Карта", "Клетка героя (OB, TR)", "erm " + HeroSquare + " !!VRx1:Sy2; !!VRx2:Sy3; !!OBy2/y3/y4:T?x3/1 U?x4; !!TRy2/y3/y4:T?x5/?y5/?y6/?y7/?x6/?y8/?y9/?x7;",
            note: "x1/x2 — координаты, x3/x4 — тип/подтип объекта под героем, x5 — почва, x6 — дорога, x7 — флаги (1 — заблокирована, 16 — вход)"),
        new("Карта", "PO: +1 к числу клетки героя", "erm " + HeroSquare + " !!POy2/y3/y4:N?y5; !!VRy5:+1; !!POy2/y3/y4:Ny5 N?x1;",
            note: "x1 — число клетки (0..15, данные WoG, сохраняются)"),

        new("Существа (MA)", "Копейщик: атака, защита, ОЗ", "erm !!MA:A0/?x1 D0/?x2 P0/?x3;", note: "x1 — атака, x2 — защита, x3 — здоровье"),
        new("Существа (MA)", "Копейщик: атака +5", "erm !!MA:A0/d5 A0/?x1;", note: "x1 — атака после"),
        new("Существа (MA)", "Копейщик: атака −5", "erm !!MA:A0/d-5 A0/?x1;", note: "x1 — атака после"),

        new("Отладка", "Состояние игры", "state"),
        new("Отладка", "Новый день (события дня)", "newday"),
        new("Отладка", "Самопроверка WoG/ERA", "selftest"),
        new("Отладка", "Неподдержанное за сессию", "compat"),
        new("Отладка", "Символы игры", "symbols"),
        new("Отладка", "Справка по командам", "help"),
    };
}
