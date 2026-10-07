# Commanders (WoG 3.58) — Reverse-Engineered Model

Source: `T1/npc.cpp` (class `NPC`, `ERM_NPC`, `SetMonInitPars`, `ApplyCmdMonChanges`, `GetNPCMagicPower`,
`NPC_Resist`, `NPC2Castle`, `ResetNPC`), cross-checked against VCMI `config/commanders.json` (S6).
Implementation — `src/WoG.Commanders` (`CommanderTables`, `CommanderService`).

## 1. Entity

* One commander slot per hero (`NPCs[HERNUM]`, HERNUM = 156). Commander number = hero number.
* Two "extra" commanders `NPCsa[0/1]` — for battles without the hero who owns the commander (attacking/
  defending side), accessible via `CO-3`/`CO-4`.
* `Type` (0…8) — commander class = hero class / 2, i.e. the faction:
  0 Castle, 1 Rampart, 2 Tower, 3 Inferno, 4 Necropolis, 5 Dungeon, 6 Stronghold, 7 Fortress, 8 Conflux.
  `HType` (0…17) — class of the owning hero (weights for automatic skill selection).
* Flags: `Used` (1 — present, 0 — absent/dismissed, −1 — forbidden), `Dead`, `Fl.CustomPrimary` (stats are set by
  scripts; leveling up stops recalculating HP/damage).
* Creature types in battle: 174…182 (side 0) and 183…191 (side 1), `= 174 + 9·side + Type`.

## 2. Base Stats (`NPC::Init`)

| Index | Stat | Start |
|-------|------|-------|
| 0 | Attack (AT) | 5 |
| 1 | Defense (DF) | 5 |
| 2 | Health (HP) | 40 |
| 3 | Damage (DM, maximum) | 12 |
| 4 | Spell power (MP) | 1 |
| 5 | Speed (SP) | 4 |
| 6 | Magic resistance % (MR) | 5 |

Experience = 0, level = 0 (displayed as 1), no skills, bonuses or artifacts.

## 3. Levels

* Experience table `NPC::Levels[]` (index — internal level, value — total experience), 75 values,
  maximum internal level **74**: `0, 1000, 2000, 3200, 4600, 6200, 8000, 10000, 12200, 14700, 17500,
  20600, 24320, 28784, 34140, 40567, 48279, 57533, 68637, 81961, 97949, …, 1810036464` (in full — in
  `CommanderTables.cs`). From level 12 on, each step is ≈ ×1.2.
* Gaining experience (`NPC::AddExp(NewHeroExp)`): the commander receives **the increase in its hero's experience**
  since the previous update (`DelExp = NewExp - OldHeroExp`); class 0 (Castle) receives **150 %**.
* For each level gained (while `NExp ≥ Levels[Level+1]`, capped at 74):
  * unless `CustomPrimary`: `HP = 40 + 20·Level`, `DM = 12 + 4·Level` (internal level after the increment);
  * choice of upgrade: one step of a secondary skill **or** one special bonus (§4–5). AI/automatic: the first
    available special bonus, otherwise a random skill weighted by `AISkillsChance[HType]`. Human: a dialog (`ShowNPC`).
* Class gold (`AddExp`): for `Type == 5`, after a battle (`LastExpoInBattle`) the owner receives
  `DelExp * 50 / 100` gold. **Discrepancy:** a design comment in the same file says "Inferno 3: 25 % of experience
  as gold" («Инферно 3: 25 % опыта в золоте»). The port follows the code (type 5, 50 %) and records the discrepancy.
* On a new game (`ResetNPC`) all commanders are initialized and receive the hero's current experience
  (`AddExp(exp, 0)`), after which they are enabled/disabled according to options 3 and 6.

## 4. Secondary Skills (`Skills[7]`, levels 0…5)

Six selectable skills — AT, DF, HP, DM, MP, SP. Choosing MP also raises the seventh — MR — by the same step.

Restrictions (`MayNextSkill`):

| Current level | Can be raised if |
|---------------|------------------|
| 0 (none) | fewer than 4 skills learned |
| 1 → 2 | ≥ 2 skills learned |
| 2 → 3 | ≥ 3 skills learned |
| 3 → 4 | ≥ 4 skills learned |
| 4 → 5 | ≥ 4 skills learned **and** ≥ 4 skills at level ≥ 2 |
| 5 | never |

