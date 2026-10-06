# Heroes of Might and Magic: Olden Era — In the Wake of Gods 3.58

Порт механик **Heroes III: In the Wake of Gods 3.58** (WoG) на **Heroes of Might and Magic: Olden Era**.
Цель — не «мод по мотивам», а воспроизведение поведения WoG: язык ERM, командиры, опыт стеков, WoG Options,
скрипты WoG — так точно, как позволяет движок Olden Era.

Работа сделана по методике скилла `mod-any-game` из репозитория
[rehan-remade/universal-modder](https://github.com/rehan-remade/universal-modder): разведка движка → выбор
самого дешёвого пути → чтение первоисточников → вертикальный срез → проверка оракулом → журнал (`MODLOG.md`).

## Что уже есть

| Часть | Состояние |
|-------|-----------|
| Реверс-инжиниринг WoG 3.58 (`WoG_ReverseEngineering/`) | по исходному коду WoG (`GrayFace/wog`), справке ERM и 78 оригинальным скриптам 3.58f |
| Реверс-инжиниринг Olden Era (`OldenEra_ReverseEngineering/`) | по рабочим модам/инструментам сообщества; каждый факт помечен уровнем проверки; план проверки в игре |
| Матрица совместимости (`Compatibility/`) | по каждой фиче: стратегия, статус, ограничения; таблица ERM генерируется из кода |
| Ядро WoG (`src/WoG.Core`) | состояние, модель, опции (1000 независимых), события, сохранение (JSON + SHA-256), IdMap, визуальный резолвер |
| Рантайм ERM (`src/WoG.Erm`) | парсер + интерпретатор с точной семантикой WoG; **все 78 скриптов 3.58f и 117 файлов 3.59 разбираются без ошибок** |
| Командиры (`src/WoG.Commanders`) | таблицы и формулы `npc.cpp`: уровни, навыки, спец-бонусы, артефакты, найм/воскрешение, профиль в бою |
| Опыт стеков (`src/WoG.CreatureExperience`) | ранги, получение опыта после боя (4 стиля), слияние, бонусы, загрузчики `CREXPMOD/CREXPBON.TXT` |
| Плагин Olden Era (`src/WoG.OldenEra`) | BepInEx 6 IL2CPP, собирается против настоящего API; символы игры — из конфига; всё непроверенное выключено и сообщает *unsupported* |
| Оверлей данных (`src/WoG.OldenEra.Data`) | чтение `Core.zip`, клоны юнитов, баффы рангов опыта, локализация в формате OE |
| Тесты (`tests/WoG.Tests`) | 102 теста xUnit; тесты корпуса прогоняют настоящие скрипты WoG |
| Инструмент (`tools/WoG.ErmTool`) | `parse`, `run`, `compat`, `probe-symbols` |

**Честно о главном ограничении:** работа выполнена без копии Olden Era. Ничего ещё **не проверено в игре**.
Следующий шаг — `OldenEra_ReverseEngineering/07_InGame_RE_Plan.md` на машине с игрой: найти символы игры,
заполнить `wog_symbols.json`, пометить проверенные как `verified`.

## Сборка и тесты

Нужен .NET SDK 8 (ядро нацелено на `net6.0`, чтобы грузиться в BepInEx 6 IL2CPP).

```bash
dotnet build WoGOldenEra.sln
dotnet test tests/WoG.Tests

# соответствие на настоящих скриптах WoG (скрипты в репозиторий не кладутся):
WOG_SCRIPTS_DIR="/путь/к/WoG/Data/s" dotnet test tests/WoG.Tests --filter CorpusTests

dotnet run --project tools/WoG.ErmTool -- parse "/путь/к/WoG/Data/s"
dotnet run --project tools/WoG.ErmTool -- run   "/путь/к/WoG/Data/s"   # новая игра на эталонном движке + отчёт совместимости
dotnet run --project tools/WoG.ErmTool -- compat                         # таблица Compatibility/ERM_Compatibility.md
```

## Установка в игру (для этапа проверки)

1. Установить BepInEx 6 IL2CPP be.785 в папку Olden Era, запустить игру один раз.
2. Скопировать `src/WoG.OldenEra/bin/.../WoG.*.dll` в `BepInEx/plugins/WoG/`.
3. Первый запуск создаст `BepInEx/config/wog_symbols.json` (шаблон) — заполнить по плану RE.
4. Свои скрипты WoG (`*.erm`) — в `BepInEx/config/WoG/scripts/`; таблицы id — в `BepInEx/config/WoG/id-maps/`.
5. Журнал — `BepInEx/LogOutput.log` (строки `WoG …`, `[ERM] …`, `Game API not found: …`).

Только одиночная игра. Файлы игры не распространяются: всё, что берётся из WoG/H3/Olden Era, читается из
установок самого пользователя.

## Карта документации

* `WoG_ReverseEngineering/` — 00 источники · 01 язык ERM · 02 триггеры · 03 ресиверы · 04 командиры ·
  05 опыт стеков · 06 WoG Options · 07 бой/карта/города/фичи · 08 сохранение.
* `OldenEra_ReverseEngineering/` — 00 источники и метки проверки · 01 движок · 02 данные `Core.zip` ·
  03 скриптинг сценариев · 04 баффы и бой · 05 сейвы · 06 что доступно · 07 план проверки в игре.
* `Compatibility/` — архитектура · матрица · ERM по командам · `id-maps/` · `options-defaults.json`.
* `MODLOG.md` — журнал работ: что сделано, что найдено, что не получилось, что дальше.
