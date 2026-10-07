[English](03_ERA_Events_Loading_Save.md) | **Русский**

# События ERA, загрузка модов и скриптов, переводы, ERT, сохранение

Источники: `Erm.pas` (`RegisterErmEventNames`, `TScriptMan`, `GetOrderedPrioritizedFileList`,
`Hook_FindErm_*`, `Hook_RunTimer`), `Triggers.pas`, `Stores.pas`, `Trans.pas`, `AdvErm.pas` (`Save*/Load*`).
Порт: `src/WoG.Erm/Era/EraEvents.cs`, `EraScriptSet.cs`, `src/WoG.Host/WoGHost.cs`,
`src/WoG.Erm/Runtime/EraValues.cs` (`EraLang`), `src/WoG.Core/State/EraState.cs`.

## 1. События

Все события ERA — это триггеры `!?FU<номер>`: в ERM 2.0 их пишут по имени, `!?FU(OnEveryDay)`, препроцессор
подставляет номер. Номера WoG сохранены, ERA добавила свои с 77001.

| Номера | Имена (`!?FU(…)`) | Кто генерирует |
|--------|-------------------|----------------|
| 30300–30371, 30400, 30600, 30800–30801, 30900–30904 | `OnBeforeBattle`, `OnAfterBattle`, `OnBattleRound` (`OnCombatRound`), `OnBeforeBattleAction`, `OnAfterBattleAction`, `OnWanderingMonsterReach/Death`, `OnMagic*Resistance`, мышь (`On…MouseClick`), `OnEquipArt/OnUnequipArt`, `On…MouseHint`, `OnMp3MusicChange`, `OnSoundPlay`, `On…AdventureMagic`, `OnEnter/LeaveTownHall`, сетевые `On…DataSend/Received`, окно командира, `On…BattleUniversal`, `OnAfterLoadGame` (GM0), `OnBeforeSaveGame` (GM1), `OnAfterErmInstructions` (PI), `OnCustomDialogEvent`, `OnHeroMove`, `OnHeroGainLevel`, `OnSetupBattlefield`, `OnMonsterPhysicalDamage`, `OnEvery…Second(s)/Minute` | WoG |
| 77001–77056 | `OnSavegameWrite/Read`, `OnKeyPressed/Released`, `OnOpen/CloseHeroScreen`, `OnBattleStackObtainsTurn`, `OnBattleRegeneratePhase`, `OnAfterSaveGame`, `OnBefore/AfterHeroInteraction`, `OnStackToStackDamage`, `OnAICalcStackAttackEffect`, `OnChat`, `OnGameEnter/Leave`, `OnEveryDay`, `On…BattlefieldVisible`, `OnAfterTacticsPhase`, окно найма, клики в форте/королевстве, `OnLoadHeroScreen`, `On…BuildTownBuilding`, экран города, `OnPre/PostHeroScreen`, `OnDetermineMonInfoDlgUpgrade`, `OnAdventureMapTileHint`, `OnBeforeBattleStackTurn`, `OnCalculateTownIncome`, повтор боя, локальные события, `OnWin/LoseGame`, `OnTransferHero`, `OnAfterHeroGainLevel`, `OnBattleActionEnd`, препятствия, `OnBattleStackRegeneration` | движок Era |
| 95000+ | любые другие имена | вызываются из ERM (`FU(…):P`) или плагинами по имени |

Полная таблица «номер → имя» — `EraEvents.Names` (в порядке Era: при синонимах номер получает имя, записанное
последним).

Порядок событий, уже воспроизведённый хостом порта:

| Момент | События (по порядку) |
|--------|----------------------|
| Новая игра | инструкции `!#` всех скриптов → `OnAfterErmInstructions` (PI) → `OnGameEnter` |
| Начало дня игрока | `OnEveryDay` (для каждого цвета, f1000 = человек) → таймеры `!?TM` |
| Сохранение | `OnBeforeSaveGame` (GM1) → `OnSavegameWrite` → запись → `OnAfterSaveGame` |
| Загрузка | (`OnGameLeave`, если игра шла) → восстановление состояния → `OnSavegameRead` → `OnAfterLoadGame` (GM0) → `OnGameEnter` |

Остальные события ERA появятся вместе с хуками Olden Era (бой, экраны, клавиатура) — список нужных хуков в
`OldenEra_ReverseEngineering/07_InGame_RE_Plan.ru.md`. События Era с параметрами передают их в `x1…xN`
(`FireErmEventEx`), результат читается из возвращённых `x` (`RetXVars`): например,
`OnBeforeHeroInteraction` — `x1`, `x2` герои, `x3` = 0 запрещает встречу. В порте для этого есть
`ErmRuntime.RaiseEra(id, ctx, args)` и `ErmRuntime.RetX`.

