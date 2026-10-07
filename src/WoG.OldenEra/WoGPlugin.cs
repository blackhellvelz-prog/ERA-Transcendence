using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using WoG.Core.Events;
using WoG.Core.Visual;
using WoG.Erm.Runtime;
using WoG.Erm.Syntax;
using WoG.Host;

namespace WoG.OldenEra;

/// <summary>
/// Entry point inside Olden Era. Wires WoG Core to the game:
///  1. loads BepInEx/config/wog_symbols.json and resolves the game symbols (gme-mod pattern);
///  2. builds the WoG host over <see cref="OldenEraGameAdapter"/> (ERA or classic WoG dialect);
///  3. loads the ERA mods (or classic WoG scripts) named in the config;
///  4. patches the verified game methods (day start, object interaction, battle, save/load) with Harmony so
///     the engine raises WoG events;
///  5. with [Debug] Enabled, runs the WoG Debug command bridge and method tracing.
/// Every unverified piece stays off and is reported in BepInEx/LogOutput.log.
/// </summary>
[BepInPlugin(Guid, "In the Wake of Gods for Olden Era", Version)]
public sealed class WoGPlugin : BasePlugin
{
    public const string Guid = "wog.oldenera";
    public const string Version = "0.2.0";

    internal static WoGPlugin? Instance;
    internal static WoGHost? Host;
    internal static OldenEraGameAdapter? Adapter;
    internal static OldenEraSymbols? Symbols;
    internal static ManualLogSource? L;
    internal static DebugBridge? DebugBridge;

