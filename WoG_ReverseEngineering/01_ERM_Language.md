# ERM language — reverse-engineered specification (WoG 3.58f)

Source of truth: `T1/erm.cpp` from S1 (see `00_Sources.md`). Function names below refer to that file.
This document is what `src/WoG.Erm` implements; each section names the C# type that implements it.

## 1. Script file

* A script is plain text (cp1251). It is only processed if the first non-blank characters are `ZVSE`
  (`CheckERM`). Everything else is free text.
* The parser (`ParseERM`) scans for `!` (`SkipUntil2` with `c='!'`). **Everything that is not part of a
  command is a comment.** There is no comment syntax; `[...]` and `**` are just conventional prose.
  Consequence: a `!` inside a comment starts a command. This is faithfully reproduced.
* After `!` the next character selects the construct:

| Text | Construct | Meaning |
|------|-----------|---------|
| `!?XX…;` | trigger | starts a new trigger section (pre-trigger) |
| `!$XX…;` | post-trigger | trigger fired *after* the native action (only where the trigger table has `post=true`, e.g. `!$OB`, `!$HL`, `!$LE`) |
| `!!XX…:…;` | receiver | command executed when the enclosing trigger fires |
| `!#XX…:…;` | instruction | receiver executed **once, at script load, only for a new game** (`GameWasLoaded==0`) |
| `!@` | post-instruction marker | instructions after it also run when a saved game is loaded (`PostInst=true`) |
| anything else | skipped | |

* Receivers that appear before the first trigger in a file are a fatal parse error for the file
  (`LastAddedTrigger==0 → l_exit`).
* Triggers and receivers are case-sensitive two-letter ids (`Word` read raw from the text, so `HE`≠`he`).
  Lower-case ids are the control-flow receivers: `if`, `el`, `en`, and **[3.59]** `la`, `go`.

C#: `ErmParser`, `ErmScript`, `ErmTriggerSection`, `ErmReceiverCall`.

## 2. Trigger header

`!?ID p1/p2/…[&cond…][|cond…];`

* Parameters are parsed with `GetNumAutoFl` (up to 16, separated by `/`, followed by an optional condition
  block).
* Parameter values of a trigger header are fixed at parse time **except** that variables are allowed and are
  read at parse time (immediate = 0 means the variable reference is stored, but trigger ids are computed
  from the value present at parse time — `InitTrigger` uses `M.n[]`). In practice scripts only use
  constants.
* Each trigger is mapped to a numeric *event id* (`InitTrigger`, `ERM_Triggers[]`); see
  `02_ERM_Triggers.md`. Execution looks up all sections with the same event id.
* Conditions on the trigger are evaluated **when the trigger fires**, not when parsed (`ProcessERM`
  → `CheckFlags(cp->Efl)`).

## 3. Receiver / instruction

`!!ID p1/p2/…[&cond…][|cond…]:CMD params CMD params …;`

* The part before `:` is the receiver's *object selector* (hero number, position x/y/l, function number…).
  The number and meaning of selector parameters depends on the receiver (`InitReciever`,
  `ERM_Addition[].Type`):

| `Type` | Selector shape | Receivers |
|--------|----------------|-----------|
| 0 | exactly one value (may be omitted ⇒ 0?) — see note | `CD`,`MA`,`UN`,`OW`,`BA`,`BF`,`BU`,`BG`,`QW`,`HL`,`CM`,`MM`,`MP`,`AI`,`VC`,`SN`,`MR`,`MF`,`TL`,`LD`,`HD`,`UX`,`CI`,`FC`,`DG` |
| 1 | one value or variable reference (kept as `VarNum`, evaluated at execution) | `VR`,`TM`,`MC`,`BM`,`BH`,`MW`,`CO`,`DL`,`SS` |
| 2 | position `x/y/l` (3), or 1, 2, 4, 5 values | `OB`,`MN`,`SC`,`CH`,`WT`,`KT`,`FR`,`LN`,`ST`,`WG`,`SK`,`SP`,`WM`,`SW`,`MT`,`GD`,`ML`,`DW`,`WH`,`SY`,`GR`,`SR`,`SG`,`UR`,`CA`,`TR`,`PO`,`PM`,`EX`,`EA`,`CB` |
| 3 | exactly one number copied | `FU`,`HO`,`IP` |
| 4 | loop: `DO#/from/to/step` (4 values) | `DO` |
| 5 | exactly 2 values | `HT` |
| inline | handled in `ProcessMes` | `GE`,`LE`,`CE`,`MO`,`AR`,`HE`,`IF` |

  Note: with `Type 0` a receiver with zero selector parameters (`!!UN:…`) still has `Num==1` because
  `GetNumAutoSelf` always returns at least one (empty) parameter whose value is 0.
