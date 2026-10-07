# Battle, adventure map, towns, and the WoG feature set

Sources: `T1/Monsters.cpp` (battle receivers and hooks), `T1/casdem.cpp` (towns, demolition), `T1/womo.cpp` (wandering
monsters), `T1/erm.cpp` (object receivers, `EventERM`, `ERM2Object`), `wog features.html` (S4), 3.58f scripts (S7).

## 1. How WoG is built — the key fact for the port

WoG 3.58 = **(a)** a C++ engine extension (the ERM interpreter, commanders, stack experience, new
creature/artifact tables, battle and object hooks, options) **+ (b)** ~77 ERM scripts that implement most of the visible
"WoG features" (new objects such as the Junk Merchant or the Adventure Cave, Week of Monsters, Henchmen, Enhanced
Secondary Skills, Map Rules, …).

Hence the porting strategy: rewrite (a) natively in WoG Core, and run (b) **unchanged** through the ERM runtime.
Each feature from (b) starts working as soon as the receivers it uses are mapped onto Olden Era.
`03_ERM_Receivers.md` ranks the receivers by actual usage; the port's parser already parses all 78 script files
of 3.58f without errors.

## 2. Battle lifecycle (trigger order — from `BACall`, `BACall2`, `BACall3`, `BFCall`, `MFCall`, and the help)

```
BA0 (attacker's PC) | BA50 (defender's PC) → BA52
  └─ BF (battlefield setup: obstacles, the BF receiver)
      └─ placement of the commander/extra stacks (PlaceNPCAtBattleStart), stack experience applied (CrExpBon::Apply)
          └─ BR v997=-1 (before tactics) → [tactics] → BR v997=0
              └─ for each action: BG0 → native action (inside it, MF/MR hooks in the damage/resistance calculation) → BG1
              └─ for each round: BR v997=n, CrExpBon::Apply2 (round bonuses), regeneration
BA1 | BA51 → BA53  (after battle: commander survival CheckForAliveNPCAfterBattle; stack experience AddExpo)
```

Battle receivers:

| Receiver | Selector | Purpose |
|---------|----------|------------|
| `BA` | — | the battle as a whole: heroes (`H`), owners (`O`), position (`P`), quick battle (`Q`), modifiers |
| `BM` (`ERM_BRound`) | stack number (0…41) | a stack in battle; commands `T N L B E I A D H S F O R J U` (type, count, attack `A`, defense `D`, speed `S`, flags `F`, position `O`, …; for the full meaning, see the help page `rebm_r`) |
| `BU` | — | general: spells `C`, obstacles `O`, finding a stack by hex `E`, damage `D`, rounds `R`, … |
| `BG` | — | the current action: type `A`, attacker/target `S`/`D`, spell `X` … |
| `BH` | side 0/1 | a hero in battle: number `N`, spells `M`, the "has cast a spell" flag `C` … |
| `BF` | — | battlefield obstacles before the battle |
| `MR` | — | overriding magic resistance inside the calculation (triggers MR0/1/2) |
| `MF` | — | monster ability hooks: defense coefficient, block, hate damage (MF0/1/2) |

Port requirement: all of this must act on the **real simulation**, so it goes through a battle adapter
that runs inside Olden Era's own battle (see `Compatibility/Architecture.md` §Battle).

## 3. Adventure map

* Object visits: `EventERM(hero, tile, MixPos)` → `ERM2Object(pre)` → native visit →
  `ERM2Object(post)`. Triggers `OB x/y/l`, `OB type/subtype`, `OB type`, `$OB…`. A script can cancel the native
  visit (`OB:S` — "disable the standard behavior"), so the port needs *the ability to block the visit before it
  happens* (`WoGEvent.CancelNative`).
* Object receivers (`MN SC CH WT KT FR LN ST WG SK SP WM SW MT GD ML DW WH SY GR SR SG UR PM CB`) read and
  write object state (owner, contents, visit flags). WoG keeps extra ERM data for objects in
  `ERM_Object[]` (hint, disable flags) — it is saved.
* Hero movement: `HM` before each step; `HE:P` — teleport; `PO` — ERM data for tiles (arrays `Square`,
  `Square2`, 4 values per tile, saved).
* Map editing: `UN:I` places an object, `UN:O` removes one, `TR` terrain, `MO` monsters, `AR` artifacts on the map,
  `LE` local events, `GE`/`CE` global events and town events.
* Timers: `TM` (by day), `TL` (real time, 3.59).
* Wandering monsters (`MW`, `womo.cpp`): monsters that walk across the map toward a target.

## 4. Towns (`casdem.cpp`)

* The `CA` receiver: buildings (`B` build/check/enable), dwelling creatures (`M`), garrison (`G`),
  owner (`O`), name (`N`), type (`T`), …; town hall triggers `!?TH0/1`; `CA:R` — the "build again" flag.
* Town demolition (`CD`, option 4) and the "Demolish" button in the town.
* Level-8 dwellings (option 0, `PL_ExtDwellStd`): external dwellings give one level-8 creature per week to
  towns that have the corresponding level-7 dwelling (S4 §8th Level Monsters).
* **[3.59]** income/growth triggers `CI`.

## 5. Feature list (S4) and what implements each feature in WoG

| WoG feature | Implemented by |
|-----------------|-----------------|
| Commanders | engine (`npc.cpp`) + script 27 (enhanced) + 12 (witch huts) + 2 (sanctuary) |
| Stack experience | engine (`crexpo.cpp`) + `CREXP*.TXT` tables |
| Level-8 monsters, new creatures 150+ | the engine's creature tables + dwellings |
| New artifacts (commander 146–155, stack 156, others) | the engine's artifact tables + scripts 07/76 |
| WoG objects (Junk Merchant, Adventure Cave, Living Skull, …) | ERM scripts (list S7, 76 files) |
| Wogification (adding WoG objects to non-WoG maps) | script 78 `wogify.erm` |
| Map rules and options | scripts 77, 53 + options 100–249 |
| Enhanced secondary skills / spells | scripts 30, 43, 55, 3 |
| Week of Monsters, Henchmen, Neutral Units, … | scripts |
| WoG Options dialog | engine (`wogsetup.cpp`) |
| Town demolition, enhanced towers | engine (`casdem.cpp`) + options 1, 4 |
| Monsters leaving the army | engine + options 2, 10 |
