# WoG 3.58 Reverse Engineering — Sources and Their Provenance

Everything in this folder is derived from primary sources. Where a claim was obtained by reading code, the file and
function are given so that anyone can re-check it. Nothing is taken "from memory".

## Primary sources used

| # | Source | What it provides | Notes |
|---|--------|------------------|-------|
| S1 | GitHub `GrayFace/wog`, folder `T1/` (C++), README: *"Heroes 3.5: In the Wake of Gods 3.59 alpha"* | Source code of the WoG engine extension: the ERM interpreter (`erm.cpp`), commanders (`npc.cpp`), stack experience (`crexpo.cpp`, `crexpo.h`), the WoG Options dialog (`wogsetup.cpp`), option flags (`erm.h`) | This is the **3.59 alpha** branch, which grew out of the 3.58f sources. Everything that appeared only in 3.59 is marked in the documents as **[3.59]** — based on the changelogs `T1/docs/ChangeLog*.txt` and the `// 3.59` comments in the code. The files are cp1251-encoded (comments in Russian). |
| S2 | `GrayFace/wog`, folder `Mods/WoG/ERM/` (216 files) | WoG scripts (`script00.erm` … `script76.erm` + text `.ert` files), `ZVSE` header | These are 3.59 versions (they use `!!SS`, etc.) — used as a second corpus for the parser. |
| S3 | ERM help (`Help/ERM-Help.chm` in `ERA-Projects/era-project-eng`) | Official reference of receivers/triggers (the WoG team's help, extended by Era), 306 pages | The Era pages (`triggers_ERA`, `trigger_ERA_*`) are not part of the WoG 3.58 reference. |
| S4 | `Help/wog features.html` (same repository) — *"New Features Added to the In the Wake of Gods AddOn"*, Timothy Pulver, 2004 | Player-facing description of the features: level-8 monsters, commanders, stack experience, new artifacts, 77 WoG scripts (00–76) | |
| S5 | `ethernidee/era` (`Erm.pas`, `Triggers.pas`, `AdvErm.pas`) | An independent rework/extension of ERM (Era). Confirms the trigger id ranges and the parameter type model | Era adds triggers 77001+, which do **not** exist in WoG 3.58 — they are mentioned only so that they can be recognized and rejected. |
| S6 | `vcmi/vcmi` `config/commanders.json`; `vcmi-mods/wake-of-gods` `Mods/stackExperience/Content/config/*.json` | An independent implementation of commanders and stack experience (VCMI) — for cross-checking the numbers from S1 | |
| S7 | `alexandersorokin/heroes3-era-wogify`, folder `Mods/WoG Wogify Scripts 3.58f/Data/s` | 3.58f scripts (78 files) packaged for Era | **Main corpus**: receiver usage statistics and the parser test. |

## Version policy

* **Reference behavior = WoG 3.58f.**
* Where S1 contains 3.59 changes, the 3.58 behavior is documented, and the 3.59 behavior is noted as an
  optional dialect (`ErmDialect.Wog359Alpha` in the code). By default the ERM runtime follows 3.58
  semantics, but it can parse the 3.59 additions (`!!la`/`!!go`, local functions `FU-1…-100`,
  numeric macros, `z-11…z-20`), since many later scripts use them.
* Era behavior (S5) is never taken as the reference where it diverges from S1.

## What the sources do not contain (and is therefore not claimed)

* S1 does not contain the H3 executable's own (pre-WoG) rules. Where WoG hooks a native H3 function
  by address (for example `0x4E3620`), only the WoG side is documented. The native behavior is taken from the
  ERM help (S3) and marked *"per the help"*.
* The data files `CREXPMOD.TXT`, `CREXPBON.TXT`, `ZCRTRAIT.TXT`, `ZSETUP00.TXT` ship with the WoG
  installation, not with S1. The loaders and the file formats are documented from S1; the numeric contents are read
  at runtime from the user's own installation (the "your own game files" principle, see `Compatibility/Architecture.md`).
  S6 serves as a cross-check of these numbers.
* The functions `SaveSetupState`/`LoadSetupState` (option presets) are implemented in the closed-source `ZvsLib1.dll` —
  the preset file format is **not confirmed**.

## Legal hygiene

No WoG/H3 binaries, data files, scripts or help pages are committed to the repository. The documents
describe the behavior in their own words, with references to files/functions. Tests that need the real scripts
receive the path to them through the `WOG_SCRIPTS_DIR` environment variable.
