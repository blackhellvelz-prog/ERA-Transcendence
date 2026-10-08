using BepInEx;
using BepInEx.Unity.IL2CPP;

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
            "the game's UI for the window's look: ui canvases | ui tree <name> [depth] | ui sprites <filter> | ui fonts");
        var window = new DebugWindow(commands, Log);
        WoGPlugin.Frame += window.Tick;
        Log.LogInfo("WoG Debug window: waiting for the game's interface (F9 opens it)");
    }
}
