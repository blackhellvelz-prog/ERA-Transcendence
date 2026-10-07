# ERM language — reverse-engineered specification (WoG 3.58f)

Primary source: `T1/erm.cpp` from S1 (see `00_Sources.md`). Function names below refer to that file.
This is exactly the specification that `src/WoG.Erm` implements; each section names the implementing C# type.

## 1. Script file

* A script is plain text (cp1251). It is processed only if the first non-whitespace characters are `ZVSE`
  (`CheckERM`). Everything else is free text.
* The parser (`ParseERM`) searches for the `!` character (`SkipUntil2` with `c='!'`). **Everything that is not a
  command is a comment.** There is no special comment syntax; `[...]` and `**` are just a convention.
  Consequence: a `!` inside a "comment" starts a command. This is reproduced exactly.
* The character after `!` determines the construct:

| Text | Construct | Meaning |
|------|-----------|---------|
| `!?XX…;` | trigger | starts a new trigger section (pre-trigger) |
| `!$XX…;` | post-trigger | fires *after* the native action (only where the trigger table has `post=true`: `!$OB`, `!$LE`, **[3.59]** `!$HL`) |
| `!!XX…:…;` | receiver | command executed when the enclosing trigger fires |
| `!#XX…:…;` | instruction | receiver executed **once at script load and only for a new game** (`GameWasLoaded==0`) |
| `!@` | post-instruction marker | in a **new** game, parsing of the file **stops** at it; in a **loaded** game, if it is followed by `ZVSE`, the instructions after the marker are executed (`PostInst=true`) |
| anything else | skipped | |

* A receiver before the first trigger in a file is a fatal error for the file (`LastAddedTrigger==0 → l_exit`).
* Trigger and receiver identifiers are two case-sensitive letters (`Word` is read directly from the text,
  so `HE` ≠ `he`). Lowercase ones are the control-flow receivers: `if`, `el`, `en` and **[3.59]** `la`, `go`.
* If fewer than 4 characters remain at the end of the file after `!`, parsing ends (`M.i >= M.m.l-4`).

C#: `ErmParser`, `ErmScript`, `ErmTriggerSection`, `ErmReceiverLine`.

## 2. Trigger header

`!?ID p1/p2/…[&cond…][|cond…];`

* Parameters are parsed by `GetNumAutoFl` (up to 16, separated by `/`, followed by an optional condition block).
* Header parameters are evaluated at parse time with `immed=0`: if a parameter is a variable, the event number
  receives its **index**, not its value (`!?FUv5;` = `!?FU5;`). In practice scripts use constants.
* Each trigger is turned into a numeric *event identifier* (`InitTrigger`, `ERM_Triggers[]`),
  see `02_ERM_Triggers.md`. When the trigger fires, all sections with that id are looked up.
* Trigger conditions are checked **at the moment the trigger fires**, not at parse time (`ProcessERM → CheckFlags`).
* Unknown trigger type → warning, the section is skipped, parsing continues. Invalid
  parameters of a known trigger → error, **the rest of the file is ignored**.

## 3. Receiver / instruction

`!!ID p1/p2/…[&cond…][|cond…]:CMD params CMD params …;`

* The part before `:` is the receiver's *object selector* (hero number, position x/y/l, function number…). The
  number and meaning of the selector parameters depend on the receiver (`InitReciever`, `ERM_Addition[].Type`):

