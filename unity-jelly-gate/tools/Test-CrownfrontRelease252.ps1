param(
    [string]$ApkPath = 'C:\Users\Administrator\Documents\Codex\2026-07-22\new-chat\outputs\Crownfront-v2.52.0.apk',
    [switch]$RequireProductionConfiguration
)

$ErrorActionPreference = 'Stop'
$projectPath = Split-Path -Parent $PSScriptRoot
$workspacePath = Split-Path -Parent $projectPath
$outputDirectory = Join-Path $workspacePath 'qa-logs\v2.52'
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
if ($null -ne $aapt2) {
    $manifestDump = (& $aapt2.FullName dump xmltree $ApkPath --file AndroidManifest.xml 2>&1) -join "`n"
    Set-Content -LiteralPath $manifestDumpPath -Value $manifestDump -Encoding utf8
}

$adb = Join-Path $sdkRoot 'platform-tools\adb.exe'
$apkanalyzer = Join-Path $sdkRoot 'cmdline-tools\latest\bin\apkanalyzer.bat'
$dexLines = @()
if (Test-Path -LiteralPath $apkanalyzer) {
    $dexLines = @(& $apkanalyzer dex packages --defined-only $ApkPath)
}
$dexHasBridge = $null -ne ($dexLines | Select-String -SimpleMatch `
    'com.crownfront.monetization.CrownfrontMonetizationBridge' | Select-Object -First 1)
$dexHasGames = $null -ne ($dexLines | Select-String -SimpleMatch `
    'com.google.android.gms.games.PlayGamesSdk' | Select-Object -First 1)
$dexHasAds = $null -ne ($dexLines | Select-String -SimpleMatch `
    'com.google.android.gms.ads.MobileAds' | Select-Object -First 1)
$dexHasBilling = $null -ne ($dexLines | Select-String -SimpleMatch `
    'com.android.billingclient.api.BillingClient' | Select-Object -First 1)
$devices = @()
if (Test-Path -LiteralPath $adb) {
    $devices = @((& $adb devices) | Select-Object -Skip 1 | Where-Object { $_ -match "\tdevice$" })
}

$gamesConfigured = -not [string]::IsNullOrWhiteSpace([string]$config.playGamesProjectId) -and
    ([string]$config.playGamesProjectId) -match '^\d{10,}$'
$testAds = [bool]$config.useTestAds
$adConfigured = -not [string]::IsNullOrWhiteSpace([string]$config.adMobAppId) -and
    -not [string]::IsNullOrWhiteSpace([string]$config.interstitialAdUnitId)
$startupAccountFlow = $bridge.Contains('PlayGamesSdk.initialize(activity)') -and
    $bridge.Contains('checkGamesSignIn();') -and
    $bridge.Contains('games_disconnected') -and
    $game.Contains('GoogleAccountPromptVisible') -and
    $game.Contains('DrawGoogleAccountPrompt()')
$endInterstitialFlow = $runtime.Contains('public bool NotifyRunEnded()') -and
    $runtime.Contains('if (runAdShown || AdsRemoved) return false;') -and
    $runtime.Contains('androidBridge.Call("showInterstitial")') -and
    $bridge.Contains('pendingInterstitialShow = true;') -and
    $bridge.Contains('if (pendingInterstitialShow)')
$removeAdsFlow = $runtime.Contains('crownfront.remove_ads_2000') -and $runtime.Contains('AdsRemoved')
$manifestHasAdsAppId = $manifestDump.Contains('com.google.android.gms.ads.APPLICATION_ID')
$manifestHasGamesAppId = $manifestDump.Contains('com.google.android.gms.games.APP_ID')
$packagePresent = $manifestDump.Contains('com.toykingdom.jellygate')
$versionPresent = $manifestDump.Contains('2.52.0')
$dependenciesPresent = $dexHasBridge -and $dexHasGames -and $dexHasAds -and $dexHasBilling
$structuralPassed = $startupAccountFlow -and $endInterstitialFlow -and $removeAdsFlow -and
    $dependenciesPresent -and
    $adConfigured -and $manifestHasAdsAppId -and $packagePresent -and $versionPresent
$productionReady = $structuralPassed -and $gamesConfigured -and -not $testAds -and $manifestHasGamesAppId

$result = [ordered]@{
    version = '2.52.0'
    apk = (Resolve-Path -LiteralPath $ApkPath).Path
    apkBytes = (Get-Item -LiteralPath $ApkPath).Length
    structuralFlowPassed = $structuralPassed
    productionReady = $productionReady
    startupGoogleAccountPromptFlow = $startupAccountFlow
    playGamesConfigured = $gamesConfigured
    manifestHasGamesAppId = $manifestHasGamesAppId
    endOfRunInterstitialFlow = $endInterstitialFlow
    removeAdsSuppressesInterstitial = $removeAdsFlow
    adConfigurationPresent = $adConfigured
    usesGoogleTestAds = $testAds
    manifestHasAdsAppId = $manifestHasAdsAppId
    dexDependencies = [ordered]@{
        crownfrontBridge = $dexHasBridge
        playGamesSdk = $dexHasGames
        mobileAdsSdk = $dexHasAds
        billingClient = $dexHasBilling
    }
    packageAndVersion = "$packagePresent/$versionPresent"
    connectedAndroidDevices = $devices.Count
    actualOnDeviceSignInObserved = $false
    actualOnDeviceInterstitialObserved = $false
    runtimeObservationNote = if ($devices.Count -eq 0) {
        'No Android device/emulator connected; APK structure verified, live UI presentation not observed.'
    } else {
        'A device is connected, but this non-destructive preflight does not install or launch without an explicit device test run.'
    }
    blockers = @(
        $(if (-not $gamesConfigured) { 'Play Games project ID is blank.' }),
        $(if ($testAds) { 'AdMob is using Google test IDs, not production ad units.' }),
        $(if ($devices.Count -eq 0) { 'No connected Android device for live sign-in/ad display verification.' })
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_) }
}
$result | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $outputPath -Encoding utf8
$result | ConvertTo-Json -Depth 5

if (-not $structuralPassed) { throw "Release structure preflight failed. See $outputPath" }
if ($RequireProductionConfiguration -and -not $productionReady) {
    throw "Production configuration is incomplete. See $outputPath"
}
