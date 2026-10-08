**English** | [Русский](NEXT_AGENT.ru.md)

# ERA:Transcendence — handoff to the next agent

This is the entry point for the Claude Code instance that continues the port. Read it first, then `CLAUDE.md`,
`HANDOFF.md`, `README.md` and the latest entries of `MODLOG.md`. The task you are given is at the end of this file
(**Appendix: the task**); everything before it is the project's actual state and what you need to work on it.

## 1. How to start

* **From the archive:** unpack it; `ERA-Transcendence/` is the repository with its full git history, `origin`
  already points to `https://github.com/blackhellvelz-prog/ERA-Transcendence`, branch `claude/wog-olden-era-port`.
* **From GitHub:** `git clone -b claude/wog-olden-era-port https://github.com/blackhellvelz-prog/ERA-Transcendence`.
* Open the folder in Claude Code. `CLAUDE.md` (rules) and the `mod-any-game` skill (`.claude/skills/`) load from it.
* Reference sources (Era, WoG, ERA Project, universal-modder) are not in the repository:
  `tools/fetch-references/fetch-references.ps1` downloads them into `../research`. The previous agent also used
  `../research/ermhelp` (the ERM help, unpacked), `../research/oe-decomp` (Olden Era IL2CPP interop decompiled with
  ILSpy) and `../research/wog/T1` (GrayFace/wog, WoG 3.59 alpha sources — WoG 3.58's code with additions).

First message you can give yourself: *"Read NEXT_AGENT.md, CLAUDE.md, HANDOFF.md, MODLOG.md (last entries). Build,
run the tests and the ERA corpus, then start with the backlog in NEXT_AGENT.md section 6."*

## 2. State at handoff (2026-10-08)

* 39 commits on `claude/wog-olden-era-port`; the build has 0 warnings; **279 xUnit tests** green.
* ERA corpus (the user's ERA 2.291, core mods in Era's order): **183 scripts, 0 ERM errors, 96 WoG options on
  (WoGified)**. The remaining *unsupported* calls are listed in section 6.
* Runs in the real game since 2026-10-07: Olden Era 0.81.04 (Unity 6000.0.66f1, IL2CPP), BepInEx 6 IL2CPP be.785.
* **Verified in the game** `[V-game]` (evidence in `MODLOG.md`): day start (OnEveryDay, timers; day 1; a day runs
  once per WoG state across saves), object visits (`!?OB`/`!$OB`), save/load of the WoG state next to Olden Era saves,
  the map layer (size, squares, objects, OB/TR/PO), heroes (HE:E/F/I/W/O/S/M/A/B/X/H/C/P; experience through the
  game's level-up; sub-skill choice through the game's own window), H3 pool heroes for numbers without an Olden Era
  hero, towns (CA: buildings, construction, owner, name, dwellings, garrison, income), players (OW incl. absent
  players 0..7), mines (MN), creature types (MA on `UnitLogicConfig`), battles (BA, BM on battle units, `!?BR`,
  `!?BG0/1` for walk/attack/shot/wait/defend/hero spell, `!?MF1`), WoGification (ERA rules) with the installation's
  WoG Options defaults, WoG's mithril, Era Erm Framework's artifact functions, Olden Era's experience table, the WoG
  Debug window (F8) and the command bridge.
* Headless only (not yet in the game): most of ERM/ERA semantics (all corpus scripts run on the reference engine),
  commanders and stack experience as state/formulas (not in Olden Era battles yet), SS (spells: read, changes kept).

## 3. Projects (do not duplicate any of them)

| Project | What it is |
|---|---|
| `src/WoG.Core` | state (`WoGGameState` — everything saved), model, options + WoG Options dialog items (`WoGOptionSetup`), events, save (JSON + SHA-256), IdMap, adapter interfaces (`Adapters/`: `IGameAdapter`, `HeroRecords`, `PoolHeroAdapter`), H3 data (`H3Data/`: LOD/PAC, Era's VFS, text tables, DEF/PCX pictures, `ExeSpellTable`) |
| `src/WoG.Erm` | ERM: parser (WoG + `ErmParserEra.cs`), ERM 2.0 preprocessor (`Era/`), runtime (`Runtime/`, `EraProcess.cs`), receivers (`Receivers/`; every command declares its status with `Declare(...)`), Era native library (`Era/EraNativeLibrary.cs`) |
| `src/WoG.Host` | `WoGHost`: mods in Era's order, WoGification (`Wogification.cs`), new game/save/load, events → triggers |
| `src/WoG.Headless` | the reference engine (H3 behaviour in memory) — the oracle for tests |
| `src/WoG.Commanders`, `src/WoG.CreatureExperience` | `npc.cpp` / `crexpo.cpp` ports |
| `src/WoG.OldenEra` | the BepInEx plugin: `OldenEraGameAdapter` (all engine access through symbols), `OldenEraSymbols` + `symbols/wog_symbols.json` (verified symbol registry), Harmony hooks, `BattleEventBridge`, `SaveSync`, `DebugSupport` (command bridge) |
| `src/WoG.OldenEra.DebugUI` | the interface plugin: game-style dialogs (WoGify question), the WoG Debug window, `FeatureCatalog.cs` (a tile per feature) |
| `src/WoG.OldenEra.Data` | `Core.zip` overlay (unit clones, buffs, localization) |
| `tools/WoG.ErmTool` | `run [--era]` (corpus), `compat [--era] [--lang ru]` (generated tables), `parse`, `era-pp`, `probe-symbols` |
| `tools/deploy/deploy.ps1` | builds and installs both plugins (`-DebugMode`); the game must be closed |
| `tools/debug/bridge.sh` | runs one WoG Debug command in the running game |
| `tools/docs/check-translations.py` | code spans and numbers of every `X.md` against `X.ru.md` |

## 4. The user's machine (if you run there)

* Olden Era: `G:\SteamLibrary\steamapps\common\Heroes of Might and Magic Olden Era` (Steam app 3105440).
* The user's ERA (read-only corpus, **never write into it**; launching it to watch a feature is allowed):
  `G:\Games\Тень Смерти` (ERA 2.291 Russian; `Mods\list.txt` lists mods lowest priority first; `h3era.exe`).
* .NET 8 SDK is user-local: `DOTNET_ROOT=%USERPROFILE%\.dotnet8` and PATH; `OLDEN_ERA_DIR=<Olden Era folder>` for
  the plugin build; `ERA_GAME_DIR=G:\Games\Тень Смерти` for the installation tests.
* The corpus command (mods highest priority first, the reverse of list.txt):
  ```bash
  M="G:/Games/Тень Смерти/Mods"
  dotnet run --project tools/WoG.ErmTool -- run --era "$M/ERA Scripts" "$M/WoG Fix Lite" "$M/WoG Scripts Rus" "$M/WoG Scripts" "$M/WoG Rus" "$M/Era Erm Framework" "$M/WoG"
  ```
  Read the summary line (`N scripts, E ERM errors, K WoG options on`); errors print as `Error …`.
* In the game the plugin loads the user's **whole** ERA mod list (also AMER_HumanAI, Game Enhancement Mod…), not
  only the seven core mods.
* Game automation (universal-modder `um`): `um win launch --steam 3105440`; wait until `bridge.sh "ui canvases"`
  shows `ScMainMenu`; clicks are in the 1600×900 frame of `um win shot --scale 0.5`:
  `um win drive --proc HeroesOldenEra "focus" "click x y"`. New map: Новая игра (195,250) → Быстрый старт (195,296) →
  Шаблоны (800,400) → ARCADE (913,285) → Выбрать (1290,800) → faction (515,290) → Выбрать → hero (465,205) → Выбрать →
  Начать игру (800,821). Load from the main menu: Загрузить (195,305) → Загрузить (1258,824). In a game: ≡ (1558,17) →
  Загрузить (798,364); end turn: hourglass (1520,828) → Принять (695,486). Keys are hex VK codes (`key 0x77` = F8).
* **Never send F9** (Olden Era's quick load). Never click the BepInEx console (QuickEdit freezes the game; deploy turns
  it off). Kill only by exact PID (`um win kill <pid>`). The user may be playing: check before closing the game or
  clicking into it.
* Never trace whole classes or empty virtual methods with `[Debug] TraceMethods`: IL2CPP folds identical bodies and
  the game crashes at start.

## 5. The working loop that worked

1. Read the primary source (WoG `erm.cpp`/`spell.cpp`/…, Era `Erm.pas`, the ERM help, the script corpus) — tag facts.
2. Implement in the semantic layer (WoG.Erm/WoG.Core) + the headless engine; xUnit test against the source.
3. Olden Era: find the engine's equivalent (Core.zip data, `oe-decomp`, `bridge.sh "peek …"`), add the symbol to
   `wog_symbols.json` as `verified` only after checking it in the game, implement it in `OldenEraGameAdapter`.
4. `dotnet build WoGOldenEra.sln` (0 warnings), `dotnet test tests/WoG.Tests`, the corpus (0 ERM errors).
5. Game closed → `powershell -ExecutionPolicy Bypass -File tools/deploy/deploy.ps1 -DebugMode` → launch → check
   through the bridge and screenshots; read `BepInEx/LogOutput.log` (`[ERM] Error …` and `[unsupported] …`).
6. A tile in `FeatureCatalog.cs`; `Declare(...)` notes (+ Russian in `ReceiverRegistry.RussianNotes`) → regenerate
   `Compatibility/ERM_Compatibility*.md` with `WoG.ErmTool compat`; matrix rows; `MODLOG.md` + `MODLOG.ru.md`.
7. Commit (`git -c user.name="Claude" -c user.email="noreply@anthropic.com" commit`, ending with the
   `Co-Authored-By` line); the user wants GitHub kept up to date — push the branch after a verified wave; no PRs.

The user writes in Russian and wants the answers in Russian; documentation is English (`X.md`) with a synchronized
Russian copy (`X.ru.md`) in the same change; code comments are English.

## 6. Remaining work, measured (start here)

**Corpus, core mods, headless** — unsupported calls (runtime count; distinct script sites in brackets):

| Item | Count | Where | Next step |
|---|---|---|---|
| `UN:C` (H3 memory) | 1291 | enhanced secondary skills (skill effect tables, e.g. Resistance at 6548044), wogify (spell schools via the spell table), the stdlibs of WoG/Era/ERA Scripts, towns may be renamed | an address registry with *semantic* accessors per known address (spell table → `SS` data; skill tables → Olden Era sub-skill configs; …); unknown addresses stay unsupported with the address in the reason |
| `SN:H` hints | 124 | secondary skill text (30 sites), enhanced secondary skills, cards of prophecy, market of time | keep hints in the WoG state; show them through Olden Era's tooltips (RE the tooltip source) |
| `UN:I` place object | 23 | wogify (12 sites) | Olden Era map object creation (RE needed) — the base of WoGification's transformations |
| `HT:W/P/T/V` | 19/19/6/2 | magic mushrooms, potion fountains, fishing well, artificer, palace of dreams, school of wizardry… | object hint texts (same UI path as SN:H) |
| `UN:B` | 10 | wogify, mithril enhancements, chests | object/resource changes on the map |
| `SN:M` address + `SN:F^Erm_FillInt32Array^` | 7 + 7 | Era stdlib `Array_Fill` (used by wogify:316, ERA Scripts option 757) | **smallest next step:** native `Array_Fill` / `Array_Revert` in `EraNativeLibrary` like `Array_Merge` |
| `IF:D/E/F/G` | 2 each | wogify (setup dialog), map rules | custom dialogs in the interface plugin; `IF:M/IF:Q` need ERM suspension (resume the exact ERM state after the answer, never block the game thread) |
| `SN:L/B/E` | 3/1/1 | stdlib DLL calls, enhanced commanders | per call: what it computes, then a native equivalent |

**In the game (the user's whole mod list)** additionally: `DW:M` (dwellings with upgraded creatures), `CB:A` (creature
banks), `MO:G/A` (map monsters), `AR:V/M` (map artifacts), `EA:F/B` (GEM), `CH:B` (adventure cave), `BA:M` (battle
armies), `TR:G` (terrain overlays), `UN:J1` with a non-zero limit (enforce through the experience logic's last level
`dyx.chmo`), `HE:X` for Olden Era-only specialties.

**Already designed, not finished:** `SS` changes → Olden Era magic configs (`DB/magics/*.json`: `rank`,
`manaCost[4]`; spells map by effect in `Compatibility/id-maps/spell.json`); the WoG Options window (the dialog items
are loaded: `WoGHost.OptionSetup`, values applied at a new map); commanders and stack experience inside Olden Era
battles (buff generator exists; commander as a unit not started); battle receivers `BU/BF/BH/MR` and a coordinate
translation layer (H3 17×11 ↔ Olden Era's field); WoGification's actual map transformations (`78 wog - wogify.erm`
needs UN:I/UN:B/UN:U/OB first); map scripts for Olden Era maps (the ERA rules are in `Wogification.Plan`).

## 7. Facts that cost hours (keep them)

* Hero numbers: Olden Era's heroes get WoG numbers 0..155 for a whole game (their OE id when free); numbers without
  one are H3 pool heroes kept in `WoGGameState.PoolHeroes` (names from the installation's hotraits.txt).
* Olden Era's autosave is taken **before** its day starts and loading it starts the day; a mid-day save does not —
  `WoGGameState.DayStarted` makes a day run once per WoG state.
* The live experience table is the hero logic's `dyx.chmm` (50 levels of `exp_standard`), not Core.zip's file.
* The H3 spell table: sptraits.txt rows i+5 / i+8 / i+11; target/animation/flags from `h3era.exe` at 0x6854A0.
* Era patches WoG's parameter checks in places (TR:T takes any number of parameters, missing ones ignored); check
  `Erm.pas`'s hook list (around line 9380) before calling a parameter count an error.
* WoG Options defaults: WoG Scripts' own ZSETUP01.TXT (231 items) over WoG's (19) through Era's VFS, plus the mods'
  `.ers` files; radio groups and the inverted options 1..4 follow `wogsetup.cpp` `Prepare2Close`.
* Battle events of Olden Era (`eor.clgz.cksd`): round start `ckso`, turn start `cksj`, move `cktd`, cast/attack
  `ckts`/`cktt`, wait `cksw`, skip (= defend) `cksv`, hero magic `cktn`/`cktm`, damage `cksq`.
* The ERA corpus run must list mods highest priority first; the H3 data then comes from the right mod (WoG Scripts'
  ZSETUP01 over WoG's).

---

## Appendix: the task (from the user, verbatim)

ERA:Transcendence — COMPLETE THE PORT

Repository: https://github.com/blackhellvelz-prog/ERA-Transcendence

Branch: `claude/wog-olden-era-port`

You are the primary implementation agent.

Your task is to continue the existing project and bring it as close as technically possible to a complete
functional port of Heroes III ERA, including:

* WoG 3.58f
* ERA 3.9.31
* ERM 2.0
* ERA Project runtime behavior
* the core ERA Project mods
* WoG scripts
* ERA scripts
* WoG Scripts
* WoG Fix Lite
* WoG Rus / WoG Scripts Rus where relevant
* Era Erm Framework

The target engine is: Heroes of Might and Magic: Olden Era

Environment:

* Unity IL2CPP
* BepInEx 6
* Harmony
* single-player
* no anti-cheat bypassing

The repository is the source of truth for the current port state.

### 0. NON-NEGOTIABLE PRINCIPLE

DO NOT restart the project. DO NOT replace working systems with cleaner-looking rewrites. DO NOT create duplicate
subsystems. DO NOT throw away existing reverse engineering. DO NOT implement hypothetical Olden Era APIs. DO NOT mark
something Unsupported merely because the original Heroes III implementation used raw memory.

The target is: semantic compatibility, not Heroes III memory-layout compatibility.

When a Heroes III operation was:

* a raw memory access,
* a pointer operation,
* an internal H3 function,
* an executable callback,
* a closed-source ERA plugin function,

reproduce its observable behavior through:

1. native Olden Era API,
2. Olden Era runtime hook,
3. semantic adapter,
4. semantic reimplementation,
5. emulation,
6. virtualization,

in that order where practical.

Only classify something as truly unsupported after all realistic semantic approaches have been investigated.

### 1. FIRST: AUDIT THE CURRENT HEAD

Before changing architecture, inspect the repository as it currently exists.

Read:

* CLAUDE.md
* CLAUDE.ru.md
* HANDOFF.md
* HANDOFF.ru.md
* MODLOG.md
* MODLOG.ru.md
* Compatibility/
* WoG_ReverseEngineering/
* src/
* tests/
* tools/

Inspect the latest commit history. Do not rely on old task descriptions. The current repository already contains
substantial working code.

Identify and preserve at minimum:

* WoG.Core
* WoG.Erm
* WoG.Host
* WoG.Headless
* WoG.OldenEra
* WoG.OldenEra.DebugUI
* WoG.Commanders
* WoG.CreatureExperience
* WoG.ErmTool
* H3 data/VFS layer
* map adapter
* object mapping
* event translation
* save/load
* debug infrastructure
* WoG Options infrastructure
* WoGification infrastructure

Record what is:

* DONE
* PARTIAL
* EMULATED
* VIRTUALIZED
* HOOKED
* REIMPLEMENTED
* UNKNOWN
* UNSUPPORTED

Do not change behavior during this audit.

### 2. DEFINE "COMPLETE"

The objective is NOT simply: 183 scripts run without ERM parser errors. That is only one milestone.

Completion means that the target ERA/WoG ecosystem can actually use its functionality in the Olden Era runtime.

The completion target is:

```
ERA / WoG
        ↓
ERM 2.0 semantics
        ↓
ERA runtime
        ↓
WoG/ERA script ecosystem
        ↓
semantic Olden Era adapters
        ↓
native Olden Era gameplay/UI/data
```

The port should support:

WoG

* WoG 3.58f ERM behavior
* WoG triggers
* WoG receivers
* WoG global scripts
* WoG map rules
* WoGification
* WoG options
* WoG objects
* WoG enhanced systems
* WoG resources/state
* WoG battle mechanics
* commanders
* stack experience
* creature enhancements
* artifact enhancements
* secondary skill enhancements
* adventure-map features
* map scripts
* dialogs/messages
* save/load state

ERA

* ERM 2.0 preprocessing
* ERA syntax/grammar
* ERA local variables and scoping
* ERA function semantics
* ERA load order
* ERA event semantics
* ERA native library functions
* ERA scripts
* ERA Script Framework behavior used by the corpus
* ERA Project compatibility behavior
* plugin-backed behavior where functionally required

### 3. BUILD A COMPLETE FEATURE INVENTORY

Do not manually guess what remains. Generate an exhaustive inventory from the actual reference material.

Use:

* GrayFace/wog
* WoG 3.58f sources
* ethernidee/era
* ERA Project sources
* Erm.pas
* Triggers.pas
* AdvErm.pas
* erm.cpp
* npc.cpp
* crexpo.cpp
* WoG help
* WoGify corpus
* ERA Project scripts
* the repositories already downloaded by tools/fetch-references

Analyze the actual script corpus.

Inventory:

Triggers — all relevant:

* `!?XX`
* `!$XX`
* timed triggers
* map triggers
* battle triggers
* hero triggers
* town triggers
* save/load triggers
* interface triggers
* plugin/ERA-specific triggers

Receivers — inventory ALL receivers and operations:

* UN
* SN
* HE
* OW
* CA
* OB
* PO
* MA
* MN
* BM
* BA
* BG
* BF
* BH
* BU
* MR
* IF
* HT
* DL
* FU
* VR
* `!!`
* all ERA extensions
* all relevant plugin functions

Do not stop at the functions currently used at day 1.

Create a matrix: Function · Source · Used by scripts · Current implementation · Olden Era equivalent ·
Implementation method · Verification level · Tests · Remaining work.

### 4. USE THE CORPUS AS THE ACCEPTANCE TEST

The project already has the most important oracle: the real ERA corpus. Run the corpus in correct ERA mod order.

The target is:

* 183+ scripts
* 0 runtime errors
* 0 parser/preprocessor errors
* 0 accidental unsupported calls in exercised functionality

But also measure:

* number of executed sections
* number of receiver calls
* number of unsupported operations
* number of fallback operations
* number of semantic mismatches
* number of no-op implementations
* number of game-verified operations

A call that silently does nothing is NOT success.

### 5. ELIMINATE UNSUPPORTED SYSTEMATICALLY

Create a living backlog from `Compatibility/ERM_Compatibility_ERA.md` and every compatibility report.

Sort it by:

1. number of calls
2. importance to core mods
3. gameplay impact
4. dependency count
5. feasibility

Do NOT just reduce the number of Unsupported strings. Each operation must have real semantics.

For each unsupported item: Why does ERA use it? What did H3 actually do? What state changed? What does the player
observe? Can Olden Era represent it? Can it be emulated? Can it be virtualized? Can a hook provide it? What is the
smallest correct implementation?

### 6. FINISH THE ADVENTURE MAP API

The project already has:

* MapData
* map size
* terrain
* map squares
* heroes
* objects
* object type mapping
* UN:X
* UN:U
* OB:T/U
* TR:T/P/E
* PO
* object visit hooks
* mines
* WoGification lifecycle

Now finish the map subsystem. Implement, where semantically possible:

* UN:I
* UN:B
* UN:J
* remaining UN:R
* remaining UN:U
* UN:X
* map object creation
* map object deletion
* map object replacement
* map object modification
* object coordinates
* terrain changes
* object ownership
* map-square state
* map object search
* map object iteration
* map entrance semantics
* object interaction
* special objects
* creature banks
* resource objects
* artifact objects
* town objects
* hero objects
* monster objects
* portals
* WoG objects

Do NOT create a second map representation. Extend the current map adapter.

### 7. FULL WOGIFICATION

This is now a core requirement.

The current `Wogification.cs` correctly implements the map decision/lifecycle logic. Do not replace it. Extend it into
actual map transformation.

The desired flow is:

```
Olden Era map
        ↓
ERA map lifecycle
        ↓
WoGify decision
        ↓
WoGify configuration
        ↓
wogify.erm semantics
        ↓
map object transformations
        ↓
WoG state
        ↓
ERA scripts
        ↓
game start
```

The real WoGify corpus must be used as the semantic reference. Implement the actual effects of
`78 wog - wogify.erm` rather than merely reproducing the yes/no WoGification decision.

Map transformations must operate through the existing semantic map API. Do NOT hard-code hundreds of map changes into
`Wogification.cs`.

Separate:

* lifecycle
* configuration
* transformation
* rules
* object placement
* reporting

### 8. WOG OPTIONS

The project already reads installation WoG Options and applies defaults. Finish the system.

Implement:

* WoG Options state
* default loading
* option inheritance
* reset behavior
* WoG/ERA differences
* map-level state
* script access
* option changes during runtime
* WoG Options UI
* persistence
* compatibility with .ers
* appropriate option semantics

The WoG Options window should use the existing Olden Era UI language. Do not create arbitrary modern UI when the
project already has native-style UI integration.

Each option should be tied to its real ERM option number.

### 9. FULL PO / MAP STATE

The current PO implementation is important. Expand it wherever needed.

Ensure:

* per-square state
* counts
* ownership
* last hero
* custom values
* save/load
* map bounds
* untouched square defaults
* compatibility with WoG Scripts

Never lose PO data after save/load.

### 10. HERO SYSTEM

Finish all relevant hero behavior. Already implemented areas must remain intact.

Complete:

* hero numbers 0..155
* pool heroes
* hired heroes
* name
* biography
* class
* specialty
* starting army
* experience
* level
* primary skills
* secondary skills
* sub-skills
* spells
* mana
* movement
* artifacts
* backpack
* equipment
* army
* ownership
* tavern heroes
* hero ordering
* hero lists
* active/event hero distinctions
* hero creation/deletion where required
* map position
* hero removal
* hero replacement

Pay special attention to: HE:B, HE:E, HE:F, HE:H, HE:M, HE:S, HE:X, HE:A and all other HE operations.

Do not merely store values in WoG state when the native Olden Era hero system can represent them.

### 11. SECONDARY SKILLS

This is a major ERA dependency.

Finish:

* SS:F
* SS:L
* SS:S
* all skill modifiers used by ERA Scripts
* skill level modifications
* skill removal
* skill ordering
* display slots
* skill prerequisites
* enhanced secondary skills
* sub-skills
* native level-up integration
* skill selection UI
* script-driven skill changes

The current native sub-skill selection hook is a major achievement. Extend it rather than bypassing it.

> Note from the previous agent: in the corpus, `SS` is WoG's **spell** receiver (`spell.cpp` `ERM_Spell`; SS:F/L/S
> are a spell's flags, level and schools), implemented on 2026-10-08 — see `MODLOG.md`. The secondary-skill work
> above still applies to HE:S, sub-skills and the enhanced secondary skills script.

### 12. TOWNS

Finish the CA subsystem. Already-supported features must remain compatible.

Complete:

* building queries
* construction
* demolition semantics where possible
* building bans
* dwellings
* creature growth
* hired creatures
* garrison
* heroes
* town ownership
* town names
* town lists
* tavern
* guilds
* spells
* income
* grail
* town type
* town positions
* construction state
* town replacement/creation if required

Where H3 has a feature Olden Era lacks: implement a semantic fallback or virtualization.

### 13. ARTIFACTS AND EQUIPMENT

Complete:

* artifact mapping
* all practical WoG/H3 artifacts
* artifact positions
* worn items
* backpack
* scrolls
* spellbooks
* artifact removal
* artifact duplication/counting
* artifact properties
* artifact restrictions
* combination artifacts
* enhanced artifacts
* WoG artifact behavior
* custom artifacts where needed

Use native Olden Era item logic whenever possible. Do not create duplicate inventory systems.

### 14. CREATURES / MA

Finish creature compatibility.

Complete:

* H3 creature IDs
* WoG creature IDs
* Olden Era creature mapping
* creature statistics
* upgrades
* costs
* growth
* creature abilities
* enhanced creatures
* level 8 creatures
* WoG creatures
* dwellings
* hiring
* neutral armies
* army replacement
* creature creation
* creature deletion
* creature naming
* creature properties

The current UnitLogicConfig bridge is the foundation. Do not replace it.

### 15. COMMANDERS

The commander subsystem already exists. Finish the actual gameplay implementation.

Complete:

* commander state
* commander levels
* commander experience
* commander skills
* commander abilities
* commander equipment
* resurrection
* hiring
* combat entity
* combat modifiers
* special bonuses
* battle UI
* ERM CO

The commander must actually influence combat. Do not leave it as a headless/state-only feature.

### 16. STACK EXPERIENCE

Finish:

* experience accumulation
* ranks
* rank progression
* merge behavior
* split behavior
* rank buffs
* chance-based abilities
* stack combat effects
* stack persistence
* ERM EX
* battle integration
* creature stack identity

The current rank/buff generator should be reused.

### 17. BATTLE SYSTEM

This is one of the highest priorities.

The project already has:

* BA
* BM
* BR
* BG
* MF
* BattleActionTracker
* Olden Era battle events

Finish:

* BU
* BF
* BH
* MR
* all practical BM
* battlefield positions
* stack positions
* attack actions
* movement
* waits
* defend
* spells
* abilities
* damage
* retaliation
* obstacles
* battlefield effects
* stack state
* commander unit
* stack experience effects
* battle rewards
* battle result semantics

H3 has a 17×11 battlefield and Olden Era currently differs. Do not fake coordinates. Create an explicit coordinate
translation layer.

### 18. BATTLE TRIGGERS

Make sure all relevant battle triggers are semantically correct:

* `!?BR`
* `!?BA0`
* `!?BA1`
* `!?BA52`
* `!?BA53`
* `!?BG0`
* `!?BG1`
* `!?MF1`

Add all remaining practical battle triggers.

Validate:

* round number
* acting stack
* side
* hero spell
* attack
* shot
* walk
* wait
* defend
* retaliation
* damage
* battle end
* simulated battles

Do not trigger events simply because they exist. Trigger them at the same semantic point expected by ERA/WoG scripts.

### 19. DIALOGS / UI / IF

Finish:

* IF:D
* IF:E
* IF:F
* IF:G
* IF:M
* IF:Q

and related dialog/message functionality.

Interactive questions are particularly important. Implement ERM suspension correctly:

```
script
 ↓
ask player
 ↓
suspend script
 ↓
show native-style Olden Era dialog
 ↓
player answers
 ↓
resume exact ERM state
```

Do not fake interactive questions with synchronous blocking calls. Do not freeze the game thread. Reuse the existing
UI plugin.

### 20. HINTS / HT

Implement practical:

* HT:P
* HT:T
* HT:V
* HT:W

and all variants used by the corpus.

Use native Olden Era notifications/dialogs/tooltips where applicable.

### 21. OW

Complete the owner/player subsystem.

Implement:

* heroes
* hero counts
* hero ordering
* towns
* town ordering
* teams
* tavern heroes
* active player
* human player
* player resources
* all relevant getters/setters
* map spells
* days without town
* keymaster tent semantics
* other practical H3 player state

Where Olden Era has an equivalent, use it. Where not, virtualize.

### 22. MINES / MAP ECONOMY

Finish:

* mine owner
* mine resource
* mine guards
* lighthouse
* production
* capture
* weekly production
* income
* map resources

Ensure all modifications update native UI/state where possible.

### 23. ERA NATIVE LIBRARY

Do not stop with the current native replacements.

Find every SN:F function used by:

* Era Erm Framework
* WoG Scripts
* ERA Scripts
* WoG Fix Lite
* other core mods

For every function:

1. understand original semantics
2. locate all H3 assumptions
3. map to Olden Era
4. implement native equivalent
5. add headless test
6. add game verification where required

Pay special attention to functions previously backed by:

* H3 memory
* H3 executable code
* map structures
* alliance structures
* object tables
* creature tables
* hero pools
* random-map checks
* game managers

### 24. UN:C

This is a major remaining category.

Do NOT attempt to recreate H3 memory literally. Instead classify each actual UN:C use:

```
UN:C call
 ↓
What does the script expect?
 ↓
What H3 state does it access?
 ↓
Can Olden Era provide equivalent state?
```

Then implement a semantic replacement. Only retain Unsupported if no realistic semantic representation exists.

The project currently has already reduced many raw-memory dependencies by replacing library functions with native
equivalents. Continue this approach.

### 25. SN:E / SN:H / SN:M / SN:B

Audit and complete all actual uses.

Especially:

* SN:E
* SN:H
* SN:M
* SN:B

Implement semantics rather than H3 internals.

### 26. SAVE / LOAD

The current checksum-based WoG state mechanism must remain. Finish persistence for every subsystem.

A save/load round trip must preserve:

* WoG options
* WoGified status
* WoG map state
* PO
* hero state
* artifacts
* creatures
* commanders
* stack experience
* town state
* mines
* virtualized data
* custom object state
* ERA state
* script state where necessary

Test:

```
new game
 ↓
modify state
 ↓
save
 ↓
load
 ↓
state equivalent
```

Do not corrupt native Olden Era saves. Do not write into the ERA installation.

### 27. MAP SCRIPTS

Implement support for Olden Era map scripts where practical.

The target should support:

```
Map
 ├─ native Olden Era map
 ├─ map-specific scripts
 └─ global ERA/WoG scripts
```

Reproduce ERA's rules concerning:

* global scripts
* map scripts
* fixed script sets
* WoG option 5
* loading only selected scripts
* map-specific ERM directories

### 28. RMG / RANDOM MAPS

Do not rewrite Olden Era RMG unless necessary. Integrate at the correct lifecycle point:

```
RMG
 ↓
generated Olden Era map
 ↓
WoGification
 ↓
ERA scripts
 ↓
game
```

Investigate deeper RMG integration only where required by actual WoG/ERA behavior.

Support:

* generated maps
* map checksum
* RMG detection
* WoGify random maps
* map-dependent rules

### 29. ERA PLUGINS / FRAMEWORK FEATURES

Audit more than the ERM syntax.

The final port should cover behavior required by:

* Era Erm Framework
* WoG Scripts
* ERA Scripts
* WoG Fix Lite
* other active ERA Project mods

Do not limit the project to the core interpreter.

If a function is provided by a closed-source ERA plugin, reproduce the behavior in native C# where possible.

The goal is compatibility, not binary plugin transplantation.

### 30. HEADLESS REFERENCE ENGINE

Every major new semantic feature needs a headless implementation whenever possible.

Architecture:

```
WoG / ERA semantics
        ↓
Headless reference
        ↓
tests
        ↓
Olden Era adapter
        ↓
game verification
```

The headless engine must remain useful as an oracle. Do not make tests depend on the game for everything.

### 31. DEBUG SYSTEM

Expand the existing WoG Debug system.

Every newly completed feature should be accessible from:

* command bridge
* debug command
* feature tile
* self-test if practical

Examples: wogify, map, hero, town, battle, artifact, creature, skill, spell, commander, experience, options, save.

Use existing: peek, invoke, set, objects, hero, town, battleevents, subskills, symbols, erm.

Do not create a second RE console.

### 32. FEATURE CATALOG

Every confirmed feature should appear in `src/WoG.OldenEra.DebugUI/FeatureCatalog.cs`.

Each feature should provide:

* name
* category
* ERM code
* description
* status
* optionally a test command

This becomes the in-game compatibility dashboard.

### 33. TESTING REQUIREMENTS

Expand the current tests substantially. For every subsystem:

* Unit tests — semantic behavior
* Integration tests — multiple subsystems interacting
* Corpus tests — real ERA scripts
* Game verification — real Olden Era runtime
* Save/load regression — state survives
* Regression — old functionality remains intact

Do not lower the test standard to make the suite pass.

### 34. GAME VERIFICATION

Use actual Olden Era where necessary.

Current game environment already used by the project:

* Olden Era 0.81.04
* Unity 6000.0.66f1
* IL2CPP
* BepInEx 6 be.785

Before deployment:

* game must be closed
* follow CLAUDE.md
* back up saves/Core.zip before risky changes
* never modify the ERA installation
* never commit game files
* never commit proprietary H3/ERA assets
* never press F9 during debugging

Use the existing WoG Debug window and command bridge.

### 35. RESEARCH RULE

When behavior is unknown: DO NOT GUESS.

Use this sequence:

```
Source code
 ↓
Script corpus
 ↓
Existing documentation
 ↓
Old WoG/ERA runtime if available
 ↓
Olden Era data
 ↓
Olden Era IL2CPP RE
 ↓
Semantic implementation
```

Tag findings:

* `[V-code]`
* `[V-data]`
* `[V-community]`
* `[V-game]`
* `[UNVERIFIED]`

These tags already exist in project documentation. Use them.

### 36. VERSION RESILIENCE

Olden Era is obfuscated and changes between versions. Do not scatter fragile metadata throughout the code.

Use the existing:

* symbol registry
* JSON symbols
* self-checks
* verified/unverified symbol statuses

Keep engine-specific details isolated from semantic code.

### 37. NO FALSE COMPLETION

A feature is NOT complete merely because:

* code compiles
* a test passes
* an ERM command does not throw
* a value exists in WoG state

A feature is complete when its intended gameplay semantics work.

Example: HE:S is not complete merely because `hero.skills.Add(...)` works. It is complete when:

* hero skill state changes
* bonuses apply
* UI reflects it
* level-up/sub-skill mechanics behave correctly
* scripts see the changed state
* save/load preserves it

Apply this standard everywhere.

### 38. COMPLETION MATRIX

Maintain a machine-readable or Markdown matrix: Feature · WoG · ERA · ERM · Used by corpus · Implemented · Engine
method · Headless · Game verified · Save/load · Debug · Tests · Status.

Target: no unexplained gaps.

### 39. PRIORITY ORDER

Work in dependency order.

P0 — Core compatibility

* remaining ERM semantics
* pool heroes
* map commands
* UN:C
* SN:E
* native library
* SS
* IF
* WoG Options
* complete WoGification
* save/load

P1 — Core WoG gameplay

* WoG objects
* creature systems
* artifacts
* towns
* heroes
* mines
* commanders
* stack experience
* battle receivers

P2 — Advanced ERA

* map scripts
* plugin-backed functionality
* advanced dialogs
* advanced hooks
* advanced map transformations
* RMG integration

P3 — Polish

* complete UI
* compatibility reporting
* diagnostics
* performance
* documentation
* localization
* visual refinement

### 40. PERFORMANCE

Do not sacrifice compatibility with unnecessary overhead.

Pay attention to:

* full-map scans
* ERM loops
* object enumeration
* battle events
* reflection
* IL2CPP calls
* repeated symbol resolution
* script execution
* save/load

Use caches where safe. Do not cache mutable game state incorrectly.

### 41. FINAL ACCEPTANCE CRITERIA

The project should eventually reach:

```
BUILD                 → 0 warnings
UNIT TESTS            → all green
ERA CORPUS            → 0 parser errors → 0 runtime errors
CORE ERA MODS         → functionally usable
WOG                   → functionally usable
WOGIFICATION          → actual map transformations
OLDEN ERA             → native systems used wherever possible
SAVE/LOAD             → persistent WoG/ERA state
DEBUG                 → all major systems inspectable
COMPATIBILITY MATRIX  → every remaining gap explicitly explained
```

### 42. MOST IMPORTANT FINAL RULE

Do NOT stop when the obvious work is finished.

After each implementation wave:

```
run corpus
 ↓
collect remaining unsupported calls
 ↓
group by subsystem
 ↓
research source behavior
 ↓
implement
 ↓
test
 ↓
game verify
 ↓
update matrix
 ↓
repeat
```

Continue this loop until the remaining gaps are either:

1. genuinely impossible to represent in Olden Era,
2. dependent on an unavailable engine capability that has been fully investigated,
3. or genuinely outside the WoG/ERA compatibility target.

Everything else should be implemented.

### 43. DO NOT ASK FOR PERMISSION FOR NORMAL CODING DECISIONS

Make reasonable architectural decisions autonomously.

Do not repeatedly stop and ask:

* what class to create
* where to put a test
* whether to add a helper
* whether to update documentation
* whether to refactor a local implementation

Use the existing project conventions.

Only stop when an external decision is genuinely required, especially if it would violate the repository's safety
rules regarding the user's game installation.

### 44. EVERY MAJOR CHANGE MUST LEAVE EVIDENCE

For each substantial feature:

* code
* tests
* compatibility matrix
* MODLOG.md
* MODLOG.ru.md
* Debug feature where appropriate
* game verification where necessary

Record:

* what was implemented
* original WoG/ERA behavior
* Olden Era equivalent
* how it was verified
* limitations
* rollback considerations

### 45. FINAL DELIVERABLE

When you believe the port is complete, produce a final audit containing:

* A. WoG coverage — every WoG receiver/trigger/system.
* B. ERA coverage — every ERA-specific receiver/trigger/runtime feature required by the corpus.
* C. ERM coverage — parser, preprocessor, runtime, functions and native libraries.
* D. ERA Project mods — actual execution status of all relevant mods.
* E. WoGify — actual map transformation coverage.
* F. Olden Era integration — every native system used.
* G. Unsupported — every remaining unsupported item with a technical reason.
* H. Reverse engineering — every newly discovered Olden Era subsystem.
* I. Tests — exact numbers.
* J. Game verification — exact features verified in the real game.
* K. Save/load — what persists and how.
* L. Performance — known bottlenecks.
* M. Remaining work — only genuinely unresolved work.

### 46. CORE ARCHITECTURE

The final system should converge toward:

```
                    ERA 3.9.31
                         │
                    ERM 2.0
                         │
                WoG / ERA Runtime
                         │
              Semantic Compatibility
                         │
       ┌─────────────────┼─────────────────┐
       │                 │                 │
      Map              Hero             Battle
       │                 │                 │
      Town            Army             Creature
       │                 │                 │
   Artifacts          Skills          Experience
       │                 │                 │
       └─────────────────┼─────────────────┘
                         │
                Olden Era Adapter
                         │
             Native Olden Era Systems
                         │
                    Olden Era
```

The golden rule remains: ERA/WoG behavior first. Native Olden Era implementation whenever possible. Semantic
emulation when necessary. Virtualization only when required. Never fake a feature just to remove an Unsupported
label.

Start by auditing the current HEAD and generating the complete remaining-work matrix. Then begin implementing the
highest-impact missing subsystem and continue until the full compatibility loop converges.
