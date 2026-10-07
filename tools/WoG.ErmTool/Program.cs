using System;
using System.IO;
using System.Linq;
using System.Reflection;
using WoG.Core.Visual;
using WoG.Erm.Runtime;
using WoG.Erm.Syntax;
using WoG.Headless;
using WoG.Host;

// WoG.ErmTool — command-line companion of the port.
//   parse   <dir|file> [--359]       parse ERM files, print diagnostics and statistics
//   run     <dir>      [--359]       load scripts as a new game on the headless engine; print the compatibility report
//   run --era <mod dir>...           same for ERA: mods highest priority first (Era load order, preprocessor, Lang)
//   compat [--era] [--lang ru]        print the whole generated page Compatibility/ERM_Compatibility[_ERA][.ru].md:
//                                     language switch, title, introduction and the receiver/command support table
//                                     (default language en; --lang ru prints the Russian copy)
//   probe-symbols <BepInEx/interop>   list Olden Era types/members matching the symbols the adapter needs
//   era-pp  <out dir> <mod dir>...   Era: collect scripts of the mods (highest priority first) in Era load order,
//                                     run the Era preprocessor, write the results, print diagnostics

if (args.Length == 0)
{
    Console.WriteLine("usage: parse <path> [--359] | run <dir> [--359] | compat [--era] [--lang ru] | probe-symbols <interop dir>");
    return 1;
}

var dialect = args.Contains("--era") ? ErmDialect.Era : args.Contains("--359") ? ErmDialect.Wog359Alpha : ErmDialect.Wog358;

switch (args[0])
{
    case "parse":
    {
        var files = Files(args[1]);
        int err = 0, warn = 0, secs = 0, lines = 0;
        foreach (var f in files)
        {
            string text = dialect == ErmDialect.Era ? WoG.Erm.Era.EraText.Decode(File.ReadAllBytes(f)) : ErmParser.DecodeFile(File.ReadAllBytes(f));
            var s = ErmParser.ParseText(Path.GetFileName(f), text, dialect);
            if (!s.IsErm) continue;
            secs += s.Sections.Count();
            lines += s.Sections.Sum(x => x.Lines.Count);
            foreach (var d in s.Diagnostics)
            {
                Console.WriteLine(d);
                if (d.Severity == ErmSeverity.Error) err++; else warn++;
            }
        }
        Console.WriteLine($"{files.Length} files, {secs} sections, {lines} receiver lines, {err} errors, {warn} warnings");
        return err == 0 ? 0 : 2;
    }
    case "run":
    {
        var game = new HeadlessGame();
        var host = new WoGHost(game, new VisualResolver(new NoAssets()), null, new ErmRuntimeOptions { Dialect = dialect });
        if (dialect == ErmDialect.Era) host.AddEraMods(args.Skip(1).Where(a => !a.StartsWith("--")));
        else foreach (var f in Files(args[1]).OrderBy(x => x, StringComparer.Ordinal)) host.AddScriptFile(f);
        host.Erm!.Log = Console.WriteLine;
        host.StartNewGame();
        for (int day = 1; day <= 7; day++)
        {
            game.AbsoluteDay = day;
            host.Erm.RunTimers(0, day);
        }
        Console.WriteLine();
        Console.WriteLine(host.Compat.ToMarkdown());
        return 0;
    }
    case "compat":
    {
        int langAt = Array.IndexOf(args, "--lang");
        string lang = langAt < 0 ? "en" : langAt + 1 < args.Length ? args[langAt + 1] : "";
        if (lang != "en" && lang != "ru")
        {
            Console.Error.WriteLine("compat: --lang must be en or ru");
            return 1;
        }
        var host = new WoGHost(new HeadlessGame(), new VisualResolver(new NoAssets()), null, new ErmRuntimeOptions { Dialect = dialect });
        Console.Write(CompatPage(dialect == ErmDialect.Era, lang, host.Erm!.Receivers.ToMarkdown(lang)));
        return 0;
    }
    case "probe-symbols":
        return ProbeSymbols(args[1]);
    case "era-pp":
    {
        string outDir = args[1];
        Directory.CreateDirectory(outDir);
        var mods = args.Skip(2).Where(a => !a.StartsWith("--")).ToList();
        var names = new WoG.Erm.Era.EraNames();
        names.ResetFunctions();
        var diags = new System.Collections.Generic.List<ErmDiagnostic>();
        var files = WoG.Erm.Era.EraScriptSet.Collect(mods);
        foreach (var f in files)
        {
            string text = WoG.Erm.Era.EraText.Decode(File.ReadAllBytes(f.Path));
            string pp = WoG.Erm.Era.EraPreprocessor.Process(f.Name, text, names, diags);
            string target = Path.Combine(outDir, f.Name.Replace('\\', '_'));
            File.WriteAllText(target, pp);
        }
        foreach (var d in diags) Console.WriteLine(d);
        Console.WriteLine($"{files.Count} scripts, {names.Functions.Count} function names (auto id now {names.FuncAutoId}), {names.Constants.Count} constants, {diags.Count} diagnostics");
        return diags.Count == 0 ? 0 : 2;
    }
    default:
        Console.WriteLine("unknown command " + args[0]);
        return 1;
}

