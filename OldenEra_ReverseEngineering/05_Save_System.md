# Olden Era — save system

## Known

* Scenario script `AutoSave` action forces an autosave; "actions immediately after AutoSave will not be
  saved" **[V-community]** — saving is snapshot-based and happens between logic ticks.
* Counters and story counters are persisted by the game (they survive save/load and, for story
  counters, missions) **[V-community]**.
* Unity convention: saves and `Player.log` live under
  `%USERPROFILE%\AppData\LocalLow\<company>\<product>\` (from `app.info`) **[UNVERIFIED for this title]**.

## Unknown — must be established on a real install

* Save file format (binary/JSON/compressed), location, and whether it has an extension slot.
* The managed methods that write/read a save (needed as Harmony hook points).

## Strategy that does not depend on the unknowns (implemented)

The WoG state is **never** stored only in memory, and **never** relies on the game's format:

1. On every game save (Harmony postfix on the save method, or — as a fallback — a file-system watcher
   on the save folder), the plugin writes `<savename>.wog.json` next to the save: the full
   `WoGGameState` (options, ERM variables, commanders, stack experience, map object state, timers,
   macros, id map) produced by `WoGSaveSerializer` with schema version and SHA-256 checksum.
2. On load, after the game has restored its state, the plugin loads the matching `.wog.json`, verifies
   the checksum and the save's identity, then raises `!?GM0`. Identity = save file name **plus** a
   fingerprint of native state the plugin can read back (game day, map id, hero ids/levels). Preferred
   upgrade once verified: store a GUID in a scenario counter, which is part of the native save
   **[UNVERIFIED: counters reachable from the plugin]**, so the link survives renamed files.
3. Missing/mismatched blob → WoG state reset to "new game" values with a visible warning; never silent.

This keeps critical state outside volatile memory while being robust to the unknown native format.
