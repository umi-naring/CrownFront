param(
    [string]$ApkPath = 'C:\Users\Administrator\Documents\Codex\2026-07-22\new-chat\outputs\Crownfront-v2.72.4.apk',
    [switch]$RequireProductionConfiguration
)

$ErrorActionPreference = 'Stop'
$projectPath = Split-Path -Parent $PSScriptRoot
$workspacePath = Split-Path -Parent $projectPath
$outputDirectory = Join-Path $workspacePath 'qa-logs\v2.72.4-google-play'
$outputPath = Join-Path $outputDirectory 'service-preflight.json'
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
if (-not (Test-Path -LiteralPath $ApkPath)) { throw "APK not found: $ApkPath" }

$sdkRoot = Join-Path $env:LOCALAPPDATA 'Android\Sdk'
$buildTools = Get-ChildItem -LiteralPath (Join-Path $sdkRoot 'build-tools') -Directory |
    Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
if ($null -eq $buildTools) { throw 'Android build-tools not found.' }
$aapt2 = Join-Path $buildTools.FullName 'aapt2.exe'
$apksigner = Join-Path $buildTools.FullName 'apksigner.bat'
$apkanalyzer = Join-Path $sdkRoot 'cmdline-tools\latest\bin\apkanalyzer.bat'

$badging = (& $aapt2 dump badging $ApkPath) -join "`n"
$manifest = (& $aapt2 dump xmltree $ApkPath --file AndroidManifest.xml) -join "`n"
$resources = (& $aapt2 dump resources $ApkPath) -join "`n"
$certificates = (& $apksigner verify --print-certs $ApkPath) -join "`n"
$packages = if (Test-Path -LiteralPath $apkanalyzer) {
    (& $apkanalyzer dex packages $ApkPath) -join "`n"
} else { '' }

$configPath = Join-Path $projectPath 'Assets\Resources\crownfront-google-services.json'
$config = Get-Content -LiteralPath $configPath -Raw -Encoding utf8 | ConvertFrom-Json
$bridgePath = Join-Path $projectPath 'Assets\Plugins\Android\CrownfrontMonetizationBridge.java.txt'
$postprocessorPath = Join-Path $projectPath 'Assets\Scripts\Editor\GooglePlayAndroidPostprocessor.cs'
$portablePath = Join-Path $projectPath 'Assets\Scripts\Runtime\JellyGateGame.PortablePersistence.cs'
$bridge = Get-Content -LiteralPath $bridgePath -Raw -Encoding utf8
$postprocessor = Get-Content -LiteralPath $postprocessorPath -Raw -Encoding utf8
$portable = Get-Content -LiteralPath $portablePath -Raw -Encoding utf8
$gamePath = Join-Path $projectPath 'Assets\Scripts\Runtime\JellyGateGame.cs'
$monetizationPath = Join-Path $projectPath 'Assets\Scripts\Runtime\CrownfrontMonetization.cs'
$localizationPath = Join-Path $projectPath 'Assets\Scripts\Runtime\GameLocalization.cs'
$game = Get-Content -LiteralPath $gamePath -Raw -Encoding utf8
$monetization = Get-Content -LiteralPath $monetizationPath -Raw -Encoding utf8
$localization = Get-Content -LiteralPath $localizationPath -Raw -Encoding utf8
$backupRuleMatch = [regex]::Match($resources, 'resource (0x[0-9a-f]+) xml/crownfront_backup_rules')
$extractionRuleMatch = [regex]::Match($resources, 'resource (0x[0-9a-f]+) xml/crownfront_data_extraction_rules')
$backupRuleRef = if ($backupRuleMatch.Success) { '@' + $backupRuleMatch.Groups[1].Value } else { '' }
$extractionRuleRef = if ($extractionRuleMatch.Success) { '@' + $extractionRuleMatch.Groups[1].Value } else { '' }

