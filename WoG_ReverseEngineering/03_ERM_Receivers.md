# ERM Receivers — Catalog and Command Specifications

Three sources are combined here:

* **what is implemented in the engine** — command letters extracted from `switch(Cmd)` / `Cmd=='X'` checks in the function
  of each receiver in S1 (`T1/*.cpp`); the extractor script is in `tools/re/extract_receivers.py`;
* **what scripts actually use** — the number of occurrences of `!!XX`/`!#XX` in the 76 original WoG 3.58f scripts (S7);
* **what each command means** — the ERM help (S3), cross-checked against the code for the implemented commands.

The usage column sets the implementation priority: `VR`, `FU`, `IF`, `UN`, `HE`, `MA`, `BM`, `DO`, `OW`, `CA`
cover ≈ 90% of all receiver lines in the WoG scripts.

## 1. Catalog

| Receiver | Area | Uses in 3.58f scripts | Source (S1) | Command letters found in the source | Version |
|---|---|---|---|---|---|
| `VR` | variables | 12366 | `erm.cpp` `ERM_Variable` | `SRTXHCUMV` | 3.58 |
| `FU` | control | 2325 | `erm.cpp` `ERM_Function` | `PDEXC` | 3.58 |
| `IF` | interface/dialogs | 2168 | `erm.cpp` `ProcessMes` | `TQMSRNVAWPEXBFGDL` | 3.58 |
| `UN` | universal | 2088 | `erm.cpp` `ERM_Universal` | `(nested switch — see the help)` | 3.58 |
| `HE` | hero | 2074 | `erm.cpp` `ProcessMes` | `OPIXUCSAMFNLWGKEVBRYTHD` | 3.58 |
| `MA` | creature data | 660 | `Monsters.cpp` `ERM_MonAtr` | `OLCFIGRPSADMENBVHXU` | 3.58 |
| `BM` | battle stack | 568 | `Monsters.cpp` `ERM_BRound` (the `BM` entry in the table; `ERM_BMonster` is commented out) | `TNLBEIADHSFORJU` | 3.58 |
| `DO` | control | 521 | `erm.cpp` `ERM_Do` | `P` | 3.58 |
| `OW` | player | 486 | `erm.cpp` `ERM_Owner` | `RDIGTHCKOVANWS` | 3.58 |
| `CA` | town | 339 | `casdem.cpp` `ERM_Castle` | `HUINPOTVRGMBS` | 3.58 |
| `PO` | map tile | 230 | `erm.cpp` `ERM_Position` | `HONTSCVB` | 3.58 |
| `MO` | monster on the map | 194 | `erm.cpp` `ProcessMes` | `GOURMBAW` | 3.58 |
| `BA` | battle | 178 | `Monsters.cpp` `ERM_Battle` | `HMPOEDABQS` | 3.58 |
| `BU` | battle (general) | 177 | `Monsters.cpp` `ERM_BUniversal` | `MEDOSTCRGVHPABQNFW` | 3.58 |
| `CM` | input | 170 | `erm.cpp` `ERM_MouseClick` | `TSIFAPRHDM` | 3.58 |
| `CO` | commander | 138 | `npc.cpp` `ERM_NPC` | `EDCTHPSANXB` | 3.58 |
| `OB` | map object | 107 | `erm.cpp` `ERM_SetObject` | `TUCMDESRHB` | 3.58 |
| `MC` | variables | 91 | `erm.cpp` `ERM_Macro` | `S` | 3.58 |
| `TR` | terrain | 77 | `erm.cpp` `ERM_Terrain` | `GTPEV` | 3.58 |
| `BG` | battle action | 73 | `Monsters.cpp` `ERM_MAction` | `ASDXQHENVC` | 3.58 |
| `HT` | map/hints | 68 | `erm.cpp` `ERM_HintType` | `TPWV` | 3.58 |
| `CB` | object: creature bank | 49 | `erm.cpp` `ERM_SetCrBank` | `MGRATV` | 3.58 |
| `EA` | stack experience (AI) | 49 | `crexpo.cpp` `ERM_AICrExp` | `MULPCBODREFASTH` | 3.58 |
| `BF` | battle | 46 | `Monsters.cpp` `ERM_BattleField` | `COM` | 3.58 |
| `BH` | hero in battle | 39 | `Monsters.cpp` `ERM_BHero` | `NMCQ` | 3.58 |
| `TM` | timers | 39 | `erm.cpp` `ERM_Timer` | `SED` | 3.58 |
| `GR` | object: garrison | 26 | `erm.cpp` `ERM_Garrison` | `OGFN` | 3.58 |
| `MN` | object: mine | 25 | `erm.cpp` `ERM_Mine` | `ORM` | 3.58 |
| `DW` | object: dwelling | 22 | `erm.cpp` `ERM_SetDwelling` | `MGO` | 3.58 |
| `AR` | artifact on the map | 18 | `erm.cpp` `ProcessMes` | `VMGX` | 3.58 |
| `MM` | input | 17 | `erm.cpp` `ERM_MouseMove` | `MSD` | 3.58 |
| `EX` | stack experience | 15 | `erm.cpp` `ERM_StackExperience` | `(nested switch — see the help)` | 3.58 |
| `MW` | wandering monster | 13 | `womo.cpp` `ERM_WMon` | `PCMEA` | 3.58 |
| `PM` | object: pyramid | 12 | `erm.cpp` `ERM_Pyramid` | `VPS` | 3.58 |
| `CH` | object: chest | 10 | `erm.cpp` `ERM_SetChest` | `SAB` | 3.58 |
| `SR` | object: shrine | 8 | `erm.cpp` `ERM_Shrine` | `S` | 3.58 |
| `MR` | magic resistance | 7 | `Monsters.cpp` `ERM_MonRes` | (see the help) | 3.58 |
| `ML` | object: mill | 6 | `erm.cpp` `ERM_SetMill` | `B` | 3.58 |
| `WH` | object: witch hut | 6 | `erm.cpp` `ERM_SetWHat` | `S` | 3.58 |
| `GD` | object: garden | 5 | `erm.cpp` `ERM_SetGarden` | `BTN` | 3.58 |
| `LE` | map event | 5 | `erm.cpp` `ProcessMes` | `MGXEPOURFNABSCDIL` | 3.58 |
| `QW` | quest log | 5 | `erm.cpp` `ERM_Qwest` | `A` | 3.58 |
| `FR` | object: campfire | 4 | `erm.cpp` `ERM_SetFire` | `B` | 3.58 |
| `IP` | network | 4 | `Monsters.cpp` `ERM_NetworkService` | `DVWFR` | 3.58 |
| `SC` | object: scholar | 4 | `erm.cpp` `ERM_SetScoolar` | `TPSL` | 3.58 |
| `SG` | object: sign | 4 | `erm.cpp` `ERM_Sign` | `M` | 3.58 |
| `WM` | object: windmill | 4 | `erm.cpp` `ERM_SetWMill` | `B` | 3.58 |
| `SY` | object: shipyard | 3 | `erm.cpp` `ERM_Shipyard` | `OP` | 3.58 |
| `MF` | monster abilities | 2 | `Monsters.cpp` `ERM_MonFeature` | (see the help) | 3.58 |
| `SN` | sound | 2 | `sound.cpp` `ERM_Sound` | `SP` | 3.58 |
| `HL` | hero | 1 | `erm.cpp` `ERM_HeroGainLevel` | `SR` | 3.58 |
| `KT` | object: tree of knowledge | 1 | `erm.cpp` `ERM_SetKTree` | `SN` | 3.58 |
| `LN` | object: lean-to | 1 | `erm.cpp` `ERM_SetLean` | `BN` | 3.58 |
| `MT` | object: monolith | 1 | `erm.cpp` `ERM_SetMonolit` | `N` | 3.58 |
| `SK` | object: skeleton | 1 | `erm.cpp` `ERM_SetSkelet` | `ANS` | 3.58 |
| `ST` | object: stone | 1 | `erm.cpp` `ERM_SetStone` | `N` | 3.58 |
| `UR` | object: university | 1 | `erm.cpp` `ERM_Univer` | `S` | 3.58 |
| `WG` | object: wagon | 1 | `erm.cpp` `ERM_SetWagon` | `SBAR` | 3.58 |
| `WT` | object: warrior's tomb | 1 | `erm.cpp` `ERM_SetWTomb` | `AS` | 3.58 |
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
| `TL` | real-time timer | 0 | `timer.cpp` `ERM_TL` | `ECTSD` | 3.59 |
| `UX` | universal (ext.) | 0 | `erm.cpp` `ERM_UniversalEx` | `KMTSGAV` | 3.59 |
| `VC` | debugging | 0 | `erm.cpp` `ERM_VarControl` | `CBEYNW` | 3.58 |

