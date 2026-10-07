**English** | [Русский](ERM_Compatibility_ERA.ru.md)

# ERM Compatibility in ERA: Receivers and Commands

**This file is generated** from the runtime's receiver registry in ERA mode:
`dotnet run --project tools/WoG.ErmTool -- compat --era`. Do not edit the table by hand — change `Declare(...)`
in `src/WoG.Erm/Receivers/*.cs`. The table for classic WoG 3.58 is `ERM_Compatibility.md`.

Differences of ERA mode from WoG: `VR`, `FU`, `DO` are replaced with the versions rewritten by ERA
(`EraReceivers.cs`), `SN` is added (`SnReceiver`, Era API functions — `EraApi.cs`), `if/el/en/re/br/co` are
executed by the interpreter itself (`EraProcess.cs`). The remaining receivers are WoG's, but parameters,
`Apply` and strings work by ERA rules (`ErmCall`, `EraValues.cs`).

How to read it — the same as `ERM_Compatibility.md`: "no" = scripts load, the command is recorded in the report
as UNSUPPORTED, execution continues; nothing is faked. The status refers to **Olden Era**.

A run of the whole ERA project (183 scripts) as a new game on the reference engine — 0 errors. Unsupported at
startup: `UN:C` (H3 memory), `SN:E` (H3 code), `FU:D` (network), `UN:A/R/X/N/U/V/J` (map and objects),
`SN:L/B`, `IF:G`.

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
| `CM` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `CO` | yes | `ABDEHNPSTX` EMULATED (commanders are an emulated entity in Olden Era) |
| `DL` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `DO` | yes | `P` FULLY SUPPORTED |
| `DW` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `EA` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `EX` | yes | `AENRT` EMULATED (experience is external WoG state applied to OE stacks); `C` UNSUPPORTED (stack merging is not implemented yet) |
| `FR` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `FU` | yes | `AEPS` FULLY SUPPORTED; `D` FULLY SUPPORTED (FU:D — call on the remote player: a single-player game has none, so nothing runs (as in WoG offline)) |
| `GD` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `GE` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `GR` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `HE` | yes | `ACS` PARTIALLY SUPPORTED (ids via IdMap; display-slot forms are not supported); `BDGHLRTUVXY` UNSUPPORTED (not mapped yet); `EFIKMNOPW` PARTIALLY SUPPORTED (values go through the adapter; OE primary stats differ (see the matrix)) |
| `HL` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `HO` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `HT` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `IF` | yes | `ARSVW` FULLY SUPPORTED; `BDEFGLNPTX` UNSUPPORTED (special WoG dialogs (pictures, sphinx, checkboxes, multiple choice) need a custom UI layer); `MQ` PARTIALLY SUPPORTED (text messages and yes/no questions; variants with pictures need custom UI) |
| `IP` | no | UNSUPPORTED — network ERM is out of scope (single-player only) |
| `KT` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `LE` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `LN` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `MA` | yes | `ABCDEFGHILMNOPRSUVX` PARTIALLY SUPPORTED (the engine's stat model differs (initiative/speed, no shots)) |
| `MC` | yes | `S` FULLY SUPPORTED |
| `MF` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `ML` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `MM` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `MN` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `MO` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `MP` | no | UNSUPPORTED — MP — H3 music (mp3): Olden Era has its own music |
| `MR` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `MT` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `MW` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `OB` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `OW` | yes | `ACGIR` PARTIALLY SUPPORTED (resource ids via IdMap); `DHKNOSTVW` UNSUPPORTED (not mapped yet) |
| `PA` | no | UNSUPPORTED — PA — receiver of the "receiver pa.era" plugin (closed-source ERA DLL) |
| `PM` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `PO` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `QU` | no | UNSUPPORTED — QU — receiver of the "receiver qu.era" plugin (closed-source ERA DLL) |
| `QW` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `RD` | no | UNSUPPORTED — RD — H3 creature recruitment window (Dwellings.pas): needs a UI adapter |
| `SC` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `SG` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `SK` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `SN` | yes | `ABL` UNSUPPORTED (SN:L/A/B — DLL loading, addresses and H3 process memory: different engine); `CDGIKMQTVWX` FULLY SUPPORTED; `E` UNSUPPORTED (SN:E — calling a function by address in the H3 exe: different engine); `F` PARTIALLY SUPPORTED (SN:F — Era API functions: those used by the ERA Project scripts are ported (see EraApi); DLL/Win32 functions are not); `H` UNSUPPORTED (SN:H — object/monster hints: needs an Olden Era UI adapter); `O` UNSUPPORTED (SN:O — object entrance tile: needs a map adapter); `P` UNSUPPORTED (SN:P — H3 sound playback: Olden Era sounds are different); `R` UNSUPPORTED (SN:R — H3 resource redirection (lod/def): Olden Era resources are different); `S` UNSUPPORTED (SN:S — sound name in !?SN: the sound trigger is not ported) |
| `SP` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `SR` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `SS` | no | UNSUPPORTED — SS — receiver of the secondary skills plugin (closed-source ERA DLL) |
| `ST` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `SW` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `SY` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `TL` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `TM` | yes | `DES` FULLY SUPPORTED |
| `TR` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `UN` | yes | `ABDEFGHIJKLMNOQRSTUVWXYZ` UNSUPPORTED (UN map/object/global commands are not mapped yet); `C` UNSUPPORTED (UN:C writes to H3 memory addresses — impossible on a different engine); `P` FULLY SUPPORTED |
| `UR` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `VC` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `VR` | yes | `%&*+-:BCFHMRSTUVXZ\|~` FULLY SUPPORTED |
| `WG` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `WH` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `WM` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
| `WT` | no | UNSUPPORTED — receiver is not mapped to the target engine yet |
