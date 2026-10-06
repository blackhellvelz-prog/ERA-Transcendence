# ERM triggers (WoG 3.58f)

Sources: `ERM_Triggers[]`, `InitTrigger`, the event-id comment block and the `*Call` functions in
`T1/erm.cpp` (S1); descriptions from the ERM help trigger page (S3).
"Event id" is the internal number the engine raises; C#: `ErmEventId`.

| Trigger | Event id | Fires when | Context set for receivers | Ver |
|---------|----------|-----------|---------------------------|-----|
| `!?FU#` (1…30000) | # | `FU:P`, `DO:P`, or engine/network call | `x1…x16` = args | 3.5x |
| `!?FU-#` (1…100) | 31000+#−1 | local function, same file only | | **3.59** |
| `!?TM#` (1…100) | 30000+#−1 | start of a player's day when timer # is due (`RunTimer(owner)`) | current player | 3.5x |
| `!?TM a/b/c/d` | 31100+i | auto-timer (first/last/period/owners) | | **3.59** |
| `!?HE#` (0…155) | 30100+# | hero # is attacked by an enemy hero or visited by an ally (before battle; again after battle if # won) | `HE-1` = hero | 3.5x |
| `!?BA0`/`!?BA1` | 30300/30301 | start/end of any battle (attacker's PC) | battle context | 3.5x |
| `!?BA50…53` | 30350…30353 | network-side variants; 52/53 both sides | | 3.58 |
| `!?BR` | 30302 | every battle round (twice for the first: −1 and 0) | `v997` = round | 3.5x |
| `!?BG0`/`!?BG1` | 30303/30304 | before/after every battle action | `v997` = round, `BG:` | 3.5x |
| `!?MW0`/`!?MW1` | 30305/30306 | wandering monster reached destination / killed | `v997` = WM id | 3.5x |
| `!?MR0/1/2` | 30307…30309 | magic resistance calc (pre/post/dwarf-style) | `MR:` | 3.58 |
| `!?CM0…4` | 30310…30314 | mouse click on adv. map / town / hero screen / two heroes / battlefield | `CM:` | 3.5x |
| `!?CM5` | 30319 | mouse click on adv. map (right) | | 3.5x |
| `!?AE0`/`!?AE1` | 30315/30316 | unequip/equip artifact (before it happens) | `HE-1` owner, `v998` art, `v999` slot | 3.5x |
| `!?MM0`/`!?MM1` | 30317/30318 | mouse over battlefield / town | | 3.5x |
| `!?MP` | 30320 | MP3 starts | | 3.5x |
| `!?SN` | 30321 | any WAV/M82 sound | `SN:` | 3.58 |
| `!?MG0`/`!?MG1` | 30322/30323 | adventure spell cast pre/post | | 3.5x |
| `!?TH0`/`!?TH1` | 30324/30325 | enter/leave town hall | | 3.58 |
| `!?IP0…3` | 30330…30333 | network battle data exchange | | 3.5x |
| `!?CO0…3` | 30340…30343 | commander dialog open/close, bought, revived | | 3.58 |
| `!?GM0`/`!?GM1` | 30360/30361 | after load / before save | | 3.58 |
| `!?PI` | 30370 | post-instruction: once, after map setup, **new game only** | | 3.58 |
| `!?DL` | 30371 | custom dialog callback | `v998` dlg, `v999` item, `v1000` action | **3.59** |
| `!?HD`,`!?CI0/1`,`!?FC`,`!?DG`,`!?AI` | 30372…30377 | hint text, castle income/growth, flag colour, dig grail, AI importance | | **3.59** |
| `!?HM#` (−1, 0…155) | 30400 / 30401+# | before each hero step (−1 = any hero; runs first) | `HE-1` mover, `v998…v1000` destination | 3.5x |
| `!?HL#` (−1, 0…155) | 30600 / 30601+# | hero gains a level (−1 first) | `HL:` | 3.5x |
| `!$HL#` | 31200… | post level-up | | **3.59** |
| `!?BF` | 30800 | battlefield prepared (before combat) | `BF:` | 3.5x |
| `!?MF0`/`!?MF1`… | 30801… | monster ability calc in battle (defence coefficient, block, hate) | `MF:` | 3.58 |
| `!?TL0…4` | 30900…30904 | real-time timer 1/2/5/10/60 s | | **3.59** |
| `!?OB x/y/l` | 0x10000000\|pos | hero visits the object whose entrance is at x/y/l (before) | `v998…v1000` pos, `HE-1` visitor | 3.5x |
| `!$OB x/y/l` | …\|0x08000000 | after visiting | | 3.5x |
| `!?OB t` / `!?OB t/st` | 0x40000000\|(t<<12)[+st+1] | any object of type t (/subtype st) | | 3.5x |
| `!?LE x/y/l` / `!$LE` | 0x20000000\|pos | local (map) event at x/y/l | | 3.5x |
| `!?GE#` | address of global event | global (timed) event whose text starts with # | | 3.5x |

## Ordering rules (from code)

1. For object visits the engine raises, in order: position trigger `OB x/y/l`, then type/subtype
   `OB t/st`, then type `OB t` (`ERM2Object`), each pre-trigger before the native visit, and the `$`
   post-triggers after it, in the same order. (Help: "The sequence of finding object triggers".)
2. `HM-1` runs before `HM#`; `HL-1` before `HL#` (separate event ids, raised in that order).
3. Battle start: `BA0` (or `BA50` on the defending network PC), then `BA52`; then `BF` while the field
   is prepared; then `BR` with `v997=-1` (before tactics), `BR` with `v997=0`, then per action `BG0`/`BG1`,
   per round `BR`. End: `BA1`/`BA51`, then `BA53`.
4. Within one event id: all sections in load order (§6 of `01_ERM_Language.md`).
5. `FU:E` ends a section; the next section of the same event still runs.

## Era additions (NOT WoG 3.58, excluded)

`77001…` savegame write/read, keypress, hero screen, stack turn, regenerate, interact hooks, damage
hook, chat, game enter/leave, daily timer, battlefield visible, after tactics (`Erm.pas`).
They are recorded here only so they are recognised and rejected with a clear message.
