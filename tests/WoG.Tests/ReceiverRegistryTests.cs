using System.Linq;
using WoG.Erm.Receivers;
using WoG.Erm.Syntax;
using Xunit;

namespace WoG.Tests;

public class ReceiverRegistryTests
{
    static string[] DeclaredNotes(ErmDialect dialect) =>
        new TestHost(dialect).Erm.Receivers.All
            .SelectMany(r => r is UnsupportedReceiver u ? new[] { u.Reason } : r.Support.Values.Select(s => s.Note))
            .Where(n => n.Length > 0)
            .Distinct()
            .ToArray();

    // The Russian copies of the generated tables (Compatibility/ERM_Compatibility*.ru.md) must not fall back to English.
    [Theory]
    [InlineData(ErmDialect.Wog358)]
    [InlineData(ErmDialect.Wog359Alpha)]
    [InlineData(ErmDialect.Era)]
    public void Every_declared_note_has_a_Russian_translation(ErmDialect dialect)
    {
        var notes = DeclaredNotes(dialect);
        Assert.NotEmpty(notes);
        Assert.All(notes, n => Assert.True(ReceiverRegistry.RussianNotes.ContainsKey(n), "no Russian translation: " + n));
    }

    [Fact]
    public void Markdown_table_is_printed_in_English_or_Russian()
    {
        var reg = new TestHost(ErmDialect.Era).Erm.Receivers;
        string en = reg.ToMarkdown(), ru = reg.ToMarkdown("ru");
        Assert.StartsWith("| Receiver | Implemented | Commands and status |\n|---|---|---|\n", en);
        Assert.StartsWith("| Ресивер | Реализован | Команды и статус |\n|---|---|---|\n", ru);
        Assert.Contains("| `IP` | no | UNSUPPORTED — network ERM is out of scope (single-player only) |", en);
        Assert.Contains("| `IP` | нет | UNSUPPORTED — сетевой ERM вне рамок проекта (только одиночная игра) |", ru);
        Assert.Contains("| `MC` | yes | `S` FULLY SUPPORTED |", en);
        Assert.Contains("| `MC` | да | `S` FULLY SUPPORTED |", ru);
        Assert.Equal(en.Split('\n').Length, ru.Split('\n').Length);
    }
}
