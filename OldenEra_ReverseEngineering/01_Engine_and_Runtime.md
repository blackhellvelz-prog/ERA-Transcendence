# Olden Era — Engine and Runtime

## Facts

| Fact | Tag | Evidence |
|------|-----|----------|
| Unity engine, data folder `HeroesOldenEra_Data` | [V-code] | O1 finds `<game>/HeroesOldenEra_Data/StreamingAssets/Core.zip` and works |
| **IL2CPP** backend | [V-code] | O2 is a `BepInEx.Unity.IL2CPP` plugin built on `Il2CppInterop`; types come from `BepInEx/interop/*.dll`, which are generated on first launch |
| The game-code assembly is named **`Hex`** | [V-code] | `FindType("Hex", "Hex.MapEditor.BhNewGenMap")` |
| Namespaces/classes are partially readable (`Hex.MapEditor.BhMapEditor`, `BhNewGenMap`, members `dropdown`, `template`, `seed`, `Start`, `OnBtn`, `Hide`, `Load`, `me`) | [V-code] | O2 |
| **Obfuscated** identifiers that change with updates (file manager type `qp`, static instance `bufc`, file index `bufo`, map size list `byok`, fields `byom`/`byon`) | [V-code] | O2 README: "names change with game updates" |
| Virtual file system: a static FileManager with a `Dictionary<string virtualPath, descriptor>` dictionary | [V-code] | O2 comment on `bufo` |
| UI built on `UnityEngine.UI` + `TMPro` (`TMP_Dropdown`) | [V-code] | O2 |
| BepInEx 6 bleeding edge **be.785** works; there is a BepInEx build "with version-independent deobfuscation" | [V-code]/[V-community] | O2 csproj, O3 |
| Harmony patches on game methods work (`EventSystem.Update` as a per-frame hook) | [V-code] | O2 README |
| Earlier/other builds may have been Mono (`hex.dll` is edited in dnSpy) | [V-community], contradicts | O4. Interpreted as early EA builds or a different platform. The port primarily supports IL2CPP; a Mono build would be strictly simpler (the same BepInEx 5/6 Mono plugin API). |
| The game has a built-in map editor | [V-code] | O2 patches it |
| Linux/Proton: a Linux build of BepInEx 6 exists for the game | [V-community] | O3 "Windows and linux version" |

## Implications for the port

1. **Native extension path = BepInEx 6 IL2CPP plugin + Harmony.** This is the only verified way
   to run your own code inside the game. It provides: hooks on any managed method that has not been inlined or stripped;
   reading/writing game objects through Il2CppInterop; custom MonoBehaviours (after
   `ClassInjector.RegisterTypeInIl2Cpp`).
2. **Obfuscation is a first-order problem.** Following the pattern verified in O2, every game symbol the adapter needs
   is looked up **by name at runtime** from a single table (`OldenEraSymbols`, file
   `BepInEx/config/wog_symbols.json`), with a startup self-check that disables only the dependent features and
   logs `Game API not found: …`. After a game update only this JSON changes — no rebuild is needed.
3. **Plugin target framework:** `net6.0`, references only to BepInEx NuGet packages (no game DLLs in the
   repository). The `src/WoG.OldenEra` project builds against the real BepInEx 6.0.0-be.785 API.
4. Anti-cheat: none reported for single-player; multiplayer must not be modded (project rule).
