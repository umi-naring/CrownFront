param(
    [string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.0.34f1\Editor\Unity.exe',
    [switch]$ReuseBuild,
    [switch]$FocusedOnly
)

$ErrorActionPreference = 'Stop'
$projectPath = Split-Path -Parent $PSScriptRoot
$workspacePath = Split-Path -Parent $projectPath
$qaDirectory = Join-Path $workspacePath 'tmp\Crownfront-QA-261'
$qaExecutable = Join-Path $qaDirectory 'Crownfront-QA.exe'
$logDirectory = Join-Path $workspacePath 'qa-logs\v2.61'
$buildLog = Join-Path $logDirectory '01-windows-player-build.log'
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
        throw "Crownfront v2.61 QA player build failed. See $buildLog"
    }
}

$probes = @(
    @{ Name = '02-all-round-runtime'; Argument = '-qaRelease261'; Pattern = 'QA_RELEASE_261 passed=True' },
    @{ Name = '03-sprite-regression'; Argument = '-qaRelease260'; Pattern = 'QA_RELEASE_260 passed=True' },
    @{ Name = '04-all-unit-directions'; Argument = '-qaUltimateSpriteAudit256'; Pattern = 'QA_ULTIMATE_SPRITE_AUDIT_256 passed=True' },
    @{ Name = '05-all-skin-skills'; Argument = '-qaSkinCombatVfx255'; Pattern = 'QA_SKIN_COMBAT_VFX_255 passed=True' },
    @{ Name = '06-sprite-range'; Argument = '-qaSpriteRange252'; Pattern = 'QA_SPRITE_RANGE_252 passed=True' },
    @{ Name = '07-navigation'; Argument = '-qaNavigationCombat'; Pattern = 'QA_NAVIGATION_COMBAT' },
    @{ Name = '08-combat'; Argument = '-qaCombatSystems'; Pattern = 'QA_COMBAT_SYSTEMS' },
    @{ Name = '09-guide'; Argument = '-qaGuide'; Pattern = 'QA_GUIDE modal=True tabs=True' },
    @{ Name = '10-augment-bronze'; Argument = '-qaAugmentBronze252'; Pattern = 'QA_AUGMENT_TIER_252 tier=Bronze passed=True' },
    @{ Name = '11-augment-silver'; Argument = '-qaAugmentSilver252'; Pattern = 'QA_AUGMENT_TIER_252 tier=Silver passed=True' },
    @{ Name = '12-augment-gold'; Argument = '-qaAugmentGold252'; Pattern = 'QA_AUGMENT_TIER_252 tier=Gold passed=True' },
    @{ Name = '13-augment-platinum'; Argument = '-qaAugmentPlatinum252'; Pattern = 'QA_AUGMENT_TIER_252 tier=Platinum passed=True' },
    @{ Name = '14-augment-diamond'; Argument = '-qaAugmentDiamond252'; Pattern = 'QA_AUGMENT_TIER_252 tier=Diamond passed=True' }
)
if ($FocusedOnly) { $probes = @($probes[0]) }

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
    version = '2.61.0'; generatedAt = (Get-Date).ToString('o'); playerBuild = $qaExecutable
    allQaPassed = -not ($results.passed -contains $false); probes = $results
}
$summary | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $summaryPath -Encoding utf8
if (-not $summary.allQaPassed) { throw "One or more Crownfront v2.61 QA probes failed. See $summaryPath" }
Write-Output "Crownfront v2.61 QA completed. Summary: $summaryPath"
