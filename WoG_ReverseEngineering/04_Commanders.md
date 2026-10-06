# Commanders (WoG 3.58) — reverse-engineered model

Source: `T1/npc.cpp` (class `NPC`, `ERM_NPC`, `SetMonInitPars`, `ApplyCmdMonChanges`,
`GetNPCMagicPower`, `NPC_Resist`), cross-checked with VCMI `config/commanders.json` (S6).
Implemented by `src/WoG.Commanders` (`CommanderRules`, `CommanderProgression`, `CommanderCombatProfile`).

## 1. Identity

* One commander slot per hero (`NPCs[HERNUM]`, HERNUM = 156). Commander number = hero number.
* Two extra "additional" commanders `NPCsa[0/1]` exist for battles without a commander-owning hero
  (attacker/defender side), addressed by `CO-3`/`CO-4`.
* `Type` (0…8) = commander class = hero class / 2, i.e. the hero's faction:
  0 Castle, 1 Rampart, 2 Tower, 3 Inferno, 4 Necropolis, 5 Dungeon, 6 Stronghold, 7 Fortress, 8 Conflux.
  `HType` (0…17) = the owning hero's class (used for AI skill choice).
* Flags: `Used` (hired/enabled), `Dead`, `Fl.CustomPrimary` (scripts own HP/damage; level-up stops
  recomputing them).
* Battle creature types: 174…182 (side 0) and 183…191 (side 1), `= 174 + 9*side + Type`.

## 2. Base stats (`NPC::Init`)

| Index | Stat | Start |
|-------|------|-------|
| 0 | Attack (AT) | 5 |
| 1 | Defence (DF) | 5 |
| 2 | Hit points (HP) | 40 |
| 3 | Damage (DM, max) | 12 |
| 4 | Magic power (MP) | 1 |
| 5 | Speed (SP) | 4 |
| 6 | Magic resistance % (MR) | 5 |

Exp = 0, Level = 0 (displayed as level 1), no skills, no bonuses, no artifacts.

## 3. Levels

* Experience table `NPC::Levels[]` (index = internal level, value = total exp needed), 75 entries,
  max internal level **74**: `0, 1000, 2000, 3200, 4600, 6200, 8000, 10000, 12200, 14700, 17500, 20600,
  24320, 28784, 34140, 40567, 48279, 57533, 68637, 81961, 97949, …, 1810036464`
  (full table in `src/WoG.Commanders/CommanderTables.cs`). From level 12 on each step ≈ ×1.2.
* Exp gain (`NPC::AddExp(NewHeroExp)`): the commander receives the **delta of its hero's experience**
  since the last update (`DelExp = NewExp - OldHeroExp`); class 0 (Castle) gets **150 %**.
* On each level gained (while `NExp ≥ Levels[Level+1]`, cap 74):
  * unless `CustomPrimary`: `HP = 40 + 20·Level`, `DM = 12 + 4·Level` (internal level, after increment);
  * a level-up choice: one secondary-skill step **or** one special bonus (see §4–5). AI / auto: first
    available special bonus if any; otherwise a weighted random skill using `AISkillsChance[HType]`.
    Human: dialog (`ShowNPC`) offering the available choices.
* Class-specific gold on exp (`AddExp` tail): for `Type == 5` after a battle (`LastExpoInBattle`) the
  owner receives `DelExp * 50 / 100` gold. **Discrepancy:** the design comment in the same file says
  "Inferno 3: gives 25 % of exp in gold". The port follows the code (type 5, 50 %) and records the
  discrepancy in the compatibility notes.

## 4. Secondary skills (`Skills[7]`, levels 0…5)

Six choosable skills — AT, DF, HP, DM, MP, SP. Choosing MP also raises the 7th, MR, by the same step.

Gating (`MayNextSkill`):

| Current level | May advance if |
|---------------|----------------|
| 0 (none) | fewer than 4 skills learned |
| 1 → 2 | ≥ 2 skills learned |
| 2 → 3 | ≥ 3 skills learned |
| 3 → 4 | ≥ 4 skills learned |
| 4 → 5 | ≥ 4 skills learned **and** ≥ 4 skills at level ≥ 2 |
| 5 | never |

