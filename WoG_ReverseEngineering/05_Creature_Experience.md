**English** | [Русский](05_Creature_Experience.ru.md)

# Stack (Creature) Experience — Reverse-Engineered Model

Sources: `T1/crexpo.h`, `T1/crexpo.cpp` (`CrExpo`, `CrExpoSet`, `CrExpMod`, `CrExpBon`), `ERM_StackExperience`
in `erm.cpp`, the "Stack Experience" section in `wog features.html` (S4), VCMI JSON data `stackExperience/*.json`
(S6) — for cross-checking the numbers. Implementation — `src/WoG.CreatureExperience`.

## 1. Where Experience Is Stored

`CrExpoSet::Body[10000]` — a table of records, **one per army slot that has ever gained experience**,
keyed by location:

| Location type | Key |
|-----------|------|
| `CE_HERO` (1) | hero number + slot |
| `CE_MAP` (2) | monster on the map x/y/l |
| `CE_TOWN` (3) | town x/y/l + garrison slot |
| `CE_MINE` (4) | mine x/y/l + slot |
| `CE_HORN` (5) | garrison x/y/l + slot |

Record (`CrExpo`): `Expo` — experience **per creature**, `Num` — the number of creatures it was computed for,
`MType` — creature type, the stack artifact fields (`mHasArt`, `mArt`, `mSubArt` 0…15, `mCopyArt` 0…3).

Reconciliation with the actual slot (`RecalcExp2RealNum`, ported exactly):
* the creature type changed → experience is reset to zero (exception — the werewolf, type 194: experience is kept);
* the creature count dropped to 0 → experience 0; decreased or stayed the same → only `Num` is updated; increased →
  `Expo = Expo·Num/n` (the new creatures "dilute" the experience), `Num = n`.

Consequence for the port: experience is **external state tied to the army's location**, not a creature field.
`StackExperienceService` reproduces exactly this.

## 2. Per-Type Parameters (`CrExpMod`, file `CREXPMOD.TXT`)

| Field | Meaning |
|------|-------|
| `ExpMul` | the creature's weight when battle experience is split |
| `UpgrMul` | experience multiplier when the stack is upgraded |
| `Limit` | experience needed for rank 10 (scales the rank table) |
| `Cap` | what % of `Limit` a stack can gain in a **single** battle (`CapIt`) |
| `Lvl11Exp` | additional experience from rank 10 to 11 |

Rows with ids `-1…-8` are the defaults for a creature level (level = `SubGroup` 0…6, 7 = others);
explicit creature rows override them.
**Code quirk:** the fallback values set before the file is read assign `CrModTmp2[j][1]` twice (50, then 55580)
and never set index 2; in a normal installation the file overwrites everything.

## 3. Ranks (`CrExpMod::Ranks`, `GetRank`, `GetRankExp`)

`Ranks = {17500 (scale base), 1000, 1000, 1200, 1400, 1600, 1800, 2000, 2200, 2500, 2800}`.
`scale = Limit / 17500`. The experience needed **for** rank r (cumulative) =
`scale · Σ_{i=1..r} Ranks[i]` (for r ≤ 10); rank 11 adds `Lvl11Exp`.
`GetRank(exp)`: subtract `Ranks[1..10]` (scaled) until the result goes negative → rank 0…10.
Maximum experience = `Limit + Lvl11Exp` (`Check4Max` clamps it).

## 4. Post-Battle Experience (`CrExpoSet::AddExpo`)

Only for a **human** player's hero ("AI experience is not supported yet" — the AI has its own mechanism, `EA`/`DaylyAIExperience`),
if option 900 (`PL_CrExpEnable`) is enabled and 906 (`PL_ExpGainDis`) is disabled.

Input: the hero's experience before (`OldHExp`) and after (`NewHExp`) the battle. For each of the 7 slots holding creatures — a weight `W[i]`
(option 901 `PL_CrExpStyle`):

| Style | Weight |
|-------|-----|
| 0 (default) | `ExpMul(type)` |
| 1 "Timothy" | `0.9 + count · (7 − level) · ExpMul` |
| 2 "QQD" | `0.9 + count · ((level+1)·100 + currentExp + 10) · ExpMul / 100` |
| 3 "evenly" | `count · ExpMul` |

