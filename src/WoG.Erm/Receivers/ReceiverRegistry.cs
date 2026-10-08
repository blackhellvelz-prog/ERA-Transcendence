using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WoG.Core.Compat;
using WoG.Erm.Runtime;
using WoG.Erm.Syntax;

namespace WoG.Erm.Receivers;

/// <summary>Declared support for one command letter of a receiver.</summary>
public sealed record CommandSupport(CompatLevel Level, string Note);

public interface IErmReceiver
{
    string Id { get; }
    /// <summary>Support per command letter; letters not listed are reported as unsupported.</summary>
    IReadOnlyDictionary<char, CommandSupport> Support { get; }
    void Execute(ErmCall call);
}

/// <summary>Base class: dispatches on the command letter and enforces the declared support table.</summary>
public abstract class ErmReceiverBase : IErmReceiver
{
    protected ErmReceiverBase(string id) { Id = id; }
    public string Id { get; }
    readonly Dictionary<char, CommandSupport> support = new();
    public IReadOnlyDictionary<char, CommandSupport> Support => support;

    protected void Declare(string letters, CompatLevel level, string note = "")
    {
        foreach (var c in letters) support[c] = new CommandSupport(level, note);
    }

    public void Execute(ErmCall call)
    {
        if (support.TryGetValue(call.Letter, out var s) && s.Level == CompatLevel.Unsupported)
            throw new ErmUnsupportedException(s.Note);
        Run(call);
    }

    protected abstract void Run(ErmCall c);
}

/// <summary>A WoG receiver that is recognised but not (yet) mapped to the engine.</summary>
public sealed class UnsupportedReceiver : IErmReceiver
{
    public UnsupportedReceiver(string id, string reason) { Id = id; Reason = reason; }
    public string Id { get; }
    public string Reason { get; }
    public IReadOnlyDictionary<char, CommandSupport> Support { get; } = new Dictionary<char, CommandSupport>();
    public void Execute(ErmCall call) => throw new ErmUnsupportedException(Reason);
}

public sealed class ReceiverRegistry
{
    readonly Dictionary<string, IErmReceiver> map = new(StringComparer.Ordinal);

    public void Register(IErmReceiver r) => map[r.Id] = r;
    public IErmReceiver? Get(string id) => map.TryGetValue(id, out var r) ? r : null;
    public IEnumerable<IErmReceiver> All => map.Values;

    public static ReceiverRegistry CreateDefault(ErmRuntime rt)
    {
        var reg = new ReceiverRegistry();
        foreach (var id in ErmParser.KnownReceivers358.Concat(ErmParser.KnownReceivers359))
            reg.Register(new UnsupportedReceiver(id, "receiver is not mapped to the target engine yet"));
        reg.Register(new VrReceiver());
        reg.Register(new FuReceiver());
        reg.Register(new DoReceiver());
        reg.Register(new McReceiver());
        reg.Register(new IfReceiver());
        reg.Register(new UnReceiver());
        reg.Register(new ObReceiver());
        reg.Register(new TrReceiver());
        reg.Register(new PoReceiver());
        reg.Register(new CaReceiver());
        reg.Register(new MnReceiver());
        reg.Register(new BaReceiver());
        reg.Register(new BmReceiver());
        reg.Register(new BgReceiver());
        reg.Register(new MfReceiver());
        reg.Register(new TmReceiver());
        reg.Register(new HeReceiver());
        reg.Register(new OwReceiver());
        reg.Register(new MaReceiver());
        reg.Register(new CoReceiver());
        reg.Register(new ExReceiver());
        reg.Register(new NoOpReceiver("la"));
        reg.Register(new NoOpReceiver("go"));
        reg.Register(new NoOpReceiver("if"));
        reg.Register(new NoOpReceiver("el"));
        reg.Register(new NoOpReceiver("en"));
        reg.Register(new UnsupportedReceiver("IP", "network ERM is out of scope (single-player only)"));
        return reg;
    }

