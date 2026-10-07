# ERM 2.0 Preprocessor (Era `PreprocessErm`)

Source: `Erm.pas`, function `PreprocessErm` (Era 3.9.31), scanner `TextScan.TTextScanner` from the B2 library.
Port: `src/WoG.Erm/Era/EraPreprocessor.cs`. Ported **line by line, bugs included**: the same input text
yields the same result. Tests: `EraPreprocessorTests`.

## Place in the pipeline

ERA does not parse "ERM 2.0" directly. On load, every script goes through text substitution, and the output is
classic ERM, which is then parsed by the WoG compiler (with its parameter-parsing functions hooked by Era).
The port works the same way: `EraPreprocessor.Process` → `ErmParser` (`Era` dialect) → runtime.

## What gets replaced

| Construct | Where it applies | What it becomes |
|-----------|------------------|-----------------|
| `(FunctionName)` | in all scripts | function number: ERA event names have their own numbers (`OnEveryDay` = 77018…), all others get numbers starting at 95000, in order of first appearance |
| `(name:y)`, `(name:x)`, `(name:z)`, `(name:e)` | `ZVSE2` only | local variable declaration; yields `y5`, `x3`, `z-1`, etc. |
| `(name)` | `ZVSE2` only | an already declared local variable |
| `(array[N]:y)` | `ZVSE2` only | declaration of an array of N consecutive variables |
| `(array[2])`, `(array[-1])` | `ZVSE2` only | an element (a negative index counts from the end) |
| `(array[i])`, `(array[variable])` | `ZVSE2` only | `!!VRyT:S<start> +<index> F<start>/<end>/0/0;` is inserted before the command, and the reference itself becomes `yyT` |
| `(@name)` | `ZVSE2` only | the variable's number (address) instead of its value |
| `(-name)` | `ZVSE2` only | frees the local variable (disappears from the text) |
| `(CONSTANT)` | `ZVSE2` only | the value of a global constant |
| `!#DC(NAME) = 10;` / `!#DC(NAME) = (OTHER);` | `ZVSE2` only | constant declaration; the line itself is removed (a `;` remains) |
| `!#VA(...);` | `ZVSE2` only | a "dummy" command for variable declarations; removed entirely |
| `(FILE)`, `(LINE)`, `(CODE)` | `ZVSE2` only | script name (in `^…^`), line number, text of the current line (up to 100 characters, escaped) |
| `[:label]` (outside a command) | all | label declaration: the number of the next command in the section; the text is removed |
| `[label]` (inside a command) | all | command number (used in `SN:G`); labels are visible only within their own section |
| `!!!` | all | becomes `!` (command disabled) |
| `%(name)` in a `^…^` string | `ZVSE2` | `%y5` (value of the local variable) |
| `%y(name)` in a string | `ZVSE2` | `%yy5` (indirect addressing through the local variable) |
| `%s(…)`, `%i(…)`, `%T(…)` | all | **left untouched**: ERA's interpolation understands them at runtime |

## Rules for recognizing a name in parentheses (`DetectIdentType`)

* First letter uppercase and all characters in `A-Z 0-9 _` → **constant** (so a function named `(F)` or `(E)` is
  an "unknown constant" error, while `(Fn)` is a function).
* First letter uppercase, contains lowercase letters → **function**.
* Starts with `@` or `-` → **variable**.
* First letter lowercase: if there is no `_` or there is a `[` → **variable**; otherwise (contains `_`) → **function**.
* A local variable name can only be `[a-z][a-zA-Z0-9]*`.
* A single-letter array index `f`…`t` is **always** a quick variable, even if a local variable with that name
  has been declared (`(arr[i])` → index `i`).

## Local variable pools

Reset at the start of every trigger (`!?`/`!$`) in `ZVSE2` scripts:

| Type | Pool | Example |
|------|------|---------|
| `y` | `y1`…`y100` | `(a:y)` → `y1`, the next one → `y2` |
| `x` | `x1`…`x16` | |
| `e` | `e2`…`e101` (as in the code: start 2, size 100) | |
| `z` | `z-1`…`z-10` (negative pool: an array `[3]` occupies `z-3, z-2, z-1`) | `(s:z)` → `z-1` |

Free ranges are kept in a list. An ERA quirk: when a range of exactly the required length is allocated and it is
not the first one in the list, it is not removed from the list (a bug involving the `PrevRange` variable). The port
reproduces this.

## Quirks to know when porting scripts

* Function numbers depend on the **load order** of scripts: the first mention of a name gets the next number.
  That is why mod load order is reproduced exactly (see `03_ERA_Events_Loading_Save.md`), and the name table
  is stored in the saved game.
* Registering a function name also creates a named variable `i^Name^` = the function number.
* Helper commands for variable indexes are inserted without incrementing the command counter, so labels after
  them "shift". That is how ERA behaves; the port replicates it.
* In ERA, preprocessor errors are shown in a message window and processing continues; in the port they go into
  the diagnostics.
