param(
    [string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.0.34f1\Editor\Unity.exe',
    [string]$ProjectPath = (Join-Path $PSScriptRoot '..\unity-jelly-gate'),
    [string]$OutputRoot = (Join-Path $PSScriptRoot '..\qa-logs\v2.70.6-runner'),
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$ProjectPath = (Resolve-Path $ProjectPath).Path
New-Item -ItemType Directory -Force $OutputRoot | Out-Null
$OutputRoot = (Resolve-Path $OutputRoot).Path
$playerRoot = Join-Path (Split-Path $OutputRoot -Parent) 'Crownfront-QA-276'
New-Item -ItemType Directory -Force $playerRoot | Out-Null
$player = Join-Path $playerRoot 'Crownfront-QA.exe'

if (-not $SkipBuild) {
    if (-not (Test-Path $UnityPath)) { throw "Unity executable not found: $UnityPath" }
    $buildLog = Join-Path $OutputRoot '00-build.log'
    $arguments = "-batchmode -nographics -projectPath `"$ProjectPath`" " +
        "-executeMethod JellyGate.Editor.JellyGateBuild.BuildWindowsQa " +
        "-outputPath `"$player`" -logFile `"$buildLog`" -quit"
    $build = Start-Process $UnityPath -ArgumentList $arguments -Wait -PassThru -WindowStyle Hidden
    if ($build.ExitCode -ne 0 -or -not (Test-Path $player) -or
        -not (Select-String -Path $buildLog -Pattern 'Build Finished, Result: Success' -Quiet)) {
        throw "Windows QA build failed. See $buildLog"
    }
}

if (-not (Test-Path $player)) { throw "QA player not found: $player" }
$tests = @(
    @{ Name = 'compact-physical-magic-stats'; Argument = '-qaCombatStats270'; Marker = 'QA_COMBAT_STATS_270 passed=True' },
    @{ Name = 'battlefield-input-layout'; Argument = '-qaBattlefield264'; Marker = 'QA_BATTLEFIELD_264 passed=True' },
    @{ Name = 'all-sprite-regression'; Argument = '-qaSprite264'; Marker = 'QA_SPRITE_264 passed=True' }
)

$results = foreach ($test in $tests) {
    $log = Join-Path $OutputRoot ("10-{0}.log" -f $test.Name)
    $process = Start-Process $player -ArgumentList @(
        '-batchmode', '-nographics', $test.Argument, '-logFile', $log
    ) -Wait -PassThru -WindowStyle Hidden
    $markerPassed = Select-String -Path $log -SimpleMatch $test.Marker -Quiet
    [pscustomobject]@{
        Name = $test.Name
        ExitCode = $process.ExitCode
        Marker = $test.Marker
        Passed = $markerPassed
        Log = $log
    }
}

$summaryPath = Join-Path $OutputRoot 'qa-summary.json'
$results | ConvertTo-Json -Depth 4 | Set-Content -Path $summaryPath -Encoding utf8
$results | Format-Table Name, ExitCode, Passed, Log -AutoSize
if ($results.Passed -contains $false) {
    throw "Crownfront v2.70.6 QA failed. See $summaryPath"
}
Write-Output "CROWNFRONT_QA_276 PASS: $summaryPath"
