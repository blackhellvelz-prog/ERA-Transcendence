<#
  Downloads reference sources into the ..\research folder (next to the repository). Read-only, installs
  nothing. Third-party files are never placed in the project repository.

  Run:  powershell -ExecutionPolicy Bypass -File tools\fetch-references\fetch-references.ps1 [-Target path] [-WithRus] [-Full]
    -WithRus  also fetch the Russian edition of ERA Project
    -Full     full clones (otherwise ERA Project is limited to scripts, translations, help and mod.json)

  RU: Скачивает исходники для справки в папку ..\research (рядом с репозиторием). Только чтение, ничего не
  устанавливает. Чужие файлы в репозиторий проекта не кладутся.
  Запуск:  powershell -ExecutionPolicy Bypass -File tools\fetch-references\fetch-references.ps1 [-Target путь] [-WithRus] [-Full]
    -WithRus  также русская версия ERA Project
    -Full     полные клоны (иначе ERA Project — только скрипты, переводы, справка и mod.json)
#>
param(
    [string]$Target = "",
    [switch]$WithRus,
    [switch]$Full
)
$ErrorActionPreference = "Stop"
if (-not $Target) { $Target = Join-Path (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)) "..\research" }
New-Item -ItemType Directory -Force -Path $Target | Out-Null
$Target = (Resolve-Path $Target).Path
Write-Host "Folder: $Target"

function Clone([string]$url, [string]$dir, [switch]$Shallow) {
    $path = Join-Path $Target $dir
    if (Test-Path (Join-Path $path ".git")) { Write-Host "exists: $dir"; return }
    Write-Host "cloning: $url"
    if ($Shallow) { git clone --depth 1 $url $path } else { git clone $url $path }
}

function CloneEraProject([string]$url, [string]$dir) {
    $path = Join-Path $Target $dir
    if (Test-Path (Join-Path $path ".git")) { Write-Host "exists: $dir"; return }
    if ($Full) { git clone --depth 1 $url $path; return }
    Write-Host "cloning (text files only): $url"
    git clone --filter=blob:none --sparse --depth 1 $url $path
    git -C $path sparse-checkout set --no-cone "/Help/" "/Mods/*/Data/s/" "/Mods/*/Lang/" "/Mods/*/lang/" "/Mods/*/mod.json" "/default heroes3.ini" "/LICENSE" "/README.md"
}

Clone "https://github.com/ethernidee/era" "era" -Shallow                 # Era 3.9.x engine (Delphi): Erm.pas, AdvErm.pas...
Clone "https://github.com/ethernidee/b2" "b2" -Shallow                   # engine library (TextScan, StrLib)
CloneEraProject "https://github.com/ERA-Projects/era-project-eng" "era-eng"
if ($WithRus) { CloneEraProject "https://github.com/ERA-Projects/era-project-rus" "era-rus" }
Clone "https://github.com/GrayFace/wog" "wog" -Shallow                   # WoG sources (erm.cpp, npc.cpp, crexpo.cpp)
Clone "https://github.com/alexandersorokin/heroes3-era-wogify" "wogify" -Shallow  # WoG 3.58f scripts
Clone "https://github.com/rehan-remade/universal-modder" "universal-modder" -Shallow  # mod-any-game skill, um CLI, knowledge base
Clone "https://github.com/Weolcan/homm-olden-era-community-mods" "homm-olden-era-community-mods" -Shallow  # OE community mods
Clone "https://github.com/vcmi/vcmi" "vcmi" -Shallow                     # H3 reference data (creature ids)

Write-Host ""
Write-Host "Done. To run the corpus tests:"
Write-Host "  `$env:ERA_MODS_DIR = `"$Target\era-eng\Mods`""
Write-Host "  `$env:WOG_SCRIPTS_DIR = `"$Target\wogify\Mods\WoG Wogify Scripts 3.58f\Data\s`""
Write-Host "  dotnet test tests\WoG.Tests"
