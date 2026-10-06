# ERM receivers — catalog and command specifications

Three sources are combined here:

* **what the engine implements** — command letters extracted from the dispatch `switch(Cmd)` /
  `Cmd=='X'` tests of each receiver function in S1 (`T1/*.cpp`); the extractor script is reproduced in
  `tools/re/extract_receivers.py`;
* **what scripts actually use** — occurrences of `!!XX`/`!#XX` in the 76 original 3.58f WoG scripts (S7);
* **what each command means** — the ERM help (S3), cross-checked against the code for the commands that
  the runtime implements.

The usage column drives implementation priority: `VR`, `FU`, `IF`, `UN`, `HE`, `MA`, `BM`, `DO`, `OW`,
`CA` cover ≈ 90 % of all receiver lines in the WoG scripts.

## 1. Catalog

| Receiver | Area | Uses in 3.58f WoG scripts | Source (S1) | Command letters found in source | Ver |
|---|---|---|---|---|---|
| `VR` | variables | 12366 | `erm.cpp` `ERM_Variable` | `SRTXHCUMV` | 3.58 |
| `FU` | control | 2325 | `erm.cpp` `ERM_Function` | `PDEXC` | 3.58 |
| `IF` | ui/dialogs | 2168 | `erm.cpp` `ProcessMes` | `TQMSRNVAWPEXBFGDL` | 3.58 |
| `UN` | universal/global | 2088 | `erm.cpp` `ERM_Universal` | `(nested switches — see help)` | 3.58 |
| `HE` | hero | 2074 | `erm.cpp` `ProcessMes` | `OPIXUCSAMFNLWGKEVBRYTHD` | 3.58 |
| `MA` | creature data | 660 | `Monsters.cpp` `ERM_MonAtr` | `OLCFIGRPSADMENBVHXU` | 3.58 |
| `BM` | battle stack | 568 | `Monsters.cpp` `ERM_BRound` (table entry `BM`; `ERM_BMonster` is commented out) | `TNLBEIADHSFORJU` | 3.58 |
| `DO` | control | 521 | `erm.cpp` `ERM_Do` | `P` | 3.58 |
| `OW` | player | 486 | `erm.cpp` `ERM_Owner` | `RDIGTHCKOVANWS` | 3.58 |
| `CA` | town | 339 | `casdem.cpp` `ERM_Castle` | `HUINPOTVRGMBS` | 3.58 |
| `PO` | map square | 230 | `erm.cpp` `ERM_Position` | `HONTSCVB` | 3.58 |
| `MO` | map monster | 194 | `erm.cpp` `ProcessMes` | `GOURMBAW` | 3.58 |
| `BA` | battle | 178 | `Monsters.cpp` `ERM_Battle` | `HMPOEDABQS` | 3.58 |
| `BU` | battle universal | 177 | `Monsters.cpp` `ERM_BUniversal` | `MEDOSTCRGVHPABQNFW` | 3.58 |
| `CM` | input | 170 | `erm.cpp` `ERM_MouseClick` | `TSIFAPRHDM` | 3.58 |
| `CO` | commander | 138 | `npc.cpp` `ERM_NPC` | `EDCTHPSANXB` | 3.58 |
| `OB` | map object | 107 | `erm.cpp` `ERM_SetObject` | `TUCMDESRHB` | 3.58 |
| `MC` | variables | 91 | `erm.cpp` `ERM_Macro` | `S` | 3.58 |
| `TR` | map terrain | 77 | `erm.cpp` `ERM_Terrain` | `GTPEV` | 3.58 |
| `BG` | battle action | 73 | `Monsters.cpp` `ERM_MAction` | `ASDXQHENVC` | 3.58 |
| `HT` | map/hint | 68 | `erm.cpp` `ERM_HintType` | `TPWV` | 3.58 |
| `CB` | object: creature bank | 49 | `erm.cpp` `ERM_SetCrBank` | `MGRATV` | 3.58 |
| `EA` | stack experience (AI) | 49 | `crexpo.cpp` `ERM_AICrExp` | `MULPCBODREFASTH` | 3.58 |
| `BF` | battle | 46 | `Monsters.cpp` `ERM_BattleField` | `COM` | 3.58 |
| `BH` | battle hero | 39 | `Monsters.cpp` `ERM_BHero` | `NMCQ` | 3.58 |
| `TM` | timers | 39 | `erm.cpp` `ERM_Timer` | `SED` | 3.58 |
| `GR` | object: garrison | 26 | `erm.cpp` `ERM_Garrison` | `OGFN` | 3.58 |
| `MN` | object: mine | 25 | `erm.cpp` `ERM_Mine` | `ORM` | 3.58 |
| `DW` | object: dwelling | 22 | `erm.cpp` `ERM_SetDwelling` | `MGO` | 3.58 |
| `AR` | map artifact | 18 | `erm.cpp` `ProcessMes` | `VMGX` | 3.58 |
| `MM` | input | 17 | `erm.cpp` `ERM_MouseMove` | `MSD` | 3.58 |
| `EX` | stack experience | 15 | `erm.cpp` `ERM_StackExperience` | `(nested switches — see help)` | 3.58 |
| `MW` | wandering monster | 13 | `womo.cpp` `ERM_WMon` | `PCMEA` | 3.58 |
| `PM` | object: pyramid | 12 | `erm.cpp` `ERM_Pyramid` | `VPS` | 3.58 |
| `CH` | object: chest | 10 | `erm.cpp` `ERM_SetChest` | `SAB` | 3.58 |
| `SR` | object: shrine | 8 | `erm.cpp` `ERM_Shrine` | `S` | 3.58 |
| `MR` | battle magic res | 7 | `Monsters.cpp` `ERM_MonRes` | (see help) | 3.58 |
| `ML` | object: mill | 6 | `erm.cpp` `ERM_SetMill` | `B` | 3.58 |
| `WH` | object: witch hut | 6 | `erm.cpp` `ERM_SetWHat` | `S` | 3.58 |
| `GD` | object: garden | 5 | `erm.cpp` `ERM_SetGarden` | `BTN` | 3.58 |
| `LE` | map event | 5 | `erm.cpp` `ProcessMes` | `MGXEPOURFNABSCDIL` | 3.58 |
| `QW` | quest log | 5 | `erm.cpp` `ERM_Qwest` | `A` | 3.58 |
| `FR` | object: fire | 4 | `erm.cpp` `ERM_SetFire` | `B` | 3.58 |
| `IP` | network | 4 | `Monsters.cpp` `ERM_NetworkService` | `DVWFR` | 3.58 |
| `SC` | object: scholar | 4 | `erm.cpp` `ERM_SetScoolar` | `TPSL` | 3.58 |
| `SG` | object: sign | 4 | `erm.cpp` `ERM_Sign` | `M` | 3.58 |
| `WM` | object: windmill | 4 | `erm.cpp` `ERM_SetWMill` | `B` | 3.58 |
| `SY` | object: shipyard | 3 | `erm.cpp` `ERM_Shipyard` | `OP` | 3.58 |
| `MF` | battle monster feature | 2 | `Monsters.cpp` `ERM_MonFeature` | (see help) | 3.58 |
| `SN` | sound | 2 | `sound.cpp` `ERM_Sound` | `SP` | 3.58 |
| `HL` | hero | 1 | `erm.cpp` `ERM_HeroGainLevel` | `SR` | 3.58 |
| `KT` | object: tree of knowledge | 1 | `erm.cpp` `ERM_SetKTree` | `SN` | 3.58 |
| `LN` | object: lean-to | 1 | `erm.cpp` `ERM_SetLean` | `BN` | 3.58 |
| `MT` | object: monolith | 1 | `erm.cpp` `ERM_SetMonolit` | `N` | 3.58 |
| `SK` | object: skeleton | 1 | `erm.cpp` `ERM_SetSkelet` | `ANS` | 3.58 |
| `ST` | object: stone | 1 | `erm.cpp` `ERM_SetStone` | `N` | 3.58 |
| `UR` | object: university | 1 | `erm.cpp` `ERM_Univer` | `S` | 3.58 |
| `WG` | object: wagon | 1 | `erm.cpp` `ERM_SetWagon` | `SBAR` | 3.58 |
| `WT` | object: warrior tomb | 1 | `erm.cpp` `ERM_SetWTomb` | `AS` | 3.58 |
| `AI` | AI | 0 | `ai.cpp` `ERM_AIRun` | `SDWM` | 3.58 |
| `CD` | town (demolition) | 0 | `casdem.cpp` `ERM_CasDem` | `PDMNEAB` | 3.58 |
| `CE` | town event | 0 | `erm.cpp` `ProcessMes` | `MFRBENHQCUD` | 3.58 |
| `CI` | town | 0 | `casdem.cpp` `ERM_CastleIncome` | `IL` | 3.59 |
| `DL` | custom dialogs | 0 | `dlg.cpp` `ERM_Dlg` | `CPNSHEA` | 3.59 |
| `GE` | global event | 0 | `erm.cpp` `ProcessMes` | `MFRBENHQD` | 3.58 |
| `HD` | hint | 0 | `erm.cpp` `ERM_HintDisplay` | `MTPC` | 3.59 |
| `HO` | hero | 0 | `erm.cpp` `ERM_SetHero` | `DESRH` | 3.58 |
| `LD` | resources (LOD) | 0 | `lod.cpp` `ERM_LODs` | `LTU` | 3.59 |
| `MP` | sound | 0 | `sound.cpp` `ERM_MP3` | `CPSN` | 3.58 |
| `SP` | object: spring | 0 | `erm.cpp` `ERM_SetSpring` | `SN` | 3.58 |
| `SS` | spells | 0 | `spell.cpp` `ERM_Spell` | `OWXFNALSCPEHID` | 3.59 |
| `SW` | object: swan pond | 0 | `erm.cpp` `ERM_SetSwan` | `BN` | 3.58 |
| `TL` | realtime timer | 0 | `timer.cpp` `ERM_TL` | `ECTSD` | 3.59 |
| `UX` | universal ext | 0 | `erm.cpp` `ERM_UniversalEx` | `KMTSGAV` | 3.59 |
| `VC` | debug | 0 | `erm.cpp` `ERM_VarControl` | `CBEYNW` | 3.58 |

