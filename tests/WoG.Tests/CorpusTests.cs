using System;
using System.IO;
using System.Linq;
using WoG.Erm.Syntax;
using Xunit;
using Xunit.Abstractions;

namespace WoG.Tests;

/// <summary>
/// Conformance against the real WoG scripts. The scripts are not part of this repository; point
/// WOG_SCRIPTS_DIR at a folder with *.erm files (e.g. the 3.58f "Data\s" folder of a WoG install).
/// Without it the tests pass trivially and say so.
/// </summary>
public class CorpusTests
{
    readonly ITestOutputHelper output;
    public CorpusTests(ITestOutputHelper output) { this.output = output; }

    static string? Dir => Environment.GetEnvironmentVariable("WOG_SCRIPTS_DIR");

    [Fact]
    public void All_scripts_parse_without_errors()
    {
        if (string.IsNullOrEmpty(Dir) || !Directory.Exists(Dir)) { output.WriteLine("WOG_SCRIPTS_DIR not set — skipped"); return; }
        var dialect = Environment.GetEnvironmentVariable("WOG_DIALECT") == "359" ? ErmDialect.Wog359Alpha : ErmDialect.Wog358;
        int files = 0, sections = 0, lines = 0;
        foreach (var f in Directory.GetFiles(Dir!, "*.erm"))
        {
            var s = ErmParser.ParseText(Path.GetFileName(f), ErmParser.DecodeFile(File.ReadAllBytes(f)), dialect);
            if (!s.IsErm) continue;
            files++;
            sections += s.Sections.Count();
            lines += s.Sections.Sum(x => x.Lines.Count) + s.Items.Count(i => i.Kind == ErmItemKind.Instruction);
            var errors = s.Diagnostics.Where(d => d.Severity == ErmSeverity.Error).ToList();
            Assert.True(errors.Count == 0, $"{Path.GetFileName(f)}: {string.Join("; ", errors)}");
        }
        output.WriteLine($"{files} files, {sections} trigger sections, {lines} receiver lines parsed");
        Assert.True(files > 0);
    }

    [Fact]
    public void All_scripts_load_as_new_game_on_the_reference_engine()
    {
        if (string.IsNullOrEmpty(Dir) || !Directory.Exists(Dir)) { output.WriteLine("WOG_SCRIPTS_DIR not set — skipped"); return; }
        var t = new TestHost();
        foreach (var f in Directory.GetFiles(Dir!, "*.erm").OrderBy(x => x, StringComparer.Ordinal))
            t.Host.AddScriptFile(f);
        t.Host.StartNewGame();
        output.WriteLine($"runtime errors: {t.ErrorCount}");
        output.WriteLine(t.Host.Compat.ToMarkdown());
        Assert.True(t.Erm.Sections.Count > 0);
    }
}
