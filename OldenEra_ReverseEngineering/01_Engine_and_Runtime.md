# Olden Era — engine and runtime

## Facts

| Fact | Tag | Evidence |
|------|-----|----------|
| Unity engine, data folder `HeroesOldenEra_Data` | [V-code] | O1 resolves `<game>/HeroesOldenEra_Data/StreamingAssets/Core.zip` and works |
| **IL2CPP** scripting backend | [V-code] | O2 is a `BepInEx.Unity.IL2CPP` plugin using `Il2CppInterop`; types come from `BepInEx/interop/*.dll` generated on first start |
| Game code assembly is named **`Hex`** | [V-code] | `FindType("Hex", "Hex.MapEditor.BhNewGenMap")` |
| Namespaces/classes partly readable (`Hex.MapEditor.BhMapEditor`, `BhNewGenMap`, members `dropdown`, `template`, `seed`, `Start`, `OnBtn`, `Hide`, `Load`, `me`) | [V-code] | O2 |
| **Obfuscated** identifiers that change with game updates (e.g. file manager type `qp`, static instance `bufc`, file index `bufo`, map-size list `byok`, fields `byom`/`byon`) | [V-code] | O2 README: "names change with game updates" |
| Virtual file system: a static FileManager with `Dictionary<string virtualPath, file handle>` | [V-code] | O2 comment on `bufo` |
| UI uses `UnityEngine.UI` + `TMPro` (`TMP_Dropdown`) | [V-code] | O2 |
| BepInEx 6 bleeding edge **be.785** works; a "version-independent deobfuscation" BepInEx build exists | [V-code]/[V-community] | O2 csproj, O3 |
| Harmony patching of game methods works (`EventSystem.Update` patched as per-frame hook) | [V-code] | O2 README |
| Older/other builds may be Mono (`hex.dll` editable in dnSpy) | [V-community], conflicting | O4. Treated as: early EA builds or a different platform build. The port supports IL2CPP first; a Mono build would be strictly easier (same plugin API via BepInEx 5/6 Mono). |
| Map editor ships in the game | [V-code] | O2 patches it |
| Linux/Proton: BepInEx 6 has a Linux build for the game | [V-community] | O3 "Windows and linux version" |

## Consequences for the port

1. **Native extension route = BepInEx 6 IL2CPP plugin + Harmony.** This is the only verified way to
   run custom code inside the game. It gives: hooks on any non-inlined, non-stripped managed method;
   reading/writing game objects through Il2CppInterop; custom MonoBehaviours (after
   `ClassInjector.RegisterTypeInIl2Cpp`).
2. **Obfuscation is a first-class problem.** Following O2's proven pattern, every game symbol the adapter
   needs is looked up **by name at runtime** from one table (`OldenEraSymbols`), with a startup self-check
   that disables only the dependent features and logs `Game API not found: …`. After a game update only
   that table changes. Readable names (namespaces like `Hex.MapEditor`) are preferred; obfuscated ones are
   located by *signature* (field types, method shapes) to survive renames.
3. **Plugin target framework:** `net6.0`, references only BepInEx NuGet packages (no game DLLs in the repo).
4. Anti-cheat: none reported for single-player; multiplayer must not be modded (project rule).
