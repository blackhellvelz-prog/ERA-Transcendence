**English** | [Русский](README.ru.md)

# ERA:Transcendence

**ERA:Transcendence** is the working name of this port: HoMM3 ERA for Heroes of Might and Magic: Olden Era.

A port of **HoMM3 ERA** — the advanced build of *In the Wake of Gods* (WoG 3.58f + the Era 3.9.31 engine + ERM 2.0 +
ERA Project scripts) — to **Heroes of Might and Magic: Olden Era**. The goal is not a "mod inspired by" ERA, but a
reproduction of ERA's behavior: the ERM 2.0 language, commanders, stack experience, WoG Options, WoG and ERA scripts —
as precisely as the Olden Era engine allows.

Russian version: [README.ru.md](README.ru.md). Every document in this repository has a Russian twin next to it (`*.ru.md`).

Initially the target was WoG 3.58; by the user's decision the target is ERA (`ERA-Projects/era-project-eng`,
`era-project-rus`). ERA is built on WoG, so all the WoG 3.58 work continues to be used as the lower layer.

The work follows the methodology of the `mod-any-game` skill from the
[rehan-remade/universal-modder](https://github.com/rehan-remade/universal-modder) repository: engine recon → choosing
the cheapest route → reading primary sources → vertical slice → oracle check → log (`MODLOG.md`).

## What is already here

| Part | Status |
|------|--------|
| ERA reverse engineering (`ERA_ReverseEngineering/`) | from the Era 3.9.31 engine sources (`ethernidee/era`) and the ERA Project 2.291 corpus |
| WoG 3.58 reverse engineering (`WoG_ReverseEngineering/`) | from the WoG source code (`GrayFace/wog`), the ERM help and the 78 scripts of 3.58f |
| Olden Era reverse engineering (`OldenEra_ReverseEngineering/`) | from working community mods/tools and the running game; every fact is tagged with its verification level |
| Compatibility matrix (`Compatibility/`) | for every feature: strategy, status, limitations; the ERM tables (WoG and ERA) are generated from code |
| Core (`src/WoG.Core`) | state, model, options and the WoG Options dialog (`WoGOptionSetup`: the installation's defaults), events, saving (JSON + SHA-256), ERA state, IdMap, Heroes III pictures (LOD/PAC, DEF, PCX) |
| ERM (`src/WoG.Erm`) | parser and interpreter for WoG 3.58/3.59 **and ERA**: ERM 2.0 preprocessor, ERA parameters/conditions/control flow/functions, receivers for heroes, towns, players, the map, battles, translations, ERT |
| Commanders (`src/WoG.Commanders`) | the `npc.cpp` tables and formulas: levels, skills, special bonuses, artifacts, hiring/resurrection, battle profile |
| Stack experience (`src/WoG.CreatureExperience`) | ranks, gaining experience after battle, merging, bonuses, `CREXPMOD/CREXPBON.TXT` loaders |
| Host (`src/WoG.Host`) | ties everything together: ERA mods in Era's order, WoGification and the WoG Options at a new map, events → ERM triggers (`BattleActionTracker` for the battle triggers), save/load |
| Olden Era plugin (`src/WoG.OldenEra`) | BepInEx 6 IL2CPP; loads the ERA mods from the user's ERA installation; game symbols verified in game (`wog_symbols.json`); hooks for days, object visits, battles and their events; the command bridge of WoG Debug |
| Interface plugin (`src/WoG.OldenEra.DebugUI`) | built from Olden Era's own window frame, buttons and fonts: WoG's questions in the game's style (always) and the **WoG Debug window** (with WoG Debug on): every feature of the port as a tile with a round icon (HoMM3, WoG and ERA pictures read from the ERA installation at run time, Olden Era sprites), round group tabs, a search, an ERM console; F8 or the round "WoG" button in the top bar |
| Data overlay (`src/WoG.OldenEra.Data`) | reading `Core.zip`, unit clones, experience-rank buffs, localization in the OE format |
| Tests (`tests/WoG.Tests`) | 271 xUnit tests; the corpus tests run the real WoG and ERA scripts and decode every picture of an ERA installation |
| Tools (`tools/`) | `WoG.ErmTool`: `parse`, `run`, `compat`, `era-pp`, `probe-symbols`; `deploy/deploy.ps1` — build and install into the game; `docs/check-translations.py` — code spans and numbers of every X.md against X.ru.md; `oe-recon/collect.ps1` |

Verification on real scripts:
* the user's ERA 2.291 (Russian) with the core mods `WoG`, `Era Erm Framework`, `WoG Rus`, `WoG Scripts`,
  `WoG Scripts Rus`, `WoG Fix Lite`, `ERA Scripts` in Era's priority order, with the WoG Options defaults of the
  installation (96 options on) and WoGification: preprocessing, parsing, a new game + 7 days — **183 scripts,
  0 ERM errors**; what the engine cannot do yet is listed as *unsupported*;
* WoG 3.58f (78 files) and WoG 3.59 (117 files): parsing and running — 0 errors.

**Status:** since session 2 (2026-10-07) the port runs in the real game (Olden Era 0.81.04, BepInEx 6). Verified in
game: day start, object visits, save/load of the WoG state, the map layer, heroes (skills, spells, artifacts, name,
biography, specialty, hired army), towns (buildings, creatures to hire, garrison, owner, name, income), players, mines,
creature types, battles (the battle's stacks and the triggers !?BR, !?BG, !?MF), WoGification and the WoG Options
defaults — each step with its evidence is in `MODLOG.md`; what is missing is reported as *unsupported* in
`Compatibility/`.

## Build and tests

Requires the .NET 8 SDK (the libraries target `net6.0` so that they load in BepInEx 6 IL2CPP). The plugins build
against the installed game: set `OLDEN_ERA_DIR` to the Olden Era folder (with BepInEx run once).

```bash
dotnet build WoGOldenEra.sln
dotnet test tests/WoG.Tests

# conformance on real scripts and data (nothing of it is committed to the repository):
ERA_MODS_DIR="/path/to/era-project-eng/Mods" dotnet test tests/WoG.Tests --filter EraCorpus
WOG_SCRIPTS_DIR="/path/to/WoG/Data/s" dotnet test tests/WoG.Tests --filter CorpusTests
ERA_GAME_DIR="/path/to/ERA" dotnet test tests/WoG.Tests   # every picture of the installation, its WoG Options defaults

# ERA: a new game on the headless reference engine + compatibility report.
# The mods go highest priority first — the reverse of ERA's Mods/list.txt.
M="/path/to/ERA/Mods"
dotnet run --project tools/WoG.ErmTool -- run --era "$M/ERA Scripts" "$M/WoG Fix Lite" "$M/WoG Scripts Rus" "$M/WoG Scripts" "$M/WoG Rus" "$M/Era Erm Framework" "$M/WoG"
dotnet run --project tools/WoG.ErmTool -- compat --era > Compatibility/ERM_Compatibility_ERA.md                # table (English)
dotnet run --project tools/WoG.ErmTool -- compat --era --lang ru > Compatibility/ERM_Compatibility_ERA.ru.md   # Russian copy of the table

# WoG 3.58
dotnet run --project tools/WoG.ErmTool -- run "/path/to/WoG/Data/s"
dotnet run --project tools/WoG.ErmTool -- compat > Compatibility/ERM_Compatibility.md                          # table (English)
dotnet run --project tools/WoG.ErmTool -- compat --lang ru > Compatibility/ERM_Compatibility.ru.md             # Russian copy of the table

# documentation: every X.md against its X.ru.md
python tools/docs/check-translations.py --verbose
```

## Installing into the game

1. Install BepInEx 6 IL2CPP be.785 into the Olden Era folder and launch the game once (it generates
   `BepInEx/interop`). Back up the saves and `HeroesOldenEra_Data/StreamingAssets/Core.zip` first.
2. With the game closed: `powershell -File tools/deploy/deploy.ps1 [-DebugMode]` builds both plugins and installs them
   into `BepInEx/plugins/WoG/`, with the game symbols (`BepInEx/config/wog_symbols.json`), the id tables
   (`BepInEx/config/WoG/id-maps/`) and the WoG Debug mod; `-DebugMode` turns WoG Debug on.
3. Settings, `BepInEx/config/wog.oldenera.cfg`: `[ERA] ModsRoot` — the `Mods` folder of your ERA installation (its
   `list.txt` gives the mods and their order), `Language` (`ru`); `[ERM] Dialect` — `Era` (default) or `Wog`;
   `[WoG] Wogify` — WoGification (-1 = as in the WoG Options); `[Debug] Enabled` — WoG Debug.
   `BepInEx/config/wog.oldenera.debugui.cfg`: `[Window] Hotkey` — the WoG Debug window's key (F8; F9 is the game's
   quick load).
4. Log: `BepInEx/LogOutput.log` (lines `WoG …`, `[ERM] …`).

Single-player only. Game files are not distributed: everything taken from ERA/WoG/H3/Olden Era is read from the
user's own installations.

## Documentation map

Every document exists in two languages: the English original `X.md` (primary) and a synchronized Russian copy
`X.ru.md` next to it. Code comments are in English.

* `ERA_ReverseEngineering/` — 00 overview and decision · 01 ERM 2.0 preprocessor · 02 ERA semantics · 03 events,
  mod loading, translations, ERT, saving.
* `WoG_ReverseEngineering/` — 00 sources · 01 ERM language · 02 triggers · 03 receivers · 04 commanders ·
  05 stack experience · 06 WoG Options · 07 battle/map/towns/features · 08 saving.
* `OldenEra_ReverseEngineering/` — 00 sources and verification tags · 01 engine · 02 `Core.zip` data ·
  03 scenario scripting · 04 buffs and battle · 05 saves · 06 what is available · 07 in-game verification plan.
* `Compatibility/` — architecture · matrix · ERM by command (WoG and ERA) · `id-maps/` · `options-defaults.json`.
* `MODLOG.md` — work log; `HANDOFF.md` — how to continue the work; `TRANSLATION_STATUS.md` — the state of the
  bilingual documentation.
