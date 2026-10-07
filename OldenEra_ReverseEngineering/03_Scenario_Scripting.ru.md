[English](03_Scenario_Scripting.md) | **Русский**

# Olden Era — скриптинг сценариев (скрипты карт)

Источник: O1 `src/schema/conditions.ts` / `actions.ts` (реестр переписан с официальных страниц Notion Unfrozen
«Conditions and their parameters — Full list» и «Actions and their parameters — Full list») и руководства O1.
Метка: **[V-community]** (реестр) / **[V-data]** (форма файла). Имена типов и описания в таблицах оставлены
на английском, как в официальном реестре, — это идентификаторы, которые читает игра.

## 1. Форма файла

```json
{ "counters": [{"sid": "...", "value": 0}],
  "interruptions": [{"sid":"...","interruption":"BeforeIamVsHero","activeOnStart":true,"p":["E_Hero_X"],"actions":[...]}],
  "quests": [{"sid":"...","activeOnStart":true,"subQuests":[{"sid":"...","activeOnStart":true,
      "triggers":[{"conditionsLogic":"And","conditions":[{"c":"StartTurn","p":["..."]}],
                   "actions":[{"a":"GiveRes","p":["gold","500"]}], "repeat": false}]}]}]}
```

## 2. Семантика (по руководствам O1, проверено сообществом)

* Условия — **одноразовые слушатели**: `And` срабатывает, когда каждое условие сработало хотя бы раз (не
  обязательно одновременно). `TriggerClear`/`TriggerClearCustom` взводят их заново. `repeat: true` — повтор.
* Триггеры подквеста выполняются в порядке объявления; действия — по порядку и **мгновенно** (ждут только
  диалоги). Одно ошибочное действие молча отключает **все** действия своего триггера.
* У действия может быть `"break": true` (прервать остальные действия) — для ветвления после `DialogIf*`.
* **Счётчики** — единственные переменные: именованные целые, локальные для карты; **сюжетные счётчики**
  переживают переход между миссиями кампании. Арифметики кроме `+`, `-`, `=`, случайного значения нет; нет
  строк и массивов.
* **Прерывания** — единственный хук, который может *приостановить/заменить* родную логику, и только вокруг
  боёв героя с героем: `BeforeIamVsHero`, `AfterIamWinVsHero`, `BeforeHeroVsHero`, `AfterHeroWinVsHero`.
  Срабатывают каждый раз, пока не отключены.
* Триггеры взаимодействия с объектами («Actions Before/After» у объектов в редакторе) существуют и
  соответствуют условиям `ObjectInteractionBefore/After`.

## 3. Оценка применительно к ERM

Система сценариев — **декларативная таблица «событие → действие»**. Разместить в ней рантайм ERM нельзя:
нет циклов, функций, общих переменных, выражений, хуков боя ниже уровня «герой против героя», доступа к
отдельным стекам/существам, интерфейса кроме диалогов. Её **можно** использовать как запасной вариант только
на данных для небольшого подмножества ERM и как источник проверенных имён действий, когда игрой управляет
плагин (те же действия существуют как код движка — их реализации можно вызывать через найденный диспетчер).

## 4. Условия (55)