Control-flow pseudo-receivers: `if`, `el`, `en` (3.58), `la`, `go` (3.59).
Notes on the extractor: `UN` and `EX` contain nested switches on their first parameter, so their letter
list is taken from the help, not the extractor. `BM` is implemented by `ERM_BRound` (the table entry
named `ERM_BMonster` is commented out); `MR`/`MF` are not `switch(Cmd)` based. Other letter lists are exact.

## 2. Command specification format

Each implemented command is specified as:

`Receiver:Command` → **Meaning** · **Inputs** · **Outputs** · **State affected** · **Context** ·
**Side effects** · **Olden Era equivalent** (filled in `Compatibility/ERM_Compatibility.md`).

`$` = parameter that accepts set/get/check/`d` syntax, `#` = plain number/variable.

## 3. Core receivers (implemented in `src/WoG.Erm/Receivers`)

### VR — variables (`ERM_Variable`)
Selector: one variable reference (evaluated at execution; may be indirect, e.g. `!!VRvy1:`).

| Cmd | Meaning | Inputs | Outputs / state | Notes from code |
|-----|---------|--------|-----------------|-----------------|
| `S$` | set | value, `^text^` or z-var for strings | var ← value | `?var` copies *into* the param; floats: only set syntax |
| `R$` / `R0/$seed` | add random 0…$ | max (inclusive) | var += rnd | 2-param form reads/sets the RNG seed |
| `T$` | add time-random 0…$ | | | |
| `+$ -$ *$ :$ %$` | arithmetic | operand | var op= operand | `:`/`%` by 0 → message, unchanged; strings support `+` (concat) |
| `&$ \|$ X$` | bitwise and/or/xor | | | `^` is the deprecated alias of `X` |
| `H#` | flag # ← string is non-empty | flag 1…1000 | flag | z-vars only |
| `C$/$/…` | set consecutive vars | up to 16 values | var, var+1, … | int vars only, each param supports set/get/check |
| `U$` | flag 1 ← substring found (case-insensitive) | z-var or text | flag 1 | |
| `M1/z/start/len` `M2/z/token` `M3/val/base` `M4/?len` `M5/?first` `M6/?last` | string ops | | | `M3` = itoa |
| `V$` | parse z-var to int/float | z index | var | |

