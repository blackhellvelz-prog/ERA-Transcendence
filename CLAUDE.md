**English** | [Русский](CLAUDE.ru.md)

# Instructions for Claude Code in this repository

## Project

**ERA:Transcendence** — the working name of the port (use it everywhere: documents, the plugin, the repository; the
repository is `ERA-Transcendence` because GitHub does not allow ':' in names).

A port of **HoMM3 ERA** (WoG 3.58f + Era 3.9.31 engine + ERM 2.0 + ERA Project scripts) to **Heroes of Might and
Magic: Olden Era** (Unity IL2CPP, BepInEx 6). The user's full assignment and all decisions are in `MODLOG.md`,
the current state and plan are in `HANDOFF.md`, and the overview is in `README.md`. Read these three files at the
start of work; a new agent starts with `NEXT_AGENT.md` (the handoff, the measured backlog and the task).

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
* Do not assume Olden Era capabilities — verify them (tags `[V-code] [V-data] [V-community] [V-game] [UNVERIFIED]`).
  Do not assume ERA/WoG behavior — read the sources (`ethernidee/era`, `GrayFace/wog`) and check against the
  script corpus.
* Assets: reuse Olden Era assets, recolor before modeling; placeholders are allowed;
  new 3D models only when there is no way around them.
* Single-player only. No anti-cheat bypassing. Game files (ERA/WoG/H3/Olden Era) and derived data must
  **not be committed** or distributed — the port reads them from the user's installation.
* Before changes in the game folder: back up saved games and `HeroesOldenEra_Data/StreamingAssets/Core.zip`.
  Before installing a loader (BepInEx) or changing game settings — **ask the user**.
* The user's ERA installation is a read-only corpus: never write into it. Launching it to watch how a feature works
  is allowed (the user's permission, 2026-10-08).
* In the running game: never send F9 (the game's quick load); do not close or click into the game while the user
  may be playing — check first; deploy only with the game closed.
* Every notable finding and failure goes into `MODLOG.md` (what, why, how it was verified, how to roll it back).

## Code

| Where | What |
|-----|-----|
| `src/WoG.Core` | state (`WoGGameState`, `EraState`), model, options and the WoG Options dialog items (`WoGOptionSetup`), events, saving, IdMap, H3 pictures (`H3Data/`), adapter interfaces |
| `src/WoG.Erm` | ERM: `Syntax/` parser (WoG and `ErmParserEra.cs`), `Era/` ERM 2.0 preprocessor, events, load order; `Runtime/` interpreter (`EraProcess.cs`, `EraValues.cs` — ERA mode); `Receivers/` receivers (`EraReceivers.cs`, `EraApi.cs`) |
| `src/WoG.Commanders`, `src/WoG.CreatureExperience` | commanders and stack experience (port of `npc.cpp`, `crexpo.cpp`) |
| `src/WoG.Host` | `WoGHost`: ties everything together; `AddEraMods` + `StartNewGame/SaveTo/LoadFrom`; WoGification (`Wogification.cs`), the battle triggers (`BattleActionTracker` in WoG.Core) |
| `src/WoG.Headless` | in-memory headless reference engine for tests |
| `src/WoG.OldenEra` | BepInEx 6 IL2CPP plugin: game symbols from `wog_symbols.json`, adapter, Harmony hooks, the WoG Debug command bridge |
| `src/WoG.OldenEra.DebugUI` | the interface plugin (uGUI from the game's own sprites/fonts): WoG's questions as game dialogs, the WoG Debug window (`FeatureCatalog.cs` — every feature gets a tile there); compiles against Unity interop of the installed game, empty without it |
| `src/WoG.OldenEra.Data` | `Core.zip` overlay (unit clones, buffs, localization) |
| `tools/WoG.ErmTool` | `parse`, `run [--era]`, `compat [--era]`, `era-pp`, `probe-symbols` |
| `tools/deploy/deploy.ps1` | builds both plugins and installs them into the game (`-DebugMode` turns WoG Debug on); the game must be closed |
| `tools/docs/check-translations.py` | code spans and numbers of every `X.md` against its `X.ru.md` |
| `tools/debug/bridge.sh` | runs one WoG Debug command in the running game (`OLDEN_ERA_DIR`) |
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
ERA_GAME_DIR="/path/to/ERA" dotnet test tests/WoG.Tests   # the user's ERA installation: every picture, the WoG Options defaults
# ERA: the mods highest priority first (the reverse of Mods/list.txt); prints "N scripts, E ERM errors, K WoG options on"
M="/path/to/ERA/Mods"
dotnet run --project tools/WoG.ErmTool -- run --era "$M/ERA Scripts" "$M/WoG Fix Lite" "$M/WoG Scripts Rus" "$M/WoG Scripts" "$M/WoG Rus" "$M/Era Erm Framework" "$M/WoG"
dotnet run --project tools/WoG.ErmTool -- compat --era > Compatibility/ERM_Compatibility_ERA.md
dotnet run --project tools/WoG.ErmTool -- compat --era --lang ru > Compatibility/ERM_Compatibility_ERA.ru.md
python tools/docs/check-translations.py --verbose
```

Before committing: the build has no warnings, all tests are green, the ERA corpus prints `0 ERM errors` (read the
summary line; errors print as `Error …`). The `Compatibility/ERM_Compatibility*.md` tables are generated — edit
`Declare(...)` in the receivers, not the tables (each new note needs its Russian text in `ReceiverRegistry.RussianNotes`).

## Git

Working branch: `claude/wog-olden-era-port` (`origin` = `github.com/blackhellvelz-prog/ERA-Transcendence`).
Do not create a PR until the user asks for one.
