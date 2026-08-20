param(
    [string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.0.34f1\Editor\Unity.exe',
    [string]$ProjectPath = (Join-Path $PSScriptRoot '..\unity-jelly-gate'),
    [string]$OutputRoot = (Join-Path $PSScriptRoot '..\qa-logs\v2.71.1-balance-runner'),
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$ProjectPath = (Resolve-Path $ProjectPath).Path
New-Item -ItemType Directory -Force $OutputRoot | Out-Null
$OutputRoot = (Resolve-Path $OutputRoot).Path
$playerRoot = Join-Path (Split-Path $OutputRoot -Parent) 'Crownfront-QA-280'
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
    @{ Name = 'unit-balance-800k'; Argument = '-qaUnitBalance280'; Marker = 'QA_UNIT_BALANCE_280 passed=True' },
    @{ Name = 'combat-stats-ui'; Argument = '-qaCombatStats270'; Marker = 'QA_COMBAT_STATS_270 passed=True' },
    @{ Name = 'augment-runtime'; Argument = '-qaAugmentRuntime264'; Marker = 'QA_AUGMENT_RUNTIME_264 passed=True' },
    @{ Name = 'combat-systems'; Argument = '-qaCombatSystems'; Marker = 'QA_COMBAT_SYSTEMS' }
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
        Passed = ($process.ExitCode -eq 0 -and $markerPassed)
        Log = $log
    }
}

$summaryPath = Join-Path $OutputRoot 'qa-summary.json'
$results | ConvertTo-Json -Depth 4 | Set-Content -Path $summaryPath -Encoding utf8
$results | Format-Table Name, ExitCode, Passed, Log -AutoSize
if ($results.Passed -contains $false) {
    throw "Crownfront v2.71.1 balance QA failed. See $summaryPath"
}
Write-Output "CROWNFRONT_QA_280 PASS: $summaryPath"
