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
        ["UN map/object/global commands are not mapped yet"] = "команды UN для карты/объектов/глобальные ещё не отображены",
        ["values go through the adapter; OE primary stats differ (see the matrix)"] = "значения идут через адаптер; первичные статы OE отличаются (см. матрицу)",
        ["ids via IdMap; display-slot forms are not supported"] = "id через IdMap; формы со слотами отображения не поддерживаются",
        ["not mapped yet"] = "ещё не отображено",
        ["resource ids via IdMap"] = "id ресурсов через IdMap",
        ["the engine's stat model differs (initiative/speed, no shots)"] = "модель статов движка отличается (initiative/speed, нет выстрелов)",
        // ModuleReceivers
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
