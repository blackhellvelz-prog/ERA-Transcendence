**English** | [Русский](03_ERA_Events_Loading_Save.ru.md)

# ERA events, loading of mods and scripts, translations, ERT, saved games

Sources: `Erm.pas` (`RegisterErmEventNames`, `TScriptMan`, `GetOrderedPrioritizedFileList`,
`Hook_FindErm_*`, `Hook_RunTimer`), `Triggers.pas`, `Stores.pas`, `Trans.pas`, `AdvErm.pas` (`Save*/Load*`).
Port: `src/WoG.Erm/Era/EraEvents.cs`, `EraScriptSet.cs`, `src/WoG.Host/WoGHost.cs`,
`src/WoG.Erm/Runtime/EraValues.cs` (`EraLang`), `src/WoG.Core/State/EraState.cs`.

## 1. Events

All ERA events are `!?FU<number>` triggers: in ERM 2.0 they are written by name, `!?FU(OnEveryDay)`, and the
preprocessor substitutes the number. The WoG numbers are preserved; ERA added its own starting at 77001.

| Numbers | Names (`!?FU(…)`) | Who generates them |
|---------|-------------------|--------------------|
| 30300–30371, 30400, 30600, 30800–30801, 30900–30904 | `OnBeforeBattle`, `OnAfterBattle`, `OnBattleRound` (`OnCombatRound`), `OnBeforeBattleAction`, `OnAfterBattleAction`, `OnWanderingMonsterReach/Death`, `OnMagic*Resistance`, mouse (`On…MouseClick`), `OnEquipArt/OnUnequipArt`, `On…MouseHint`, `OnMp3MusicChange`, `OnSoundPlay`, `On…AdventureMagic`, `OnEnter/LeaveTownHall`, network `On…DataSend/Received`, commander window, `On…BattleUniversal`, `OnAfterLoadGame` (GM0), `OnBeforeSaveGame` (GM1), `OnAfterErmInstructions` (PI), `OnCustomDialogEvent`, `OnHeroMove`, `OnHeroGainLevel`, `OnSetupBattlefield`, `OnMonsterPhysicalDamage`, `OnEvery…Second(s)/Minute` | WoG |
| 77001–77056 | `OnSavegameWrite/Read`, `OnKeyPressed/Released`, `OnOpen/CloseHeroScreen`, `OnBattleStackObtainsTurn`, `OnBattleRegeneratePhase`, `OnAfterSaveGame`, `OnBefore/AfterHeroInteraction`, `OnStackToStackDamage`, `OnAICalcStackAttackEffect`, `OnChat`, `OnGameEnter/Leave`, `OnEveryDay`, `On…BattlefieldVisible`, `OnAfterTacticsPhase`, recruitment window, clicks in the fort/kingdom screens, `OnLoadHeroScreen`, `On…BuildTownBuilding`, town screen, `OnPre/PostHeroScreen`, `OnDetermineMonInfoDlgUpgrade`, `OnAdventureMapTileHint`, `OnBeforeBattleStackTurn`, `OnCalculateTownIncome`, battle replay, local events, `OnWin/LoseGame`, `OnTransferHero`, `OnAfterHeroGainLevel`, `OnBattleActionEnd`, obstacles, `OnBattleStackRegeneration` | Era engine |
| 95000+ | any other names | called from ERM (`FU(…):P`) or by plugins by name |

The full "number → name" table is `EraEvents.Names` (in Era order: for synonyms, the number gets the name
registered last).

Event order already reproduced by the port's host:

| Moment | Events (in order) |
|--------|-------------------|
| New game | `!#` instructions of all scripts → `OnAfterErmInstructions` (PI) → `OnGameEnter` |
| Start of a player's day | `OnEveryDay` (for each color, f1000 = human) → `!?TM` timers |
| Saving | `OnBeforeSaveGame` (GM1) → `OnSavegameWrite` → write → `OnAfterSaveGame` |
| Loading | (`OnGameLeave`, if a game was in progress) → state restore → `OnSavegameRead` → `OnAfterLoadGame` (GM0) → `OnGameEnter` |

