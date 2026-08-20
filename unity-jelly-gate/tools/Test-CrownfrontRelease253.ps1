param(
    [string]$ApkPath = 'C:\Users\Administrator\Documents\Codex\2026-07-22\new-chat\outputs\Crownfront-v2.53.0.apk',
    [switch]$RequireProductionConfiguration
)

$ErrorActionPreference = 'Stop'
$projectPath = Split-Path -Parent $PSScriptRoot
$workspacePath = Split-Path -Parent $projectPath
$outputDirectory = Join-Path $workspacePath 'qa-logs\v2.53'
$outputPath = Join-Path $outputDirectory 'release-preflight.json'
$manifestDumpPath = Join-Path $outputDirectory 'android-manifest.txt'
$configPath = Join-Path $projectPath 'Assets\Resources\crownfront-google-services.json'
$bridgePath = Join-Path $projectPath 'Assets\Plugins\Android\CrownfrontMonetizationBridge.java.txt'
$runtimePath = Join-Path $projectPath 'Assets\Scripts\Runtime\CrownfrontMonetization.cs'
$gamePath = Join-Path $projectPath 'Assets\Scripts\Runtime\JellyGateGame.cs'

New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
if (-not (Test-Path -LiteralPath $ApkPath)) { throw "APK not found: $ApkPath" }

$config = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
$bridge = Get-Content -LiteralPath $bridgePath -Raw
$runtime = Get-Content -LiteralPath $runtimePath -Raw
$game = Get-Content -LiteralPath $gamePath -Raw
$sdkRoot = Join-Path $env:LOCALAPPDATA 'Android\Sdk'
$aapt2 = Get-ChildItem -LiteralPath (Join-Path $sdkRoot 'build-tools') -Recurse -Filter 'aapt2.exe' `
    -ErrorAction SilentlyContinue | Sort-Object FullName -Descending | Select-Object -First 1
$manifestDump = ''
$resourceDump = ''
if ($null -ne $aapt2) {
    $manifestDump = (& $aapt2.FullName dump xmltree $ApkPath --file AndroidManifest.xml 2>&1) -join "`n"
    $resourceDump = (& $aapt2.FullName dump resources $ApkPath 2>&1) -join "`n"
    Set-Content -LiteralPath $manifestDumpPath -Value $manifestDump -Encoding utf8
}

$apkanalyzer = Join-Path $sdkRoot 'cmdline-tools\latest\bin\apkanalyzer.bat'
$dexLines = if (Test-Path -LiteralPath $apkanalyzer) {
    @(& $apkanalyzer dex packages --defined-only $ApkPath)
} else { @() }
function Test-DexType([string]$name) {
    $null -ne ($dexLines | Select-String -SimpleMatch $name | Select-Object -First 1)
}

$projectMetadataMatches = ([string]$config.playGamesProjectId) -eq '228925673337' -and
    ([string]$config.googleCloudProjectNumber) -eq '228925673337' -and
    ([string]$config.googleCloudProjectId) -eq 'project-4fef7106-3754-4175-9e8'
$gamesConfigured = ([string]$config.playGamesProjectId) -match '^\d{10,}$'
$testAds = [bool]$config.useTestAds
$adConfigured = -not [string]::IsNullOrWhiteSpace([string]$config.adMobAppId) -and
    -not [string]::IsNullOrWhiteSpace([string]$config.interstitialAdUnitId)
$startupAccountFlow = $bridge.Contains('PlayGamesSdk.initialize(activity)') -and
    $bridge.Contains('checkGamesSignIn();') -and $game.Contains('DrawGoogleAccountPrompt()')
$endInterstitialFlow = $runtime.Contains('public bool NotifyRunEnded()') -and
    $runtime.Contains('if (runAdShown || AdsRemoved) return false;') -and
    $runtime.Contains('androidBridge.Call("showInterstitial")') -and
    $bridge.Contains('pendingInterstitialShow = true;')
$removeAdsFlow = $runtime.Contains('crownfront.remove_ads_2000') -and $runtime.Contains('AdsRemoved')
$manifestHasAds = $manifestDump.Contains('com.google.android.gms.ads.APPLICATION_ID')
$manifestHasGames = $manifestDump.Contains('com.google.android.gms.games.APP_ID')
$manifestHasProjectNumber = $manifestHasGames -and
    $resourceDump.Contains('string/crownfront_play_games_app_id') -and
    $resourceDump.Contains('228925673337')
$packagePresent = $manifestDump.Contains('com.toykingdom.jellygate')
$versionPresent = $manifestDump.Contains('2.53.0')
$dependencies = [ordered]@{
    crownfrontBridge = Test-DexType 'com.crownfront.monetization.CrownfrontMonetizationBridge'
    playGamesSdk = Test-DexType 'com.google.android.gms.games.PlayGamesSdk'
    mobileAdsSdk = Test-DexType 'com.google.android.gms.ads.MobileAds'
    billingClient = Test-DexType 'com.android.billingclient.api.BillingClient'
}
$dependenciesPresent = -not ($dependencies.Values -contains $false)
$structuralPassed = $projectMetadataMatches -and $startupAccountFlow -and $endInterstitialFlow -and
    $removeAdsFlow -and $dependenciesPresent -and $adConfigured -and $manifestHasAds -and
    $manifestHasGames -and $manifestHasProjectNumber -and $packagePresent -and $versionPresent
$productionReady = $structuralPassed -and -not $testAds

$adb = Join-Path $sdkRoot 'platform-tools\adb.exe'
$devices = if (Test-Path -LiteralPath $adb) {
    @((& $adb devices) | Select-Object -Skip 1 | Where-Object { $_ -match "\tdevice$" })
} else { @() }
$result = [ordered]@{
    version = '2.53.0'; apk = (Resolve-Path -LiteralPath $ApkPath).Path
    apkBytes = (Get-Item -LiteralPath $ApkPath).Length
    structuralFlowPassed = $structuralPassed; productionReady = $productionReady
    googleProjectMetadataMatches = $projectMetadataMatches
    playGamesConfigured = $gamesConfigured; manifestHasGamesAppId = $manifestHasGames
    manifestContainsProjectNumber = $manifestHasProjectNumber
    startupGoogleAccountPromptFlow = $startupAccountFlow
    endOfRunInterstitialFlow = $endInterstitialFlow
    removeAdsSuppressesInterstitial = $removeAdsFlow
    usesGoogleTestAds = $testAds; dexDependencies = $dependencies
    packageAndVersion = "$packagePresent/$versionPresent"
    connectedAndroidDevices = $devices.Count
    actualOnDeviceSignInObserved = $false; actualOnDeviceInterstitialObserved = $false
    blockers = @(
        $(if ($testAds) { 'AdMob is still using Google test IDs; production ad unit IDs are required.' }),
        $(if ($devices.Count -eq 0) { 'No connected Android device for live sign-in/ad verification.' })
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_) }
}
$result | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $outputPath -Encoding utf8
$result | ConvertTo-Json -Depth 5
if (-not $structuralPassed) { throw "Release structure preflight failed. See $outputPath" }
if ($RequireProductionConfiguration -and -not $productionReady) {
    throw "Production configuration is incomplete. See $outputPath"
}