## 2. Порядок загрузки скриптов

`TScriptMan.LoadScriptsFromDisk` при новой игре (после `FuncNames.Clear`, `FuncAutoId := 95000`, регистрации
имён событий и очистки констант):

1. `Data\s\lib\*.erm` — библиотеки (имя скрипта `lib\…`);
2. скрипты карты (`_inmap_` из глобальных событий карты, затем `Maps\<карта>\Data\s`);
3. `Data\s\*.erm` — глобальные скрипты (если не включён фиксированный список «load only these scripts.txt» и
   карта не запретила «вогификацию»);
4. `Data\s\lib_end\*.erm` — завершающие библиотеки.

Внутри папки файлы упорядочены `GetOrderedPrioritizedFileList`: сначала по имени (`AnsiCompareText`), затем
устойчивой сортировкой вставками по числовому приоритету — первому слову имени файла («906 имя.erm» → 906,
иначе 0), больший приоритет раньше. Скрипт с уже загруженным именем пропускается.

Моды ERA видит через виртуальную файловую систему: файл, который есть в нескольких модах, берётся из мода с
большим приоритетом. Порт: `EraScriptSet.Collect(моды по убыванию приоритета)`.

Отклонение порта: `AnsiCompareText` Windows сравнивает с учётом правил языка («word sort»: дефис и апостроф
почти не весят). Порт сравнивает без учёта регистра, игнорируя `-` и `'` на первом шаге. Для имён файлов
проекта ERA порядок совпадает; для экзотических имён возможны расхождения.

Кодировка: ERA читает байты как есть. Современные моды ERA в UTF-8 (комментарии, ключи переводов), старые
файлы WoG — cp1251. Порт декодирует строго корректный UTF-8 как UTF-8, всё остальное — как cp1251
(`EraText.Decode`).

## 3. Строки ERT (`z` больше 1000)

Рядом со скриптом может лежать `<имя>.ert` — текстовая таблица: первая строка — заголовок, далее строки
`номер<TAB>текст<TAB>…`, разделитель строк — CRLF (внутри текста бывает одиночный LF). Номер — индекс
z-строки (обычно больше 1000). При повторе номера ERA показывает «Duplicate ERM string index» и оставляет
первый. При чтении `z>1000` строка интерполируется (кроме локальных строк, см. ниже).

Локальные строки ERA — индексы от 1 000 000 000: строковые литералы параметров (живут до конца строки
команд), `VR:Z`, результаты `Erm_*` (живут до конца триггера). Они не сохраняются.

Порт: `WoGHost.LoadErt`, `EraState.Ert`, локальные — `ErmRuntime.CreateCmdLocalErt/CreateTriggerLocalErt`.

## 4. Переводы (`Trans.pas`)

`Lang\<язык>\*.json`, затем `Lang\*.json` каждого мода; ключи вложенных объектов склеиваются через точку,
элементы списков — по номеру; уже загруженный ключ не перезаписывается (поэтому мод с большим приоритетом
выигрывает). Числа и логические значения становятся строками («1»/«0»). `%T(ключ)` подставляет перевод без
параметров; `SN:T^ключ^/?строка/имя/значение/…` подставляет `@имя@` (`StrLib.BuildStr`: каждый нечётный
фрагмент между `@` — имя параметра, неизвестное имя остаётся как `@имя@`). Отсутствующий ключ возвращается
как есть. Язык — опция `Language` из `heroes3.ini` (по умолчанию `en`).

Порт: `EraLang` (`LoadMods`, `Tr`, `BuildStr`).

## 5. Сохранение

ERA пишет в сейв свои разделы: версию, скрипты (уже обработанные препроцессором), `FuncAutoId` и имена
функций, глобальные константы, строки ERT, w-переменные героев, `Era.DynArrays_SN_M` (все массивы `SN:M`;
элементы — только у массивов с хранением 1), `Era.AssocArray_SN_W` (именованные переменные; при загрузке
пустые — 0 и "" — отбрасываются), подсказки `SN:H`.

Порт хранит то же в `EraState` внутри файла состояния `*.wog.json` рядом с сейвом Olden Era (формат и проверка
принадлежности — `WoG_ReverseEngineering/08_Save_Load.ru.md`). После чтения вызывается
`EraState.NormalizeAfterLoad` (правила раздела выше). Скрипты при загрузке заново читаются с диска и
обрабатываются препроцессором с **сохранёнными** именами: известные имена сохраняют свои номера.

Отличие от ERA: ERA при загрузке берёт тексты скриптов из сейва, если они отличаются от текущих на диске.
Порт всегда берёт скрипты с диска (сейвы Olden Era не содержат скриптов ERA).
