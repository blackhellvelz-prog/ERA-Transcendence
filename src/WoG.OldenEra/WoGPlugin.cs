using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using WoG.Core.Events;
using WoG.Core.Visual;
using WoG.Erm.Runtime;
using WoG.Host;

namespace WoG.OldenEra;

/// <summary>
/// Entry point inside Olden Era. Wires WoG Core to the game:
///  1. loads BepInEx/config/wog_symbols.json and resolves the game symbols (gme-mod pattern);
///  2. builds the WoG host over <see cref="OldenEraGameAdapter"/>;
///  3. patches the verified game methods (turn start, object interaction, battle, save/load) with
///     Harmony so the engine raises WoG events;
///  4. loads ERM scripts from BepInEx/config/WoG/scripts (bring-your-own WoG scripts).
/// Every unverified piece stays off and is reported in BepInEx/LogOutput.log.
/// </summary>
[BepInPlugin(Guid, "In the Wake of Gods for Olden Era", Version)]
public sealed class WoGPlugin : BasePlugin
{
    public const string Guid = "wog.oldenera";
    public const string Version = "0.1.0";

    internal static WoGPlugin? Instance;
    internal static WoGHost? Host;
    internal static ManualLogSource? L;

    public override void Load()
    {
        Instance = this;
        L = Log;
        string cfgDir = Path.Combine(Paths.ConfigPath, "WoG");
        Directory.CreateDirectory(cfgDir);
        string symbolsPath = Path.Combine(Paths.ConfigPath, "wog_symbols.json");
        if (!File.Exists(symbolsPath))
        {
            OldenEraSymbols.WriteTemplate(symbolsPath);
            Log.LogWarning($"WoG: wrote symbol template {symbolsPath}; fill it after the in-game RE step (07_InGame_RE_Plan.md)");
        }
        var symbols = OldenEraSymbols.Load(symbolsPath);
        symbols.Resolve(m => Log.LogInfo(m));

        WoGHost? host = null;
        var adapter = new OldenEraGameAdapter(symbols, () => host!.State.Ids);
        host = new WoGHost(adapter, new VisualResolver(new UnityAssetProbe()), new WoGModules(), new ErmRuntimeOptions());
        Host = host;
        host.Erm!.Log = m => Log.LogInfo("[ERM] " + m);

        LoadIdMaps(cfgDir, host);
        string scripts = Path.Combine(cfgDir, "scripts");
        if (Directory.Exists(scripts))
            foreach (var f in Directory.GetFiles(scripts, "*.erm").OrderBy(x => x, StringComparer.Ordinal))
                host.AddScriptFile(f);

        var harmony = new Harmony(Guid);
        HookPostfix(harmony, symbols, "turn.start", nameof(Hooks.TurnStartPostfix));
        HookPrefix(harmony, symbols, "object.interact", nameof(Hooks.InteractPrefix));
        HookPostfix(harmony, symbols, "object.interact", nameof(Hooks.InteractPostfix));
        HookPostfix(harmony, symbols, "battle.start", nameof(Hooks.BattleStartPostfix));
        HookPostfix(harmony, symbols, "battle.end", nameof(Hooks.BattleEndPostfix));
        HookPostfix(harmony, symbols, "save.write", nameof(Hooks.SavePostfix));
        HookPostfix(harmony, symbols, "save.read", nameof(Hooks.LoadPostfix));

        Log.LogInfo($"WoG for Olden Era {Version} loaded; {symbols.Missing.Count} game symbols missing (see above).");
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

/// <summary>Harmony patch bodies: translate game calls into engine-neutral WoG events.</summary>
internal static class Hooks
{
    static WoGHost? H => WoGPlugin.Host;

    internal static void TurnStartPostfix()
    {
        if (H == null) return;
        H.Events.Raise(new WoGEvent { Kind = WoGEventKind.PlayerDayStarted, Player = H.Game.Players.CurrentPlayer });
    }

    // The argument layout of the interaction method is not known yet; until it is verified the hooks
    // only signal that an interaction happened (position/hero come from the bound symbols later).
    internal static bool InteractPrefix()
    {
        if (H == null) return true;
        var e = H.Events.Raise(new WoGEvent { Kind = WoGEventKind.HeroVisitPre, Player = H.Game.Players.CurrentPlayer });
        return !e.CancelNative;
    }

    internal static void InteractPostfix()
    {
        H?.Events.Raise(new WoGEvent { Kind = WoGEventKind.HeroVisitPost, Player = H.Game.Players.CurrentPlayer });
    }

    internal static void BattleStartPostfix() => H?.Events.Raise(new WoGEvent { Kind = WoGEventKind.BattleStart });
    internal static void BattleEndPostfix() => H?.Events.Raise(new WoGEvent { Kind = WoGEventKind.BattleEnd });

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

/// <summary>Asset probe for the running game. Until asset lookup is verified it reports nothing available,
/// so every visual resolves to its placeholder — gameplay is never blocked by art.</summary>
internal sealed class UnityAssetProbe : IVisualAssetProbe
{
    public bool IsAvailable(VisualCandidate candidate) => false;
}
