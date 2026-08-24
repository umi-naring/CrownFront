param(
    [string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.0.82f1\Editor\Unity.exe',
    [switch]$ReuseBuild
)

$ErrorActionPreference = 'Stop'
$projectPath = Split-Path -Parent $PSScriptRoot
$workspacePath = Split-Path -Parent $projectPath
$qaDirectory = Join-Path $workspacePath 'qa-artifacts\Crownfront-Spawn-Pool-310'
$qaExecutable = Join-Path $qaDirectory 'Crownfront-QA.exe'
$logDirectory = Join-Path $workspacePath 'qa-logs\spawn-pool-310'
$buildLog = Join-Path $logDirectory 'windows-player-build.log'
$runtimeLog = Join-Path $logDirectory 'spawn-pool-310.log'
$summaryPath = Join-Path $logDirectory 'qa-summary.json'
New-Item -ItemType Directory -Path $qaDirectory, $logDirectory -Force | Out-Null
$env:USERPROFILE = 'C:\Users\Administrator'
$env:LOCALAPPDATA = 'C:\Users\Administrator\AppData\Local'
$env:BEE_CACHE_DIRECTORY = Join-Path $workspacePath 'qa-artifacts\bee-cache'
New-Item -ItemType Directory -Path $env:BEE_CACHE_DIRECTORY -Force | Out-Null

if (-not $ReuseBuild -or -not (Test-Path -LiteralPath $qaExecutable)) {
    $buildArguments = @(
        '-batchmode', '-nographics', '-noUpm', '-projectPath', $projectPath,
        '-executeMethod', 'JellyGate.Editor.JellyGateBuild.BuildWindowsQa',
        '-outputPath', $qaExecutable, '-logFile', $buildLog, '-quit'
    )
    $buildProcess = Start-Process -FilePath $UnityPath -ArgumentList $buildArguments `
        -WindowStyle Hidden -Wait -PassThru
    $buildSucceeded = $buildProcess.ExitCode -eq 0 -and
        (Test-Path -LiteralPath $qaExecutable) -and
        $null -ne (Select-String -LiteralPath $buildLog -SimpleMatch 'Build Finished, Result: Success.' | Select-Object -Last 1) -and
        @(Select-String -LiteralPath $buildLog -Pattern 'error CS|Scripts have compiler errors|BuildFailedException').Count -eq 0
    if (-not $buildSucceeded) { throw "CROWNFRONT spawn-pool QA build failed. See $buildLog" }
}

$process = Start-Process -FilePath $qaExecutable -ArgumentList @(
    '-batchmode', '-nographics', '-qaSpawnPool310', '-logFile', $runtimeLog
) -WindowStyle Hidden -Wait -PassThru
$match = Get-Content -LiteralPath $runtimeLog |
    Select-String -SimpleMatch 'QA_SPAWN_POOL_310 passed=True' | Select-Object -Last 1
$exceptions = @(Select-String -LiteralPath $runtimeLog -Pattern `
    'NullReferenceException|ArgumentException|IndexOutOfRangeException')
$passed = $process.ExitCode -eq 0 -and $null -ne $match -and $exceptions.Count -eq 0
[ordered]@{
    generatedAt=(Get-Date).ToString('o'); passed=$passed; exitCode=$process.ExitCode;
    exceptions=$exceptions.Count; result=if ($match) { $match.Line.Trim() } else { '' };
    buildLog=$buildLog; runtimeLog=$runtimeLog
} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $summaryPath -Encoding utf8
if (-not $passed) { throw "CROWNFRONT spawn-pool QA failed. See $summaryPath" }
Write-Output "CROWNFRONT spawn-pool QA completed: $summaryPath"
