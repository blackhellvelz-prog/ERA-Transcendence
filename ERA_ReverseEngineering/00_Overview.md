# HoMM3 ERA: what it is and why the project's target is now ERA

## Decision

The user asked to port not WoG 3.58 but **ERA**, a more advanced build based on WoG
(`ERA-Projects/era-project-eng`, `ERA-Projects/era-project-rus`). ERA is now the behavioral reference.
WoG 3.58 remains the "lower layer": ERA is built on WoG, and everything already done for WoG (commanders,
stack experience, WoG Options, WoG receivers) continues to be used.

## What ERA is

ERA (Era Project; engine author: Berserker / Alexander Shostak, `ethernidee`) is a mod platform for
Heroes III built on top of WoG:

| Layer | What it is | Where to get the behavior source |
|-------|------------|----------------------------------|
| Era 3.9.31 engine (`era.dll`) | new ERM interpreter (ERM 2.0), loading of scripts and mods, saved games, translations, plugin API | `github.com/ethernidee/era` (Delphi): `Erm.pas`, `AdvErm.pas`, `Triggers.pas`, `Trans.pas`, `Extern.pas`, `Stores.pas` |
| WoG (`WoG` mod) | WoG 3.58f + fixes, "WoG 3.59 TE" in the version string | WoG sources (`GrayFace/wog`), see `WoG_ReverseEngineering/` |
| WoG scripts (`WoG Scripts` mod) | ~90 WoG 3.58f scripts rewritten for ERA (ERM 2.0, named functions) | ERA Project repository |
| ERA Scripts (`ERA Scripts` mod) | ~90 new option scripts (bank, bounty hunting, third class, etc.) | ERA Project repository |
| Era Erm Framework | ERM standard library (`lib\9999 era - stdlib.erm`, constants) | ERA Project repository |
| Plugins (`*.era`, `*.dll` in `EraPlugins`) | closed binaries: new receivers (`SS`, `PA`, `QU`), WoG dialogs, object extensions | no sources |
| Other project mods | Game Enhancement Mod, Advanced Classes, Mixed Neutrals, etc. | ERA Project repository |

Versions examined (re-check them when updating):

| Source | Version / commit | Date |
|--------|------------------|------|
| `ethernidee/era` | `8c2fd47` (`ERA_VERSION_STR = '3.9.31'`) | 2026-09-23 |
| `ERA-Projects/era-project-eng` | `1229a9b` ("2.291 update") | 2026-10-06 |
| `ERA-Projects/era-project-rus` | `48d9717` ("2.291 update") | 2026-10-06 |
| `ethernidee/b2` (engine library: `TextScan`, `StrLib`) | `b26cfca` | 2026-09-23 |

Licenses: ERA Project is MIT (`LICENSE` in the repository). The Era engine and B2 repositories have no license:
their sources are only **read**, and the behavior is reimplemented from scratch in C#. No Era code is copied into
our repository, and no ERA/WoG/H3 files are distributed: the port reads them from the user's installation.

## What ERA adds to WoG (and what of it is ported)

| ERA feature | Port | Where in the code |
|-------------|------|-------------------|
| ERM 2.0: named functions, local variables, arrays, constants, labels | **fully** (the ERA preprocessor is ported line by line) | `src/WoG.Erm/Era/EraPreprocessor.cs` |
| New parameter parsing (`d-`, `d*`, `d\|`… strings as parameters, `i^…^`, `s^…^`) | **fully** | `src/WoG.Erm/Syntax/ErmParserEra.cs` |
| New interpreter: per-event local variables, `re/br/co`, `el&…`, `SN:G`, `SN:Q`, `*_Quit` | **fully** | `src/WoG.Erm/Runtime/EraProcess.cs` |
| ERA values, conditions, `%…` interpolation | **fully** | `src/WoG.Erm/Runtime/EraValues.cs` |
| Rewritten `VR`, `FU`, `DO`; new `SN` | `VR/FU/DO` fully; `SN`: everything except calls into H3 code | `src/WoG.Erm/Receivers/EraReceivers.cs` |
| About 110 named events (`OnEveryDay`, `OnGameEnter`, …) | id table ported; event generation follows as Olden Era hooks become available | `src/WoG.Erm/Era/EraEvents.cs` |
| Mod script load order, `.ert` files, `Lang\*.json` translations | **fully** | `EraScriptSet.cs`, `WoGHost.PrepareEraScripts`, `EraLang` |
| Saving: named variables, arrays, ERT strings, function names | **fully** (into the port's state file) | `src/WoG.Core/State/EraState.cs` |
| `SN:F`: calling Era API functions | partially: the functions that the project's scripts call | `src/WoG.Erm/Receivers/EraApi.cs` |
| `SN:E`, `UN:C`, `SN:B/L/A`: calling H3 code and H3 process memory | **impossible** (different engine) | marked UNSUPPORTED |
| Plugins (`SS`, `PA`, `QU`, WoG dialogs, HD mod) | no sources | UNSUPPORTED until a separate implementation |

## Verification on the real ERA corpus

* Preprocessor: 183 scripts from the mods `Era Erm Framework`, `ERA Scripts`, `WoG Scripts`, `WoG`: **0 errors**;
  1115 function names, 1991 constants.
* Parser: 2277 sections, 37,958 command lines: **0 errors**.
* New game + 7 days on the headless reference engine: **0 runtime errors**. Unsupported calls at
  startup: `UN:C` (70, H3 memory), `SN:E` (55, H3 code), `FU:D` (28, network), `UN:A/R/X/N/U/V/J` (35, not yet
  mapped onto the Olden Era map), `SN:L/B` (4), `IF:G` (2).
* The `EraCorpusTests` test (variable `ERA_MODS_DIR` → the ERA project's `Mods` folder) repeats this run.

Most frequent receivers in the ERA corpus (after the preprocessor): `VR` 13,039, `FU` 4021, `en` 3317, `UN` 2855,
`if` 2618, `SN` 2244, `HE` 2021, `IF` 1547, `el` 1002, `MA` 795, `re` 698, `BM` 586, `OW` 547, `CA` 382,
`DO` 304, `PO` 207, `co` 161, `CM` 161, `DL` 145, `br` 128, `OB` 121, `BU` 96, `CO` 91, `BG` 84, `BA` 84.
`SN` subcommands: `M` 412, `H` 361, `T` 351, `E` 311, `W` 302, `F` 162, `X` 52, `V` 50, `B` 44, `P` 43,
`D` 39, `Q` 35, `K` 25, `R` 24, `O` 15, `L` 9, `A` 7, `G` 1, `C` 1.

## Documents in this folder

* `01_ERM2_Preprocessor.md` — the ERM 2.0 preprocessor: names, local variables, constants, labels.
* `02_ERA_Semantics.md` — how the ERA interpreter differs from WoG: parameters, conditions, control flow,
  local variables, functions, strings, `VR`, `SN`, API.
* `03_ERA_Events_Loading_Save.md` — ERA events, mod and script load order, translations, ERT, saving.