Bonus by skill level (`NPC::Bonus[7][5]`; HP and DM are **percentages**):

| Skill | L1 | L2 | L3 | L4 | L5 |
|-------|----|----|----|----|----|
| AT + | 2 | 5 | 9 | 15 | 25 |
| DF + | 4 | 10 | 18 | 30 | 50 |
| HP +% | 10 | 25 | 45 | 70 | 100 |
| DM +% | 10 | 25 | 45 | 70 | 100 |
| MP + | 1 | 3 | 6 | 14 | 29 |
| SP + | 1 | 2 | 3 | 4 | 6 |
| MR +% | 5 | 15 | 35 | 60 | 90 |

Final stat (`CalcSkill(i)`): `val = Primary[i]`; plus the skill bonus (a percentage of `val` for HP/DM, otherwise a
flat addition); then, for each of the 10 artifact slots, the artifact bonus (a percentage for HP/DM). The "super ring"
(art. 155) instead grants the level-2 bonus once if the skill is ≤ 2.
(The `SpecBonus[][]` table exists in the code but is disabled — `spec` is always 0.)

## 5. Special Bonuses (`SpecBon[0]` — obtained, `SpecBon[1]` — forbidden)

A bonus becomes available when **both** skills of its pair are ≥ 4 (`GetAvailableSpecBon`); bonuses are granted in
bit order:

| Bit | Pair | Effect in battle — per the code that checks the bit |
|-----|------|------------------------------------------------------|
| 0 | AT+DF | the target's defense is halved: `NPCReduceDefence → Defence*50/100` |
| 1 | AT+HP | fear (`HasNPCFear`) |
| 2 | AT+DM | always deals maximum damage (`DamageL = DamageH`) |
| 3 | AT+MP | the enemy does not retaliate (monster flag `0x10000`) |
| 4 | AT+SP | shoots (flags `0x1004`) |
| 5 | DF+HP | unlimited retaliation (`AddMagic2NPC`) |
| 6 | DF+DM | attacks everyone around it (flag `0x80000`) |
| 7 | DF+MP | permanent Fire Shield (`AddMagic2NPC`) |
| 8 | DF+SP | 30 % chance to fully block an attack (`CommanderBlock`) |
| 9 | HP+DM | strikes twice (flag `0x8000`) |
| 10 | HP+MP | 50 % chance to paralyze in melee (`NPC_Paralize`) |
| 11 | HP+SP | regeneration (`CanNPCRegenerate`) |
| 12 | DM+MP | death stare: kills `(Level+1) / (target level)` creatures (`NPCDeathStare`, target level = `SubGroup+1`) |
| 13 | DM+SP | champion distance bonus (`NPCChampion`) |
| 14 | MP+SP | flight (flag `0x2`) |

The bit → pair mapping comes from the masks `#define AT_DF … MP_SP`; the effects come from the functions that check
each mask and from `NPC::ToHint` (the in-game hint). **The large design comment in `npc.cpp` (next to `NPC2Castle`)
is outdated** — for example, it lists AT+HP as "strikes twice" and MP+SP as "summon a stack"; the code does something
else. The hint promises "−80 % defense", while the code applies 50 %. The port follows the code. VCMI (S6) matches the
code for every pair.

## 6. Class Features (from the code, not just from the text)

| Type | Faction | Spell after attack (`SetMonInitAfter`, slot `0x4E0`) | Other |
|------|---------|-------------------------------------------------------|-------|
| 0 | Castle | Cure (37) | experience ×1.5 |
| 1 | Rampart | Shield (27) | (First Aid Tent stack — the code is disabled) |
| 2 | Tower | Precision (44) | |
| 3 | Inferno | Fire Shield (29) | |
| 4 | Necropolis | Animate Dead (39) (for an extra commander — Haste 53) | undead |
| 5 | Dungeon | Bloodlust (43) | gold = 50 % of experience (per the code) |
| 6 | Stronghold | Stone Skin (46) | controls the ballista (`NPCBalistaControl`) |
| 7 | Fortress | Haste (53) | hero's contribution to attack/defense +50 %: `v += (v - CalcSkill)·50/100` |
| 8 | Conflux | Counterstrike (58) | |