    public override void Load()
    {
        Instance = this;
        L = Log;
        string cfgDir = Path.Combine(Paths.ConfigPath, "WoG");
        Directory.CreateDirectory(cfgDir);

        var dialect = Config.Bind("ERM", "Dialect", "Era", "Era: ERA 2.x scripts (ERM 2.0); Wog: classic WoG 3.58 scripts from WoG/scripts");
        var timeLimit = Config.Bind("ERM", "TimeLimitMs", 10000,
            "An ERM event running longer than this is abandoned with an error (0 = no limit); keeps an endless script loop from freezing the game");
        var modsRoot = Config.Bind("ERA", "ModsRoot", Path.Combine(cfgDir, "mods"),
            "Folder with ERA mods (each mod has Data/s). Can be the Mods folder of an ERA installation.");
        var modList = Config.Bind("ERA", "ModList", "WoG Debug",
            "Mods to load, comma-separated, lowest priority first (the order of ERA's Mods/list.txt). Empty: use ModsRoot/list.txt.");
        var language = Config.Bind("ERA", "Language", "ru", "Language of ERA translations (Lang/<language>)");
        var debugEnabled = Config.Bind("Debug", "Enabled", false, "WoG Debug: command bridge (BepInEx/config/WoG/debug/in) and self-test");
        var allowUnverified = Config.Bind("Debug", "AllowUnverifiedSymbols", false,
            "Debug only: use resolved but not yet verified game symbols, so the self-test can verify them");
        var trace = Config.Bind("Debug", "TraceMethods", "",
            "Debug only: Type.Method list (Hex assembly, ';'-separated) whose calls are logged, e.g. ebe.OnStartDay;ebe.OnStartWeek");

        string symbolsPath = Path.Combine(Paths.ConfigPath, "wog_symbols.json");
        if (!File.Exists(symbolsPath))
        {
            OldenEraSymbols.WriteTemplate(symbolsPath);
            Log.LogWarning($"WoG: wrote symbol template {symbolsPath}; fill it after the in-game RE step (07_InGame_RE_Plan.md)");
        }
        var symbols = OldenEraSymbols.Load(symbolsPath);
        symbols.AllowUnverified = debugEnabled.Value && allowUnverified.Value;
        symbols.Resolve(m => Log.LogInfo(m));
        Symbols = symbols;

        bool era = string.Equals(dialect.Value, "Era", StringComparison.OrdinalIgnoreCase);
        WoGHost? host = null;
        var adapter = new OldenEraGameAdapter(symbols, () => host!.State.Ids);
        host = new WoGHost(adapter, new VisualResolver(new UnityAssetProbe()), new WoGModules(),
            new ErmRuntimeOptions { Dialect = era ? ErmDialect.Era : ErmDialect.Wog358, TimeLimitMs = timeLimit.Value });
        Host = host;
        Adapter = adapter;
        host.ErmLog = m => Log.LogInfo("[ERM] " + m);

        LoadIdMaps(cfgDir, host);
        if (era)
        {
            var mods = EraModFolders(modsRoot.Value, modList.Value);
            // An ERA installation's Mods folder: Era runs in its parent; files scripts write go to config/WoG/era-root.
            string? eraFolder = string.Equals(Path.GetFileName(modsRoot.Value.TrimEnd('\\', '/')), "Mods", StringComparison.OrdinalIgnoreCase)
                ? Path.GetDirectoryName(modsRoot.Value.TrimEnd('\\', '/')) : null;
            host.SetEraFolders(eraFolder, Path.Combine(cfgDir, "era-root"));
            host.AddEraMods(mods, language.Value);
            Log.LogInfo($"WoG: ERA mods (highest priority first): {string.Join(", ", mods.Select(Path.GetFileName))}");
            Log.LogInfo(host.H3.Sources.Count == 0
                ? "WoG: no H3 text tables found (ERA installation not found?) — UN:A and other table commands are unsupported"
                : $"WoG: H3 tables: {string.Join("; ", host.H3.Sources.Select(kv => kv.Key + " ← " + kv.Value))}; {host.H3.Artifacts.Count} artifacts");
        }
        else
        {
            string scripts = Path.Combine(cfgDir, "scripts");
            if (Directory.Exists(scripts))
                foreach (var f in Directory.GetFiles(scripts, "*.erm").OrderBy(x => x, StringComparer.Ordinal))
                    host.AddScriptFile(f);
        }

        var harmony = new Harmony(Guid);
        HookPostfix(harmony, symbols, "turn.start", nameof(Hooks.TurnStartPostfix));
        HookPrefix(harmony, symbols, "object.interact", nameof(Hooks.InteractPrefix));
        HookPostfix(harmony, symbols, "object.interact", nameof(Hooks.InteractPostfix));
        HookPostfix(harmony, symbols, "battle.start", nameof(Hooks.BattleStartPostfix));
        HookPostfix(harmony, symbols, "battle.end", nameof(Hooks.BattleEndPostfix));
        HookPostfix(harmony, symbols, "save.write", nameof(Hooks.SavePostfix));
        HookPostfix(harmony, symbols, "save.read", nameof(Hooks.LoadPostfix));

        if (debugEnabled.Value)
        {
            DebugBridge = new DebugBridge(Path.Combine(cfgDir, "debug"), host, new OldenEraDebugEngine(), Log);
            MethodTrace.Install(harmony, trace.Value, Log);
            Log.LogWarning("WoG Debug is ON: command bridge in " + Path.Combine(cfgDir, "debug", "in"));
        }
        FrameHook.Install(harmony, Log);

        Log.LogInfo($"WoG for Olden Era {Version} loaded ({(era ? "ERA" : "WoG")} dialect); {symbols.Missing.Count} game symbols missing (see above).");
    }

    /// <summary>ERA mod folders, highest priority first (ERA's list.txt is lowest priority first: a translation
    /// mod such as "WoG Rus" comes after the mod it overrides).</summary>
    static List<string> EraModFolders(string root, string list)
    {
        IEnumerable<string> names = list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (!names.Any())
        {
            string listTxt = Path.Combine(root, "list.txt");
            names = File.Exists(listTxt) ? File.ReadAllLines(listTxt).Select(l => l.Trim()).Where(l => l.Length > 0) : Enumerable.Empty<string>();
        }
        var result = new List<string>();
        foreach (var n in names.Reverse())
        {
            string dir = Path.Combine(root, n);
            if (Directory.Exists(dir)) result.Add(dir);
            else L?.LogWarning("WoG: ERA mod folder not found: " + dir);
        }
        return result;
    }

    static void LoadIdMaps(string cfgDir, WoGHost host)
    {
        string dir = Path.Combine(cfgDir, "id-maps");
        if (!Directory.Exists(dir)) return;
        foreach (var f in Directory.GetFiles(dir, "*.json"))
            host.State.Ids.LoadDomainFile(Path.GetFileNameWithoutExtension(f), f);
    }

    void HookPostfix(Harmony h, OldenEraSymbols s, string key, string patch) => Hook(h, s, key, patch, prefix: false);
    void HookPrefix(Harmony h, OldenEraSymbols s, string key, string patch) => Hook(h, s, key, patch, prefix: true);