* Selector values are stored as `VarNum` and **evaluated at execution time** (`GetVarVal(&sp->Par[0])`),
  so `!!HEv5:…` addresses the hero whose number is in `v5` *when the line runs*.
* Unknown receivers are reported once at load and the line is ignored (`_next`), the rest of the file
  continues.
* After `:` follow one or more *commands*. A command is one letter followed by parameters (`ProcessCmd`):
  `GetNumAuto` with `c=1` reads parameters separated by `/` until a character that cannot continue a
  parameter. Then the next command letter follows. Whitespace is skipped. The list ends at `;`.
  Example: `!!HE-1:Ed500Fd1/d1/0/0;` = command `E` (`d500`) then `F` (`d1/d1/0/0`).
* If a command fails, the remaining commands of that receiver line are skipped and an error is reported;
  execution continues with the next receiver line (`ProcessCmd → l_exit`).

## 4. Parameter syntax (`GetNum`)

A parameter is a sequence of prefix characters followed by a value:

| Prefix | Effect |
|--------|--------|
| `?` | **get**: write the current value into the variable that follows (`Check=1`) |
| `d` | **add** (delta): `new = old + value` (`PutVal: *dp = *dp*f + n` with `f=1`; without `d`, `f=0`) |
| `<` `=` `>` (combinations `<=`,`>=`,`<>`) | **check**: compare the current value with the value and store the boolean in **flag 1** (`Apply → ERMFlags[0]`) |
| `c` (inside the number) | add the current game day number to the value (`GetSubNum`, `GetCurDate()`) |

Comparison codes (`GetCmpCode`): bits `<`=1, `=`=2, `>`=4 → `<`=5, `=`=2, `<=`=7, `>`=4, `<>`=3, `>=`=6.

Values:

| Form | Meaning |
|------|---------|
| `123`, `-5`, `+5` | integer constant |
| `f` … `t` | quick variables (15 ints) |
| `v#` | global int `v1…v10000` |
| `w#` | hero int `w1…w200` of the *current w-hero* (`IF:W`) |
| `x#` | function parameter `x1…x16` |
| `y#` | function-local `y1…y100`; `y-1…y-100` trigger-local |
| `z#` | string `z1…z1000` global (512 chars), `z-1…z-10` local (**[3.59]** `z-1…z-20`); `z>1000` = ERT text |
| `e#` | float `e1…e100` function-local, `e-1…e-100` trigger-local |
| `v<var>` | **indirection**: `vy5` = `v[y5]`, `vf` = `v[f]`; any int var may index any var (`IType`) |
| `$name$` | macro (`MC`), resolves to the variable it names |
| `^text^` | string literal (read by the command itself, not by `GetNum`; `GetNum` returns 0 and leaves the cursor on `^`) |

Rules (all from `GetNum` / `Apply`):

* `?` and a comparison cannot be combined (error).
* `?` requires a variable (`?5` is an error: "cannot get flag").
* For **set** syntax the variable's value is read *when the command runs* (immediate=1 in `GetNumAuto`).
* For **get** syntax with `d` (`?d`) the value is ignored; `d` only matters for set.
* `Apply` returns 1 when a get/check happened; many commands then skip their "set" side effect.
* Range checks (`CheckVarIndex`): flags 1…1000, f…t 1…15, v 1…10000, w 1…200, x 1…16, y −100…−1,1…100,
  z −20…−1,1…1000 (+ERT ids), e −100…−1,1…100. Out-of-range ⇒ error, command aborted.