static string[] Files(string path) =>
    File.Exists(path) ? new[] { path } : Directory.GetFiles(path, "*.erm");

// The whole generated page Compatibility/ERM_Compatibility[_ERA][.ru].md: language switch, title and introduction,
// then the receiver/command table. Output uses "\n" line endings on every platform.
static string CompatPage(bool era, string lang, string table)
{
    string name = era ? "ERM_Compatibility_ERA" : "ERM_Compatibility";
    string languageSwitch = lang == "ru" ? $"[English]({name}.md) | **Русский**" : $"**English** | [Русский]({name}.ru.md)";
    string intro = (era, lang) switch
    {
        (false, "en") => """
            # ERM Compatibility: Receivers and Commands

            **This file is generated** from the runtime's receiver registry:
            `dotnet run --project tools/WoG.ErmTool -- compat`. Do not edit the table by hand — change `Declare(...)` in
            `src/WoG.Erm/Receivers/*.cs`.

            How to read it:
            * "Implemented: yes" — the receiver is executed by the runtime; the status is given for each command letter.
              Letters not listed in the row produce a "wrong command" error, as in WoG.
            * "Implemented: no" — the receiver is recognized by the parser (scripts that use it load), but when executed
              the command is recorded in the compatibility report as UNSUPPORTED and execution of the line continues.
              Nothing is faked.
            * The status refers to **Olden Era**: for example, `MA` is fully implemented in the runtime, but on Olden Era
              it is PARTIALLY SUPPORTED because the engine's stat model is different.

            Which receivers WoG scripts actually need — see the usage column in
            `WoG_ReverseEngineering/03_ERM_Receivers.md`. A run of all 78 3.58f scripts as a new game on the reference
            engine (`WoG.ErmTool run`) currently runs into: `HT:P/W`, `OW:T`, `UN:A/B/R/V/X`, `IF:D/F` and ERT strings
            (`z > 1000`) — these are the next tasks for extending the runtime.
            """,
        (false, _) => """
            # Совместимость ERM: ресиверы и команды

            **Этот файл генерируется** из реестра ресиверов рантайма:
            `dotnet run --project tools/WoG.ErmTool -- compat --lang ru`. Не правьте таблицу вручную — меняйте `Declare(...)` в
            `src/WoG.Erm/Receivers/*.cs`.

            Как читать:
            * «Реализован: да» — ресивер исполняется рантаймом; для каждой буквы команды указан статус. Буквы, не
              перечисленные в строке, выдают ошибку «wrong command», как в WoG.
            * «Реализован: нет» — ресивер распознаётся парсером (скрипты с ним грузятся), но при выполнении команда
              записывается в отчёт совместимости как UNSUPPORTED, выполнение строки продолжается. Ничего не подделывается.
            * Статус относится к **Olden Era**: например, `MA` реализован в рантайме полностью, но на Olden Era он
              PARTIALLY SUPPORTED, потому что модель статов движка другая.

            Какие ресиверы реально нужны скриптам WoG — см. столбец использований в
            `WoG_ReverseEngineering/03_ERM_Receivers.ru.md`. Прогон всех 78 скриптов 3.58f как новой игры на эталонном
            движке (`WoG.ErmTool run`) сейчас упирается в: `HT:P/W`, `OW:T`, `UN:A/B/R/V/X`, `IF:D/F` и строки ERT
            (`z > 1000`) — это ближайшие задачи по расширению.
            """,
        (true, "en") => """
            # ERM Compatibility in ERA: Receivers and Commands

            **This file is generated** from the runtime's receiver registry in ERA mode:
            `dotnet run --project tools/WoG.ErmTool -- compat --era`. Do not edit the table by hand — change `Declare(...)`
            in `src/WoG.Erm/Receivers/*.cs`. The table for classic WoG 3.58 is `ERM_Compatibility.md`.

            Differences of ERA mode from WoG: `VR`, `FU`, `DO` are replaced with the versions rewritten by ERA
            (`EraReceivers.cs`), `SN` is added (`SnReceiver`, Era API functions — `EraApi.cs`), `if/el/en/re/br/co` are
            executed by the interpreter itself (`EraProcess.cs`). The remaining receivers are WoG's, but parameters,
            `Apply` and strings work by ERA rules (`ErmCall`, `EraValues.cs`).

            How to read it — the same as `ERM_Compatibility.md`: "no" = scripts load, the command is recorded in the report
            as UNSUPPORTED, execution continues; nothing is faked. The status refers to **Olden Era**.

            A run of the whole ERA project (183 scripts) as a new game on the reference engine — 0 errors. Unsupported at
            startup: `UN:C` (H3 memory), `SN:E` (H3 code), `FU:D` (network), `UN:A/R/X/N/U/V/J` (map and objects),
            `SN:L/B`, `IF:G`.
            """,
        (true, _) => """
            # Совместимость ERM в ERA: ресиверы и команды

            **Этот файл генерируется** из реестра ресиверов рантайма в режиме ERA:
            `dotnet run --project tools/WoG.ErmTool -- compat --era --lang ru`. Не правьте таблицу вручную — меняйте `Declare(...)`
            в `src/WoG.Erm/Receivers/*.cs`. Таблица для классического WoG 3.58 — `ERM_Compatibility.ru.md`.

            Отличия режима ERA от WoG: `VR`, `FU`, `DO` заменены переписанными ERA версиями (`EraReceivers.cs`), добавлен
            `SN` (`SnReceiver`, функции API Era — `EraApi.cs`), `if/el/en/re/br/co` исполняет сам интерпретатор
            (`EraProcess.cs`). Остальные ресиверы — WoG, но параметры, `Apply` и строки работают по правилам ERA
            (`ErmCall`, `EraValues.cs`).

            Как читать — так же, как `ERM_Compatibility.ru.md`: «нет» = скрипты грузятся, команда записывается в отчёт как
            UNSUPPORTED, выполнение продолжается; ничего не подделывается. Статус относится к **Olden Era**.

            Прогон всего проекта ERA (183 скрипта) как новой игры на эталонном движке — 0 ошибок. Неподдержанное при
            старте: `UN:C` (память H3), `SN:E` (код H3), `FU:D` (сеть), `UN:A/R/X/N/U/V/J` (карта и объекты),
            `SN:L/B`, `IF:G`.
            """,
    };
    return languageSwitch + "\n\n" + intro.Replace("\r\n", "\n").TrimEnd('\n') + "\n\n" + table;
}