    void Hook(Harmony harmony, OldenEraSymbols symbols, string key, string patchName, bool prefix)
    {
        if (!symbols.Has(key)) { Log.LogInfo($"WoG: hook {key} disabled (symbol not verified)"); return; }
        if (symbols.MemberOf(key) is not MethodBase target) { Log.LogWarning($"WoG: {key} is not a method"); return; }
        var patch = new HarmonyMethod(typeof(Hooks).GetMethod(patchName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public));
        harmony.Patch(target, prefix: prefix ? patch : null, postfix: prefix ? null : patch);
        Log.LogInfo($"WoG: hooked {key} → {target.DeclaringType?.FullName}.{target.Name}");
    }
}

/// <summary>
/// One WoG game per Olden Era session. A session is the game's state object (symbol game.root); when it
/// changes, a new map was started or loaded.
/// </summary>
internal static class WoGSession
{
    static IntPtr current;

    public static int DayStarts { get; private set; }
    public static string LastDayStart { get; private set; } = "never";

    /// <summary>Starts the WoG game for a new session; true while a session runs.</summary>
    public static bool Ensure()
    {
        var host = WoGPlugin.Host;
        var adapter = WoGPlugin.Adapter;
        if (host == null || adapter == null) return false;
        object? root;
        try { root = adapter.Root(); }
        catch (Exception ex) { WoGPlugin.L?.LogError("WoG: reading game.root failed: " + ex.Message); return false; }
        IntPtr ptr = root is Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase o ? o.Pointer : IntPtr.Zero;
        if (ptr == IntPtr.Zero) { current = IntPtr.Zero; return false; }
        if (ptr == current)
        {
            if (firstDayPending) TryFirstDay(host, adapter);
            return true;
        }
        current = ptr;
        // Saved WoG state is matched to saves once save.write/save.read are verified; until then every
        // session starts as a new WoG game (ERA instructions, OnGameEnter).
        WoGPlugin.L?.LogInfo("WoG: new game session — running ERM instructions");
        host.StartNewGame();
        firstDayPending = true;
        return true;
    }

    static bool firstDayPending;

    /// <summary>
    /// Olden Era does not call its day start (turn.start) on day 1, while ERA runs OnEveryDay and the timers on
    /// day 1 too. Once the new map is ready — players exist and the local player has a hero — day 1 is started
    /// here, once. A game loaded on day 1 would get it again: loads are told apart once save.read is verified.
    /// </summary>
    static void TryFirstDay(WoGHost host, OldenEraGameAdapter adapter)
    {
        int day = host.Game.Clock.AbsoluteDay;
        if (day > 1) { firstDayPending = false; return; }
        if (day < 1) return; // the map is still being built (the day counter starts at 0)
        if (adapter.PlayerObjects().Count == 0) return;
        int local = host.Game.Players.CurrentPlayer;
        if (adapter.GetHeroes(local) is { IsOk: true } mine && mine.Value.Count == 0) return;
        firstDayPending = false;
        WoGPlugin.L?.LogInfo("WoG: day 1 — " + StartDay());
    }

    /// <summary>
    /// The start of a day: ERA OnEveryDay and the timers for every player still in the game, in side order,
    /// each with that player as the current one (Olden Era starts a day for all sides at once).
    /// </summary>
    public static string StartDay()
    {
        var host = WoGPlugin.Host;
        var adapter = WoGPlugin.Adapter;
        if (host == null || adapter == null) return "WoG is not loaded";
        if (!Ensure()) return "no game session";
        int day = host.Game.Clock.AbsoluteDay;
        var players = new List<int>();
        int count = adapter.PlayerObjects().Count;
        for (int p = 0; p < count; p++)
        {
            if (adapter.IsAlive(p) is { IsOk: true, Value: false }) continue;
            adapter.CurrentOverride = p;
            try { host.Events.Raise(new WoGEvent { Kind = WoGEventKind.PlayerDayStarted, Player = p }); }
            finally { adapter.CurrentOverride = null; }
            players.Add(p);
        }
        DayStarts++;
        LastDayStart = $"day {day}, players {string.Join(",", players)} at {DateTime.Now:HH:mm:ss}";
        return "day started: " + LastDayStart;
    }
}

