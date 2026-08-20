param(
    [string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.0.34f1\Editor\Unity.exe',
    [switch]$ReuseBuild
)

$ErrorActionPreference = 'Stop'
$projectPath = Split-Path -Parent $PSScriptRoot
$workspacePath = Split-Path -Parent $projectPath
$qaDirectory = Join-Path $workspacePath 'tmp\Crownfront-QA-255'
$qaExecutable = Join-Path $qaDirectory 'Crownfront-QA.exe'
$logDirectory = Join-Path $workspacePath 'qa-logs\v2.55'
$buildLog = Join-Path $logDirectory '00-windows-player-build-runner.log'
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
        throw "Crownfront v2.55 QA player build failed. See $buildLog"
    }
}

$probes = @(
    @{ Name = '01-skin-combat-vfx';    Argument = '-qaSkinCombatVfx255';    Pattern = 'QA_SKIN_COMBAT_VFX_255 passed=True' },
    @{ Name = '02-boss-archer-google'; Argument = '-qaBossArcherGoogle254'; Pattern = 'QA_BOSS_ARCHER_GOOGLE_254 passed=True' },
    @{ Name = '03-presentation';        Argument = '-qaPresentation253';      Pattern = 'QA_PRESENTATION_253 passed=True' },
    @{ Name = '04-boss-directional';    Argument = '-qaBossDirectional248';  Pattern = 'QA_BOSS_DIRECTIONAL_248 loaded=True' },
    @{ Name = '05-vfx';                 Argument = '-qaVfx245';              Pattern = 'QA_VFX_245' },
    @{ Name = '06-augment-bronze';      Argument = '-qaAugmentBronze252';    Pattern = 'QA_AUGMENT_TIER_252 tier=Bronze passed=True' },
    @{ Name = '07-augment-silver';      Argument = '-qaAugmentSilver252';    Pattern = 'QA_AUGMENT_TIER_252 tier=Silver passed=True' },
    @{ Name = '08-augment-gold';        Argument = '-qaAugmentGold252';      Pattern = 'QA_AUGMENT_TIER_252 tier=Gold passed=True' },
    @{ Name = '09-augment-platinum';    Argument = '-qaAugmentPlatinum252';  Pattern = 'QA_AUGMENT_TIER_252 tier=Platinum passed=True' },
    @{ Name = '10-augment-diamond';     Argument = '-qaAugmentDiamond252';   Pattern = 'QA_AUGMENT_TIER_252 tier=Diamond passed=True' },
    @{ Name = '11-sprite-range';        Argument = '-qaSpriteRange252';      Pattern = 'QA_SPRITE_RANGE_252 passed=True' },
    @{ Name = '12-special-enemies';     Argument = '-qaSpecialEnemies252';   Pattern = 'QA_SPECIAL_ENEMIES_252 passed=True' },
    @{ Name = '13-monetization';        Argument = '-qaMonetization252';     Pattern = 'QA_MONETIZATION_252 structural=True' },
    @{ Name = '14-combat';              Argument = '-qaCombatSystems';       Pattern = 'QA_COMBAT_SYSTEMS' },
    @{ Name = '15-performance';         Argument = '-qaPerformance240';      Pattern = 'QA_PERFORMANCE_240' }
)

$results = @()
foreach ($probe in $probes) {
    $runtimeLog = Join-Path $logDirectory "$($probe.Name).log"
    $process = Start-Process -FilePath $qaExecutable `
        -ArgumentList @('-batchmode', '-nographics', $probe.Argument, '-logFile', $runtimeLog) `
        -WindowStyle Hidden -Wait -PassThru
    $match = Get-Content -LiteralPath $runtimeLog | Select-String -SimpleMatch $probe.Pattern |
        Select-Object -Last 1
    $exceptions = @(Select-String -LiteralPath $runtimeLog `
        -Pattern 'NullReferenceException|ArgumentOutOfRangeException|IndexOutOfRangeException')
    $passed = $process.ExitCode -eq 0 -and $null -ne $match -and $exceptions.Count -eq 0
    $line = if ($null -ne $match) { $match.Line.Trim() } else { '' }
    $results += [ordered]@{
        name = $probe.Name; argument = $probe.Argument; passed = $passed
        exitCode = $process.ExitCode; exceptions = $exceptions.Count; log = $runtimeLog; result = $line
    }
    Write-Output "$($probe.Name): passed=$passed exit=$($process.ExitCode) exceptions=$($exceptions.Count) $line"
}

$summary = [ordered]@{
    version = '2.55.0'; generatedAt = (Get-Date).ToString('o'); playerBuild = $qaExecutable
    allQaPassed = -not ($results.passed -contains $false); probes = $results
}
$summary | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $summaryPath -Encoding utf8
if (-not $summary.allQaPassed) { throw "One or more Crownfront v2.55 QA probes failed. See $summaryPath" }
Write-Output "Crownfront v2.55 QA completed. Summary: $summaryPath"
