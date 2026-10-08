# MODLOG — work log

Log following the `mod-any-game` (universal-modder) methodology: paths, versions, formats, what did not work and
why, next step.

## 2026-10-06 — session 1

### Task
Port WoG 3.58 to Heroes of Might and Magic: Olden Era (the full task text is in the session history). Game:
Olden Era, Steam 3105440, early access since April 30, 2026, Unity IL2CPP. Platform: Windows (Linux via Proton —
BepInEx 6 exists for the game). Online/anti-cheat: single-player only.

### Recon (universal-modder knowledge base)
* The universal-modder knowledge base has no notes on Olden Era/Heroes. The Unity playbook
  (`references/engines/unity.md`) applies: the BepInEx 6 IL2CPP + Harmony route.
* `um scan` is unavailable (the game is not in the container) — facts about the game were collected from working
  community code (see `OldenEra_ReverseEngineering/00_Sources_and_Verification.md`).

### Chosen route and why
* Data (`Core.zip` + zip overlay) — for static content (unit clones, buffs).
* BepInEx 6 IL2CPP plugin — for everything dynamic (ERM, commanders, experience, combat hooks, saves): Olden Era's
  scenario scripting is too limited for ERM (no variables/loops/functions, no combat hooks).
* The WoG core is separate and has no engine dependencies: testable without the game, portable.

### Findings (non-obvious)
1. The WoG sources are available: `GrayFace/wog` (the 3.59 alpha branch, based on 3.58f) — `erm.cpp`, `npc.cpp`,
   `crexpo.cpp`.
2. Event 30000 is `TM1`, even though `FU` accepts the number 30000: in WoG a function frame is created only for
   `Event < 30000` (caught by the test `Trigger_local_y_minus_are_zeroed_per_section`).
3. `VR:U` checks "ends with", not "contains"; a single-character search finds nothing.
4. String comparison in `StrCmpExt` is word-by-word, case-insensitive, and asymmetric for the empty string.
5. The comment about commander special bonuses in `npc.cpp` is outdated; the truth is in the functions that check
   the masks and in `ToHint`. Gold for experience is granted by class 5 (code), not Inferno 3 (comment); 50%, not
   25%.
6. `CO:A` falls through into `CO:N` (no `break`); `CO:B3` writes the bit to the wrong mask — reproduced behind a
   flag.
7. In a new game `!@` **terminates** parsing of the file; in a loaded game (with `ZVSE` after it) it enables
   instructions.
8. The option preset format (`SaveSetupState`) lives in the closed-source `ZvsLib1.dll`; not confirmed.
9. Olden Era: IL2CPP + obfuscation, assembly `Hex`; names like `qp/bufc/bufo` change with updates.
   Solution — symbols in JSON with self-checking (as in gme-mod); no rebuild is needed after updates.
10. OE localization: `{"tokens":[{"sid","text"}]}` with BOM; archives are uncompressed.

### Verification (oracles)
* 102 xUnit tests (parser, ERM semantics, commanders, stack experience, saving, options, overlay, visuals).
* 3.58f corpus: 78 files, 1561 sections, ~25K lines — 0 parse errors; loading as a new game — 0 runtime
  errors. 3.59 corpus: 117 files, 44,464 lines — 0 errors.
* In game — **nothing** (no copy of the game).

### What did not work / limitations
* `dotnet new classlib -f net6.0` does not work in SDK 8 without the net6 template — a net8 project is created
  and retargeted.
* `JsonNode.DeepClone` does not exist in .NET 6 — cloning goes through serialization.
* ERT strings (`z>1000`) are not loaded yet (the `.ert` format has not been decoded).

### Next steps
1. On the machine with the game: `07_InGame_RE_Plan.md` → `wog_symbols.json` (symbols for heroes, turns, objects,
   combat, saves).
2. Map the receivers the scripts need at startup: `HT`, `OW:T`, `UN:A/B/R/V/X`, `IF:D/F`, ERT.
3. Fill in `Compatibility/id-maps/creature.json` (choosing OE units for H3 creatures by role/level).
4. Combat adapter: rank buffs + hooks for chance-based effects; commander unit.
5. UI: commander window, WoG Options dialog.

### How to roll back
The plugin is installed as a separate folder, `BepInEx/plugins/WoG/`; delete it. Overlay — delete `wog_core.zip`
next to `Core.zip`. Before the first launch — back up the saves and `Core.zip`.

## 2026-10-06 — session 1, continued: preparing to work on the machine with the game

* The user is at a computer with Olden Era, but the cloud session cannot reach it. Two routes: local Claude Code
  in the repository folder (better — changes can be verified in game right away) or data collection by a script.
* Added `tools/oe-recon/collect.ps1` — read-only data collection (see `07_InGame_RE_Plan.md`, item 0.1).
* Did not work / taken into account:
  - a `.ps1` file containing Cyrillic **must** be UTF-8 with BOM: Windows PowerShell 5.1 reads a file without a
    BOM in the ANSI code page (cp1251), and the UTF-8 bytes of the letters "Б", "В", "Г", "Д" turn into
    typographic quotes `‘ ’ “ ”`, which PowerShell treats as quotes — the script fails to parse;
  - `(if …)` inside an expression is a syntax error; `$(if …)` is required;
  - `Format-Table | Out-String` without `-Width` returns an empty string in an environment without a console.
* Verification: PowerShell 7.4 parser — 0 errors; a run on a fake game folder (`Core.zip` with a JSON array,
  `{"array":[…]}`, a corrupted file and a `//` comment) — the archive is built, the corrupted file ends up in
  `parse-errors.txt`. The script has not yet been run on the real game or in Windows PowerShell 5.1.

