param(
    [string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.0.34f1\Editor\Unity.exe',
    [switch]$ReuseBuild,
    [switch]$FocusedOnly
)

$ErrorActionPreference = 'Stop'
$projectPath = Split-Path -Parent $PSScriptRoot
$workspacePath = Split-Path -Parent $projectPath
$qaDirectory = Join-Path $workspacePath 'tmp\Crownfront-QA-265'
$qaExecutable = Join-Path $qaDirectory 'Crownfront-QA.exe'
$logDirectory = Join-Path $workspacePath 'qa-logs\v2.65'
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
        throw "Crownfront v2.65 QA player build failed. See $buildLog"
    }
}

$probes = @(
    @{ Name = '01-all-boss-and-enemy-sprites'; Arguments = @('-qaSprite264'); Pattern = 'QA_SPRITE_264 passed=True bossPoses=5760 regularPoses=10176' },
    @{ Name = '02-battlefield-density-navigation'; Arguments = @('-qaBattlefield264'); Pattern = 'QA_BATTLEFIELD_264 passed=True wave=12-88 squad=4-10 burst=2-4 side=0.40' },
    @{ Name = '03-every-augment-runtime'; Arguments = @('-qaAugmentRuntime264'); Pattern = 'QA_AUGMENT_RUNTIME_264 passed=True' },
    @{ Name = '04-bronze-monte-carlo'; Arguments = @('-qaAugmentBronze252'); Pattern = 'QA_AUGMENT_TIER_252 tier=Bronze passed=True' },
    @{ Name = '05-silver-monte-carlo'; Arguments = @('-qaAugmentSilver252'); Pattern = 'QA_AUGMENT_TIER_252 tier=Silver passed=True' },
    @{ Name = '06-gold-monte-carlo'; Arguments = @('-qaAugmentGold252'); Pattern = 'QA_AUGMENT_TIER_252 tier=Gold passed=True' },
    @{ Name = '07-platinum-monte-carlo'; Arguments = @('-qaAugmentPlatinum252'); Pattern = 'QA_AUGMENT_TIER_252 tier=Platinum passed=True' },
    @{ Name = '08-diamond-monte-carlo'; Arguments = @('-qaAugmentDiamond252'); Pattern = 'QA_AUGMENT_TIER_252 tier=Diamond passed=True' }
)
if ($FocusedOnly) { $probes = @($probes[0..2]) }

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
    $line = if ($null -ne $match) { $match.Line.Trim() } else {
        (Get-Content -LiteralPath $runtimeLog | Select-String -Pattern 'QA_.*264|QA_AUGMENT_TIER_252' |
            Select-Object -Last 1).Line.Trim()
    }
    $results += [ordered]@{
        name = $probe.Name; passed = $passed; exitCode = $process.ExitCode
        exceptions = $exceptions.Count; log = $runtimeLog; result = $line
    }
    Write-Output "$($probe.Name): passed=$passed exit=$($process.ExitCode) exceptions=$($exceptions.Count) $line"
}

$summary = [ordered]@{
    version = '2.65.0'; generatedAt = (Get-Date).ToString('o'); playerBuild = $qaExecutable
    allQaPassed = -not ($results.passed -contains $false); probes = $results
}
$summary | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath $summaryPath -Encoding utf8
if (-not $summary.allQaPassed) { throw "One or more Crownfront v2.65 QA checks failed. See $summaryPath" }
Write-Output "Crownfront v2.65 QA completed. Summary: $summaryPath"