$checks = [ordered]@{
    package = $badging.Contains("package: name='com.toykingdom.jellygate'")
    versionName = $badging.Contains("versionName='2.72.4'")
    versionCode = $badging.Contains("versionCode='130'")
    billingPermission = $manifest.Contains('com.android.vending.BILLING')
    billingLibrary = $manifest.Contains('com.android.billingclient.api.ProxyBillingActivity') -and
        $manifest.Contains('9.1.0')
    adsAppId = $manifest.Contains('com.google.android.gms.ads.APPLICATION_ID') -and
        $manifest.Contains('ca-app-pub-1688606489162660~5049427486')
    interstitialAdUnitId = -not [bool]$config.useTestAds -and
        $config.interstitialAdUnitId -eq 'ca-app-pub-1688606489162660/1175233834'
    playGamesRemoved = -not $manifest.Contains('com.google.android.gms.games.APP_ID') -and
        -not $packages.Contains('com.google.android.gms.games') -and
        -not $bridge.Contains('PlayGamesSdk') -and -not $bridge.Contains('signInGames')
    billingClasses = $packages.Contains('com.android.billingclient')
    adsClasses = $packages.Contains('com.google.android.gms.ads')
    umpClasses = $packages.Contains('com.google.android.ump')
    umpDependency = $postprocessor.Contains('com.google.android.ump:user-messaging-platform:4.0.0')
    consentBeforeAds = $bridge.Contains('initializeConsentAndAds()') -and
        $bridge.Contains('consentInformation.canRequestAds()') -and
        $bridge.IndexOf('initializeConsentAndAds();') -lt $bridge.IndexOf('MobileAds.initialize')
    purchaseQuery = $bridge.Contains('queryProductDetailsAsync')
    purchaseLaunch = $bridge.Contains('launchBillingFlow')
    purchaseAcknowledge = $bridge.Contains('acknowledgePurchase')
    purchaseRestore = $bridge.Contains('queryPurchasesAsync')
    loginFreeBackupManifest = $manifest.Contains('allowBackup') -and
        $manifest.Contains('=true') -and
        $backupRuleMatch.Success -and $manifest.Contains('fullBackupContent') -and
        $manifest.Contains($backupRuleRef) -and
        $extractionRuleMatch.Success -and $manifest.Contains('dataExtractionRules') -and
        $manifest.Contains($extractionRuleRef)
    loginFreeBackupResources = $resources.Contains('crownfront_backup_rules') -and
        $resources.Contains('crownfront_data_extraction_rules')
    portableProgressScope = $portable.Contains('crownfront_portable_progress_v1.json') -and
        $portable.Contains('runCheckpointJson') -and $portable.Contains('completedChallenges') -and
        -not $portable.Contains('OwnedPrefix') -and -not $portable.Contains('EquippedCastleKey')
    sideloadUsesSafeTestAd = $monetization.Contains('Application.installerName') -and
        $monetization.Contains('com.android.vending') -and
        $monetization.Contains('TestInterstitialId')
    rawAdErrorsHidden = $monetization.Contains('Debug.LogWarning($"Interstitial ad unavailable:') -and
        -not $monetization.Contains('statusSink?.Invoke(string.IsNullOrWhiteSpace(nativeEvent.message)') -and
        $bridge.Contains('"AD_LOAD_FAILED|code=" + error.getCode());') -and
        $bridge.Contains('"AD_SHOW_FAILED|code=" + error.getCode());')
    settingsAdStatusRemoved = -not $game.Contains('AdStatusMessage') -and
        -not $game.Contains('AD PRIVACY OPTIONS')
    testProductControlsRemoved = -not $game.Contains('UNLOCK ALL') -and
        -not $game.Contains('LOCK ALL') -and -not $game.Contains('TEST TOOLS')
    systemLocaleDefault = $game.Contains('GameLocalization.LoadInitialLanguage()') -and
        $localization.Contains('Application.systemLanguage') -and
        $localization.Contains('systemLanguage == SystemLanguage.Korean') -and
        $localization.Contains('GameLanguage.English')
    testAdConfiguration = [bool]$config.useTestAds -and
        $config.interstitialAdUnitId -eq 'ca-app-pub-3940256099942544/1033173712'
    productionAdConfiguration = -not [bool]$config.useTestAds -and
        -not [string]::IsNullOrWhiteSpace([string]$config.adMobAppId) -and
        -not ([string]$config.adMobAppId).StartsWith('ca-app-pub-3940256099942544')
    releaseSigning = -not $certificates.Contains('CN=Android Debug')
}

$requiredWiring = @('package','versionName','versionCode','billingPermission','billingLibrary',
    'adsAppId','interstitialAdUnitId','playGamesRemoved','billingClasses','adsClasses','umpClasses',
    'umpDependency','consentBeforeAds','purchaseQuery','purchaseLaunch','purchaseAcknowledge',
    'purchaseRestore','loginFreeBackupManifest','loginFreeBackupResources','portableProgressScope')
$requiredWiring += @('sideloadUsesSafeTestAd','rawAdErrorsHidden','settingsAdStatusRemoved',
    'testProductControlsRemoved','systemLocaleDefault')
$wiringPassed = $true
foreach ($name in $requiredWiring) { if (-not [bool]$checks[$name]) { $wiringPassed = $false } }
$productionPassed = $wiringPassed -and [bool]$checks.productionAdConfiguration -and
    [bool]$checks.releaseSigning

$result = [ordered]@{
    version = '2.72.4'
    generatedAt = (Get-Date).ToString('o')
    apk = (Resolve-Path -LiteralPath $ApkPath).Path
    runtimeServiceWiringPassed = $wiringPassed
    productionConfigurationPassed = $productionPassed
    checks = $checks
    signer = ($certificates -split "`n" | Where-Object { $_ -match 'certificate (DN|SHA-1)' })
    note = if ($productionPassed) {
        'APK wiring and production identifiers/signing passed.'
    } elseif ($wiringPassed) {
        'Runtime wiring and production AdMob IDs passed. Store release is blocked by debug signing.'
    } else {
        'Runtime service wiring failed.'
    }
}
$result | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $outputPath -Encoding utf8
$result | ConvertTo-Json -Depth 6
if (-not $wiringPassed) { throw "Crownfront Google Play service wiring failed. See $outputPath" }
if ($RequireProductionConfiguration -and -not $productionPassed) {
    throw "Production Google Play configuration is incomplete. See $outputPath"
}
