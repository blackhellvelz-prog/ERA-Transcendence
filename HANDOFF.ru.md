[English](HANDOFF.md) | **Русский**

# Как продолжить работу на своём компьютере

> **Не закончено:** переход на английскую основную документацию почти готов — см. `TRANSLATION_STATUS.md`, закончить первым делом.

Этот файл — для вас и для Claude Code, который будет работать на компьютере с Olden Era. Облачная сессия, в
которой сделана текущая часть работы, не видит ваш компьютер; локальный Claude Code видит и может проверять
всё прямо в игре.

## Где мы сейчас (07.10.2026)

* Цель — порт **HoMM3 ERA** на Olden Era (решение пользователя: ERA вместо WoG 3.58).
* Готово и проверено без игры: реверс-инжиниринг ERA/WoG/Olden Era (папки `*_ReverseEngineering`), матрица
  совместимости, ядро, рантайм ERM для WoG и **ERA** (препроцессор ERM 2.0, семантика ERA), командиры, опыт
  стеков, сохранение, оверлей данных, плагин BepInEx (собирается, но в игре не запускался).
* Весь проект ERA (183 скрипта) запускается как новая игра на тестовом движке без ошибок. 132 теста зелёные.
* **В самой Olden Era ещё ничего не проверено** — это следующий этап.

## Что поставить

1. **Git** — https://git-scm.com/download/win
2. **.NET 8 SDK** — https://dotnet.microsoft.com/download/dotnet/8.0
3. **Claude Code** — приложение Claude для компьютера (вкладка Code) или CLI в терминале; как установить —
   https://code.claude.com/docs
4. Heroes of Might and Magic: Olden Era (Steam) — уже есть.

## Шаги

1. Распакуйте архив (или клонируйте: `git clone -b claude/wog-olden-era-port
   https://github.com/blackhellvelz-prog/ModsClaudeVelz`). В архиве — репозиторий с историей git, так что
   коммиты и `git push` работают как обычно.
2. Откройте папку репозитория в Claude Code: в приложении — «Open folder», в терминале — `cd ModsClaudeVelz`
   и `claude`. Claude прочитает `CLAUDE.md` и подхватит скилл `mod-any-game` из `.claude/skills`.
3. Отправьте первое сообщение (можно скопировать как есть):

> Продолжаем порт ERA на Olden Era. Прочитай CLAUDE.md, HANDOFF.ru.md и MODLOG.ru.md. Затем:
> 1) запусти `tools\fetch-references\fetch-references.ps1` и проверь `dotnet test` (включая корпуса ERA и WoG);
> 2) запусти `tools\oe-recon\collect.ps1` и разбери результат: версия игры, Unity, BepInEx, данные Core.zip,
>    сейвы; запиши факты в OldenEra_ReverseEngineering с меткой проверки;
> 3) предложи план установки BepInEx 6 IL2CPP be.785 (с резервными копиями) и спроси меня перед установкой;
> 4) дальше — OldenEra_ReverseEngineering/07_InGame_RE_Plan.ru.md: найти символы игры, заполнить
>    wog_symbols.json, подключить моды ERA в плагине (WoGHost.AddEraMods) и проверить первый вертикальный срез
>    в игре (например, ERA-скрипт на OnEveryDay, который меняет ресурсы игрока).
> Документацию пиши на английском, с русской копией рядом (*.ru.md).

## Что дальше по плану (кратко)

1. Разведка установки Olden Era (`collect.ps1`) → факты с метками проверки.
2. BepInEx 6 IL2CPP + `BepInEx/interop` → `probe-symbols` → `wog_symbols.json` (герои, игроки, ход, объекты,
   бой, сейвы) → пометить проверенные символы `verified`.
3. Плагин: подключить моды ERA (папка с модами ERA пользователя или `BepInEx/config/WoG/mods`), события ERA
   из хуков Olden Era, сохранение рядом с сейвом.
4. Ресиверы, которые нужны корпусу ERA: `UN` (карта), `BM/BU/BG/BA` (бой), `CA`, `PO`, `OB`, `DL`.
5. Командиры и опыт стеков в бою (баффы, юнит командира), UI WoG Options.

## Важно

* Только одиночная игра. Перед изменением папки игры — копия сейвов и `Core.zip`.
* Файлы игр (Olden Era, ERA, WoG, H3) не коммитить в репозиторий — он публичный.
* Справочные исходники (Era, WoG, ERA Project, universal-modder) скачиваются скриптом
  `tools/fetch-references` в `..\research` — в архив они не входят.
