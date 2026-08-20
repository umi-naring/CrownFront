param(
    [string]$ApkPath = 'C:\Users\Administrator\Documents\Codex\2026-07-22\new-chat\outputs\Crownfront-v2.56.0.apk',
    [switch]$RequireProductionConfiguration
)

$ErrorActionPreference = 'Stop'
$projectPath = Split-Path -Parent $PSScriptRoot
$workspacePath = Split-Path -Parent $projectPath
$outputDirectory = Join-Path $workspacePath 'qa-logs\v2.56'
$outputPath = Join-Path $outputDirectory 'release-preflight.json'
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
$apkanalyzer = Join-Path $sdkRoot 'cmdline-tools\latest\bin\apkanalyzer.bat'
$dexLines = if (Test-Path -LiteralPath $apkanalyzer) { @(& $apkanalyzer dex packages --defined-only $ApkPath) } else { @() }
function Test-DexType([string]$name) { $null -ne ($dexLines | Select-String -SimpleMatch $name | Select-Object -First 1) }

$projectMetadataMatches = ([string]$config.playGamesProjectId) -eq '228925673337' -and
    ([string]$config.googleCloudProjectNumber) -eq '228925673337' -and
    ([string]$config.googleCloudProjectId) -eq 'project-4fef7106-3754-4175-9e8'
$startupAccountFlow = $bridge.Contains('PlayGamesSdk.initialize(activity)') -and
    $bridge.Contains('gamesSignInClient.signIn()') -and $game.Contains('DrawGoogleAccountPrompt()') -and
    $runtime.Contains('GoogleSignInStatusMessage')
$diagnosticFlow = $bridge.Contains('signing SHA-1') -and $bridge.Contains('tester access') -and
    $runtime.Contains('case "games_disconnected"')
$productTestToggle = $runtime.Contains('GrantAllProductsForTesting') -and
    $runtime.Contains('ResetAllProductsForTesting') -and $game.Contains('CURRENT: ALL PRODUCTS LOCKED')
$endInterstitialFlow = $runtime.Contains('NotifyRunEnded()') -and
    $runtime.Contains('if (runAdShown || AdsRemoved) return false;') -and $bridge.Contains('showInterstitial')
$dependencies = [ordered]@{
    bridge = Test-DexType 'com.crownfront.monetization.CrownfrontMonetizationBridge'
    playGames = Test-DexType 'com.google.android.gms.games.PlayGamesSdk'
    mobileAds = Test-DexType 'com.google.android.gms.ads.MobileAds'
    billing = Test-DexType 'com.android.billingclient.api.BillingClient'
}
$dependenciesPresent = -not ($dependencies.Values -contains $false)
$testAds = [bool]$config.useTestAds
$structuralPassed = $projectMetadataMatches -and $startupAccountFlow -and $diagnosticFlow -and
    $productTestToggle -and $endInterstitialFlow -and $dependenciesPresent
$productionReady = $structuralPassed -and -not $testAds
$adb = Join-Path $sdkRoot 'platform-tools\adb.exe'
$devices = if (Test-Path -LiteralPath $adb) {
    @((& $adb devices) | Select-Object -Skip 1 | Where-Object { $_ -match "\tdevice$" })
} else { @() }
$result = [ordered]@{
    version = '2.56.0'; apk = (Resolve-Path -LiteralPath $ApkPath).Path
    apkBytes = (Get-Item -LiteralPath $ApkPath).Length
    structuralFlowPassed = $structuralPassed; productionReady = $productionReady
    googleProjectMetadataMatches = $projectMetadataMatches
    startupGoogleAccountPromptFlow = $startupAccountFlow; persistentGoogleDiagnosticFlow = $diagnosticFlow
    productTestLockAndUnlock = $productTestToggle; endOfRunInterstitialFlow = $endInterstitialFlow
    usesGoogleTestAds = $testAds; dexDependencies = $dependencies
    connectedAndroidDevices = $devices.Count
    actualOnDeviceSignInObserved = $false; actualOnDeviceInterstitialObserved = $false
    blockers = @(
        $(if ($testAds) { 'AdMob still uses Google test IDs; production ad unit IDs are required.' }),
        $(if ($devices.Count -eq 0) { 'No connected Android device for live sign-in/ad verification.' })
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_) }
}
$result | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $outputPath -Encoding utf8
$result | ConvertTo-Json -Depth 5
if (-not $structuralPassed) { throw "Release structure preflight failed. See $outputPath" }
if ($RequireProductionConfiguration -and -not $productionReady) { throw "Production configuration is incomplete. See $outputPath" }
