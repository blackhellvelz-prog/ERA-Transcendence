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
