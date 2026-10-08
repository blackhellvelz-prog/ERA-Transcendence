using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WoG.Core.Adapters;
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
    /// <summary>Writes a member of a game object found by a member path (reverse engineering only).</summary>
    string Set(string args) => "set is not supported by this engine";
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
        "  hero [number]                a hero through the WoG layer: stats, skills, spells, artifacts by position\n" +
        "  peek <path> [max]            read a game object: root.heroes.list[0].node, Type.staticMember.member...\n" +
        "  invoke <path> <method> [arg...]  call a game method; args: numbers, \"text\", true/false, null, @<path>\n" +
        "  set <path>.<member> <value>  write a member of a game object (same values as invoke)\n" +
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
                "hero" => Hero(rest),
                "peek" => engine?.Peek(rest) ?? "no engine",
                "invoke" => engine?.Invoke(rest) ?? "no engine",
                "set" => engine?.Set(rest) ?? "no engine",
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
        // x1..x16 the code left: a snippet returns values this way
        var xs = r.Returns.Select((v, i) => (v, i)).Where(p => p.v != 0).Select(p => $"x{p.i + 1}={p.v}").ToList();
        if (xs.Count > 0) sb.Append("  ").Append(string.Join(" ", xs)).Append('\n');
        foreach (var l in r.Log) sb.Append("  log: ").Append(l).Append('\n');
        return sb.ToString();
    }

    static readonly string[] SlotNames =
    {
        "голова", "плечи", "шея", "правая рука", "левая рука", "торс", "кольцо П", "кольцо Л", "ноги",
        "разное 1", "разное 2", "разное 3", "разное 4", "баллиста", "тележка", "палатка", "катапульта", "книга", "разное 5",
    };

    /// <summary>hero [number] — a hero as the WoG layer sees it (by default the active hero of the current player).</summary>
    string Hero(string args)
    {
        var g = host.Game;
        var heroes = g.Heroes;
        int h;
        if (args.Length > 0) h = int.Parse(args.Trim());
        else
        {
            var active = g.Players.GetActiveHero(g.Players.CurrentPlayer);
            if (!active.IsOk) return "no active hero: " + active;
            h = active.Value;
        }
        var h3 = host.H3;
        string Name<T>(IReadOnlyList<T> table, int i, Func<T, string> name) => i >= 0 && i < table.Count ? name(table[i]) : "#" + i;
        string Val<T>(AdapterResult<T> r) => r.IsOk ? r.Value?.ToString() ?? "-" : "?";
        var sb = new StringBuilder();
        sb.Append($"hero {h} \"{Val(heroes.GetName(h))}\", owner {Val(heroes.Get(h, HeroStat.Owner))}, at {Val(heroes.GetPosition(h))}\n");
        sb.Append("  ").Append(string.Join(", ", new[] { HeroStat.Attack, HeroStat.Defence, HeroStat.Power, HeroStat.Knowledge,
            HeroStat.Level, HeroStat.Experience, HeroStat.Mana, HeroStat.Movement }.Select(s => $"{s} {Val(heroes.Get(h, s))}"))).Append('\n');
        var skills = new List<string>();
        for (int s = 0; s < WoGLimits.SecondarySkillCount; s++)
            if (heroes.GetSecondarySkill(h, s) is { IsOk: true, Value: > 0 } r)
                skills.Add($"{s} {Name(h3.SecondarySkills, s, x => x[0])} {r.Value}");
        sb.Append("  skills: ").Append(skills.Count == 0 ? "-" : string.Join(", ", skills)).Append('\n');
        var spells = new List<string>();
        for (int s = 0; s < WoGLimits.SpellCount; s++)
            if (heroes.HasSpell(h, s) is { IsOk: true, Value: true })
                spells.Add($"{s} {Name(h3.Spells, s, x => x)}");
        sb.Append("  spells: ").Append(spells.Count == 0 ? "-" : string.Join(", ", spells)).Append('\n');
        var arts = heroes.GetArtifacts(h);
        if (!arts.IsOk) sb.Append("  artifacts: ").Append(arts).Append('\n');
        else
        {
            var worn = new List<string>();
            var pack = new List<string>();
            for (int p = 0; p < arts.Value.Length; p++)
            {
                int a = arts.Value[p];
                if (a < 0) continue;
                string name = a >= ArtifactSlots.ScrollBase ? "свиток: " + Name(h3.Spells, a - ArtifactSlots.ScrollBase, x => x)
                    : Name(h3.Artifacts, a, x => x.Name);
                if (p < ArtifactSlots.Worn) worn.Add($"{SlotNames[p]}: {a} {name}");
                else pack.Add($"{a} {name}");
            }
            sb.Append("  worn: ").Append(worn.Count == 0 ? "-" : string.Join(", ", worn)).Append('\n');
            sb.Append("  backpack: ").Append(pack.Count == 0 ? "-" : string.Join(", ", pack)).Append('\n');
        }
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
