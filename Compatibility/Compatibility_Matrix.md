# Compatibility Matrix

The project target is **HoMM3 ERA** (WoG 3.58f + Era 3.9.31 engine + ERM 2.0 + ERA Project scripts). The ERA rows
come first; the WoG rows describe the lower layer on which ERA is built, and they apply to it as well.

Status vocabulary (from the assignment): **FULLY SUPPORTED · PARTIALLY SUPPORTED · EMULATED · WORKAROUND · UNSUPPORTED**.
"Target" is the status achievable on Olden Era given the verified capabilities; the implementation columns show what
is in the repository **now**.

Columns:
* **WoG RE** — reverse engineering of the WoG side: *Full* (from source code), *Partial*.
* **OE support** — what Olden Era itself provides (`OldenEra_ReverseEngineering/06_Capabilities.md`).
* **Core (headless)** — implemented in the engine-independent core and covered by tests.
* **OE adapter** — implemented for Olden Era. *Designed* = the interface and the code path exist in the adapter, but
  depend on symbols with [UNVERIFIED] status; *Not started*.
* **Verified in game** — confirmed in a running Olden Era 0.81.04 with the user's ERA mods (since session 2, see
  `MODLOG.md`); the symbols involved are tagged `[V-game]` in `src/WoG.OldenEra/symbols/wog_symbols.json`.

