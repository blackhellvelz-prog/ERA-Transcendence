**English** | [Русский](ERM_Compatibility.ru.md)

# ERM Compatibility: Receivers and Commands

**This file is generated** from the runtime's receiver registry:
`dotnet run --project tools/WoG.ErmTool -- compat`. Do not edit the table by hand — change `Declare(...)` in
`src/WoG.Erm/Receivers/*.cs`.

How to read it:
* "Implemented: yes" — the receiver is executed by the runtime; the status is given for each command letter.
  Letters not listed in the row produce a "wrong command" error, as in WoG.
* "Implemented: no" — the receiver is recognized by the parser (scripts that use it load), but when executed
  the command is recorded in the compatibility report as UNSUPPORTED and execution of the line continues.
  Nothing is faked.
* The status refers to **Olden Era**: for example, `MA` is fully implemented in the runtime, but on Olden Era
  it is PARTIALLY SUPPORTED because the engine's stat model is different.

Which receivers WoG scripts actually need — see the usage column in
`WoG_ReverseEngineering/03_ERM_Receivers.md`. A run of all 78 3.58f scripts as a new game on the reference
engine (`WoG.ErmTool run`) currently runs into: `HT:P/W`, `OW:T`, `UN:A/B/R/V/X`, `IF:D/F` and ERT strings
(`z > 1000`) — these are the next tasks for extending the runtime.

