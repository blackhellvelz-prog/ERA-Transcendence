[English](02_Data_Layer_CoreZip.md) | **Русский**

# Olden Era — слой данных (`Core.zip`)

Весь игровой контент — JSON внутри `HeroesOldenEra_Data/StreamingAssets/Core.zip` **[V-data]** (O1 читает его
через JSZip и перечисляет папки ниже). Обновления игры перезаписывают `Core.zip`.

## 1. Механизм оверлея

* Дополнительные `.zip`, положенные **рядом с `Core.zip`**, подмешиваются поверх него при запуске
  **[V-community]** (README O1: «The engine merges your ZIP on top of `Core.zip` at runtime»; его свои герои,
  объекты карты, артефакты и баффы, поставляемые так, появляются во встроенном редакторе карт и работают —
  по комментариям к типам O1, задачи #139/#146/#150/#165, проверено в игре).
* Новый контент добавляется **клонированием существующего определения под новым id** («clone a real
  definition, mint a new id, ship it»). Клон логики объекта карты **обязан** лежать в той же подпапке семейства
  `objects_logic`, что и исходник — «общая подпапка ломает объект» **[V-community]**. Для юнитов порт кладёт
  клон в папку исходника (`OverlayBuilder.CloneUnit`) — то же правило по аналогии, **[UNVERIFIED]**.
* Действует ли оверлей глобально или только для своей карты — **[UNVERIFIED]**; O1 всегда поставляет его к
  карте. Генератор порта выпускает один `wog_core.zip` и проверяет глобальность отдельной пробой.
* Локализация: `Lang/<язык>/texts/<файл>.json` = `{"tokens":[{"sid","text"}]}` с UTF-8 BOM; архив без сжатия
  (STORE) — как у O1 **[V-code]**. Отсутствующий ключ показывается сырым sid **[V-data]**.

## 2. Карта папок

| Путь в zip | Содержимое | Форма (по документации O1) |
|------------|------------|----------------------------|
| `DB/units/units_logics/<фракция>/*.json` | существа | `id`, `fraction`, `tier`, `icon`, `stats{hp, offence, defence, damageMin, damageMax, initiative, speed, luck, moral, actionPoints, numCounters, energyPerCast, energyPerRound, energyPerTakeDamage}`, `unitCost{costResArray[{name,cost}]}`, способности |
| `DB/heroes/<фракция>/*.json`, `DB/heroes/custom_maps/` | герои (108 в 6 фракциях + кампания) | `id,name,description,motto,mesh,mounts,icon,fraction,nativeBiome,classType (might/magic),skillsRollVariant,costGold,startLevel,startSquad[{sid,min,max}],specialization,stats{viewRadius,statsNum,magicCastsPerRound,enableTactics,tacticsPlacementSize,offence,defence,spellPower,intelligence,luck,moral},statsRolls,startSkills[{sid,skillLevel}],startMagics[{sidConfig,level,isLearned}]` |
| `DB/heroes_specializations/*.json` | специализации героев | |
| `DB/heroes_skills/skills/*.json` | вторичные навыки («subskills») | |
| `DB/magics/*.json` | заклинания | |
| `DB/items/items/*.json` | артефакты (плоские массивы) | `id,name,description,narrativeDescription,icon,slot_,rarity,bonuses,goodsValue,…` (в т.ч. `battleSubskillBonus` со ссылками на баффы) |
| `DB/buffs/*.json` | баффы / эффекты (19 файлов, 414 баффов) | см. `04_Buffs_and_Battle.ru.md` |
| `DB/fractions/*.json` | фракции | порядок и названия |
| `DB/squads/**` | шаблоны нейтральных отрядов | |
| `DB/map/objects/{1_environments,2_animals,3_resources,4_interactables,5_fxs,6_artifacts,7_spawns,8_test,9_blocks}.json` | шаблоны объектов карты | `id,name,tag,isInteractable,prefs (ссылки на 3D-префабы),geometry,generatorConfig` — **поля иконки нет** |
| `DB/objects_logic/<семейство>/**` (`cities/*_city.json`, `res_mines/mines.json`, `event_banks/**` …) | поведение интерактивных объектов | |
| `DB/map/trigger_zones/zones.json`, `waters`, `rivers`, `roads`, `tiles` | слои карты | |
| `DB/dialogs/dialogs/**` | ~769 диалогов | `{"array":[flow]}` |
| `DB/res/resources_info.json` | ресурсы | |
| `DB/difficulties_lobby.json` | пресеты сложности | |
| `Lang/<язык>/texts/*.json` | локализация (16 языков) | |

Объёмы **[V-data]**: 146 id существ (6 фракций × 21 вместе с апгрейдом и альтернативным апгрейдом + 19
нейтральных), 108 героев, 158 интерактивных объектов карты.
Фракции: **Temple, Necropolis, Grove, Dungeon, Hive, Schism** (+ нейтралы). У каждого существа есть база,
`_upg` и `_upg_alt`.

## 3. Карты

* Бинарный файл `.map` = gzip + JSON-блоки с префиксом длины LEB128 **[V-data]**; O1 читает и пишет его.
* Скрипт сценария — `.json` рядом с `.map` с тем же именем **[V-community]**; он обязателен, как только у
  любого объекта есть скриптовые действия (иначе карта не загружается).
* O1 умеет импортировать карты Heroes III `.h3m` в карты Olden Era (ландшафт, города, герои, монстры, шахты,
  жилища, артефакты, порталы, глобальные события) **[V-code]** — пригодно для переноса геометрии карт WoG;
  ERM таких карт исполняет наш рантайм ERM, а не этот импортёр.

## 4. Что слой данных может дать WoG

| Задача | Через данные? |
|--------|---------------|
| Новые *типы* существ (8-й уровень, нейтралы WoG) как клоны существующих моделей с новыми статами | **Да** — клон юнита, новый id, та же модель (перекраска = замена материала, нужна работа с ассетами) |
| Новые артефакты с бонусами к статам | **Да** — клон предмета, правка `bonuses`; общность ограничена тем, что выражают `bonuses`/баффы |
| Новые баффы (бонусы рангов опыта стеков как стат-баффы) | **Да** — `OverlayBuilder.StatBuff`, `StackExperienceBuffs` |
| Новые объекты карты со своим поведением | **Частично** — клон объекта (+семейство логики); своё поведение — скриптами или плагином |
| Состояние экземпляра (опыт *этого* стека, уровень командира) | **Нет** — только статические определения; нужен рантайм (плагин) |
| Правила, меняющиеся опциями во время игры | **Нет** — нужен плагин |
