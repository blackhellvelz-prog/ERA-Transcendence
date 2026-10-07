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
| `BA` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `BF` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `BG` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `BH` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `BM` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `BU` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `CA` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
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
| `HE` | yes | `ACS` PARTIALLY SUPPORTED (ids via IdMap; display-slot forms are not supported); `BDGHLRTUVXY` UNSUPPORTED (not mapped yet); `EFIKMNOPW` PARTIALLY SUPPORTED (values go through the adapter; OE primary stats differ (see the matrix)) |
| `HL` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `HO` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `HT` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `IF` | yes | `ARSVW` FULLY SUPPORTED; `BDEFGLNPTX` UNSUPPORTED (special WoG dialogs (pictures, sphinx, checkboxes, multiple choice) need a custom UI layer); `MQ` PARTIALLY SUPPORTED (text messages and yes/no questions; variants with pictures need custom UI) |
| `IP` | no | UNSUPPORTED — network ERM is out of scope (single-player only) |
| `KT` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `LD` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `LE` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `LN` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `MA` | yes | `ABCDEFGHILMNOPRSUVX` PARTIALLY SUPPORTED (the engine's stat model differs (initiative/speed, no shots)) |
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
| `OB` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `OW` | yes | `ACGIR` PARTIALLY SUPPORTED (resource ids via IdMap); `DHKNOSTVW` UNSUPPORTED (not mapped yet) |
| `PM` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `PO` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
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
| `TR` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `UN` | yes | `A` PARTIALLY SUPPORTED (UN:A — artifact types come from the ERA installation's artraits.txt and are kept per game; Olden Era items are not linked to them yet, so changes do not affect the game's items, and the map ban does not affect map generation); `BDEFGHIJKLMNOQRSTUWXYZ` UNSUPPORTED (UN map/object/global commands are not mapped yet); `C` UNSUPPORTED (UN:C writes to H3 memory addresses — impossible on a different engine); `P` FULLY SUPPORTED; `V` FULLY SUPPORTED (UN:V — WoG/ERM versions of the dialect (ERA 400/3931, WoG 358/281); a single-player game: no network, no cheat tracking (0)) |
| `UR` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `UX` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `VC` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `VR` | yes | `%&*+-:CHMRSTUVX^\|` FULLY SUPPORTED |
| `WG` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `WH` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `WM` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `WT` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
