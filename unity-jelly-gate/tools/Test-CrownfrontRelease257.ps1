param(
    [string]$ApkPath = 'C:\Users\Administrator\Documents\Codex\2026-07-22\new-chat\outputs\Crownfront-v2.57.0.apk',
    [switch]$RequireProductionConfiguration
)

$ErrorActionPreference = 'Stop'
$projectPath = Split-Path -Parent $PSScriptRoot
$workspacePath = Split-Path -Parent $projectPath
$outputDirectory = Join-Path $workspacePath 'qa-logs\v2.57'
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

$buildTools = Get-ChildItem -LiteralPath (Join-Path $sdkRoot 'build-tools') -Directory |
    Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
$aapt = if ($buildTools) { Join-Path $buildTools.FullName 'aapt.exe' } else { '' }
$apkSigner = if ($buildTools) { Join-Path $buildTools.FullName 'apksigner.bat' } else { '' }
$badging = if (Test-Path -LiteralPath $aapt) { @(& $aapt dump badging $ApkPath) } else { @() }
$manifestDump = if (Test-Path -LiteralPath $apkanalyzer) { @(& $apkanalyzer manifest print $ApkPath) } else { @() }
$certDump = if (Test-Path -LiteralPath $apkSigner) { @(& $apkSigner verify --print-certs $ApkPath) } else { @() }
$packageMatches = $null -ne ($badging | Select-String -SimpleMatch "package: name='com.toykingdom.jellygate'" | Select-Object -First 1)
$billingPermission = $null -ne ($badging | Select-String -SimpleMatch 'com.android.vending.BILLING' | Select-Object -First 1)
$gamesMetadata = $null -ne ($manifestDump | Select-String -SimpleMatch 'com.google.android.gms.games.APP_ID' | Select-Object -First 1)
$profileCreationAllowed = $null -eq ($manifestDump | Select-String -SimpleMatch 'SUPPRESS_GAME_PROFILE_CREATION' | Select-Object -First 1)
$projectResourceValue = if (Test-Path -LiteralPath $apkanalyzer) {
    [string](& $apkanalyzer resources value --config default --name crownfront_play_games_app_id `
        --type string --package com.toykingdom.jellygate $ApkPath 2>$null | Select-Object -First 1)
} else { '' }
$projectResource = $projectResourceValue.Trim() -eq '228925673337'
$signingSha1Line = ($certDump | Select-String -Pattern 'SHA-1 digest:' | Select-Object -First 1).Line
$signingSha1Raw = if ($signingSha1Line) { (($signingSha1Line -split ': ', 2)[1] -replace ':', '').Trim().ToUpper() } else { '' }
$signingSha1 = if ($signingSha1Raw) {
    ([regex]::Matches($signingSha1Raw, '.{2}') | ForEach-Object { $_.Value }) -join ':'
} else { '' }

$projectMetadataMatches = ([string]$config.playGamesProjectId) -eq '228925673337' -and
    ([string]$config.googleCloudProjectNumber) -eq '228925673337' -and
    ([string]$config.googleCloudProjectId) -eq 'project-4fef7106-3754-4175-9e8'
$startupAccountFlow = $bridge.Contains('PlayGamesSdk.initialize(activity)') -and
    $bridge.Contains('gamesSignInClient.signIn()') -and $game.Contains('DrawGoogleAccountPrompt()') -and
    $runtime.Contains('GoogleSignInStatusMessage')
$diagnosticFlow = $bridge.Contains('PGS_SIGN_IN_FAILED|') -and $bridge.Contains('signingSha1()') -and
    $runtime.Contains('LocalizeGamesFailure')
$purchaseRetryFlow = $bridge.Contains('retryBillingAndPurchase(String productId)') -and
    $bridge.Contains('pendingProductId') -and $bridge.Contains('checkout_launched') -and
    $runtime.Contains('PurchaseStatusMessage') -and $game.Contains('waitingForThisProduct')
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
$structuralPassed = $projectMetadataMatches -and $packageMatches -and $billingPermission -and
    $gamesMetadata -and $profileCreationAllowed -and $projectResource -and $startupAccountFlow -and $diagnosticFlow -and
    $purchaseRetryFlow -and $productTestToggle -and $endInterstitialFlow -and $dependenciesPresent
$productionReady = $structuralPassed -and -not $testAds
$adb = Join-Path $sdkRoot 'platform-tools\adb.exe'
$devices = if (Test-Path -LiteralPath $adb) {
    @((& $adb devices) | Select-Object -Skip 1 | Where-Object { $_ -match "\tdevice$" })
} else { @() }
$result = [ordered]@{
    version = '2.57.0'; apk = (Resolve-Path -LiteralPath $ApkPath).Path
    apkBytes = (Get-Item -LiteralPath $ApkPath).Length
    structuralFlowPassed = $structuralPassed; productionReady = $productionReady
    packageNameMatches = $packageMatches; billingPermission = $billingPermission
    googleProjectMetadataMatches = $projectMetadataMatches; packagedGamesMetadata = $gamesMetadata
    packagedProjectNumber = $projectResource; automaticProfileCreationAllowed = $profileCreationAllowed
    apkSigningSha1 = $signingSha1
    startupGoogleAccountPromptFlow = $startupAccountFlow; persistentGoogleDiagnosticFlow = $diagnosticFlow
    queuedPurchaseRetryFlow = $purchaseRetryFlow; productTestLockAndUnlock = $productTestToggle
    endOfRunInterstitialFlow = $endInterstitialFlow; usesGoogleTestAds = $testAds
    dexDependencies = $dependencies; connectedAndroidDevices = $devices.Count
    actualOnDeviceSignInObserved = $false; actualOnDeviceCheckoutObserved = $false
    blockers = @(
        $(if ($testAds) { 'AdMob still uses Google test IDs; production ad unit IDs are required.' }),
        $(if ($devices.Count -eq 0) { 'No connected Android device for live sign-in and checkout verification.' }),
        'Play Console must link this exact package and APK signing SHA-1, add the test account, and activate all product IDs.'
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_) }
}
$result | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $outputPath -Encoding utf8
$result | ConvertTo-Json -Depth 5
if (-not $structuralPassed) { throw "Release structure preflight failed. See $outputPath" }
if ($RequireProductionConfiguration -and -not $productionReady) { throw "Production configuration is incomplete. See $outputPath" }
