<#
  Сбор данных об установке Olden Era для порта WoG — ТОЛЬКО ЧТЕНИЕ.
  Ничего не меняет в папке игры и в сейвах; ничего не отправляет в сеть.

  Запуск (Windows PowerShell 5.1 или PowerShell 7):
    powershell -ExecutionPolicy Bypass -File tools\oe-recon\collect.ps1
  Необязательно:
    -GamePath "D:\Steam\steamapps\common\Heroes of Might and Magic Olden Era"
    -SkipProbe        не запускать WoG.ErmTool probe-symbols (нужен .NET 8 SDK)

  Результат: oe-recon-<дата>.zip на рабочем столе. Пришлите его в чат.
  Внутри — имена, id и числа из файлов игры и логов; в публичный репозиторий это не коммитится.
#>
param(
    [string]$GamePath = "",
    [switch]$SkipProbe
)

$ErrorActionPreference = "Continue"
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$out = Join-Path $env:TEMP "oe-recon-$stamp"
New-Item -ItemType Directory -Path $out -Force | Out-Null
$report = Join-Path $out "report.txt"
function Say([string]$t) { Write-Host $t; Add-Content -Path $report -Value $t -Encoding UTF8 }

Say "=== Olden Era recon $stamp ==="
Say ("PowerShell " + $PSVersionTable.PSVersion + " | " + [Environment]::OSVersion.VersionString)

