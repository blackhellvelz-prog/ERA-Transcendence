**English** | [Русский](08_Save_Load.ru.md)

# Saving/loading WoG state

Sources: `SaveERM`/`LoadERM` (`erm.cpp`), `SaveNPC`/`LoadNPC` (`npc.cpp`), `CrExpoSet::Save`, `CrExpMod::Save`,
`CrExpBon::Save` (`crexpo.cpp`), `GameBeforeSave`/`GameAfterLoad` (`erm.cpp`).

## 1. What WoG writes to the save (in this order inside the ERM block)

1. The `"LERM"` label, Lua state, string manager.
2. `f…t` (`ERMVar`) + the names of their macros.
3. `v1…v10000` + macro names.
4. The current w-hero (`ERMW`), the `w` variables of all heroes + macro names.
5. Flags 1…1000.
6. The macro table (`ERMMacroName`, `ERMMacroVal`).
7. ERM data of objects (`ERM_Object[]`).
8. `z1…z1000` + macro names.
9. Timers `TM1…100` (+ the last auto-timer).
10. ERM data of heroes (`ERM_Hero`).
11. "Week/month …" overrides and their messages.
12. Tile data (`Square`, `Square2`), the `HTable` hint table, the quest log.
13. Artifact overrides (names, cost, slot, type, combination artifacts, ban, spell).
14. The monster upgrade table, the 8th-level option, mithril/chest settings.
15. **WoG Options, row 0** (the first half of `PL_WoGOptions`).
16. Secondary skill names, monster names, hero specialties (+ names).
17. 3.58: AI delay, autosave flag. 3.59: text constants, whether the Grail is enabled.

Separately: commanders (the whole `NPCs[]`), stack experience records + `PlayerMult` + AI tables, `CREXPMOD`
parameters per type, `CREXPBON` tables, creature data modified via `MA`.

## 2. What is intentionally **not** saved

`x`, `y`, `e`, local `z-*` — which is why a save cannot happen inside a trigger section. Parsed scripts are
re-read from the map/ERM files on load; instructions (`!#`) are **not** executed on load (except those after the
`!@ZVSE` marker); `!?PI` does not fire on load.

## 3. Load order

Native game load → restore the WoG blocks → re-parse the scripts without instructions →
**`!?GM0`** ("right after loading, before the map is shown"). Before saving — **`!?GM1`**.

## 4. Requirements for the port (implemented in `WoG.Core.Save` and `WoGHost`)

* A versioned WoG state block (`WoGSaveSerializer`: JSON + schema number + SHA-256), written next to the
  Olden Era save (see `OldenEra_ReverseEngineering/05_Save_System.md`).
* Identity mapping: WoG binds records to H3 hero numbers / positions; the port binds them to stable Olden Era
  ids (hero sid, object entity, army slot) via an `IdMap` stored in the same block.
* The load order is reproduced: restore state → parse the scripts without instructions → `GM0`
  (test `Host_save_load_keeps_erm_state_and_fires_GM_triggers`).
* Round-trip test: state → save → load → identical state
  (`Round_trip_preserves_all_wog_state`); a corrupted or foreign block is rejected.