| Receiver | Implemented | Commands and status |
|---|---|---|
| `AI` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `AR` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `BA` | yes | `AEHOPQS` PARTIALLY SUPPORTED (BA:H/O/P/Q/S/E/A — the battle as the adapter saw it start: attacker = the active hero of the player whose turn it is, defender = the monster squad next to it (hero-vs-hero and town battles are not told apart yet); read only); `BD` UNSUPPORTED (BA:D/B — cancelling a battle and its background are not mapped yet); `M` UNSUPPORTED (BA:M — the armies of the battle sides are not mapped yet) |
| `BF` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `BG` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `BH` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `BM` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `BU` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `CA` | yes | `B` PARTIALLY SUPPORTED (CA:B — buildings by H3 number: dwellings, mage guild, fort/citadel/castle, village/town/city hall, tavern, marketplace, resource silo and grail are Olden Era's buildings (a higher level counts its lower ones as built); the other numbers read as not built and cannot be built; B1/B6 build through the game's construction, free and without using the day's one; B4/B5 allow or forbid; B3…/1 (bonus taken) reads as built; B2 is not possible: Olden Era has no demolition); `D` UNSUPPORTED (CA:D — the recruitment window with several creatures of the Battery plugin (closed-source ERA DLL)); `GNORS` PARTIALLY SUPPORTED (CA:O/G/N/R/S — owner, mage guild level and spells, name (Olden Era's own, localized), built today, daily gold income; see the matrix for what can be changed); `HPTU` PARTIALLY SUPPORTED (CA:H/P/T/U — garrison and visiting hero, position, town type (Olden Era factions as the closest H3 town), town number: read; moving heroes into a town, moving a town, changing its type or number are not mapped); `I` PARTIALLY SUPPORTED (CA:I — the ruined looks of a town (I-1, I1..I3) are not mapped; I0 (the look of its buildings) is what Olden Era shows); `M` PARTIALLY SUPPORTED (CA:M — M1 creatures to hire (Olden Era keeps one number per dwelling: the row of the building that stands is used), M2 the town's own garrison (creature ids via IdMap), M4 weekly growth (read only, as in WoG); M3 (summoning portal) is not mapped) |
| `CB` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `CD` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `CE` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `CH` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `CI` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `CM` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `CO` | yes | `ABDEHNPSTX` EMULATED (commanders are an emulated entity in Olden Era) |
| `DG` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `DL` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `DO` | yes | `P` FULLY SUPPORTED |
| `DW` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `EA` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `EX` | yes | `AENRT` EMULATED (experience is external WoG state applied to OE stacks); `C` UNSUPPORTED (stack merging is not implemented yet) |
| `FC` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `FR` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `FU` | yes | `CEPX` FULLY SUPPORTED; `D` FULLY SUPPORTED (FU:D — call on the remote player: a single-player game has none, so nothing runs (as in WoG offline)) |
| `GD` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `GE` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `GR` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `HD` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `HE` | yes | `A` PARTIALLY SUPPORTED (A: artifacts by position (0..18 worn, 19..82 backpack); Olden Era maps H3 artifacts by effect or name (id-maps/artifact.json, 61 of 171), its items fit only their own slot type, has no war machines (the spellbook is always there) and no gaps in the backpack; A5 slot locks are not mapped); `B` PARTIALLY SUPPORTED (B: name (B0), biography (B1, B3) and class (B2); Olden Era shows a hero's name and biography through its localization, so a new one is the text of that hero's key for the game, kept with the WoG state; the class is the closest H3 class of the hero's faction and might/magic kind and cannot be changed); `C` PARTIALLY SUPPORTED (ids via IdMap; display-slot forms are not supported); `DGLRTUVY` UNSUPPORTED (not mapped yet); `EFIKNOPW` PARTIALLY SUPPORTED (values go through the adapter; OE primary stats differ (see the matrix)); `H` PARTIALLY SUPPORTED (H: the army a hero type is hired with (Olden Era's start squad of the type, kept with the WoG state); creatures without an Olden Era unit cannot be set); `M` PARTIALLY SUPPORTED (M: spells; Olden Era maps H3 spells by effect (id-maps/spell.json, 37 of 70), a spell it does not have reads as not known and cannot be learned, a specialist knows a spell as its masterful variant, the spells of a hero that is not on the map cannot be changed); `S` PARTIALLY SUPPORTED (S: secondary skills; Olden Era maps H3 skills by effect (id-maps/skill.json, 14 of 28) and a level 2 or 3 given by a script brings the game's own sub-skill choice; a skill it does not have reads as not learned and cannot be learned, lowering a learned skill and changing the hero-screen order are not mapped, Olden Era-only skills are invisible to scripts); `X` PARTIALLY SUPPORTED (X: the specialty as H3's record; an Olden Era specialty reads as the closest H3 one (a creature, a spell, a resource, or a secondary skill whose effect it has), others have none; it cannot be changed) |
| `HL` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `HO` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `HT` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `IF` | yes | `ARSVW` FULLY SUPPORTED; `BDEFGLNPTX` UNSUPPORTED (special WoG dialogs (pictures, sphinx, checkboxes, multiple choice) need a custom UI layer); `MQ` PARTIALLY SUPPORTED (text messages and yes/no questions; variants with pictures need custom UI) |
| `IP` | no | UNSUPPORTED — network ERM is out of scope (single-player only) |
| `KT` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `LD` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `LE` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `LN` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `MA` | yes | `ACDEFILMOPSU` PARTIALLY SUPPORTED (MA — creatures with an Olden Era unit change the game's unit type (attack, defence, hit points, speed, damage, cost, unit value; level, town and upgrade read only); creatures without one use the ERA installation's zcrtrait.txt and change nothing in the game; changes are saved with the game); `BGHNRVX` PARTIALLY SUPPORTED (MA:N/G/R/H/V/B/X — shots, growth, adventure-map counts, casts and flags exist only for creatures without an Olden Era unit (from zcrtrait.txt); Olden Era units have no such stats) |
| `MC` | yes | `S` FULLY SUPPORTED |
| `MF` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `ML` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `MM` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `MN` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `MO` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `MP` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `MR` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `MT` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `MW` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `OB` | yes | `BDEHMRS` UNSUPPORTED (OB:D/E/R/S/M/H/B — disabling objects, auto-answers and hints need the object visit hook (not verified yet)); `C` UNSUPPORTED (OB:C — the control word is H3's object setup data: different engine); `TU` PARTIALLY SUPPORTED (OB:T/U — the type and subtype of the object on a square (Format OB via id-maps/object.json); they cannot be changed) |
| `OW` | yes | `ACGIR` PARTIALLY SUPPORTED (resource ids via IdMap); `DKS` UNSUPPORTED (OW:D/K/S — days without a town, keymaster tents and adventure-map spells have no Olden Era equivalent mapped); `HOTVW` PARTIALLY SUPPORTED (OW:H/O/T/V/W — the player's heroes and hero list, team, tavern heroes and towns (the player's towns in town-number order); reordering the lists, changing teams and the tavern are not mapped); `N` PARTIALLY SUPPORTED (OW:N — the player's towns by list slot; the selected town and reordering the list are not mapped) |
| `PM` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `PO` | yes | `BCHNOSTV` FULLY SUPPORTED (PO — WoG data of a map square, kept in the WoG state and saved with it) |
| `QW` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `SC` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `SG` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `SK` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `SN` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `SP` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `SR` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `SS` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `ST` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `SW` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `SY` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `TL` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `TM` | yes | `DES` FULLY SUPPORTED |
| `TR` | yes | `EPT` PARTIALLY SUPPORTED (TR:T/P/E — terrain (Olden Era biomes as the H3 terrain of the matching town), road, blocked (red) and entrance (yellow) squares; read only; rivers are 0); `G` UNSUPPORTED (TR:G — H3 terrain overlays (magic plains, cursed ground…) have no Olden Era equivalent mapped); `V` UNSUPPORTED (TR:V — square visibility (fog of war) is not mapped yet) |
| `UN` | yes | `A` PARTIALLY SUPPORTED (UN:A — artifact types come from the ERA installation's artraits.txt and are kept per game; Olden Era items are not linked to them yet, so changes do not affect the game's items, and the map ban does not affect map generation); `BDEFGHIKLMOQSTWYZ` UNSUPPORTED (UN map/object/global commands are not mapped yet); `C` UNSUPPORTED (UN:C writes to H3 memory addresses — impossible on a different engine); `J` PARTIALLY SUPPORTED (UN:J — J0 spell bans (kept; Olden Era's guilds do not use them yet), J2 difficulty (Olden Era's AI difficulty), J8/J9 files and folders (the write folder first, then the ERA installation), J10 variable log, J11; J1, J3-J7, J12, J13 are not mapped yet); `N` PARTIALLY SUPPORTED (UN:N — names of artifacts, spells, creatures and secondary skills from the ERA installation's text tables; N5/N6 ini values (written under BepInEx/config/WoG/era-root); N2 building names are not read yet); `P` FULLY SUPPORTED; `R` PARTIALLY SUPPORTED (UN:R — R1-R4 redraws: Olden Era redraws its screens itself; R5-R7 (mouse pointer shape, delay) are cosmetic and do nothing); `U` PARTIALLY SUPPORTED (UN:U — Olden Era map objects with an H3 type (Compatibility/id-maps/object.json; Olden Era-only objects have types from 1000); a monster squad of several unit types counts as its first unit); `V` FULLY SUPPORTED (UN:V — WoG/ERM versions of the dialect (ERA 400/3931, WoG 358/281); a single-player game: no network, no cheat tracking (0)); `X` FULLY SUPPORTED (UN:X — map size; Olden Era maps have no underground (levels 0)) |
| `UR` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `UX` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `VC` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `VR` | yes | `%&*+-:CHMRSTUVX^\|` FULLY SUPPORTED |
| `WG` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `WH` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `WM` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `WT` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