# ---------------------------------------------------------------- 1. Найти игру
$AppId = "3105440"   # Steam App ID Olden Era
function Test-GameDir([string]$d) {
    if (-not $d -or -not (Test-Path $d)) { return $false }
    return [bool](Get-ChildItem -Path $d -Directory -Filter "*_Data" -ErrorAction SilentlyContinue |
        Where-Object { Test-Path (Join-Path $_.FullName "StreamingAssets") })
}
function Find-Game {
    $libs = @()
    try {
        $steam = (Get-ItemProperty -Path "HKCU:\Software\Valve\Steam" -Name SteamPath -ErrorAction Stop).SteamPath
        if ($steam) {
            $steam = $steam -replace "/", "\"
            $libs += $steam
            $vdf = Join-Path $steam "steamapps\libraryfolders.vdf"
            if (Test-Path $vdf) {
                foreach ($m in [regex]::Matches((Get-Content $vdf -Raw), '"path"\s+"([^"]+)"')) {
                    $libs += ($m.Groups[1].Value -replace "\\\\", "\")
                }
            }
        }
    } catch { }
    $libs += @("C:\Program Files (x86)\Steam", "C:\Program Files\Steam", "D:\Steam", "D:\SteamLibrary", "E:\SteamLibrary", "F:\SteamLibrary")
    $candidates = @()
    foreach ($lib in $libs | Select-Object -Unique) {
        # точное имя папки — из манифеста Steam (installdir)
        $acf = Join-Path $lib "steamapps\appmanifest_$AppId.acf"
        if (Test-Path $acf) {
            $m = [regex]::Match((Get-Content $acf -Raw), '"installdir"\s+"([^"]+)"')
            if ($m.Success) { $candidates += (Join-Path $lib ("steamapps\common\" + $m.Groups[1].Value)) }
        }
        $common = Join-Path $lib "steamapps\common"
        if (Test-Path $common) {
            $candidates += (Get-ChildItem -Path $common -Directory -ErrorAction SilentlyContinue |
                Where-Object { $_.Name -like "*Olden*" } | ForEach-Object { $_.FullName })
        }
    }
    foreach ($c in $candidates | Select-Object -Unique) {
        if (Test-GameDir $c) { return $c }
    }
    return $null
}

if (-not $GamePath) { $GamePath = Find-Game }
if (-not (Test-GameDir $GamePath)) {
    Say "ОШИБКА: папка игры не найдена. Запустите снова с -GamePath ""путь\к\папке\игры"" (там, где лежит .exe)."
    exit 1
}
Say "Игра: $GamePath"
$data = (Get-ChildItem -Path $GamePath -Directory -Filter "*_Data" |
    Where-Object { Test-Path (Join-Path $_.FullName "StreamingAssets") } | Select-Object -First 1).FullName
Say "Папка данных: $data"

# ---------------------------------------------------------------- 2. Движок
Say ""
Say "--- Движок ---"
Get-ChildItem -Path $GamePath -File | Select-Object Name, Length, LastWriteTime |
    Format-Table -AutoSize | Out-String -Width 200 | ForEach-Object { Say $_ }
$up = Join-Path $GamePath "UnityPlayer.dll"
if (Test-Path $up) { Say ("UnityPlayer.dll версия: " + (Get-Item $up).VersionInfo.FileVersion) }
Say ("IL2CPP (GameAssembly.dll): " + (Test-Path (Join-Path $GamePath "GameAssembly.dll")))
Say ("global-metadata.dat: " + (Test-Path (Join-Path $data "il2cpp_data\Metadata\global-metadata.dat")))
Say ("Mono (Managed\Assembly-CSharp.dll): " + (Test-Path (Join-Path $data "Managed\Assembly-CSharp.dll")))
$appInfo = Join-Path $data "app.info"
$company = $null; $product = $null
if (Test-Path $appInfo) {
    $lines = Get-Content $appInfo
    $company = $lines[0]; $product = $lines[1]
    Say "app.info: $company / $product"
}
$sa = Join-Path $data "StreamingAssets"
if (Test-Path $sa) {
    Say "StreamingAssets:"
    Get-ChildItem -Path $sa | Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize | Out-String -Width 200 | ForEach-Object { Say $_ }
}

# ---------------------------------------------------------------- 3. BepInEx
Say ""
Say "--- BepInEx ---"
$bep = Join-Path $GamePath "BepInEx"
Say ("Папка BepInEx: " + (Test-Path $bep))
Say ("winhttp.dll (doorstop): " + (Test-Path (Join-Path $GamePath "winhttp.dll")))
if (Test-Path $bep) {
    $core = Join-Path $bep "core"
    Get-ChildItem -Path $core -Filter "BepInEx*.dll" -ErrorAction SilentlyContinue |
        ForEach-Object { Say ($_.Name + " " + $_.VersionInfo.FileVersion + " " + $_.VersionInfo.ProductVersion) }
    $interop = Join-Path $bep "interop"
    if (Test-Path $interop) {
        $dlls = Get-ChildItem -Path $interop -Filter *.dll
        Say ("interop: " + $dlls.Count + " сборок")
        $dlls | Select-Object Name, Length | Format-Table -AutoSize | Out-String -Width 200 | Set-Content -Path (Join-Path $out "interop-list.txt") -Encoding UTF8
    } else { Say "interop: нет (BepInEx ещё не запускался с игрой)" }
    Get-ChildItem -Path (Join-Path $bep "plugins") -Recurse -File -ErrorAction SilentlyContinue |
        ForEach-Object { Say ("plugin: " + $_.FullName.Substring($bep.Length)) }
    $log = Join-Path $bep "LogOutput.log"
    if (Test-Path $log) { Copy-Item $log (Join-Path $out "LogOutput.log") }
}

# ---------------------------------------------------------------- 4. Core.zip
Say ""
Say "--- Core.zip ---"
$coreZip = Join-Path $sa "Core.zip"
if (Test-Path $coreZip) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [System.IO.Compression.ZipFile]::OpenRead($coreZip)
    try {
        Say ("Записей: " + $zip.Entries.Count + ", размер " + (Get-Item $coreZip).Length)
        # дерево папок (3 уровня) с числом файлов
        $zip.Entries | ForEach-Object {
            $p = $_.FullName.Split('/')
            if ($p.Length -gt 1) { ($p[0..([Math]::Min(3, $p.Length - 2))] -join '/') }
        } | Group-Object | Sort-Object Name | ForEach-Object { "{0,6}  {1}" -f $_.Count, $_.Name } |
            Set-Content -Path (Join-Path $out "corezip-tree.txt") -Encoding UTF8

        function Read-Entry($e) {
            $sr = New-Object System.IO.StreamReader($e.Open(), [Text.Encoding]::UTF8, $true)
            try { return $sr.ReadToEnd() } finally { $sr.Dispose() }
        }
        function Parse-Json([string]$t) {
            # ConvertFrom-Json в PS 5.1 не понимает комментарии — убираем строки //
            $t = [regex]::Replace($t, '(?m)^\s*//.*$', '')
            return ($t | ConvertFrom-Json)
        }
        function As-List($node) {
            if ($null -eq $node) { return @() }
            if ($node -is [System.Array]) { return $node }
            if ($node.PSObject.Properties.Name -contains 'array') { return $node.array }
            return @($node)
        }

        # юниты: id, фракция, уровень, основные статы
        $units = New-Object System.Collections.Generic.List[object]
        $bad = New-Object System.Collections.Generic.List[string]
        $sampleSaved = $false
        foreach ($e in $zip.Entries | Where-Object { $_.FullName -like "DB/units/units_logics/*.json" }) {
            try {
                $text = Read-Entry $e
                if (-not $sampleSaved) { Set-Content -Path (Join-Path $out "sample-unit.json") -Value $text -Encoding UTF8; $sampleSaved = $true }
                foreach ($u in (As-List (Parse-Json $text))) {
                    if (-not $u.id) { continue }
                    $s = $u.stats
                    $units.Add([pscustomobject]@{
                        file = $e.FullName; id = $u.id; fraction = $u.fraction; tier = $u.tier
                        hp = $s.hp; offence = $s.offence; defence = $s.defence; damageMin = $s.damageMin; damageMax = $s.damageMax
                        initiative = $s.initiative; speed = $s.speed; numCounters = $s.numCounters; actionPoints = $s.actionPoints
                    })
                }
            } catch { $bad.Add($e.FullName + " : " + $_.Exception.Message) }
        }
        $units | Export-Csv -Path (Join-Path $out "units.csv") -NoTypeInformation -Encoding UTF8
        Say ("Юнитов: " + $units.Count + $(if ($bad.Count) { ", не разобрано файлов: " + $bad.Count } else { "" }))

        # герои
        $heroes = New-Object System.Collections.Generic.List[object]
        foreach ($e in $zip.Entries | Where-Object { $_.FullName -like "DB/heroes/*.json" }) {
            try {
                foreach ($h in (As-List (Parse-Json (Read-Entry $e)))) {
                    if (-not $h.id) { continue }
                    $heroes.Add([pscustomobject]@{ file = $e.FullName; id = $h.id; fraction = $h.fraction; classType = $h.classType; specialization = $h.specialization })
                }
            } catch { $bad.Add($e.FullName + " : " + $_.Exception.Message) }
        }
        $heroes | Export-Csv -Path (Join-Path $out "heroes.csv") -NoTypeInformation -Encoding UTF8
        Say ("Героев: " + $heroes.Count)

        # артефакты, навыки, заклинания, баффы — только id и пара полей
        foreach ($spec in @(
            @{ glob = "DB/items/items/*.json"; name = "items.csv"; f = { param($x) [pscustomobject]@{ id = $x.id; slot = $x.slot_; rarity = $x.rarity } } },
            @{ glob = "DB/heroes_skills/skills/*.json"; name = "skills.csv"; f = { param($x) [pscustomobject]@{ id = $x.id } } },
            @{ glob = "DB/magics/*.json"; name = "spells.csv"; f = { param($x) [pscustomobject]@{ id = $x.id; level = $x.level; school = $x.school } } },
            @{ glob = "DB/buffs/*.json"; name = "buffs.csv"; f = { param($x) [pscustomobject]@{ id = $x.id } } }
        )) {
            $rows = New-Object System.Collections.Generic.List[object]
            foreach ($e in $zip.Entries | Where-Object { $_.FullName -like $spec.glob }) {
                try { foreach ($x in (As-List (Parse-Json (Read-Entry $e)))) { if ($x.id) { $r = & $spec.f $x; $r | Add-Member -NotePropertyName file -NotePropertyValue $e.FullName; $rows.Add($r) } } }
                catch { $bad.Add($e.FullName + " : " + $_.Exception.Message) }
            }
            $rows | Export-Csv -Path (Join-Path $out $spec.name) -NoTypeInformation -Encoding UTF8
            Say ($spec.name + ": " + $rows.Count)
        }
        if ($bad.Count) { $bad | Set-Content -Path (Join-Path $out "parse-errors.txt") -Encoding UTF8 }
    } finally { $zip.Dispose() }
} else { Say "Core.zip не найден в $sa" }

# ---------------------------------------------------------------- 5. Сейвы и логи (только имена/размеры/заголовок)
Say ""
Say "--- Сейвы и логи ---"
$lowRoots = @()
if ($company -and $product) { $lowRoots += (Join-Path $env:USERPROFILE "AppData\LocalLow\$company\$product") }
$lowRoots += (Get-ChildItem -Path (Join-Path $env:USERPROFILE "AppData\LocalLow") -Directory -ErrorAction SilentlyContinue |
    ForEach-Object { Get-ChildItem $_.FullName -Directory -ErrorAction SilentlyContinue } |
    Where-Object { $_.Name -like "*Olden*" -or $_.Name -like "*Heroes*" } | ForEach-Object { $_.FullName })
foreach ($root in $lowRoots | Select-Object -Unique) {
    if (-not $root -or -not (Test-Path $root)) { continue }
    Say "Папка: $root"
    Get-ChildItem -Path $root -Recurse -File -ErrorAction SilentlyContinue | Select-Object -First 300 | ForEach-Object {
        $hdr = ""
        try {
            $fs = [IO.File]::OpenRead($_.FullName)
            $buf = New-Object byte[] 16
            $n = $fs.Read($buf, 0, 16); $fs.Dispose()
            if ($n -gt 0) { $hdr = ($buf[0..($n - 1)] | ForEach-Object { $_.ToString("X2") }) -join " " }
        } catch { }
        "{0,10}  {1}  {2}  [{3}]" -f $_.Length, $_.LastWriteTime.ToString("yyyy-MM-dd HH:mm"), $_.FullName.Substring($root.Length), $hdr
    } | Set-Content -Path (Join-Path $out ("files-" + (Split-Path $root -Leaf) + ".txt")) -Encoding UTF8
    foreach ($log in @("Player.log", "Player-prev.log")) {
        $p = Join-Path $root $log
        if (Test-Path $p) { Copy-Item $p (Join-Path $out $log) }
    }
}

# ---------------------------------------------------------------- 6. Символы (нужен BepInEx interop + .NET 8 SDK)
if (-not $SkipProbe) {
    Say ""
    Say "--- probe-symbols ---"
    $interop = Join-Path $GamePath "BepInEx\interop"
    $tool = Join-Path $PSScriptRoot "..\WoG.ErmTool"
    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if (-not (Test-Path (Join-Path $tool "WoG.ErmTool.csproj"))) { Say "пропущено: скрипт запущен не из клона репозитория (нет tools\WoG.ErmTool)" }
    elseif (-not (Test-Path $interop)) { Say "пропущено: нет BepInEx\interop (поставьте BepInEx 6 IL2CPP be.785 и запустите игру один раз)" }
    elseif (-not $dotnet) { Say "пропущено: нет dotnet (установите .NET 8 SDK: https://dotnet.microsoft.com/download)" }
    else {
        & dotnet run --project $tool -- probe-symbols $interop *> (Join-Path $out "symbols.txt")
        Say ("symbols.txt: " + (Get-Item (Join-Path $out "symbols.txt")).Length + " байт")
    }
}

# ---------------------------------------------------------------- 7. Упаковать
$zipOut = Join-Path ([Environment]::GetFolderPath("Desktop")) "oe-recon-$stamp.zip"
Compress-Archive -Path (Join-Path $out "*") -DestinationPath $zipOut -Force
Say ""
Say "ГОТОВО: $zipOut"
Say "Пришлите этот файл в чат. Папку игры и сейвы скрипт не менял."
