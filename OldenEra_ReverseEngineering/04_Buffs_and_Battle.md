**English** | [Русский](04_Buffs_and_Battle.ru.md)

# Olden Era — buffs, creature stats and battle

## 1. Creature battle stats **[V-data]**

Unit `stats`: `hp, offence, defence, damageMin, damageMax, initiative, speed, luck, moral, actionPoints,
numCounters, energyPerCast, energyPerRound, energyPerTakeDamage`.

Differences from H3 that matter for WoG (visible from the stat set itself):

| H3/WoG concept | Olden Era |
|----------------|-----------|
| speed = both turn order and movement | **split**: `initiative` (turn order) + `speed` (movement) |
| shots | `stats` has no shots stat (shooting goes through abilities) **[UNVERIFIED: ammo]** |
| retaliations (1 or unlimited) | `numCounters` |
| creature spell casts per battle | energy system (`energyPer*`, `maxEnergy`, `actionPoints`) |
| morale/luck | `moral`, `luck` (present) |

## 2. Buffs (`DB/buffs/*.json`) **[V-data]**

414 buffs in 19 files. Fields in real data: `id, name_, description_, icon, duration (infinite /
maxDuration / caster-defined), addition (stacking rule), data{stats{…}, outDmgMods, inDmgMods},
actions, mechanics, disablers, immunities[{type: mechanic|effect|magic_level|ability_rank|damage}],
sequenceEffect, statOverrides, vfxList, timeoutActions, mimicStats, activationParams`.

`data.stats` keys (all occur in real buffs):
`offence, defence, offencePerc, defencePerc, initiative, speed, moral, luck, hp, hpPerc, damageMin, damageMax,
damagePerc, inAllDmgMod, outAllDmgMod, alwaysMaxDmg, alwaysMinDmg, alwaysTakeMaxDmg, accumulateDamage,
healthLimitMinPercent, actionPoints, maxAddedApPerRound, maxEnergy, energyPerCast, energyPerRound,
energyPerTakeDamage, blockEnergyRegen, finalAbilityDamageBonusPercent, finalHealingBonusPercent,
finalSummonBonusPercent, numCounters, maxOverwatchStrikes, disableCounterOnCrit, skipActionChanceModifier,
anticritChanceModifier, untargetable, untargetByLowLevel, tauntRadius, ignoreShootingBlock, ignoreShootDmgBuff,
ignoreObstacles, ignoreCastleProtection, armorPen, attackPen, lifetimeBonus, heroOffenceModifier,
heroDefenceModifier, heroSpellPowerModifier, heroIntelligenceModifier`; damage modifiers by type
(`normal_damage, melee_attack, shoot_attack, range_attack, counter_attack, magic_damage, …`).

Buffs are applied by: artifacts (`battleSubskillBonus`), scenario actions `AddBuffHeroDays`,
`RemoveBuffHero`, `AddGlobalBuff` (duration `Infinite`, days…), spells/abilities, object rewards.

### Why buffs matter for WoG

The buff system is the **verified, data-driven way to change creature stats in real battle**. Most
WoG stat effects map onto it directly:

| WoG effect | Buff expression |
|------------|-----------------|
| Stack experience `A/D/H/m/M/S` with `+`/`-`/`%`/`=` | delta to `offence`/`defence`/`hp`/`damageMin`/`damageMax`/`speed`(+`initiative`), computed from the OE unit base (`StackExperienceBuffs`) |
| Stack experience `R` (retaliations) | `numCounters` |
| `fM` / commander AT+DM "always maximum damage" | `alwaysMaxDmg` |
| `fE` no melee penalty / point-blank shooting | `ignoreShootingBlock`, `ignoreShootDmgBuff` |
| `fF` flying / commander DF+SP… | `ignoreObstacles` (the "flying" movement type is a unit property; **[UNVERIFIED]** whether a buff can grant it) |
| Commander magic resistance % | `inDmgMods` with `magic_damage` |
| Commander AT+DF "target defense ×0.5" | `armorPen` (semantics need calibration) |

What buffs cannot express (plugin hooks are needed): chance-based effects (30 % block, 50 % paralysis), the
death stare formula, exact regeneration, "cast a spell after attacking", fear, creature flags (`f`), shots (`O`),
casts (`P`). The generator lists such bonuses in `NotExpressible` instead of faking them.

## 3. Available battle hooks

| Hook | Source | Tag |
|------|--------|-----|
| Before/after hero-vs-hero battle | scenario interruptions | [V-community] |
| Battle start/end, round start, unit turn, before/after action, damage calculation | Harmony patches on battle methods via BepInEx | **[UNVERIFIED]** — class/method names must be found (plan: `07_InGame_RE_Plan.md`) |
| Fights with neutral squads | `SquadInteraction`, `SquadKill`, `InitiateAttack` in the scenario | [V-community] |

The port's battle layer is written against the `IBattleAdapter` interface; the Olden Era implementation is
enabled only after the battle symbols are found and marked `verified`.
