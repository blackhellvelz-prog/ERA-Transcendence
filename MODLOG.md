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