| `Type` | Selector shape | Receivers |
|--------|----------------|-----------|
| 0 | one value | `CD`,`MA`,`UN`,`OW`,`BA`,`BF`,`BU`,`BG`,`QW`,`HL`,`CM`,`MM`,`MP`,`AI`,`VC`,`SN`,`MR`,`MF`,`TL`,`LD`,`HD`,`UX`,`CI`,`FC`,`DG` |
| 1 | one value or a variable reference (stored as `VarNum`, evaluated at execution time) | `VR`,`TM`,`MC`,`BM`,`BH`,`MW`,`CO`,`DL`,`SS` |
| 2 | position `x/y/l` (3) or 1, 2, 4, 5 values | `OB`,`MN`,`SC`,`CH`,`WT`,`KT`,`FR`,`LN`,`ST`,`WG`,`SK`,`SP`,`WM`,`SW`,`MT`,`GD`,`ML`,`DW`,`WH`,`SY`,`GR`,`SR`,`SG`,`UR`,`CA`,`TR`,`PO`,`PM`,`EX`,`EA`,`CB` |
| 3 | exactly one number | `FU`,`HO`,`IP` |
| 4 | loop: `DO#/from/to/step` (4 values) | `DO` |
| 5 | exactly 2 values | `HT` |
| inline | handled in `ProcessMes` | `GE`,`LE`,`CE`,`MO`,`AR`,`HE`,`IF` |

  Note: a receiver without selector parameters (`!!UN:…`) still has `Num==1`, because
  `GetNumAutoSelf` always returns at least one (empty) parameter with the value 0.
* Selector values are stored as `VarNum` and **evaluated at execution time** (`GetVarVal(&sp->Par[0])`),
  so `!!HEv5:…` addresses the hero whose number is in `v5` *at the moment the line executes*.
* Unknown receiver: a message at load time, the line is ignored (the rest of the line becomes plain
  text), parsing of the file continues.
* After `:` come the *commands*. A command is one letter plus parameters (`ProcessCmd`): `GetNumAuto` with `c=1`
  reads `/`-separated parameters up to a character that cannot continue a parameter. Then comes the next
  command letter. Whitespace is skipped. The list ends with `;`.
  Example: `!!HE-1:Ed500Fd1/d1/0/0;` = command `E` (`d500`), then `F` (`d1/d1/0/0`).
* If a command fails, the remaining commands of this line are skipped and a message is shown;
  execution continues with the next line (`ProcessCmd → l_exit`).

## 4. Parameter syntax (`GetNum`)

A parameter is a sequence of prefixes followed by a value:

| Prefix | Action |
|--------|--------|
| `?` | **get**: write the current value into the variable that follows (`Check=1`) |
| `d` | **add** (delta): `new = old + value` (`PutVal: *dp = *dp*f + n`; with `d`, `f=1`; without `d`, `f=0`) |
| `<` `=` `>` (and the combinations `<=`,`>=`,`<>`) | **check**: compare the current value with the parameter value and write the result to **flag 1** (`Apply → ERMFlags[0]`) |
| `c` (inside a number) | add the number of the current game day to the value (`GetSubNum`, `GetCurDate()`) |

Comparison codes (`GetCmpCode`): bits `<`=1, `=`=2, `>`=4 → `<`=5, `=`=2, `<=`=7, `>`=4, `<>`=3, `>=`=6.

Values:

| Form | Meaning |
|------|---------|
| `123`, `-5`, `+5` | integer constant |
| `f` … `t` | quick variables (15 integers) |
| `v#` | global integer `v1…v10000` |
| `w#` | hero variable `w1…w200` of the current *w-hero* (`IF:W`) |
| `x#` | function parameter `x1…x16` |
| `y#` | function local variable `y1…y100`; `y-1…y-100` are trigger local variables |
| `z#` | string `z1…z1000` (512 bytes), local `z-1…z-10` (**[3.59]** `z-1…z-20`); `z>1000` is ERT text |
| `e#` | floating-point `e1…e100` (function-local), `e-1…e-100` (trigger-local) |
| `v<var>` | **indirect addressing**: `vy5` = `v[y5]`, `vf` = `v[f]`; any integer variable can index any other (`IType`) |
| `$name$` | macro (`MC`), replaced by the variable it is bound to |
| `^text^` | string literal (read by the command itself, not by `GetNum`; `GetNum` returns 0 and leaves the cursor on `^`) |

Rules (from `GetNum` / `Apply`):

