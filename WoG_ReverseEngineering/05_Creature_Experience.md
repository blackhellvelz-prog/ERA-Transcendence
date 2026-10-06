# Stack (creature) experience — reverse-engineered model

Sources: `T1/crexpo.h`, `T1/crexpo.cpp` (`CrExpo`, `CrExpoSet`, `CrExpMod`, `CrExpBon`,
`ERM_StackExperience` in `erm.cpp`), `wog features.html` §Stack Experience (S4), VCMI WoG mod
`stackExperience/*.json` (S6) as numeric cross-check.
Implemented by `src/WoG.CreatureExperience`.

## 1. Where experience lives

`CrExpoSet::Body[10000]` — a table of records, **one per army slot that has ever gained experience**,
keyed by location:

| Location type | Key |
|---------------|-----|
| `CE_HERO` (1) | hero index + slot |
| `CE_MAP` (2) | map monster x/y/l |
| `CE_TOWN` (3) | town x/y/l + garrison slot |
| `CE_MINE` (4) | mine x/y/l + slot |
| `CE_HORN` (5) | garrison x/y/l + slot |

Record (`CrExpo`): `Expo` = experience **per creature**, `Num` = number of creatures it was computed for,
`MType` = creature type, stack-artifact fields (`mHasArt`, `mArt`, `mSubArt` 0…15, `mCopyArt` 0…3).
The engine re-validates the record against the real slot (`RecalcExp2RealNum`, `Validate`) whenever it
reads it, so external changes to the army do not corrupt it.

Port consequence: experience is **external state keyed by army location**, not a field of the creature.
`WoG.CreatureExperience.StackExperienceStore` reproduces exactly this.

## 2. Per-type parameters (`CrExpMod`, file `CREXPMOD.TXT`)

| Field | Meaning |
|-------|---------|
| `ExpMul` | weight of this creature when sharing battle experience |
| `UpgrMul` | multiplier applied to experience when the stack is upgraded |
| `Limit` | experience needed for rank 10 (scales the rank table) |
| `Cap` | % of `Limit` a stack may gain in **one** battle (`CapIt`) |
| `Lvl11Exp` | extra experience from rank 10 to rank 11 |

Rows with negative ids `-1…-8` are per-creature-level defaults (level = `SubGroup`, 0…6, 7 = other);
explicit creature rows override them.
**Code quirk:** the hard-coded fallback before the file is read sets `CrModTmp2[j][1]` twice (50, then
55580) and never sets index 2; the values are always overwritten by `CREXPMOD.TXT` in a normal install.

## 3. Ranks (`CrExpMod::Ranks`, `GetRank`, `GetRankExp`)

`Ranks = {17500 (scale base), 1000, 1000, 1200, 1400, 1600, 1800, 2000, 2200, 2500, 2800}`.
`scale = Limit / 17500`. Experience needed **for** rank r (cumulative) =
`scale · Σ_{i=1..r} Ranks[i]` (for r ≤ 10), and rank 11 adds `Lvl11Exp`.
`GetRank(exp)`: subtract `Ranks[1..10]` (scaled) until negative → rank 0…10. Ranks are 0…10 internally
(11 is only reachable through `Exp4Level`/`MaxExpo`).
Max experience = `Limit + Lvl11Exp` (`Check4Max` clamps).

## 4. Gaining experience after battle (`CrExpoSet::AddExpo`)

Runs for a **human** hero only ("do not support AI experience yet" — AI uses `EA`/`DaylyAIExperience`),
when option 900 (`PL_CrExpEnable`) is on and 906 (`PL_ExpGainDis`) is off.

Input: hero experience before (`OldHExp`) and after (`NewHExp`) the battle.
For each of the 7 slots with creatures, a weight `W[i]` (option 901 `PL_CrExpStyle`):

| Style | Weight |
|-------|--------|
| 0 (default) | `ExpMul(type)` |
| 1 "Timothy" | `0.9 + count · (7 − level) · ExpMul` |
| 2 "QQD" | `0.9 + count · ((level+1)·100 + currentExp + 10) · ExpMul / 100` |
| 3 "equal" | `count · ExpMul` |

