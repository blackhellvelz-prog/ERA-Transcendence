**English** | [Русский](07_InGame_RE_Plan.ru.md)

# In-game reverse-engineering plan (to be carried out on a computer that has Olden Era)

This is work that cannot be done without the game. The steps are ordered so that each one unlocks adapter features.
Tooling follows the universal-modder playbook for Unity/IL2CPP.

## 0. Safety
1. Back up the saves and `HeroesOldenEra_Data/StreamingAssets/Core.zip`.
2. Single-player only. Windowed mode for screenshots.

## 0.1 Automatic data collection (read-only)
`powershell -ExecutionPolicy Bypass -File tools\oe-recon\collect.ps1` (from a clone of the repository). The script
finds the game through Steam by itself (`appmanifest_3105440.acf`), changes nothing, and collects into
`oe-recon-<date>.zip` on the desktop: the Unity version and IL2CPP indicators, the BepInEx state, the `Core.zip`
tree, tables of units/heroes/items/skills/spells/buffs (`*.csv`), a sample unit JSON, the list of save files with
their first 16 bytes, `Player.log`; if `BepInEx/interop` and the .NET 8 SDK are present, also the output of
`probe-symbols` (`symbols.txt`). The archive contains game data — do not put it into a public repository.

## 1. Obtain the code model
1. Install BepInEx 6 IL2CPP **be.785** (as in O2) into the game folder; run the game once — this produces
   `BepInEx/interop/*.dll` (typed proxies of all game classes, including those with obfuscated names).
2. Alternative: **Cpp2IL** / **Il2CppDumper** on `GameAssembly.dll` +
   `HeroesOldenEra_Data/il2cpp_data/Metadata/global-metadata.dat`.
3. Run `dotnet run --project tools/WoG.ErmTool -- probe-symbols <BepInEx/interop>` — the tool
   (it only reads metadata; the game code is not executed) prints the types and members of `Hex.dll` whose names
   relate to heroes, battle, units, buffs, saving, scenarios, turns, the map, towns, items, spells, dialogs.
   Using this list and ILSpy/dnSpy, fill in `BepInEx/config/wog_symbols.json` (the plugin creates the template
   itself on first launch).

## 2. Symbols to find (`OldenEraSymbols.Known` keys)

| Key | What to find | How to recognize it |
|-----|--------------|---------------------|
| `game.root` | singleton holding the game state | static instance with lists of players/heroes; used by the top bar |
| `game.day`, `turn.start` | day counter; the "start of turn/new day" method | called once per player turn; the scenario condition `StartTurn` is also raised from here |
| `hero.list`, `hero.id`, `hero.owner`, `hero.experience`, `hero.level`, `hero.offence`, `hero.defence`, `hero.spellPower`, `hero.intelligence`, `hero.mana`, `hero.movement` | hero model | the fields match the hero JSON: `offence, defence, spellPower, intelligence, luck, moral`, experience, level, mana |
| `hero.army`, `stack.unitSid`, `stack.count` | army and stack | unit sid + count |
| `object.interact` | method called when a hero enters an object | the code path that raises `ObjectInteractionBefore/After` |
| (scenario dispatcher) | condition/action dispatcher | `switch` on the strings `"GiveRes"`, `"StartTurn"` … — **gives callable implementations of all 114 actions** |
| `battle.start`, `battle.end`, `battle.round`, `battle.action` | battle controller | queue ordered by `initiative`, use of `numCounters`, the `damageMin/Max` roll |
| `buff.apply` | method that applies a buff by id | used by the `AddBuffHeroDays` action |
| `save.write`, `save.read` | writing/reading a save | file I/O in the saves folder; the argument layout (slot name) is needed as well |
| `ui.message`, `ui.question` | message window, yes/no question | used by the `Dialog` action |
| FileManager | (in O2: `qp`/`bufc`/`bufo`) virtual file index | for loading our data |

After verifying each entry in `wog_symbols.json` in game, mark it `"Status": "verified"` — only then will the
plugin enable the feature that depends on it.

## 3. Verify the [UNVERIFIED] items
1. Global scope of the overlay: put `wog_core.zip` with one cloned creature next to `Core.zip`, start a
   random map, and check that the creature exists (for example, via `GiveUnitHero` in a test scenario).
2. A buff that grants flight; the `numCounters` buff; rounding of percentage stats.
3. Accessibility of the scenario counters from the plugin (the save-ownership marker).
4. The shots/ammo model.
5. A custom uGUI window over the adventure map and the town screen.

## 4. Oracles (how to tell that the mapping works)
* Lines of the `wog` channel in `BepInEx/LogOutput.log`.
* A test map whose scenario JSON prints counters (the `Print` action), while an ERM script (executed by our
  runtime) checks the same values — the results must match.
* Screenshots of the commander/experience UI for each feature.

## 5. Outcome of this phase
A filled-in `wog_symbols.json` + the "Verified in game" column in `Compatibility/Compatibility_Matrix.md`.
