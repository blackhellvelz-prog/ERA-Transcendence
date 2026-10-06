# Olden Era — движок и среда выполнения

## Факты

| Факт | Метка | Доказательство |
|------|-------|----------------|
| Движок Unity, папка данных `HeroesOldenEra_Data` | [V-code] | O1 находит `<игра>/HeroesOldenEra_Data/StreamingAssets/Core.zip` и работает |
| Бэкенд **IL2CPP** | [V-code] | O2 — плагин `BepInEx.Unity.IL2CPP` на `Il2CppInterop`; типы берутся из `BepInEx/interop/*.dll`, генерируемых при первом запуске |
| Сборка игрового кода называется **`Hex`** | [V-code] | `FindType("Hex", "Hex.MapEditor.BhNewGenMap")` |
| Пространства имён/классы частично читаемы (`Hex.MapEditor.BhMapEditor`, `BhNewGenMap`, члены `dropdown`, `template`, `seed`, `Start`, `OnBtn`, `Hide`, `Load`, `me`) | [V-code] | O2 |
| **Обфусцированные** идентификаторы, меняющиеся с обновлениями (тип файлового менеджера `qp`, статический экземпляр `bufc`, индекс файлов `bufo`, список размеров карты `byok`, поля `byom`/`byon`) | [V-code] | README O2: «names change with game updates» |
| Виртуальная файловая система: статический FileManager со словарём `Dictionary<string виртуальныйПуть, дескриптор>` | [V-code] | комментарий O2 к `bufo` |
| Интерфейс на `UnityEngine.UI` + `TMPro` (`TMP_Dropdown`) | [V-code] | O2 |
| BepInEx 6 bleeding edge **be.785** работает; есть сборка BepInEx «с независимой от версии деобфускацией» | [V-code]/[V-community] | csproj O2, O3 |
| Патчи Harmony на методы игры работают (`EventSystem.Update` как ежекадровый хук) | [V-code] | README O2 |
| Ранние/другие сборки, возможно, были Mono (`hex.dll` правится в dnSpy) | [V-community], противоречит | O4. Трактуется как ранние сборки EA или другая платформа. Порт поддерживает прежде всего IL2CPP; Mono-сборка была бы строго проще (тот же API плагинов BepInEx 5/6 Mono). |
| В игру встроен редактор карт | [V-code] | O2 его патчит |
| Linux/Proton: для игры есть Linux-сборка BepInEx 6 | [V-community] | O3 «Windows and linux version» |

## Следствия для порта

1. **Путь нативного расширения = плагин BepInEx 6 IL2CPP + Harmony.** Это единственный проверенный способ
   выполнять свой код внутри игры. Он даёт: хуки на любой не-встроенный и не-вырезанный управляемый метод;
   чтение/запись игровых объектов через Il2CppInterop; свои MonoBehaviour (после
   `ClassInjector.RegisterTypeInIl2Cpp`).
2. **Обфускация — проблема первого порядка.** По проверенному в O2 шаблону каждый нужный адаптеру символ игры
   ищется **по имени во время работы** по одной таблице (`OldenEraSymbols`, файл
   `BepInEx/config/wog_symbols.json`), с самопроверкой при старте, которая отключает только зависимые функции и
   пишет `Game API not found: …`. После обновления игры меняется только этот JSON — пересборка не нужна.
3. **Целевой фреймворк плагина:** `net6.0`, ссылки только на NuGet-пакеты BepInEx (никаких DLL игры в
   репозитории). Проект `src/WoG.OldenEra` собирается против настоящего API BepInEx 6.0.0-be.785.
4. Античит: для одиночной игры не сообщается; мультиплеер модифицировать нельзя (правило проекта).