* `?` and a comparison cannot be combined (error).
* `?` requires a variable (`?5` is an error: "cannot get flag").
* For **set**, the variable's value is read *at the moment the command executes* (`immed=1` in `GetNumAuto`).
* `?z5` is a special case in 3.58: it behaves like `PutVal` with the index 5 as the value.
* `Apply` returns 1 if a get/check took place; many commands then skip their "write".
* Ranges (`CheckVarIndex`): flags 1…1000, f…t 1…15, v 1…10000, w 1…200, x 1…16, y −100…−1,1…100,
  z −20…−1,1…1000 (+ERT ids), e −100…−1,1…100. Out of range ⇒ error, the command is aborted.

C#: `ErmParam`, `ErmVarRef`, `ErmParamMode` (`Set`, `Get`, `Check`, `Add` flag), `WoGVariables`.

## 5. Conditions (`GetFlags`, `CheckFlags`)

`&a/b/c` is an AND list, `|a/b/c` is an OR list; both are optional, up to 16 items, the AND part comes first.

An item is

* a **flag check**: `5` (flag 5 is set), `-5` (flag 5 is cleared), flags 1…1000;
* a **comparison**: `<var><op><var-or-number>`, for example `v10>=3`, `y-1<>0`, `z2=z3` (strings: only
  `=`/`<>`, compared with `StrCmpExt`: word by word, ignoring ASCII case and the number of spaces); if
  either side is `e`, the comparison is floating-point.

Evaluation (`CheckFlags`) — reproduced exactly:

1. Neither an AND nor an OR part ⇒ **true**.
2. AND part present: if **all** items are true ⇒ **true**; if at least one is false ⇒ go to the OR part.
3. OR part: if **at least one** item is true ⇒ **true**.
4. Otherwise ⇒ **false**. (`&1|2` = `1 OR 2`; `&1/3|2` = `(1 AND 3) OR 2`.)

An error inside a condition makes it **false**.

## 6. Execution model (`ProcessERM`)

When the game raises event *E* (with a context: current hero, position, player…):

1. All sections with id *E* are found in **load order** (scripts in load order; within a file,
   top to bottom).
2. Before the first section that executes, a frame of local variables is created (`StoreVars`):
   * function events (`FU` 1…29999, local functions): `y1…y100`, `e1…e100` start at 0;
     `z-1…z-10` are **copied** from the caller (backward compatibility), `z-11…z-20` are cleared **[3.59]**;
   * other events: `z-1…z-20` are cleared.
   The frame is removed after the last section of event *E* — local variables are shared by all sections of
   one firing.
   **Important:** event 30000 is already `TM1`, even though `FU` accepts the number 30000 (a collision in WoG);
   for frames the deciding condition is `Event < 30000`.
3. For each section, flags 999 and 1000 and `v998…v1000` are set from the context (`ERMFlags[999]`,
   `ERMFlags[998]`, `ERMVar2[997..999]` — zero-based arrays): `flag1000` = the current player is human (or
   the `GM_ai` override), `flag999` = the current player is sitting at this PC (`IsThis`),
   `v998/v999/v1000` = x/y/l of the event position.
4. The section's conditions are checked. If they are false, the section is skipped.
5. For non-function events, `y-1…y-100` and `e-1…e-100` are zeroed **for every section**.
6. Lines are executed in order; `if/el/en` and `la/go` are processed here as well (§7). `FU:E` ends the
   section (with a parameter — skip/return by N sections).
7. Sections can synchronously raise other events (`FU:P`, hero movement `HE:P`, etc.) —
   this is a nested `ProcessERM` with its own local variables.

## 7. Control flow

* `!!if&cond:;` / `!!el&cond:;` (or `!!el:;`) / `!!en:;` (`CheckConditions`, `_IfStruct_`): ordinary
  if / else-if / else. Nested `if`s inside a false branch become "ghosts". An extra `en` is an
  error; an unclosed `if` at the end of a section is the error "no ENDIF for IF". In 3.58 the nesting depth is
  limited; **[3.59]** — unlimited.
* `!!DO#/from/to/step:P params;` — a loop that calls function `#`, with the counter in `x16`:
  `for (x16=from; x16<=to; x16+=step)`. Parameters work as in `FU:P`; the called function's local `y`/`e`
  are preserved between iterations (the frame is created once). `=` parameters are re-read on every
  iteration; `?` parameters are written after the loop (and after every iteration if there is an `=` parameter).
