<#
  Builds the WoG plugin and installs it into Olden Era's BepInEx (BepInEx 6 IL2CPP must already be installed).

    powershell -ExecutionPolicy Bypass -File tools\deploy\deploy.ps1 [-GamePath <dir>] [-DebugMode] [-NoBuild]

  Installs:
    BepInEx\plugins\WoG\WoG.*.dll                 the plugin and the WoG libraries (WoG.OldenEra.DebugUI.dll: the
                                                  in-game WoG Debug window, active only with WoG Debug on)
    BepInEx\config\wog_symbols.json               game symbols (src\WoG.OldenEra\symbols; the old file is kept as .bak)
    BepInEx\config\WoG\id-maps\*.json             WoG/H3 id <-> Olden Era id tables (Compatibility\id-maps)
    BepInEx\config\WoG\mods\WoG Debug\            the ERA mod of the vertical slice
  -DebugMode turns on WoG Debug in BepInEx\config\wog.oldenera.cfg: the command bridge, unverified symbols
  and tracing of the day-start candidates.

  Roll back: delete BepInEx\plugins\WoG and BepInEx\config\WoG (and wog_symbols.json, wog.oldenera.cfg).
#>
param(
    [string]$GamePath = "",
    [switch]$DebugMode,
    [switch]$NoBuild
)
$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

if (-not $GamePath) {
    $steam = (Get-ItemProperty "HKCU:\Software\Valve\Steam" -ErrorAction SilentlyContinue).SteamPath
    $libs = @()
    if ($steam) {
        $libs += $steam
        $vdf = Join-Path $steam "steamapps\libraryfolders.vdf"
        if (Test-Path $vdf) { $libs += (Select-String -Path $vdf -Pattern '"path"\s+"([^"]+)"').Matches | ForEach-Object { $_.Groups[1].Value -replace '\\\\', '\' } }
    }
    # [IO.Path]/[IO.File]: a library on a drive that is not connected must not stop the search (Join-Path throws).
    foreach ($l in $libs) {
        if ([IO.File]::Exists([IO.Path]::Combine($l, "steamapps", "appmanifest_3105440.acf"))) {
            $GamePath = [IO.Path]::Combine($l, "steamapps", "common", "Heroes of Might and Magic Olden Era"); break
        }
    }
}
if (-not (Test-Path (Join-Path $GamePath "HeroesOldenEra.exe"))) { throw "Olden Era not found; pass -GamePath" }
$bep = Join-Path $GamePath "BepInEx"
if (-not (Test-Path (Join-Path $bep "core\BepInEx.Unity.IL2CPP.dll"))) { throw "BepInEx 6 IL2CPP is not installed in $GamePath" }
Write-Host "Game: $GamePath"

if (-not $NoBuild) {
    $dotnet = Join-Path $env:USERPROFILE ".dotnet8\dotnet.exe"
    if (Test-Path $dotnet) { $env:DOTNET_ROOT = Split-Path $dotnet } else { $dotnet = "dotnet" }
    & $dotnet build (Join-Path $repo "src\WoG.OldenEra\WoG.OldenEra.csproj") -nologo -v q "-p:OldenEraDir=$GamePath"
    if ($LASTEXITCODE -ne 0) { throw "build failed" }
    # the in-game WoG Debug window (needs BepInEx\interop, which BepInEx writes on the game's first start)
    & $dotnet build (Join-Path $repo "src\WoG.OldenEra.DebugUI\WoG.OldenEra.DebugUI.csproj") -nologo -v q "-p:OldenEraDir=$GamePath"
    if ($LASTEXITCODE -ne 0) { throw "build failed (debug window)" }
}

$plugin = Join-Path $bep "plugins\WoG"
New-Item -ItemType Directory -Force $plugin | Out-Null
Copy-Item (Join-Path $repo "src\WoG.OldenEra\bin\Debug\net6.0\WoG.*.dll") $plugin -Force
Copy-Item (Join-Path $repo "src\WoG.OldenEra\bin\Debug\net6.0\WoG.*.pdb") $plugin -Force
$ui = Join-Path $repo "src\WoG.OldenEra.DebugUI\bin\Debug\net6.0\WoG.OldenEra.DebugUI.dll"
if (Test-Path $ui) { Copy-Item $ui, ($ui -replace '\.dll$', '.pdb') $plugin -Force }

$cfg = Join-Path $bep "config"
$wogCfg = Join-Path $cfg "WoG"
New-Item -ItemType Directory -Force (Join-Path $wogCfg "id-maps") | Out-Null
Copy-Item (Join-Path $repo "Compatibility\id-maps\*.json") (Join-Path $wogCfg "id-maps") -Force

$symbols = Join-Path $cfg "wog_symbols.json"
if (Test-Path $symbols) { Copy-Item $symbols "$symbols.bak" -Force }
Copy-Item (Join-Path $repo "src\WoG.OldenEra\symbols\wog_symbols.json") $symbols -Force

$mods = Join-Path $wogCfg "mods"
New-Item -ItemType Directory -Force $mods | Out-Null
Copy-Item (Join-Path $repo "mods\WoG Debug") $mods -Recurse -Force

function Set-CfgValue([string]$file, [string]$section, [string]$key, [string]$value) {
    $lines = @(); if (Test-Path $file) { $lines = @(Get-Content $file -Encoding UTF8) }
    $out = New-Object System.Collections.Generic.List[string]
    $inSection = $false; $done = $false; $sawSection = $false
    foreach ($l in $lines) {
        if ($l -match '^\s*\[(.+)\]\s*$') {
            if ($inSection -and -not $done) { $out.Add("$key = $value"); $done = $true }
            $inSection = ($Matches[1] -eq $section); if ($inSection) { $sawSection = $true }
        }
        elseif ($inSection -and $l -match ("^\s*" + [regex]::Escape($key) + "\s*=")) { $out.Add("$key = $value"); $done = $true; continue }
        $out.Add($l)
    }
    if (-not $done) {
        if (-not $sawSection) { $out.Add(""); $out.Add("[$section]") }
        $out.Add("$key = $value")
    }
    [IO.File]::WriteAllLines($file, $out, (New-Object System.Text.UTF8Encoding $false))
}

# The BepInEx console window freezes the game: one click in it starts QuickEdit selection, Windows then blocks
# every write to the console, and the game's main thread stops at its next log line. LogOutput.log stays.
$bepCfg = Join-Path $cfg "BepInEx.cfg"
if (Test-Path $bepCfg) { Set-CfgValue $bepCfg "Logging.Console" "Enabled" "false" }

if ($DebugMode) {
    $pluginCfg = Join-Path $cfg "wog.oldenera.cfg"
    Set-CfgValue $pluginCfg "Debug" "Enabled" "true"
    Set-CfgValue $pluginCfg "Debug" "AllowUnverifiedSymbols" "true"
    Set-CfgValue $pluginCfg "Debug" "TraceMethods" "ebe.OnStartDay;ebe.OnStartWeek"
    Write-Host "WoG Debug enabled in $pluginCfg"
}
Write-Host "Installed WoG into $bep"
