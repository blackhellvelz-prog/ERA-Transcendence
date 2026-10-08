**English** | [Русский](06_Capabilities.ru.md)

# Olden Era — what is available to modding

The classification required by the task. Tags are as in `00_Sources_and_Verification.md`.

## Available (usable now, verified)

| Capability | Via | Tag |
|------------|-----|-----|
| Static content: creatures, heroes, artifacts, buffs, spells, skills, map objects, factions, dialogs, localization | JSON in `Core.zip` + zip overlay | V-data / V-community |
| New content by cloning existing definitions under new ids | zip overlay | V-community |
| Map scripting: counters, quests, triggers on turn/week/visit/capture/kill/recruit/build/spell/hero stat, 114 actions (resources, units, experience, stats, mana, spells, skills, items, buffs, buildings, object creation, fog, camera, AI control, victory/defeat) | Scenario JSON next to the map | V-community |
| Hero-vs-hero combat interrupts | Scenario JSON | V-community |
| Running your own managed code inside the game process, Harmony patches, reading/writing game objects | BepInEx 6 IL2CPP | V-code |
| Extending the map editor UI | BepInEx (O2) | V-code |
| Reading/writing binary `.map`, importing `.h3m` | O1 | V-code |

## Partially available

| Capability | Limitation |
|------------|------------|
| Game logic classes | IL2CPP + obfuscation: names change with updates; resolved by name/signature at runtime |
| Changing creature stats in combat | buffs cover flat/percentage bonuses and many flags; per-instance and chance-based effects require hooks |
| Map object behavior | object logic families are fixed; new behavior only via scripts or hooks |
| Scope of a zip overlay | global or map-only — not confirmed |

## Unavailable (requires plugin hooks or impossible)

| Capability | Status |
|------------|--------|
| General-purpose scripting language (variables, loops, functions) | **no** — our ERM runtime in the plugin provides it |
| Combat event hooks (round, action, damage, death) | **not via data/scripts**; only Harmony on combat methods — [UNVERIFIED names] |
| Persistent stack state (experience, stack artifact) | **no**; external WoG state + combat hooks |
| Commanders (a special hero unit with progression) | **no equivalent**; emulated (special stack + external state) |
| Custom UI windows | **no**; Unity UI can be built from the plugin (uGUI/TMP are present) — [UNVERIFIED stability] |
| Extending the save file | **no**; "file next to the save" strategy |
| Direct memory writes (`UN:C`) | **fundamentally impossible** (different engine, no addresses) |
| H3 graphics formats (DEF/PCX/LOD) | not native; 2D icons can be imported as PNG → Unity `Sprite` from the plugin |

## Verification debt

Every **[UNVERIFIED]** row has a symbol in `OldenEraSymbols` and a step in `07_InGame_RE_Plan.md`. Until the symbol is
marked `verified`, the WoG features that depend on it report *unsupported* at runtime (they are never
silently faked).
