param(
    [string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.0.34f1\Editor\Unity.exe',
    [switch]$ReuseBuild,
    [switch]$FocusedOnly
)

$ErrorActionPreference = 'Stop'
$projectPath = Split-Path -Parent $PSScriptRoot
$workspacePath = Split-Path -Parent $projectPath
$qaDirectory = Join-Path $workspacePath 'tmp\Crownfront-QA-267'
$qaExecutable = Join-Path $qaDirectory 'Crownfront-QA.exe'
$logDirectory = Join-Path $workspacePath 'qa-logs\v2.67'
$buildLog = Join-Path $logDirectory 'runner-windows-player-build.log'
$summaryPath = Join-Path $logDirectory 'qa-summary.json'

if (-not (Test-Path -LiteralPath $UnityPath)) { throw "Unity executable not found: $UnityPath" }
New-Item -ItemType Directory -Path $qaDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null

if (-not $ReuseBuild -or -not (Test-Path -LiteralPath $qaExecutable)) {
    $arguments = @(
        '-batchmode', '-nographics', '-projectPath', $projectPath,
        '-executeMethod', 'JellyGate.Editor.JellyGateBuild.BuildWindowsQa',
        '-outputPath', $qaExecutable, '-logFile', $buildLog, '-quit'
    )
    $build = Start-Process -FilePath $UnityPath -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
    $compileErrors = if (Test-Path -LiteralPath $buildLog) {
        @(Select-String -LiteralPath $buildLog -Pattern 'error CS|Scripts have compiler errors|BuildFailedException')
    } else { @('missing build log') }
    if ($build.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $qaExecutable) -or $compileErrors.Count -gt 0) {
        throw "Crownfront v2.67 QA player build failed. See $buildLog"
    }
}

$probes = @(
    @{ Name = '01-tank-movement-performance'; Arguments = @('-qaTankMovement267'); Pattern = 'QA_TANK_MOVEMENT_267 passed=True poses=24576 actors=48 metricMisses=0' },
    @{ Name = '02-all-sprite-regression'; Arguments = @('-qaSprite264'); Pattern = 'QA_SPRITE_264 passed=True bossPoses=5760 regularPoses=10176' },
    @{ Name = '03-battlefield-regression'; Arguments = @('-qaBattlefield264'); Pattern = 'QA_BATTLEFIELD_264 passed=True wave=12-88 squad=4-10 burst=2-4 side=0.40' },
    @{ Name = '04-checkpoint-regression'; Arguments = @('-qaCheckpoint266'); Pattern = 'QA_CHECKPOINT_266 write=True battleStable=True restore=True choice=True' }
)
if ($FocusedOnly) { $probes = @($probes[0]) }

$results = @()
foreach ($probe in $probes) {
    $runtimeLog = Join-Path $logDirectory "$($probe.Name).log"
    $arguments = @('-batchmode', '-nographics') + $probe.Arguments + @('-logFile', $runtimeLog)
    $process = Start-Process -FilePath $qaExecutable -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
    $match = Get-Content -LiteralPath $runtimeLog | Select-String -SimpleMatch $probe.Pattern | Select-Object -Last 1
    $exceptions = @(Select-String -LiteralPath $runtimeLog `
        -Pattern 'NullReferenceException|ArgumentException|ArgumentOutOfRangeException|IndexOutOfRangeException')
    $passed = $process.ExitCode -eq 0 -and $null -ne $match -and $exceptions.Count -eq 0
    $line = if ($null -ne $match) { $match.Line.Trim() } else {
        (Get-Content -LiteralPath $runtimeLog | Select-String -Pattern 'QA_.*267|QA_.*266|QA_.*264' |
            Select-Object -Last 1).Line.Trim()
    }
    $results += [ordered]@{
        name = $probe.Name; passed = $passed; exitCode = $process.ExitCode
        exceptions = $exceptions.Count; log = $runtimeLog; result = $line
    }
    Write-Output "$($probe.Name): passed=$passed exit=$($process.ExitCode) exceptions=$($exceptions.Count) $line"
}

$summary = [ordered]@{
    version = '2.67.0'; generatedAt = (Get-Date).ToString('o'); playerBuild = $qaExecutable
    allQaPassed = -not ($results.passed -contains $false); probes = $results
}
$summary | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath $summaryPath -Encoding utf8
if (-not $summary.allQaPassed) { throw "One or more Crownfront v2.67 QA checks failed. See $summaryPath" }
Write-Output "Crownfront v2.67 QA completed. Summary: $summaryPath"
