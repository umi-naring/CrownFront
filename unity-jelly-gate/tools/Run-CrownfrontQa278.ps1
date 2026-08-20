param(
    [string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.0.34f1\Editor\Unity.exe',
    [switch]$ReuseBuild
)

$ErrorActionPreference = 'Stop'
$projectPath = Split-Path -Parent $PSScriptRoot
$workspacePath = Split-Path -Parent $projectPath
$qaDirectory = Join-Path $workspacePath 'qa-artifacts\Crownfront-QA-278'
$qaExecutable = Join-Path $qaDirectory 'Crownfront-QA.exe'
$logDirectory = Join-Path $workspacePath 'qa-logs\v2.70.8-runner'
$buildLog = Join-Path $logDirectory 'windows-player-build.log'
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
    $build = Start-Process -FilePath $UnityPath -ArgumentList $arguments `
        -WindowStyle Hidden -Wait -PassThru
    $errors = @(Select-String -LiteralPath $buildLog `
        -Pattern 'error CS|Scripts have compiler errors|BuildFailedException')
    if ($build.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $qaExecutable) -or $errors.Count -gt 0) {
        throw "Crownfront v2.70.8 QA player build failed. See $buildLog"
    }
}

$probes = @(
    @{ Name='01-boss-grounding-direction-seam'; Argument='-qaBossGrounding278'; Marker='QA_BOSS_GROUNDING_278 passed=True' },
    @{ Name='02-every-enemy-state'; Argument='-qaEnemyPresentation269'; Marker='QA_ENEMY_PRESENTATION_269 passed=True profiles=53 regularPoses=61056/61056 bossPoses=11520/11520 transitions=2520/2520' },
    @{ Name='03-enemy-pursuit'; Argument='-qaEnemyPursuit277'; Marker='QA_ENEMY_PURSUIT_277 passed=True profiles=53 reachable=53/53 unreachable=48/48 corridor=9/9 boss=10/10 facing=' }
)

$results = @()
foreach ($probe in $probes) {
    $runtimeLog = Join-Path $logDirectory ($probe.Name + '.log')
    $process = Start-Process -FilePath $qaExecutable -ArgumentList @(
        '-batchmode', '-nographics', $probe.Argument, '-logFile', $runtimeLog
    ) -WindowStyle Hidden -Wait -PassThru
    $match = Get-Content -LiteralPath $runtimeLog |
        Select-String -SimpleMatch $probe.Marker | Select-Object -Last 1
    $exceptions = @(Select-String -LiteralPath $runtimeLog `
        -Pattern 'NullReferenceException|ArgumentException|ArgumentOutOfRangeException|IndexOutOfRangeException')
    $passed = $process.ExitCode -eq 0 -and $null -ne $match -and $exceptions.Count -eq 0
    $results += [ordered]@{
        name=$probe.Name; passed=$passed; exitCode=$process.ExitCode
        exceptions=$exceptions.Count
        result=if ($null -ne $match) { $match.Line.Trim() } else { '' }
        runtimeLog=$runtimeLog
    }
    Write-Output "$($probe.Name): passed=$passed exit=$($process.ExitCode) exceptions=$($exceptions.Count)"
    Get-Process 'UnityCrashHandler64' -ErrorAction SilentlyContinue |
        Wait-Process -Timeout 5 -ErrorAction SilentlyContinue
}

$passed = -not ($results.passed -contains $false)
[ordered]@{
    version='2.70.8'; generatedAt=(Get-Date).ToString('o'); passed=$passed
    buildLog=$buildLog; probes=$results
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $summaryPath -Encoding utf8
if (-not $passed) { throw "Crownfront v2.70.8 boss presentation QA failed. See $summaryPath" }
Write-Output "Crownfront v2.70.8 QA completed. Summary: $summaryPath"
