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
            reg.Register(new UnsupportedReceiver(id, "receiver not mapped to the target engine yet"));
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

    /// <summary>Markdown table of receiver/command support (feeds Compatibility/ERM_Compatibility.md).</summary>
    public string ToMarkdown()
    {
        var sb = new StringBuilder();
        sb.Append("| Ресивер | Реализован | Команды и статус |\n|---|---|---|\n");
        foreach (var r in map.Values.OrderBy(r => r.Id, StringComparer.Ordinal))
        {
            if (r is NoOpReceiver) continue;
            if (r is UnsupportedReceiver u)
            {
                sb.Append($"| `{r.Id}` | нет | UNSUPPORTED — {u.Reason} |\n");
                continue;
            }
            var cmds = string.Join("; ", r.Support.OrderBy(k => k.Key)
                .GroupBy(k => (k.Value.Level, k.Value.Note))
                .Select(g => $"`{string.Concat(g.Select(x => x.Key))}` {Level(g.Key.Level)}{(g.Key.Note.Length > 0 ? " (" + g.Key.Note + ")" : "")}"));
            sb.Append($"| `{r.Id}` | да | {cmds} |\n");
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
