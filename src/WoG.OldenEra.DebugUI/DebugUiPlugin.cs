using System;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using UnityEngine.InputSystem;

namespace WoG.OldenEra.DebugUI;

/// <summary>
/// The WoG Debug window in the running game. It exists only with WoG Debug on ([Debug] Enabled), next to the
/// command bridge, and runs the same commands.
/// </summary>
[BepInPlugin(Guid, "ERA:Transcendence — WoG Debug window", WoGPlugin.Version)]
[BepInDependency(WoGPlugin.Guid)]
public sealed class DebugUiPlugin : BasePlugin
{
    public const string Guid = "wog.oldenera.debugui";

    public override void Load()
    {
        var commands = WoGPlugin.Commands;
        if (commands == null)
        {
            Log.LogInfo("WoG Debug is off: no in-game debug window");
            return;
        }
        commands.Add("ui", UiProbe.Run,
            "the game's UI for the window's look: ui canvases | ui tree <name> [depth] | ui sprites <filter> | ui fonts | ui keys [filter]");
        // F9 was Olden Era's quick load (the game reloaded under the window). The game binds F1 F2 F5 F7 F9 F12, the
        // back quote and most letters (its JsonBindingContainer in resources.assets); F10 opens the window menu in Windows
        var hotkey = Config.Bind("Window", "Hotkey", "F8",
            "The key that opens and closes the WoG Debug window (a UnityEngine.InputSystem.Key name: F8, F6, F11, …). " +
            "Olden Era uses F1 F2 F5 F7 F9 F12, the back quote and most letters.");
        if (!Enum.TryParse<Key>(hotkey.Value, true, out var key) || key == Key.None)
        {
            Log.LogWarning($"WoG Debug window: unknown hotkey \"{hotkey.Value}\", using F8");
            key = Key.F8;
        }
        var window = new DebugWindow(commands, Log, key);
        WoGPlugin.Frame += window.Tick;
        Log.LogInfo($"WoG Debug window: waiting for the game's interface ({key} opens it)");
    }
}
