# ERM Semantics in ERA (Differences from WoG 3.58)

Source: `Erm.pas` (`Hook_ZvsGetNum`, `GetErmParamValue`, `SetErmParamValue`, `Hook_ZvsGetFlags`,
`Hook_ZvsCheckFlags`, `ProcessErm`, `InterpolateErmStr`, `New_VR_Receiver`, `Hook_FU_P`, `Hook_FU_EXT`, `DO_P`),
`AdvErm.pas` (`GetServiceParams`, `SN_*`), `Trans.pas`, `Extern.pas`. Port: `src/WoG.Erm/Syntax/ErmParserEra.cs`,
`src/WoG.Erm/Runtime/EraValues.cs`, `EraProcess.cs`, `src/WoG.Erm/Receivers/EraReceivers.cs`, `EraApi.cs`.
Tests: `EraParserTests`, `EraRuntimeTests`, `EraCorpusTests`.

## 1. Parameters

Parsing order for a single parameter:

1. Skip whitespace (`#1..#32`).
2. One prefix: `?` (get), a `d` modifier, or a comparison (`=`, `<>`, `>`, `<`, `>=`, `<=`, `=>`, `=<`).
   ERA modifiers: `d` / `d+` (add), `d-`, `d*`, `d:` (division), `d%`, `d|` (OR), `d~` (AND-NOT),
   `d<<`, `d>>`; in `SN` parameters also `d&` (string concatenation).
3. Base: `v y x z e w` — a variable with an index; `c` — add the current day (numbers only).
4. Value/index: `^строка^` (string), `i^имя^`, `s^имя^` (name), quick variable `f`…`t`, `$макрос$` (macro),
   a number, or a variable letter + a number (`vy5` = v[y5]).
5. Skip whitespace.

Value types: integers (`число` (a number), `f..t`, `v`, `w`, `x`, `y`, `i^^`), floats (`e`), strings (`z`, `s^^`,
`^…^`), booleans (flags, only in conditions and `%F`).

Differences from WoG:
* a `^…^` string is an **ordinary parameter** (in WoG it is the "command text"); `IF:Q1^текст^` = two parameters;
* spaces are allowed in expressions: `!!VRy1:Sy2 :5000 +1;`;
* parameters that the command does not have are **ignored** when setting (WoG reports an error);
* `?` applied to an `e` variable in an integer command writes the **bits of the integer** into the float variable
  (this is how ERA does it);
* `FU`/`DO` may have zero parameters (`!!FU(F):P;`).

## 2. Conditions `&` / `|`

* An element is `значение` (a value) or `значение<сравнение>значение` (value, comparison, value).
* A single **number** is a flag: `&500` = flag 500 is set, `&-500` = flag 500 is cleared.
* A single **value of another type** is converted to a boolean: a number ≠ 0, a non-empty string (`&z1`).
* Numbers are compared as integers or as floats (if one side is `e`); strings are compared **byte by byte,
  case-sensitively** (in WoG, word by word and case-insensitively).
* Result: (all `&` elements are true and there is at least one) OR (at least one `|` element is true). An error in
  any element cancels the command.

## 3. Control Flow Within a Section

| Command | Behavior |
|---------|-----------|
| `!!if&усл;` … `!!el&усл;` … `!!el;` … `!!en;` | `el` with a condition is "else if" |
| `!!re var/начало/конец/шаг/доб;` … `!!en;` | loop (parameters: variable/start/end/step/addend): `var` = start; while `var <= конец` (end) when step ≥ 0, or `>=` when step < 0; the default step is 1; without `конец` (end) — an infinite loop (exit via `br`); the fifth parameter is added to the end value |
| `!!br;` / `!!br N;` | exit from the N-th enclosing loop |
| `!!co;` / `!!co N;` | next iteration of the N-th enclosing loop |
| `!!SN:G<номер>;` | jump to the command with this number (the numbers are assigned by the preprocessor from labels) |
| `!!FU:E;` | end of the current section (the following sections of the event are still executed) |
| `!!SN:Q;` | end of event processing: the remaining sections are not executed |

The maximum nesting depth of `if/re` is 16. A control-flow error (`el` without `if`, an unmatched `en`, `br` outside
a loop) aborts the entire event.

## 4. Local Variables and the Event (`ProcessErm`)

On every event (not on every section, as in WoG):

* saved, and restored after the event: `y1..y100`, `e1..e100`, `x1..x16`, **quick variables
  `f..t`**, `z-1..z-10`, flags 996–1000, `v997..v1000`;
* zeroed: `y1..y100`, `e1..e100`; `z-1..z-10` — except for functions 1–29999 when `ErmLegacySupport` is on;
* `x1..x16` = the call arguments (`ArgXVars`); on completion, the result (`RetXVars`);
* with `ErmLegacySupport`, `y-1..y-100` are saved for non-functions and zeroed before each section of classic
  triggers (timers, objects, …); without this option they are global;
