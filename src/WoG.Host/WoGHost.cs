using System;
using System.Collections.Generic;
using System.IO;
using WoG.Commanders;
using WoG.Core.Adapters;
using WoG.Core.Compat;
using WoG.Core.Events;
using WoG.Core.Model;
using WoG.Core.Random;
using WoG.Core.Save;
using WoG.Core.Services;
using WoG.Core.State;
using WoG.Core.Visual;
using WoG.CreatureExperience;
using WoG.Erm.Runtime;
using WoG.Erm.Syntax;

namespace WoG.Host;

/// <summary>Which optional modules are active (modularity requirement).</summary>
public sealed class WoGModules
{
    public bool Commanders { get; set; } = true;
    public bool StackExperience { get; set; } = true;
    public bool Erm { get; set; } = true;
}

/// <summary>
/// Composition root. Owns the state and the module instances and translates engine-neutral
/// <see cref="WoGEvent"/>s into ERM events and module hooks, in WoG's order.
/// </summary>
public sealed class WoGHost : IWoGServices
{
    public WoGGameState State { get; private set; }
    public IGameAdapter Game { get; }
    public IWoGRandom Random { get; }
    public WoGEventBus Events { get; } = new();
    public CompatibilityReport Compat { get; } = new();
    public IVisualResolver Visuals { get; }
    public WoG.Core.H3Data.H3Tables H3 { get; private set; } = new();
    public ICommanderService? Commanders => commanders;
    public IStackExperienceService? StackExperience => stackExp;

    public ErmRuntime? Erm { get; private set; }
    public WoGModules Modules { get; }
    public ErmRuntimeOptions ErmOptions { get; }

    CommanderService? commanders;
    StackExperienceService? stackExp;
    readonly List<ErmScript> scripts = new();

    /// <summary>ERA: mod folders (highest priority first) whose Data\s scripts and Lang files are used.</summary>
    readonly List<string> eraMods = new();
    string eraLanguage = "en";
    public IReadOnlyList<WoG.Erm.Era.EraScriptFile> EraScripts { get; private set; } = Array.Empty<WoG.Erm.Era.EraScriptFile>();
    bool IsEra => ErmOptions.Dialect == ErmDialect.Era;

    public WoGHost(IGameAdapter game, IVisualResolver visuals, WoGModules? modules = null,
        ErmRuntimeOptions? ermOptions = null, IWoGRandom? random = null, WoGGameState? state = null)
    {
        Game = game;
        Visuals = visuals;
        Modules = modules ?? new WoGModules();
        ErmOptions = ermOptions ?? new ErmRuntimeOptions();
        Random = random ?? new MsvcRandom(1);
        State = state ?? new WoGGameState();
        Build();
        Events.Subscribe(OnEvent);
    }

    void Build()
    {
        commanders = Modules.Commanders
            ? new CommanderService(State, Random, hero => IsAutomatic(hero), (hero, gold) => GrantGold(hero, gold))
            : null;
        stackExp = Modules.StackExperience ? new StackExperienceService(State, Game) : null;
        var oldIni = Erm?.Ini;
        Erm = Modules.Erm ? new ErmRuntime(this, ErmOptions) : null;
        if (Erm != null)
        {
            Erm.Log = ermLog;
            if (oldIni != null) Erm.Ini = oldIni; // the ini cache belongs to the process, as in Era
            else if (EraGameFolder != null) ConfigureIni();
        }
    }

    Action<string>? ermLog;

    /// <summary>
    /// ERA: the folder Era would run in (the ERA installation, read only) and the folder where files that scripts
    /// write go instead (ini files). Relative paths of ini functions resolve against them.
    /// </summary>
    public void SetEraFolders(string? gameFolder, string writeFolder)
    {
        EraGameFolder = gameFolder;
        EraWriteFolder = writeFolder;
        ConfigureIni();
        LoadH3Tables();
    }