## 2026-10-07 — session 1, continued: target — HoMM3 ERA

### Decision
The user: "If there is a more advanced version of WOG — ERA, let's go with that instead" («Если есть более
продвинутая версия WOG — ERA, давай лучше её»). The project target is ERA (`ERA-Projects/era-project-eng`/`-rus`
2.291, engine `ethernidee/era` 3.9.31). WoG 3.58 is the bottom layer of ERA; the work done for it is kept.

### Recon
* ERA = WoG 3.58f (+ "3.59 TE") + `era.dll` (new ERM interpreter, ERM 2.0, mods, translations, saves) +
  closed-source plugins + scripts (WoG Scripts rewritten for ERM 2.0, ERA Scripts, Era Erm Framework).
* First check: the WoG parser fails on almost every ERA script file (`!?FU(Имя)`, etc.).
* Key point: ERA does not parse ERM 2.0 directly — `PreprocessErm` (Erm.pas) translates the text into classic ERM,
  which is then parsed by the WoG compiler with hooked functions. Hence the port = preprocessor (line by line) +
  ERA's parameter grammar + ERA's `ProcessErm` interpreter.
* The engine library `ethernidee/b2` (`TextScan`, `StrLib`) is needed to replicate the preprocessor's scanner.

### Findings (non-obvious)
1. A single uppercase letter `(F)` is a **constant**, not a function (the `DetectIdentType` rule); function names
   are mixed case. A single-letter index `f..t` in `arr[i]` is always a quick variable.
2. In ERA, y1..y100, e1..e100, `x`, the quick variables `f..t`, z-1..z-10, f996–1000, v997–1000 are local to the
   **event** (saved/restored), not to the section; `ErmLegacySupport` (set to 1 as shipped) adds zeroing of
   y-1..-100 in every section of classic triggers.
3. In ERA, `VR:U` is a case-insensitive "contains" (in WoG — "ends with"); strings in conditions are compared byte
   by byte; z1..z1000 are **not** interpolated on output (in WoG they are).
4. ERA bugs reproduced by the port: `d|` in `SN` parameters performs AND; `SN:K` with an integer GET returns the
   code of the *first* character; the type check in `VR:+` for strings never fires (`not` precedence); freeing a
   range of local variables in the preprocessor leaves it in the list; inserted helper commands shift labels.
5. `SN:F` calls exported functions of `era.dll`; the project's scripts use ~30 of them — the needed ones have been
   ported (`EraApi`), including `ExtendArrayLifetime` (without it, the arrays of standard library functions
   disappear).
6. The load order determines function numbers (95000+), so `GetOrderedPrioritizedFileList` was replicated.

### Verification
* 132 xUnit tests (30 of them for ERA: preprocessor, grammar, semantics, saving).
* ERA 2.291 corpus (183 scripts): preprocessor 0 errors; parsing 0 errors; new game + 7 days — 0 runtime errors.
  WoG 3.58f/3.59 corpora — no regressions.
* In game — still nothing.

### What did not work / limitations
* File name sorting is an approximation of Windows `AnsiCompareText`.
* `SN:E`/`UN:C`/`SN:B` — H3 code and memory; plugins are closed-source DLLs. Marked UNSUPPORTED.
* The project's other mods (Game Enhancement Mod, Advanced Classes, etc.) have not been run yet.

### Next steps
1. On the machine with the game: `tools/oe-recon/collect.ps1`, then Olden Era symbols (`07_InGame_RE_Plan.md`).
2. Hook up ERA mods in the plugin (`WoGHost.AddEraMods`) and ERA event hooks (combat, screens, keys).
3. Receivers needed by the ERA corpus: `UN` (map), `BM/BU/BG/BA` (combat), `CA`, `PO`, `OB`, `DL` (dialogs).
4. Run the remaining ERA Project mods; document and reimplement the plugins (`SS`, etc.).

## 2026-10-07 — session 2 (local Claude Code on the machine with the game)

### Environment
* Olden Era **0.81.04** (version in the main menu), Steam build of 2026-10-05, Unity **6000.0.66f1**, IL2CPP, no
  anti-cheat. Install: `G:\SteamLibrary\steamapps\common\Heroes of Might and Magic Olden Era`.
* The user's HoMM3 ERA install: `G:\Games\Тень Смерти` — ERA Project **2.291 rus**, 17 active mods in
  `Mods\list.txt` (WoG, Era Erm Framework, Easy Cheats, BattleQueue, Game Enhancement Mod, WoG Rus, WoG Scripts,
  WoG Scripts Rus, TrainerX, ERA Scripts, AMER_HumanAI, Enhanced Henchmen, …).
* .NET 8 SDK installed user-local in `%USERPROFILE%\.dotnet8` (PATH untouched; set `DOTNET_ROOT` to use it).
* `nuget.bepinex.dev`, `builds.bepinex.dev`, `unity.bepinex.dev` and `archive.org` time out from this machine, also
  with the user's VPN on (console programs do not go through it). GitHub and nuget.org work.

### BepInEx without bepinex.dev (how it was installed)
1. BepInEx 6 IL2CPP **be.785**: the exact `BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.785+6abdba4.zip` is attached to the
   GitHub release `MrDiamond64/Hydra` v2.0.0; its SHA-256 `2A7CBF74…7E2D9B7F` equals the one pinned by
   `mimiasei/map-editor-json-tool/scripts/prepare-gme-mod.ps1`. (The Scenario Editor installer does **not** contain
   BepInEx — dead end.)
