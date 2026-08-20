param(
    [string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.0.34f1\Editor\Unity.exe',
    [int]$Passes = 2
)

$ErrorActionPreference = 'Stop'
$projectPath = Split-Path -Parent $PSScriptRoot
$workspacePath = Split-Path -Parent $projectPath
$qaDirectory = Join-Path $workspacePath 'tmp\Crownfront-QA-234'
$qaExecutable = Join-Path $qaDirectory 'Crownfront-QA.exe'
$buildLog = Join-Path $workspacePath 'qa-build-234-runner.log'

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
    $runtimeLog = Join-Path $workspacePath "qa-release-234-runner-pass$pass.log"
    $process = Start-Process -FilePath $qaExecutable `
        -ArgumentList @('-batchmode', '-nographics', '-qaRelease234', '-logFile', $runtimeLog) `
        -WindowStyle Hidden -Wait -PassThru
    $result = Get-Content -LiteralPath $runtimeLog | Select-String -Pattern 'QA_RELEASE_234' |
        Select-Object -Last 1
    if ($process.ExitCode -ne 0 -or $null -eq $result -or $result.Line -match '=False') {
        throw "QA pass $pass failed. See $runtimeLog"
    }
    Write-Output "PASS ${pass}: $($result.Line)"
}

Write-Output "Crownfront v2.34.0 QA completed successfully."
