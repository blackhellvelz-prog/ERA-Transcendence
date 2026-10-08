using System.Collections.Generic;

namespace WoG.OldenEra.DebugUI;

/// <summary>A button of the window's feature list: a debug command (often "erm ..."), then optionally a second one
/// that shows the result (such as "hero"). Note tells what the x1.. values printed after an ERM snippet mean; Icon is
/// its round picture (an <see cref="Icons"/> spec: HoMM3, WoG and ERA pictures of the ERA installation, Olden Era sprites).</summary>
internal sealed class Feature
{
    public Feature(string group, string label, string command, string then = null, string note = null, string icon = null)
    {
        Group = group;
        Label = label;
        Command = command;
        Then = then;
        Note = note;
        Icon = icon;
    }

    public string Group { get; }
    public string Label { get; }
    public string Command { get; }
    public string Then { get; }
    public string Note { get; }
    public string Icon { get; }
}

/// <summary>A tab of the window: the features of a group (<see cref="Feature.Group"/>), its short name and icon.</summary>
internal sealed class FeatureGroup
{
    public FeatureGroup(string name, string tab, string icon) { Name = name; Tab = tab; Icon = icon; }
    public string Name { get; }
    public string Tab { get; }
    public string Icon { get; }
}

/// <summary>A round button of the window's toolbar.</summary>
internal sealed class ToolButton
{
    public ToolButton(string label, string command, string icon) { Label = label; Command = command; Icon = icon; }
    public string Label { get; }
    public string Command { get; }
    public string Icon { get; }
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

    static Feature Hero(string label, string erm, string note = null, string icon = null) =>
        new("Герой (активный)", label, "erm " + ActiveHero + " " + erm, "hero", note, icon);

    // {town}: the town the active hero visits, else the current player's first town (WoG Debug fills it in)
    static Feature Town(string label, string erm, string note = null, string icon = null) =>
        new("Город", label, "erm " + erm.Replace("CA:", "CA0/{town}:"), "town {town}", note, icon);

    // pictures: H3 secondary skills at expert level, spells, artifacts, creature portraits (Secskill.def, spells.def,
    // Artifact.def, TwCrPort.def frames), then Olden Era sprites where the H3 one is missing
    static string Skill(int skill) => "h3:Secskill.def#" + (5 + 3 * skill);
    static string Spell(int spell) => "h3:spells.def#" + spell;
    static string Art(int art) => "h3:Artifact.def#" + art;
    static string Creature(int type) => "h3:TwCrPort.def#" + (type + 2);
    const string Portrait = "h3:HPL000KN.pcx|oe:Icon_Class_Warrior";
    const string Cancel = "h3:wogbttn.def#3|h3:iCANCEL.def#0";

    public static readonly IReadOnlyList<FeatureGroup> Groups = new[]
    {
        new FeatureGroup("Герой (активный)", "Герой", Portrait),
        new FeatureGroup("Город", "Город", "h3:itpt.def#0|oe:Icon_City"),
        new FeatureGroup("Игрок", "Игрок", "h3:CREST58.def#0|oe:Icon_PlayerStatus_KingdomStatistics"),
        new FeatureGroup("Карта", "Карта", Skill(2)),
        new FeatureGroup("Бой (BM)", "Бой", Skill(19) + "|oe:battle_icon_Onesword"),
        new FeatureGroup("Существа (MA)", "Существа", Creature(4)),
        new FeatureGroup("Отладка", "Отладка", "oe:Button_BugReport_Normal|h3:dlg_npc1.def#5"),
    };