* after all sections, the function `<ИмяСобытия>_Quit` (the event's name followed by _Quit) is called, if it is declared;
* flag 999 means "a human's turn on this computer", flag 1000 means "human visitor/real battle",
  `v998..v1000` are the event coordinates.

`ErmLegacySupport` is enabled in the ERA Project distribution (`default heroes3.ini`), and it is also on by default
in the port (`ErmRuntimeOptions.EraLegacySupport`).

Consequence: in ERA, `y` variables live **across all sections of a single event**, while quick variables `f..t`
are **local** (changes made inside a trigger are not visible outside it). WoG scripts that relied on the 3.58 behavior
have already been rewritten in ERA, which is why ERA is the behavioral reference.

## 5. Functions

* `!!FU(F):P a/b/?c;` — `x1..xN` = the values; `?число` (? followed by a number) passes the current value and receives the
  result ("by reference"); `?строка` (? followed by a string) receives the string that the function pointed to in `xN`
  by string number (for example, `!!VRx1:Z^текст^`); a local `z-` string is copied into a temporary string; `d-`
  negates the value.
* `!!FU:A?n;` — how many arguments were passed; `!!FU:A5/6;` — default values for the arguments not passed.
* `!!FU:S#/?r;` — how argument # was passed (0 — `?`, 1 — a value, 2 — with `d`).
* `!!DO(F)/начало/конец/шаг:P…;` (start/end/step) — the whole event is repeated, the counter is `x16`; the other
  `x` variables are preserved between iterations.
* `!!FU:D` — a network call (not supported in the port: single-player only).

## 6. Strings

* `z1..z1000` are global, `z-1..z-10` are local, `z>1000` are strings from `.ert` files,
  `z ≥ 1 000 000 000` are temporary strings (command literals, function results). A command's temporary strings
  live until the end of the command line; a trigger's strings live until the end of the event.
* **Interpolation** `%…` is a single pass (in WoG, 5 passes): `%y1`, `%Y1`, `%vy5`, `%i(имя)`, `%s(имя)`, `%F5`,
  `%e1` (3 digits after the decimal point), `%Dd/%Dw/%Dm/%Da`, `%Gc`, `%T(ключ)` (translation by key), `%Vf..%Vt`,
  `%%`, `%\:` (`;`), `%\"` (`^`). The contents of `z1..z1000` are **not interpolated** on output (in WoG they are);
  ERT strings are.
* `VR:U` means "contains" (case-insensitive, on trimmed strings); in WoG it means "ends with".

## 7. `VR` (Rewritten by ERA)

`S` with type rules (integer ← float: the fractional part is truncated, or rounded according to the second parameter:
0 — to nearest, <0 — down, >0 — up), `+ - * : %` (mixed integer/float operands are computed as floats;
`+` on a string is concatenation), `& | X ~` (integers only), `B` (to 0/1), `C` (several variables in a row,
including `y-`), `F` (clamp to a range, with a default value), `H` (flag "string is not empty"), `M1..M6`
(substring, N-th word, number → string in a given radix, length, first/last significant position),
`R` (random: `R0/мин/макс` — min/max, `R0/сид` — seed), `T` (random without a seed), `V` (string → number), `U`,
`Z` (create a temporary trigger string and write its number).

## 8. `SN` and Named Variables

| Command | What it does | Port |
|---------|-----------|------|
| `SN:W^имя^/значение` | named variable (integer or string) | yes |
| `i^имя^`, `s^имя^` | the same, directly in parameters | yes |
| `SN:M` | dynamic arrays: create (`-1` — new number; storage: −1 — until the end of the trigger, 0 — temporary, 1 — saved with the game), size, element | yes (element addresses — no) |
| `SN:V` | several array elements at once | yes |
| `SN:K` | string length, character by index | yes (memory copying — no) |
| `SN:X` | read/write the `x` variables of the current call | yes |
| `SN:T^ключ^/?z#/имя/значение…` | translation (by key) with `@имя@` (name) substitution | yes |
| `SN:C^ИМЯ^/?значение/?есть` | value of a constant (name / value / exists) | yes |
| `SN:G`, `SN:Q`, `SN:I`, `SN:D` | jump, exit, string interpolation, redraw | yes (`D` does nothing) |
| `SN:F^имя^/…` | call an Era API function (result in `v1`) | some of the functions (table below) |
| `SN:H` (hints), `SN:O`, `SN:P`, `SN:S`, `SN:R` | interface, sound, H3 resources | no (requires Olden Era UI/resources) |
| `SN:E`, `SN:L`, `SN:A`, `SN:B` | calling code by address, DLLs, memory | impossible |

An ERA bug preserved in the port: in `SN` parameters, the `d|` modifier performs **AND**, not OR
(`ApplyIntParam`).

### Era API Functions Available via `SN:F` in the Port

`ShowErmError`, `ExtendArrayLifetime`, `IsCommanderId` (monsters 174–191), `IsCampaign` (0), `Hash32` (CRC32),
`SplitMix32`, `Erm_Substr`, `Erm_StrTrim`, `Erm_StrReplace`, `Erm_StrPos`, `Erm_Interpolate`,
`Erm_CompareStrings`, `Erm_IntLog2`, `trStatic`, `PluginExists` (0), `PcxPngExists` (0), `Disable/Enable/Restore/
ResetErmTracking` (do nothing), `GetProcessGuid`; Win32: `GetKeyState` (0), `GetModuleHandleA` (0),
`GetFileAttributesA` (−1), `FindFirstFileA` (−1). The rest (`GetButtonID`, ini file handling, `FormatQuantity`,
`GetGameState`, array sorting by address, plugin functions `dll:функция`) are UNSUPPORTED; execution
continues.

## 9. What Is Fundamentally Non-Portable

* `UN:C`, `SN:B/L/A`, `SN:E`, `SN:M …/?адрес`, `SN:K` with memory copying — they operate on the memory and code
  of the Heroes III process. Scripts that rely on them have to be moved over to receivers/the adapter case by case.
* Plugin receivers (`SS`, `PA`, `QU`, `RD`) and plugin functions (`SN:F^плагин:функция^`) are closed DLLs;
  they can only be reimplemented from a description of their behavior.