Bonus by skill level (`NPC::Bonus[7][5]`; HP and DM are **percent**):

| Skill | L1 | L2 | L3 | L4 | L5 |
|-------|----|----|----|----|----|
| AT + | 2 | 5 | 9 | 15 | 25 |
| DF + | 4 | 10 | 18 | 30 | 50 |
| HP +% | 10 | 25 | 45 | 70 | 100 |
| DM +% | 10 | 25 | 45 | 70 | 100 |
| MP + | 1 | 3 | 6 | 14 | 29 |
| SP + | 1 | 2 | 3 | 4 | 6 |
| MR +% | 5 | 15 | 35 | 60 | 90 |

Effective stat (`CalcSkill(i)`): `val = Primary[i]`; add skill bonus (percent of `val` for HP/DM, flat
otherwise); then for each of up to 10 artifact slots add the artifact bonus (percent for HP/DM).
The "super ring" (art 155) instead grants the level-2 bonus once if the skill is ≤ 2.
(`SpecBonus[][]` doubles exist in code but are disabled — `spec` is always 0.)

## 5. Special bonuses (`SpecBon[0]` = owned mask, `SpecBon[1]` = forbidden mask)

Available when **both** paired skills are ≥ 4 (`GetAvailableSpecBon`), taken in bit order:

| Bit | Pair | Effect in battle — taken from the battle code that tests the bit |
|-----|------|-------------------------------------------------------------------|
| 0 | AT+DF | target's defence halved: `NPCReduceDefence → Defence*50/100` |
| 1 | AT+HP | fearsome (`HasNPCFear`) |
| 2 | AT+DM | always maximum damage (`DamageL = DamageH`) |
| 3 | AT+MP | no enemy retaliation (monster flag `0x10000`) |
| 4 | AT+SP | can shoot (monster flags `0x1004`) |
| 5 | DF+HP | unlimited retaliation (`AddMagic2NPC`) |
| 6 | DF+DM | attacks all adjacent (monster flag `0x80000`) |
| 7 | DF+MP | permanent fire shield (`AddMagic2NPC`) |
| 8 | DF+SP | 30 % chance to block an attack completely (`CommanderBlock`) |
| 9 | HP+DM | attacks twice (monster flag `0x8000`) |
| 10 | HP+MP | melee: 50 % chance to paralyse (`NPC_Paralize`) |
| 11 | HP+SP | regeneration (`CanNPCRegenerate`) |
| 12 | DM+MP | death stare: kills `(Level+1) / (targetLevel)` creatures (`NPCDeathStare`, target level = `SubGroup+1`) |
| 13 | DM+SP | champion distance bonus for melee (`NPCChampion`) |
| 14 | MP+SP | flying (monster flag `0x2`) |

The bit → pair mapping is from the `#define AT_DF … MP_SP` masks; the effects are from the functions
that test each mask and from `NPC::ToHint` (the in-game hint). **The large design comment block in
`npc.cpp` (near `NPC2Castle`) is outdated** — e.g. it lists AT+HP as "attack twice" and MP+SP as
"summon stack"; the code does not. The hint text claims "reduce defence by 80 %"; the code uses 50 %.
The port follows the code. VCMI (S6) agrees with the code on every pair.

## 6. Class abilities (code, not just text)

| Type | Faction | Spell cast after attack (`SetMonInitAfter`, monster spell slot `0x4E0`) | Other |
|------|---------|----------------------------------------------------------------------|-------|
| 0 | Castle | Cure (37) | exp ×1.5 |
| 1 | Rampart | Shield (27) | (First Aid Tent stack — code disabled) |
| 2 | Tower | Precision (44) | |
| 3 | Inferno | Fire Shield (29) | |
| 4 | Necropolis | Animate Dead (39) (additional commander: Haste 53) | undead |
| 5 | Dungeon | Bloodlust (43) | gold 50 % of exp (code) |
| 6 | Stronghold | Stone Skin (46) | controls ballista (`NPCBalistaControl`) |
| 7 | Fortress | Haste (53) | hero's AT/DF contribution +50 %: `v += (v - CalcSkill)·50/100` |
| 8 | Conflux | Counterstrike (58) | |