The remaining ERA events will appear together with the Olden Era hooks (battle, screens, keyboard); the list
of required hooks is in `OldenEra_ReverseEngineering/07_InGame_RE_Plan.md`. Era events with parameters pass
them in `x1…xN` (`FireErmEventEx`), and the result is read from the returned `x` (`RetXVars`): for example,
`OnBeforeHeroInteraction`: `x1`, `x2` are the heroes, `x3` = 0 forbids the meeting. The port has
`ErmRuntime.RaiseEra(id, ctx, args)` and `ErmRuntime.RetX` for this.

## 2. Script loading order

`TScriptMan.LoadScriptsFromDisk` on a new game (after `FuncNames.Clear`, `FuncAutoId := 95000`, registration
of event names and clearing of constants):

1. `Data\s\lib\*.erm`: libraries (script name `lib\…`);
2. map scripts (`_inmap_` from the map's global events, then `Maps\<map>\Data\s`);
3. `Data\s\*.erm`: global scripts (unless the fixed list "load only these scripts.txt" is enabled or the
   map has forbidden "wogification");
4. `Data\s\lib_end\*.erm`: final libraries.

Inside a folder, files are ordered by `GetOrderedPrioritizedFileList`: first by name (`AnsiCompareText`), then
by a stable insertion sort on numeric priority, which is the first word of the file name ("906 name.erm" → 906,
otherwise 0); higher priority comes first. A script whose name has already been loaded is skipped.

ERA sees mods through a virtual file system: a file present in several mods is taken from the mod with the
higher priority. Port: `EraScriptSet.Collect(mods in descending priority order)`.

Port deviation: Windows `AnsiCompareText` compares according to locale rules ("word sort": hyphen and
apostrophe carry almost no weight). The port compares case-insensitively, ignoring `-` and `'` in the first
step. For the file names of the ERA project the order matches; for exotic names discrepancies are possible.

Encoding: ERA reads bytes as they are. Modern ERA mods are in UTF-8 (comments, translation keys), old WoG
files are cp1251. The port decodes strictly valid UTF-8 as UTF-8 and everything else as cp1251
(`EraText.Decode`).

## 3. ERT strings (`z` greater than 1000)

A script may have a `<name>.ert` file next to it: a text table where the first line is a header, followed by
lines `number<TAB>text<TAB>…`; the line separator is CRLF (a lone LF can occur inside the text). The number
is the z-string index (usually greater than 1000). When a number is repeated, ERA shows "Duplicate ERM string
index" and keeps the first one. When `z>1000` is read, the string is interpolated (except for local strings,
see below).

ERA local strings are indexes starting at 1 000 000 000: string literals of parameters (live until the end of
the command line), `VR:Z`, results of `Erm_*` (live until the end of the trigger). They are not saved.

Port: `WoGHost.LoadErt`, `EraState.Ert`, local ones: `ErmRuntime.CreateCmdLocalErt/CreateTriggerLocalErt`.

## 4. Translations (`Trans.pas`)

`Lang\<language>\*.json`, then `Lang\*.json` of each mod; keys of nested objects are joined with a dot, list
elements by index; an already loaded key is not overwritten (which is why the mod with the higher priority
wins). Numbers and booleans become strings ("1"/"0"). `%T(key)` substitutes a translation without
parameters; `SN:T^key^/?string/name/value/…` substitutes `@name@` (`StrLib.BuildStr`: every odd fragment
between `@` is a parameter name, an unknown name stays as `@name@`). A missing key is returned as is. The
language is the `Language` option from `heroes3.ini` (default `en`).

Port: `EraLang` (`LoadMods`, `Tr`, `BuildStr`).

## 5. Saved games

ERA writes its own sections into the saved game: the version, the scripts (already processed by the
preprocessor), `FuncAutoId` and function names, global constants, ERT strings, hero w-variables,
`Era.DynArrays_SN_M` (all `SN:M` arrays; elements only for arrays with storage type 1), `Era.AssocArray_SN_W`
(named variables; empty ones, 0 and "", are discarded on load), `SN:H` hints.

The port stores the same data in `EraState` inside the state file `*.wog.json` next to the Olden Era saved
game (format and ownership check: `WoG_ReverseEngineering/08_Save_Load.md`). After reading,
`EraState.NormalizeAfterLoad` is called (the rules of the section above). On load, scripts are re-read from
disk and processed by the preprocessor with the **saved** names: known names keep their numbers.

Difference from ERA: on load, ERA takes the script texts from the saved game if they differ from the current
ones on disk. The port always takes scripts from disk (Olden Era saved games do not contain ERA scripts).