    /// <summary>
    /// Era receivers: WoG's (with Era's ErmCall semantics), Era's rewrites of VR/FU/DO, SN, the flow-control
    /// commands handled by the interpreter, and the receivers of Era plugins.
    /// </summary>
    public static ReceiverRegistry CreateEra(ErmRuntime rt)
    {
        var reg = new ReceiverRegistry();
        foreach (var id in ErmParser.KnownReceivers358.Concat(ErmParser.KnownReceiversEra))
            reg.Register(new UnsupportedReceiver(id, "receiver is not mapped to the target engine yet"));
        reg.Register(new EraVrReceiver());
        reg.Register(new EraFuReceiver());
        reg.Register(new EraDoReceiver());
        reg.Register(new SnReceiver());
        reg.Register(new McReceiver());
        reg.Register(new IfReceiver());
        reg.Register(new UnReceiver());
        reg.Register(new ObReceiver());
        reg.Register(new TrReceiver());
        reg.Register(new PoReceiver());
        reg.Register(new CaReceiver());
        reg.Register(new MnReceiver());
        reg.Register(new BaReceiver());
        reg.Register(new BmReceiver());
        reg.Register(new BgReceiver());
        reg.Register(new MfReceiver());
        reg.Register(new TmReceiver());
        reg.Register(new HeReceiver());
        reg.Register(new OwReceiver());
        reg.Register(new MaReceiver());
        reg.Register(new CoReceiver());
        reg.Register(new ExReceiver());
        foreach (var id in new[] { "if", "el", "en", "re", "br", "co" }) reg.Register(new NoOpReceiver(id));
        reg.Register(new UnsupportedReceiver("IP", "network ERM is out of scope (single-player only)"));
        reg.Register(new UnsupportedReceiver("MP", "MP — H3 music (mp3): Olden Era has its own music"));
        reg.Register(new UnsupportedReceiver("RD", "RD — H3 creature recruitment window (Dwellings.pas): needs a UI adapter"));
        reg.Register(new UnsupportedReceiver("SS", "SS — receiver of the secondary skills plugin (closed-source ERA DLL)"));
        reg.Register(new UnsupportedReceiver("PA", "PA — receiver of the \"receiver pa.era\" plugin (closed-source ERA DLL)"));
        reg.Register(new UnsupportedReceiver("QU", "QU — receiver of the \"receiver qu.era\" plugin (closed-source ERA DLL)"));
        return reg;
    }