Number of casts per battle = `Skills[MP] + 1`. Magic power used by spells = `CalcSkill(MP)`; for
creature types 178/187 (Necropolis) it is divided by 4 (min 1) — "3.58 reduction".
Magic resistance: incoming spell damage × `(100 − CalcSkill(MR)) / 100` (`NPC_Resist`).

## 7. Battle stats (`SetMonInitPars`)

`Attack = CalcSkill(0)`, `Defence = CalcSkill(1)`, `HP = CalcSkill(2)`, `DamageMax = CalcSkill(3)`,
`DamageMin = AT+DM bonus ? DamageMax : DamageMax/2`, `Speed = CalcSkill(5)`, casts = `MPS+1`.
The hero's primary skills are then applied by the native battle code (as for any creature),
which is why class 7 boosts the *difference*.

## 8. Artifacts (146…155)

Six usable slots (`Arts[10][8]`, slots 0…5 via ERM), each with a "battles won" counter:

| Art | Bonus (`ArtCalcSkill`) |
|-----|------------------------|
| 146 | AT +5, +1 per 6 battles |
| 147 | HP +12 % +1 % per battle |
| 148 | DM +12 % +1 % per battle |
| 149 | (none yet) |
| 150 | MP +1, +1 per 10 battles |
| 151 | SP +1, +1 per 10 battles |
| 152 | ≥ 5 battles: can shoot (`SetMonInitPars`) |
| 153 | flag `0x80000008` (special) |
| 154 | DF +5, +1 per 6 battles |
| 155 | super ring: level-2 bonus to every skill ≤ 2 |

## 9. ERM interface (`ERM_NPC`) — exact semantics

| Cmd | Syntax | Semantics | Faithful quirks |
|-----|--------|-----------|-----------------|
| `E` | `E$` | enabled (`Used`) | `CO-2` set-only |
| `D` | `D$` | dead | |
| `T` | `T$` | class 0…8 (clamped) | |
| `H` | `H$` | hero class 0…17 (clamped) | |
| `P` | `P$` / `P#/$` | custom-primary flag / primary stat # (0…6) | |
| `S` | `S#/$` | skill level # (0…6) | |
| `A` | `A1/art/won`, `A2/art`, `A3/slot/$art/$won`, `A4/…12` | artifacts, result in `v1` | **bug:** no `break` after `A` — execution falls into `N` and copies `z<p1>` into the name. Reproduced only with `ErmCompat.ReproduceKnownBugs` |
| `N` | `N$` | name via z-var (31 chars) | |
| `X` | `X0/$` old hero exp, `X1/$` exp, `X2/$` level (1-based) | | |
| `B` | `B0/$mask`, `B1/#/$`, `B2/$mask`, `B3/#/$` | owned/forbidden special bonuses | **bug:** single-commander `B3` writes the bit into the *owned* mask; `CO-2:B1/#/0` uses an uninitialised mask. Reproduced only with `ReproduceKnownBugs` |

Triggers: `!?CO0` before opening the dialog, `!?CO1` after closing, `!?CO2` after buying, `!?CO3` after
reviving.

## 10. Hiring / death / persistence

* WoG option 3 (`PL_NoNPC`) disables commanders; option 6 (`PL_NPC2Hire`) makes them hireable in
  town instead of present from the start (`EnableNPC(-1, !PL_NPC2Hire)`).
* Town visit (`NPC2Castle`, the hero's own town):
  * no commander (`Used==0`): hire for **1000 gold** → `Init`, enable, `!?CO2`;
  * dead commander: requires mage guild level ≥ 1 / 2 / 3 when commander level ≥ 10 / 20 / 30
    (internal level), price `(L·L + L%2) · 50` gold with `L` = internal level; → enable, `!?CO3`.
  * `Used < 0` = forbidden for this hero.
  (VCMI uses a flat 1500 gold — a VCMI simplification, not WoG behaviour.)
* `SaveNPC`/`LoadNPC` write the whole `NPCs[]` array: the port persists every field above.
