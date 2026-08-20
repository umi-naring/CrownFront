param(
    [string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.0.34f1\Editor\Unity.exe',
    [switch]$ReuseBuild,
    [switch]$FocusedOnly
)

$ErrorActionPreference = 'Stop'
$projectPath = Split-Path -Parent $PSScriptRoot
$workspacePath = Split-Path -Parent $projectPath
$qaDirectory = Join-Path $workspacePath 'tmp\Crownfront-QA-263'
$qaExecutable = Join-Path $qaDirectory 'Crownfront-QA.exe'
$logDirectory = Join-Path $workspacePath 'qa-logs\v2.63'
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
    $build = Start-Process -FilePath $UnityPath -ArgumentList $arguments `
        -WindowStyle Hidden -Wait -PassThru
    $compileErrors = if (Test-Path -LiteralPath $buildLog) {
        @(Select-String -LiteralPath $buildLog -Pattern 'error CS|Scripts have compiler errors|BuildFailedException')
    } else { @('missing build log') }
    if ($build.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $qaExecutable) -or $compileErrors.Count -gt 0) {
        throw "Crownfront v2.63 QA player build failed. See $buildLog"
    }
}

$probes = @(
    @{
        Name = '01-exhaustive-sprite-runtime';
        Arguments = @('-qaRelease263', '-qaExportBossFrames');
        Pattern = 'QA_RELEASE_263 passed=True bosses=10 poses=5760'
    },
    @{ Name = '02-chairman-regression'; Arguments = @('-qaRelease262'); Pattern = 'QA_RELEASE_262 passed=True' },
    @{ Name = '03-all-round-runtime'; Arguments = @('-qaRelease261'); Pattern = 'QA_RELEASE_261 passed=True' },
    @{ Name = '04-all-unit-directions'; Arguments = @('-qaUltimateSpriteAudit256'); Pattern = 'QA_ULTIMATE_SPRITE_AUDIT_256 passed=True' },
    @{ Name = '05-guide-modal'; Arguments = @('-qaGuide'); Pattern = 'QA_GUIDE modal=True tabs=True' }
)
if ($FocusedOnly) { $probes = @($probes[0]) }

$results = @()
foreach ($probe in $probes) {
    $runtimeLog = Join-Path $logDirectory "$($probe.Name).log"
    $arguments = @('-batchmode', '-nographics') + $probe.Arguments + @('-logFile', $runtimeLog)
    $process = Start-Process -FilePath $qaExecutable -ArgumentList $arguments `
        -WindowStyle Hidden -Wait -PassThru
    $match = Get-Content -LiteralPath $runtimeLog | Select-String -SimpleMatch $probe.Pattern |
        Select-Object -Last 1
    $exceptions = @(Select-String -LiteralPath $runtimeLog `
        -Pattern 'NullReferenceException|ArgumentException|ArgumentOutOfRangeException|IndexOutOfRangeException')
    $passed = $process.ExitCode -eq 0 -and $null -ne $match -and $exceptions.Count -eq 0
    $line = if ($null -ne $match) { $match.Line.Trim() } else { '' }
    $results += [ordered]@{
        name = $probe.Name; arguments = $probe.Arguments; passed = $passed
        exitCode = $process.ExitCode; exceptions = $exceptions.Count; log = $runtimeLog; result = $line
    }
    Write-Output "$($probe.Name): passed=$passed exit=$($process.ExitCode) exceptions=$($exceptions.Count) $line"
}

$bossRuntime = Join-Path $qaDirectory 'qa-exported-boss-frames-v263\runtime'
$jellyRuntime = Join-Path $qaDirectory 'qa-exported-jelly-frames-v263\runtime'
$guideRuntime = Join-Path $qaDirectory 'qa-exported-guide-portraits-v263'
$artifactChecks = [ordered]@{
    bossRuntimeFrames = @(Get-ChildItem -LiteralPath $bossRuntime -Filter '*.bmp').Count
    jellyMageDirections = @(Get-ChildItem -LiteralPath $jellyRuntime -Filter '*.bmp').Count
    guideBossPortraits = @(Get-ChildItem -LiteralPath $guideRuntime -Filter '*.bmp').Count
}
$artifactChecksPassed = $artifactChecks.bossRuntimeFrames -eq 895 -and
    $artifactChecks.jellyMageDirections -eq 8 -and $artifactChecks.guideBossPortraits -eq 10

& (Join-Path $PSScriptRoot 'Build-CrownfrontSpriteContactSheets263.ps1') | Out-Null
$contactRoot = Join-Path $workspacePath 'qa-artifacts\v2.63\boss-contact-sheets-runtime'
$artifactChecks.bossContactSheets = @(Get-ChildItem -LiteralPath $contactRoot -Filter 'boss-*-contact-sheet-v263.png').Count
$artifactChecksPassed = $artifactChecksPassed -and $artifactChecks.bossContactSheets -eq 10

$summary = [ordered]@{
    version = '2.63.0'; generatedAt = (Get-Date).ToString('o'); playerBuild = $qaExecutable
    allQaPassed = -not ($results.passed -contains $false) -and $artifactChecksPassed
    artifacts = $artifactChecks; probes = $results
}
$summary | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath $summaryPath -Encoding utf8
if (-not $summary.allQaPassed) { throw "One or more Crownfront v2.63 QA checks failed. See $summaryPath" }
Write-Output "Crownfront v2.63 QA completed. Summary: $summaryPath"
