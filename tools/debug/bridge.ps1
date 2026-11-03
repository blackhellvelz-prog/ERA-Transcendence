<#
  WoG Debug command bridge client (PowerShell twin of bridge.sh): runs one command in the running game and prints
  the answer. Needs [Debug] Enabled = true in BepInEx\config\wog.oldenera.cfg (deploy.ps1 -DebugMode sets it).

    powershell -ExecutionPolicy Bypass -File tools\debug\bridge.ps1 "erm !!OW:R-1/6/?x1;" [-TimeoutSec 30]
    powershell -ExecutionPolicy Bypass -File tools\debug\bridge.ps1 help

  The game folder comes from OLDEN_ERA_DIR, else the Steam library that has Olden Era (app 3105440).
  The command file is written as .tmp and renamed, so the plugin never reads half a file.
#>
param(
    [Parameter(Mandatory = $true, Position = 0)][string]$Command,
    [int]$TimeoutSec = 30
)
$ErrorActionPreference = "Stop"
$game = $env:OLDEN_ERA_DIR
if (-not $game) {
    $steam = (Get-ItemProperty "HKCU:\Software\Valve\Steam" -ErrorAction SilentlyContinue).SteamPath
    $libs = @()
    if ($steam) {
        $libs += $steam
        $vdf = Join-Path $steam "steamapps\libraryfolders.vdf"
        if (Test-Path $vdf) { $libs += (Select-String -Path $vdf -Pattern '"path"\s+"([^"]+)"').Matches | ForEach-Object { $_.Groups[1].Value -replace '\\\\', '\' } }
    }
    foreach ($l in $libs) {
        if ([IO.File]::Exists([IO.Path]::Combine($l, "steamapps", "appmanifest_3105440.acf"))) {
            $game = [IO.Path]::Combine($l, "steamapps", "common", "Heroes of Might and Magic Olden Era"); break
        }
    }
}
if (-not $game) { throw "set OLDEN_ERA_DIR to the Olden Era folder" }
$dir = Join-Path $game "BepInEx\config\WoG\debug"
New-Item -ItemType Directory -Force (Join-Path $dir "in"), (Join-Path $dir "out") | Out-Null
$name = "q" + [DateTime]::UtcNow.Ticks
$tmp = Join-Path $dir "$name.tmp"
[IO.File]::WriteAllText($tmp, $Command, (New-Object System.Text.UTF8Encoding $false))
Move-Item $tmp (Join-Path $dir "in\$name.txt")
$out = Join-Path $dir "out\$name.txt"
$deadline = (Get-Date).AddSeconds($TimeoutSec)
while ((Get-Date) -lt $deadline) {
    if (Test-Path $out) {
        Start-Sleep -Milliseconds 100 # the plugin writes the answer in one call; give it a moment to close the file
        [IO.File]::ReadAllText($out, [Text.Encoding]::UTF8)
        exit 0
    }
    Start-Sleep -Milliseconds 300
}
Write-Error "timeout: is the game running with WoG Debug on, and a map loaded?"
exit 1