C#: `ErmParam`, `ErmValueRef`, `ErmParamMode` (`Set`, `Add`, `Get`, `Check`), `ErmVariableStore`.

## 5. Conditions (`GetFlags`, `CheckFlags`)

`&a/b/c` — AND list, `|a/b/c` — OR list, both optional, up to 16 items each, AND part first.

An item is either

* a **flag test**: `5` (flag 5 is set), `-5` (flag 5 is not set), flags 1…1000;
* a **comparison**: `<var><op><var-or-number>`, e.g. `v10>=3`, `y-1<>0`, `z2=z3` (strings: only `=`/`<>`,
  case-insensitive compare `StrCmpExt`), floats compare as floats if either side is `e`.

Evaluation (`CheckFlags`) — reproduced exactly:

1. No AND and no OR items ⇒ **true**.
2. AND items present: if **all** are true ⇒ **true**; if any is false ⇒ go to the OR list.
3. OR list: if **any** item is true ⇒ **true**.
4. Otherwise ⇒ **false**. (So `&1|2` = `1 OR 2`; `&1/3|2` = `(1 AND 3) OR 2`.)

Errors inside a condition make it **false**.

## 6. Execution model (`ProcessERM`)

When the game raises event *E* (with a context: current hero, position, player…):

1. Find all trigger sections whose event id = *E*, in **load order** (all scripts in load order; within a
   file, top to bottom).
2. Before the first section runs, local variables are framed (`StoreVars`):
   * function events (`FU`, local functions): `y1…y100`, `e1…e100` start at 0 for this call;
     `z-1…z-10` are **copied** from the caller (backward compatibility), `z-11…z-20` cleared **[3.59]**;
   * other events: `z-1…z-20` cleared.
   The frame is popped after the last section for *E* — locals are shared by all sections of the same
   event in one firing.
3. For each section: flags 999 and 1000 and `v998…v1000` are set from the context
   (`ERMFlags[999]`, `ERMFlags[998]`, `ERMVar2[997..999]` — the arrays are 0-based):
   `flag1000` = current player is human (or `GM_ai` override), `flag999` = current player is the local
   player (`IsThis`), `v998/v999/v1000` = x/y/l of the event position.
4. Evaluate the section's conditions. If false, skip the section.
5. For non-function events, `y-1…y-100` and `e-1…e-100` are zeroed **per section**.
6. Run the receiver lines in order. `if/el/en` and `la/go` are processed here (§7). `FU:E` stops the
   section (and with a parameter jumps back N sections **[3.59]**).
7. Trigger sections can raise other events synchronously (e.g. `FU:P`, `HE:P` moving a hero); this is a
   nested `ProcessERM` with its own locals.

`ProcessCmd` is called per receiver line; `sp->Disabled` (set by `Z` command of `UF`/`EG`/`EL`, legacy)
skips the line.

## 7. Control flow

* `!!if&cond:;` / `!!el&cond:;` (or `!!el:;`) / `!!en:;` (`CheckConditions`, `_IfStruct_`): classic
  structured if/else-if/else. Nested `if` inside a false branch become "ghost" ifs. Unbalanced `en` is an
  error; a missing `en` at the end of a section is an error ("no ENDIF for IF"). 3.58 limited nesting; **[3.59]**
  unlimited.
* `!!DO#/from/to/step:P params;` — loop calling function `#` with `x16` as counter: `for (x16=from;
  x16<=to; x16+=step)`. Parameters behave like `FU:P`; `y`/`e` locals of the called function are kept
  across iterations (framed once, zeroed only at loop start). `=` params are re-read every iteration;
  `?` params are written back after the loop (and after every iteration if any `=` param exists).
* `!!FU#:P params;` — call function `#` (1…30000; **[3.59]** −1…−100 local to the file): `x1…x16` saved,
  set to the parameters (missing ones = 0), function runs, then `x` restored; `?var` parameters receive the
  callee's final `x` values (3.58 return values).
