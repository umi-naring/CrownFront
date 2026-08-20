param(
    [string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.0.34f1\Editor\Unity.exe',
    [int]$AugmentPasses = 3
)

$ErrorActionPreference = 'Stop'
$projectPath = Split-Path -Parent $PSScriptRoot
$workspacePath = Split-Path -Parent $projectPath
$qaDirectory = Join-Path $workspacePath 'tmp\Crownfront-QA-250'
$qaExecutable = Join-Path $qaDirectory 'Crownfront-QA.exe'
$buildLog = Join-Path $workspacePath 'qa-build-250-runner.log'

if (-not (Test-Path -LiteralPath $UnityPath)) {
    throw "Unity executable not found: $UnityPath"
}

$unityArguments = @(
    '-batchmode'
    '-nographics'
    '-projectPath', $projectPath
    '-executeMethod', 'JellyGate.Editor.JellyGateBuild.BuildWindowsQa'
    '-outputPath', $qaExecutable
    '-logFile', $buildLog
    '-quit'
)
$unityProcess = Start-Process -FilePath $UnityPath `
    -ArgumentList $unityArguments -WindowStyle Hidden -Wait -PassThru
if ($unityProcess.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $qaExecutable)) {
    throw "Crownfront QA player build failed. See $buildLog"
}

for ($pass = 1; $pass -le [Math]::Max(1, $AugmentPasses); $pass++) {
    $runtimeLog = Join-Path $workspacePath "qa-augment-250-pass$pass.log"
    $process = Start-Process -FilePath $qaExecutable `
        -ArgumentList @('-batchmode', '-nographics', '-qaAugmentRevamp250', '-logFile', $runtimeLog) `
        -WindowStyle Hidden -Wait -PassThru
    $result = Get-Content -LiteralPath $runtimeLog | Select-String -Pattern 'QA_AUGMENT_REVAMP_250' |
        Select-Object -Last 1
    if ($process.ExitCode -ne 0 -or $null -eq $result) {
        throw "Augment pass $pass failed. See $runtimeLog"
    }
    Write-Output "AUGMENT PASS ${pass}: $($result.Line)"
}

$probes = @(
    @{ Name = 'offer-rules'; Argument = '-qaAugmentRules'; Pattern = 'QA_AUGMENT_RULES' },
    @{ Name = 'hill'; Argument = '-qaHillAugments'; Pattern = 'QA_HILL_AUGMENTS' },
    @{ Name = 'guide'; Argument = '-qaGuide'; Pattern = 'QA_GUIDE' },
    @{ Name = 'release'; Argument = '-qaRelease249'; Pattern = 'QA_RELEASE_249' },
    @{ Name = 'combat'; Argument = '-qaCombatSystems'; Pattern = 'QA_COMBAT_SYSTEMS' },
    @{ Name = 'performance'; Argument = '-qaPerformance240'; Pattern = 'QA_PERFORMANCE_240' }
)

foreach ($probe in $probes) {
    $runtimeLog = Join-Path $workspacePath "qa-250-$($probe.Name)-runner.log"
    $process = Start-Process -FilePath $qaExecutable `
        -ArgumentList @('-batchmode', '-nographics', $probe.Argument, '-logFile', $runtimeLog) `
        -WindowStyle Hidden -Wait -PassThru
    $result = Get-Content -LiteralPath $runtimeLog | Select-String -Pattern $probe.Pattern |
        Select-Object -Last 1
    if ($process.ExitCode -ne 0 -or $null -eq $result) {
        throw "QA probe $($probe.Name) failed. See $runtimeLog"
    }
    Write-Output "PROBE $($probe.Name): $($result.Line)"
}

Write-Output 'Crownfront v2.50.0 augment overhaul QA completed successfully.'
