**English** | [Русский](06_WoG_Options.ru.md)

# WoG Options — Reverse-Engineered Model

Sources: `T1/erm.h` (`PL_*` macros), `T1/wogsetup.cpp` (options dialog, loading/saving), `T1/erm.cpp`
(`ERM_Universal` command `P`, `SaveERM`, `FindERM`, `ResetWog*`), 3.58f scripts (S7) — which option each
script reads. Implementation — `src/WoG.Core/Options`.

## 1. Storage

* `int PL_WoGOptions[2][1000]` (`PL_WONUM = 1000`). Row 0 holds the active options; row 1 holds the dialog
  state (from the user's preset) and is used for *Restore*/*Cancel*.
* The dialog has 8 pages × 4 groups × up to 20 items. Each item is bound to one option index
  (`InternalVars[page][group][item]`); the binding and the texts come from the WoG installation's `ZSETUP00.TXT`
  (it is not in S1). A radio-button group stores the number of the selected item in a single option.
* **Inverted options:** indices **1…4** are stored with the opposite sign relative to the checkbox
  (`if (ind>0 && ind<5) value = !checked`) — these are "No …" flags (`PL_TowerStd`, `PL_MLeaveStd`, `PL_NoNPC`,
  `PL_NoTownDem`).
* Dependencies between checkboxes are hard-coded in `CheckDepend()` (for example, if *[0][2][4]* is unchecked,
  five dependent items are grayed out). In the port this is data (`OptionDependency`, planned).
* Saving: the first half of `PL_WoGOptions` (row 0) is written into every saved game (`SaveERM`). Presets are
  written to/read from `.dat` files (`SaveSetupState`, `WoGSetupEx.dat` by default) — implemented in the
  closed-source `ZvsLib1.dll`; the format is **not confirmed**.
* **Default values:** on a new game WoG copies row 1 (the player's choice in the dialog, loaded from the
  preset) into row 0 (`ResetWogify`). The engine has no hard-coded table of defaults. Therefore the port's
  built-in defaults are marked as *assumptions* (`DefaultVerified = false`), and when a user preset exists
  they are taken from it.
* Scripts read and write options via `!!UN:P#/$` (any index 0…999). Writing to 3 or 6 immediately
  enables/disables commanders for all heroes; writing to 0…10 and 900…907 also updates the "reset copies"
  (`PL_OptionReset`, `PL_OptionReset2`), so that the value survives calls to `ResetWoG*`.

## 2. Engine options (hard-coded)

| Index | Macro | Meaning |
|--------|--------|-------|
| 0 | `PL_ExtDwellStd` | standard behavior of external level-8 dwellings |
| 1 | `PL_TowerStd` | (inv.) enhanced town towers |
| 2 | `PL_MLeaveStd` | (inv.) monsters can leave the army |
| 3 | `PL_NoNPC` | (inv.) **commanders enabled** |
| 4 | `PL_NoTownDem` | (inv.) town demolition allowed |
| 5 | `PL_ApplyWoG` | apply WoG to non-WoG maps (wogification level) |
| 6 | `PL_NPC2Hire` | commanders must be hired in a town |
| 7 | `PL_DwellAccum` | dwellings accumulate creatures |
| 8 | `PL_GuardAccum` | dwelling guards accumulate |
| 9 | `PL_CentElf` | centaurs/elves setting |
| 10 | `PL_MLeaveStyle` | monster leaving style |
| 900 | `PL_CrExpEnable` | **stack experience enabled** |
| 901 | `PL_CrExpStyle` | experience splitting style 0…3 |
| 902 | `PL_LeaveArt` | leave artifacts on death |
| 903 | `PL_CheatDis` | cheats disabled |
| 904 | `PL_ERMErrDis` | do not show ERM error dialogs |
| 905 | `PL_ERMError` | ERM error state |
| 906 | `PL_ExpGainDis` | stack experience gain disabled |
| 907 | `PL_NewHero` | new heroes setting |

## 3. Script options

Each WoG script is enabled by its own option and usually reads sub-options. The table lists all option
indices read by each 3.58f script (`UN:P#`), extracted from S7:

| Script file (3.58f) | Options it reads via UN:P |
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

Pattern: options **11…77** are on/off switches for individual scripts (the first number in the row for
non-Wogify scripts), **100…249** are sub-options (map options, spell/artifact bans, enhanced secondary skills
201…214 …), **900…907** are the engine options above.

## 4. Port requirements

1. Options are **separate values** and are never collapsed into a single switch: the port stores all 1000
   indices (`WoGOptions` = `int[1000]` + metadata).
2. Defaults are taken from `ZSETUP00.TXT` / the user's `.dat` preset, if present; otherwise from
   `Compatibility/options-defaults.json` (our documented defaults, each one marked "verified" or
   "assumption").
3. Options are saved together with the game (row 0) and restored on load **before** any `!?GM0`.
4. Inverted indices 1…4 keep their original polarity (scripts check the stored value).
5. The side effects of `UN:P` for 3/6 are reproduced in `UnReceiver` + `CommanderService.ApplyOptions`.