/// <summary>Harmony patch bodies: translate game calls into engine-neutral WoG events. They never throw into the game.</summary>
internal static class Hooks
{
    static WoGHost? H => WoGPlugin.Host;

    internal static void TurnStartPostfix()
    {
        try { WoGPlugin.L?.LogInfo("WoG: " + WoGSession.StartDay()); }
        catch (Exception ex) { WoGPlugin.L?.LogError("WoG: day start failed: " + ex); }
    }

    // The argument layout of the interaction method is not known yet; until it is verified the hooks
    // only signal that an interaction happened (position/hero come from the bound symbols later).
    internal static bool InteractPrefix()
    {
        if (H == null) return true;
        try
        {
            var e = H.Events.Raise(new WoGEvent { Kind = WoGEventKind.HeroVisitPre, Player = H.Game.Players.CurrentPlayer });
            return !e.CancelNative;
        }
        catch (Exception ex) { WoGPlugin.L?.LogError("WoG: interaction hook failed: " + ex.Message); return true; }
    }

    internal static void InteractPostfix()
    {
        try { H?.Events.Raise(new WoGEvent { Kind = WoGEventKind.HeroVisitPost, Player = H.Game.Players.CurrentPlayer }); }
        catch (Exception ex) { WoGPlugin.L?.LogError("WoG: interaction hook failed: " + ex.Message); }
    }

    internal static void BattleStartPostfix()
    {
        try { H?.Events.Raise(new WoGEvent { Kind = WoGEventKind.BattleStart }); }
        catch (Exception ex) { WoGPlugin.L?.LogError("WoG: battle hook failed: " + ex.Message); }
    }

    internal static void BattleEndPostfix()
    {
        try { H?.Events.Raise(new WoGEvent { Kind = WoGEventKind.BattleEnd }); }
        catch (Exception ex) { WoGPlugin.L?.LogError("WoG: battle hook failed: " + ex.Message); }
    }

    internal static void SavePostfix()
    {
        if (H == null) return;
        try
        {
            string path = Path.Combine(Paths.ConfigPath, "WoG", "last.wog.json");
            H.SaveTo(path, "last");
        }
        catch (Exception ex) { WoGPlugin.L?.LogError("WoG save failed: " + ex.Message); }
    }

    internal static void LoadPostfix()
    {
        if (H == null) return;
        string path = Path.Combine(Paths.ConfigPath, "WoG", "last.wog.json");
        try
        {
            if (File.Exists(path)) H.LoadFrom(path, "last");
            else WoGPlugin.L?.LogWarning("WoG: no WoG state for this save — WoG state starts fresh");
        }
        catch (Exception ex) { WoGPlugin.L?.LogError("WoG load failed (state reset): " + ex.Message); }
    }
}

/// <summary>
/// Per-frame tick: a Harmony postfix on UnityEngine.EventSystems.EventSystem.Update, which runs every frame
/// in the game's UI (the hook gme-mod verified on Olden Era). Looked up by name: no Unity reference needed.
/// </summary>
internal static class FrameHook
{
    static DateTime next;

    public static void Install(Harmony harmony, ManualLogSource log)
    {
        var type = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "UnityEngine.UI")
            ?.GetType("UnityEngine.EventSystems.EventSystem");
        var update = type?.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(m => m.Name == "Update" && m.GetParameters().Length == 0);
        if (update == null) { log.LogWarning("WoG: EventSystem.Update not found — no per-frame tick (debug bridge, session tracking)"); return; }
        harmony.Patch(update, postfix: new HarmonyMethod(typeof(FrameHook), nameof(Tick)));
    }

    /// <summary>Runs every frame; does its work at most four times a second.</summary>
    static void Tick()
    {
        var now = DateTime.UtcNow;
        if (now < next) return;
        next = now.AddMilliseconds(250);
        try
        {
            WoGSession.Ensure();
            WoGPlugin.DebugBridge?.Poll();
        }
        catch (Exception ex) { WoGPlugin.L?.LogError("WoG tick failed: " + ex); }
    }
}

/// <summary>Asset probe for the running game. Until asset lookup is verified it reports nothing available,
/// so every visual resolves to its placeholder — gameplay is never blocked by art.</summary>
internal sealed class UnityAssetProbe : IVisualAssetProbe
{
    public bool IsAvailable(VisualCandidate candidate) => false;
}
