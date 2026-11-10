# Launches the built game, which captures two screenshots and quits, then checks its log.
$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $root 'Builds\Windows\Metropolis.exe'
if (-not (Test-Path $exe)) {
    Write-Output "Player is missing: $exe"
    exit 1
}

$work = Join-Path ([IO.Path]::GetTempPath()) 'metropolis-player'
if (Test-Path $work) { Remove-Item $work -Recurse -Force }
New-Item -ItemType Directory -Force $work | Out-Null
$log = Join-Path $work 'player.log'

$playerArgs = @(
    '-capture', "`"$work`"", '-logFile', "`"$log`"",
    '-screen-fullscreen', '0', '-screen-width', '1600', '-screen-height', '900'
)
$process = Start-Process -FilePath $exe -ArgumentList $playerArgs -PassThru
if (-not $process.WaitForExit(120000)) {
    Stop-Process -Id $process.Id -Force
    Write-Output 'Player did not exit within 120 seconds.'
    exit 1
}

$failed = $false
foreach ($image in 'menu.png', 'hud.png') {
    if (-not (Test-Path (Join-Path $work $image))) {
        Write-Output "Missing screenshot: $image"
        $failed = $true
    }
}

if (-not (Test-Path $log)) {
    Write-Output 'Player wrote no log.'
    exit 1
}

$problems = Select-String -Path $log -Pattern 'Exception|GameController:|GameUi:' |
    ForEach-Object { $_.Line } | Select-Object -Unique -First 10
foreach ($line in $problems) {
    Write-Output "LOG: $line"
    $failed = $true
}

if ($failed) { exit 1 }
Write-Output "Player OK: $work\menu.png and $work\hud.png"
exit 0