| Тип | Категория | Название | Параметры | Описание (оригинал, англ.) |
|---|---|---|---|---|
| `Counter` |  | Counter Check | Counter SID; Operator; Value | Triggers when the value of a local counter with the specified SID satisfies the inequality for the first time. |
| `CompareCounters` |  | Compare Two Counters | Counter SID 1; Operator; Counter SID 2 | Triggers when, after changing either of the two specified counters, their values satisfy the inequality for the first time. |
| `CounterEqualityInDays` |  | Counter Unchanged for N Days | Counter SID; Days | Triggers if the value of the counter does not change for X days in a row. Used to call actions N days after an event. |
| `StoryCounter` |  | Campaign Counter Check | Story Counter SID; Operator; Value | Triggers when the value of a campaign (story) counter satisfies the inequality for the first time. Campaign counters persist across missions. |
| `QuestCompleted` |  | Quest Completed | Quest SID | Triggers when the quest with the specified SID is turned off (does not distinguish completed vs. failed). |
| `Difficulty` |  | Campaign Difficulty Check | Difficulty index | Triggers at the start of a new day if the current campaign mission difficulty matches the specified index. 0=Easy, 1=Normal, 2=Difficult, 3=Impossible, 4=Lethal. |
| `DifficultyCustomMap` |  | Custom Map Difficulty Check | Difficulty type; Difficulty index | Triggers at the start of a new day if the custom map difficulty matches. typeDifficulty: Economy/Neutral/Ai. Index: 0=Easy, 1=Normal, 2=Difficult, 3=Impossible, 4=Lethal. |
| `StartTurn` |  | Start of Turn (Week/Day) | Week number; Day number | Triggers at the start of a specific turn. Params: weekNumber, dayNumber (both optional). Only works for weeks 1–4, month 1. Omit params for any turn. Use "counter: X" to fire after X days instead. |
| `AnyStartTurn` |  | Any Turn Start (Month/Week/Day) | Month; Week; Day | Triggers at the start of a specific month/week/day. Use -1 for "any". [-1,-1,-1] = nearest turn start. |
| `StartWeek` |  | Start of Week |  | Triggers when a new week begins. Use "counter: X" to require X week starts. Omit counter for nearest week start. |
| `NodeRevealed` |  | Node Revealed | Node index | Triggers when the node with the specified index has been revealed from fog of war. |
| `PlayerDefeated` |  | Player Defeated | Player index | DEPRECATED — use CheckLoseIfHeroKilled + CheckLoseIfCityLost instead. Triggers when the player with the specified index is completely removed from the map. |
| `CheckLoseIfHeroKilled` |  | Check Lose: Hero Killed | Player index | Auxiliary condition. Triggers if the player loses a hero and has no castles/heroes remaining. Use only in an "Or" combination with CheckLoseIfCityLost. |
| `CheckLoseIfCityLost` |  | Check Lose: City Lost | Player index | Auxiliary condition. Triggers if the player loses a castle and has no castles/heroes remaining. Use only in an "Or" combination with CheckLoseIfHeroKilled. |
| `ResCounter` |  | Resource Check | Resource; Operator; Value |  |
| `BuildingConstruct` |  | Building Constructed | Building SID; Level; Castle entity | Triggers when the player constructs a building with the specified SID at the specified level in the given castle entity. Leave entityCity blank for any castle. |
| `BuildingOwn` |  | Building Owned | Building SID; Level; Castle entity | Triggers if the player controls a castle with the specified entity that has the building already constructed. Leave entityCity blank for any castle. |
| `SpellCast` |  | Spell Cast | Spell SID | Triggers when the player casts the specified spell X times (set counter). Applies to both combat and adventure map spells. |
| `ItemOwnSide` |  | Item Owned (Side) | Item SID; Operator; Value |  |
| `ItemDestroyed` |  | Item Destroyed | Item SID | Triggers when the player destroys (dismantles) an artifact with the specified SID. |
| `UnitOwnSide` |  | Unit Owned (Side) | Unit SID; Operator; Value |  |
| `UnitHire` |  | Unit Hired | Unit SID | Triggers when the player recruits X units with the specified SID (direct purchase only). Set counter to require N hires. |
| `UnitLose` |  | Unit Lost | Unit SID | Triggers when the player loses X units with the specified SID (death in combat or dismissed). Set counter to require N losses. |
| `UnitKill` |  | Unit Killed | Unit SID | Triggers when the player kills X units with the specified SID (enemy units only). Set counter to require N kills. |
| `HeroKill` |  | Hero Killed / Removed | Hero SID | Triggers when the hero with the specified SID is removed in any way (DeleteHero, defeat, retreat, surrender). Does NOT trigger for temporary removal. |
| `ItemOwnHero` |  | Item Owned by Hero | Item SID; Hero SID | Triggers when an artifact with the specified SID appears in the inventory of the specified hero for the first time. Leave item SID blank for any artifact. |
| `UnitOwnHero` |  | Unit Owned (Hero) | Hero SID; Unit SID; Operator; Value | Triggers when the number of units with the specified SID belonging to the specified hero satisfies the inequality for the first time. |
| `HeroStat` |  | Hero Stat Check | Hero SID; Stat; Operator; Value | Triggers when the numerical value of the specified stat of the hero satisfies the inequality for the first time. Leave hero SID blank for the currently selected hero. |
| `ObjectInteractionBefore` |  | Object Interaction (Before) | Object entity; Hero SID | Triggers BEFORE the hero interacts with the specified interactive object (akin to Actions Before). Leave hero SID blank for any human player's hero. |
| `ObjectInteractionAfter` |  | Object Interaction (After) | Object entity; Hero SID | Triggers AFTER the hero interacts with the specified interactive object (akin to Actions After). Leave hero SID blank for any human player's hero. |
| `ObjectCaptureEntity` |  | Object Captured (Entity) | Object entity | Triggers when the player captures an object with the specified entity for the first time. Works on mines, barracks, castles and certain special objects. |
| `ObjectCaptureSid` |  | Object Captured (SID) | Object SID | Triggers when the player captures an object with the specified SID X times (set counter). Also counts repeated captures of the same object. |
| `MultipleObjectOwn` |  | Multiple Objects Owned | Object SID; Operator; Value | Triggers when the number of objects with the specified SID captured by the player satisfies the inequality for the first time. |
| `ObjectLose` |  | Object Lost | Object entity | Triggers when an object with the specified entity belonging to the player is captured by another player for the first time. |
| `SquadInteraction` |  | Squad Interaction (Before Battle) | Squad entity; Hero SID | Triggers BEFORE the specified hero interacts with the neutral squad (akin to Actions Before). Leave hero SID blank for any human player's hero. NOTE: Do NOT use for dialogues — cannot interrupt battle start. |
| `SquadKill` |  | Squad Killed (After Battle) | Squad entity; Hero SID | Triggers AFTER the specified neutral squad is killed by any player (akin to Actions After). Leave hero SID blank for any hero. |
| `TutorialMovePoints` |  | Tutorial: Move Points | Value; Operator; Hero SID | Triggers when the specified hero has a number of movement points satisfying the inequality after stopping. Leave hero SID blank for any hero. |
| `TutorialOpenCity` |  | Tutorial: Open City |  | Triggers when the player opens the city screen for the first time. |
| `TutorialShowTooltipSquad` |  | Tutorial: Show Squad Tooltip |  | Triggers when the player opens the tooltip of a neutral squad for the first time. |
| `TutorialShowTooltipWO` |  | Tutorial: Show Object Tooltip |  | Triggers when the player opens the tooltip of any interactive object (except a city) for the first time. |
| `TutorialResChange` |  | Tutorial: Resource Changed | Resource SID | Triggers when the player's amount of the specified resource changes for the first time. |
| `TutorialLevelUp` |  | Tutorial: Level Up |  | Triggers when the player levels up a hero for the first time. |
| `TutorialHeroUI` |  | Tutorial: Open Hero UI |  | Triggers when the player opens the hero interface for the first time. |
| `TutorialMagicGuild` |  | Tutorial: Open Magic Observatory |  | Triggers when the player opens the magic observatory window for the first time. |
| `TutorialOpenFractionLaws` |  | Tutorial: Open Faction Laws |  | Triggers when the player opens the faction laws window for the first time. |
| `TutorialLevelUppedFractionLaws` |  | Tutorial: Faction Law Unlocked |  | Triggers when the player accumulates enough experience to learn a faction law for the first time. |
| `TutorialOpenMagicBookMap` |  | Tutorial: Open Spellbook (Map) |  | Triggers when the player opens the spellbook on the map for the first time. |
| `TutorialHeroInteractWithAllyHero` |  | Tutorial: Interact with Ally Hero |  | Triggers when the player interacts with an allied hero for the first time. |
| `TutorialStartBattleForMap` |  | Tutorial: Start Battle (Map) |  | Triggers when the player enters combat for the first time (tactical phase starts). |
| `TutorialStartBattleForCity` |  | Tutorial: Start Siege Battle |  | Triggers when the player enters a siege battle for the first time (tactical phase starts). |
| `TutorialStartBattleForWorldObject` |  | Tutorial: Start Bank Battle |  | Triggers when the player enters combat with a bank's inner guard for the first time (tactical phase starts). |
| `TutorialStartTurnUnit` |  | Tutorial: Unit Turn in Battle |  | Triggers when the turn passes to a player's stack in combat for the first time. |
| `TutorialOpenMagicBookBattle` |  | Tutorial: Open Spellbook (Battle) |  | Triggers when the player opens the spellbook in combat for the first time. |
| `TutorialBattleEnergy` |  | Tutorial: Battle Energy |  | Triggers when the player accumulates 1 energy cell for the first time. |
| `TutorialUnitUI` |  | Tutorial: Open Unit View |  | Triggers when the player opens the detailed unit view window for the first time. |