Flow-control pseudo-receivers: `if`, `el`, `en` (3.58), `la`, `go` (3.59).
Extraction notes: `UN` and `EX` have nested `switch` statements on the first parameter, so their lists are taken from
the help. `BM` is implemented by the `ERM_BRound` function (the `ERM_BMonster` entry is commented out); `MR`/`MF` are not
built on `switch(Cmd)`. All other letter lists are exact.

## 2. Command specification format

`Receiver:Command` → **Meaning** · **Inputs** · **Outputs** · **Affected state** · **Context** ·
**Side effects** · **Olden Era equivalent** (filled in in `Compatibility/ERM_Compatibility.md`).

`$` — a parameter that allows set/get/check/`d`; `#` — a plain number/variable.

## 3. Main receivers (implemented in `src/WoG.Erm/Receivers`)

### VR — variables (`ERM_Variable`)
Selector: a single variable reference (evaluated at execution time; may be indirect, `!!VRvy1:`).

| Command | Meaning | Inputs | Output / state | Details from the code |
|---------|---------|--------|----------------|-----------------------|
| `S$` | assign | a value, `^text^`, or a z-var for strings | var ← value | `?var` copies *into* the parameter; for z: a number ≠ 0 = copy the string with that index; for e — set only |
| `R$` / `R0/$seed` | add a random 0…$ | maximum (inclusive) | var += rnd | the 2-parameter form reads/sets the seed |
| `T$` | add a "temporary" random 0…$ | | | |
| `+$ -$ *$ :$ %$` | arithmetic | operand | var op= operand | `:`/`%` by 0 → a message, no change; for strings `+` = concatenation |
| `&$ \|$ X$` | bitwise AND/OR/XOR | | | `^` — a deprecated synonym for `X` (prints a warning) |
| `H#` | flag # ← string is non-empty | flag 1…1000 | flag | z only |
| `C$/$/…` | assign consecutive variables | up to 16 values | var, var+1, … | integers only; each parameter supports set/get/check |
| `U$` | flag 1 ← string **ends** with $ | z-var or text | flag 1 | see item 9 of §11 in `01_ERM_Language.md` |
| `M1/z/start/length` `M2/z/number` `M3/value/base` `M4/?length` `M5/?first` `M6/?last` | string operations | | | `M2` — token by the delimiters " ,.\t\n\a"; `M3` — itoa |
| `V$` | parse z into an integer/float | z index | var | |

