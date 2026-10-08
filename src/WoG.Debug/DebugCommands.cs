using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WoG.Core.Model;
using WoG.Host;

namespace WoG.Debug;

/// <summary>The engine-specific half of WoG Debug (implemented by the Olden Era plugin).</summary>
public interface IDebugEngine
{
    string Name { get; }
    /// <summary>Raw engine state: players, resources, heroes, day — read directly, without ERM.</summary>
    string DumpState();
    /// <summary>Runs the start-of-day path the game hook runs (ERA OnEveryDay and timers for every player).</summary>
    string NewDay();
    /// <summary>Game symbols: bound/resolved/verified, and which hooks fired.</summary>
    string Symbols();
    /// <summary>Reads any game object by a member path, for reverse engineering in a running game.</summary>
    string Peek(string args) => "peek is not supported by this engine";
    /// <summary>Calls a game method on an object found by a member path (reverse engineering only).</summary>
    string Invoke(string args) => "invoke is not supported by this engine";
}

/// <summary>
/// WoG Debug commands. One command per call; "erm" takes the rest of the text (several lines) as ERM code.
/// Output is plain text; the self-test returns Markdown.
/// </summary>
public sealed class DebugCommands
{
    readonly WoGHost host;
    readonly IDebugEngine? engine;

    public DebugCommands(WoGHost host, IDebugEngine? engine)
    {
        this.host = host;
        this.engine = engine;
        Console = new ErmConsole(host);
    }

    public ErmConsole Console { get; }

    readonly Dictionary<string, (Func<string, string> run, string help)> extra = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Adds a command (an engine-side module such as the in-game window registers its own).</summary>
    public void Add(string name, Func<string, string> run, string help) => extra[name] = (run, help);

    string FullHelp() => Help + string.Concat(extra.Select(kv => $"  {kv.Key,-28} {kv.Value.help}\n"));

    /// <summary>The last self-test report (Markdown), for writing next to the log.</summary>
    public string? LastReport { get; private set; }

    public const string Help =
        "WoG Debug commands:\n" +
        "  help                         this text\n" +
        "  state                        clock, players and resources through the adapter + raw engine state\n" +
        "  erm <code>                   run ERM code (ERA syntax in ERA mode); several lines allowed\n" +
        "  event <name|number> [player] raise an ERM/ERA event (OnEveryDay, OnGameEnter, 30300, ...)\n" +
        "  newday                       run the start-of-day path for every player\n" +
        "  symbols                      game symbols and hook calls\n" +
        "  selftest [interactive] [id-prefix]  run the WoG/ERA self-test (Markdown report)\n" +
        "  compat                       commands the engine could not perform so far\n" +
        "  vars v|z <from> <to>         ERM variables; vars i <name> for i^name^\n" +
        "  peek <path> [max]            read a game object: root.heroes.list[0].node, Type.staticMember.member...\n" +
        "  invoke <path> <method> [arg...]  call a game method; args: numbers, \"text\", true/false, null, @<path>\n" +
        "                               (a type alone: its static members; a list: its first max items)\n";

    public string Execute(string text)
    {
        text = text.Trim();
        if (text.Length == 0) return "";
        int sp = text.IndexOfAny(new[] { ' ', '\n', '\r', '\t' });
        string cmd = (sp < 0 ? text : text[..sp]).ToLowerInvariant();
        string rest = sp < 0 ? "" : text[(sp + 1)..].Trim();
        try
        {
            return cmd switch
            {
                "help" => FullHelp(),
                "state" => State(),
                "erm" => Erm(rest),
                "event" => Event(rest),
                "newday" => engine?.NewDay() ?? "no engine",
                "symbols" => engine?.Symbols() ?? "no engine",
                "selftest" => RunSelfTest(rest),
                "compat" => host.Compat.ToMarkdown(),
                "vars" => Vars(rest),
                "peek" => engine?.Peek(rest) ?? "no engine",
                "invoke" => engine?.Invoke(rest) ?? "no engine",
                _ when extra.TryGetValue(cmd, out var x) => x.run(rest),
                _ => "unknown command '" + cmd + "'\n" + FullHelp(),
            };
        }
        catch (Exception ex)
        {
            return "error: " + ex;
        }
    }

    string State()
    {
        var sb = new StringBuilder();
        var g = host.Game;
        sb.Append($"engine: {g.EngineName}\n");
        sb.Append($"clock: day {g.Clock.AbsoluteDay} (day of week {g.Clock.DayOfWeek}, week {g.Clock.Week}, month {g.Clock.Month})\n");
        sb.Append($"current player: {g.Players.CurrentPlayer}\n");
        for (int p = 0; p < WoGLimits.PlayerCount; p++)
        {
            var alive = g.Players.IsAlive(p);
            if (alive.IsOk && !alive.Value) continue;
            var res = Enumerable.Range(0, WoGLimits.ResourceCount).Select(r => g.Players.GetResource(p, r))
                .Select(r => r.IsOk ? r.Value.ToString() : "—");
            sb.Append($"player {p}: alive {alive}, human {g.Players.IsHuman(p)}, local {g.Players.IsLocal(p)}, resources [{string.Join(", ", res)}]\n");
        }
        if (engine != null) sb.Append("\n--- ").Append(engine.Name).Append(" ---\n").Append(engine.DumpState());
        return sb.ToString();
    }

    string Erm(string code)
    {
        if (code.Length == 0) return "usage: erm <code>";
        // A single line typed without ';' gets one, as in the ERM console of Era's debug mode.
        if (!code.Contains('\n') && !code.TrimEnd().EndsWith(";")) code += ";";
        var r = Console.Run(code);
        var sb = new StringBuilder(r.Summary()).Append('\n');
        foreach (var l in r.Log) sb.Append("  log: ").Append(l).Append('\n');
        return sb.ToString();
    }

    string Event(string args)
    {
        var parts = args.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "usage: event <name|number> [player]";
        int player = parts.Length > 1 ? int.Parse(parts[1]) : host.Game.Players.CurrentPlayer;
        var r = Console.Raise(parts[0], player);
        return r.Summary() + "\n" + string.Join("\n", r.Log.Select(l => "  log: " + l));
    }

    string RunSelfTest(string args)
    {
        var words = args.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        bool interactive = words.Remove("interactive");
        string? filter = words.FirstOrDefault();
        var results = SelfTest.Run(host, Console, filter, interactive);
        LastReport = SelfTest.ToMarkdown(host, results, host.Game.EngineName);
        return LastReport;
    }

    string Vars(string args)
    {
        var p = args.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (p.Length == 0) return "usage: vars v|z <from> <to> | vars i <name>";
        var v = host.State.Erm;
        var sb = new StringBuilder();
        switch (p[0])
        {
            case "i":
                foreach (var name in p.Skip(1))
                    sb.Append($"i^{name}^ = {(host.State.Era.Assoc.TryGetValue(name, out var a) ? a.Int.ToString() : "(none)")}\n");
                break;
            case "v":
            case "z":
            {
                int from = p.Length > 1 ? int.Parse(p[1]) : 1, to = p.Length > 2 ? int.Parse(p[2]) : from;
                for (int i = from; i <= to; i++)
                    sb.Append(p[0] == "v" ? $"v{i} = {v.V[i - 1]}\n" : $"z{i} = \"{v.Z[i - 1]}\"\n");
                break;
            }
            default:
                return "usage: vars v|z <from> <to> | vars i <name>";
        }
        return sb.ToString();
    }
}
