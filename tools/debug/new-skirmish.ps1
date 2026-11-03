<#
  Starts Olden Era (if it is not running) and a new ARCADE skirmish for in-game checks: main menu → New game →
  Quick start → Templates → ARCADE → Choose → Temple → the first hero → Start game; waits for the WoG day 1.

    powershell -ExecutionPolicy Bypass -File tools\debug\new-skirmish.ps1 [-TimeoutSec 240]

  Needs universal-modder's `um` (um win launch / drive / shot). Clicks are in the 1600×900 window frame
  (Olden Era 0.82.07). Never sends F9 (the game's quick load). Do not run it while the user plays: it clicks
  into the game window. Prints the WoG log lines of the new session.
#>
param([int]$TimeoutSec = 240)
$ErrorActionPreference = "Stop"
$game = $env:OLDEN_ERA_DIR
if (-not $game) { $game = "G:\SteamLibrary\steamapps\common\Heroes of Might and Magic Olden Era" }
$log = Join-Path $game "BepInEx\LogOutput.log"

function Click([int]$x, [int]$y, [double]$wait) {
    # the game reacts to a hover before the click: move near, a relative step, then click
    um win drive --proc HeroesOldenEra "focus" "move $($x - 2) $($y - 2)" "rel 2 2" "click $x $y" | Out-Null
    Start-Sleep -Milliseconds ([int]($wait * 1000))
}

$p = Get-Process HeroesOldenEra -ErrorAction SilentlyContinue
if (-not $p) {
    um win launch --steam 3105440 | Out-Null
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    while (-not ($p = Get-Process HeroesOldenEra -ErrorAction SilentlyContinue)) {
        if ((Get-Date) -gt $deadline) { throw "the game did not start" }
        Start-Sleep 3
    }
    # the main menu: the process answers and has run long enough for BepInEx and the plugins
    while (-not ($p.Refresh(), $p.Responding)[1] -or ((Get-Date) - $p.StartTime).TotalSeconds -lt 45) {
        if ((Get-Date) -gt $deadline) { throw "the main menu did not appear" }
        Start-Sleep 3
    }
}
$before = @(Get-Content $log).Count
Click 195 250 2     # New game
Click 195 296 3     # Quick start
Click 800 400 3     # Templates
Click 912 280 2     # ARCADE
Click 1290 800 3    # Choose
Click 515 290 3     # Temple
Click 465 205 3     # the first hero
Click 800 821 1     # Start game
$deadline = (Get-Date).AddSeconds($TimeoutSec)
while ((Get-Date) -lt $deadline) {
    $new = @(Get-Content $log) | Select-Object -Skip $before
    if (($new -join "`n") -match "day started") { $new | Select-String "WoG: " | ForEach-Object { $_.Line }; exit 0 }
    Start-Sleep 3
}
throw "no WoG day start within $TimeoutSec s (is the plugin installed, did the menu clicks land?)"