* `!!FU:E;` — exit the current trigger section.
* `!!FU:X#/$` **[3.59]** — inspect how argument # was passed.
* **[3.59]** `!!la#;` label, `!!go#;` goto (0…49) within a section.

## 8. Strings and interpolation (`_Message2ERM`)

Text printed by `IF:M`, set by `VR:S^…^`, etc. undergoes **5 passes** of `%` substitution, so a
substituted value may itself contain codes:

| Code | Value |
|------|-------|
| `%%` | `%` |
| `%V#`, `%Vf`…`%Vt` | int var |
| `%W#`,`%X#`,`%Y#` (`%Y-#`) | int var |
| `%E#` (`%E-#`) | float printed with exactly 3 decimals (`f2a`: `1.5 → 1.500`) |
| `%Z#` (`%Z-#`) | string var / ERT string |
| `%F#` | flag value 0/1 |
| `%Dd`,`%Dw`,`%Dm`,`%Da` | day of week, week, month, absolute day `((m-1)*4+w-1)*7+d` |
| `%Gc` | current player's colour name |
| `%$macro$` | value of macro |
| `%` + other | copied literally |

## 9. Variables — lifetime and persistence

| Kind | Range | Scope | Saved in savegame (`SaveERM`) |
|------|-------|-------|-------------------------------|
| flags | 1…1000 | global | **yes** |
| `f…t` | 15 | global | **yes** |
| `v` | 1…10000 | global | **yes** (+ macro names) |
| `w` | 1…200 × 156 heroes | per hero | **yes** (+ current `ERMW`) |
| `x` | 1…16 | per call | no |
| `y` | 1…100 / −1…−100 | per function call / per section | no |
| `e` | 1…100 / −1…−100 | per function call / per section | **no** |
| `z` | 1…1000 | global | **yes** |
| `z` | −1…−20 | per call / per section | no |
| macros (`MC`) | — | global | **yes** |
| timers (`TM`) | 1…100 | global | **yes** |
| ERM object hints/settings (`ERM_Object`), hero ERM data, quest log, artifact/monster/skill name and setup overrides, WoG options (first half of `PL_WoGOptions`) | — | global | **yes** |

Implementation consequence: the WoG save layer must persist exactly these; `y`/`e`/`x`/local `z` are never
written, which is why a save can never happen inside a trigger section.

## 10. Error behaviour

* Parse errors in a line: the line is reported (with the surrounding text) and skipped; a malformed
  trigger header disables the rest of the file (`ParseERM → l_exit`).
* Runtime errors (`MError`, `MError2`): message with receiver/command name, the receiver line is
  abandoned, execution continues with the next line. The `PL_ERMErrDis` option (904) suppresses the
  dialogs; errors still go to `WOGERMLOG.TXT`.
* Division by zero in `VR:` shows "Sorry. Division by zero :-)" and aborts the command (value unchanged).

## 11. Edge cases reproduced

1. `!!VR:S` on an int var with a `?` param copies *into* the param (`Apply` returns 1 ⇒ no set).
2. Integer arithmetic is 32-bit signed with C semantics (`/` truncates toward zero, `%` sign follows the
   dividend). `&`,`|`,`X` are bitwise on the unsigned representation.
3. `VR:R0/…` adds `Random(0,n)` (inclusive) to the variable; `VR:T` uses a time-seeded generator.
4. `VR:+` on strings concatenates (max 511 chars), `VR:S` truncates to 511.
5. A condition item that starts with a number is always a flag test; `&5>3` is not a comparison. WoG
   leaves the cursor on `>` and mis-parses the rest of the line; our parser reports it as a syntax error.
6. Indexing a variable with an out-of-range index aborts the command.
7. `y-*`/`e-*` are zeroed for every trigger section — values do not leak between two sections of the
   same event; `y1…y100` do leak between sections of the same non-function event (same frame).
8. A function does not get its own `y-1…y-100`/`e-1…e-100`: inside `!?FU` they still address the
   caller's trigger-local frame (only the positive `y`/`e` are framed for functions).