### FU — functions (`ERM_Function`); DO — loops (`ERM_Do`)
| Command | Meaning |
|---------|---------|
| `FU#:P$…` | call function # with ≤ 16 arguments → `x1…x16`; `?var` arguments receive the final `x` values |
| `FU:E[#]` | exit the current section (with # > 0 — skip the next # sections; < 0 — go back) |
| `FU:D…` | network "remote" call → **not supported (no network layer)** |
| `FU:C#` | enable/disable checking of y-variables outside functions (debugging) |
| `FU:X#/$` [3.59] | how argument # was passed |
| `DO#/a/b/s:P$…` | loop `x16=a..b step s` calling function # |

### MC — macros (`ERM_Macro`)
`!!MCv5:S@name@;` binds the macro `name` (≤ 16 characters) to the selector variable; after that, `$name$` can be
written instead of the variable. Name lookup order: numeric macros **[3.59]**, f…t, v, z, w. Macros
are global and are saved.

### IF — messages/dialogs/flags (inline `IF`)
| Command | Meaning | Implementation |
|---------|---------|----------------|
| `M^text^` / `M1/$` / `M$/type/text` | show a message (with substitutions) | UI adapter `ShowMessage` (type 2 — question) |
| `Q#^text^` / `Q#/z` | "yes/no" question → flag # | UI adapter `AskYesNo`; the variants with pictures need a custom UI |
| `V#/$` | flag # ← $ (0/1) | core |
| `W$` | select the hero for `w` variables (−1 — current) | core |
| `A#`, `S#`, `R#` | flags 1…10 from the decimal digits of a number (A — assign all, S — set only, R — reset only) | core |
| `G…`, `B…`, `D…`, `E…`, `N…`, `P…`, `X…`, `F…`, `L^text^` | complex dialogs, the battle log, etc. | needs a custom UI layer |

### UN — universal (`ERM_Universal`) — implemented part
| Command | Meaning |
|---------|---------|
| `P#/$` | read/write WoG option # (0…999) — `PL_WoGOptions[0][#]`; writing to 3/6 immediately enables/disables commanders |
| `P$` (1 parameter) | legacy form: option 0 |
| `C…` | direct write to H3 memory — **fundamentally unsupported** |
| `I`,`R`,`O`,`T`,`S`,`V`,`A`,`X`,… | placing/removing objects, sizes, etc. — through the map adapter (not mapped yet) |

### HE — hero (inline `HE`) — implemented part
Selector: `#` hero number, `-1` current hero, `-10/-20` attacker/defender in battle, `x/y/l` — the hero at that position.

| Command | Meaning | State |
|---------|---------|-------|
| `E$` / `E$/$lvl` | experience (and level) | hero experience |
| `F$/$/$/$` | primary skills (Attack/Defense/Power/Knowledge); a 5th parameter `1` = base values without artifacts (read-only) | hero stats |
| `I$` | spell points | mana |
| `W$` | movement points | |
| `S#/$` | secondary skill level 0…3 | skills |
| `M#/$` | spell in the spell book 0/1 | spell book |
| `A#` / `A1/art/slot` `A2/art/?n/?m` `A3/art/n/m` `A4/art` | give/take/count/equip an artifact | inventory |
| `C0/slot/$type/$count[/$exp[/mode]]` | army slot (+ stack experience) | army |
| `C1/type/$type/$count` | all stacks of the given type | army |
| `C2/type/count/ask` | add a stack | army |
| `O$` | owner | |
| `P$x/$y/$l[/style]` | move | position (generates `HM`/visit triggers) |
| `N?$` | hero number | |
| `K` | kill the hero | |

### OW — players (`ERM_Owner`) — implemented part
`R#/res/$` resources (player −1 = current); `C?$` current player; `A#/$` active hero; `I#/$[/$]` AI/human
(and whether alive); `G#/$` the player at this PC.

### MA — creature type data (`ERM_MonAtr`)
`A D P S M E N F I G R H V C L O U X B` = attack, defense, HP, speed, damage min/max, shots, Fight value,
AI value, growth, horde growth, max/min count on the map, cost, level, town, upgrades-to, flags, casts.
Applies to the whole type; is saved.

### TM — timers (`ERM_Timer`)
`S$first/$last/$period/$owners` · `E$player` · `D$player`. The fields are unsigned 16-bit. A timer fires
at the start of a player's day if `first ≤ day ≤ last`, `(day-first) % period == 0`, and the player's bit is set
(`RunTimer`). With a period of 0, WoG divides by zero (crash); the port simply does not run such a timer.

### CO — commanders (`ERM_NPC`, `npc.cpp`) — see `04_Commanders.md`
Selector: `-1` the current hero's commander, `-2` all, `-3` the attackers' extra commander, `-4` the defenders', `#` hero number.
`E$` hired · `D$` dead · `T$` class · `H$` hero class · `P$`/`P#/$` primary skills · `S#/$` skills · `B…` special bonuses ·
`A1…A4` artifacts · `N$` name · `X0/1/2` experience/level.

### EX — stack experience (`ERM_StackExperience`) — see `05_Creature_Experience.md`
Selector: `hero/slot` or `x/y/l/slot[/ownerType]`. `A$type/$count/$exp` · `T$` · `N$` · `E$` (experience per
creature) · `R$art/$subtype` or `R$has/$art/$subtype/$copies` (stack artifact) · `C…` stack merging.

### BA / BM / BU / BG / BH / BF — battle
See `07_Battle_Map_Towns_Features.md`; they depend on the battle adapter.

## 4. Other receivers

All other receivers (objects `MN SC CH WT KT FR LN ST WG SK SP WM SW MT GD ML DW WH SY GR SR SG UR PM CB`,
`PO TR OB MO AR LE GE CE HT QW`, input `CM MM`, sound `MP SN`, network `IP`, AI `AI EA`) are parsed, have a
compatibility status, and call the adapter interfaces. The per-command status is in `Compatibility/ERM_Compatibility.md`,
generated from the runtime registry (`dotnet run --project tools/WoG.ErmTool -- compat`).
