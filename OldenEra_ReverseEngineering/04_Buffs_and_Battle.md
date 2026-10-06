# Olden Era — buffs, creature stats and battle

## 1. Creature battle stats **[V-data]**

`stats` of a unit: `hp, offence, defence, damageMin, damageMax, initiative, speed, luck, moral,
actionPoints, numCounters, energyPerCast, energyPerRound, energyPerTakeDamage`.

Differences from H3 that matter for WoG (from the stat set itself):

| H3/WoG concept | Olden Era |
|----------------|-----------|
| speed = both turn order and movement | **split**: `initiative` (turn order) + `speed` (movement) |
| shots | no `shots` stat in `stats` (ranged handled by abilities) **[UNVERIFIED: ammo]** |
| retaliations (1, or unlimited) | `numCounters` |
| spell casts per battle (creature casters) | energy system (`energyPer*`, `maxEnergy`, `actionPoints`) |
| morale/luck | `moral`, `luck` (present) |

## 2. Buffs (`DB/buffs/*.json`) **[V-data]**

414 buffs in 19 files. Fields seen in real data: `id, name_, description_, icon, duration
(infinite / maxDuration / caster-defined), addition (stacking rule), data{stats{…}, outDmgMods,
inDmgMods}, actions, mechanics, disablers, immunities[{type: mechanic|effect|magic_level|ability_rank|damage}],
sequenceEffect, statOverrides, vfxList, timeoutActions, mimicStats, activationParams`.

`data.stats` keys (all confirmed in real buffs):
`offence, defence, offencePerc, defencePerc, initiative, speed, moral, luck, hp, hpPerc, damageMin,
damageMax, damagePerc, inAllDmgMod, outAllDmgMod, alwaysMaxDmg, alwaysMinDmg, alwaysTakeMaxDmg,
accumulateDamage, healthLimitMinPercent, actionPoints, maxAddedApPerRound, maxEnergy, energyPerCast,
energyPerRound, energyPerTakeDamage, blockEnergyRegen, finalAbilityDamageBonusPercent,
finalHealingBonusPercent, finalSummonBonusPercent, numCounters, maxOverwatchStrikes,
disableCounterOnCrit, skipActionChanceModifier, anticritChanceModifier, untargetable, untargetByLowLevel,
tauntRadius, ignoreShootingBlock, ignoreShootDmgBuff, ignoreObstacles, ignoreCastleProtection, armorPen,
attackPen, lifetimeBonus, heroOffenceModifier, heroDefenceModifier, heroSpellPowerModifier,
heroIntelligenceModifier`; damage modifiers by damage type (`normal_damage, melee_attack, shoot_attack,
range_attack, counter_attack, magic_damage, …`).

Buffs are applied by: artifacts (`battleSubskillBonus`), map script actions `AddBuffHeroDays`,
`RemoveBuffHero`, `AddGlobalBuff` (durations `Infinite`, days…), spells/abilities, object rewards.

### Why buffs matter for WoG

The buff system is the **verified, data-driven way to change creature stats in real combat**. Most WoG
stat effects map onto it directly:

| WoG effect | Buff expression |
|------------|-----------------|
| Stack exp `A/D/H/m/M/S` with `+`/`-`/`=` | `offence`/`defence`/`hp`/`damageMin`/`damageMax`/`speed` (+`initiative`, see compat) |
| `%` modifiers | `offencePerc`, `defencePerc`, `hpPerc`, `damagePerc` |
| Stack exp `R` (retaliations) | `numCounters` |
| `fM`/`AT+DM` "always max damage" | `alwaysMaxDmg` |
| `fE` no melee penalty / shoot adjacent | `ignoreShootingBlock`, `ignoreShootDmgBuff` |
| `fF` fly / commander DF+SP… | `ignoreObstacles` (movement-type flying is a unit property — **[UNVERIFIED]** whether a buff can grant it) |
| Commander MR % | `inDmgMods` with `magic_damage` |
| Commander AT+DF "halve target defence" | `armorPen` (semantics to calibrate) |

What buffs cannot express (needs plugin hooks): per-instance scaling by experience rank (solved by
generating one buff per rank and applying the right one), chance effects (block 30 %, paralyse 50 %),
death stare formula, regeneration of exact HP, "cast spell after attack", fear.

## 3. Battle hooks available

| Hook | Source | Tag |
|------|--------|-----|
| Before/after hero-vs-hero battle | scenario interruptions | [V-community] |
| Battle start/end, round start, unit turn, before/after action, damage calculation | BepInEx Harmony patches on combat methods | **[UNVERIFIED]** — class/method names must be found (plan in `07_InGame_RE_Plan.md`) |
| Neutral squad fights | scenario `SquadInteraction`, `SquadKill`, `InitiateAttack` | [V-community] |

The port's battle layer is written against an interface (`IBattleAdapter`) whose Olden Era
implementation is enabled only after the probe finds the combat methods.
