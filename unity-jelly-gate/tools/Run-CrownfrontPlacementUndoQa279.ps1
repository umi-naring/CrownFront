param(
    [string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.0.34f1\Editor\Unity.exe',
    [switch]$ReuseBuild
)

$ErrorActionPreference = 'Stop'
$projectPath = Split-Path -Parent $PSScriptRoot
$workspacePath = Split-Path -Parent $projectPath
$qaDirectory = Join-Path $workspacePath 'qa-artifacts\Crownfront-QA-279'
$qaExecutable = Join-Path $qaDirectory 'Crownfront-QA.exe'
$logDirectory = Join-Path $workspacePath 'qa-logs\v2.71.0-placement-undo'
$buildLog = Join-Path $logDirectory 'windows-player-build.log'
$runtimeLog = Join-Path $logDirectory 'placement-undo.log'
$summaryPath = Join-Path $logDirectory 'qa-summary.json'

if (-not (Test-Path -LiteralPath $UnityPath)) { throw "Unity executable not found: $UnityPath" }
New-Item -ItemType Directory -Path $qaDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null

if (-not $ReuseBuild -or -not (Test-Path -LiteralPath $qaExecutable)) {
    $build = Start-Process -FilePath $UnityPath -ArgumentList @(
        '-batchmode', '-nographics', '-projectPath', $projectPath,
        '-executeMethod', 'JellyGate.Editor.JellyGateBuild.BuildWindowsQa',
        '-outputPath', $qaExecutable, '-logFile', $buildLog, '-quit'
    ) -WindowStyle Hidden -Wait -PassThru
    $errors = @(Select-String -LiteralPath $buildLog -Pattern 'error CS|Scripts have compiler errors|BuildFailedException')
    if ($build.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $qaExecutable) -or $errors.Count -gt 0) {
        throw "Crownfront v2.71.0 QA player build failed. See $buildLog"
    }
}

$run = Start-Process -FilePath $qaExecutable -ArgumentList @(
    '-batchmode', '-nographics', '-qaPlacementUndo279', '-logFile', $runtimeLog
) -WindowStyle Hidden -Wait -PassThru
$marker = Get-Content -LiteralPath $runtimeLog | Select-String -SimpleMatch 'QA_PLACEMENT_UNDO_279 passed=True' |
    Select-Object -Last 1
$exceptions = @(Select-String -LiteralPath $runtimeLog -Pattern 'NullReferenceException|ArgumentException|IndexOutOfRangeException')
$passed = $run.ExitCode -eq 0 -and $null -ne $marker -and $exceptions.Count -eq 0
[ordered]@{
    version='2.71.0'; generatedAt=(Get-Date).ToString('o'); passed=$passed
    exitCode=$run.ExitCode; exceptions=$exceptions.Count
    result=if ($null -ne $marker) { $marker.Line.Trim() } else { '' }
    buildLog=$buildLog; runtimeLog=$runtimeLog
} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $summaryPath -Encoding utf8
if (-not $passed) { throw "Crownfront v2.71.0 placement undo QA failed. See $summaryPath" }
Write-Output "Crownfront v2.71.0 placement undo QA passed. Summary: $summaryPath"