## 5. Действия (114)

| Тип | Категория | Название | Параметры | Описание (оригинал, англ.) |
|---|---|---|---|---|
| `NextQuest` | Quest Management | Next Quest | Quest SID | Disables the current quest and activates the quest with the specified SID. Leave SID blank to simply disable the current quest. Only one new quest can be enabled at a time. |
| `EndQuest` | Quest Management | End Quest(s) | Quest SID 1; Quest SID 2; Quest SID 3 | Disables ALL quests with the specified SIDs. |
| `NextSubQuest` | Quest Management | Next Subquest(s) | Subquest SID 1; Subquest SID 2; Subquest SID 3 | Disables ALL subquests of the current quest and activates ALL subquests with the specified SIDs. |
| `SubQuestActivate` | Quest Management | Activate Subquest | Quest SID; Subquest SID | Activates a specific subquest on the specified quest. |
| `SubQuestDeactivate` | Quest Management | Deactivate Subquest | Quest SID; Subquest SID | Deactivates a specific subquest on the specified quest. |
| `CurrentSubQuestDone` | Quest Management | Grey Out Current Subquest |  | "Greys out" the current subquest in the quest log (visually finished). The subquest remains active — this is purely visual. |
| `SubQuestDone` | Quest Management | Grey Out Subquest | Quest SID; Subquest SID | "Greys out" the specified subquest in the quest log (visually finished). The subquest remains active — this is purely visual. |
| `NextAfterGroup` | Quest Management | Next Subquest After Group | Group SID; Next subquest SID | When ALL subquests in the specified group are completed, enables the specified subquest. Action with a precondition. |
| `NextQuestAfterGroup` | Quest Management | Next Quest After Group | Group SID; Next quest SID | When ALL subquests in the specified group are completed, enables the specified quest. Action with a precondition. |
| `NextSubGroupAfterGroup` | Quest Management | Next Subquest Group After Group | Completed group SID; Next group SID | When ALL subquests in group 1 are completed, enables ALL subquests in group 2. Action with a precondition. |
| `TriggerClear` | Quest Management | Clear All Trigger Conditions | Quest SID; Subquest SID; Trigger index | Re-enables ALL conditions of the specified trigger (by 0-based index), making them "forget" they were triggered previously. |
| `TriggerClearCustom` | Quest Management | Clear Single Trigger Condition | Quest SID; Subquest SID; Trigger index; Condition index | Re-enables a specific condition (by 0-based index) on a trigger, making it "forget" it was triggered. Used primarily to re-enable individual conditions in "And" combinations. |
| `AutoSave` | Technical | Auto Save |  | Forces the game to create an autosave. NOTE: Actions immediately after AutoSave will not be saved. Overwrites last day's autosave. |
| `Print` | Technical | Print to Log | Message; Message 2; Message 3 | Outputs text to the developer console/log. Debugging only — no in-game effect. |
| `EnableInterruption` | Technical | Enable Interruption | Interruption SID | Forces the specified disabled interruption to start triggering again. |
| `DisableInterruption` | Technical | Disable Interruption | Interruption SID | Forces the specified interruption to stop triggering (it still exists but won't fire). |
| `BreakInterruptions` | Technical | Break Interruptions |  | Stops the current interruption, allowing normal game flow to resume. Used exclusively in dialog mapActions to prevent battle when a hero should step back instead. Must be paired with StepBack. |
| `CounterPlus` | Counter | Counter + | Counter SID; Amount | Increases the value of the local counter by the specified integer X. |
| `CounterMinus` | Counter | Counter − | Counter SID; Amount | Decreases the value of the local counter by the specified integer X. |
| `CounterSet` | Counter | Counter = (Set) | Counter SID; Value | Overwrites the value of the local counter with the new specified integer X. |
| `CounterSetRandom` | Counter | Counter = Random | Counter SID; Min; Max | Overwrites the value of the local counter with a random integer between XMin and XMax. |
| `StoryCounterPlus` | Counter | Story Counter + | Story Counter SID; Amount | Increases the value of the campaign (story) counter by X. Campaign-only. When inside a dialog, write to "actions" block, not "mapActions". |
| `StoryCounterMinus` | Counter | Story Counter − | Story Counter SID; Amount | Decreases the value of the campaign (story) counter by X. Campaign-only. When inside a dialog, write to "actions" block, not "mapActions". |
| `StoryCounterSet` | Counter | Story Counter = (Set) | Story Counter SID; Value | Overwrites the value of the campaign (story) counter with X. Campaign-only. When inside a dialog, write to "actions" block, not "mapActions". |
| `Dialog` | Dialogs | Show Dialog | Dialog SID; "break" flag | Calls the dialog with the specified SID. |
| `DialogIfHero` | Dialogs | Show Dialog If Hero | Dialog SID; Hero SID; "break" flag | If the currently selected hero has the specified SID, calls the dialog. Precondition action. |
| `DialogIfRes` | Dialogs | Show Dialog If Resource | Dialog SID; Resource; Operator; Value; "break" flag | If the player's resource satisfies the inequality, calls the dialog. Precondition action. |
| `DialogIfCounter` | Dialogs | Show Dialog If Counter | Dialog SID; Counter SID; Operator; Value; "break" flag | If the local counter satisfies the inequality, calls the dialog. Precondition action. Works only with local counters. |
| `DialogIfItem` | Dialogs | Show Dialog If Item | Dialog SID; Item SID; "break" flag | If the currently selected hero has the item in their inventory/backpack, calls the dialog. Precondition action. |
| `RandomDialog` | Dialogs | Random Dialog | Dialog SID 1; Dialog SID 2; Dialog SID 3; Dialog SID 4; Dialog SID 5 | Calls one random dialog from the specified list. NOTE: "break" cannot be added to RandomDialog. |
| `DialogOne` | Dialogs | Show Dialog (Once) | Dialog SID; "break" flag | A Dialog action that triggers only once for the rest of the match. |
| `DialogOneIfHero` | Dialogs | Show Dialog If Hero (Once) | Dialog SID; Hero SID; "break" flag | A DialogIfHero action that triggers only once for the rest of the match. |
| `DialogOneIfRes` | Dialogs | Show Dialog If Resource (Once) | Dialog SID; Resource; Operator; Value; "break" flag | A DialogIfRes action that triggers only once for the rest of the match. |
| `DialogOneIfCounter` | Dialogs | Show Dialog If Counter (Once) | Dialog SID; Counter SID; Operator; Value; "break" flag | A DialogIfCounter action that triggers only once for the rest of the match. |
| `ShowFloatingUI` | UI | Show Floating UI | Icon SID; Text string | Displays a floating/fading icon with text above the currently selected hero. Used for faction reputation changes. Possible icons: dungeon_icon, human_icon, undead_icon, spring_icon, unfrozen_icon, hive_icon. |
| `OpenUI` | UI | Open UI | UI SID | Forcibly opens the specified interface. Possible values: LevelUp, HeroUI, HeroWithHero, MagicBook, MagicBookBattle. |
| `Guide` | UI | Open Tutorial Guide | Guide SID | Opens a tutorial window (block of slides) with the specified SID. When inside a dialog, write to "actions" block, not "mapActions". |
| `GameVictory` | Game Rules | Player Victory |  | Forces the interacting player to win. In quest script, acts on the player under the specified "sharing" type. |
| `GameLose` | Game Rules | Player Defeat |  | Forces the interacting player to lose. In quest script, acts on the player under the specified "sharing" type. |
| `SideLose` | Game Rules | Force Player Defeat | Player index | Forces the player with the specified index to lose (defeat message shown, all heroes removed, all objects go neutral). |
| `ChangeCampaignOneStep` | Game Rules | Toggle Single-Turn Mode | Enable? | Enables or disables single-turn mode. When enabled: heroes get unlimited movement, skipping turns is blocked. |
| `AddGlobalBuff` | Game Rules | Add Global Buff | Buff SID; Duration type; X (count) | Applies a global effect with the specified SID. durationType: Infinite, UntilNextWeek, UntilNextDay, UntilNextBattle, ForSeveralDays, ForSeveralBattles. X is optional (used for ForSeveralDays/Battles). |
| `RemoveGlobalBuff` | Game Rules | Remove Global Buff | Buff SID | Disables the global effect with the specified SID. To re-enable it, use AddGlobalBuff again. |
| `MoveCamera` | Camera | Move Camera | Node index | Moves the camera center of the given player to the node/cell with the specified index. |
| `MoveCameraToSelectHero` | Camera | Move Camera to Selected Hero |  | Moves the camera center to the cell where the currently selected hero is located. |
| `RevealFogOfWar` | Camera | Reveal Fog of War | Node index; Radius | Reveals fog of war within a radius of X around the node. X=1 reveals the cell and all adjacent cells. X=0 reveals nothing. |
| `CreateFogOfWar` | Camera | Create Fog of War | Node index; Radius | Creates fog of war within a radius of X around the node. Cannot create fog in the visibility range of a hero/castle/object with an owner. |
| `GiveRes` | Economy | Give Resources | Resource; Amount | Gives X units of the specified resource to the interacting player. |
| `RemoveRes` | Economy | Remove Resources | Resource; Amount |  |
| `UnlockSpell` | Economy | Unlock Spell | Spell SID | Unlocks the spell with the specified SID in the player's magic observatory. |
| `UnlockBuildingCity` | Economy | Unlock Building for Construction | Building SID; Level; Castle entity | Unlocks a previously restricted building for construction in the specified castle. Level: mage_guild 1–5, main building/walls 1–3, dwellings 1–2, others 1. |
| `CreateBuildingCity` | Economy | Build Building in Castle | Building SID; Level; Castle entity | Creates (builds) the specified building at the specified level in the castle. Level: mage_guild 1–5, main building/walls 1–3, dwellings 1–2, others 1. |
| `CaptureObject` | Economy | Capture Object | Object entity | Transfers the object with the specified entity under the control of the given player. |
| `LoseObject` | Economy | Make Object Neutral | Object entity | Makes the object with the specified entity neutral (removes player ownership). |
| `SpawnObject` | Map Objects | Spawn Interactive Object | Object SID; Node index; Mirror?; Entity SID | Creates an interactive object at the specified node. boolMirror: true=mirror horizontally. entityObject is optional. |
| `SpawnMapObject` | Map Objects | Spawn Decoration Object | Object SID; Node index; Rotation; Entity SID | Creates a non-interactive decoration object at the specified node. Rotation: 0=0°, 1=90°, 2=180°, 3=270°. entityObject is optional. |
| `CreateVFX` | Map Objects | Create VFX | VFX SID; Node index; Random rotation?; Active?; Entity SID | Creates a visual effect at the specified node. boolRotation: random rotation. isActive: show/hide. entityVFX is optional. |
| `EventBankRefresh` | Map Objects | Recharge Bank | Object entity | Recharges a building that was previously marked with the "Already visited" marker. |
| `SetActiveVFX` | Map Objects | Show/Hide VFX | VFX entity SID; Active? | Enables or disables the visual effect with the specified entity. A disabled effect is fully hidden but still exists on the map. |
| `SetQuestMarker` | Map Objects | Set Quest Marker | Entity; Marker path | Sets a quest marker on the object/squad with the specified entity. Gold markers = main quests. Silver = side quests. Types: _01=?, _02=!, _03=^. |
| `SetActiveQuestMarker` | Map Objects | Show/Hide Quest Marker | Entity; Active? | Shows or hides a quest marker on the specified entity. The marker must have been set beforehand with SetQuestMarker. |
| `SetActivePortal` | Map Objects | Set Portal Active | Portal entity; Active? | Enables or disables a portal. A disabled portal displays as an "exit portal" in-game. |
| `SetActiveMarker` | Map Objects | Enable/Disable Trigger Zone | Zone entity; Active? | Enables or disables the trigger zone with the specified entity. Non-disabled zones interrupt hero movement every time — even if their action had an unmet precondition. |
| `DeleteEntity` | Map Objects | Delete Entity | Entity | Removes any entity (interactive object, decoration, VFX, castle, neutral squad) from the map, EXCEPT heroes. Non-interactive objects require "No combine geometry" property. |
| `DeleteMarkerByNode` | Map Objects | Delete Trigger Zone by Node | Node index | Deletes ONE trigger zone whose cells lie in the specified node. Used for zones without their own entity property (e.g. zones ending with "resultDialog": "Interrupt"). |
| `EntityActionsOff` | Map Objects | Disable Entity Triggers | Entity | Disables all triggers on the object with the specified entity. Permanently removes all Actions Before and Actions After. |
| `SpawnSquad` | Squads | Spawn Neutral Squad | Squad SID; Node index; Value (strength); Scale by difficulty?; Entity SID | Adds a new neutral squad at the specified node with the given total strength value. boolDifficulty: multiply value by difficulty coefficient. |
| `SpawnSquadNPC` | Squads | Spawn NPC Squad | Squad SID; Node index; Value (strength); Scale by difficulty?; Entity SID | Like SpawnSquad but combat cannot be initiated upon interaction. Used for squads that call a dialog via SquadInteraction condition. |
| `SetFlagEscape` | Squads | Set Squad Escape Flag | Squad entity; Enable? | Enables or disables the ability for the squad to flee instead of fighting. |
| `SetFlagAutobattle` | Squads | Set Squad Auto-Battle Flag | Squad entity; Enable? | Enables or disables the ability for the squad to resolve combat via auto-calculation. |
| `ChangeCampaignDiplomacy` | Squads | Set Guaranteed Free Join | Squad entity; Enable? | Enables or disables guaranteed free joining for the squad. |
| `ChangeAlwaysDiplomacy` | Squads | Set Diplomacy Join Offer | Squad entity; Enable? | Enables or disables the ability for the squad to offer to join via the "Diplomacy" mechanic. |
| `ChangeSquadReactionType` | Squads | Change Squad Mood | Squad entity; Affinity | Changes the mood of the squad, affecting the chance it will offer to join. Aggressive=0%, Negative=½x, Common=1x, Friendly=2.5x, Peaceful=5x, Docile=0%. |
| `IncreaseStrengthSquad` | Squads | Increase Squad Strength | Squad entity; Percent | Increases the total strength/size of the squad by the specified percentage. Write as decimal fraction (e.g. 0.65 = 65%). |
| `ReduceStrengthSquad` | Squads | Reduce Squad Strength | Squad entity; Percent | Decreases the total strength/size of the squad by the specified percentage. Minimum size is 1. Write as decimal fraction (e.g. 0.2 = 20%). |
| `DeleteSquad` | Squads | Delete Squad | Squad entity; Animation | Removes the neutral squad from the map with an animation. indexAnimation: 0=death, 1=flee (dust cloud), 2=join (shining flash). |
| `SpawnHero` | Heroes | Spawn Hero | Hero SID; Node index; Player index | Creates a hero with the specified SID belonging to the specified player at the given node. Player index starts at 0. |
| `DeleteHero` | Heroes | Delete Hero | Hero SID | "Kills" the hero with the specified SID. Triggers the HeroKill condition. |
| `GiveUnitHero` | Heroes | Give Units to Hero | Unit SID; Count; Hero SID | Adds a stack of X units to the army of the specified hero. If army is full, the "accept creatures" UI opens. |
| `GiveUnitHeroPerWeek` | Heroes | Give Units × Week Number | Unit SID; Count; Hero SID | Adds [X × current week number] units to the hero's army. Week numbering does NOT reset with a new month. |
| `GiveUnitHeroPerMonth` | Heroes | Give Units × Month Number | Unit SID; Count; Hero SID | Adds [X × current month number] units to the hero's army. |
| `RemoveUnitHero` | Heroes | Remove Units from Hero | Unit SID; Count; Hero SID | Removes X units of the specified type from the hero's army. Use "all" instead of a number to remove all units of that type. |
| `GiveExpHero` | Heroes | Give Experience to Hero | Amount; Hero SID | Gives X experience points to the specified hero. |
| `GiveStatsHero` | Heroes | Give Stats to Hero | Stat; Hero SID; Amount | Adds X points of the specified stat to the hero. X can be negative (reduces stats down to 0). Leave hero SID blank for the currently selected hero. |
| `GiveManaHero` | Heroes | Add Mana to Hero | Hero SID; Amount | Adds X mana to the current mana pool of the hero. X can be negative (reduces mana down to 0). Leave hero SID blank for the currently selected hero. |
| `ChangeManaHero` | Heroes | Set Hero Mana | Hero SID; Mana amount |  |
| `AddSpellHero` | Heroes | Teach Spell to Hero | Spell SID; Hero SID | Adds the spell to the spellbook of the hero. |
| `AddSkillHero` | Heroes | Add Skill to Hero | Skill SID; Hero SID | Adds the skill to the skill list of the hero (if there is a free slot). |
| `AddSkillAll` | Heroes | Add Skill to All Heroes | Skill SID | Adds the skill to the skill list of ALL heroes of the given player (if each has a free slot). |
| `GiveItemHero` | Heroes | Give Item to Hero | Item SID; Hero SID | Gives an artifact to the hero. Leave hero SID blank to give to the currently selected hero. |
| `RemoveItem` | Heroes | Remove Item from Player | Item SID | Sequentially checks the inventory of every hero belonging to the player and removes the first encountered item with the specified SID. |
| `AddBuffHeroDays` | Heroes | Apply Buff to Hero | Buff SID; Hero SID; Days | Applies a buff to the hero for X turns. Leave X empty for permanent (until mission end). Leave hero SID blank for the currently selected hero. |
| `RemoveBuffHero` | Heroes | Remove Buff from Hero | Buff SID; Hero SID | Removes the specified buff from the hero permanently. Leave hero SID blank for the currently selected hero. |
| `SetHeroMovePoints` | Heroes | Set Hero Move Points | Hero SID; Move points |  |
| `HeroResetMovePointsMax` | Heroes | Zero Max Move Points | Hero SID |  |
| `HeroStop` | Heroes | Stop Hero |  |  |
| `StepBack` | Heroes | Step Back |  | Forces the currently selected hero to step back to the cell from which the last step was taken. Used to prevent combat with NPC squads/heroes. Must be used with BreakInterruptions. |
| `HeroToNode` | Heroes | Move Hero to Node | Hero SID; Node index | Forces the specified hero to move to the node and/or interact with any object/squad in it. Works for both human and AI heroes. If path cannot be built, the hero does not move. |
| `HeroToHero` | Heroes | Move Hero Toward Hero | Moving hero SID; Target hero SID | Forces hero1 to move toward hero2 and start combat or open "Exchange" UI. Works for both human and AI heroes. |
| `TeleportHero` | Heroes | Teleport Hero | Hero SID; Node index 1; Node index 2; Node index 3; Node index 4; Node index 5 | Teleports the hero to one random cell from the list of node indices. If only one node is given, the hero is guaranteed to teleport there. |
| `InitiateInteract` | Heroes | Initiate Interaction | Entity | Initiates a remote interaction between the currently selected hero and the squad or object with the specified entity. Distance does not matter. |
| `InitiateAttack` | Heroes | Initiate Attack | Squad entity | Remotely initiates combat between the currently selected hero and the squad with the specified entity. Distance does not matter. |
| `ForceLastInteractObject` | Heroes | Force Last Object Interaction |  | Initiates a remote interaction between the currently selected hero and the last object the player interacted with (calls its "internal logic"). |
| `ForceLastInteractSquad` | Heroes | Force Last Squad Interaction |  | Initiates a remote interaction between the currently selected hero and the last squad the player interacted with. |
| `HeroInteractWorldObject` | Heroes | Hero Interact with Nearest Object | Hero SID | Forces the specified hero to find the nearest available object on the map and interact with it. Auxiliary technical action. |
| `ResurrectHero` | Heroes | Resurrect Hero | Hero SID; Node index; Flag | Resurrects a defeated hero at a map node. Undocumented in the official guide — verified real via example-map usage, but the Flag parameter's effect is unconfirmed (always "0" in the only usage found). |
| `AiBanArea` | AI | AI Ban Area | Node index | Blocks the AI from entering the zone containing the specified node. |
| `AiUnbanArea` | AI | AI Unban Area | Node index | Unblocks the AI for the zone containing the specified node. |
| `AiClearBanArea` | AI | AI Clear All Bans |  | Unblocks all zones for the AI. |
| `AiOnSelectHero` | AI | AI Recalculate Objectives |  | Triggers a recalculation of the AI's current objectives. Simulates the start of a new day in AI logic. |
| `DisableAIHero` | AI | Disable AI Hero | Hero SID | Completely removes the specified hero from AI logic. Must be called BEFORE giving movement commands to an AI hero. |
| `EnableAIHero` | AI | Enable AI Hero | Hero SID | Re-enables AI for the specified hero (previously disabled by DisableAIHero). |
| `EnableAiResurrect` | AI | Enable AI Auto-Resurrection | Hero SID | Enables campaign-unique auto-resurrection for the specified hero. Campaign AI only. |
| `DisableAiResurrect` | AI | Disable AI Auto-Resurrection | Hero SID | Disables campaign-unique auto-resurrection for the specified hero. Campaign AI only. |
