# WoG 3.58 reverse engineering — sources and provenance

Everything in this folder is derived from primary sources. Where a statement comes from reading code,
the file and function are named so the next person can re-check it. Nothing here was taken from memory.

## Primary sources used

| # | Source | What it gives us | Notes |
|---|--------|------------------|-------|
| S1 | `GrayFace/wog` on GitHub, folder `T1/` (C++), README: *"Heroes 3.5: In the Wake of Gods 3.59 alpha"* | The WoG engine extension source code: ERM interpreter (`erm.cpp`), Commanders (`npc.cpp`), Stack Experience (`crexpo.cpp`, `crexpo.h`), WoG Options dialog (`wogsetup.cpp`), option flags (`erm.h`) | This is the **3.59 alpha** tree, which descends from the 3.58f source. Every 3.59-only behaviour is marked in the docs as **[3.59]** using the changelogs in `T1/docs/ChangeLog*.txt` and the `// 3.59` comments in the code. Source files are cp1251 encoded (Russian comments). |
| S2 | `GrayFace/wog`, folder `Mods/WoG/ERM/` (216 files) | The original WoG scripts (`script00.erm` … `script76.erm` + `.ert` text files), header `ZVSE`, *"Requires WOG version 3.58f or later"* | Used as a parse corpus and to rank receivers by real-world usage. |
| S3 | ERM help (`Help/ERM-Help.chm` in `ERA-Projects/era-project-eng`) | Official receiver/trigger reference (WoG team ERM help, extended by Era). 306 pages. | Era-only pages (`triggers_ERA`, `trigger_ERA_*`) are excluded from the WoG 3.58 reference. |
| S4 | `Help/wog features.html` (same repo) — *"New Features Added to the In the Wake of Gods AddOn"*, Timothy Pulver, 2004 | Player-facing feature list: 8th level monsters, Commanders, Stack Experience, new artifacts, the 77 WoG scripts (00–76) | |
| S5 | `ethernidee/era` (`Erm.pas`, `Triggers.pas`, `AdvErm.pas`) | Independent re-implementation/extension of the WoG ERM engine (Era). Confirms trigger id ranges and parameter type model. | Era adds triggers 77001+ that are **not** WoG 3.58. They are listed only to be excluded. |
| S6 | `vcmi/vcmi` `config/commanders.json`; `vcmi-mods/wake-of-gods` `Mods/stackExperience/Content/config/*.json` | Independent re-implementation of WoG Commanders and Stack Experience (VCMI). Used as a cross-check of the numbers read from S1. | |
| S7 | `alexandersorokin/heroes3-era-wogify`, folder `Mods/WoG Wogify Scripts 3.58f/Data/s` | 3.58f scripts as packaged for Era | Secondary corpus. |

## Version policy

* **Target behaviour = WoG 3.58f.**
* Where S1 contains 3.59 changes, the 3.58 behaviour is documented and the 3.59 behaviour is listed as an
  optional dialect (`ErmDialect.Wog359Alpha` in code). The ERM runtime defaults to 3.58 semantics but can
  parse the 3.59 additions (`!!la`/`!!go`, local functions `FU-1…-100`, numeric macros, `z-11…z-20`)
  because many later scripts use them.
* Era (S5) behaviour is never used as the reference when it differs from S1.

## What is *not* in these sources and therefore not claimed

* The H3 executable's own (pre-WoG) game rules are not in S1. Where WoG hooks a native H3 routine by
  address (e.g. `0x4E3620`), only the WoG side of the hook is documented. The native behaviour is taken from
  the ERM help (S3) and marked *"per help"*.
* The data files `CREXPMOD.TXT`, `CREXPBON.TXT`, `ZCRTRAIT.TXT`, `ZSETUP00.TXT` ship with a WoG install,
  not with S1. The loaders and their file layout are documented from S1; the numeric contents are read
  from the user's own install at runtime (bring-your-own-files, see `Compatibility/Architecture.md`).
  S6 is used as the cross-check of those numbers.

## Legal hygiene

No WoG/H3 binaries, data files, scripts or help pages are committed to this repository. The docs describe
behaviour in our own words and cite file/function names. Test fixtures that need real scripts read them from
a path given in the `WOG_SCRIPTS_DIR` environment variable.
