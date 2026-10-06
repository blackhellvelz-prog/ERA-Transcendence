# Olden Era reverse engineering — sources and verification levels

Heroes of Might and Magic: Olden Era (Unfrozen / Ubisoft), Steam app **3105440**, Early Access since
**2026-04-30**. Install layout `…/Heroes of Might and Magic Olden Era/HeroesOldenEra_Data/…`.

## Honest scope statement

This repository was produced in a cloud container **without a copy of Olden Era**. Nothing here was
obtained by running or disassembling the game binaries in this session. Every Olden Era fact is
therefore tagged with how it was verified:

| Tag | Meaning |
|-----|---------|
| **[V-code]** | Verified from source code of a working community mod/tool that runs against the real game (the code would not work otherwise) |
| **[V-data]** | Verified from tools that parse the real `Core.zip` and document the exact keys they found ("confirmed against N real files") |
| **[V-community]** | Stated by modders who tested it in-game (Steam/Nexus/tool docs), not independently checked |
| **[UNVERIFIED]** | Plausible, required by the design, must be confirmed on a real install — see `07_InGame_RE_Plan.md` |

Rule 7 of the project ("do not assume Olden Era capabilities") is enforced by this tagging: the
Compatibility layer may only rely on **[V-*]** facts; **[UNVERIFIED]** items are behind feature probes
that report *unsupported* until confirmed at runtime.

## Sources

| # | Source | Used for |
|---|--------|----------|
| O1 | `mimiasei/map-editor-json-tool` (GitHub; "HoMM Olden Era Scenario Editor", v0.9.1, updated 2026-10-06) | Scenario scripting registry (55 conditions, 114 actions, sourced from Unfrozen's official Notion "Conditions/Actions and their parameters — Full list"), `Core.zip` DB layout, custom content cloning, map format, H3 map import |
| O2 | O1 `gme-mod/` — a **BepInEx 6 IL2CPP** plugin for the game's map editor (`GameApi.cs`, `Plugin.cs`) | Engine/runtime facts: IL2CPP, obfuscation, assembly names, real type names (`Hex.MapEditor.BhNewGenMap`, `Hex.MapEditor.BhMapEditor`), BepInEx build `6.0.0-be.785`, `net6.0` |
| O3 | Nexus Mods pages for Olden Era: "BepInEx 6 (with version-independent deobfuscation)", "CheatPanel – BepInEx 6 IL2CPP", "Mod Configuration Panel", "Hero Creator and Editor" | Modding ecosystem **[V-community]** |
| O4 | Steam discussion "Modding Game Files" (app 3105440) | Players editing "JSON core files", mention of `hex.dll` + dnSpy **[V-community]**, conflicting with O2 — see `01_Engine_and_Runtime.md` |
| O5 | `KhanDevelopsGames/Olden-Era---Template-Generator`, `ignis-sec/HoMM-OE-Template-Editor` | Random map template format (`.rmg.json`) |

Not used: `Weolcan/homm-olden-era-community-mods` ("Official Release & Review Guide" — SEO/redistribution
content, not a tool; no code to verify).
