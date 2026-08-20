param(
    [string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.0.34f1\Editor\Unity.exe',
    [int]$Passes = 3,
    [switch]$Capture
)

$ErrorActionPreference = 'Stop'
$projectPath = Split-Path -Parent $PSScriptRoot
$workspacePath = Split-Path -Parent $projectPath
$qaDirectory = Join-Path $workspacePath 'tmp\Crownfront-QA-237'
$qaExecutable = Join-Path $qaDirectory 'Crownfront-QA.exe'
$buildLog = Join-Path $workspacePath 'qa-build-237-runner.log'

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
    $runtimeLog = Join-Path $workspacePath "qa-release-237-runner-pass$pass.log"
    $process = Start-Process -FilePath $qaExecutable `
        -ArgumentList @('-batchmode', '-nographics', '-qaRelease237', '-logFile', $runtimeLog) `
        -WindowStyle Hidden -Wait -PassThru
    $result = Get-Content -LiteralPath $runtimeLog | Select-String -Pattern 'QA_RELEASE_237' |
        Select-Object -Last 1
    if ($process.ExitCode -ne 0 -or $null -eq $result -or $result.Line -match '=False') {
        throw "QA pass $pass failed. See $runtimeLog"
    }
    Write-Output "PASS ${pass}: $($result.Line)"
}

foreach ($probe in @(
    @{ Name = 'movement'; Argument = '-qaMovementPolish'; Pattern = 'QA_MOVEMENT_POLISH' },
    @{ Name = 'weapon'; Argument = '-qaWeaponConsistency'; Pattern = 'QA_WEAPON_CONSISTENCY' },
    @{ Name = 'combat'; Argument = '-qaCombatPresentation236'; Pattern = 'QA_COMBAT_PRESENTATION_236' },
    @{ Name = 'camera-shake'; Argument = '-qaCameraShakeBudget237'; Pattern = 'QA_CAMERA_SHAKE_BUDGET_237' },
    @{ Name = 'vfx'; Argument = '-qaVfx'; Pattern = 'QA_VFX' },
    @{ Name = 'expanded-map'; Argument = '-qaExpandedMap'; Pattern = 'QA_EXPANDED_MAP' },
    @{ Name = 'navigation'; Argument = '-qaNavigationCombat'; Pattern = 'QA_NAVIGATION_COMBAT' },
    @{ Name = 'enemy-flow'; Argument = '-qaEnemyFlow'; Pattern = 'QA_ENEMY_FLOW' }
)) {
    $runtimeLog = Join-Path $workspacePath "qa-237-$($probe.Name).log"
    $process = Start-Process -FilePath $qaExecutable `
        -ArgumentList @('-batchmode', '-nographics', $probe.Argument, '-logFile', $runtimeLog) `
        -WindowStyle Hidden -Wait -PassThru
    $result = Get-Content -LiteralPath $runtimeLog | Select-String -Pattern $probe.Pattern |
        Select-Object -Last 1
    if ($process.ExitCode -ne 0 -or $null -eq $result -or $result.Line -match '=False') {
        throw "QA probe $($probe.Name) failed. See $runtimeLog"
    }
    Write-Output "PROBE $($probe.Name): $($result.Line)"
}

if ($Capture) {
    $captureLog = Join-Path $workspacePath 'qa-release-237-capture.log'
    $process = Start-Process -FilePath $qaExecutable `
        -ArgumentList @('-qaRelease237Capture', '-screen-width', '540', '-screen-height', '960', '-logFile', $captureLog) `
        -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -ne 0) {
        throw "Release capture failed. See $captureLog"
    }
    Write-Output "Release captures written under $qaDirectory"
}

Write-Output 'Crownfront v2.37.0 QA completed successfully.'
