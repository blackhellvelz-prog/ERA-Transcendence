# Compatibility matrix

Status vocabulary (project brief): **FULLY SUPPORTED · PARTIALLY SUPPORTED · EMULATED · WORKAROUND ·
UNSUPPORTED**. "Target" is the status the design can reach on Olden Era given the verified
capabilities; "Now" is what exists in this repository today.

Columns:
* **WoG RE** — reverse-engineering of the WoG side: *Complete* (from source code), *Partial*.
* **OE support** — what Olden Era offers natively (`OldenEra_ReverseEngineering/06_Capabilities.md`).
* **Core (headless)** — implemented in the engine-independent WoG Core and covered by tests.
* **OE adapter** — implemented against Olden Era. *Designed* = interface + adapter code path exist but
  depend on [UNVERIFIED] symbols; *Not started*.
* **In-game verified** — confirmed in a running Olden Era. **Nothing is yet**, because this work was done
  without a copy of the game (see `OldenEra_ReverseEngineering/00_Sources_and_Verification.md`).

| WoG feature | WoG RE | OE support | Implementation strategy | Core (headless) | OE adapter | In-game verified | Target | Known limitations |
|-------------|--------|-----------|------------------------|-----------------|------------|------------------|--------|-------------------|
| ERM parser (syntax, params, conditions, strings) | Complete | None | own parser (`WoG.Erm.Syntax`) | **Done + tests** | n/a (engine-free) | n/a | FULLY SUPPORTED | 3.59 additions parsed; Era extensions rejected with a message |
| ERM variables, flags, strings, floats, macros, indirection | Complete | None (counters only) | runtime + persisted state | **Done + tests** | n/a | n/a | FULLY SUPPORTED | |
| ERM control flow (triggers order, FU/DO, if/el/en, FU:E, la/go) | Complete | None | runtime | **Done + tests** | n/a | n/a | FULLY SUPPORTED | |
| ERM timers (TM) | Complete | turn start condition | runtime + day hook | **Done + tests** | Designed | No | FULLY SUPPORTED | needs verified "new day" hook |
| ERM triggers: hero/object/battle/level/move/mouse | Complete | partial (visit/turn/kill/hero battle) | adapter raises WoG events | event model done | Designed | No | PARTIALLY SUPPORTED | mouse/click triggers depend on UI hooks; network (IP) not applicable |
| `VR FU DO MC` receivers | Complete | — | runtime | **Done + tests** | n/a | n/a | FULLY SUPPORTED | `FU:D` (network) unsupported |
| `IF` messages/questions | Complete | dialogs | UI adapter | **Done (M,Q,V,W,X) + tests** | Designed | No | PARTIALLY SUPPORTED | multi-choice/picture dialogs need custom UI |
| `UN:P` options | Complete | — | Core options | **Done + tests** | n/a | n/a | FULLY SUPPORTED | |
| `UN` other (object placement, memory) | Partial | spawn/delete objects via actions | adapter | stubs report Unsupported | Not started | No | PARTIALLY SUPPORTED | `UN:C` memory poke: UNSUPPORTED by design |
| `HE` hero receiver | Complete (core cmds) | hero model via plugin; scenario actions for exp/stats/mana/units/items/skills/spells | adapter | **Done (E F I W S M A C O P N K) + tests** | Designed | No | PARTIALLY SUPPORTED | H3 has 4 primary skills, OE has 6 (offence, defence, spellPower, intelligence, luck, moral) — mapped A→offence, D→defence, P→spellPower, K→intelligence; 28 H3 secondary skills ≠ OE skill set (id map) |
| `OW` players | Complete (core) | resources via actions | adapter | **Done (R C A I) + tests** | Designed | No | PARTIALLY SUPPORTED | 7 H3 resources vs OE resource set (id map) |
| `MA` creature type data | Complete | static JSON; runtime change needs plugin | adapter | **Done + tests** | Designed | No | PARTIALLY SUPPORTED | OE splits speed into initiative+speed; shots not a stat |
| `CA` towns | Partial | buildings via actions | adapter | Not started | Not started | No | PARTIALLY SUPPORTED | OE town/building ids differ completely |
| Map object receivers (`OB MN DW CB …`) | Partial | object logic families | adapter + clones | Not started | Not started | No | PARTIALLY SUPPORTED | |
| Battle receivers (`BA BM BU BG BH BF MR MF`) | Partial | none native | battle adapter (Harmony) + buffs | interfaces only | Designed | No | EMULATED | depends on combat symbols |
| WoG Options system | Complete | none | Core options + plugin UI | **Done + tests** | UI not started | No | FULLY SUPPORTED | option texts from user's `ZSETUP00.TXT` |
| Commanders (state, exp, levels, skills, bonuses, artifacts, hire/revive, ERM `CO`) | Complete | **no equivalent entity** | WoG.Commanders + emulated battle unit | **Done + tests** | Designed | No | EMULATED | battle-side special abilities need hooks; UI new |
| Stack experience (records, gain, ranks, merge, ERM `EX`) | Complete | none | WoG.CreatureExperience + rank buffs | **Done + tests** | Designed | No | EMULATED | bonus tables from user's `CREXPBON.TXT`/`CREXPMOD.TXT` or VCMI data |
| Stack exp bonuses in combat | Complete | buffs (V-data) | generated buffs per rank + hooks | profile calc done | Designed | No | PARTIALLY SUPPORTED | chance-based abilities need hooks |
| Commander/stack artifacts (146–156) | Complete | items + buffs | clones + WoG state | commander arts done | Not started | No | EMULATED | |
| 8th-level creatures, WoG creatures | Partial | clone units (V-community) | overlay zip + recolour | Not started | Not started | No | PARTIALLY SUPPORTED | new models not created (asset policy) |
| WoG scripts 00–76 (objects, rules, enhancements) | corpus parsed | — | run through ERM runtime | **all 3.58f scripts parse** (test) | depends on receivers | No | PARTIALLY SUPPORTED | each script inherits the status of the receivers it uses (`ERM_Compatibility.md`) |
| Save/load of WoG state | Complete | unknown native format | side-car `.wog.json` + identity check | **Done + round-trip tests** | Designed | No | WORKAROUND | requires save/load hook or file watcher |
| Visual assets | — | OE models/icons | VisualRef + priority resolver | **Done (resolver) + tests** | Designed | No | PARTIALLY SUPPORTED | placeholders allowed |
| Multiplayer / network ERM (`IP`, `FU:D`) | Partial | n/a | — | — | — | — | UNSUPPORTED | project rule: single-player only |
| Memory-level ERM (`UN:C`, raw addresses) | Complete | impossible | — | — | — | — | UNSUPPORTED | different engine; no addresses |

This table is regenerated in part from code: `dotnet run --project tools/WoG.ErmTool -- compat`
prints per-receiver/per-command status (`ERM_Compatibility.md`).
