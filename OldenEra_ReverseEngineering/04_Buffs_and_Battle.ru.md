[English](04_Buffs_and_Battle.md) | **Русский**

# Olden Era — баффы, статы существ и бой

## 1. Боевые статы существ **[V-data]**

`stats` юнита: `hp, offence, defence, damageMin, damageMax, initiative, speed, luck, moral, actionPoints,
numCounters, energyPerCast, energyPerRound, energyPerTakeDamage`.

Отличия от H3, важные для WoG (видны из самого набора статов):

| Понятие H3/WoG | Olden Era |
|----------------|-----------|
| скорость = и очерёдность хода, и передвижение | **разделено**: `initiative` (очерёдность) + `speed` (передвижение) |
| выстрелы | в `stats` нет стата выстрелов (стрельба — через способности) **[UNVERIFIED: боезапас]** |
| ответные удары (1 или бесконечно) | `numCounters` |
| касты заклинаний существ за бой | система энергии (`energyPer*`, `maxEnergy`, `actionPoints`) |
| мораль/удача | `moral`, `luck` (есть) |

## 2. Баффы (`DB/buffs/*.json`) **[V-data]**

414 баффов в 19 файлах. Поля в реальных данных: `id, name_, description_, icon, duration (бесконечный /
maxDuration / задаётся кастующим), addition (правило наложения), data{stats{…}, outDmgMods, inDmgMods},
actions, mechanics, disablers, immunities[{type: mechanic|effect|magic_level|ability_rank|damage}],
sequenceEffect, statOverrides, vfxList, timeoutActions, mimicStats, activationParams`.

Ключи `data.stats` (все встречаются в реальных баффах):
`offence, defence, offencePerc, defencePerc, initiative, speed, moral, luck, hp, hpPerc, damageMin, damageMax,
damagePerc, inAllDmgMod, outAllDmgMod, alwaysMaxDmg, alwaysMinDmg, alwaysTakeMaxDmg, accumulateDamage,
healthLimitMinPercent, actionPoints, maxAddedApPerRound, maxEnergy, energyPerCast, energyPerRound,
energyPerTakeDamage, blockEnergyRegen, finalAbilityDamageBonusPercent, finalHealingBonusPercent,
finalSummonBonusPercent, numCounters, maxOverwatchStrikes, disableCounterOnCrit, skipActionChanceModifier,
anticritChanceModifier, untargetable, untargetByLowLevel, tauntRadius, ignoreShootingBlock, ignoreShootDmgBuff,
ignoreObstacles, ignoreCastleProtection, armorPen, attackPen, lifetimeBonus, heroOffenceModifier,
heroDefenceModifier, heroSpellPowerModifier, heroIntelligenceModifier`; модификаторы урона по типу
(`normal_damage, melee_attack, shoot_attack, range_attack, counter_attack, magic_damage, …`).

Баффы накладываются: артефактами (`battleSubskillBonus`), действиями сценария `AddBuffHeroDays`,
`RemoveBuffHero`, `AddGlobalBuff` (длительность `Infinite`, дни…), заклинаниями/способностями, наградами объектов.

### Почему баффы важны для WoG

Система баффов — **проверенный, управляемый данными способ менять статы существ в настоящем бою**. Большинство
стат-эффектов WoG ложатся на неё напрямую:

| Эффект WoG | Выражение баффом |
|------------|------------------|
| Опыт стека `A/D/H/m/M/S` с `+`/`-`/`%`/`=` | дельта к `offence`/`defence`/`hp`/`damageMin`/`damageMax`/`speed`(+`initiative`), посчитанная от базы юнита OE (`StackExperienceBuffs`) |
| Опыт стека `R` (ответы) | `numCounters` |
| `fM` / AT+DM командира «всегда максимальный урон» | `alwaysMaxDmg` |
| `fE` без штрафа в ближнем бою / стрельба в упор | `ignoreShootingBlock`, `ignoreShootDmgBuff` |
| `fF` полёт / DF+SP командира… | `ignoreObstacles` (тип передвижения «полёт» — свойство юнита; **[UNVERIFIED]**, может ли его дать бафф) |
| Сопротивление магии командира % | `inDmgMods` с `magic_damage` |
| AT+DF командира «защита цели ×0.5» | `armorPen` (семантику нужно откалибровать) |

Что баффы выразить не могут (нужны хуки плагина): шансовые эффекты (блок 30 %, паралич 50 %), формула
смертельного взгляда, точная регенерация, «заклинание после атаки», страх, флаги существ (`f`), выстрелы (`O`),
касты (`P`). Генератор перечисляет такие бонусы в `NotExpressible`, а не подделывает их.

## 3. Доступные хуки боя

| Хук | Источник | Метка |
|-----|----------|-------|
| До/после боя героя с героем | прерывания сценария | [V-community] |
| Начало/конец боя, начало раунда, ход юнита, до/после действия, расчёт урона | патчи Harmony на методы боя через BepInEx | **[UNVERIFIED]** — имена классов/методов надо найти (план — `07_InGame_RE_Plan.ru.md`) |
| Бои с нейтральными отрядами | `SquadInteraction`, `SquadKill`, `InitiateAttack` в сценарии | [V-community] |

Слой боя порта написан против интерфейса `IBattleAdapter`; реализация для Olden Era включается только после
того, как символы боя найдены и помечены `verified`.
