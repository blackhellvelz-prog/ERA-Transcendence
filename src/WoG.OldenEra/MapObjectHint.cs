using System;
using System.Globalization;
using WoG.Erm.Receivers;

namespace WoG.OldenEra;

/// <summary>
/// WoG's hint of a map object in Olden Era's tooltip. Olden Era sets the tooltip's texts (BhTooltip.vzp: name,
/// content, extra), then shows it over the object's model with BhTooltip.Show(camera, model, delay) [V-game 0.82.07,
/// WoG Debug traces]. When ERM gave the object a hint — Era's SN:H, WoG's OB:H and HT, in their order
/// (<see cref="MapHints.ForObject"/>) — it takes the place of the object's name, as in H3, where the hint stood in the
/// status bar in place of the name; the game's own content (what the object gives) stays.
/// </summary>
internal static class MapObjectHint
{
    static bool failed;

    /// <summary>Hooks.TooltipShowAtPrefix: before the tooltip of <paramref name="model"/> shows.</summary>
    public static void Show(object tooltip, object? model)
    {
        if (failed || model == null) return;
        try
        {
            var rt = WoGPlugin.Host?.Erm;
            var adapter = WoGPlugin.Adapter;
            var sym = WoGPlugin.Symbols;
            if (rt == null || adapter == null || sym == null || !sym.Has("ui.tooltipHeader")) return;
            if (UnityObjects.Describe(model) is not var (name, x, _, z)) return;
            var obj = adapter.ObjectOfModel(name, x, z);
            if (obj == null || obj.Type == 34) return; // a hero has its own (HE) hint in WoG, not ported yet
            var players = adapter.Players;
            int hero = players.GetActiveHero(players.CurrentPlayer) is { IsOk: true } h ? h.Value : -1;
            // a tooltip shows only for an object the player sees (Era's hints need a visible square)
            string? text = MapHints.ForObject(rt, obj.Position, obj.Type, obj.SubType, visible: true, hero);
            if (text == null || sym.Read("ui.tooltipHeader", tooltip) is not { } header) return;
            header.GetType().GetProperty("text")?.SetValue(header, text);
        }
        catch (Exception ex)
        {
            failed = true; // the game's tooltip stays as it is
            WoGPlugin.L?.LogError("WoG: map hints stopped: " + ex);
        }
    }
}

/// <summary>Unity objects read by reflection (the plugin has no Unity references): a GameObject's or component's name and place.</summary>
internal static class UnityObjects
{
    /// <summary>The name and world position; null for an object without a transform.</summary>
    public static (string? Name, float X, float Y, float Z)? Describe(object o)
    {
        var transform = o.GetType().GetProperty("transform")?.GetValue(o);
        if (transform == null) return null;
        var pos = transform.GetType().GetProperty("position")?.GetValue(transform);
        float F(string f) => pos?.GetType().GetField(f)?.GetValue(pos) is float v ? v : float.NaN;
        return (Name(o), F("x"), F("y"), F("z"));
    }

    public static string? Name(object? o) => o?.GetType().GetProperty("name")?.GetValue(o) as string;

    /// <summary>The names of up to four parents of a Unity object (its place in the scene).</summary>
    public static string Parents(object o)
    {
        var names = new System.Collections.Generic.List<string>();
        var transform = o.GetType().GetProperty("transform")?.GetValue(o);
        var p = transform?.GetType().GetProperty("parent")?.GetValue(transform);
        for (int i = 0; p != null && i < 4; i++, p = p.GetType().GetProperty("parent")?.GetValue(p)) names.Add(Name(p) ?? "?");
        return string.Join(" < ", names);
    }

    public static string Number(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);
}