| WoG feature | WoG RE | OE support | Implementation strategy | Core (headless) | OE adapter | Verified in game | Target | Known limitations |
|-------------|--------|------------|-------------------------|-----------------|------------|------------------|--------|-------------------|
| **ERA:** ERM 2.0 preprocessor (named functions, local variables and arrays, constants, labels, `(FILE)/(LINE)/(CODE)`) | Full (`Erm.pas`) | no | line-by-line port of `PreprocessErm` | **Done + tests; 183 ERA scripts without errors** | not needed | n/a | FULLY SUPPORTED | preprocessor errors go to diagnostics, not to a window |
| **ERA:** parameters (`d-`/`d*`/`d:`/`d%`/`d\|`/`d~`/`d<<`/`d>>`, string parameters, `i^…^`, `s^…^`, `c`), conditions (`&z1`, strings) | Full | no | ERA runtime | **Done + tests** | not needed | n/a | FULLY SUPPORTED | |
| **ERA:** control flow (`if/el&/en`, `re/br/co`, `SN:G`, `SN:Q`, `*_Quit`), event local variables, `FU:P/A/S`, `DO:P` | Full | no | ERA runtime (`ProcessErm`) | **Done + tests** | not needed | n/a | FULLY SUPPORTED | `ErmLegacySupport` = 1, as in the ERA distribution |
| **ERA:** `VR` (rewritten by ERA), named variables, `SN:M/V` arrays, `SN:W/K/X/T/C/I`, ERT strings, `Lang\*.json` translations | Full | counters only | runtime + `EraState` in the saved game | **Done + tests** | not needed | n/a | FULLY SUPPORTED | `SN:X` with strings stores the string index, not a pointer (behavior within a trigger is the same) |
| **ERA:** `SN:F` — Era API functions | Full (`Extern.pas`) | no | own implementations of the functions | **partial: functions called by the project's scripts** | not needed | n/a | PARTIALLY SUPPORTED | ini files, `GetGameState`, sorting by address, plugin functions — no |
| **ERA:** events 77001+ (`OnEveryDay`, `OnGameEnter`, `OnSavegameWrite/Read`, battle, screens, keys…) | Full (table of names and ids) | partial | the adapter generates the events | table + PI/GameEnter/EveryDay/save/load | Designed | No | PARTIALLY SUPPORTED | H3 screen and key events — as OE hooks become available |
| **ERA:** mod and script load order (`lib`, global, `lib_end`, priority in the name, VFS) | Full | — | `EraScriptSet` | **Done** | not needed | n/a | FULLY SUPPORTED | name sorting is an approximation of `AnsiCompareText` |
| **ERA:** H3 data of the ERA installation (LOD/PAC archives, Era's VFS order, text tables: artifacts, creatures, spells, skills) | Full | — | read-only reader of the user's installation | **Done + tests** | read at start | **Yes** | FULLY SUPPORTED | nothing is copied into the repository |
| **ERA:** `SN:E`, `UN:C`, `SN:B/L/A`, array addresses | Full | impossible | — | — | — | — | UNSUPPORTED | H3 process code and memory |
| **ERA:** plugin receivers and functions (`SS`, `PA`, `QU`, `RD`, WoG dialogs, HD mod) | no sources | — | reimplementation from behavior | recognized, UNSUPPORTED | Not started | No | UNSUPPORTED (for now) | closed-source DLLs |
| ERM parser (syntax, parameters, conditions, strings) | Full | no | own parser (`WoG.Erm.Syntax`) | **Done + tests; 78/78 3.58f scripts and 117 3.59 files without errors** | not needed | n/a | FULLY SUPPORTED | 3.59 additions — behind a dialect flag; Era triggers are not supported |
| ERM variables, flags, strings, floating-point values, macros, indirect addressing | Full | counters only | runtime + persisted state | **Done + tests** | not needed | n/a | FULLY SUPPORTED | ERT strings (`z>1000`) are not loaded yet |
| ERM control flow (section order, FU/DO, if/el/en, FU:E, la/go) | Full | no | runtime | **Done + tests** | not needed | n/a | FULLY SUPPORTED | |
| ERM timers (TM) | Full | turn-start condition | runtime + day hook | **Done + tests** | **Done** (`turn.start` = `ebe.OnStartDay`; day 1 once the map is ready) | **Yes** | FULLY SUPPORTED | |
| ERM triggers: hero/object/battle/level/step/mouse | Full | partial (visit/turn/kill/hero battles) | the adapter generates WoG events | event model and bridge are ready | **object visits done**: `fnt.bmjj` → `!?OB` before the object acts and before its dialog, `fnt.bmiw` → `!$OB` after its action; battle, level-up, step, mouse: not started | **object visits: yes** (resource, treasure chest) | PARTIALLY SUPPORTED | mouse triggers depend on UI hooks; network (IP) is not needed |
| Receivers `VR FU DO MC TM` | Full | — | runtime | **Done + tests** | not needed | n/a | FULLY SUPPORTED | `FU:D` (network) — UNSUPPORTED |
| `IF` messages/questions/flags | Full | dialogs | UI adapter | **Done (M, Q, V, W, A, S, R) + tests** | Designed (`ui.*`) | No | PARTIALLY SUPPORTED | dialogs with pictures/multiple choice require a custom UI |
| `UN:P` options | Full | — | core options | **Done + tests** | not needed | n/a | FULLY SUPPORTED | |
| `UN` other (objects, memory) | Partial | object creation/removal via actions | adapter + H3 tables of the ERA installation | **A V X U N R done + tests** | **X U N R** on the Olden Era map; A from the ERA tables (not linked to Olden Era items) | **Yes** (X, U, N, R) | PARTIALLY SUPPORTED | `UN:C` (memory write) — UNSUPPORTED in principle; I O S H J K M T B: not started |
| `HE` hero | Full (main commands) | hero model via plugin | adapter | **Done (E F I W S M A C O P N K) + tests** | **Done: E F I W O, army (C), position (P, read), secondary skills (S: learn, raise with the game's sub-skill choice, read; hero-screen slots read), experience (E: gains level the hero up in the game's window), spells (M: learn, forget, read), artifacts (A: worn and backpack positions, give, equip, count, remove)** | **Yes** | PARTIALLY SUPPORTED | H3 heroes do not exist in Olden Era: WoG numbers 0..155 are given to the game's heroes; skills by effect (`id-maps/skill.json`, 14 of 28), lowering a skill and reordering: not mapped; spells by effect (`id-maps/spell.json`, 37 of 70); artifacts by name or effect (`id-maps/artifact.json`, 61 of 171), items fit only their own slot type, no war machines; moving: not started |
| `OW` players | Full (main parts) | resources via the game's methods | adapter | **Done (R C A I G) + tests** | **Done** (resources through the game's own add/spend, so the UI updates) | **Yes** | PARTIALLY SUPPORTED | 7 H3 resources ↔ Olden Era resources (IdMap); no sulfur |
| `MA` creature type data | Full | unit type configs (`UnitLogicConfig`, via `Unit.ctzt`) | creature table: engine units, else the ERA installation's zcrtrait.txt; changes kept in the WoG state | **Done + tests** | **Done**: attack, defence, hit points, speed, damage, cost, unit value; level/town/upgrade read only | **Yes** | PARTIALLY SUPPORTED | Olden Era units have no shots/casts/growth/adventure-map counts; creatures without an Olden Era unit change nothing in the game |
| `CA` towns | Partial | buildings via actions | adapter | Not started | Not started | No | PARTIALLY SUPPORTED | OE town/building ids are entirely different |
| Map object receivers (`OB MN DW CB …`) | Partial | object logic families | adapter + object type map (`id-maps/object.json`) | **OB:T/U, TR:T/P/E + tests** | **Done: map layer** (objects, monsters, heroes, squares, terrain) | **Yes** | PARTIALLY SUPPORTED | changing objects and terrain, OB:D/E/R/S/M/H/B, MN DW CB MO…: not started |
| Battle receivers (`BA BM BU BG BH BF MR MF`) | Partial | no native equivalent | battle adapter (Harmony) + buffs | interfaces only | Designed | No | EMULATED | depends on battle symbols |
| WoG Options system | Full | no | core options + plugin UI | **Done + tests** | UI not started | No | FULLY SUPPORTED | option texts come from the user's `ZSETUP00.TXT`; the `.dat` preset format is not confirmed |
| Commanders (state, experience, levels, skills, special bonuses, artifacts, hiring/resurrection, ERM `CO`) | Full | **no equivalent** | WoG.Commanders + emulated unit in battle | **Done + tests** | Designed | No | EMULATED | combat special abilities require hooks; the UI is new |
| Stack experience (records, gaining, ranks, merging, ERM `EX`) | Full | no | WoG.CreatureExperience + rank buffs | **Done + tests** | Designed | No | EMULATED | bonus tables come from the user's `CREXPBON.TXT`/`CREXPMOD.TXT` (or VCMI data); `EX` by map position — not yet |
| Stack experience bonuses in battle | Full | buffs (V-data) | generated per-rank buffs + hooks | **buff generator done + test** | Designed (`buff.apply`) | No | PARTIALLY SUPPORTED | chance-based abilities and flags — via hooks |
| Commander/stack artifacts (146–156) | Full | items + buffs | clones + WoG state | commander artifacts done | Not started | No | EMULATED | |
| 8th-level creatures, WoG creatures | Partial | unit clones (V-community) | overlay + recolor | unit cloning done (test) | Not started | No | PARTIALLY SUPPORTED | new models are not created (asset policy) |
| WoG scripts 00–76 (objects, rules, upgrades) | script corpus parsed | — | execution by the ERM runtime | **all 78 3.58f files load as a new game, 0 errors** | depends on receivers | No | PARTIALLY SUPPORTED | each script's status = the status of its receivers (`ERM_Compatibility.md`) |
| Saving/loading WoG state | Full | saves are gzip text starting with a checksum; `StartInfo.fileHashSum` after loading | state file per save in `BepInEx/config/WoG/saves/<checksum>.wog.json` + ownership check | **Done + round-trip tests** | **Done**: `MapSaved` (path) → state file; session from a save → state restored, no instructions, no second day 1; a save without WoG state gets a fresh one | **Yes** (quick save / quick load, autosave) | WORKAROUND | the state stays on this PC (not inside the Olden Era save file) |
| Visual resources | — | OE models/icons | `VisualRef` + priority-based resolver | **Done (resolver) + test** | placeholders | No | PARTIALLY SUPPORTED | placeholders are allowed by the policy |
| Multiplayer / network ERM (`IP`, `FU:D`) | Partial | n/a | — | — | — | — | UNSUPPORTED | project rule: single-player only |
| Memory-level ERM (`UN:C`, addresses) | Full | impossible | — | — | — | — | UNSUPPORTED | different engine, no addresses |

