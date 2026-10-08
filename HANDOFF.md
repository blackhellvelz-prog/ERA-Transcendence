**English** | [Русский](HANDOFF.ru.md)

# How to continue the work

This file is for you and for the Claude Code instance that continues the port on the computer with Olden Era and
ERA installed. The full history with evidence is in `MODLOG.md`; the rules are in `CLAUDE.md`.

## Where we are now (2026-10-08)

* The goal is a port of **HoMM3 ERA** to Olden Era (the user's decision: ERA instead of WoG 3.58; ERA is built on
  WoG, so all of WoG is the lower layer). The full mod is the target: every WoG and ERA function, the core mods first
  (`WoG`, `Era Erm Framework`, `WoG Rus`, `WoG Scripts`, `WoG Scripts Rus`, `WoG Fix Lite`, `ERA Scripts`).
* Since session 2 (2026-10-07) the port **runs in the real game** (Olden Era 0.81.04, BepInEx 6 be.785). Verified
  in game: day start, object visits, save/load of the WoG state, the map layer, heroes, towns, players, mines,
  creature types, battles with the triggers `!?BR`, `!?BG0/1` (every action: walk, attack, shot, wait, defend, hero
  spell) and `!?MF1`, WoGification, the WoG Debug window.
* A new map starts **WoGified with the installation's WoG Options defaults** (96 options on in the user's ERA:
  commanders, stack experience, new objects, enhanced secondary skills…), as ERA does: an Olden Era map has no
  scripts of its own, so ERA does not ask.
* The headless reference engine runs the user's whole ERA (183 scripts) for a new game + 7 days with **0 ERM
  errors**; 273 xUnit tests are green. What the engine cannot do yet is reported as *unsupported* in
  `Compatibility/ERM_Compatibility_ERA.md`.

## Local setup

1. **.NET 8 SDK**, **Git**, Claude Code (desktop app or CLI).
2. Heroes of Might and Magic: Olden Era (Steam, app 3105440) with **BepInEx 6 IL2CPP be.785** installed and run once
   (it generates `BepInEx/interop`, which the plugins compile against). Back up the saves and
   `HeroesOldenEra_Data/StreamingAssets/Core.zip` before any change in the game folder.
3. An **ERA installation** (HoMM3 ERA 2.291 with the core mods). The port reads its scripts, texts and pictures at
   run time; nothing of it is in the repository. It is a read-only corpus: never write into it.
4. Environment variables: `OLDEN_ERA_DIR` — the Olden Era folder (the build of the plugins); `ERA_GAME_DIR` — the ERA
   folder (installation tests); `ERA_MODS_DIR`, `WOG_SCRIPTS_DIR` — the script corpora (see `README.md`).

## The working loop

1. Code + a headless test (`tests/WoG.Tests`) against the primary source (Era's Delphi, WoG's C++, the ERM help).
2. `dotnet build WoGOldenEra.sln` (no warnings), `dotnet test tests/WoG.Tests`, the ERA corpus
   (`WoG.ErmTool run --era …` with the mods highest priority first — the summary line must say `0 ERM errors`).
3. With the game **closed**: `powershell -ExecutionPolicy Bypass -File tools/deploy/deploy.ps1 -DebugMode`.
4. Launch the game (`steam://rungameid/3105440`), load a save or start a map, check the feature:
   * the **WoG Debug window**: F8 or the round "WoG" button in the top bar; every feature has a tile there with its
     ERM code — add a tile in `src/WoG.OldenEra.DebugUI/FeatureCatalog.cs` for each new feature;
   * the **command bridge** (WoG Debug on): write a command into a new file in
     `BepInEx/config/WoG/debug/in/<name>.txt`; the answer appears in `BepInEx/config/WoG/debug/out/<name>.txt`.
     Commands: `erm <ERM code>`, `hero`, `town`, `objects`, `state`, `vars`, `peek`/`invoke`/`set` (reflection),
     `battleevents`, `subskills`, `symbols`, `ui canvases|tree|sprites|fonts|keys`, `help`;
   * the log: `BepInEx/LogOutput.log` (`WoG …`, `[ERM] …`).
5. Record the finding in `MODLOG.md` and `MODLOG.ru.md`, update the matrix (and `Declare(...)` → regenerate the
   compatibility tables), commit.

In the game: never press F9 (the game's quick load); the game's own hotkeys are switched off while you type in the
WoG Debug window. The user may be playing — check before closing the game or clicking into it.

## What comes next

1. **The WoG level limit** (`UN:J1` with a limit): hold Olden Era's heroes to it (the experience logic's last level,
   `dyx.chmo`, is the candidate); the experience table itself is done.
2. **What the full WoG exercises now** (the unsupported list of the corpus run): `SS:F/L/S` (ERA's secondary skills
   plugin, ×490), `UN:C` (H3 memory, ×1498 — map the addresses scripts actually use), `SN:H` hints (×124), `HT`,
   `UN:I` (placing objects), `OW:I`, `UN:B`, `SN:F^Erm_FillInt32Array^`, `SN:M`, `UN:J1`, `IF:D/E/F/G` dialogs,
   `IF:M`/`IF:Q` messages (they need the ERM call to wait for the player's answer).
3. **The WoG Options window** in the game's style: the dialog items are already loaded (`WoGHost.OptionSetup`, pages
   × groups × items from ZSETUP01.TXT and the .ers files), the values are applied at a new map.
4. **Commanders and stack experience in battle** (buffs, the commander as a unit), the battle receivers `BU`, `BF`,
   `BH`, `BM`, `MR`.
5. To see how an ERA feature behaves, the user's ERA may be launched and watched (read-only).

## Important

* Single-player only. No anti-cheat bypassing.
* Do not commit game files (Olden Era, ERA, WoG, H3) or data derived from them — the repository is public.
* The reference sources (Era, WoG, ERA Project, universal-modder) are downloaded by `tools/fetch-references` into
  `..\research`.