    /// <summary>The H3/WoG text tables from the ERA installation and the active mods (Era's VFS order).</summary>
    public void LoadH3Tables()
    {
        var vfs = new WoG.Core.H3Data.EraVfs(EraGameFolder, eraMods);
        H3 = vfs.IsEmpty ? new WoG.Core.H3Data.H3Tables() : WoG.Core.H3Data.H3Tables.Load(vfs);
    }


    public string? EraGameFolder { get; private set; }
    public string? EraWriteFolder { get; private set; }

    void ConfigureIni()
    {
        if (Erm == null) return;
        var ini = new WoG.Erm.Era.EraIni(EraWriteFolder ?? WoG.Erm.Era.EraIni.DefaultWriteRoot);
        if (EraGameFolder != null) ini.ReadRoots.Add(EraGameFolder);
        Erm.Ini = ini;
    }

    /// <summary>Log of the ERM runtime; kept when the runtime is rebuilt (new game, loaded game).</summary>
    public Action<string>? ErmLog
    {
        get => ermLog;
        set { ermLog = value; if (Erm != null) Erm.Log = value; }
    }

    bool IsAutomatic(int hero)
    {
        int owner = Game.Heroes.Get(hero, HeroStat.Owner) is { IsOk: true } o ? o.Value : -1;
        if (owner < 0) return true;
        bool human = Game.Players.IsHuman(owner) is { IsOk: true, Value: true };
        bool local = Game.Players.IsLocal(owner) is { IsOk: true, Value: true };
        return !human || !local;
    }

    void GrantGold(int hero, int gold)
    {
        int owner = Game.Heroes.Get(hero, HeroStat.Owner) is { IsOk: true } o ? o.Value : -1;
        if (owner < 0) return;
        if (Game.Players.GetResource(owner, WoGLimits.Gold) is { IsOk: true } g)
        {
            var r = Game.Players.SetResource(owner, WoGLimits.Gold, g.Value + gold);
            if (!r.IsOk) Compat.Unsupported("commanders", "class gold bonus", r.Reason ?? "");
        }
    }

    // ------------------------------------------------------------------------------------------
    // Scripts
    // ------------------------------------------------------------------------------------------

    public ErmScript AddScript(string name, string text)
    {
        var s = ErmParser.ParseText(name, text, ErmOptions.Dialect);
        scripts.Add(s);
        return s;
    }

    public void AddScriptFile(string path) => AddScript(Path.GetFileName(path), ErmParser.DecodeFile(File.ReadAllBytes(path)));

    /// <summary>
    /// ERA: use the scripts and translations of these mods (highest priority first, as Era's virtual file
    /// system sees them). Scripts are preprocessed when a game starts or is loaded.
    /// </summary>
    public void AddEraMods(IEnumerable<string> modRoots, string language = "en")
    {
        if (!IsEra) throw new InvalidOperationException("AddEraMods needs ErmRuntimeOptions.Dialect = Era");
        eraMods.AddRange(modRoots);
        eraLanguage = language;
        LoadH3Tables();
    }

    /// <summary>ERA: preprocess (shared function/constant names) and parse every script in Era's load order.</summary>
    void PrepareEraScripts(bool newGame)
    {
        if (Erm == null) return;
        var names = Erm.EraNames;
        if (newGame)
        {
            State.Era.ResetMemory();
            names.ResetFunctions();
        }
        names.ResetConstants();
        State.Era.Ert.Clear();
        scripts.Clear();
        EraScripts = WoG.Erm.Era.EraScriptSet.Collect(eraMods);
        foreach (var f in EraScripts)
        {
            string text = WoG.Erm.Era.EraText.Decode(File.ReadAllBytes(f.Path));
            var diags = new List<ErmDiagnostic>();
            string pp = WoG.Erm.Era.EraPreprocessor.Process(f.Name, text, names, diags);
            var script = ErmParser.ParseText(f.Name, pp, ErmDialect.Era);
            script.Diagnostics.InsertRange(0, diags);
            scripts.Add(script);
            LoadErt(Path.ChangeExtension(f.Path, ".ert"));
        }
        Erm.Lang = new EraLang();
        Erm.Lang.LoadMods(eraMods, eraLanguage);
    }

