# Olden Era Reverse Engineering — Sources and Verification Levels

Heroes of Might and Magic: Olden Era (Unfrozen / Ubisoft), Steam app **3105440**, in early access since
**2026-04-30**. Install layout: `…/Heroes of Might and Magic Olden Era/HeroesOldenEra_Data/…`.

## Honest scope statement

This work was done in a cloud container **without a copy of Olden Era**. Nothing here was obtained by running or
disassembling the game in this session. Therefore every fact about Olden Era carries a tag that states how it was verified:

| Tag | Meaning |
|-----|---------|
| **[V-code]** | Verified against the source code of a working community mod/tool that operates on the real game (otherwise the code would not work) |
| **[V-data]** | Verified by tools that parse the real `Core.zip` and document the keys they found ("checked against N real files") |
| **[V-community]** | Claimed by modders who checked it in game (Steam/Nexus/tool documentation); not independently verified |
| **[UNVERIFIED]** | Plausible and required by the architecture, but must be confirmed on a real install — see `07_InGame_RE_Plan.md` |

Project rule 7 ("do not assume Olden Era capabilities") is enforced through these tags: the compatibility layer
relies only on **[V-*]** facts; **[UNVERIFIED]** items are gated behind checks (symbols with status
`verified`) that return *unsupported* until confirmed.

## Sources

| # | Source | Used for |
|---|--------|----------|
| O1 | `mimiasei/map-editor-json-tool` (GitHub; "HoMM Olden Era Scenario Editor", v0.9.1, updated 2026-10-06) | Scenario scripting registry (55 conditions, 114 actions — from Unfrozen's official Notion page "Conditions/Actions and their parameters — Full list"), `Core.zip` structure, content cloning, map format, H3 map import |
| O2 | The `gme-mod/` folder from O1 — a **BepInEx 6 IL2CPP** plugin for the built-in map editor (`GameApi.cs`, `Plugin.cs`) | Engine facts: IL2CPP, obfuscation, assembly names, real type names (`Hex.MapEditor.BhNewGenMap`, `Hex.MapEditor.BhMapEditor`), BepInEx build `6.0.0-be.785`, `net6.0` |
| O3 | Nexus Mods pages for Olden Era: "BepInEx 6 (with version-independent deobfuscation)", "CheatPanel – BepInEx 6 IL2CPP", "Mod Configuration Panel", "Hero Creator and Editor" | Mod ecosystem **[V-community]** |
| O4 | Steam discussion "Modding Game Files" (app 3105440) | Players edit "JSON core files"; mentions `hex.dll` + dnSpy **[V-community]**; contradicts O2 — see `01_Engine_and_Runtime.md` |
| O5 | `KhanDevelopsGames/Olden-Era---Template-Generator`, `ignis-sec/HoMM-OE-Template-Editor` | Random map template format (`.rmg.json`) |

Not used: `Weolcan/homm-olden-era-community-mods` ("Official Release & Review Guide" — SEO content,
not a tool; there is nothing to verify).