`AllW = Σ W` for styles 1–3 (style 0 leaves `AllW = 0 → 1`, so each stack gets its weight in full).
Gain per creature:
* style 0: `ΔHeroExp · PlayerMult/100 · W[i] / AllW`
* styles 1–3: `ΔHeroExp · PlayerMult/100 · W[i] / AllW / count[i]`
then `CapIt` (≤ `Limit·Cap/100`, at least 1), ×1.5 if the stack artifact sub-type is 5, added to
`Expo`, then clamped to max. Stacks that died are deleted (and a stack artifact is returned to the hero).
`PlayerMult` defaults to 100 (saved).

## 5. Merging and moving stacks (`ApplyExpo` modes, `HComb`, `HMove`)

When N creatures with experience E are added to a stack of `Num` creatures with experience `Exp`:
`Exp = (Exp·Num + E·(N − Num)) / N` (weighted average, integer division) — mode 0/1 of `ApplyExpo`.
Upgrading: `Exp · UpgrMul(type)` (mode 5). Rank-based modes 10–14 convert through `Exp4Level`.
All 15 modes are reproduced (`ExperienceMath.Apply`).

## 6. Bonuses per rank (`CrExpBon`, file `CREXPBON.TXT`)

Up to 20 bonus lines per creature; each line: `Type` char, `Mod` char (or `#n` byte), 11 values
(ranks 0…10). Level defaults (`-1…-8`) fill missing types. Applied at battle start (`Apply`) to the
battle stack, from the **stack's original stats** (stored once per battle in `BFStat`):

| Type | Stat | 
|------|------|
| `A` | attack |
| `D` | defence |
| `H` | hit points (both current-max fields) |
| `m` / `M` | min / max damage |
| `S` | speed |
| `O` | shots |
| `P` | spell casts |
| `R` | number of retaliations (+ native count) |
| `f` + flag letter | creature flag on (value 1) / off (0) / unchanged (2): `F` fly, `S` shoot, `B` breath, `L` alive, `1/2/3` king, `P` mind immunity, `E` no melee penalty, `I` fire immunity, `D` double strike, `R` no retaliation, `M` no morale penalty, `U` undead, `A` attack all around, `G` dragon |

`Mod`: `+` add, `-` subtract, `%` `val += val·p/100 + 0.5` (then truncated), `=` set.
Stack artifact (art 156) sub-types double a bonus and add a constant: sub 0 HP ×2+2, 1 attack ×2+2,
2 defence ×2+2, 3 damage ×2+1, 4 speed ×2+1, 5 +50 % experience gain, 8 +2 retaliations.

Other types handled by dedicated hooks (each is a separate battle rule in the port's battle layer):
blocking (`StackBlock`, `StackBlockPartial`), fear/fearless, dwarf-style resistance (own and friendly),
no distance/obstacle penalty, spell cost reduction, defence bonus when defending, death blow, personal
hate, cast spell on attack (single and mass, before/after hit), cast spell at round start, harpy
return, defence reduction, champion, golem resistance, dispel resistance, regeneration, minotaur
morale, unicorn aura, shoot adjacent, death stare, rebirth.

## 7. ERM (`EX`, `EA`)

`!!EX hero/slot:` or `!!EX x/y/l/slot[/ownerType]:` —
`A$type/$num/$exp`, `T$type`, `N$num`, `E$exp`, `R$rank`, `C…` combine, plus stack-artifact commands.
`HE:C0/slot/$type/$num/$exp[/mode]` also reads/writes experience (mode ≥ 10 → rank instead of exp,
**[3.59]** returns rank when modifier ≥ 10).
`!!EA` controls AI experience parameters (`AIMult`, `AIBase`, `AITMult` per difficulty) and bonus lines.

## 8. Persistence

`CrExpoSet::Save` writes `Body[]` + `PlayerMult` (+ AI tables); `CrExpMod::Save` writes `Body[]` ("CRMD");
`CrExpBon::Save` writes the bonus tables. All three are part of the WoG save layer in the port.
