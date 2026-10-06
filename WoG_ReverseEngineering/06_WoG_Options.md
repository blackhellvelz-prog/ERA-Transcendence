# WoG Options — reverse-engineered model

Sources: `T1/erm.h` (`PL_*` macros), `T1/wogsetup.cpp` (options dialog, load/save), `T1/erm.cpp`
(`ERM_Universal` case `P`, `SaveERM`), the 3.58f scripts (S7) for which option each script reads.
Implemented by `src/WoG.Core/Options`.

## 1. Storage

* `int PL_WoGOptions[2][1000]` (`PL_WONUM = 1000`). Row 0 = the options in effect; row 1 = a copy used
  by the dialog's *Restore*/*Cancel*.
* The options dialog has 8 pages × 4 groups × up to 20 items. Each item is bound to one option index
  (`InternalVars[page][group][item]`); the binding and the texts come from `ZSETUP00.TXT` in the WoG
  install (not in S1). A group whose items are radio buttons stores the selected item number in a single
  option.
* **Inverted options:** indices **1…4** are stored negated relative to the check box
  (`if (ind>0 && ind<5) value = !checked`) — they are "No …" flags (`PL_TowerStd`, `PL_MLeaveStd`,
  `PL_NoNPC`, `PL_NoTownDem`).
* Dependencies between check boxes are hard-coded in `CheckDepend()` (e.g. if option *[0][2][4]* is off,
  five dependent items are greyed). The port encodes them as data (`OptionDependency`).
* Persistence: the first half of `PL_WoGOptions` (row 0) is written into every savegame (`SaveERM`).
  Option presets are saved to/loaded from `.dat` files (`SaveSetupState`, default `WoGSetupEx.dat`).
* Scripts read and write options with `!!UN:P#/$` (any index 0…999). Writing 3 or 6 (commanders)
  immediately enables/disables commanders for all heroes; writing 0…10 or 900…907 also updates the
  "reset" copies (`PL_OptionReset`, `PL_OptionReset2`) so the value survives `ResetWoG*` calls.

## 2. Engine options (hard-coded)

| Index | Macro | Meaning |
|-------|-------|---------|
| 0 | `PL_ExtDwellStd` | 8th-level external dwellings standard behaviour |
| 1 | `PL_TowerStd` | (inverted) enhanced town towers |
| 2 | `PL_MLeaveStd` | (inverted) monsters may leave the army |
| 3 | `PL_NoNPC` | (inverted) **commanders enabled** |
| 4 | `PL_NoTownDem` | (inverted) town demolition allowed |
| 5 | `PL_ApplyWoG` | apply WoG to non-WoG maps (wogify level) |
| 6 | `PL_NPC2Hire` | commanders must be hired in town |
| 7 | `PL_DwellAccum` | dwellings accumulate creatures |
| 8 | `PL_GuardAccum` | dwelling guards accumulate |
| 9 | `PL_CentElf` | centaur/elf tweak |
| 10 | `PL_MLeaveStyle` | monster leaving style |
| 900 | `PL_CrExpEnable` | **stack experience enabled** |
| 901 | `PL_CrExpStyle` | experience sharing style 0…3 |
| 902 | `PL_LeaveArt` | leave artifacts on death |
| 903 | `PL_CheatDis` | cheats disabled |
| 904 | `PL_ERMErrDis` | ERM error dialogs suppressed |
| 905 | `PL_ERMError` | ERM error state |
| 906 | `PL_ExpGainDis` | experience gain disabled |
| 907 | `PL_NewHero` | new hero setup |

## 3. Script options

Each WoG script is gated by its own option and usually reads sub-options. The table lists every option
index each 3.58f script reads (`UN:P#`), extracted from S7:

| Script file (3.58f) | Options read with UN:P |
|---|---|
| 1 wog - cheat menu | 77 903 904 905 |
| 2 wog - commander sanctuary | 3 76 |
| 3 wog - secondary skill text | 23 35 58 71 75 102 103 190 191 201 202 203 204 205 206 207 208 209 210 211 212 213 214 215 216 217 218 |
| 4 wog - summon elementals | 74 |
| 5 wog - enhanced war machines 3 | 73 |
| 6 wog - random hero | 72 77 904 905 |
| 7 wog - enhanced artifacts | 71 219 |
| 8 wog - death chamber | 70 |
| 9 wog - custom alliances | 69 |
| 10 wog - new battlefields | 68 |
| 11 wog - neutral town | 50 67 133 |
| 12 wog - commander witch huts | 66 |
| 13 wog - monolith costs | 65 |
| 14 wog - tobyn's scripts | 36 54 55 75 188 189 190 191 192 193 194 201 203 204 |
| 15 wog - passable terrain | 63 |
| 16 wog - split decision | 56 62 218 |
| 17 wog - protection from the elements | 61 |
| 18 wog - forgotten shrine | 60 |
| 19 wog - piercing shot | 59 |
| 20 wog - espionage | 58 |
| 21 wog - neutral units | 53 57 231 232 235 900 |
| 22 wog - metamorphs | 56 |
| 23 wog - enhanced war machines 2 | 55 900 |
| 24 wog - enhanced war machines 1 | 54 |
| 25 wog - dungeon of the dragonmaster | 53 143 234 900 |
| 26 wog - mirror of the home-way | 52 |
| 27 wog - enhanced commanders | 3 51 186 |
| 28 wog - enhanced monsters | 50 900 |
| 29 wog - henchmen | 49 218 |
| 30 wog - enhanced secondary skills | 201 202 203 204 205 206 207 208 209 210 211 212 213 214 |
| 31 wog - creature relationships | 47 |
| 32 wog - berserker flies | 46 900 |
| 33 wog - castle upgrading | 45 |
| 34 wog - emerald tower | 44 900 |
| 35 wog - obelisk runes | 43 |
| 36 wog - garrisons | 42 |
| 37 wog - battle extender | 41 |
| 38 wog - first money | 40 |
| 39 wog - hero specialization boost | 3 39 67 198 |
| 40 wog - karmic battles | 38 |
| 41 wog - rebalanced factions | 35 37 39 50 67 103 188 189 191 198 199 202 203 205 207 210 216 |
| 42 wog - mithril enhancements | 36 149 170 171 |
| 43 wog - mysticism skill enhancement | 35 |
| 44 wog - cards of prophecy | 34 207 233 |
| 45 wog - living scrolls | 33 |
| 46 wog - summoning stones | 32 |
| 47 wog - treasure chest 2 | 31 33 |
| 48 wog - adventure cave | 30 |
| 49 wog - chest | 29 |
| 50 wog - school of wizardry | 28 |
| 51 wog - spell book | 27 |
| 52 wog - artificer | 26 71 102 159 160 161 162 164 167 168 |
| 53 wog - map options | 22 23 25 33 34 37 51 67 100 105 106 131 144 145 146 147 148 150 151 152 153 154 155 156 157 158 159 160 161 162 163 164 166 167 168 169 173 174 175 176 177 178 179 180 182 183 184 185 186 187 193 220 221 222 223 226 227 228 233 234 236 237 238 240 241 243 244 246 247 |
| 54 wog - enhanced dwelling hint text | 24 |
| 55 wog - sorcery skill enhancement | 23 33 36 |
| 56 wog - monster mutterings | 22 |
| 57 wog - freelancers guild | 21 |
| 58 wog - week of monsters | 20 134 135 136 200 234 |
| 59 wog - masters of life | 19 67 |
| 60 wog - alms house | 18 |
| 61 wog - potion fountains | 17 |
| 62 wog - battle academy | 16 |
| 63 wog - mysterious creature dwelling | 15 |
| 64 wog - altar of transformation | 14 67 |
| 65 wog - tavern gambling game | 13 |
| 66 wog - living skull | 12 |
| 67 wog - palace of dreams | 11 900 |
| 68 wog - magic mushrooms | 110 |
| 69 wog - market of time | 109 193 |
| 70 wog - junk merchant | 108 |
| 71 wog - fishing well | 3 107 |
| 72 wog - hourglass of asmodeus | 56 106 |
| 73 wog - bank | 105 181 225 |
| 74 wog - arcane tower | 104 |
| 75 wog - secondary skills boost | 103 215 216 217 218 |
| 76 wog - artifact boost | 102 |
| 77 wog - map rules | 63 67 101 119 193 230 |
| 78 wog - wogify | 3 11 12 13 14 15 16 17 18 21 26 27 28 29 30 31 32 44 52 60 63 70 76 104 107 108 109 110 132 133 137 138 139 140 141 142 143 165 176 177 195 196 219 226 227 229 234 236 237 238 241 242 243 245 248 900 901 |

Pattern: options **11…77** are the per-script on/off switches (the first number in each row of
non-Wogify scripts), **100…249** are sub-options (map options, banned spells/artifacts, enhanced
secondary skills 201…214 …), **900…907** the engine switches above.

## 4. Requirements for the port

1. Options are **separate values**, never merged into one toggle: the port stores all 1000 indices
   individually (`WoGOptions` = `int[1000]` + metadata).
2. Defaults come from the user's `ZSETUP00.TXT` / preset `.dat` when present; otherwise from
   `Compatibility/options-defaults.json` (our documented defaults, each marked *verified* or *assumed*).
3. Options are saved with the game (row 0) and restored on load **before** any `!?GM0` trigger runs.
4. The inverted indices 1…4 must keep their stored polarity (scripts test the stored value).
5. `UN:P` side effects for 3/6 and the reset copies are reproduced in `UniversalReceiver`.
