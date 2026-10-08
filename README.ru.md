[English](README.md) | **Русский**

# ERA:Transcendence

**ERA:Transcendence** — рабочее имя этого переноса: HoMM3 ERA для Heroes of Might and Magic: Olden Era.

Порт **HoMM3 ERA** — развитой сборки *In the Wake of Gods* (WoG 3.58f + движок Era 3.9.31 + ERM 2.0 + скрипты
ERA Project) — на **Heroes of Might and Magic: Olden Era**. Цель — не «мод по мотивам», а воспроизведение
поведения ERA: язык ERM 2.0, командиры, опыт стеков, WoG Options, скрипты WoG и ERA — так точно, как позволяет
движок Olden Era.

Английская (основная) версия: [README.md](README.md). У каждого документа в репозитории есть русская копия рядом с ним (`*.ru.md`).

Сначала целью был WoG 3.58; по решению пользователя цель — ERA (`ERA-Projects/era-project-eng`,
`era-project-rus`). ERA построена на WoG, поэтому вся работа по WoG 3.58 используется дальше как нижний слой.

Работа ведётся по методике скилла `mod-any-game` из репозитория
[rehan-remade/universal-modder](https://github.com/rehan-remade/universal-modder): разведка движка → выбор
самого дешёвого пути → чтение первоисточников → вертикальный срез → проверка оракулом → журнал (`MODLOG.md`).

## Что уже есть

| Часть | Состояние |
|-------|-----------|
| Реверс-инжиниринг ERA (`ERA_ReverseEngineering/`) | по исходникам движка Era 3.9.31 (`ethernidee/era`) и корпусу ERA Project 2.291 |
| Реверс-инжиниринг WoG 3.58 (`WoG_ReverseEngineering/`) | по исходному коду WoG (`GrayFace/wog`), справке ERM и 78 скриптам 3.58f |
| Реверс-инжиниринг Olden Era (`OldenEra_ReverseEngineering/`) | по рабочим модам/инструментам сообщества и запущенной игре; каждый факт помечен уровнем проверки |
| Матрица совместимости (`Compatibility/`) | по каждой фиче: стратегия, статус, ограничения; таблицы ERM (WoG и ERA) генерируются из кода |
| Ядро (`src/WoG.Core`) | состояние, модель, опции и окно WoG Options (`WoGOptionSetup`: значения установки по умолчанию), события, сохранение (JSON + SHA-256), состояние ERA, IdMap, картинки Heroes III (LOD/PAC, DEF, PCX) |
| ERM (`src/WoG.Erm`) | парсер и интерпретатор WoG 3.58/3.59 **и ERA**: препроцессор ERM 2.0, параметры/условия/поток управления/функции ERA, ресиверы героев, городов, игроков, карты, боёв, переводы, ERT |
| Командиры (`src/WoG.Commanders`) | таблицы и формулы `npc.cpp`: уровни, навыки, спец-бонусы, артефакты, найм/воскрешение, профиль в бою |
| Опыт стеков (`src/WoG.CreatureExperience`) | ранги, получение опыта после боя, слияние, бонусы, загрузчики `CREXPMOD/CREXPBON.TXT` |
| Хост (`src/WoG.Host`) | связывает всё вместе: моды ERA в порядке Era, WoG'ификация и WoG Options при новой карте, события → триггеры ERM (`BattleActionTracker` для триггеров боя), сохранение/загрузка |
| Плагин Olden Era (`src/WoG.OldenEra`) | BepInEx 6 IL2CPP; загружает моды ERA из установки ERA пользователя; символы игры проверены в игре (`wog_symbols.json`); хуки дней, посещений объектов, боёв и их событий; командный мост WoG Debug |
| Плагин интерфейса (`src/WoG.OldenEra.DebugUI`) | собран из рамки окна, кнопок и шрифтов самой Olden Era: вопросы WoG в стиле игры (всегда) и **окно WoG Debug** (при включённом WoG Debug): каждая функция порта — плитка с круглой иконкой (картинки HoMM3, WoG и ERA читаются из установки ERA во время игры, спрайты Olden Era), круглые вкладки групп, поиск, консоль ERM; F8 или круглая кнопка «WoG» в верхней полосе |
| Оверлей данных (`src/WoG.OldenEra.Data`) | чтение `Core.zip`, клоны юнитов, баффы рангов опыта, локализация в формате OE |
| Тесты (`tests/WoG.Tests`) | 273 теста xUnit; тесты корпусов прогоняют настоящие скрипты WoG и ERA и декодируют каждую картинку установки ERA |
| Инструменты (`tools/`) | `WoG.ErmTool`: `parse`, `run`, `compat`, `era-pp`, `probe-symbols`; `deploy/deploy.ps1` — сборка и установка в игру; `docs/check-translations.py` — фрагменты кода и числа каждой пары X.md и X.ru.md; `oe-recon/collect.ps1` |

Проверка на настоящих скриптах:
* ERA 2.291 пользователя (русская) с основными модами `WoG`, `Era Erm Framework`, `WoG Rus`, `WoG Scripts`,
  `WoG Scripts Rus`, `WoG Fix Lite`, `ERA Scripts` в порядке приоритета Era, со значениями WoG Options по умолчанию
  из установки (включено 96 опций) и WoG'ификацией: препроцессор, разбор, новая игра + 7 дней — **183 скрипта,
  0 ошибок ERM**; то, что движок пока не умеет, перечислено как *unsupported*;
* WoG 3.58f (78 файлов) и WoG 3.59 (117 файлов): разбор и запуск — 0 ошибок.

**Состояние:** с сессии 2 (2026-10-07) порт работает в настоящей игре (Olden Era 0.81.04, BepInEx 6). Проверено в
игре: начало дня, посещение объектов, сохранение/загрузка состояния WoG, слой карты, герои (навыки, заклинания,
артефакты, имя, биография, специализация, нанятая армия), города (здания, существа для найма, гарнизон, владелец,
название, доход), игроки, шахты, типы существ, бои (стеки боя и триггеры !?BR, !?BG, !?MF), WoG'ификация и значения
WoG Options по умолчанию — каждый шаг с доказательствами в `MODLOG.md`; чего нет, отмечено как *unsupported* в
`Compatibility/`.

## Сборка и тесты

Нужен .NET 8 SDK (библиотеки нацелены на `net6.0`, чтобы грузиться в BepInEx 6 IL2CPP). Плагины собираются против
установленной игры: задайте `OLDEN_ERA_DIR` — папку Olden Era (BepInEx в ней запускался хотя бы раз).

```bash
dotnet build WoGOldenEra.sln
dotnet test tests/WoG.Tests

# соответствие на настоящих скриптах и данных (ничего из этого в репозиторий не кладётся):
ERA_MODS_DIR="/путь/к/era-project-eng/Mods" dotnet test tests/WoG.Tests --filter EraCorpus
WOG_SCRIPTS_DIR="/путь/к/WoG/Data/s" dotnet test tests/WoG.Tests --filter CorpusTests
ERA_GAME_DIR="/путь/к/ERA" dotnet test tests/WoG.Tests   # каждая картинка установки, её значения WoG Options по умолчанию

# ERA: новая игра на безголовом эталонном движке + отчёт совместимости.
# Моды идут от высшего приоритета — в порядке, обратном Mods/list.txt ERA.
M="/путь/к/ERA/Mods"
dotnet run --project tools/WoG.ErmTool -- run --era "$M/ERA Scripts" "$M/WoG Fix Lite" "$M/WoG Scripts Rus" "$M/WoG Scripts" "$M/WoG Rus" "$M/Era Erm Framework" "$M/WoG"
dotnet run --project tools/WoG.ErmTool -- compat --era > Compatibility/ERM_Compatibility_ERA.md                # таблица (английская)
dotnet run --project tools/WoG.ErmTool -- compat --era --lang ru > Compatibility/ERM_Compatibility_ERA.ru.md   # русская копия таблицы

# WoG 3.58
dotnet run --project tools/WoG.ErmTool -- run "/путь/к/WoG/Data/s"
dotnet run --project tools/WoG.ErmTool -- compat > Compatibility/ERM_Compatibility.md                          # таблица (английская)
dotnet run --project tools/WoG.ErmTool -- compat --lang ru > Compatibility/ERM_Compatibility.ru.md             # русская копия таблицы

# документация: каждый X.md против своего X.ru.md
python tools/docs/check-translations.py --verbose
```

## Установка в игру

1. Установить BepInEx 6 IL2CPP be.785 в папку Olden Era и запустить игру один раз (она создаст
   `BepInEx/interop`). Перед этим сделать копию сохранений и `HeroesOldenEra_Data/StreamingAssets/Core.zip`.
2. При закрытой игре: `powershell -File tools/deploy/deploy.ps1 [-DebugMode]` собирает оба плагина и ставит их
   в `BepInEx/plugins/WoG/` вместе с символами игры (`BepInEx/config/wog_symbols.json`), таблицами id
   (`BepInEx/config/WoG/id-maps/`) и модом WoG Debug; `-DebugMode` включает WoG Debug.
3. Настройки, `BepInEx/config/wog.oldenera.cfg`: `[ERA] ModsRoot` — папка `Mods` вашей установки ERA (её
   `list.txt` задаёт моды и их порядок), `Language` (`ru`); `[ERM] Dialect` — `Era` (по умолчанию) или `Wog`;
   `[WoG] Wogify` — WoG'ификация (-1 = как в WoG Options); `[Debug] Enabled` — WoG Debug.
   `BepInEx/config/wog.oldenera.debugui.cfg`: `[Window] Hotkey` — клавиша окна WoG Debug (F8; F9 — быстрая
   загрузка игры).
4. Журнал: `BepInEx/LogOutput.log` (строки `WoG …`, `[ERM] …`).

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
* `MODLOG.md` — журнал работ; `HANDOFF.md` — как продолжить работу; `TRANSLATION_STATUS.md` — состояние
  двуязычной документации.