Number of casts per battle = `Skills[MP] + 1`. Spell power = `CalcSkill(MP)`; for types 178/187 (Necropolis) it is
divided by 4 (minimum 1) — the "3.58 reduction". Resistance: incoming spell damage ×
`(100 − CalcSkill(MR)) / 100` (`NPC_Resist`).

## 7. Battle Stats (`SetMonInitPars`)

`Attack = CalcSkill(0)`, `Defense = CalcSkill(1)`, `HP = CalcSkill(2)`, `DamageMax = CalcSkill(3)`,
`DamageMin = AT+DM ? DamageMax : DamageMax/2`, `Speed = CalcSkill(5)`, casts = `MPS+1`.
The hero's primary skills are then applied by the native battle code (as for any creature) — which is why class 7
amplifies precisely the *difference*.

## 8. Artifacts (146…155)

Six usable slots (`Arts[10][8]`, slots 0…5 from ERM), each with a counter of battles won:

| Art. | Bonus (`ArtCalcSkill`) |
|------|------------------------|
| 146 | AT +5, +1 for every 6 battles |
| 147 | HP +12 % +1 % per battle |
| 148 | DM +12 % +1 % per battle |
| 149 | (nothing yet) |
| 150 | MP +1, +1 for every 10 battles |
| 151 | SP +1, +1 for every 10 battles |
| 152 | with ≥ 5 battles: shoots (`SetMonInitPars`) |
| 153 | flag `0x80000008` (special) |
| 154 | DF +5, +1 for every 6 battles |
| 155 | super ring: level-2 bonus to every skill ≤ 2 |

A free slot is the first one whose artifact number is ≤ 0 (`ArtGetFreeSlot`).

## 9. ERM Interface (`ERM_NPC`) — Exact Semantics

| Command | Syntax | Semantics | Reproduced quirks |
|---------|--------|-----------|-------------------|
| `E` | `E$` | `Used` | write-only for `CO-2` |
| `D` | `D$` | dead | |
| `T` | `T$` | class 0…8 (clamped) | |
| `H` | `H$` | hero class 0…17 (clamped) | |
| `P` | `P$` / `P#/$` | CustomPrimary flag / primary stat # (0…6) | |
| `S` | `S#/$` | skill level # (0…6) | |
| `A` | `A1/art/wins`, `A2/art`, `A3/slot/$art/$wins`, `A4/…12` | artifacts, result in `v1` (0 OK, 1 not a commander artifact, 3 already present, 4 no room) | **bug:** there is no `break` after `A` — execution falls through into `N` and copies `z<p1>` into the name. Reproduced only with `ReproduceKnownBugs` |
| `N` | `N$` | name via a z-variable (31 characters) | |
| `X` | `X0/$` old hero experience, `X1/$` experience, `X2/$` level (starting from 1) | | |
| `B` | `B0/$mask`, `B1/#/$`, `B2/$mask`, `B3/#/$` | obtained/forbidden special bonuses | **bug:** a single `B3` writes the bit into the *obtained* mask; `CO-2:B1/#/0` uses an uninitialized mask. Reproduced only with `ReproduceKnownBugs` |

Triggers: `!?CO0` before the dialog opens, `!?CO1` after it closes, `!?CO2` after a purchase, `!?CO3` after a
resurrection.

## 10. Hiring / Death / Saving

* Option 3 (`PL_NoNPC`) disables commanders: `DisableNPC(-1)` → `Used = −1`, `Dead = 0` for all of them. Otherwise
  `EnableNPC(-1, !PL_NPC2Hire)`: `Used = 1` (or 0 if option 6 requires commanders to be hired), `Dead = 0`.
* A hero visiting their own town (`NPC2Castle`):
  * no commander (`Used==0`): hire for **1000 gold** → `Init`, experience reset, enable, `!?CO2`;
  * the commander is dead: requires a mage guild of level ≥ 1 / 2 / 3 when the commander's level is ≥ 10 / 20 / 30
    (internal level); price `(L·L + L%2) · 50` gold, `L` — internal level; → enable, `!?CO3`;
  * `Used < 0` — a commander is forbidden for this hero.
  (VCMI uses a fixed 1500 gold — a VCMI simplification, not WoG behavior.)
* `SaveNPC`/`LoadNPC` write the whole `NPCs[]` array — the port preserves all of the fields above.
