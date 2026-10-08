**English** | [Русский](Architecture.ru.md)

# Architecture — WoG 3.58 on Olden Era

```
                 WoG 3.58 scripts (.erm) — run unmodified
                                │
                       ┌────────┴────────┐
                       │   WoG.Erm        │  parser → IR → interpreter → receivers
                       └────────┬────────┘
                                │ IWoGServices (interfaces only)
   ┌──────────────┬─────────────┼──────────────┬────────────────────┐
   │ WoG.Core      │ WoG.Commanders│ WoG.CreatureExperience │ (next: MapObjects, Towns, Battle)
   │ state, model, │ rules +       │ rules + state           │
   │ options, save,│ state         │                         │
   │ events        │               │                         │
   └──────┬───────┴──────┬────────┴──────────┬──────────────┘
          │  WoG.Host — composition root: modules + ERM runtime + WoG → ERM event bridge
          │  IGameAdapter (heroes/armies/players/creatures/map/towns/UI/battle/clock)
   ┌──────┴───────────────────────────────────────────┐
   │ WoG.Headless (reference engine: tests, ErmTool)   │  WoG.OldenEra (BepInEx 6 IL2CPP plugin)
   └───────────────────────────────────────────────────┘  ├─ OldenEraSymbols (game symbols from wog_symbols.json)
                                                          ├─ OldenEraGameAdapter (Il2CppInterop via symbols)
                                                          ├─ WoGPlugin + Hooks (Harmony → WoG events)
                                                          └─ WoG.OldenEra.Data (Core.zip → wog_core.zip)
```

## Rules enforced by the code structure

1. **The WoG core knows nothing about Olden Era.** `WoG.Core`, `WoG.Erm`, `WoG.Commanders`,
   `WoG.CreatureExperience`, `WoG.Host` do not reference Unity/BepInEx/IL2CPP. They are built and tested on
   plain .NET (`tests/WoG.Tests`, 100 tests).
2. **Engine-specific code lives only in `WoG.OldenEra`.** It implements `IGameAdapter`.
3. **Every adapter call returns an `AdapterResult`**: `Ok` / `Unsupported(reason)` / `Failed(reason)`.
   The ERM runtime turns `Unsupported` into a compatibility report entry instead of pretending the command worked
   (rule 6). `CompatibilityReport` aggregates the entries by receiver/command — this is the evidence base for the matrix.
4. **Optional modules are services.** The `CO` and `EX` receivers look up `ICommanderService` /
   `IStackExperienceService` in `IWoGServices`; when the module is disabled (`WoGModules`), they report
   `Unsupported("… module disabled")`, and everything else keeps working (test `Disabled_module_reports_unsupported`).
5. **Gameplay and visuals are separated.** Gameplay code refers to graphics only through `VisualRef` —
   logical keys such as `creature:wog.commander.castle`, `icon:artifact:146`. `IVisualResolver` picks the
   resource by a mandatory priority order: OE asset → recolor → material/texture → VFX → imported H3/WoG 2D
   art → placeholder. Replacing an icon never touches gameplay code; missing graphics never block the
   game (the resolver always returns at least a placeholder).
6. **State is centralized and saved.** `WoGGameState` holds options, ERM variables/flags/strings/macros/
   timers, commanders, stack experience, object and tile data, creature overrides, and the `IdMap`.
   It is serialized by `WoGSaveSerializer` (versioned JSON + SHA-256) next to each saved game.

## Identity mapping

ERM scripts address the world by **H3 numbers** (hero 0…155, creature 0…196, artifact 0…170, spell,
skill, positions). Olden Era uses **string sids** and its own map. `IdMap` translates in both directions and is part
of the save. The mapping data lives in `Compatibility/id-maps/*.json` (the plugin reads it from `BepInEx/config/WoG/id-maps`);
an unmapped id returns `Unsupported("… has no WoG creature mapping")` — again, with no silent substitution.

## Battle

`IBattleAdapter` describes the WoG battle lifecycle (start, battlefield setup, round, before/after action, end)
and reading/writing stacks. The Olden Era implementation patches the battle controller (the `battle.*` symbols from
the reverse-engineering plan). Stat effects are applied through **generated buffs** (one buff per effect and rank,
shipped in the overlay, `StackExperienceBuffs`), so they act on the real simulation; whatever buffs cannot
express (chance-based effects and the like) is implemented with Harmony hooks on the damage/resistance/action methods.

## Commanders without a native counterpart

A commander is emulated as follows: (1) WoG state (`WoGCommander`, saved); (2) an extra unit in battle,
created at battle start from a separate creature definition per class (an OE unit clone, recolored —
`creature:wog.commander.<class>`), whose stats are set by `CommanderService.BattleProfile`; (3) a button on the
hero screen that opens the commander window (uGUI from the plugin). Death/resurrection/experience follow `04_Commanders.md`.

## Data overlay

`WoG.OldenEra.Data` (`CoreZipReader`, `OverlayBuilder`, `StackExperienceBuffs`) builds `wog_core.zip` from WoG
data: creature clones for commanders and WoG creatures, buffs for stack experience ranks and commander bonuses, artifacts
146–156, localization. The archive is placed next to `Core.zip` (a verified mechanism). It contains no game files —
only JSON generated on the user's machine from their own `Core.zip` and the WoG tables.