`AllW = Σ W` for styles 1–3 (in style 0 `AllW` stays 0 → 1, so each stack receives its full weight).
Gain per creature:
* style 0: `(int)(ΔHeroExp) · PlayerMult/100 · W[i] / AllW` (the difference is cast to int first — the order of
  operations is as in the code);
* styles 1–3: `ΔHeroExp · PlayerMult/100 · W[i] / AllW / count[i]`;
then `CapIt` (≤ `Limit·Cap/100`, minimum 1), ×1.5 if the stack artifact subtype = 5, the gain is added to `Expo`, and the result is clamped to
the maximum. Dead stacks are removed (the stack artifact is returned to the hero). `PlayerMult` defaults to 100
(it is saved).

## 5. Merging and Moving Stacks (`ApplyExpo`, `HComb`, `HMove` modes)

When creatures with experience E are added to a stack of `Num` creatures with experience `Exp`, bringing the total to N:
`Exp = (Exp·Num + E·(N − Num)) / N` (weighted average, integer division) — `ApplyExpo` modes 0/1.
Upgrade: `Exp · UpgrMul(type) + E` (mode 5). Modes 10–14 work through ranks (`Exp4Level`).
All 15 modes are reproduced (`ExperienceMath.Apply`).

## 6. Rank Bonuses (`CrExpBon`, file `CREXPBON.TXT`)

Up to 20 bonus rows per creature; a row: a `Type` character, a `Mod` character (or a `#n` byte), 11 values (ranks
0…10). The level defaults (`-1…-8`) fill in the missing types; explicit `A D H m M S` rows replace the row
of the same type. They are applied at the start of a battle (`Apply`) to the stack in the battle, based on the **stack's original stats** (stored
once per battle in `BFStat`):

| Type | Stat |
|-----|------|
| `A` | attack |
| `D` | defense |
| `H` | health (both fields) |
| `m` / `M` | damage min / max |
| `S` | speed |
| `O` | shots |
| `P` | spell casts |
| `R` | number of retaliations (+ native) |
| `f` + flag letter | creature flag on (value 1) / off (0) / unchanged (2): `F` flying, `S` shooting, `B` breath attack, `L` living, `1/2/3` king, `P` mind immunity, `E` no melee penalty, `I` fire immunity, `D` double strike, `R` no enemy retaliation, `M` no morale penalty, `U` undead, `A` attacks all around, `G` dragon |

`Mod`: `+` add, `-` subtract, `%` `val += val·p/100 + 0.5` (then the fractional part is discarded),
`=` assign. The stack artifact (artifact 156) doubles the bonus depending on its subtype and adds a constant: subtype 0 HP ×2+2,
1 attack ×2+2, 2 defense ×2+2, 3 damage ×2+1, 4 speed ×2+1, 5 +50 % experience gained, 8 +2 retaliations.
When the tables are loaded, the `fS` flag (shooting) also changes the creature type's flag in `MonTable`.

Other types are handled by separate battle hooks (in the port — separate rules of the battle layer): block
(`StackBlock`, `StackBlockPartial`), fear/fearlessness, "dwarf" resistance (own and to friendly magic), no
distance/obstacle penalty, reduced spell cost, defense bonus when defending, death blow, personal
hatred, spell on attack (single-target and mass, before/after the strike), spell at the start of a round, harpy-style
return, defense reduction, champion, golem resistance, dispel resistance, regeneration, minotaur
morale, unicorn aura, point-blank shooting, death stare, rebirth.

## 7. ERM (`EX`, `EA`)

`!!EX hero/slot:` (hero −1 = current) or `!!EX x/y/l/slot[/ownerType]:` —
`E$` experience per creature · `N$` count · `T$` type · `A$type/$count/$exp` · `R$art/$subtype` or
`R$has/$art/$subtype/$copies` (stack artifact) · `C…` merging with another stack (result in `v1`).
A record is created only when a value is set (reading a nonexistent record returns the slot's values).
`HE:C0/slot/$type/$count/$exp[/mode]` also reads/writes experience (mode ≥ 10 → rank instead of experience;
**[3.59]** returns the rank when the modifier is ≥ 10).
`!!EA` controls the AI experience parameters (`AIMult`, `AIBase`, `AITMult` per difficulty level) and the bonus rows.

## 8. Saving

`CrExpoSet::Save` writes `Body[]` + `PlayerMult` (+ the AI tables); `CrExpMod::Save` — `Body[]` ("CRMD");
`CrExpBon::Save` — the bonus tables. All three are part of the WoG save layer in the port.
