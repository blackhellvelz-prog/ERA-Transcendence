# WoG Options — модель по результатам реверс-инжиниринга

Источники: `T1/erm.h` (макросы `PL_*`), `T1/wogsetup.cpp` (диалог опций, загрузка/сохранение), `T1/erm.cpp`
(`ERM_Universal` команда `P`, `SaveERM`, `FindERM`, `ResetWog*`), скрипты 3.58f (S7) — какую опцию читает
каждый скрипт. Реализация — `src/WoG.Core/Options`.

## 1. Хранение

* `int PL_WoGOptions[2][1000]` (`PL_WONUM = 1000`). Строка 0 — действующие опции; строка 1 — состояние
  диалога (из пресета пользователя), используется для *Restore*/*Cancel*.
* В диалоге 8 страниц × 4 группы × до 20 элементов. Каждый элемент привязан к одному индексу опции
  (`InternalVars[стр][группа][элем]`); привязка и тексты берутся из `ZSETUP00.TXT` установки WoG (в S1 его
  нет). Группа радиокнопок хранит номер выбранного элемента в одной опции.
* **Инвертированные опции:** индексы **1…4** хранятся с обратным знаком относительно галочки
  (`if (ind>0 && ind<5) value = !checked`) — это флаги «Нет …» (`PL_TowerStd`, `PL_MLeaveStd`, `PL_NoNPC`,
  `PL_NoTownDem`).
* Зависимости между галочками зашиты в `CheckDepend()` (например, если *[0][2][4]* выключена, пять зависимых
  элементов становятся серыми). В порте — данные (`OptionDependency`, запланировано).
* Сохранение: первая половина `PL_WoGOptions` (строка 0) пишется в каждый сейв (`SaveERM`). Пресеты
  пишутся/читаются в файлы `.dat` (`SaveSetupState`, по умолчанию `WoGSetupEx.dat`) — реализовано в закрытой
  `ZvsLib1.dll`, формат **не подтверждён**.
* **Значения по умолчанию:** при новой игре WoG копирует строку 1 (выбор игрока в диалоге, загруженный из
  пресета) в строку 0 (`ResetWogify`). Жёсткой таблицы умолчаний в движке нет. Поэтому встроенные умолчания
  порта помечены как *допущения* (`DefaultVerified = false`), а при наличии пресета пользователя берутся из него.
* Скрипты читают и пишут опции через `!!UN:P#/$` (любой индекс 0…999). Запись в 3 или 6 сразу
  включает/выключает командиров у всех героев; запись в 0…10 и 900…907 обновляет и «копии для сброса»
  (`PL_OptionReset`, `PL_OptionReset2`), чтобы значение пережило вызовы `ResetWoG*`.

## 2. Опции движка (зашиты в код)

| Индекс | Макрос | Смысл |
|--------|--------|-------|
| 0 | `PL_ExtDwellStd` | стандартное поведение внешних жилищ 8-го уровня |
| 1 | `PL_TowerStd` | (инв.) усиленные башни городов |
| 2 | `PL_MLeaveStd` | (инв.) монстры могут покидать армию |
| 3 | `PL_NoNPC` | (инв.) **командиры включены** |
| 4 | `PL_NoTownDem` | (инв.) снос городов разрешён |
| 5 | `PL_ApplyWoG` | применять WoG к не-WoG картам (уровень вогификации) |
| 6 | `PL_NPC2Hire` | командиров нужно нанимать в городе |
| 7 | `PL_DwellAccum` | жилища накапливают существ |
| 8 | `PL_GuardAccum` | охрана жилищ накапливается |
| 9 | `PL_CentElf` | настройка кентавров/эльфов |
| 10 | `PL_MLeaveStyle` | стиль ухода монстров |
| 900 | `PL_CrExpEnable` | **опыт стеков включён** |
| 901 | `PL_CrExpStyle` | стиль деления опыта 0…3 |
| 902 | `PL_LeaveArt` | оставлять артефакты при гибели |
| 903 | `PL_CheatDis` | читы запрещены |
| 904 | `PL_ERMErrDis` | не показывать диалоги ошибок ERM |
| 905 | `PL_ERMError` | состояние ошибки ERM |
| 906 | `PL_ExpGainDis` | получение опыта стеками выключено |
| 907 | `PL_NewHero` | настройка новых героев |

## 3. Опции скриптов

Каждый скрипт WoG включается своей опцией и обычно читает подопции. В таблице — все индексы опций, которые
читает каждый скрипт 3.58f (`UN:P#`), извлечено из S7:

| Файл скрипта (3.58f) | Опции, которые он читает через UN:P |
|---|---|
| 1 wog - cheat menu | 77 903 904 905 |
| 2 wog - commander sanctuary | 3 76 |
| 3 wog - secondary skill text | 23 35 58 71 75 102 103 190 191 201 202 203 204 205 206 207 208 209 210 211 212 213 214 215 216 217 218 |
| 4 wog - summon elementals | 74 |
| 5 wog - enhanced war machines 3 | 73 |
| 6 wog - random hero | 72 77 904 905 |
| 7 wog - enhanced artifacts | 71 219 |
| 8 wog - death chamber | 70 |
| 9 wog - custom alliances | 69 |
| 10 wog - new battlefields | 68 |
| 11 wog - neutral town | 50 67 133 |
| 12 wog - commander witch huts | 66 |
| 13 wog - monolith costs | 65 |
| 14 wog - tobyn's scripts | 36 54 55 75 188 189 190 191 192 193 194 201 203 204 |
| 15 wog - passable terrain | 63 |
| 16 wog - split decision | 56 62 218 |
| 17 wog - protection from the elements | 61 |
| 18 wog - forgotten shrine | 60 |
| 19 wog - piercing shot | 59 |
| 20 wog - espionage | 58 |
| 21 wog - neutral units | 53 57 231 232 235 900 |
| 22 wog - metamorphs | 56 |
| 23 wog - enhanced war machines 2 | 55 900 |
| 24 wog - enhanced war machines 1 | 54 |
| 25 wog - dungeon of the dragonmaster | 53 143 234 900 |
| 26 wog - mirror of the home-way | 52 |
| 27 wog - enhanced commanders | 3 51 186 |
| 28 wog - enhanced monsters | 50 900 |
| 29 wog - henchmen | 49 218 |
| 30 wog - enhanced secondary skills | 201 202 203 204 205 206 207 208 209 210 211 212 213 214 |
| 31 wog - creature relationships | 47 |
| 32 wog - berserker flies | 46 900 |
| 33 wog - castle upgrading | 45 |
| 34 wog - emerald tower | 44 900 |
| 35 wog - obelisk runes | 43 |
| 36 wog - garrisons | 42 |
| 37 wog - battle extender | 41 |
| 38 wog - first money | 40 |
| 39 wog - hero specialization boost | 3 39 67 198 |
| 40 wog - karmic battles | 38 |
| 41 wog - rebalanced factions | 35 37 39 50 67 103 188 189 191 198 199 202 203 205 207 210 216 |
| 42 wog - mithril enhancements | 36 149 170 171 |
| 43 wog - mysticism skill enhancement | 35 |
| 44 wog - cards of prophecy | 34 207 233 |
| 45 wog - living scrolls | 33 |
| 46 wog - summoning stones | 32 |
| 47 wog - treasure chest 2 | 31 33 |
| 48 wog - adventure cave | 30 |
| 49 wog - chest | 29 |
| 50 wog - school of wizardry | 28 |
| 51 wog - spell book | 27 |
| 52 wog - artificer | 26 71 102 159 160 161 162 164 167 168 |
| 53 wog - map options | 22 23 25 33 34 37 51 67 100 105 106 131 144 145 146 147 148 150 151 152 153 154 155 156 157 158 159 160 161 162 163 164 166 167 168 169 173 174 175 176 177 178 179 180 182 183 184 185 186 187 193 220 221 222 223 226 227 228 233 234 236 237 238 240 241 243 244 246 247 |
| 54 wog - enhanced dwelling hint text | 24 |
| 55 wog - sorcery skill enhancement | 23 33 36 |
| 56 wog - monster mutterings | 22 |
| 57 wog - freelancers guild | 21 |
| 58 wog - week of monsters | 20 134 135 136 200 234 |
| 59 wog - masters of life | 19 67 |
| 60 wog - alms house | 18 |
| 61 wog - potion fountains | 17 |
| 62 wog - battle academy | 16 |
| 63 wog - mysterious creature dwelling | 15 |
| 64 wog - altar of transformation | 14 67 |
| 65 wog - tavern gambling game | 13 |
| 66 wog - living skull | 12 |
| 67 wog - palace of dreams | 11 900 |
| 68 wog - magic mushrooms | 110 |
| 69 wog - market of time | 109 193 |
| 70 wog - junk merchant | 108 |
| 71 wog - fishing well | 3 107 |
| 72 wog - hourglass of asmodeus | 56 106 |
| 73 wog - bank | 105 181 225 |
| 74 wog - arcane tower | 104 |
| 75 wog - secondary skills boost | 103 215 216 217 218 |
| 76 wog - artifact boost | 102 |
| 77 wog - map rules | 63 67 101 119 193 230 |
| 78 wog - wogify | 3 11 12 13 14 15 16 17 18 21 26 27 28 29 30 31 32 44 52 60 63 70 76 104 107 108 109 110 132 133 137 138 139 140 141 142 143 165 176 177 195 196 219 226 227 229 234 236 237 238 241 242 243 245 248 900 901 |

Закономерность: опции **11…77** — выключатели отдельных скриптов (первое число в строке у не-Wogify
скриптов), **100…249** — подопции (опции карты, запрет заклинаний/артефактов, улучшенные вторичные навыки
201…214 …), **900…907** — опции движка выше.

## 4. Требования к порту

1. Опции — **отдельные значения**, никогда не сводятся в один переключатель: порт хранит все 1000 индексов
   (`WoGOptions` = `int[1000]` + метаданные).
2. Умолчания берутся из `ZSETUP00.TXT` / пресета `.dat` пользователя, если есть; иначе — из
   `Compatibility/options-defaults.json` (наши документированные умолчания, каждое помечено «проверено» или
   «допущение»).
3. Опции сохраняются вместе с игрой (строка 0) и восстанавливаются при загрузке **до** любого `!?GM0`.
4. Инвертированные индексы 1…4 хранят исходную полярность (скрипты проверяют хранимое значение).
5. Побочные эффекты `UN:P` для 3/6 воспроизведены в `UnReceiver` + `CommanderService.ApplyOptions`.
