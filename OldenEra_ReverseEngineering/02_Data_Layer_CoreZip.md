# Olden Era — data layer (`Core.zip`)

All game content is JSON inside `HeroesOldenEra_Data/StreamingAssets/Core.zip` **[V-data]** (O1 reads it
via JSZip and enumerates the folders below). Game updates overwrite `Core.zip`.

## 1. Overlay mechanism

* Additional `.zip` files placed **next to `Core.zip`** are merged on top of it at startup
  **[V-community]** (O1 README: "The engine merges your ZIP on top of `Core.zip` at runtime"; its own heroes,
  map objects, artifacts and buffs shipped this way appear in the built-in map editor and work —
  per O1's type comments, issues #139/#146/#150/#165, verified in game).
* New content is added by **cloning an existing definition under a new id** ("clone a real
  definition, mint a new id, ship it"). A clone of a map object's logic **must** live in the same `objects_logic`
  family subfolder as the original — "a shared subfolder breaks the object" **[V-community]**. For units, the port places
  the clone in the original's folder (`OverlayBuilder.CloneUnit`) — the same rule by analogy, **[UNVERIFIED]**.
* Whether the overlay applies globally or only to its own map is **[UNVERIFIED]**; O1 always ships it alongside a
  map. The port's generator emits a single `wog_core.zip` and checks global scope with a separate probe.
* Localization: `Lang/<language>/texts/<file>.json` = `{"tokens":[{"sid","text"}]}` with a UTF-8 BOM; the archive is
  uncompressed (STORE), as in O1 **[V-code]**. A missing key is displayed as the raw sid **[V-data]**.

## 2. Folder map

| Path in zip | Contents | Shape (per O1 documentation) |
|-------------|----------|------------------------------|
| `DB/units/units_logics/<faction>/*.json` | creatures | `id`, `fraction`, `tier`, `icon`, `stats{hp, offence, defence, damageMin, damageMax, initiative, speed, luck, moral, actionPoints, numCounters, energyPerCast, energyPerRound, energyPerTakeDamage}`, `unitCost{costResArray[{name,cost}]}`, abilities |
| `DB/heroes/<faction>/*.json`, `DB/heroes/custom_maps/` | heroes (108 across 6 factions + campaign) | `id,name,description,motto,mesh,mounts,icon,fraction,nativeBiome,classType (might/magic),skillsRollVariant,costGold,startLevel,startSquad[{sid,min,max}],specialization,stats{viewRadius,statsNum,magicCastsPerRound,enableTactics,tacticsPlacementSize,offence,defence,spellPower,intelligence,luck,moral},statsRolls,startSkills[{sid,skillLevel}],startMagics[{sidConfig,level,isLearned}]` |
| `DB/heroes_specializations/*.json` | hero specializations | |
| `DB/heroes_skills/skills/*.json` | secondary skills ("subskills") | |
| `DB/magics/*.json` | spells | |
| `DB/items/items/*.json` | artifacts (flat arrays) | `id,name,description,narrativeDescription,icon,slot_,rarity,bonuses,goodsValue,…` (including `battleSubskillBonus` with references to buffs) |
| `DB/buffs/*.json` | buffs / effects (19 files, 414 buffs) | see `04_Buffs_and_Battle.md` |
| `DB/fractions/*.json` | factions | order and names |
| `DB/squads/**` | neutral squad templates | |
| `DB/map/objects/{1_environments,2_animals,3_resources,4_interactables,5_fxs,6_artifacts,7_spawns,8_test,9_blocks}.json` | map object templates | `id,name,tag,isInteractable,prefs (references to 3D prefabs),geometry,generatorConfig` — **there is no icon field** |
| `DB/objects_logic/<family>/**` (`cities/*_city.json`, `res_mines/mines.json`, `event_banks/**` …) | behavior of interactive objects | |
| `DB/map/trigger_zones/zones.json`, `waters`, `rivers`, `roads`, `tiles` | map layers | |
| `DB/dialogs/dialogs/**` | ~769 dialogs | `{"array":[flow]}` |
| `DB/res/resources_info.json` | resources | |
| `DB/difficulties_lobby.json` | difficulty presets | |
| `Lang/<language>/texts/*.json` | localization (16 languages) | |

Counts **[V-data]**: 146 creature ids (6 factions × 21 including the upgrade and the alternate upgrade + 19
neutrals), 108 heroes, 158 interactive map objects.
Factions: **Temple, Necropolis, Grove, Dungeon, Hive, Schism** (+ neutrals). Every creature has a base form,
`_upg` and `_upg_alt`.

## 3. Maps

* The binary `.map` file = gzip + JSON blocks with a LEB128 length prefix **[V-data]**; O1 reads and writes it.
* The scenario script is a `.json` next to the `.map` with the same name **[V-community]**; it is mandatory as soon as
  any object has scripted actions (otherwise the map does not load).
* O1 can import Heroes III `.h3m` maps into Olden Era maps (terrain, towns, heroes, monsters, mines,
  dwellings, artifacts, portals, global events) **[V-code]** — suitable for carrying over the geometry of WoG maps;
  the ERM of such maps is executed by our ERM runtime, not by this importer.

## 4. What the data layer can give WoG

| Task | Via data? |
|------|-----------|
| New creature *types* (8th tier, WoG neutrals) as clones of existing models with new stats | **Yes** — unit clone, new id, same model (recolor = material swap, requires asset work) |
| New artifacts with stat bonuses | **Yes** — item clone, edit `bonuses`; generality is limited to what `bonuses`/buffs can express |
| New buffs (stack experience rank bonuses as stat buffs) | **Yes** — `OverlayBuilder.StatBuff`, `StackExperienceBuffs` |
| New map objects with their own behavior | **Partially** — object clone (+ logic family); custom behavior via scripts or a plugin |
| Instance state (experience of *this* stack, commander level) | **No** — static definitions only; requires a runtime (plugin) |
| Rules that change via options during play | **No** — requires a plugin |
