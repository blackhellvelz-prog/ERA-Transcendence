# Olden Era — what is exposed

Classification required by the project brief. Tags as in `00_Sources_and_Verification.md`.

## Exposed (usable today, verified)

| Capability | Through | Tag |
|------------|---------|-----|
| Static content: creatures, heroes, artifacts, buffs, spells, skills, map objects, factions, dialogs, localisation | JSON in `Core.zip` + overlay zip | V-data / V-community |
| New content by cloning existing definitions under new ids | overlay zip | V-community |
| Map scripting: counters, quests, triggers on turn/week/visit/capture/kill/hire/build/cast/hero stat, 114 actions (resources, units, exp, stats, mana, spells, skills, items, buffs, buildings, spawning, fog, camera, AI control, victory/defeat) | scenario JSON beside the map | V-community |
| Hero-vs-hero battle interruptions | scenario JSON | V-community |
| Running managed code in the game process, Harmony patches, reading/writing game objects | BepInEx 6 IL2CPP | V-code |
| Map editor UI extension | BepInEx (O2) | V-code |
| Reading/writing binary `.map`, importing `.h3m` | O1 | V-code |

## Partially exposed

| Capability | Limitation |
|------------|-----------|
| Game logic classes | IL2CPP + obfuscation: names change per update; must be resolved by name/signature at runtime |
| Creature stat changes in combat | buffs cover additive/percent stats and many flags; per-instance and chance effects need hooks |
| Adventure-map object behaviour | object logic families are fixed; new behaviour only through scripts or hooks |
| Overlay zip scope | global vs per-map not confirmed |

## Not exposed (needs plugin hooks, or not possible)

| Capability | Status |
|------------|--------|
| General-purpose scripting language (variables, loops, functions) | **not exposed** — must be provided by our ERM runtime in the plugin |
| Battle event hooks (round, action, damage, death) | **not exposed by data/scripting**; reachable only via Harmony on combat methods — [UNVERIFIED names] |
| Per-stack persistent state (experience, stack artifacts) | **not exposed**; external WoG state + battle hooks |
| Commanders (hero-bound special unit with progression) | **no equivalent entity**; must be emulated (special stack + external state) |
| Custom UI windows | **not exposed**; Unity UI can be built from a plugin (uGUI/TMP present) — [UNVERIFIED stability] |
| Save-file extension | **not exposed**; side-car file strategy |
| Raw memory pokes (`UN:C`) | **impossible by design** (different engine; no address compatibility) |
| H3 graphics formats (DEF/PCX/LOD) | not native; 2D icons can be imported as PNG → Unity `Sprite` from the plugin |

## Verification debt

Every **[UNVERIFIED]** row has a probe in the adapter (`OldenEraProbes`) and a step in
`07_InGame_RE_Plan.md`. Until a probe passes, the dependent WoG features report *unsupported* at
runtime (never silently faked).