    /// <summary>ERT text table next to a script: header row, then "index TAB text ..." rows (CRLF-separated).</summary>
    void LoadErt(string path)
    {
        if (!File.Exists(path)) return;
        string text = WoG.Erm.Era.EraText.Decode(File.ReadAllBytes(path));
        string[] rows = text.Contains("\r\n") ? text.Split("\r\n") : text.Split('\n');
        for (int r = 1; r < rows.Length; r++)
        {
            var cells = rows[r].Split('\t');
            if (cells.Length < 2) continue;
            if (!int.TryParse(cells[0].Trim(), System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out int idx) || idx < 1) continue;
            if (State.Era.Ert.ContainsKey(idx))
            {
                Compat.Unsupported("erm", "ERT", $"Duplicate ERM string index {idx} ({Path.GetFileName(path)})");
                continue;
            }
            State.Era.Ert[idx] = cells[1];
        }
    }

    /// <summary>New game: load scripts (running instructions), then fire !?PI (post-instruction).</summary>
    public void StartNewGame()
    {
        if (State.InstructionsDone)
        {
            // Another game already ran in this host (a new map in the same game process): start from a clean
            // WoG state and a fresh runtime, keeping the id tables — otherwise every script section would be
            // registered twice and ERA function numbers (reset to 95000) would collide with the old ones.
            State = new WoGGameState { Ids = State.Ids };
            Build();
        }
        if (Erm == null) return;
        if (IsEra) PrepareEraScripts(newGame: true);
        foreach (var s in scripts) Erm.Load(s, newGame: true);
        State.InstructionsDone = true;
        var ctx = new ErmEventContext { Player = Game.Players.CurrentPlayer };
        Erm.Raise(30370, ctx);
        if (IsEra) Erm.Raise(WoG.Erm.Era.EraEvents.GameEnter, ctx);
    }

    // ------------------------------------------------------------------------------------------
    // Save / load (05_Save_System.md)
    // ------------------------------------------------------------------------------------------

    public void SaveTo(string path, string identity)
    {
        var ctx = new ErmEventContext { Player = Game.Players.CurrentPlayer };
        Erm?.Raise(30361, ctx); // !?GM1 before saving
        if (IsEra) Erm?.Raise(WoG.Erm.Era.EraEvents.SavegameWrite, ctx);
        WoGSaveSerializer.WriteFile(path, State, identity);
        if (IsEra) Erm?.Raise(WoG.Erm.Era.EraEvents.AfterSaveGame, ctx);
    }

    /// <summary>Loaded game: restore state, re-parse scripts without instructions, fire !?GM0.</summary>
    public void LoadFrom(string path, string identity)
    {
        var ctx = new ErmEventContext { Player = Game.Players.CurrentPlayer };
        if (IsEra && Erm != null && State.InstructionsDone) Erm.Raise(WoG.Erm.Era.EraEvents.GameLeave, ctx);
        State = WoGSaveSerializer.ReadFile(path, identity);
        State.Era.NormalizeAfterLoad();
        Build();
        if (Erm == null) return;
        if (IsEra)
        {
            var ert = new Dictionary<int, string>(State.Era.Ert);
            PrepareEraScripts(newGame: false);
            foreach (var kv in ert) State.Era.Ert[kv.Key] = kv.Value;
        }
        foreach (var s in scripts) Erm.Load(s, newGame: false);
        if (IsEra) Erm.Raise(WoG.Erm.Era.EraEvents.SavegameRead, ctx);
        Erm.Raise(30360, ctx);
        if (IsEra) Erm.Raise(WoG.Erm.Era.EraEvents.GameEnter, ctx);
    }

    // ------------------------------------------------------------------------------------------
    // Engine events → WoG
    // ------------------------------------------------------------------------------------------

