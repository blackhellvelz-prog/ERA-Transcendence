[English](00_Sources_and_Verification.md) | **Русский**

# Реверс-инжиниринг Olden Era — источники и уровни проверки

Heroes of Might and Magic: Olden Era (Unfrozen / Ubisoft), Steam app **3105440**, ранний доступ с
**30.04.2026**. Структура установки: `…/Heroes of Might and Magic Olden Era/HeroesOldenEra_Data/…`.

## Честное заявление об объёме

Эта работа выполнена в облачном контейнере **без копии Olden Era**. Ничего здесь не получено запуском или
дизассемблированием игры в этой сессии. Поэтому у каждого факта об Olden Era есть метка способа проверки:

| Метка | Значение |
|-------|----------|
| **[V-code]** | Проверено по исходному коду работающего мода/инструмента сообщества, который работает с настоящей игрой (иначе код бы не работал) |
| **[V-data]** | Проверено инструментами, которые разбирают настоящий `Core.zip` и документируют найденные ключи («сверено с N реальными файлами») |
| **[V-community]** | Утверждается моддерами, проверявшими это в игре (Steam/Nexus/документация инструментов), независимо не проверено |
| **[UNVERIFIED]** | Правдоподобно, нужно по архитектуре, но должно быть подтверждено на реальной установке — см. `07_InGame_RE_Plan.ru.md` |
| **[V-game]** | Проверено в запущенной игре на установке пользователя (07.10.2026, версия 0.81.04): лог плагина, самотест WoG Debug и интерфейс игры как оракул (`MODLOG.ru.md`, сессия 2) |

Правило 7 проекта («не предполагать возможностей Olden Era») соблюдается через эти метки: слой совместимости
опирается только на факты **[V-*]**; пункты **[UNVERIFIED]** закрыты проверками (символы со статусом
`verified`), которые до подтверждения возвращают *unsupported*.

## Источники

| # | Источник | Для чего |
|---|----------|----------|
| O1 | `mimiasei/map-editor-json-tool` (GitHub; «HoMM Olden Era Scenario Editor», v0.9.1, обновлён 06.10.2026) | Реестр скриптинга сценариев (55 условий, 114 действий — из официального Notion Unfrozen «Conditions/Actions and their parameters — Full list»), структура `Core.zip`, клонирование контента, формат карт, импорт карт H3 |
| O2 | Папка `gme-mod/` из O1 — плагин **BepInEx 6 IL2CPP** для встроенного редактора карт (`GameApi.cs`, `Plugin.cs`) | Факты о движке: IL2CPP, обфускация, имена сборок, реальные имена типов (`Hex.MapEditor.BhNewGenMap`, `Hex.MapEditor.BhMapEditor`), сборка BepInEx `6.0.0-be.785`, `net6.0` |
| O3 | Страницы Nexus Mods для Olden Era: «BepInEx 6 (with version-independent deobfuscation)», «CheatPanel – BepInEx 6 IL2CPP», «Mod Configuration Panel», «Hero Creator and Editor» | Экосистема модов **[V-community]** |
| O4 | Обсуждение Steam «Modding Game Files» (app 3105440) | Игроки правят «JSON core files», упоминание `hex.dll` + dnSpy **[V-community]**, противоречит O2 — см. `01_Engine_and_Runtime.ru.md` |
| O5 | `KhanDevelopsGames/Olden-Era---Template-Generator`, `ignis-sec/HoMM-OE-Template-Editor` | Формат шаблонов случайных карт (`.rmg.json`) |

Не использовался: `Weolcan/homm-olden-era-community-mods` («Official Release & Review Guide» — SEO-контент,
а не инструмент; проверять нечего).
