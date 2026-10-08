[English](README.md) | **Русский**

# ERA:Transcendence

**ERA:Transcendence** — рабочее имя этого переноса: HoMM3 ERA для Heroes of Might and Magic: Olden Era.

Порт **HoMM3 ERA** — развитой сборки *In the Wake of Gods* (WoG 3.58f + движок Era 3.9.31 + ERM 2.0 + скрипты
ERA Project) — на **Heroes of Might and Magic: Olden Era**. Цель — не «мод по мотивам», а воспроизведение
поведения ERA: язык ERM 2.0, командиры, опыт стеков, WoG Options, скрипты WoG и ERA — так точно, как позволяет
движок Olden Era.

Английская (основная) версия: [README.md](README.md). У каждого документа в репозитории есть английский оригинал `X.md` и русская копия `X.ru.md`.

Сначала целью был WoG 3.58; по решению пользователя цель — ERA (`ERA-Projects/era-project-eng`,
`era-project-rus`). ERA построена на WoG, поэтому вся работа по WoG 3.58 используется дальше как нижний слой.

Работа ведётся по методике скилла `mod-any-game` из репозитория
[rehan-remade/universal-modder](https://github.com/rehan-remade/universal-modder): разведка движка → выбор
самого дешёвого пути → чтение первоисточников → вертикальный срез → проверка оракулом → журнал (`MODLOG.ru.md`).

## Что уже есть

| Часть | Состояние |
|-------|-----------|
| Реверс-инжиниринг ERA (`ERA_ReverseEngineering/`) | по исходникам движка Era 3.9.31 (`ethernidee/era`) и корпусу ERA Project 2.291 |
| Реверс-инжиниринг WoG 3.58 (`WoG_ReverseEngineering/`) | по исходному коду WoG (`GrayFace/wog`), справке ERM и 78 скриптам 3.58f |
| Реверс-инжиниринг Olden Era (`OldenEra_ReverseEngineering/`) | по рабочим модам/инструментам сообщества; каждый факт помечен уровнем проверки; план проверки в игре |
| Матрица совместимости (`Compatibility/`) | по каждой фиче: стратегия, статус, ограничения; таблицы ERM (WoG и ERA) генерируются из кода |
| Ядро (`src/WoG.Core`) | состояние, модель, опции, события, сохранение (JSON + SHA-256), состояние ERA (`EraState`), IdMap, визуальный резолвер |
| ERM (`src/WoG.Erm`) | парсер и интерпретатор WoG 3.58/3.59 **и ERA**: препроцессор ERM 2.0, параметры/условия/поток управления/функции ERA, `VR`/`FU`/`DO`/`SN` ERA, переводы, ERT |
| Командиры (`src/WoG.Commanders`) | таблицы и формулы `npc.cpp`: уровни, навыки, спец-бонусы, артефакты, найм/воскрешение, профиль в бою |
| Опыт стеков (`src/WoG.CreatureExperience`) | ранги, получение опыта после боя, слияние, бонусы, загрузчики `CREXPMOD/CREXPBON.TXT` |
| Плагин Olden Era (`src/WoG.OldenEra`) | BepInEx 6 IL2CPP, собирается против настоящего API; символы игры — из конфига; всё непроверенное выключено и сообщает *unsupported* |
| Окно WoG Debug (`src/WoG.OldenEra.DebugUI`) | консоль ERM и команд отладки прямо в игре (F9 или кнопка «WoG»), собранная из рамки окна, кнопок, шрифтов и полосы прокрутки самой Olden Era; только при включённом WoG Debug |
| Оверлей данных (`src/WoG.OldenEra.Data`) | чтение `Core.zip`, клоны юнитов, баффы рангов опыта, локализация в формате OE |
| Тесты (`tests/WoG.Tests`) | 187 тестов xUnit; тесты корпусов прогоняют настоящие скрипты WoG и ERA |
| Инструменты (`tools/`) | `WoG.ErmTool`: `parse`, `run`, `compat`, `era-pp`, `probe-symbols`; `oe-recon/collect.ps1` — сбор данных об установке Olden Era |

Проверка на настоящих скриптах:
* ERA Project 2.291 (моды `Era Erm Framework`, `ERA Scripts`, `WoG Scripts`, `WoG`; 183 скрипта, 37 958 строк
  команд): препроцессор, разбор и запуск новой игры + 7 дней — **0 ошибок**;
* WoG 3.58f (78 файлов) и WoG 3.59 (117 файлов): разбор и запуск — 0 ошибок.

**Состояние:** с сессии 2 (2026-10-07) порт работает в настоящей игре (Olden Era 0.81.04, BepInEx 6): начало дня,
посещение объектов, бои, сохранение/загрузка состояния WoG, слой карты, герои (навыки, заклинания, артефакты), города
(здания, найм, гарнизон, владелец, название), типы существ и другое проверены в игре — каждый шаг с доказательствами в `MODLOG.ru.md`; чего нет, отмечено как
*unsupported* в `Compatibility/`.

## Сборка и тесты

Нужен .NET SDK 8 (библиотеки нацелены на `net6.0`, чтобы грузиться в BepInEx 6 IL2CPP).

```bash
dotnet build WoGOldenEra.sln
dotnet test tests/WoG.Tests

# соответствие на настоящих скриптах (скрипты в репозиторий не кладутся):
ERA_MODS_DIR="/путь/к/era-project-eng/Mods" dotnet test tests/WoG.Tests --filter EraCorpus
WOG_SCRIPTS_DIR="/путь/к/WoG/Data/s" dotnet test tests/WoG.Tests --filter CorpusTests

# ERA: новая игра на эталонном движке + отчёт совместимости (моды — от большего приоритета к меньшему)
M="/путь/к/era-project-eng/Mods"
dotnet run --project tools/WoG.ErmTool -- run --era "$M/Era Erm Framework" "$M/ERA Scripts" "$M/WoG Scripts" "$M/WoG"
dotnet run --project tools/WoG.ErmTool -- era-pp /tmp/era-pp "$M/Era Erm Framework" "$M/ERA Scripts" "$M/WoG Scripts" "$M/WoG"
dotnet run --project tools/WoG.ErmTool -- compat --era > Compatibility/ERM_Compatibility_ERA.md                # таблица (английская)
dotnet run --project tools/WoG.ErmTool -- compat --era --lang ru > Compatibility/ERM_Compatibility_ERA.ru.md   # русская копия таблицы

# WoG 3.58
dotnet run --project tools/WoG.ErmTool -- run "/путь/к/WoG/Data/s"
dotnet run --project tools/WoG.ErmTool -- compat > Compatibility/ERM_Compatibility.md                          # таблица (английская)
dotnet run --project tools/WoG.ErmTool -- compat --lang ru > Compatibility/ERM_Compatibility.ru.md             # русская копия таблицы
```

## Установка в игру (для этапа проверки)

1. Установить BepInEx 6 IL2CPP be.785 в папку Olden Era, запустить игру один раз.
2. Скопировать `src/WoG.OldenEra/bin/.../WoG.*.dll` в `BepInEx/plugins/WoG/`.
3. Первый запуск создаст `BepInEx/config/wog_symbols.json` (шаблон) — заполнить по плану RE.
4. Скрипты — в `BepInEx/config/WoG/scripts/`; таблицы id — в `BepInEx/config/WoG/id-maps/`.
   (Подключение модов ERA целиком через `WoGHost.AddEraMods` в плагине — следующий шаг.)
5. Журнал — `BepInEx/LogOutput.log` (строки `WoG …`, `[ERM] …`, `Game API not found: …`).

Только одиночная игра. Файлы игры не распространяются: всё, что берётся из ERA/WoG/H3/Olden Era, читается из
установок самого пользователя.

## Карта документации

Каждый документ есть на двух языках: английский оригинал `X.md` (основной) и синхронизированная русская копия
`X.ru.md` рядом с ним. Комментарии в коде — на английском.

* `ERA_ReverseEngineering/` — 00 обзор и решение · 01 препроцессор ERM 2.0 · 02 семантика ERA · 03 события,
  загрузка модов, переводы, ERT, сохранение.
* `WoG_ReverseEngineering/` — 00 источники · 01 язык ERM · 02 триггеры · 03 ресиверы · 04 командиры ·
  05 опыт стеков · 06 WoG Options · 07 бой/карта/города/фичи · 08 сохранение.
* `OldenEra_ReverseEngineering/` — 00 источники и метки проверки · 01 движок · 02 данные `Core.zip` ·
  03 скриптинг сценариев · 04 баффы и бой · 05 сейвы · 06 что доступно · 07 план проверки в игре.
* `Compatibility/` — архитектура · матрица · ERM по командам (WoG и ERA) · `id-maps/` · `options-defaults.json`.
* `MODLOG.ru.md` — журнал работ; `HANDOFF.ru.md` — как продолжить работу на своём компьютере.
