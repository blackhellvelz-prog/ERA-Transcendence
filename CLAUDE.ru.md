[English](CLAUDE.md) | **Русский**

# Инструкции для Claude Code в этом репозитории

## Проект

Порт **HoMM3 ERA** (WoG 3.58f + движок Era 3.9.31 + ERM 2.0 + скрипты ERA Project) на **Heroes of Might and
Magic: Olden Era** (Unity IL2CPP, BepInEx 6). Полное задание пользователя и все решения — в `MODLOG.ru.md`,
текущее состояние и план — в `HANDOFF.ru.md`, обзор — в `README.ru.md`. Прочитай эти три файла в начале работы.

Работа ведётся по скиллу `mod-any-game` (лежит в `.claude/skills/mod-any-game`, из
`rehan-remade/universal-modder`): разведка → самый дешёвый путь → первоисточники → вертикальный срез →
проверка оракулом → журнал.

## Правила пользователя (обязательные)

* **Документация — на русском** (все `.md`). Комментарии в коде — на английском, как в остальном коде.
* Сначала функциональность, потом графика. Не подделывать геймплей: чего нет — честно `Unsupported` в отчёте
  совместимости, ничего не «делать вид».
* Не предполагать возможности Olden Era — проверять (метки `[V-code] [V-data] [V-community] [UNVERIFIED]`).
  Не предполагать поведение ERA/WoG — читать исходники (`ethernidee/era`, `GrayFace/wog`) и проверять на
  корпусе скриптов.
* Ассеты: переиспользовать ассеты Olden Era, перекрашивать раньше, чем моделировать; заглушки разрешены;
  новые 3D-модели — только если без них нельзя.
* Только одиночная игра. Никакого обхода античитов. Файлы игр (ERA/WoG/H3/Olden Era) и производные данные
  **не коммитить** и не распространять — порт читает их из установки пользователя.
* Перед изменениями в папке игры: резервная копия сейвов и `HeroesOldenEra_Data/StreamingAssets/Core.zip`.
  Перед установкой загрузчика (BepInEx) или изменением настроек игры — **спросить пользователя**.
* Каждое заметное открытие и неудача — в `MODLOG.ru.md` (что, почему, как проверено, как откатить).

## Код

| Где | Что |
|-----|-----|
| `src/WoG.Core` | состояние (`WoGGameState`, `EraState`), модель, опции, события, сохранение, IdMap, интерфейсы адаптера |
| `src/WoG.Erm` | ERM: `Syntax/` парсер (WoG и `ErmParserEra.cs`), `Era/` препроцессор ERM 2.0, события, порядок загрузки; `Runtime/` интерпретатор (`EraProcess.cs`, `EraValues.cs` — режим ERA); `Receivers/` ресиверы (`EraReceivers.cs`, `EraApi.cs`) |
| `src/WoG.Commanders`, `src/WoG.CreatureExperience` | командиры и опыт стеков (порт `npc.cpp`, `crexpo.cpp`) |
| `src/WoG.Host` | `WoGHost`: связывает всё; `AddEraMods` + `StartNewGame/SaveTo/LoadFrom` |
| `src/WoG.Headless` | эталонный движок в памяти для тестов |
| `src/WoG.OldenEra` | плагин BepInEx 6 IL2CPP: символы игры из `wog_symbols.json`, адаптер, хуки Harmony |
| `src/WoG.OldenEra.Data` | оверлей `Core.zip` (клоны юнитов, баффы, локализация) |
| `tools/WoG.ErmTool` | `parse`, `run [--era]`, `compat [--era]`, `era-pp`, `probe-symbols` |
| `tools/oe-recon/collect.ps1` | сбор данных об установке Olden Era (только чтение) |
| `tools/fetch-references/` | скачать исходники для справки в `../research` |

Режим ERA: `ErmRuntimeOptions.Dialect = ErmDialect.Era`. Принцип — построчный перенос функций Era
(`PreprocessErm`, `Hook_ZvsGetNum`, `ProcessErm`, `VR_*`, `SN_*`) с сохранением их ошибок; каждый перенос
подписан именем исходной функции в комментарии.

## Команды

```bash
dotnet build WoGOldenEra.sln
dotnet test tests/WoG.Tests
# корпуса (после tools/fetch-references):
ERA_MODS_DIR=../research/era-eng/Mods dotnet test tests/WoG.Tests --filter EraCorpus
WOG_SCRIPTS_DIR="../research/wogify/Mods/WoG Wogify Scripts 3.58f/Data/s" dotnet test tests/WoG.Tests --filter CorpusTests
M=../research/era-eng/Mods
dotnet run --project tools/WoG.ErmTool -- run --era "$M/Era Erm Framework" "$M/ERA Scripts" "$M/WoG Scripts" "$M/WoG"
dotnet run --project tools/WoG.ErmTool -- compat --era > /tmp/t.md   # обновить Compatibility/ERM_Compatibility_ERA.ru.md
```

Перед коммитом: сборка без предупреждений, все тесты зелёные, корпус ERA без ошибок. Таблицы
`Compatibility/ERM_Compatibility*.md` генерируются — правь `Declare(...)` в ресиверах, а не таблицы.

## Git

Рабочая ветка: `claude/wog-olden-era-port` (`origin` = `github.com/blackhellvelz-prog/ModsClaudeVelz`).
PR не создавать, пока пользователь не попросит.