    void OnEvent(WoGEvent e)
    {
        var ctx = new ErmEventContext { Hero = e.Hero, Player = e.Player, Position = e.Position.IsNone ? new MapPos(0, 0, 0) : e.Position };
        switch (e.Kind)
        {
            case WoGEventKind.PlayerDayStarted:
                Erm?.RunTimers(e.Player, Game.Clock.AbsoluteDay);
                break;
            case WoGEventKind.HeroVisitPre:
            case WoGEventKind.HeroVisitPost:
                if (Erm != null)
                {
                    bool post = e.Kind == WoGEventKind.HeroVisitPost;
                    int pf = post ? ErmEventIds.PostFlag : 0;
                    // ERM2Object order: position, type/subtype, type.
                    Erm.Raise(ErmEventIds.ObjectPos | e.Position.Pack() | pf, ctx);
                    if (e.ObjectType >= 0)
                    {
                        Erm.Raise(ErmEventIds.ObjectType | ((e.ObjectType << 12) + (e.ObjectSubType + 1)) | pf, ctx);
                        Erm.Raise(ErmEventIds.ObjectType | (e.ObjectType << 12) | pf, ctx);
                    }
                    if (ctx.CancelNative) e.CancelNative = true;
                }
                break;
            case WoGEventKind.HeroMeetsHero:
                Erm?.Raise(30100 + e.Arg, ctx);
                break;
            case WoGEventKind.HeroStep:
                Erm?.Raise(30400, ctx);
                Erm?.Raise(30401 + e.Hero, ctx);
                break;
            case WoGEventKind.HeroLevelUp:
                Erm?.Raise(30600, ctx);
                Erm?.Raise(30601 + e.Hero, ctx);
                break;
            case WoGEventKind.BattleStart:
                Erm?.Raise(30300, ctx);
                Erm?.Raise(30352, ctx);
                break;
            case WoGEventKind.BattleFieldSetup:
                Erm?.Raise(30800, ctx);
                break;
            case WoGEventKind.BattleRound:
                State.Erm.V[996] = e.Arg; // v997 = round
                Erm?.Raise(30302, ctx);
                break;
            case WoGEventKind.BattleActionPre:
                Erm?.Raise(30303, ctx);
                break;
            case WoGEventKind.BattleActionPost:
                Erm?.Raise(30304, ctx);
                break;
            case WoGEventKind.BattleEnd:
                Erm?.Raise(30301, ctx);
                Erm?.Raise(30353, ctx);
                break;
            case WoGEventKind.ArtifactUnequip:
                Erm?.Raise(30315, ctx);
                break;
            case WoGEventKind.ArtifactEquip:
                Erm?.Raise(30316, ctx);
                break;
            case WoGEventKind.TownHallEnter:
                Erm?.Raise(30324, ctx);
                break;
            case WoGEventKind.TownHallLeave:
                Erm?.Raise(30325, ctx);
                break;
            case WoGEventKind.CommanderDialog:
                Erm?.Raise(30340 + e.Arg, ctx);
                break;
            case WoGEventKind.GameLoaded:
                Erm?.Raise(30360, ctx);
                break;
            case WoGEventKind.GameSaving:
                Erm?.Raise(30361, ctx);
                break;
        }
    }

    /// <summary>
    /// After a battle won by <paramref name="hero"/>: commander experience follows the hero's total
    /// experience (NPC::AddExp, with the class-5 battle gold), stack experience distributes the gain.
    /// </summary>
    public void AfterBattleExperience(int hero, int oldHeroExp, int newHeroExp)
    {
        if (commanders != null)
        {
            var c = commanders.Get(hero);
            if (c.Used == 1 && c.Dead == 0)
            {
                c.LastExpoInBattle = 1;
                commanders.OnHeroExperience(hero, newHeroExp, auto: false);
            }
        }
        stackExp?.OnHeroBattleExperience(hero, oldHeroExp, newHeroExp);
    }
}
