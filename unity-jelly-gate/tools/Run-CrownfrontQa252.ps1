param(
    [string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.0.34f1\Editor\Unity.exe',
    [switch]$ReuseBuild
)

$ErrorActionPreference = 'Stop'
$projectPath = Split-Path -Parent $PSScriptRoot
$workspacePath = Split-Path -Parent $projectPath
$qaDirectory = Join-Path $workspacePath 'tmp\Crownfront-QA-252'
$qaExecutable = Join-Path $qaDirectory 'Crownfront-QA.exe'
$logDirectory = Join-Path $workspacePath 'qa-logs\v2.52'
$buildLog = Join-Path $logDirectory '00-windows-player-build.log'
$summaryPath = Join-Path $logDirectory 'qa-summary.json'

if (-not (Test-Path -LiteralPath $UnityPath)) {
    throw "Unity executable not found: $UnityPath"
}
New-Item -ItemType Directory -Path $qaDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null

if (-not $ReuseBuild -or -not (Test-Path -LiteralPath $qaExecutable)) {
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
}

$probes = @(
    @{ Name = '01-augment-bronze';   Argument = '-qaAugmentBronze252';   Pattern = 'QA_AUGMENT_TIER_252 tier=Bronze passed=True' },
    @{ Name = '02-augment-silver';   Argument = '-qaAugmentSilver252';   Pattern = 'QA_AUGMENT_TIER_252 tier=Silver passed=True' },
    @{ Name = '03-augment-gold';     Argument = '-qaAugmentGold252';     Pattern = 'QA_AUGMENT_TIER_252 tier=Gold passed=True' },
    @{ Name = '04-augment-platinum'; Argument = '-qaAugmentPlatinum252'; Pattern = 'QA_AUGMENT_TIER_252 tier=Platinum passed=True' },
    @{ Name = '05-augment-diamond';  Argument = '-qaAugmentDiamond252';  Pattern = 'QA_AUGMENT_TIER_252 tier=Diamond passed=True' },
    @{ Name = '06-sprite-range';     Argument = '-qaSpriteRange252';     Pattern = 'QA_SPRITE_RANGE_252 passed=True' },
    @{ Name = '07-special-enemies';  Argument = '-qaSpecialEnemies252'; Pattern = 'QA_SPECIAL_ENEMIES_252 passed=True' },
    @{ Name = '08-monetization';     Argument = '-qaMonetization252';    Pattern = 'QA_MONETIZATION_252 structural=True' },
    @{ Name = '09-augment-rules';    Argument = '-qaAugmentRules';       Pattern = 'QA_AUGMENT_RULES' },
    @{ Name = '10-combat';           Argument = '-qaCombatSystems';      Pattern = 'QA_COMBAT_SYSTEMS' },
    @{ Name = '11-performance';      Argument = '-qaPerformance240';     Pattern = 'QA_PERFORMANCE_240' }
)

$results = @()
foreach ($probe in $probes) {
    $runtimeLog = Join-Path $logDirectory "$($probe.Name).log"
    $process = Start-Process -FilePath $qaExecutable `
        -ArgumentList @('-batchmode', '-nographics', $probe.Argument, '-logFile', $runtimeLog) `
        -WindowStyle Hidden -Wait -PassThru
    $match = Get-Content -LiteralPath $runtimeLog | Select-String -SimpleMatch $probe.Pattern |
        Select-Object -Last 1
    $passed = $process.ExitCode -eq 0 -and $null -ne $match
    $resultLine = if ($null -ne $match) { $match.Line.Trim() } else { '' }
    $results += [ordered]@{
        name = $probe.Name
        argument = $probe.Argument
        passed = $passed
        exitCode = $process.ExitCode
        log = $runtimeLog
        result = $resultLine
    }
    Write-Output "$($probe.Name): passed=$passed exit=$($process.ExitCode) $resultLine"
}

$configPath = Join-Path $projectPath 'Assets\Resources\crownfront-google-services.json'
$config = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
$releaseState = [ordered]@{
    playGamesConfigured = -not [string]::IsNullOrWhiteSpace([string]$config.playGamesProjectId)
    playGamesProjectId = [string]$config.playGamesProjectId
    useTestAds = [bool]$config.useTestAds
    adMobAppId = [string]$config.adMobAppId
    interstitialAdUnitId = [string]$config.interstitialAdUnitId
    productionAdsConfigured = (-not [bool]$config.useTestAds) -and
        -not [string]::IsNullOrWhiteSpace([string]$config.adMobAppId) -and
        -not [string]::IsNullOrWhiteSpace([string]$config.interstitialAdUnitId)
}
$summary = [ordered]@{
    version = '2.52.0'
    generatedAt = (Get-Date).ToString('o')
    playerBuild = $qaExecutable
    allQaPassed = -not ($results.passed -contains $false)
    probes = $results
    releaseConfiguration = $releaseState
}
$summary | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $summaryPath -Encoding utf8

if (-not $summary.allQaPassed) {
    throw "One or more Crownfront v2.52 QA probes failed. See $summaryPath"
}
Write-Output "Crownfront v2.52 QA completed. Summary: $summaryPath"
