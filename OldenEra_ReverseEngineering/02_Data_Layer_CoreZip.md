# Olden Era — data layer (`Core.zip`)

All game content is JSON inside `HeroesOldenEra_Data/StreamingAssets/Core.zip` **[V-data]** (O1 reads it
with JSZip and enumerates the folders below). Game updates overwrite `Core.zip`.

## 1. Overlay mechanism

* Extra `.zip` files placed **next to `Core.zip`** are merged on top of it at runtime **[V-community]**
  (O1 README: "The engine merges your ZIP on top of `Core.zip` at runtime"; its custom heroes, map
  objects, artifacts and buffs shipped this way appear in the official map editor and work when placed —
  stated as verified in-game in O1's type comments, issues #139/#146/#150/#165).
* New content is added by **cloning an existing definition under a new id** ("clone a real definition,
  mint a new id, ship it"). For map-object logic the clone **must** live in the same `objects_logic`
  family sub-folder as its source — "a shared/generic subfolder breaks the object" **[V-community]**.
* Whether an overlay zip is applied globally or only to its map is **[UNVERIFIED]**; O1 always ships it
  per map. The port's data generator emits one `wog_core.zip` and treats global application as a probe.
* Localisation files need a UTF-8 BOM; missing keys show raw SIDs **[V-data]**.

## 2. Folder map

| Path in zip | Content | Shape (as documented by O1) |
|-------------|---------|------------------------------|
| `DB/units/units_logics/<faction>/*.json` | creatures | `id`, `fraction`, `tier`, `icon`, `stats{hp, offence, defence, damageMin, damageMax, initiative, speed, luck, moral, actionPoints, numCounters, energyPerCast, energyPerRound, energyPerTakeDamage}`, `unitCost{costResArray[{name,cost}]}`, abilities/passives |
| `DB/heroes/<faction>/*.json`, `DB/heroes/custom_maps/` | heroes (108 in 6 factions + campaign) | `id,name,description,motto,mesh,mounts,icon,fraction,nativeBiome,classType (might/magic),skillsRollVariant,costGold,startLevel,startSquad[{sid,min,max}],specialization,stats{viewRadius,statsNum,magicCastsPerRound,enableTactics,tacticsPlacementSize,offence,defence,spellPower,intelligence,luck,moral},statsRolls,startSkills[{sid,skillLevel}],startMagics[{sidConfig,level,isLearned}]` |
| `DB/heroes_specializations/*.json` | hero specialities | |
| `DB/heroes_skills/skills/*.json` | secondary skills ("subskills") | |
| `DB/magics/*.json` | spells | |
| `DB/items/items/*.json` | artifacts (flat arrays) | `id,name,description,narrativeDescription,icon,slot_,rarity,bonuses,goodsValue,…` (incl. `battleSubskillBonus` referencing buffs) |
| `DB/buffs/*.json` | buffs / status effects (19 files, 414 buffs) | see `04_Buffs_and_Battle.md` |
| `DB/fractions/*.json` | factions | order and names |
| `DB/squads/**` | neutral squad templates | |
| `DB/map/objects/{1_environments,2_animals,3_resources,4_interactables,5_fxs,6_artifacts,7_spawns,8_test,9_blocks}.json` | map object templates | `id,name,tag,isInteractable,prefs (3D prefab refs),geometry,generatorConfig` — **no icon field** |
| `DB/objects_logic/<family>/**` (e.g. `cities/*_city.json`, `res_mines/mines.json`, `event_banks/**`) | behaviour of interactive objects | |
| `DB/map/trigger_zones/zones.json`, `waters`, `rivers`, `roads`, `tiles` | map layers | |
| `DB/dialogs/dialogs/**` | ~769 dialog flows | `{"array":[flow]}` |
| `DB/res/resources_info.json` | resources | |
| `DB/difficulties_lobby.json` | difficulty presets | |
| `Lang/<language>/texts/*.json` | localisation (16 languages) | |

Content counts **[V-data]**: 146 creature ids (6 factions × 21 incl. upgrade + alternative upgrade, plus
19 neutral), 108 heroes, 158 interactive map object ids.
Factions: **Temple, Necropolis, Grove, Dungeon, Hive, Schism** (+ Neutral). Every creature has a base,
`_upg` and `_upg_alt` form.

## 3. Maps

* Binary `.map` file = gzip + LEB128-framed JSON blocks **[V-data]**; O1 reads and writes it (Map Grid).
* The scenario script is a `.json` beside the `.map` with the same name **[V-community]**; it is
  mandatory as soon as any object has scripted actions (otherwise map loading breaks).
* O1 can import Heroes III `.h3m` maps into Olden Era maps (terrain, towns, heroes, monsters, mines,
  dwellings, artifacts, portals, global events) **[V-code]** — usable for porting WoG maps' geometry; WoG
  ERM in those maps is handled by our ERM runtime, not by this importer.

## 4. What the data layer can do for WoG

| Need | Possible via data? |
|------|--------------------|
| New creature *types* (8th-level, WoG neutrals) as clones of existing models with new stats | **Yes** — clone unit definition, new id, reuse mesh (recolour = material swap needs asset work) |
| New artifacts with stat bonuses | **Yes** — clone item, edit `bonuses`; mechanical generality limited to what `bonuses`/buffs express |
| New buffs (stack-experience rank bonuses as stat buffs) | **Yes** — clone buff, edit `data.stats` |
| New map objects with custom behaviour | **Partly** — clone object (+logic family); custom behaviour needs scripting or the plugin |
| Per-instance state (experience of *this* stack, commander level) | **No** — static definitions only; needs runtime (plugin) |
| Rules changed by options at runtime | **No** — needs plugin |
