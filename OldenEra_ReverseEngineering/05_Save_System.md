# Olden Era — save system

## Known

* The `AutoSave` scenario action forces an autosave; "actions immediately after AutoSave are not saved"
  **[V-community]** — a save is a snapshot taken between logic ticks.
* Counters and story counters are saved by the game (they survive save/load, and story counters also survive the
  transition between missions) **[V-community]**.
* Unity convention: saves and `Player.log` live in `%USERPROFILE%\AppData\LocalLow\<company>\<product>\`
  (from `app.info`) **[UNVERIFIED for this game]**.

* **[V-game]** Location: `%USERPROFILE%\AppData\LocalLow\Unfrozen\HeroesOldenEra\users\Steam_<id>\saves\singleplayer\<game folder>\*.saveskirmish`
  (autosaves `as_<day>`, quicksave); settings in `…\users\Steam_<id>\prefs\` (`Settings.json`, `SettingsLocal.json` —
  display mode and resolution); `Player.log` in `LocalLow\Unfrozen\HeroesOldenEra\`.
* **[V-game]** Format: gzip; inside — a length-prefixed hash string, the game version string (`0.80.48` in a September
  2026 save), then a JSON header (`title`, `template`, `gameMode`, `spawns`, …) and further data not parsed yet. Lobby
  presets (`*.lobby`) are MessagePack.
* Candidate save method: `Hex.Session.Data.Data.Save(string _fileName)` (interop) **[UNVERIFIED as the save hook]**.

## Unknown — must be established on a real installation

* Save file format (binary/JSON/compressed), location, and whether there is room for extensions.
* Managed methods that write/read the save (needed as Harmony hook points).

## Strategy that does not depend on the unknowns (implemented)

WoG state is **never** kept only in memory and does **not** depend on the game's format:

1. On every game save (a Harmony postfix on the save method — symbol `save.write`; fallback —
   watching the saves folder) the plugin writes a WoG state block next to the save: the full `WoGGameState`
   (options, ERM variables, commanders, stack experience, object data, timers, macros, IdMap), serialized by
   `WoGSaveSerializer` with a schema number and SHA-256.
2. On load, after the game has restored its own state, the plugin reads the corresponding block,
   verifies the checksum and that it belongs to this save, then raises `!?GM0`. Ownership = the save file
   name **plus** a fingerprint of the native state the plugin can read (day, map id, hero ids/levels).
   Improvement once verified: store a GUID in a scenario counter that is part of the native save
   **[UNVERIFIED: whether counters are accessible from the plugin]** — then the link will survive file renames.
3. No block / mismatch → WoG state is reset to "new game" with a visible warning; never silently.

**Current state of the code:** `Hooks.SavePostfix/LoadPostfix` in `WoGPlugin.cs` write/read a single file,
`BepInEx/config/WoG/last.wog.json`, because the argument layout of the save method (slot name) is not yet
known. Once the `save.write` symbol is found, the file name will be taken from the argument — this is noted in
`07_InGame_RE_Plan.md`. Serialization, integrity checking and load ordering are already implemented and covered by tests.