* `!!FU#:P params;` — call function `#` (1…30000; **[3.59]** −1…−100 are file-local functions):
  `x1…x16` are saved and filled with the parameters (missing ones = 0), the function executes, `x` are
  restored; `?var` parameters receive the final `x` values of the called function.
* `!!FU:E;` — exit the current section.
* `!!FU:X#/$` **[3.59]** — how argument # was passed.
* **[3.59]** `!!la#;` — label, `!!go#;` — jump (0…49) within a section.

## 8. Strings and substitutions (`_Message2ERM`)

Text (`IF:M`, `VR:S^…^`, etc.) goes through **5 passes** of `%` substitution, so a substituted value
can itself contain codes:

| Code | Value |
|------|-------|
| `%%` | `%` |
| `%V#`, `%Vf`…`%Vt` | integer variable |
| `%W#`,`%X#`,`%Y#` (`%Y-#`) | integer variable |
| `%E#` (`%E-#`) | floating-point, exactly 3 digits after the decimal point (`f2a`: `1.5 → 1.500`) |
| `%Z#` (`%Z-#`) | string variable / ERT string |
| `%F#` | flag value 0/1 |
| `%Dd`,`%Dw`,`%Dm`,`%Da` | day of the week, week, month, absolute day `((m-1)*4+w-1)*7+d` |
| `%Gc` | name of the current player's color |
| `%$macro$` | macro value |
| `%` + anything else | copied as is |

## 9. Variables — lifetime and saving

| Kind | Range | Scope | Written to the saved game (`SaveERM`) |
|------|-------|-------|---------------------------------------|
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
| ERM data of objects (hints) and heroes, the quest log, artifact/monster/skill overrides, WoG options (first half of `PL_WoGOptions`) | — | global | **yes** |

Consequence for the port: the WoG save layer must save exactly this; `x`/`y`/`e`/local `z` are never
written — which is why a save cannot happen in the middle of a trigger section.

## 10. Error behavior

* Line parse error: the line is shown in a message and skipped; a faulty trigger header or a
  receiver outside a trigger disables the rest of the file (`ParseERM → l_exit`).
* Runtime errors (`MError`, `MError2`): a message with the receiver/command name, the rest of the line
  is discarded, execution continues with the next line. The `PL_ERMErrDis` option (904) hides the dialogs;
  errors are still written to `WOGERMLOG.TXT`.
* Division by zero in `VR:` shows "Sorry. Division by zero :-)" and aborts the command (the value does not change).

## 11. Reproduced edge cases

1. `!!VR:S` into an integer variable with a `?` parameter copies *into* the parameter (`Apply` returned 1 ⇒ no write).
2. Integer arithmetic is 32-bit signed and follows C rules (`/` rounds toward zero, the sign of `%` follows the
   dividend). `&`, `|`, `X` are bitwise.
3. `VR:R` adds `Random(0,n)` (inclusive); `VR:T` is a time-seeded generator.
4. `VR:+` for strings is concatenation (at most 511 characters), `VR:S` truncates to 511.
5. A condition item that starts with a number is always a flag check; `&5>3` is not a comparison. WoG leaves the
   cursor on `>` and misparses the rest of the line; our parser reports a syntax error.
6. Indexing a variable with an invalid index aborts the command.
7. `y-*`/`e-*` are zeroed for every section — values do not "leak" between sections of one event;
   `y1…y100` do leak between sections of one non-function event (shared frame).
8. A function has no `y-1…y-100`/`e-1…e-100` of its own: inside `!?FU` they address the caller's
   trigger-local frame (only the positive `y`/`e` are created for functions).
9. **`VR:U` actually checks "the string ends with…", not "contains"**: `Search4Substring`
   compares the trimmed search string with the *entire remainder* of the source string (`StrCmp` — full equality).
   A one-character search string is never found. Reproduced exactly.
10. `StrCmpExt` string comparison is asymmetric: an empty string is not equal to a string of spaces, but in the
    reverse direction they compare as equal.