    /// <summary>
    /// Russian texts of the declared notes and reasons, keyed by the English text in the code. Used only for the
    /// Russian copy of the generated tables (Compatibility/ERM_Compatibility*.ru.md); a note without an entry is
    /// printed in English. Every note/reason of <see cref="CreateDefault"/> and <see cref="CreateEra"/> must be here.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> RussianNotes = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        // ReceiverRegistry
        ["receiver is not mapped to the target engine yet"] = "ресивер ещё не отображён на целевой движок",
        ["network ERM is out of scope (single-player only)"] = "сетевой ERM вне рамок проекта (только одиночная игра)",
        ["MP — H3 music (mp3): Olden Era has its own music"] = "MP — музыка H3 (mp3): у Olden Era своя музыка",
        ["RD — H3 creature recruitment window (Dwellings.pas): needs a UI adapter"] = "RD — окно найма существ H3 (Dwellings.pas): нужен UI-адаптер",
        ["SS — receiver of the secondary skills plugin (closed-source ERA DLL)"] = "SS — ресивер плагина вторичных навыков (закрытая DLL ERA)",
        ["PA — receiver of the \"receiver pa.era\" plugin (closed-source ERA DLL)"] = "PA — ресивер плагина «receiver pa.era» (закрытая DLL ERA)",
        ["QU — receiver of the \"receiver qu.era\" plugin (closed-source ERA DLL)"] = "QU — ресивер плагина «receiver qu.era» (закрытая DLL ERA)",
        // CoreReceivers, EraReceivers
        ["FU:D — call on the remote player: a single-player game has none, so nothing runs (as in WoG offline)"] = "FU:D — вызов у удалённого игрока: в одиночной игре его нет, поэтому ничего не выполняется (как в WoG без сети)",
        ["SN:H — object/monster hints: needs an Olden Era UI adapter"] = "SN:H — подсказки объектов/монстров: нужен UI-адаптер Olden Era",
        ["SN:O — object entrance tile: needs a map adapter"] = "SN:O — клетка входа объекта: нужен адаптер карты",
        ["SN:P — H3 sound playback: Olden Era sounds are different"] = "SN:P — проигрывание звука H3: звуки Olden Era другие",
        ["SN:S — sound name in !?SN: the sound trigger is not ported"] = "SN:S — имя звука в !?SN: звуковой триггер не перенесён",
        ["SN:R — H3 resource redirection (lod/def): Olden Era resources are different"] = "SN:R — подмена ресурсов H3 (lod/def): ресурсы Olden Era другие",
        ["SN:F — Era API functions: those used by the ERA Project scripts are ported (see EraApi); DLL/Win32 functions are not"] = "SN:F — функции API Era: перенесены те, что используют скрипты проекта ERA (см. EraApi); функции DLL/Win32 — нет",
        ["SN:E — calling a function by address in the H3 exe: different engine"] = "SN:E — вызов функции по адресу в exe H3: другой движок",
        ["SN:L/A/B — DLL loading, addresses and H3 process memory: different engine"] = "SN:L/A/B — загрузка DLL, адреса и память процесса H3: другой движок",
        // GameReceivers
        ["text messages and yes/no questions; variants with pictures need custom UI"] = "текстовые сообщения и вопросы да/нет; варианты с картинками требуют своего UI",
        ["special WoG dialogs (pictures, sphinx, checkboxes, multiple choice) need a custom UI layer"] = "особые диалоги WoG (картинки, сфинкс, флажки, множественный выбор) требуют своего UI-слоя",
        ["UN:C writes to H3 memory addresses — impossible on a different engine"] = "UN:C пишет по адресам памяти H3 — на другом движке невозможно",
        ["UN:A — artifact types come from the ERA installation's artraits.txt and are kept per game; Olden Era items are not linked to them yet, so changes do not affect the game's items, and the map ban does not affect map generation"] =
            "UN:A — типы артефактов берутся из artraits.txt установки ERA и хранятся в каждой игре; с предметами Olden Era они пока не связаны, поэтому изменения не влияют на предметы игры, а запрет на карте не влияет на генерацию карты",
        ["UN:V — WoG/ERM versions of the dialect (ERA 400/3931, WoG 358/281); a single-player game: no network, no cheat tracking (0)"] =
            "UN:V — версии WoG/ERM диалекта (ERA 400/3931, WoG 358/281); одиночная игра: сети нет, читы не отслеживаются (0)",
        ["UN:X — map size; Olden Era maps have no underground (levels 0)"] =
            "UN:X — размер карты; у карт Olden Era нет подземелья (уровней 0)",
        ["UN:U — Olden Era map objects with an H3 type (Compatibility/id-maps/object.json; Olden Era-only objects have types from 1000); a monster squad of several unit types counts as its first unit"] =
            "UN:U — объекты карты Olden Era с типом H3 (Compatibility/id-maps/object.json; объекты только Olden Era имеют типы от 1000); отряд монстров из нескольких типов юнитов считается по первому юниту",
        ["OB:T/U — the type and subtype of the object on a square (Format OB via id-maps/object.json); they cannot be changed"] =
            "OB:T/U — тип и подтип объекта на клетке (Format OB через id-maps/object.json); изменить их нельзя",
        ["OB:C — the control word is H3's object setup data: different engine"] =
            "OB:C — управляющее слово — это данные настройки объекта H3: другой движок",
        ["OB:D/E/R/S/M/H/B — disabling objects, auto-answers and hints need the object visit hook (not verified yet)"] =
            "OB:D/E/R/S/M/H/B — запрет объектов, автоответы и подсказки требуют хука посещения объекта (ещё не проверен)",
        ["TR:T/P/E — terrain (Olden Era biomes as the H3 terrain of the matching town), road, blocked (red) and entrance (yellow) squares; read only; rivers are 0"] =
            "TR:T/P/E — почва (биомы Olden Era как почва H3 соответствующего города), дорога, занятые (красные) и входные (жёлтые) клетки; только чтение; реки 0",
        ["TR:G — H3 terrain overlays (magic plains, cursed ground…) have no Olden Era equivalent mapped"] =
            "TR:G — наложения почвы H3 (магические равнины, проклятая земля…) не имеют отображённого аналога в Olden Era",
        ["TR:V — square visibility (fog of war) is not mapped yet"] =
            "TR:V — видимость клетки (туман войны) пока не отображена",
        ["UN:N — names of artifacts, spells, creatures and secondary skills from the ERA installation's text tables; N5/N6 ini values (written under BepInEx/config/WoG/era-root); N2 building names are not read yet"] =
            "UN:N — названия артефактов, заклинаний, существ и вторичных навыков из текстовых таблиц установки ERA; значения ini N5/N6 (пишутся в BepInEx/config/WoG/era-root); названия построек N2 пока не читаются",
        ["UN:R — R1-R4 redraws: Olden Era redraws its screens itself; R5-R7 (mouse pointer shape, delay) are cosmetic and do nothing"] =
            "UN:R — перерисовка R1-R4: Olden Era сама обновляет свои экраны; R5-R7 (вид указателя мыши, задержка) косметические и ничего не делают",
        ["BA:H/O/P/Q/S/E/A — the battle as the adapter saw it start: attacker = the active hero of the player whose turn it is, defender = the monster squad next to it (hero-vs-hero and town battles are not told apart yet); read only"] =
            "BA:H/O/P/Q/S/E/A — бой таким, каким адаптер увидел его начало: атакующий — активный герой игрока, чей ход, защитник — отряд монстров рядом с ним (бои герой-против-героя и за города пока не различаются); только чтение",
        ["BA:M — the armies of the battle sides are not mapped yet"] =
            "BA:M — армии сторон боя пока не отображены",
        ["BA:D/B — cancelling a battle and its background are not mapped yet"] =
            "BA:D/B — отмена боя и его фон пока не отображены",
        ["MA — creatures with an Olden Era unit change the game's unit type (attack, defence, hit points, speed, damage, cost, unit value; level, town and upgrade read only); creatures without one use the ERA installation's zcrtrait.txt and change nothing in the game; changes are saved with the game"] =
            "MA — у существ с юнитом Olden Era меняется тип юнита в игре (атака, защита, здоровье, скорость, урон, стоимость, ценность; уровень, город и улучшение только чтение); существа без него берутся из zcrtrait.txt установки ERA и в игре ничего не меняют; изменения сохраняются вместе с игрой",
        ["MA:N/G/R/H/V/B/X — shots, growth, adventure-map counts, casts and flags exist only for creatures without an Olden Era unit (from zcrtrait.txt); Olden Era units have no such stats"] =
            "MA:N/G/R/H/V/B/X — выстрелы, прирост, численность на карте, заклинания и флаги есть только у существ без юнита Olden Era (из zcrtrait.txt); у юнитов Olden Era таких характеристик нет",
        ["UN:J — J0 spell bans (kept; Olden Era's guilds do not use them yet), J1 the WoG level limit (kept, 0 = none; a limit is not enforced yet) and the experience of a level (the engine's table), J2 difficulty (Olden Era's AI difficulty), J8/J9 files and folders (the write folder first, then the ERA installation), J10 variable log, J11; J3-J7, J12, J13 are not mapped yet"] =
            "UN:J — J0 запрет заклинаний (хранится; гильдии Olden Era его пока не используют), J1 предел уровня WoG (хранится, 0 — нет; предел пока не соблюдается) и опыт уровня (таблица движка), J2 сложность (сложность ИИ Olden Era), J8/J9 файлы и папки (сначала папка записи, затем установка ERA), J10 лог переменных, J11; J3-J7, J12, J13 пока не отображены",
        ["UN map/object/global commands are not mapped yet"] = "команды UN для карты/объектов/глобальные ещё не отображены",
        ["values go through the adapter; OE primary stats differ (see the matrix)"] = "значения идут через адаптер; первичные статы OE отличаются (см. матрицу)",
        ["S: secondary skills; Olden Era maps H3 skills by effect (id-maps/skill.json, 14 of 28) and a level 2 or 3 given by a script brings the game's own sub-skill choice; a skill it does not have reads as not learned and cannot be learned, lowering a learned skill and changing the hero-screen order are not mapped, Olden Era-only skills are invisible to scripts"] =
            "S: вторичные навыки; в Olden Era навыки H3 сопоставлены по эффекту (id-maps/skill.json, 14 из 28), а уровень 2 или 3, выданный скриптом, приносит родной выбор поднавыка игры; навык, которого там нет, читается как не изученный и не может быть изучен, понижение изученного навыка и смена порядка на экране героя не отображены, навыки только Olden Era скриптам не видны",
        ["M: spells; Olden Era maps H3 spells by effect (id-maps/spell.json, 37 of 70), a spell it does not have reads as not known and cannot be learned, a specialist knows a spell as its masterful variant, the spells of a hero that is not on the map cannot be changed"] =
            "M: заклинания; в Olden Era заклинания H3 сопоставлены по эффекту (id-maps/spell.json, 37 из 70), заклинание, которого там нет, читается как неизвестное и не может быть изучено, герой-специалист знает заклинание в мастерском варианте, заклинания героя не на карте менять нельзя",
        ["A: artifacts by position (0..18 worn, 19..82 backpack); Olden Era maps H3 artifacts by effect or name (id-maps/artifact.json, 61 of 171), its items fit only their own slot type, has no war machines (the spellbook is always there) and no gaps in the backpack; A5 slot locks are not mapped"] =
            "A: артефакты по позициям (0..18 надеты, 19..82 рюкзак); в Olden Era артефакты H3 сопоставлены по эффекту или названию (id-maps/artifact.json, 61 из 171), её предметы встают только в свой тип слота, боевых машин нет (книга заклинаний есть всегда), рюкзак без пропусков; A5 (замки слотов) не отображены",
        ["B: name (B0), biography (B1, B3) and class (B2); Olden Era shows a hero's name and biography through its localization, so a new one is the text of that hero's key for the game, kept with the WoG state; the class is the closest H3 class of the hero's faction and might/magic kind and cannot be changed"] =
            "B: имя (B0), биография (B1, B3) и класс (B2); Olden Era показывает имя и биографию героя через локализацию, поэтому новое значение — текст ключа этого героя на время игры, хранится в состоянии WoG; класс — ближайший класс H3 по фракции и типу героя (сила/магия), изменить нельзя",
        ["X: the specialty as H3's record; an Olden Era specialty reads as the closest H3 one (a creature, a spell, a resource, or a secondary skill whose effect it has), others have none; it cannot be changed"] =
            "X: специализация как запись H3; специализация Olden Era читается как ближайшая H3 (существо, заклинание, ресурс или вторичный навык с тем же эффектом), у остальных аналога нет; изменить её нельзя",
        ["H: the army a hero type is hired with (Olden Era's start squad of the type, kept with the WoG state); creatures without an Olden Era unit cannot be set"] =
            "H: армия, с которой нанимается герой этого типа (стартовый отряд типа в Olden Era, хранится в состоянии WoG); существ без юнита Olden Era задать нельзя",
        ["OW:H/O/T/V/W — the player's heroes and hero list, team, tavern heroes and towns (the player's towns in town-number order); reordering the lists, changing teams and the tavern are not mapped"] =
            "OW:H/O/T/V/W — герои игрока и его список героев, команда, герои таверны и города (города игрока по порядку номеров); перестановка списков, смена команд и таверны не отображены",
        ["OW:N — the player's towns by list slot; the selected town and reordering the list are not mapped"] =
            "OW:N — города игрока по слотам списка; выбранный город и перестановка списка не отображены",
        ["OW:D/K/S — days without a town, keymaster tents and adventure-map spells have no Olden Era equivalent mapped"] =
            "OW:D/K/S — дни без города, палатки ключника и заклинания на карте приключений не имеют сопоставленного аналога в Olden Era",
        ["MN:O — the owner of a mine; setting it is the game's change of owner (flag, income)"] =
            "MN:O — владелец шахты; установка — родная смена владельца игрой (флаг, доход)",
        ["MN:R — the resource a mine produces (id-maps/object.json); it cannot be changed"] =
            "MN:R — ресурс, который даёт шахта (id-maps/object.json); изменить нельзя",
        ["MN:M — the guards a mine keeps itself; an Olden Era mine has none (it is guarded by squads on the map), so they read as empty and cannot be set"] =
            "MN:M — собственная охрана шахты; у шахты Olden Era её нет (её охраняют отряды на карте), поэтому читается пустой и задать её нельзя",
        ["MN:R — changing what a mine produces: an Olden Era mine is its own object type"] =
            "MN:R — смена ресурса шахты: шахта Olden Era — отдельный тип объекта",
        ["BM:N/L/A/D/H/S/U1/U2 — count, hit points lost by the top creature, attack, defence, hit points, speed and damage of a battle stack: Olden Era's own unit in the battle; a changed stat is the unit's battle modifier, so the game's recalculations keep it"] =
            "BM:N/L/A/D/H/S/U1/U2 — количество, потерянные ОЗ верхнего существа, атака, защита, ОЗ, скорость и урон отряда в бою: собственный юнит боя Olden Era; изменённая характеристика — модификатор юнита в бою, поэтому пересчёты игры её сохраняют",
        ["BM:T/B/I/O/P/F/E/R/J/U3 — the type, start count, side, army slot, position, flags, casts, retaliations, active spells and shots of a battle stack: read where Olden Era has them, changing them is not mapped"] =
            "BM:T/B/I/O/P/F/E/R/J/U3 — тип, количество в начале, сторона, слот армии, позиция, флаги, заклинания, ответные удары, активные заклинания и выстрелы отряда в бою: чтение там, где в Olden Era они есть; изменение не отображено",
        ["BM:G/C/K/M/Q/V/U4/U5 — spells on a stack, casting, damage, magic obstacles, animations and spell or clone settings are not mapped yet"] =
            "BM:G/C/K/M/Q/V/U4/U5 — заклинания на отряде, их применение, урон, магические препятствия, анимации и настройки заклинания или клона ещё не отображены",
        ["BG:A/N/Q/H/E — the action in !?BG (the hero's spell, walk, defend, attack, shoot, wait, a monster's spell, no action; Olden Era's turn events), the acting stack, its side, its hero and the targeted stack; read only"] =
            "BG:A/N/Q/H/E — действие в !?BG (заклинание героя, движение, защита, атака, выстрел, ожидание, заклинание монстра, без действия; события хода Olden Era), действующий отряд, его сторона, его герой и отряд-цель; только чтение",
        ["BG:D/S/X — the destination position, the spell and the second position of an action are not mapped yet"] =
            "BG:D/S/X — позиция назначения, заклинание и вторая позиция действия ещё не отображены",
        ["BG — changing a stack's action, its target or spell: Olden Era reports the action as it starts"] =
            "BG — смена действия отряда, его цели или заклинания: Olden Era сообщает о действии, когда оно уже начинается",
        ["BG:A — this kind of Olden Era action has no H3 action type mapped"] =
            "BG:A — у этого вида действия Olden Era нет сопоставленного типа действия H3",
        ["MF:D/N/W — the damage, the stack taking it, the kind of attacker (0); Olden Era reports the damage after it is dealt"] =
            "MF:D/N/W — урон, отряд, получающий его, вид атакующего (0); Olden Era сообщает об уроне после того, как он нанесён",
        ["MF:E/F — enabled (1) and the corrected damage read as dealt; changing them is not possible when the damage is reported after it is dealt"] =
            "MF:E/F — «разрешён» (1) и исправленный урон читаются как нанесённые; изменить их нельзя, пока об уроне сообщается после нанесения",
        ["MF:E/F — Olden Era reports the damage after it is dealt, so it cannot be changed or cancelled yet"] =
            "MF:E/F — Olden Era сообщает об уроне после того, как он нанесён, поэтому изменить или отменить его пока нельзя",
        ["PO — WoG data of a map square, kept in the WoG state and saved with it"] =
            "PO — данные WoG для клетки карты, хранятся в состоянии WoG и сохраняются вместе с ним",
        ["CA:B — buildings by H3 number: dwellings, mage guild, fort/citadel/castle, village/town/city hall, tavern, marketplace, resource silo and grail are Olden Era's buildings (a higher level counts its lower ones as built); the other numbers read as not built and cannot be built; B1/B6 build through the game's construction, free and without using the day's one; B4/B5 allow or forbid; B3…/1 (bonus taken) reads as built; B2 is not possible: Olden Era has no demolition"] =
            "CA:B — здания по номерам H3: жилища, гильдия магов, форт/цитадель/замок, управа/ратуша/муниципалитет, таверна, рынок, склад ресурсов и Грааль — здания Olden Era (более высокий уровень засчитывает нижние как построенные); остальные номера читаются как не построенные и не строятся; B1/B6 строят через строительство игры, бесплатно и не тратя постройку дня; B4/B5 разрешают или запрещают; B3…/1 (бонус получен) читается как построено; B2 невозможен: сноса в Olden Era нет",
        ["CA:M — M1 creatures to hire (Olden Era keeps one number per dwelling: the row of the building that stands is used), M2 the town's own garrison (creature ids via IdMap), M4 weekly growth (read only, as in WoG); M3 (summoning portal) is not mapped"] =
            "CA:M — M1 существа для найма (в Olden Era одно число на жилище: используется строка того здания, что стоит), M2 собственный гарнизон города (id существ через IdMap), M4 недельный прирост (только чтение, как в WoG); M3 (портал призыва) не отображён",
        ["CA:O/G/N/R/S — owner, mage guild level and spells, name (Olden Era's own, localized), built today, daily gold income; see the matrix for what can be changed"] =
            "CA:O/G/N/R/S — владелец, уровень и заклинания гильдии магов, название (своё у Olden Era, локализованное), строился ли сегодня, дневной доход золота; что можно менять — см. матрицу",
        ["CA:H/P/T/U — garrison and visiting hero, position, town type (Olden Era factions as the closest H3 town), town number: read; moving heroes into a town, moving a town, changing its type or number are not mapped"] =
            "CA:H/P/T/U — герой в гарнизоне и гость, позиция, тип города (фракции Olden Era как ближайший город H3), номер города: чтение; перемещение героев в город, перенос города, смена его типа или номера не отображены",
        ["CA:I — the ruined looks of a town (I-1, I1..I3) are not mapped; I0 (the look of its buildings) is what Olden Era shows"] =
            "CA:I — разрушенный вид города (I-1, I1..I3) не отображён; I0 (вид по постройкам) — то, что Olden Era и так показывает",
        ["CA:D — the recruitment window with several creatures of the Battery plugin (closed-source ERA DLL)"] =
            "CA:D — окно найма нескольких существ из плагина Battery (закрытая DLL ERA)",
        ["ids via IdMap; display-slot forms are not supported"] = "id через IdMap; формы со слотами отображения не поддерживаются",
        ["not mapped yet"] = "ещё не отображено",
        ["HE:Z (ERA) — the address of H3's hero structure, for UN:C and SN:E: H3 memory, a different engine; Era Erm Framework's artifact functions that use it run natively"] = "HE:Z (ERA) — адрес структуры героя H3 для UN:C и SN:E: память H3, другой движок; функции артефактов Era Erm Framework, которые его используют, работают нативно",
        ["resource ids via IdMap; OW:R resource 7 is WoG's mithril, kept by the port per player and saved with the game (Olden Era has no mithril)"] = "id ресурсов через IdMap; ресурс 7 в OW:R — мифрил WoG, порт хранит его для каждого игрока и сохраняет с игрой (в Olden Era мифрила нет)",
        ["commanders are an emulated entity in Olden Era"] = "командиры — эмулируемая сущность в Olden Era",
        ["experience is external WoG state applied to OE stacks"] = "опыт — внешнее состояние WoG, применяемое к стекам OE",
        ["stack merging is not implemented yet"] = "объединение стеков ещё не реализовано",
    };

    /// <summary>
    /// Markdown table of receiver/command support (feeds Compatibility/ERM_Compatibility*.md).
    /// <paramref name="lang"/>: "en" (default) or "ru" (headers, yes/no and notes in Russian; status words stay English).
    /// </summary>
    public string ToMarkdown(string lang = "en")
    {
        bool ru = lang switch
        {
            "en" => false,
            "ru" => true,
            _ => throw new ArgumentException($"unknown language '{lang}' (expected en or ru)", nameof(lang)),
        };
        string Note(string note) => ru && RussianNotes.TryGetValue(note, out var t) ? t : note;
        string yes = ru ? "да" : "yes", no = ru ? "нет" : "no";
        var sb = new StringBuilder();
        sb.Append(ru ? "| Ресивер | Реализован | Команды и статус |\n" : "| Receiver | Implemented | Commands and status |\n");
        sb.Append("|---|---|---|\n");
        foreach (var r in map.Values.OrderBy(r => r.Id, StringComparer.Ordinal))
        {
            if (r is NoOpReceiver) continue;
            if (r is UnsupportedReceiver u)
            {
                sb.Append($"| `{r.Id}` | {no} | UNSUPPORTED — {Note(u.Reason)} |\n");
                continue;
            }
            var cmds = string.Join("; ", r.Support.OrderBy(k => k.Key)
                .GroupBy(k => (k.Value.Level, k.Value.Note))
                .Select(g => $"`{string.Concat(g.Select(x => x.Key)).Replace("|", "\\|")}` {Level(g.Key.Level)}{(g.Key.Note.Length > 0 ? " (" + Note(g.Key.Note) + ")" : "")}"));
            sb.Append($"| `{r.Id}` | {yes} | {cmds} |\n");
        }
        return sb.ToString();
    }

    static string Level(CompatLevel l) => l switch
    {
        CompatLevel.FullySupported => "FULLY SUPPORTED",
        CompatLevel.PartiallySupported => "PARTIALLY SUPPORTED",
        CompatLevel.Emulated => "EMULATED",
        CompatLevel.Workaround => "WORKAROUND",
        _ => "UNSUPPORTED",
    };
}

/// <summary>Control-flow ids handled by the interpreter itself.</summary>
public sealed class NoOpReceiver : IErmReceiver
{
    public NoOpReceiver(string id) { Id = id; }
    public string Id { get; }
    public IReadOnlyDictionary<char, CommandSupport> Support { get; } = new Dictionary<char, CommandSupport>();
    public void Execute(ErmCall call) { }
}
