using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace WoG.Core.Options;

/// <summary>
/// Option metadata. Engine options are described here from erm.h/wogsetup.cpp. Script options come
/// from a JSON catalogue (Compatibility/options-defaults.json) or, when available, from the user's
/// own WoG install (ZSETUP00.TXT + WoGSetupEx.dat via <see cref="LoadPresetFile"/>).
///
/// Defaults: in WoG a new game copies the *dialog state* (row 1, loaded from the user's preset file)
/// into row 0 (ResetWogify). There is no hard-coded default table in the engine, so every built-in
/// default below is marked DefaultVerified = false.
/// </summary>
public static class WoGOptionCatalog
{
    public static IReadOnlyList<WoGOptionDefinition> Engine { get; } = new[]
    {
        Def(WoGOptionIds.ExtDwellStd, "8th level dwellings: standard", 0, "towns", "dwellings"),
        Def(WoGOptionIds.TowerStd, "No enhanced town towers (inverted)", 0, "towns", "battle"),
        Def(WoGOptionIds.MLeaveStd, "Monsters never leave (inverted)", 0, "armies"),
        Def(WoGOptionIds.NoNPC, "No commanders (inverted)", 0, "commanders"),
        Def(WoGOptionIds.NoTownDem, "No town demolition (inverted)", 0, "towns"),
        Def(WoGOptionIds.ApplyWoG, "Apply WoG to non-WoG maps", 0, "scripts", "map"),
        Def(WoGOptionIds.NPC2Hire, "Commanders must be hired in town", 0, "commanders", "towns"),
        Def(WoGOptionIds.DwellAccum, "Dwellings accumulate creatures", 0, "dwellings"),
        Def(WoGOptionIds.GuardAccum, "Dwelling guards accumulate", 0, "dwellings"),
        Def(WoGOptionIds.CentElf, "Centaur/elf tweak", 0, "creatures"),
        Def(WoGOptionIds.MLeaveStyle, "Monster leaving style", 0, "armies"),
        Def(WoGOptionIds.CrExpEnable, "Stack experience enabled", 1, "stack-experience", "battle"),
        Def(WoGOptionIds.CrExpStyle, "Stack experience sharing style (0..3)", 0, "stack-experience"),
        Def(WoGOptionIds.LeaveArt, "Leave artifacts on death", 0, "artifacts"),
        Def(WoGOptionIds.CheatDis, "Cheats disabled", 0, "cheats"),
        Def(WoGOptionIds.ErmErrDis, "Suppress ERM error dialogs", 0, "erm"),
        Def(WoGOptionIds.ErmError, "ERM error state", 0, "erm"),
        Def(WoGOptionIds.ExpGainDis, "Stack experience gain disabled", 0, "stack-experience"),
        Def(WoGOptionIds.NewHero, "New hero setup", 0, "heroes"),
    };

    static WoGOptionDefinition Def(int i, string name, int def, params string[] systems) => new()
    {
        Index = i, Name = name, Default = def, AffectedSystems = systems, DefaultVerified = false,
    };

    sealed class JsonOption
    {
        public int index { get; set; }
        public string name { get; set; } = "";
        public string description { get; set; } = "";
        public int @default { get; set; }
        public bool defaultVerified { get; set; }
        public string[] systems { get; set; } = Array.Empty<string>();
    }

    /// <summary>Loads script option definitions from the JSON catalogue.</summary>
    public static List<WoGOptionDefinition> LoadJson(string path)
    {
        var items = JsonSerializer.Deserialize<List<JsonOption>>(File.ReadAllText(path)) ?? new();
        return items.Select(o => new WoGOptionDefinition
        {
            Index = o.index, Name = o.name, Description = o.description, Default = o.@default,
            DefaultVerified = o.defaultVerified, AffectedSystems = o.systems,
        }).ToList();
    }

    /// <summary>
    /// Loads a WoG options preset (.dat). SaveSetupState/LoadSetupState forward to ZvsLib1.dll, whose
    /// source is not available, so the on-disk layout is UNVERIFIED. This reader assumes the buffer is
    /// written verbatim (row 0 = 1000 little-endian int32) and rejects files that are too short.
    /// </summary>
    public static bool LoadPresetFile(string path, WoGOptions into)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length < WoGOptionIds.Count * 4) return false;
        for (int i = 0; i < WoGOptionIds.Count; i++)
            into.Values[i] = BitConverter.ToInt32(bytes, i * 4);
        return true;
    }
}
