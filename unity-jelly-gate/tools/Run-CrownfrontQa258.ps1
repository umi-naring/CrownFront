param(
    [string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.0.34f1\Editor\Unity.exe',
    [switch]$ReuseBuild,
    [switch]$FocusedOnly
)

$ErrorActionPreference = 'Stop'
$projectPath = Split-Path -Parent $PSScriptRoot
$workspacePath = Split-Path -Parent $projectPath
$qaDirectory = Join-Path $workspacePath 'tmp\Crownfront-QA-258'
$qaExecutable = Join-Path $qaDirectory 'Crownfront-QA.exe'
$logDirectory = Join-Path $workspacePath 'qa-logs\v2.58'
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
        throw "Crownfront v2.58 QA player build failed. See $buildLog"
    }
}

$probes = @(
    @{ Name = '01-battle-design'; Argument = '-qaBattleDesign258'; Pattern = 'QA_BATTLE_DESIGN_258 passed=True' },
    @{ Name = '02-ultimate-sprite-audit'; Argument = '-qaUltimateSpriteAudit256'; Pattern = 'QA_ULTIMATE_SPRITE_AUDIT_256 passed=True' },
    @{ Name = '03-skin-combat-vfx'; Argument = '-qaSkinCombatVfx255'; Pattern = 'QA_SKIN_COMBAT_VFX_255 passed=True' },
    @{ Name = '04-default-skin-commerce'; Argument = '-qaDefaultSkinCommerce257'; Pattern = 'QA_DEFAULT_SKIN_COMMERCE_257 passed=True' },
    @{ Name = '05-boss-archer-google'; Argument = '-qaBossArcherGoogle254'; Pattern = 'QA_BOSS_ARCHER_GOOGLE_254 passed=True' },
    @{ Name = '06-sprite-range'; Argument = '-qaSpriteRange252'; Pattern = 'QA_SPRITE_RANGE_252 passed=True' },
    @{ Name = '07-special-enemies'; Argument = '-qaSpecialEnemies252'; Pattern = 'QA_SPECIAL_ENEMIES_252 passed=True' },
    @{ Name = '08-combat'; Argument = '-qaCombatSystems'; Pattern = 'QA_COMBAT_SYSTEMS' },
    @{ Name = '09-guide'; Argument = '-qaGuide'; Pattern = 'QA_GUIDE modal=True tabs=True' },
    @{ Name = '10-performance'; Argument = '-qaPerformance240'; Pattern = 'QA_PERFORMANCE_240' },
    @{ Name = '11-navigation'; Argument = '-qaNavigationCombat'; Pattern = 'QA_NAVIGATION_COMBAT' }
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
    version = '2.58.0'; generatedAt = (Get-Date).ToString('o'); playerBuild = $qaExecutable
    allQaPassed = -not ($results.passed -contains $false); probes = $results
}
$summary | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $summaryPath -Encoding utf8
if (-not $summary.allQaPassed) { throw "One or more Crownfront v2.58 QA probes failed. See $summaryPath" }
Write-Output "Crownfront v2.58 QA completed. Summary: $summaryPath"