The per-command ERM table is generated from the code: `dotnet run --project tools/WoG.ErmTool -- compat`
→ `ERM_Compatibility.md`.

## Why some things are not FULLY SUPPORTED (as required by the assignment)

| What | Why it cannot be full | What was tried | Engine limitation | How much is reproducible | Possible path |
|------|-----------------------|----------------|-------------------|--------------------------|---------------|
| `UN:C` | writes to H3 memory addresses | — | different engine | 0 % | rewrite the specific scripts that use `UN:C` in terms of receivers |
| `SN:E` (ERA) | calls an H3 exe function by address | — | different engine | 0 % | for frequent addresses — a custom function with the same meaning (based on the call site in the script) |
| ERA plugins | closed-source DLLs | — | no sources | 0 % for now | describe the behavior from documentation/experience and reimplement it |
| Commanders as an entity | OE has no "special hero unit" | data analysis (`units`, `heroes`) | no such entity | estimate: everything except the native UI (not measured) | emulation: unit clone + WoG state + uGUI |
| Creature speed | in OE it is split into `initiative` and `speed` | analysis of `stats` | different battle model | approximately | change both stats by the same delta (this is what the buff generator does) |
| Creature shots/casts | no such stat | analysis of `stats`/buffs | energy and abilities instead | undetermined | find the ammunition model in the game (RE plan, item 3.4) |
| Save format | unknown | — | closed format | 100 % of WoG state, but outside the save | file next to the save + ownership check |