2. The first start fails: BepInEx downloads the Unity base libraries from `unity.bepinex.dev`. Fix: `Managed.zip` of
   the GitHub release `LavaGang/MelonLoader.UnityDependencies` **6000.0.66** (generated from Unity's own build), saved
   as `BepInEx\unity-libs\6000.0.66.zip` — BepInEx uses a local zip with the URL's file name. Interop: 140 assemblies,
   `Hex.dll` 33 MB, no errors.
3. The plugin compiles against the game's `BepInEx\core` (`OldenEraDir` / `OLDEN_ERA_DIR`), not NuGet.

### Changes outside the repository (all reversible)
* Backups: saves → `%USERPROFILE%\.universal-modder\backups\oldenera-saves\` (um backup, 1.39 GB); `Core.zip` +
  SHA-256, `SettingsLocal.json`, `BepInEx.cfg` → `%USERPROFILE%\.universal-modder\backups\oldenera-game\`.
* BepInEx in the game folder (`BepInEx\`, `dotnet\`, `winhttp.dll`, `doorstop_config.ini`, `.doorstop_version`,
  `changelog.txt`); WoG in `BepInEx\plugins\WoG` and `BepInEx\config\WoG`, `wog_symbols.json`, `wog.oldenera.cfg`.
* User request: intro videos off — `Ubisoft.mp4`, `Hooded_Horse.mp4`, `Unfrozen.mp4`, `Trailer.mp4` moved from
  `StreamingAssets\video` to the backup folder `video-intros` (the game logs "video … dont found" and goes on; the menu
  appears after ~25 s). A Steam update or a file check may bring them back.
* User request: windowed 1600×900 — `prefs\SettingsLocal.json` `fullScreenMode` 0→3, resolution 1600×900, and Unity's
  `HKCU\Software\Unfrozen\HeroesOldenEra` `Screenmanager Fullscreen mode` 0→3 (+ width/height, Use Native 0); the
  registry backup is in `%LOCALAPPDATA%\universal-modder\reg-backups\`.
* The BepInEx console window is off (`[Logging.Console] Enabled = false`), see gotcha 3.

### Found (non-obvious)
1. Olden Era's obfuscation (GUPS Obfuscator) is partial: the data model keeps readable names
   (`Hex.Session.Data.Data` with `sides`, `heroes`, `day`, `week`, `month`, `daysInGameCount`; `Side.res` →
   `ResHeap{gold, wood, ore, gemstones, crystals, mercury, dust, graal, starDust}` → `Resource.value`); the classes that
   hold them are obfuscated (`dbx` = session singleton with static `me`, property `cfjl` = Data; `ebe` = game logic
   singleton with `OnStartDay()` / `OnStartWeek()`).
2. The O4 contradiction ("hex.dll in dnSpy") is explained: the **demo** (`Heroes of Might & Magic Olden Era Demo`,
   Oct 2025) was Unity 2020.3.48 **Mono**; the full game is IL2CPP.
3. Saves: `%USERPROFILE%\AppData\LocalLow\Unfrozen\HeroesOldenEra\users\Steam_<id>\saves\singleplayer\…\*.saveskirmish`
   = gzip → a length-prefixed hash string, the game version string (`0.80.48` in a September save), then a JSON header
   (title, template, spawns, …) and the rest (not parsed yet). Lobby presets are MessagePack.
4. `ebe.OnStartDay()` is called **once per day for all sides** (day 2 after End Turn) and **not on day 1**.
5. ERA's `Mods\list.txt` is lowest priority first (a translation mod such as `WoG Rus` comes after `WoG`); the plugin
   reverses it for `AddEraMods` (highest first).

### Gotchas
1. Do not `Join-Path` Steam library paths: a library on a disconnected drive (`E:`) throws `DriveNotFound`.
2. `um win shot --exe HeroesOldenEra.exe` may capture the BepInEx console instead of the game (same process).
3. **One click into the BepInEx console freezes the game**: QuickEdit selection blocks console writes, and the game's
   main thread stops at its next log line (window title "Выбрать BepInEx …"). The deploy script turns the console off.
4. The plugin had never run before: the adapter called `ids()` in its constructor before the host existed (NRE at
   load) — fixed; reading an instance member of a null session threw `TargetException` in the main menu — symbol
   reads now return null outside a session.
5. A real bug found by the in-game self-test: a second `StartNewGame` in one process (a new map) kept the old sections
   and reset ERA function ids to 95000, so new functions collided with old ones and ran twice (DO loop 110 instead of
   55, `OW:R d100` added 200). `StartNewGame` now starts from a clean state and runtime when a game already ran.
6. ERA `HE-1` is the hero of the event, not the active hero.

### WoG Debug (new)
* `src/WoG.Debug`: ERM console (runs ERA code as a one-off `!?FU(WogDebug_Exec_N)`), self-test of the WoG/ERA layer
  (core ERM + engine-facing cases compared with the adapter read directly) and a receiver coverage table.
* In game (`[Debug] Enabled = true`, `tools\deploy\deploy.ps1 -DebugMode`): command bridge — a text file in
  `BepInEx\config\WoG\debug\in` is executed on the game thread and the result appears in `debug\out` (commands:
  `state`, `erm`, `event`, `newday`, `symbols`, `selftest`, `compat`, `vars`); method tracing (`TraceMethods`).
* `mods\WoG Debug`: the ERA script of the slice.

### Verification
* 142 xUnit tests (+6: the self-test passes completely on the headless engine, the state is restored, the slice, two
  games in one host). ERA corpus of the user's install: 0 runtime errors — the 4 project mods and all 17 mods of
  `list.txt`.
* **In game (skirmish ARCADE, Temple, 2 players):** the state dump matches the UI (gold 35000, wood 30, ore 30, gems,
  crystals, mercury 35, dust 250, date М1 Н1 Д1, Player/Bot). After End Turn: `ebe.OnStartDay` call #1, WoG
  "day started: day 2, players 0,1", the ERA script `!!OW:R(owner)/6/d1000` added 1000 gold to the human only, and the
  top bar shows 36750 = 35000 + 750 income + 1000. Self-test in game: 26 pass, 0 fail, 2 unsupported (OW:A, sulfur),
  1 error (HE:E — heroes not bound yet), 2 skipped (interactive). 12 symbols are marked `verified`.

### Menu click points (client area 1600×900)
New game 195,249 → Quick start 196,296 → Templates 800,400 → ARCADE 913,280 → Choose 1290,799 → Temple 514,290 →
first hero 464,210 → difficulty "low" 632,742 → Start 800,821. End turn 1522,828, confirm 695,486.

### Next steps
1. Day 1: run OnEveryDay once the map is ready in a new game (OnStartDay is not called on day 1); per-player order.
2. Heroes: `DataHeroes` → hero list/id/owner/stats/army; `OW:A`, `HE`; then save/load hooks (`Data.Save(string)` is a
   candidate), map objects, battle.
3. Load the user's full ERA mod list in game and grow the self-test along with the adapter.
4. Finish `TRANSLATION_STATUS.md`.

### How to roll back
Delete `BepInEx\plugins\WoG`, `BepInEx\config\WoG`, `BepInEx\config\wog_symbols.json`, `wog.oldenera.cfg`; BepInEx
itself: `BepInEx\`, `dotnet\`, `winhttp.dll`, `doorstop_config.ini`, `.doorstop_version`, `changelog.txt`. Videos:
move them back from `video-intros`. Window: restore `SettingsLocal.json.orig` and the `.reg` backup.

## 2026-10-07 — session 2, continued: heroes, armies, ERM robustness, H3 data layer

### Done
* **Heroes on Olden Era** `[V-game]`: WoG numbers 0..155 are given to the game's heroes (in play first, then the hire
  pool) and kept in the WoG state; level, experience, primary skills (type base + growth), mana, movement, owner,
  player heroes, active hero. **Armies**: slots read/written, units added/replaced/removed (HE:C).
* **Resources** change through the game's own add/spend methods, so the top bar, quests and statistics update (a plain
  field write did not refresh the UI).
* **Day 1**: OnEveryDay/timers run once the new map is ready; the day counter is 0 while the map is being built, so the
  plugin waits instead of giving up.
* **ERM time limit** (`[ERM] TimeLimitMs`, 10 s): an engine call into ERM that runs too long is abandoned with an error
  naming the line — an endless loop caused by an unsupported command no longer freezes the game. Repeated log lines
  from one script line are muted after 3.
* **Era ini files** (port of b2 `Ini.pas`): `ReadStrFromIni`, `WriteStrToIni`, `SaveIni`, … — read from the write root,
  then the ERA installation; saving never writes into the ERA folder (`BepInEx\config\WoG\era-root`; tools and tests
  use a temp folder — a tool run used to write `Runtime\*.ini` into the current folder).
* **FU:D** does nothing in a single-player game (WoG sends it to the remote player only).
* **H3 data layer** (`src/WoG.Core/H3Data`): read-only LOD/PAC reader, Era's VFS order (mods highest priority first:
  loose file in the mod's `Data`, then its archives; then the game's `Data` with `h3bitmap.lod` first), H3 text tables
  (tab-separated, quoted multi-line fields, UTF-8 or cp1251). `WoG.ErmTool h3data <ERA folder> [ids]` prints what is
  read and from where. Nothing from the installation is copied into the repository.
* **UN:A** (artifact setup, map ban, combination table) on top of `artraits.txt` + the SoD combinations; changes are
  copy-on-write in the WoG state and saved with the game. **UN:V** returns the dialect's versions (ERA 400/3931).

### Found (non-obvious)
1. The user's ERA reads `artraits.txt` from `Mods\WoG\Data\hmm35wog.pac` (English texts; "WoG Rus" does not override
   it), 171 artifacts.
2. `UN:A…/2` (position) uses ERM's **Format P2** — 0 none, 1 head, 2 shoulders, 3 neck, 4 right hand, 5 left hand,
   6 torso, 7 ring, 8 feet, 9 misc, 10–13 war machines, 14 spell book — not hero slot numbers. Hero slots
   (`ART_SLOT_*` in Era Erm Framework) are 0 head … 8 feet, 9–12 misc 1–4, 13–16 war machines, 17 spell book,
   18 misc 5. Confirmed by `AMER_HumanAI_Artifacts.erm` ("artiSlotP2", "-1 maps P2 to AP except boots").
3. `UN:A` "gives spells" (field 8) has no table in the text files; it starts at 0 `[UNVERIFIED]` for the tomes etc.
4. The remaining unsupported commands at start + day 1 with all 17 mods: `UN:C` 103 and `SN:E` 78 (raw H3 memory and
   exe functions: game/combat/adventure managers, hook contexts, dialog structures — they need per-mod semantic ports),
   then `UN:U` 14, `UN:X` 11, `UN:R` 10, `UN:J` 7, `UN:N` 6 — map commands; the adapter has no map layer yet.
5. Olden Era's map (`Hex.Map.MapData`) has `sizeX_`/`sizeZ_`, an elevation `levelsMap` and no underground `[V-code]`.

### Verification
156 xUnit tests (+9: synthetic LOD archive, VFS priority, artraits parsing, UN:A/UN:V through ERM, UN:A changes saved
with the game). `WoG.ErmTool h3data` on the user's installation: 171 artifacts with names, costs, positions and
combinations.

### Next steps
1. Map layer in the adapter: map size, hero positions, map objects with an H3 type/subtype mapping, towns; then
   `UN:X/U/I/O/S/H`, `HE:P`, `OB`, `CA`.
2. `UN:N/G` from the H3 tables (names of spells, creatures, skills, buildings), `UN:R/J`.
3. Link H3 artifacts to Olden Era items (id map "artifact") so `HE:A` and the UN:A changes act on the game.

## 2026-10-07 — session 2, continued: the adventure map for ERM

The user named the core ERA set most other mods need: WoG, Era Erm Framework, WoG Rus, WoG Scripts, WoG Scripts Rus,
WoG Fix Lite, ERA Scripts. It is measured on its own (`WoG.ErmTool run --era` with exactly these) and fixed first.

### Done
* **Map layer of the adapter** `[V-game]`: map size, hero positions, map objects, squares. Olden Era's map is a node
  grid (node = x + z·sizeX) with z growing to the north and no underground; H3 counts y from the top, so
  y = sizeZ − 1 − z. Objects: `Map.bzjn.bzsr` (2581 on an 80×80 map, scenery included); each has entrance nodes
  (`bzrf`) and occupied nodes (`bzrg`). A pick-up (one occupied node ringed by entrances: resource, chest, artifact,
  prison) stands on its own node, as in H3; a building at its first entrance in H3 scan order — every object has one
  ERM position. Monster squads (`Data.squads`) are type 54 with the creature number of the first unit; heroes on the
  map are type 34. The map is read once per game frame (one ERM call runs within a frame).
* **`Compatibility/id-maps/object.json`** (`WoG.ErmTool idmap-objects <Core.zip> <object.json>`): Olden Era object sid →
  H3 type/subtype (Format OB), chosen by what the object does (DB/objects_logic), not by its name: mines by
  `resName`, windmill → Windmill, watchtower → Redwood Observatory, mana well → Magic Well, forge → Black Market,
  dwellings → the town dwelling of the level (Format CG), portals → Two-Way Monoliths, … 243 of 486 interactive
  objects have an H3 type; the others get types from 1000. Only sids and numbers — no game data.
* **ERM**: `UN:X` (size; levels 0), `UN:U` (WoG's CalcObjects/FindObjects/FindNextObjects order and errors, and Era's
  `UN:U(type)/(sub)/(dir)/(x)/(y)/(z)` form where "not found" is x = −1), `OB:T/U` (a hero on the square is type 34
  unless `T?$/1`), `TR:T/P/E` (biome → H3 terrain of the matching town, road, red/yellow bits, the 3-parameter entrance
  form), `HE:P` (read), `UN:N` (names from zcrtrait/sptraits/sstraits/artraits of the installation, N5/N6 ini
  values), `UN:R` (redraws: Olden Era redraws itself; pointer and delay are cosmetic).
* H3 tables now also hold creatures (all zcrtrait.txt stats, for MA later), spells and secondary skills.
* WoG Debug: `peek <path> [max] [field,field]` reads any game object in a running game (how all of the above was
  found); the bridge reads only `*.txt` commands (a half-written file was picked up before).

### Found (non-obvious)
1. With `UN:X` working, the stdlibs of WoG Scripts and ERA Scripts scan every square at game start (`OB:T/U`,
   `TR:E/P`) — 6400 iterations; this is what the core set needed most after the data layer.
2. In the user's ERA the Russian creature, spell and skill tables come from "WoG Rus" (`era rus.pac`); artifact texts
   stay English (`hmm35wog.pac`). ERA's spell table has 201 rows (WoG spell slots after 80).
3. `gem_fixes.erm` relies on Era's UN:U extension; WoG's v-index check made it an error before.
4. Core set at start + day 1 now: `UN:C` 70, `SN:E` 55 (raw H3 memory, exe functions, hooks — library functions such as
   `GetMaxMonsterId`, `WOG_CheckRandomMap`, `WOG_GameMgr_GetPlayer_Team` need native implementations), `SN:L` 3,
   `IF:G` 2, `UN:J` 1, `SN:B` 1; no ERM errors with all 17 mods.

### Verification
164 xUnit tests (+8). In game (ARCADE, 80×80): `UN:X` 80/0; `UN:U` 4 gold mines, 7 towns, 55 resources, 7 dwellings;
the human hero stands on its town's entrance (59,57,0 = node 1819); `OB:T` there = 34 (the hero), `TR:E` = 0,
`TR:T` grass + road; a full 80×80 `OB:T`+`TR:E` scan in about a second; start of a new game with all 17 mods: no ERM
errors.

### Next steps
1. Object visit hook (`object.interact`) → `!?OB`/`!?HM` triggers, OB:D/E/R/S; battle start/end; hero level-up.
2. Native implementations of the stdlib functions built on `UN:C`/`SN:E`.
3. Monsters (`MO`), towns (`CA`), mines (`MN`), `MA` from the creature tables.

## 2026-10-07 — session 2, continued: object visits (!?OB / !$OB)

### Found (method traces in game)
* Player actions go through a command bus (`bgj.Invoke(bgg)`, `ECmd`): a move is command `dha` (hero, path, target
  node), a reward choice `dfv`; a visit itself is not a command — it happens in the world logic when the hero arrives.
* World objects have a logic class `fnt` (`coyw` = map object, `coyx` = session DataObject); the hero's logic is `fdq`
  (`cnsj.chna` = `Hex.Session.Data.Hero`). On arrival `fnt.bmjj(fdq)` runs first; the object's action is applied by
  `fnt.bmjb` + `fnt.bmiw` — at once for a pick-up (gems: bmjj, bmjb, bmiw, bmjf, bmji, bmip, bmio), after the
  player's choice for an object with a dialog (chest: bmjj, bmji, bmip, bmio, dialog, `dfv`, bmjb, bmiw).
* Tracing every method of `fnt` crashed the game at start, and so did its empty virtual methods: IL2CPP folds identical
  method bodies, so a patch on one hooks many unrelated methods. Trace only non-virtual methods with real bodies.

### Done
* `!?OB` runs before `fnt.bmjj` (before the object acts and before its dialog, as WoG's pre-visit trigger),
  `!$OB` after `fnt.bmiw`; the hero (HE-1), its owner and the object's ERM position (v998–v1000) and type/subtype
  come from the map layer. The log line "WoG: visit of T/S at x/y/l by hero N" shows each visit.
* WoG Debug's ERA mod is loaded on top of the user's mod list while debugging; `wogdebug - visits.erm` counts visits.

### Verification
In game: picking up wood ran `!?OB79` and `!$OB79` once each (79/0 at 10/38/0, hero 105); a treasure chest ran
`!?OB101`/`!$OB101`. ERM lesson: consecutive trigger lines are separate empty sections — each needs its own body.

## 2026-10-07 — session 2, continued: game event bus, battles, BA

### Found (traces in game)
* Olden Era raises its game events through one method, `zb.Invoke(EEvent, yx)` (singleton `zb.buvw`; 229 event kinds:
  `HeroMakeStep`, `HeroLevelUp`, `SideStartBattle`, `SideEndBattle`, `BattleResultsStartApplying`, `KillSquad`,
  `InteractWithWoObject`, `MapSaved`, `SaveLoaded`, …). One hook gives most adventure-map triggers.
* Attacking a squad first shows a simulated result with "To battle / Accept / Flee". Accepting gives
  `BattleResultsStartApplying (Simulated)`, … `SideEndBattle` ×2 (one per side); a fought battle gives
  `SideStartBattle` ×2 (`xe.buri` = side id, −1 neutral) at the tactics phase, then `BattleResultsStartApplying
  (Standard)` and `SideEndBattle` ×2. Rounds and stack turns do not go through this bus.
* `HeroMakeStep` (`ve.buog` = hero logic) came once per move in the traces, not once per square [UNVERIFIED for long
  paths].
* Era rewrote `OW:C`: `OW:C?(current player)/?(the human player at this PC)`, other parameters ignored
  (`Hook_OW_C`); GEM's battle script uses it.

### Done
* Hook on the event bus: battle start → `!?BA0/52` (OnBeforeBattle/Universal) once per battle (for an accepted
  simulation the result is already decided then — documented limitation), battle end → `!?BA1/53`, `HeroMakeStep` →
  `!?HM`, `HeroLevelUp` → `!?HL`.
* `BA` receiver (H O P Q S E A, read only): the adapter records the battle at its start — attacker = active hero of the
  side's player, defender = the monster squad next to it (neutral, no hero), quick = accepted simulation. Hero-vs-hero
  and town battles are not told apart yet; `BA:M` (armies) and `BA:D/B` are unsupported.
* Era's `OW:C`.

### Verification
In game with all 17 mods: an accepted battle ran OnBeforeBattleUniversal and OnAfterBattleUniversal once each
(player 0, hero 105, squad at 57/19/0, quick); Era Erm Framework's `UpdateBattleVars` (BA:Q/P/O/H) no longer reports
unsupported commands. 166 xUnit tests.

## 2026-10-07 — session 2, continued: creature types (MA)

* Found `[V-game]`: an Olden Era unit type is a global `Hex.Configs.UnitLogicConfig` shared by every unit of that
  type; a session `Unit` resolves it from its sid through `Unit.ctzt` (`cnkj` is a cache that stays null). It holds
  `stats` (offence, defence, hp, damageMin/Max, speed, initiative…), `tier`, `fraction`, `upgradeSid`, `squadValue`
  and `unitCost.costResArray` (name, cost). The adapter gets the config of any type through a temporary `Unit`.
* `CreatureTable` (core): MA on a creature with an Olden Era unit changes the game's unit type; MA on an H3/WoG
  creature without one (Tower, Stronghold, Fortress, many WoG creatures) reads zcrtrait.txt of the ERA installation
  and changes nothing in the game. Changes are kept in the WoG state (saved with the game), applied again after
  loading, and undone for a new game — the unit configs live as long as the game process.
* Verified in game: pikeman (esquire) reads attack 4, defence 4, hit points 12, speed 4, 85 gold, level 0, town 0,
  upgrade 1; `MA:A0/10` makes the live config's offence 10. 168 xUnit tests.

## 2026-10-07 — session 2, continued: WoG state with Olden Era saves

* Found `[V-game]`: no `Data.Save` call when the game saves; the event bus raises `MapSaved` (`wb.bupq` = saved,
  `wb.bupr` = path relative to `%USERPROFILE%\AppData\LocalLow\Unfrozen\HeroesOldenEra\users\<user>`), and the file
  appears a moment later. A save is gzip text starting with a 32-hex checksum; a session loaded from it has
  `StartInfo.load` = LoadSave and `StartInfo.fileHashSum` = that checksum (`dbx.me.ctic`).
* Done: at `MapSaved` the WoG state is taken (with !?GM1/OnSavegameWrite) and written, once the save exists, to
  `BepInEx\config\WoG\saves\<checksum>.wog.json`; a session started from a save restores it (or starts a fresh WoG
  state without instructions when the save has none) and does not run day 1 again. Nothing is written into the game's
  save folder.
* Verified in game: autosave and quick save got their state files; v500 = 777, quick save, v500 = 1, quick load →
  v500 = 777, no instructions and no second day 1. 169 xUnit tests.
* Note: every test skirmish leaves an autosave folder `saves\singleplayer\ld_*_07.10.2026_*` in the user's saves.

## 2026-10-07 — session 2, continued: native library functions, UN:J

* **Native library functions** (`src/WoG.Erm/Era/EraNativeLibrary.cs`): stdlib functions of Era Erm Framework,
  WoG Scripts and ERA Scripts whose ERM bodies read H3 memory or call H3 code run as native code under the same name
  and with the same x-parameters: `GetMaxMonsterId`, `GetMaxHeroId`, `GetUpgradedMonster`, `GetTimeMsec`,
  `Array_CountValue`, `Array_IndexOf`, `Array_Merge`, `Array_Slice`, `Array_Shuffle`, `WOG_/ES_PackedCoords`,
  `WOG_/ES_UnPackedCoords` (WoG's PosMixed), `WOG_/ES_CheckRandomMap` (Olden Era's `MapData.generatorChecksum`),
  `WOG_GameMgr_GetPlayer_Me`, `WOG_GameMgr_GetPlayer_Team` (Olden Era alliances).
  Effect at start + day 1: core set `UN:C` 70 → 10, `SN:E` 55 → 1; all 17 mods `UN:C` 103 → 43, `SN:E` 78 → 24.
* **UN:J**: J0 spell bans (kept), J2 difficulty (`StartInfo.settings.AiDifficulty` `[V-game]`: 1 for "low"; scale
  taken as H3's 0..4 `[UNVERIFIED]`), J8/J9 files and folders (the write folder first; J9 hands out the write folder,
  so scripts never write into the ERA installation), J10 variable log, J11; the rest is reported unsupported.
* 171 xUnit tests.

## 2026-10-07 — session 2, continued: HE:S secondary skills on Olden Era

* **What:** `HE:S` works on Olden Era heroes: read a level, learn a skill, raise it, read the hero-screen slots
  (`S$`, `S?slot/skill/1`, `S slot/?skill/1`). The headless engine now also models WoG's display slots (`SShow`/`SSNum`
  from `erm.cpp`: a learned skill takes the next of 8 slots, a forgotten one gives its slot to the last skill).
* **Skill map** `Compatibility/id-maps/skill.json` (by effect, 14 of 28). An H3 skill Olden Era does not have reads as
  not learned (`S7/?v` → 0, `S7/0` is accepted); learning it is `Unsupported`. Olden Era-only skills are not shown to
  scripts.
* **How the game applies a skill** `[V-game]`: a hero's skills are data (`Hero.skills.list`: `HeroSkill` sid, level,
  wasApplied) plus logic (`Logic.Hero.chnk` = `eaj`, one `eah` per applied skill in `eaj.chrt`). Adding only the data
  entry (`HeroSkills.bjfv(sid, level)`) shows nothing and gives no bonus until the save is loaded again (on loading,
  `eaj.bazp(HeroSkill)` runs for every entry — traced 523 calls). So `HE:S` learns with `bjfv` + `eaj.bazp(entry)` and
  raises with `eah.LevelUp()` once per level. Verified in a skirmish: `S3/1` → skill_scouting 1, view radius 7 → 8;
  `S2/2` → skill_logistic 1 → 2, movement bonus 0.10 → 0.15, max movement 201; the hero screen shows the skills, luck
  2 after `S9/2`.
* **Not mapped (honest Unsupported):** lowering or removing a learned skill (its bonuses stay applied), changing the
  number of shown skills or their order (Olden Era lists skills in learning order), raising a skill of a hero that is
  not on the map (no skill logic).
* **Debug:** new bridge command `invoke <path> <method> [arg...]` (numbers, "text", true/false, null, `@path`) to call
  game methods while reverse engineering.
* 175 xUnit tests.

## 2026-10-07 — session 2, continued: HE:M spells on Olden Era; id tables after loading

* **What:** `HE:M` works on Olden Era heroes (read, learn, forget). Spell map `Compatibility/id-maps/spell.json` by
  effect, 37 of 70; a spell Olden Era does not have reads as not known, learning it is `Unsupported`; the spell number
  is checked (0..69) as in WoG.
* **How the game keeps spells** `[V-game]`: data `Hero.magics.list` (`MagicData` sidConfig, level, isLearned) plus the
  magic logic of a hero on the map (`Logic.Hero.chnj` = `eaa`, one `dzx` per spell in `eaa.chqq`). `eaa.baxs(sid,
  false)` learns a spell (data and logic; the spellbook showed Lightning Bolt in the primal school), `eaa.bayb(dzx)`
  removes one (data and logic), `eaa.baxy(sid)` names the hero's `_special` variant: a Web specialist learning
  `night_2_magic_web` gets `night_2_magic_web_special`, so `HE:M54` (Slow) reads 1 for that hero. Verified with the
  new `invoke` bridge command and with `HE:M` in a skirmish.
* **Bug fixed:** loading a saved game replaced the id tables of `Compatibility/id-maps` with the copies in the saved
  WoG state, so a table added after the save (here `spell.json`) was empty after loading. The file tables now win;
  numbers given during the game still come from the save (`IdMap.AdoptFileDomains`, test).
* 177 xUnit tests.

## 2026-10-07 — session 2, continued: HE:A artifacts on Olden Era

* **What:** `HE:A` is a port of WoG's `erm.cpp` (HE Cmd=='A') over hero positions 0..18 worn and 19..82 backpack:
  `A$` into the backpack, `A=$` has it (flag 1), `A-$` removes every copy (`A-1` every scroll), `A1/art/pos` puts an
  artifact into an empty position (flag 1) or reads the one there (artifact 0 reads 1000, scrolls 1001 + spell),
  `A2` counts all and worn copies, `A3` removes copies (the backpack first, or worn ones first), `A4` is the game's
  own "give" (the first suitable empty slot, else the backpack); `A5` (slot locks) is `Unsupported`. The adapter API
  changed to positions (`GetArtifacts`, `PutArtifact`, `RemoveArtifactAt`, `AddToBackpack`, `EquipArtifact`); the
  headless engine keeps gaps in the backpack as H3 does.
* **Artifact map** `Compatibility/id-maps/artifact.json` (61 of 171, by name or effect; Olden Era-only items from
  500); `object.json` regenerated: artifact objects on the map now have their subtype.
* **Olden Era items** `[V-game]`: the doll (`Hero.slots`) and the backpack (`Hero.inventory`) are item containers
  whose slots hold item ids of `Data.items`. Changes go through the item logic of a hero on the map
  (`Logic.Hero.chnh`/`chni`, class `eas`): `bbci(sid, Real, 1)` on the backpack adds an item, `Move(toType, index,
  doll, ANY, k, Real)` puts it on, `bbck(type, index, false, Real)` removes it (also from `Data.items`). `Move` does
  not check the slot type (it put a spyglass on the head), so the adapter checks `ItemConfig`'s slot type and reports
  a wrong slot as `Unsupported`. H3 positions map to Olden Era's slots: neck → belt (Olden Era's sets carry a sash
  where H3 has a necklace), right hand (weapons) → LEFT_HAND, left hand (shields) → RIGHT_HAND, misc 1..4 → ITEM_SLOT,
  misc 5 → UNIQUE_SLOT; there are no war machines and the spellbook is always there.
* **Verified in a skirmish:** `A53`, `A1/53/9`, `A1/22/0`, `A4/40`, `A1018`: spyglass in the backpack and in misc 1,
  Crown of the Supreme Magi on the head, Dragon Scale Armor on the torso, a Lightning Bolt scroll in the backpack;
  attack 2 → 6, defence 3 → 7, knowledge 1 → 5, view radius 7 → 9 (the hero screen shows them); `A1/22/10` →
  `Unsupported` (a crown in a misc slot); `A-53`, `A3/40/1/1`, `A-1`, `A-22` removed everything, the stats went back.
* An earlier experiment left two items without a container in that test session's `Data.items` (a failed `bbci` on
  the doll creates the item before it throws); the test game was not saved.
* 182 xUnit tests.

## 2026-10-08 — session 2, continued: PO (WoG data of map squares)

* **What:** `!!PO` is a port of `erm.cpp` `ERM_Position`: per-square WoG data kept in the WoG state and saved with it
  (`WoGGameState.Squares`, only squares that differ from the start values): `H` "last hero" (8 bits, start 255), `O`
  owner (4 bits signed, start -1), `N` (4 bits), `T`/`S` (8 bits), `V0..3` shorts, `B0..1` longs, `C t/st/h/o/n`
  counts matching squares (-1 = any; untouched squares count with their start values) into v1. The bit fields keep
  only their bits as in WoG (`H300` reads 44, `O9` reads -7); positions are checked against the map size. The engine
  is not involved, so it is fully supported on Olden Era. The unused `Squares` placeholder of the state was replaced.
* Used 207 times in the core mod set. 187 xUnit tests.

## 2026-10-08 — session 2, continued: the WoG Debug window in the game

* **What:** `src/WoG.OldenEra.DebugUI` — a second BepInEx plugin (`wog.oldenera.debugui`, depends on the main one)
  with an in-game console over the WoG Debug commands: a line starting with `!` is ERM code, anything else a debug
  command (`state`, `vars v 1 10`, `help`, ...); buttons State / New day / Self-test / Compatibility / Help; history
  with the arrow keys; errors red, *unsupported* yellow, passes green. F9 or the "WoG" button on the left edge opens
  it; the title drags it. It exists only with WoG Debug on.
* **The game's look, borrowed** `[V-game]`: a new bridge command `ui canvases | tree <name> | sprites <filter> | fonts`
  reads the game's interface. The window uses the sprites of the game's message box (`ScMessageBox`: the 9-piece
  frame `Window_ModalWindow_*`, buttons `buttom`/`buttom_MouseOver`, `buttom_violet`), the input frame of its input
  box (`ScInputBox`: `TextFrame`), the golden scrollbar of the load screen (`Gold_Beck`/`Gold_Top`), `text_background`
  behind the output, and the Amrys fonts with the game's own materials, taken from its visible texts (`RU-Regular` /
  `AmrysRegular...SDF Material`, `RU-Medium` for headers). The game's fonts lack some symbols (✕, arrows): the window
  uses plain letters.
* **Build:** unlike the main plugin this project compiles against Unity's interop assemblies of the installed game
  (`UnityEngine.*`, `Unity.TextMeshPro`, `Il2Cppmscorlib`; engine code, not obfuscated game code, never shipped,
  `Private=false`); without `OLDEN_ERA_DIR` it builds an empty assembly. Nullable is off there: the interop assemblies
  carry their own `System.Runtime.CompilerServices` types (CS0656). `deploy.ps1` builds and installs it.
* **Plumbing:** `DebugCommands.Add(name, run, help)` lets modules add commands; `WoGPlugin.Commands` and the per-frame
  `WoGPlugin.Frame` event.
* **Verified:** the window builds in the main menu, opens, runs `state` typed into its input (the user tried it).
* **Next (the user's idea):** a similar button for a **WoG Options** window with the ERA scripts in it (the WoG/ERA
  options screen), in the same style.
