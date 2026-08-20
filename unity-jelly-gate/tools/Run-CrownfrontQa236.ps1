param(
    [string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.0.34f1\Editor\Unity.exe',
    [int]$Passes = 3,
    [switch]$CaptureCombat
)

$ErrorActionPreference = 'Stop'
$projectPath = Split-Path -Parent $PSScriptRoot
$workspacePath = Split-Path -Parent $projectPath
$qaDirectory = Join-Path $workspacePath 'tmp\Crownfront-QA-236'
$qaExecutable = Join-Path $qaDirectory 'Crownfront-QA.exe'
$buildLog = Join-Path $workspacePath 'qa-build-236-runner.log'

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
    $runtimeLog = Join-Path $workspacePath "qa-release-236-runner-pass$pass.log"
    $process = Start-Process -FilePath $qaExecutable `
        -ArgumentList @('-batchmode', '-nographics', '-qaRelease236', '-logFile', $runtimeLog) `
        -WindowStyle Hidden -Wait -PassThru
    $result = Get-Content -LiteralPath $runtimeLog | Select-String -Pattern 'QA_RELEASE_236' |
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
    @{ Name = 'vfx'; Argument = '-qaVfx'; Pattern = 'QA_VFX' }
)) {
    $runtimeLog = Join-Path $workspacePath "qa-236-$($probe.Name).log"
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

if ($CaptureCombat) {
    $captureLog = Join-Path $workspacePath 'qa-release-236-capture.log'
    $process = Start-Process -FilePath $qaExecutable `
        -ArgumentList @('-batchmode', '-qaRelease236Capture', '-logFile', $captureLog) `
        -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -ne 0) {
        throw "Combat capture failed. See $captureLog"
    }
    Write-Output "Combat frames captured in $qaDirectory"
}

Write-Output "Crownfront v2.36.0 QA completed successfully."
