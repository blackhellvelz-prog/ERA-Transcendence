# Architecture — WoG 3.58 on Olden Era

```
                    WoG 3.58 scripts (.erm) — run unchanged
                                │
                       ┌────────┴────────┐
                       │   WoG.Erm        │  parser → IR → interpreter → receivers
                       └────────┬────────┘
                                │ IWoGServices (interfaces only)
   ┌──────────────┬─────────────┼──────────────┬───────────────┐
   │ WoG.Core      │ WoG.Commanders│ WoG.CreatureExperience │ (future: WoG.MapObjects, WoG.Towns, WoG.Battle)
   │ state, model, │ rules + state │ rules + state          │
   │ options, save,│               │                        │
   │ events, ids   │               │                        │
   └──────┬───────┴──────┬────────┴──────────┬─────────────┘
          │  IGameAdapter (hero/army/player/creature/map/town/ui/battle/visual)
   ┌──────┴───────────────────────────────────────────┐
   │ WoG.Headless (reference engine, tests, ErmTool)   │  WoG.OldenEra (BepInEx 6 IL2CPP plugin)
   └───────────────────────────────────────────────────┘  ├─ OldenEraSymbols (name/signature resolution)
                                                          ├─ OldenEraGameAdapter (Harmony + Il2CppInterop)
                                                          ├─ OldenEraProbes (capability verification)
                                                          └─ DataOverlayBuilder (wog_core.zip)
```

## Rules enforced by the code structure

1. **WoG Core knows nothing about Olden Era.** `WoG.Core`, `WoG.Erm`, `WoG.Commanders`,
   `WoG.CreatureExperience` have no reference to Unity/BepInEx/IL2CPP. They compile and are tested on
   plain .NET (`tests/WoG.Tests`).
2. **Engine-specific code lives only in `WoG.OldenEra`.** It implements `IGameAdapter`.
3. **Every adapter call returns an `AdapterResult`** with `Supported` / `Unsupported(reason)` /
   `Failed(reason)`. The ERM runtime turns `Unsupported` into a logged compatibility event instead of
   pretending the command worked (project rule 6). `CompatibilityReport` aggregates them per
   receiver/command for the matrix.
4. **Optional modules are services.** `CO` and `EX` receivers look up `ICommanderService` /
   `IStackExperienceService` in `IWoGServices`; with the module disabled the receivers report
   `Unsupported("module disabled")` and nothing else breaks.
5. **Gameplay/visual separation.** Gameplay code refers to visuals only through `VisualRef`
   (logical keys like `creature:wog.commander.castle`, `icon:artifact:146`). `IVisualAdapter` resolves
   them by the asset priority policy (OE asset → recolour → material/texture → VFX → imported H3/WoG 2D →
   placeholder). Replacing an icon never touches gameplay code.
6. **State is centralised and persisted.** `WoGGameState` holds options, ERM variables/flags/strings/
   macros/timers, commanders, stack experience, per-object and per-square ERM data, id map. It is
   serialised by `WoGSaveSerializer` (versioned JSON + SHA-256) next to every save.

## Identity mapping

ERM scripts address the world by **H3 numbers** (hero 0…155, creature 0…196, artifact 0…170, spell,
skill, positions). Olden Era uses **string sids** and its own map. `IdMap` translates in both directions
and is part of the save. Mapping data lives in `Compatibility/id-maps/*.json`; unmapped ids return
`Unsupported("no mapping for creature 151")` — again never silently replaced.

## Battle

`IBattleAdapter` exposes the WoG battle lifecycle (start, field setup, round, action pre/post, end) and
stack get/set. The Olden Era implementation patches the combat controller (symbols from the in-game RE
plan). Stat effects are applied as **generated buffs** (one buff per WoG effect and rank, shipped in the
overlay zip) so they act on the real simulation; effects buffs cannot express are implemented as
Harmony hooks on damage/resistance/action methods.

## Commanders without a native equivalent

A commander is emulated as: (1) WoG state (`WoGCommander`, persisted); (2) an extra battle unit
created at battle start from a dedicated creature definition per class (cloned OE unit, recoloured —
`creature:wog.commander.<class>`), whose stats are set from `CommanderRules.BattleProfile`; (3) a hero
screen button opening the commander window (uGUI from the plugin). Death/revival/exp follow
`04_Commanders.md`.

## Data overlay

`DataOverlayBuilder` (tools + plugin) produces `wog_core.zip` from WoG data: cloned creatures for
commanders and WoG creatures, buffs for stack experience ranks and commander bonuses, artifacts
146–156, localisation. Placed next to `Core.zip` (verified mechanism). Contains no game files — only
JSON derived from WoG tables and references to existing OE assets.
