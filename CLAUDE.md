# Instructions for Claude Code in this repository

## Project

A port of **HoMM3 ERA** (WoG 3.58f + Era 3.9.31 engine + ERM 2.0 + ERA Project scripts) to **Heroes of Might and
Magic: Olden Era** (Unity IL2CPP, BepInEx 6). The user's full assignment and all decisions are in `MODLOG.md`,
the current state and plan are in `HANDOFF.md`, and the overview is in `README.md`. Read these three files at the
start of work.

Work follows the `mod-any-game` skill (located in `.claude/skills/mod-any-game`, from
`rehan-remade/universal-modder`): recon → cheapest route → primary sources → vertical slice →
oracle check → log.

## User rules (mandatory)

* **Documentation: English is primary (`X.md`); a Russian copy lives next to it (`X.ru.md`)** and must be kept in
  sync — update both in the same change. Code comments are English. Generated tables
  (`Compatibility/ERM_Compatibility*.md` and their `.ru.md` twins) come from
  `WoG.ErmTool compat [--era] [--lang ru]`.
* Functionality first, graphics later. Do not fake gameplay: whatever is missing is honestly `Unsupported` in the
  compatibility report; never "pretend" anything.
* Do not assume Olden Era capabilities — verify them (tags `[V-code] [V-data] [V-community] [UNVERIFIED]`).
  Do not assume ERA/WoG behavior — read the sources (`ethernidee/era`, `GrayFace/wog`) and check against the
  script corpus.
* Assets: reuse Olden Era assets, recolor before modeling; placeholders are allowed;
  new 3D models only when there is no way around them.
* Single-player only. No anti-cheat bypassing. Game files (ERA/WoG/H3/Olden Era) and derived data must
  **not be committed** or distributed — the port reads them from the user's installation.
* Before changes in the game folder: back up saved games and `HeroesOldenEra_Data/StreamingAssets/Core.zip`.
  Before installing a loader (BepInEx) or changing game settings — **ask the user**.
* Every notable finding and failure goes into `MODLOG.md` (what, why, how it was verified, how to roll it back).

## Code

| Where | What |
|-----|-----|
| `src/WoG.Core` | state (`WoGGameState`, `EraState`), model, options, events, saving, IdMap, adapter interfaces |
| `src/WoG.Erm` | ERM: `Syntax/` parser (WoG and `ErmParserEra.cs`), `Era/` ERM 2.0 preprocessor, events, load order; `Runtime/` interpreter (`EraProcess.cs`, `EraValues.cs` — ERA mode); `Receivers/` receivers (`EraReceivers.cs`, `EraApi.cs`) |
| `src/WoG.Commanders`, `src/WoG.CreatureExperience` | commanders and stack experience (port of `npc.cpp`, `crexpo.cpp`) |
| `src/WoG.Host` | `WoGHost`: ties everything together; `AddEraMods` + `StartNewGame/SaveTo/LoadFrom` |
| `src/WoG.Headless` | in-memory headless reference engine for tests |
| `src/WoG.OldenEra` | BepInEx 6 IL2CPP plugin: game symbols from `wog_symbols.json`, adapter, Harmony hooks |
| `src/WoG.OldenEra.Data` | `Core.zip` overlay (unit clones, buffs, localization) |
| `tools/WoG.ErmTool` | `parse`, `run [--era]`, `compat [--era]`, `era-pp`, `probe-symbols` |
| `tools/oe-recon/collect.ps1` | collects data about the Olden Era installation (read-only) |
| `tools/fetch-references/` | downloads reference sources into `../research` |

ERA mode: `ErmRuntimeOptions.Dialect = ErmDialect.Era`. The principle is a line-by-line port of Era functions
(`PreprocessErm`, `Hook_ZvsGetNum`, `ProcessErm`, `VR_*`, `SN_*`) that preserves their bugs; each ported piece is
labeled with the name of the original function in a comment.

## Commands

```bash
dotnet build WoGOldenEra.sln
dotnet test tests/WoG.Tests
# corpora (after tools/fetch-references):
ERA_MODS_DIR=../research/era-eng/Mods dotnet test tests/WoG.Tests --filter EraCorpus
WOG_SCRIPTS_DIR="../research/wogify/Mods/WoG Wogify Scripts 3.58f/Data/s" dotnet test tests/WoG.Tests --filter CorpusTests
M=../research/era-eng/Mods
dotnet run --project tools/WoG.ErmTool -- run --era "$M/Era Erm Framework" "$M/ERA Scripts" "$M/WoG Scripts" "$M/WoG"
dotnet run --project tools/WoG.ErmTool -- compat --era > Compatibility/ERM_Compatibility_ERA.md
dotnet run --project tools/WoG.ErmTool -- compat --era --lang ru > Compatibility/ERM_Compatibility_ERA.ru.md
```

Before committing: the build has no warnings, all tests are green, the ERA corpus runs without errors. The
`Compatibility/ERM_Compatibility*.md` tables are generated — edit `Declare(...)` in the receivers, not the tables.

## Git

Working branch: `claude/wog-olden-era-port` (`origin` = `github.com/blackhellvelz-prog/ModsClaudeVelz`).
Do not create a PR until the user asks for one.
