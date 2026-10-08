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
| Olden Era reverse engineering (`OldenEra_ReverseEngineering/`) | from working community mods/tools; every fact is tagged with its verification level; an in-game verification plan |
| Compatibility matrix (`Compatibility/`) | for every feature: strategy, status, limitations; the ERM tables (WoG and ERA) are generated from code |
| Core (`src/WoG.Core`) | state, model, options, events, saving (JSON + SHA-256), ERA state (`EraState`), IdMap, visual resolver |
| ERM (`src/WoG.Erm`) | parser and interpreter for WoG 3.58/3.59 **and ERA**: ERM 2.0 preprocessor, ERA parameters/conditions/control flow/functions, ERA `VR`/`FU`/`DO`/`SN`, translations, ERT |
| Commanders (`src/WoG.Commanders`) | the `npc.cpp` tables and formulas: levels, skills, special bonuses, artifacts, hiring/resurrection, battle profile |
| Stack experience (`src/WoG.CreatureExperience`) | ranks, gaining experience after battle, merging, bonuses, `CREXPMOD/CREXPBON.TXT` loaders |
| Olden Era plugin (`src/WoG.OldenEra`) | BepInEx 6 IL2CPP, builds against the real API; game symbols come from a config; everything unverified is disabled and reports *unsupported* |
| WoG Debug window (`src/WoG.OldenEra.DebugUI`) | an in-game ERM and debug-command console (F9 or the "WoG" button), built from Olden Era's own window frame, buttons, fonts and scrollbar; only with WoG Debug on |
| Data overlay (`src/WoG.OldenEra.Data`) | reading `Core.zip`, unit clones, experience-rank buffs, localization in the OE format |
| Tests (`tests/WoG.Tests`) | 187 xUnit tests; the corpus tests run the real WoG and ERA scripts |
| Tools (`tools/`) | `WoG.ErmTool`: `parse`, `run`, `compat`, `era-pp`, `probe-symbols`; `oe-recon/collect.ps1` — collects data about an Olden Era installation |

Verification on real scripts:
* ERA Project 2.291 (the mods `Era Erm Framework`, `ERA Scripts`, `WoG Scripts`, `WoG`; 183 scripts, 37,958 lines
  of commands): preprocessing, parsing and running a new game + 7 days — **0 errors**;
* WoG 3.58f (78 files) and WoG 3.59 (117 files): parsing and running — 0 errors.

**Status:** since session 2 (2026-10-07) the port runs in the real game (Olden Era 0.81.04, BepInEx 6): day start,
object visits, battles, save/load of the WoG state, the map layer, heroes (skills, spells, artifacts), towns (buildings,
creatures to hire, garrison, owner, name), creature types and more are verified in game — each step with its evidence is in `MODLOG.md`; what is missing is reported as
*unsupported* in `Compatibility/`.

## Build and tests

Requires the .NET 8 SDK (the libraries target `net6.0` so that they load in BepInEx 6 IL2CPP).

```bash
dotnet build WoGOldenEra.sln
dotnet test tests/WoG.Tests

# conformance on real scripts (the scripts are not committed to the repository):
ERA_MODS_DIR="/path/to/era-project-eng/Mods" dotnet test tests/WoG.Tests --filter EraCorpus
WOG_SCRIPTS_DIR="/path/to/WoG/Data/s" dotnet test tests/WoG.Tests --filter CorpusTests

# ERA: a new game on the headless reference engine + compatibility report (mods from highest to lowest priority)
M="/path/to/era-project-eng/Mods"
dotnet run --project tools/WoG.ErmTool -- run --era "$M/Era Erm Framework" "$M/ERA Scripts" "$M/WoG Scripts" "$M/WoG"
dotnet run --project tools/WoG.ErmTool -- era-pp /tmp/era-pp "$M/Era Erm Framework" "$M/ERA Scripts" "$M/WoG Scripts" "$M/WoG"
dotnet run --project tools/WoG.ErmTool -- compat --era > Compatibility/ERM_Compatibility_ERA.md                # table (English)
dotnet run --project tools/WoG.ErmTool -- compat --era --lang ru > Compatibility/ERM_Compatibility_ERA.ru.md   # Russian copy of the table

# WoG 3.58
dotnet run --project tools/WoG.ErmTool -- run "/path/to/WoG/Data/s"
dotnet run --project tools/WoG.ErmTool -- compat > Compatibility/ERM_Compatibility.md                          # table (English)
dotnet run --project tools/WoG.ErmTool -- compat --lang ru > Compatibility/ERM_Compatibility.ru.md             # Russian copy of the table
```

## Installing into the game (for the verification stage)

1. Install BepInEx 6 IL2CPP be.785 into the Olden Era folder and launch the game once.
2. Copy `src/WoG.OldenEra/bin/.../WoG.*.dll` to `BepInEx/plugins/WoG/`.
3. The first launch creates `BepInEx/config/wog_symbols.json` (a template) — fill it in according to the RE plan.
4. Scripts go into `BepInEx/config/WoG/scripts/`; id tables go into `BepInEx/config/WoG/id-maps/`.
   (Loading whole ERA mods through `WoGHost.AddEraMods` in the plugin is the next step.)
5. Log: `BepInEx/LogOutput.log` (lines `WoG …`, `[ERM] …`, `Game API not found: …`).

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
* `MODLOG.md` — work log; `HANDOFF.md` — how to continue the work on your own computer.
