# Save/load of WoG state

Sources: `SaveERM`/`LoadERM` (`erm.cpp`), `SaveNPC`/`LoadNPC` (`npc.cpp`), `CrExpoSet::Save`,
`CrExpMod::Save`, `CrExpBon::Save` (`crexpo.cpp`), `GameBeforeSave`/`GameAfterLoad` (`erm.cpp`).

## 1. What WoG writes into a savegame (in this order inside the ERM block)

1. `"LERM"` tag, Lua state, string manager.
2. `f…t` (`ERMVar`) + their macro names.
3. `v1…v10000` + macro names.
4. current w-hero (`ERMW`), `w` vars of all heroes + macro names.
5. flags 1…1000.
6. macro table (`ERMMacroName`, `ERMMacroVal`).
7. per-object ERM data (`ERM_Object[]`).
8. `z1…z1000` + macro names.
9. timers `TM1…100` (+ last auto-timer).
10. per-hero ERM data (`ERM_Hero`).
11. next "week of"/"month of" overrides and messages.
12. per-square storage (`Square`, `Square2`), hint table `HTable`, quest log.
13. artifact setup overrides (names, cost, slot, type, combo info, disabled, spell).
14. monster upgrade table, 8th-level option, mithril/chest settings.
15. **WoG options row 0** (first half of `PL_WoGOptions`).
16. secondary-skill names, monster names, hero specialisations (+ names).
17. 3.58: AI delay, autosave flag. 3.59: text constants, grail enabled.

Separately: commanders (`NPCs[]` whole), stack experience records + `PlayerMult` + AI tables,
`CREXPMOD` per-type parameters, `CREXPBON` bonus tables, creature data changed by `MA` (sent/saved).

## 2. What is deliberately **not** saved

`x`, `y`, `e`, local `z-*` — so no save may happen inside a trigger section. Parsed scripts are
re-read from the map/ERM files on load; instructions (`!#`) are **not** re-executed on load, except
after `!@` (post-instruction marker); `!?PI` does not fire on load.

## 3. Load order

Native game load → WoG blocks restored → scripts re-parsed (instructions skipped) → **`!?GM0`** fires
"right after loading but before showing the map". Before save: **`!?GM1`**.

## 4. Requirements for the port

* A versioned WoG save blob (`WoGSaveSerializer`, JSON + schema version + checksum) containing every
  item of §1 that the port models, written next to / inside the Olden Era save (see
  `OldenEra_ReverseEngineering/05_Save_System.md`).
* Identity mapping: WoG keys records by H3 hero index / map position; the port keys them by stable
  Olden Era ids (hero sid, object entity sid, army slot) through the `IdMap` stored in the same blob.
* Load sequence reproduced: restore state → re-parse scripts without instructions → raise `GM0`.
* Round-trip test: state → save → load → identical state (`SaveRoundTripTests`).
