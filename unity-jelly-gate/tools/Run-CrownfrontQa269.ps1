param(
    [string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.0.34f1\Editor\Unity.exe',
    [switch]$ReuseBuild
)

$ErrorActionPreference = 'Stop'
$projectPath = Split-Path -Parent $PSScriptRoot
$workspacePath = Split-Path -Parent $projectPath
$qaDirectory = Join-Path $workspacePath 'qa-artifacts\Crownfront-QA-269'
$qaExecutable = Join-Path $qaDirectory 'Crownfront-QA.exe'
$logDirectory = Join-Path $workspacePath 'qa-logs\v2.69'
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
        throw "Crownfront v2.69 QA player build failed. See $buildLog"
    }
}

$probes = @(
    @{ Name = '01-every-enemy-state'; Arguments = @('-qaEnemyPresentation269', '-qaExportEnemyFrames269'); Pattern = 'QA_ENEMY_PRESENTATION_269 passed=True profiles=53 regularPoses=61056/61056 bossPoses=11520/11520 transitions=2520/2520' },
    @{ Name = '02-legacy-sprite-regression'; Arguments = @('-qaSprite264'); Pattern = 'QA_SPRITE_264 passed=True bossPoses=5760 regularPoses=10176' },
    @{ Name = '03-jelly-identity-regression'; Arguments = @('-qaJellyIdentity268'); Pattern = 'QA_JELLY_IDENTITY_268 ready=True mage=front/rear bomber=front/rear' }
)
$results = @()
foreach ($probe in $probes) {
    $runtimeLog = Join-Path $logDirectory "$($probe.Name).log"
    $arguments = @('-batchmode', '-nographics') + $probe.Arguments + @('-logFile', $runtimeLog)
    $process = Start-Process -FilePath $qaExecutable -ArgumentList $arguments `
        -WindowStyle Hidden -Wait -PassThru
    $match = Get-Content -LiteralPath $runtimeLog |
        Select-String -SimpleMatch $probe.Pattern | Select-Object -Last 1
    $exceptions = @(Select-String -LiteralPath $runtimeLog `
        -Pattern 'NullReferenceException|ArgumentException|ArgumentOutOfRangeException|IndexOutOfRangeException')
    $passed = $process.ExitCode -eq 0 -and $null -ne $match -and $exceptions.Count -eq 0
    $results += [ordered]@{
        name = $probe.Name; passed = $passed; exitCode = $process.ExitCode
        exceptions = $exceptions.Count
        result = if ($null -ne $match) { $match.Line.Trim() } else { '' }
        runtimeLog = $runtimeLog
    }
    Write-Output "$($probe.Name): passed=$passed exit=$($process.ExitCode) exceptions=$($exceptions.Count) $($results[-1].result)"
    # Unity player can keep its crash handler/file handles alive for a fraction of a second
    # after the main process exits. Wait for that helper before launching the next probe so the
    # following log is never created as an empty, locked file on Windows.
    Get-Process 'UnityCrashHandler64' -ErrorAction SilentlyContinue |
        Wait-Process -Timeout 5 -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 350
}
$passed = -not ($results.passed -contains $false)
$summary = [ordered]@{
    version = '2.69.0'; generatedAt = (Get-Date).ToString('o'); passed = $passed
    buildLog = $buildLog; probes = $results
}
$summary | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $summaryPath -Encoding utf8
if (-not $passed) { throw "Crownfront v2.69 enemy presentation QA failed. See $summaryPath" }
Write-Output "Crownfront v2.69 QA completed. Summary: $summaryPath"