### FU — functions (`ERM_Function`); DO — loops (`ERM_Do`)
| Cmd | Meaning |
|-----|---------|
| `FU#:P$…` | call function # with up to 16 args → `x1…x16`; `?var` args receive the final `x` values |
| `FU:E[#]` | exit current trigger section ([3.59] `#`<0 jumps back) |
| `FU:D…` | network "distant" call → **unsupported (no network layer)** |
| `FU:C#` | toggle y-var-outside-function check (debug) |
| `FU:X#/$` [3.59] | how arg # was passed |
| `DO#/a/b/s:P$…` | loop `x16=a..b step s` calling function # |

### MC — macros (`ERM_Macro`)
`!#MC:S@name@` / `!!VRv5:…$name$…` — `!!MCv5:S@name@;` binds macro `name` to the variable in the selector.
Macros are global and saved.

### IF — messages/dialogs/flags (inline `IF`)
| Cmd | Meaning | Implementation |
|-----|---------|----------------|
| `M^text^` / `M1/$` | show a message (interpolated) | UI adapter `ShowMessage` |
| `Q#/pic…^text^` | yes/no or picture question → flag # | UI adapter `AskQuestion` |
| `V#/$` | set flag # to $ (0/1) | core |
| `W$` | select the hero whose `w` vars are used | core |
| `X$` | flags from bit mask (A/R/S variants set/reset) | core |
| `G…`, `B…`, `D…`, `E…`, `N…`, `P…`, `T…`, `L^text^` | multi-choice dialogs, battle log, etc. | UI adapter where available |