    /// <summary>The toolbar over the console; "clear" empties the console.</summary>
    public static readonly IReadOnlyList<ToolButton> Tools = new[]
    {
        new ToolButton("Состояние", "state", "h3:iam002.def#0|oe:Icon_PlayerStatus_KingdomStatistics"),
        new ToolButton("Герой", "hero", Portrait),
        new ToolButton("Самопроверка", "selftest", "h3:wogbttn.def#0|h3:iOKAY.def#0"),
        new ToolButton("Совместимость", "compat", "h3:wogcurse.def#13"),
        new ToolButton("Справка", "help", Skill(8)),
        new ToolButton("Очистить", "clear", "h3:wogbttn.def#12|" + Cancel),
    };

    public static readonly IReadOnlyList<Feature> All = new List<Feature>
    {
        new("Герой (активный)", "Сводка героя", "hero", icon: Portrait),
        new("Герой (активный)", "Поднавыки навыков", "subskills", icon: Skill(21) + "|oe:default_sub_skill_icon_1"),
        Hero("Основные навыки (HE:F, F…/1)", "!!HEy1:F?x1/?x2/?x3/?x4; !!HEy1:F?x5/?x6/?x7/?x8/1;",
            "x1..x4 — атака, защита, сила магии, знание с артефактами; x5..x8 — без них", icon: "oe:Icon_Stats|h3:PSKILL.def#2"),
        Hero("Атака +1 (HE:F)", "!!HEy1:F?y2/?y3/?y4/?y5; !!VRy2:+1; !!HEy1:Fy2/y3/y4/y5;", icon: "h3:PSKILL.def#0|oe:Icon_Stats_Attack"),
        Hero("Опыт +500 (HE:E)", "!!HEy1:E?y2; !!VRy2:+500; !!HEy1:Ey2;", "опыт идёт через игру: повышение уровня с её окном выбора", icon: "h3:PSKILL.def#4|oe:Icon_Stats_Experience"),
        Hero("Мана = 50 (HE:I)", "!!HEy1:I50;", icon: "oe:Icon_Stats_Mana|h3:PSKILL.def#5"),
        Hero("Очки движения +500 (HE:W)", "!!HEy1:W?y2; !!VRy2:+500; !!HEy1:Wy2;", icon: "oe:Icon_Stats_Moving|" + Skill(0)),
        Hero("Удача: +1 уровень (HE:S9)", "!!HEy1:S9/?y2; !!VRy2:+1; !!HEy1:S9/y2;", "на уровнях 2 и 3 игра предложит выбрать поднавык", icon: Skill(9)),
        Hero("Логистика: +1 уровень (HE:S2)", "!!HEy1:S2/?y2; !!VRy2:+1; !!HEy1:S2/y2;", icon: Skill(2)),
        Hero("Разведка: изучить (HE:S3)", "!!HEy1:S3/1;", icon: Skill(3)),
        Hero("Мудрость: нет в Olden Era (HE:S7)", "!!HEy1:S7/1;", "ожидается Unsupported: аналога нет", icon: Skill(7)),
        Hero("Имя, биография, класс (HE:B)", "!!HEy1:B0/?z1 B1/?z2 B3/?z3 B2/?x1;",
            "z1 — имя, z2 — своя биография (пусто, пока не задана), z3 — биография героя, x1 — класс H3 (0 рыцарь … 17 элементалист)", icon: "h3:wogcurse.def#6"),
        Hero("Специализация (HE:X)", "!!HEy1:X?x1/?x2/?x3/?x4/?x5/?x6/?x7;",
            "x1 — тип (0 навык, 1 существо, 2 ресурс, 3 заклинание), x2 — какой; Unsupported — у специализации Olden Era нет аналога H3", icon: "h3:UN44.def#0"),
        Hero("Армия при найме (HE:H)", "!!HEy1:H0/?x1/?x2/?x3 H1/?x4/?x5/?x6 H2/?x7/?x8/?x9;",
            "по слотам 0..2: существо (−1 — нет), минимум, максимум", icon: Creature(0)),
        Hero("Армия при найме: 3–5 грифонов в слоте 2 (HE:H)", "!!HEy1:H2/4/3/5 H2/?x1/?x2/?x3;",
            "действует на героев этого типа, нанятых позже", icon: Creature(4)),
        Hero("Переименовать (HE:B0)", "!!HEy1:B0/^Сэр WoG^;", "имя меняется везде, где игра его показывает; сохраняется с игрой", icon: Skill(24)),
        Hero("Своя биография (HE:B1)", "!!HEy1:B1/^Герой, переживший Войну Богов.^ B1/?z1;", icon: "h3:wogcurse.def#7"),
        Hero("Слоты навыков (HE:S$, S#/?/1)", "!!HEy1:S?x1 S1/?x2/1 S2/?x3/1;", "x1 — показано навыков, x2/x3 — навыки в слотах 1 и 2", icon: Skill(18)),
        Hero("Выучить Молнию (HE:M17)", "!!HEy1:M17/1;", icon: Spell(17)),
        Hero("Забыть Молнию (HE:M17)", "!!HEy1:M17/0;", icon: Spell(17)),
        Hero("Выучить Ускорение (HE:M53)", "!!HEy1:M53/1;", icon: Spell(53)),
        Hero("Знает ли Замедление (HE:M54)", "!!HEy1:M54/?x1;", "x1 — 1, если знает (у специалиста — «Паутина»)", icon: Spell(54)),
        Hero("Корона мага на голову (HE:A1)", "!!HEy1:A1/22/0;", icon: Art(22)),
        Hero("Драконья броня: надеть (HE:A4)", "!!HEy1:A4/40;", icon: Art(40)),
        Hero("Подзорная труба в рюкзак (HE:A)", "!!HEy1:A53;", icon: Art(53)),
        Hero("Свиток Молнии в рюкзак (HE:A1018)", "!!HEy1:A1018;", icon: Art(1)),
        Hero("Корона в слот разного (HE:A1)", "!!HEy1:A1/22/10;", "ожидается Unsupported: предмет не подходит к слоту", icon: Art(22)),
        Hero("Артефакты по слотам (FU GetArtAtSlot)", "!!FU(GetArtAtSlot):Py1/0/?x1/?x2; !!FU(GetArtAtSlot):Py1/19/?x3/?x4;",
            "функция Era Erm Framework (в ERA — память героя H3, здесь — через игру): x1/x2 — артефакт и модификатор на голове, x3/x4 — в первом слоте рюкзака (-1 — пусто, свиток — 1 и номер заклинания)", icon: Art(22)),
        Hero("Навыки без артефактов (FU)", "!!FU(GetHeroPrimarySkillsWithoutArts):Py1/?x1/?x2/?x3/?x4;",
            "функция Era Erm Framework: x1..x4 — атака, защита, сила магии, знание без артефактов", icon: "h3:PSKILL.def#1|oe:Icon_Stats"),
        Hero("Сколько труб (HE:A2)", "!!HEy1:A2/53/?x1/?x2;", "x1 — всего, x2 — надето", icon: Art(53)),
        Hero("Убрать трубы и свитки (HE:A-)", "!!HEy1:A-53 A-1;", icon: Cancel),
        Hero("Снять корону и броню (HE:A-)", "!!HEy1:A-22 A-40;", icon: Cancel),

        new("Город", "Список городов (CA0/n)", "town", icon: "h3:ITPA.def#0|oe:Icon_City"),
        new("Город", "Сводка города", "town {town}", icon: "h3:itpt.def#0|oe:Icon_City"),
        Town("Владелец, тип, позиция, герои (CA:O/T/P/H)", "!!CA:O?x1 T?x2 P?x3/?x4/?x5 H0/?x6 H1/?x7;",
            "x1 — владелец, x2 — тип (0 Замок … 8 Сопряжение), x3..x5 — позиция, x6/x7 — герой в гарнизоне / гость", icon: "h3:CREST58.def#1"),
        Town("Ратуша, форт, жилище 7+ (CA:B3)", "!!VRx1:S0; !!VRx2:S0; !!VRx3:S0; !!CA:B3/11; !!VRx1&1:S1; !!CA:B3/7; !!VRx2&1:S1; !!CA:B3/43; !!VRx3&1:S1;",
            "1 — построено: x1 ратуша, x2 форт, x3 улучшенное жилище 7", icon: "h3:itmtl.def#1"),
        Town("Построить форт (CA:B1/7)", "!!CA:B1/7;", "строит игра, без цены и без траты стройки дня", icon: "h3:itmcl.def#0"),
        Town("Построить цитадель (CA:B6/8)", "!!CA:B6/8;", icon: "h3:itmcl.def#1"),
        Town("Гильдия магов 2 (CA:B6/1)", "!!CA:B6/1;", icon: Skill(25)),
        Town("Запретить гильдию 3 (CA:B5/2)", "!!CA:B5/2 B3/2/2; !!VRx1:S0; !!VRx1&1:S1;", "x1 — 1, если разрешено (после запрета 0)", icon: "h3:wogcurse.def#2"),
        Town("Разрешить гильдию 3 (CA:B4/2)", "!!CA:B4/2 B3/2/2; !!VRx1:S0; !!VRx1&1:S1;", "x1 — 1, если разрешено", icon: "h3:wogcurse.def#8"),
        Town("Снести форт (CA:B2/7)", "!!CA:B2/7;", "ожидается Unsupported: в Olden Era сноса нет", icon: Skill(10)),
        Town("Существа 1 уровня +10 (CA:M1)", "!!CA:M1/0/d10/d10 M1/0/?x1/?x2;", "x1/x2 — в простом / улучшенном жилище (одно число в Olden Era)", icon: Creature(0)),
        Town("Прирост 7 уровня (CA:M4)", "!!CA:M4/6/?x1;", "x1 — недельный прирост", icon: "h3:wogcurse.def#23"),
        Town("Гарнизон: 5 копейщиков в слот 0 (CA:M2)", "!!CA:M2/0/0/5 M2/0/?x1/?x2;", "x1/x2 — тип и число в слоте 0", icon: Creature(1)),
        Town("Гарнизон: очистить слот 0 (CA:M2)", "!!CA:M2/0/-1/0;", icon: Cancel),
        Town("Строился сегодня → 0 (CA:R)", "!!CA:R?x1 R0 R?x2;", "x1 — до, x2 — после (0 — можно строить)", icon: "oe:Button_CityNavigation_Build|h3:itmtl.def#0"),
        Town("Уровень гильдии, доход (CA:G, S)", "!!CA:G?x1 S?x2;", "x1 — уровень гильдии магов, x2 — доход золота", icon: "h3:Resour82.def#6|oe:Icon_Resource_Gold"),
        Town("Переименовать (CA:N)", "!!CA:N^Твердыня WoG^;", icon: Skill(24)),

        new("Игрок", "Ресурсы (OW:R)", "erm !!OW:R-1/0/?x1 R-1/1/?x2 R-1/2/?x3 R-1/3/?x4 R-1/4/?x5 R-1/5/?x6 R-1/6/?x7;",
            note: "x1..x7 — дерево, ртуть, руда, сера, кристаллы, самоцветы, золото", icon: "h3:wogcurse.def#5|h3:Resour82.def#0"),
        new("Игрок", "Золото +1000 (OW:R)", "erm !!OW:R-1/6/d1000 R-1/6/?x1;", note: "x1 — золото после", icon: "oe:Icon_Resource_Gold|h3:RESOURCE.def#6"),
        new("Игрок", "Сложность (UN:J2)", "erm !!UN:J2/?x1;", note: "x1 — сложность ИИ", icon: "h3:skull.def#0|h3:dlg_npc1.def#8"),
        new("Игрок", "Мифрил WoG +5 (OW:R7)", "erm !!OW:R-1/7/d5 R-1/7/?x1;",
            note: "x1 — мифрил игрока после: ресурс WoG, которого в Olden Era нет; порт хранит его и сохраняет с игрой", icon: "h3:RESOURCE.def#7|oe:Icon_Resource_Gold"),
        new("Игрок", "Игрок вне игры (OW:I7)", "erm !!OW:I7/?x1/?x2;",
            note: "в H3 есть все 8 игроков: игрок без стороны в Olden Era — ИИ (x1 = 1) и выбыл (x2 = 1)", icon: "h3:CREST58.def#7|h3:skull.def#0"),
        new("Игрок", "Герой пула H3 (HE19)", "erm !!HE19:O?x1 P?x2/?x3/?x4 B0/?z1;",
            note: "у номера 19 нет героя Olden Era: это герой пула H3 — владелец -1 (x1), не на карте (x2..x4 = -1), имя из hotraits.txt (z1); скрипты WoG обходят всех 156 героев", icon: Portrait),
        new("Игрок", "Герои игрока (OW:H, O)", "erm !!OW:H-1/1; !!VRx1:Sv1; !!VRx2:Sv2; !!VRx3:Sv3; !!OW:O-1/0/?x4;",
            note: "x1 — число героев, x2/x3 — первые по номеру, x4 — первый в списке героев", icon: Skill(6)),
        new("Игрок", "Города, команда, таверна (OW:W, T, V)", "erm !!OW:W-1/?x1 W-1/0/?x2 T-1/?x3 V-1/?x4/?x5;",
            note: "x1 — число городов, x2 — первый город, x3 — команда, x4/x5 — герои в таверне", icon: "oe:Button_CityNavigation_Tavern|h3:ITPA.def#2"),

        new("Карта", "Размер карты (UN:X)", "erm !!UN:X?x1/?x2;", note: "x1 — размер, x2 — есть ли подземелье", icon: "h3:iam003.def#0"),
        new("Карта", "Клетка героя (OB, TR)", "erm " + HeroSquare + " !!VRx1:Sy2; !!VRx2:Sy3; !!OBy2/y3/y4:T?x3/1 U?x4; !!TRy2/y3/y4:T?x5/?y5/?y6/?y7/?x6/?y8/?y9/?x7;",
            note: "x1/x2 — координаты, x3/x4 — тип/подтип объекта под героем, x5 — почва, x6 — дорога, x7 — флаги (1 — заблокирована, 16 — вход)", icon: Skill(0)),
        new("Карта", "PO: +1 к числу клетки героя", "erm " + HeroSquare + " !!POy2/y3/y4:N?y5; !!VRy5:+1; !!POy2/y3/y4:Ny5 N?x1;",
            note: "x1 — число клетки (0..15, данные WoG, сохраняются)", icon: "h3:wogcurse.def#13"),

        new("Карта", "Шахты: число, первая (MN:O/R/M)", "erm !!UN:U53/-1/?x1; !!UN:U53/-1/1/10; !!MN10:O?x2 R?x3 M0/?x4/?x5; !!VRx6:Sv10; !!VRx7:Sv11;",
            note: "x1 — шахт на карте; первая: x2 — владелец (−1 никто), x3 — ресурс (0 дерево … 6 золото), x4/x5 — охрана, x6/x7 — координаты", icon: "h3:wogcurse.def#16|h3:RESOURCE.def#2"),
        new("Карта", "Захватить первую шахту (MN:O)", "erm !!UN:U53/-1/1/10; !!MN10:O-2 O?x1;",
            note: "x1 — новый владелец (текущий игрок); игра меняет владельца сама: флаг, доход", icon: "h3:CREST58.def#0"),

        new("Бой (BM)", "Отряд 0: тип, число, атака, защита, ОЗ", "erm !!BM0:T?x1 N?x2 A?x3 D?x4 H?x5 S?x6 L?x7 B?x8 O?x9;",
            note: "только в бою; x1 — тип, x2 — число, x3/x4 — атака/защита, x5 — ОЗ, x6 — скорость, x7 — потеряно ОЗ верхним, x8 — число в начале, x9 — слот армии", icon: Creature(6)),
        new("Бой (BM)", "Ходящий отряд (BM-1)", "erm !!BM-1:T?x1 N?x2 I?x3;", note: "x1 — тип, x2 — число, x3 — сторона (0 атакующий, 1 защитник)", icon: Creature(4)),
        new("Бой (BM)", "Отряд 0: +10 существ (BM:N)", "erm !!BM0:Nd10 N?x1;", note: "x1 — число после", icon: Creature(7)),
        new("Бой (BM)", "Отряд 0: атака +5 (BM:A)", "erm !!BM0:Ad5 A?x1;", note: "сохраняется при пересчётах игры (модификатор отряда)", icon: "oe:Icon_Stats_Attack|h3:PSKILL.def#0"),
        new("Бой (BM)", "Враг 21: защита −3 (BM:D)", "erm !!BM21:Dd-3 D?x1;", icon: "oe:Icon_Stats_Defence|h3:PSKILL.def#1"),
        new("Бой (BM)", "Триггеры боя (!?BR, !?BG, !?MF)",
            "vars i wogdebug_br wogdebug_round wogdebug_bg0 wogdebug_bg1 wogdebug_action wogdebug_stack wogdebug_side wogdebug_target wogdebug_mf wogdebug_damage wogdebug_hit",
            note: "счётчики скрипта WoG Debug: br — раундов (round = v997), bg0/bg1 — действий, action — BG:A последнего (1 заклинание героя, 2 движение, 3 защита, 6 атака, 7 выстрел, 8 ожидание, 12 без действия), mf — ударов, damage — урон последнего", icon: "h3:icm004.def#0|oe:battle_icon_Onesword"),
        new("Бой (BM)", "События боя: подписаться", "battleevents", note: "журнал всех событий боя Olden Era; затем «События боя: показать»", icon: "h3:icm003.def#0"),
        new("Бой (BM)", "События боя: показать", "battleevents show", icon: "h3:iam004.def#0"),

        new("Существа (MA)", "Копейщик: атака, защита, ОЗ", "erm !!MA:A0/?x1 D0/?x2 P0/?x3;", note: "x1 — атака, x2 — защита, x3 — здоровье", icon: Creature(0)),
        new("Существа (MA)", "Копейщик: атака +5", "erm !!MA:A0/d5 A0/?x1;", note: "x1 — атака после", icon: Skill(22)),
        new("Существа (MA)", "Копейщик: атака −5", "erm !!MA:A0/d-5 A0/?x1;", note: "x1 — атака после", icon: "h3:dlg_npc1.def#8"),

        new("Отладка", "WoG'ификация карты (UN:P5)", "erm !!UN:P5/?x1;",
            note: "x1 — опция 5: 2 — карта WoG'ифицирована, 0 — нет (ERA: без глобальных скриптов); настройка игрока — [WoG] Wogify в wog.oldenera.cfg: 0 никогда, 1–2 всегда, 3 спрашивать при новой карте",
            icon: "h3:wogcurse.def#9|h3:wogbttn.def#0"),
        new("Отладка", "Состояние игры", "state", icon: "h3:iam002.def#0|oe:Icon_PlayerStatus_KingdomStatistics"),
        new("Отладка", "Новый день (события дня)", "newday", icon: "h3:icm006.def#0"),
        new("Отладка", "Самопроверка WoG/ERA", "selftest", icon: "h3:wogbttn.def#0|h3:iOKAY.def#0"),
        new("Отладка", "Неподдержанное за сессию", "compat", icon: "h3:ILCK42.def#0"),
        new("Отладка", "Символы игры", "symbols", icon: "h3:dlg_npc1.def#5"),
        new("Отладка", "Справка по командам", "help", icon: Skill(8)),
    };
}