// Scans the IL2CPP interop assemblies BepInEx generates (BepInEx/interop/*.dll) for the shapes listed in
// OldenEra_ReverseEngineering/07_InGame_RE_Plan.md. Uses metadata-only loading: no game code runs.
static int ProbeSymbols(string dir)
{
    var resolver = new PathAssemblyResolver(Directory.GetFiles(dir, "*.dll")
        .Concat(Directory.GetFiles(Path.GetDirectoryName(typeof(object).Assembly.Location)!, "*.dll")));
    using var mlc = new MetadataLoadContext(resolver);
    var hex = Path.Combine(dir, "Hex.dll");
    if (!File.Exists(hex)) { Console.WriteLine("Hex.dll not found in " + dir); return 2; }
    var asm = mlc.LoadFromAssemblyPath(hex);
    string[] keywords = { "Hero", "Battle", "Combat", "Unit", "Squad", "Buff", "Save", "Load", "Scenario", "Quest", "Counter", "Turn", "Day", "Map", "City", "Town", "Artifact", "Item", "Spell", "Magic", "Dialog" };
    Type[] types;
    try { types = asm.GetTypes(); }
    catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray()!; }
    foreach (var t in types.OrderBy(t => t.FullName))
    {
        string n = t.FullName ?? t.Name;
        if (!keywords.Any(k => n.Contains(k, StringComparison.OrdinalIgnoreCase))) continue;
        Console.WriteLine(n);
        try
        {
            foreach (var m in t.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                Console.WriteLine("    " + m.MemberType + " " + m);
        }
        catch (Exception ex) { Console.WriteLine("    (members unavailable: " + ex.GetType().Name + ")"); }
    }
    return 0;
}
