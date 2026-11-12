param([ValidateSet('EditMode', 'PlayMode')][string]$Platform = 'EditMode')

# shortcut: editor path is fixed, read ProjectVersion.txt when a second machine appears
$unity = 'E:\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe'
$root = Split-Path -Parent $PSScriptRoot
$work = Join-Path ([IO.Path]::GetTempPath()) 'metropolis-unity'
New-Item -ItemType Directory -Force $work | Out-Null
$results = Join-Path $work "$Platform.xml"
$log = Join-Path $work "$Platform.log"
if (Test-Path $results) { Remove-Item $results }

$unityArgs = @(
    '-batchmode', '-nographics', '-runTests', '-testPlatform', $Platform,
    '-projectPath', "`"$root`"", '-testResults', "`"$results`"", '-logFile', "`"$log`""
)
$process = Start-Process -FilePath $unity -ArgumentList $unityArgs -Wait -PassThru

Select-String -Path $log -Pattern 'error CS\d+' |
    ForEach-Object { $_.Line } | Sort-Object -Unique | Select-Object -First 20

if (-not (Test-Path $results)) {
    Write-Output "${Platform}: no results file (Unity exit $($process.ExitCode)). Log: $log"
    exit 1
}

[xml]$xml = Get-Content $results -Raw
$run = $xml.'test-run'
Write-Output "${Platform}: total=$($run.total) passed=$($run.passed) failed=$($run.failed)"
foreach ($case in $xml.SelectNodes("//test-case[@result='Failed']")) {
    Write-Output "  FAILED $($case.fullname)"
    Write-Output "    $($case.failure.message.'#cdata-section')"
}
if ([int]$run.total -eq 0) {
    Write-Output "${Platform}: no tests ran."
    exit 1
}
if ([int]$run.failed -gt 0) { exit 1 }
exit 0
