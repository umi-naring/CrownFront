param(
    [string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.0.34f1\Editor\Unity.exe',
    [int]$Passes = 3
)

$ErrorActionPreference = 'Stop'
$projectPath = Split-Path -Parent $PSScriptRoot
$workspacePath = Split-Path -Parent $projectPath
$qaDirectory = Join-Path $workspacePath 'tmp\Crownfront-QA-249'
$qaExecutable = Join-Path $qaDirectory 'Crownfront-QA.exe'
$buildLog = Join-Path $workspacePath 'qa-build-249-runner.log'

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

for ($pass = 1; $pass -le [Math]::Max(1, $Passes); $pass++) {
    $runtimeLog = Join-Path $workspacePath "qa-release-249-pass$pass.log"
    $process = Start-Process -FilePath $qaExecutable `
        -ArgumentList @('-batchmode', '-nographics', '-qaRelease249', '-logFile', $runtimeLog) `
        -WindowStyle Hidden -Wait -PassThru
    $result = Get-Content -LiteralPath $runtimeLog | Select-String -Pattern 'QA_RELEASE_249' |
        Select-Object -Last 1
    if ($process.ExitCode -ne 0 -or $null -eq $result) {
        throw "QA release pass $pass failed. See $runtimeLog"
    }
    Write-Output "PASS ${pass}: $($result.Line)"
}

$probes = @(
    @{ Name = 'boss-directional'; Argument = '-qaBossDirectional248'; Pattern = 'QA_BOSS_DIRECTIONAL_248 ' },
    @{ Name = 'loading-tips'; Argument = '-qaLoadingTips'; Pattern = 'QA_LOADING_TIPS' },
    @{ Name = 'hill-control'; Argument = '-qaHillAugments'; Pattern = 'QA_HILL_AUGMENTS' },
    @{ Name = 'guide'; Argument = '-qaGuide'; Pattern = 'QA_GUIDE' },
    @{ Name = 'camera-shake'; Argument = '-qaCameraShakeBudget237'; Pattern = 'QA_CAMERA_SHAKE_BUDGET_237' },
    @{ Name = 'combat'; Argument = '-qaCombatSystems'; Pattern = 'QA_COMBAT_SYSTEMS' },
    @{ Name = 'performance'; Argument = '-qaPerformance240'; Pattern = 'QA_PERFORMANCE_240' },
    @{ Name = 'navigation'; Argument = '-qaNavigationCombat'; Pattern = 'QA_NAVIGATION_COMBAT' }
)

foreach ($probe in $probes) {
    $runtimeLog = Join-Path $workspacePath "qa-249-$($probe.Name).log"
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

Write-Output 'Crownfront v2.49.0 QA completed successfully.'
