# Builds Builds\Windows\Metropolis.exe. The Unity Editor must be closed.
# shortcut: editor path is fixed, read ProjectVersion.txt when a second machine appears
$unity = 'E:\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe'
$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $root 'Builds\Windows\Metropolis.exe'
$work = Join-Path ([IO.Path]::GetTempPath()) 'metropolis-unity'
New-Item -ItemType Directory -Force $work | Out-Null
$log = Join-Path $work 'build-windows.log'

$unityArgs = @(
    '-batchmode', '-quit', '-projectPath', "`"$root`"",
    '-executeMethod', 'Metropolis.GameEditor.BuildScript.BuildWindows', '-logFile', "`"$log`""
)
$process = Start-Process -FilePath $unity -ArgumentList $unityArgs -Wait -PassThru

Select-String -Path $log -Pattern 'error CS\d+|BuildScript: |Error building Player|Build Failed' |
    ForEach-Object { $_.Line } | Select-Object -Unique -First 20

if ($process.ExitCode -ne 0 -or -not (Test-Path $exe)) {
    Write-Output "Build failed (Unity exit $($process.ExitCode)). Log: $log"
    exit 1
}

Write-Output "Built: $exe"
exit 0
