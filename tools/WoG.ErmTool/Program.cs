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
//   compat                            print the receiver/command support table (Compatibility/ERM_Compatibility.md)
//   probe-symbols <BepInEx/interop>   list Olden Era types/members matching the symbols the adapter needs

if (args.Length == 0)
{
    Console.WriteLine("usage: parse <path> [--359] | run <dir> [--359] | compat | probe-symbols <interop dir>");
    return 1;
}

var dialect = args.Contains("--359") ? ErmDialect.Wog359Alpha : ErmDialect.Wog358;

switch (args[0])
{
    case "parse":
    {
        var files = Files(args[1]);
        int err = 0, warn = 0, secs = 0, lines = 0;
        foreach (var f in files)
        {
            var s = ErmParser.ParseText(Path.GetFileName(f), ErmParser.DecodeFile(File.ReadAllBytes(f)), dialect);
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
        foreach (var f in Files(args[1]).OrderBy(x => x, StringComparer.Ordinal)) host.AddScriptFile(f);
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
        var host = new WoGHost(new HeadlessGame(), new VisualResolver(new NoAssets()));
        Console.WriteLine(host.Erm!.Receivers.ToMarkdown());
        return 0;
    }
    case "probe-symbols":
        return ProbeSymbols(args[1]);
    default:
        Console.WriteLine("unknown command " + args[0]);
        return 1;
}

static string[] Files(string path) =>
    File.Exists(path) ? new[] { path } : Directory.GetFiles(path, "*.erm");

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