### UN — universal (`ERM_Universal`) — implemented subset
| Cmd | Meaning |
|-----|---------|
| `P#/$` | get/set WoG option # (0…999) — writes `PL_WoGOptions[0][#]`; options 3/6 also enable/disable commanders immediately |
| `P$` (1 param) | legacy: option 0 |
| `C…` | raw memory poke — **unsupported by design** (see compatibility) |
| `I`,`R`,`O`,`T`,`S`,… | object placement/removal, dimensions, etc. — map adapter |

### HE — hero (inline `HE`) — implemented subset
Selector: `#` hero id, `-1` current hero, `-10/-20` attacker/defender in battle, `x/y/l` hero at position.

| Cmd | Meaning | State |
|-----|---------|-------|
| `E$` / `E$/$lvl` | experience (and level) | hero exp |
| `F$/$/$/$` | primary skills (A/D/P/K); `/1` 5th param = base without artifacts (get only) | hero stats |
| `I$` | spell points | hero mana |
| `W$` | movement points | |
| `S#/$` | secondary skill level 0…3 | skills |
| `M#/$` | spell in book 0/1 | spellbook |
| `A#` / `A1/art/slot` `A2/art/?n/?m` `A3/art/n/m` `A4/art` | give/remove/count/equip artifacts | inventory |
| `C0/slot/$type/$num[/$exp]` | army slot | army (+stack exp) |
| `C1/type/$type/$num` | all stacks of a type | army |
| `C2/type/num/ask` | add a stack | army |
| `O$` | owner | ownership |
| `P$x/$y/$l[/style]` | move | position (raises `HM`/visit triggers) |
| `N?$` | hero id | |
| `K` | kill hero | |

### OW — players (`ERM_Owner`) — implemented subset
`R#/res/$` resources (player −1 = current); `C?$` current player; `A#/$` active hero; `I#/$` AI/human;
`H#/v/#` hero list into v vars.

### MA — creature type data (`ERM_MonAtr`)
`A D P S M E N F I G R H V C L O U X B` = attack, defence, HP, speed, dmg low/high, shots, fight value,
AI value, growth, horde growth, adventure-map high/low, cost, level, town, upgrade-to, flags, casts.
Global for the creature type; saved (`MonsterUpgradeTable`, monster data via `SendCreatures`).

### TM — timers (`ERM_Timer`)
`S$first/$last/$period/$owners` · `E$player` · `D$player`. A timer fires at the start of a player's day if
`first ≤ day ≤ last` and `(day-first) % period == 0` and the player's bit is set (`RunTimer`).

### CO — commanders (`ERM_NPC`, `npc.cpp`) — see `04_Commanders.md`
Selector `-1` current hero's commander, `-2` all, `-3` attacker side, `-4` defender side, `#` hero id.
`E$exp/$level` · `P$a/$d/$hp/$dmg/$mp/$spd/$mr` (primary) · `S#/$` (skill level) · `B…` special
bonuses · `A1…A4` artifacts · `D`/`X` dead/alive · `N^name^` · `T$type` · `H$hero` · `C` enable/hire.

### EX — stack experience (`ERM_StackExperience`) — see `05_Creature_Experience.md`
Selector `hero/slot` or `x/y/l/slot[/ownerType]`. `A$type/$num/$exp` · `T$` · `N$` · `E$` (exp) ·
`R$` (rank) · `C…` combine stacks · artifact commands.

### BA / BM / BU / BG / BH / BF — battle
Specified in `07_Battle.md`; adapter-dependent.

## 4. Remaining receivers

All other receivers (object-specific `MN SC CH WT KT FR LN ST WG SK SP WM SW MT GD ML DW WH SY GR SR SG
UR PM CB`, `PO TR OB MO AR LE GE CE HT QW`, input `CM MM`, sound `MP SN`, network `IP`, AI `AI EA`) are
parsed, registered with a compatibility status, and dispatched to adapter interfaces. Their per-command
status lives in `Compatibility/ERM_Compatibility.md` and is generated from the runtime's receiver registry
(`dotnet run --project tools/WoG.ErmTool -- compat`).
