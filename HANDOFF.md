# How to continue the work on your own computer

> **Unfinished:** the switch to English-primary docs is almost done — see `TRANSLATION_STATUS.md` and finish it first.

This file is for you and for the Claude Code instance that will run on the computer with Olden Era. The cloud
session in which the current part of the work was done cannot see your computer; a local Claude Code can see it
and can verify everything directly in the game.

## Where we are now (2026-10-07)

* The goal is a port of **HoMM3 ERA** to Olden Era (the user's decision: ERA instead of WoG 3.58).
* Done and verified without the game: reverse engineering of ERA/WoG/Olden Era (the `*_ReverseEngineering`
  folders), the compatibility matrix, the core, the ERM runtime for WoG and **ERA** (ERM 2.0 preprocessor, ERA
  semantics), commanders, stack experience, saving, the data overlay, the BepInEx plugin (it builds, but has not
  been run in the game).
* The entire ERA project (183 scripts) starts as a new game on the test engine without errors. 132 tests are
  green.
* **Nothing has been verified in Olden Era itself yet** — that is the next stage.

## What to install

1. **Git** — https://git-scm.com/download/win
2. **.NET 8 SDK** — https://dotnet.microsoft.com/download/dotnet/8.0
3. **Claude Code** — the Claude desktop app (the Code tab) or the CLI in a terminal; how to install it —
   https://code.claude.com/docs
4. Heroes of Might and Magic: Olden Era (Steam) — you already have it.

## Steps

1. Unpack the archive (or clone: `git clone -b claude/wog-olden-era-port
   https://github.com/blackhellvelz-prog/ERA-Transcendence`). The archive contains the repository with its git
   history, so commits and `git push` work as usual.
2. Open the repository folder in Claude Code: in the app, "Open folder"; in a terminal, `cd ERA-Transcendence`
   and `claude`. Claude will read `CLAUDE.md` and pick up the `mod-any-game` skill from `.claude/skills`.
3. Send the first message (you can copy it as is):

> We are continuing the port of ERA to Olden Era. Read CLAUDE.md, HANDOFF.md and MODLOG.md. Then:
> 1) run `tools\fetch-references\fetch-references.ps1` and check `dotnet test` (including the ERA and WoG script corpora);
> 2) run `tools\oe-recon\collect.ps1` and analyze the output: game version, Unity, BepInEx, Core.zip data,
>    saves; record the facts in OldenEra_ReverseEngineering with a verification tag;
> 3) propose a plan for installing BepInEx 6 IL2CPP be.785 (with backups) and ask me before installing it;
> 4) after that — OldenEra_ReverseEngineering/07_InGame_RE_Plan.md: find the game symbols, fill in
>    wog_symbols.json, hook up the ERA mods in the plugin (WoGHost.AddEraMods) and verify the first vertical slice
>    in the game (for example, an ERA script on OnEveryDay that changes the player's resources).
> Write documentation in English with a synchronized Russian copy (*.ru.md).

## What comes next in the plan (in brief)

1. Recon of the Olden Era installation (`collect.ps1`) → facts with verification tags.
2. BepInEx 6 IL2CPP + `BepInEx/interop` → `probe-symbols` → `wog_symbols.json` (heroes, players, turn, objects,
   battle, saves) → mark the verified symbols as `verified`.
3. Plugin: hook up the ERA mods (the user's ERA mods folder or `BepInEx/config/WoG/mods`), ERA events
   from Olden Era hooks, saving next to the saved game.
4. Receivers that the ERA corpus needs: `UN` (map), `BM/BU/BG/BA` (battle), `CA`, `PO`, `OB`, `DL`.
5. Commanders and stack experience in battle (buffs, the commander unit), the WoG Options UI.

## Important

* Single-player only. Before changing the game folder, make a copy of the saves and `Core.zip`.
* Do not commit game files (Olden Era, ERA, WoG, H3) to the repository — it is public.
* The reference sources (Era, WoG, ERA Project, universal-modder) are downloaded by the
  `tools/fetch-references` script into `..\research` — they are not included in the archive.
