# Battle, adventure map, towns and the WoG feature set

Sources: `T1/Monsters.cpp` (battle receivers and hooks), `T1/casdem.cpp` (towns, demolition),
`T1/womo.cpp` (wandering monsters), `T1/erm.cpp` (object receivers, `EventERM`, `ERM2Object`),
`wog features.html` (S4), the 3.58f scripts (S7).

## 1. How WoG is built — the key fact for the port

WoG 3.58 = **(a)** an engine extension in C++ (ERM interpreter, Commanders, Stack Experience, new
creature/artifact tables, battle hooks, object hooks, options) **+ (b)** ~77 ERM scripts that implement
most of the visible "WoG features" (new objects such as the Junk Merchant or Adventure Cave, Week of
Monsters, Henchmen, Enhanced Secondary Skills, Map Rules, …).

Therefore the porting strategy is: re-implement (a) natively in the WoG Core, and run (b) **unchanged**
through the ERM runtime. Every (b) feature then works as soon as the receivers it uses are mapped to
Olden Era. `03_ERM_Receivers.md` ranks those receivers by real usage.

## 2. Battle lifecycle (trigger order, from `BACall`, `BACall2`, `BACall3`, `BFCall`, `MFCall`, help)

```
BA0 (attacker PC) | BA50 (defender PC) → BA52
  └─ BF (battlefield setup: obstacles, BF receiver)
      └─ commander/extra stacks placed (PlaceNPCAtBattleStart), stack experience applied (CrExpBon::Apply)
          └─ BR v997=-1 (before tactics) → [tactics] → BR v997=0
              └─ per action: BG0 → native action (MF/MR hooks inside damage/resistance calc) → BG1
              └─ per round: BR v997=n, CrExpBon::Apply2 (per-round bonuses), regeneration hooks
BA1 | BA51 → BA53  (after battle; commander death/survival CheckForAliveNPCAfterBattle; stack exp AddExpo)
```

Battle receivers:

| Receiver | Selector | Purpose |
|----------|----------|---------|
| `BA` | — | battle-wide: heroes (`H`), owners (`O`), position (`P`), auto/quick (`Q`), modifiers |
| `BM` (`ERM_BRound`) | stack index (0…41) | per battle stack; commands `T N L B E I A D H S F O R J U` (type, number, attack `A`, defence `D`, speed `S`, flags `F`, position `O`, …; full meanings per help page `rebm_r`) |
| `BU` | — | universal: cast spells `C`, obstacles `O`, find stack at hex `E`, damage `D`, rounds `R`, … |
| `BG` | — | current action: type `A`, attacker/target `S`/`D`, spell `X` … |
| `BH` | side 0/1 | battle hero: number `N`, spells `M`, casted flag `C` … |
| `BF` | — | battlefield obstacles before battle |
| `MR` | — | magic resistance override inside the calculation (MR0/1/2 triggers) |
| `MF` | — | monster feature hooks: defence coefficient, block, hate damage (MF0/1/2) |

Port requirement: these must act on the **real simulation**, therefore they are routed to a
battle adapter that runs inside Olden Era's own combat (see `Compatibility/Architecture.md` §Battle).

## 3. Adventure map

* Object visits: `EventERM(hero, mapItem, MixPos)` → `ERM2Object(pre)` → native visit →
  `ERM2Object(post)`. Triggers `OB x/y/l`, `OB type/sub`, `OB type`, `$OB…`. Scripts may cancel the native
  visit (`OB:S` "disable standard behaviour") — the port needs a *pre-visit veto*.
* Per-object receivers (`MN SC CH WT KT FR LN ST WG SK SP WM SW MT GD ML DW WH SY GR SR SG UR PM CB`)
  read/write object state (owner, contents, visited flags). WoG keeps extra per-object ERM state in
  `ERM_Object[]` (hint text, disabled flags) — saved.
* Hero movement: `HM` before each step; `HE:P` teleport; `PO` per-square ERM storage (`Square`,
  `Square2` arrays — 4 values per square, saved).
* Map editing: `UN:I` place object, `UN:O` remove, `TR` terrain, `MO` monsters, `AR` artifacts on map,
  `LE` local events, `GE`/`CE` global and town events.
* Timers: `TM` (day-based), `TL` (real time, 3.59).
* Wandering monsters (`MW`, `womo.cpp`): monsters that move on the map with destinations.

## 4. Towns (`casdem.cpp`)

* `CA` receiver: buildings (`B` build/check/enable), dwelling creatures (`M`), garrison (`G`),
  owner (`O`), name (`N`), type (`T`), …; `!?TH0/1` town hall triggers; `CA:R` build-again flag.
* Town demolition (`CD`, option 4) and the Castle "Demolish" UI.
* 8th-level dwellings (option 0, `PL_ExtDwellStd`): external dwellings provide one 8th-level creature
  per week to towns with the matching 7th-level dwelling (S4 §8th Level Monsters).
* **[3.59]** `CI` income/growth triggers.

## 5. Feature list (S4) and how each is realised in WoG

| WoG feature | Realised by |
|-------------|-------------|
| Commanders | engine (`npc.cpp`) + script 27 (enhanced) + 12 (witch huts) + 2 (sanctuary) |
| Stack experience | engine (`crexpo.cpp`) + tables `CREXP*.TXT` |
| 8th level monsters, new creatures 150+ | engine creature tables + dwellings |
| New artifacts (commander 146–155, stack 156, others) | engine artifact tables + script 07/76 |
| WoG objects (Junk Merchant, Adventure Cave, Living Skull, …) | ERM scripts (S7 list, 76 files) |
| Wogify (add WoG objects to non-WoG maps) | script 78 `wogify.erm` |
| Map rules, map options | scripts 77, 53 + options 100–249 |
| Enhanced secondary skills / spells | scripts 30, 43, 55, 3 |
| Week of Monsters, Henchmen, Neutral units, … | scripts |
| WoG options dialog | engine (`wogsetup.cpp`) |
| Town demolition, enhanced towers | engine (`casdem.cpp`) + options 1, 4 |
| Monster leaving army | engine + options 2, 10 |
