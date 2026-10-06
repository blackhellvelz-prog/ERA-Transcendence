# In-game reverse-engineering plan (to run on a machine that owns Olden Era)

This is the work that cannot be done without the game. It is ordered so each step unlocks adapter
features. Tooling follows the universal-modder Unity/IL2CPP playbook.

## 0. Lab safety
1. Back up saves and the game folder's `HeroesOldenEra_Data/StreamingAssets/Core.zip`.
2. Single-player only. Windowed mode for screenshots.

## 1. Dump the code model
1. Install BepInEx 6 IL2CPP **be.785** (same as O2) into the game folder; start once — it generates
   `BepInEx/interop/*.dll` (typed proxies of every game class, incl. obfuscated names).
2. Alternatively run **Cpp2IL** / **Il2CppDumper** on `GameAssembly.dll` +
   `HeroesOldenEra_Data/il2cpp_data/Metadata/global-metadata.dat` for dummy DLLs.
3. Open `BepInEx/interop/Hex.dll` in ILSpy/dnSpy. Produce `OldenEra_ReverseEngineering/symbols.json`
   by running `tools/WoG.ErmTool -- probe-symbols <interop dir>` (searches the patterns below and writes
   the resolved names; the plugin reads the same file).

## 2. Symbols to locate (fill `OldenEraSymbols`)

| Area | What to find | How to recognise it |
|------|--------------|---------------------|
| Game state root | singleton holding players, heroes, map, day | static instance with `List<Hero>`-like fields; used by UI top bar |
| Day/turn | "start turn"/"new day" method | called once per player turn; scenario `StartTurn` condition must be raised from it |
| Hero | hero model class | fields matching hero JSON: `offence, defence, spellPower, intelligence, luck, moral`, exp, level, mana, army (7 slots?) |
| Army slot | stack model | unit sid + count |
| Map object interaction | method invoked when a hero enters an object | the code path that raises `ObjectInteractionBefore/After` |
| Scenario engine | condition/action dispatcher | string switch on `"GiveRes"`, `"StartTurn"`, … — **gives callable implementations of all 114 actions** |
| Battle | battle controller: start, end, round, unit turn, action, damage | `initiative` queue, `numCounters` use, `damageMin/Max` roll |
| Buff application | method that adds a buff by id to a unit/hero | used by `AddBuffHeroDays` |
| Save/Load | methods writing/reading save files | file I/O under the saves folder |
| UI | message box, yes/no dialog, hero screen, town screen | used by scenario `Dialog` |
| FileManager | (O2: `qp`/`bufc`/`bufo`) virtual file index | for loading our overlay data |

## 3. Verify the [UNVERIFIED] items
1. Overlay zip global scope: put `wog_core.zip` with one cloned creature next to `Core.zip`; start a
   random map; check the creature exists (e.g. via `GiveUnitHero` in a test scenario).
2. Buff granting flight; buff `numCounters`; percent stats rounding.
3. Counters writable from the plugin (save-identity token).
4. Ammo/shots model.
5. Custom uGUI window over adventure map and town screen.

## 4. Oracles (how we know a mapping works)
* `BepInEx/LogOutput.log` lines from the plugin's `wog-probe` channel.
* A test map `tests/oe-maps/wog_probe` whose scenario JSON prints counters (`Print` action) and whose
  ERM script (run by our runtime) asserts the same values — the two must agree.
* Screenshots of the commander/experience UI per feature.

## 5. Deliverable of this phase
`symbols.json` + a filled "Verified" column in `Compatibility/Compatibility_Matrix.md`.
