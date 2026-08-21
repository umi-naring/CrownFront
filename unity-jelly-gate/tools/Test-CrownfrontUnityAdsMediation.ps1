param(
    [string]$OutputPath = ''
)

$ErrorActionPreference = 'Stop'
$projectPath = Split-Path -Parent $PSScriptRoot
$workspacePath = Split-Path -Parent $projectPath
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $workspacePath 'qa-logs\unity-ads-mediation\qa-summary.json'
}
$outputDirectory = Split-Path -Parent $OutputPath
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null

$postprocessor = Get-Content -LiteralPath (Join-Path $projectPath `
    'Assets\Scripts\Editor\GooglePlayAndroidPostprocessor.cs') -Raw -Encoding utf8
$bridge = Get-Content -LiteralPath (Join-Path $projectPath `
    'Assets\Plugins\Android\CrownfrontMonetizationBridge.java.txt') -Raw -Encoding utf8
$monetization = Get-Content -LiteralPath (Join-Path $projectPath `
    'Assets\Scripts\Runtime\CrownfrontMonetization.cs') -Raw -Encoding utf8
$guide = Get-Content -LiteralPath (Join-Path $projectPath `
    'Assets\Scripts\Runtime\JellyGateGame.Guide.cs') -Raw -Encoding utf8

$checks = [ordered]@{
    googleMobileAds254 = $postprocessor.Contains("com.google.android.gms:play-services-ads:25.4.0")
    unityAds419 = $postprocessor.Contains("com.unity3d.ads:unity-ads:4.19.0")
    unityAdapter41901 = $postprocessor.Contains("com.google.ads.mediation:unity:4.19.0.1")
    consentBeforeAds = $bridge.Contains('initializeConsentAndAds()') -and
        $bridge.Contains('consentInformation.canRequestAds()') -and
        $bridge.IndexOf('initializeConsentAndAds();') -lt $bridge.IndexOf('MobileAds.initialize')
    teenTreatment = $bridge.Contains('AgeRestrictedTreatment.TEEN') -and
        $bridge.Contains('MAX_AD_CONTENT_RATING_T')
    singleMediatedRequest = $bridge.Contains('InterstitialAd.load(activity, interstitialId') -and
        -not $bridge.Contains('UnityAds.show(') -and -not $bridge.Contains('Advertisement.Show(')
    winningAdapterTelemetry = $bridge.Contains('getMediationAdapterClassName()') -and
        $monetization.Contains('LastAdNetwork')
    noDoubleFullscreenFallback = $monetization.Contains('AdMob mediation selects Google demand first') -and
        -not $monetization.Contains('showUnityInterstitial')
    unityPrivacyDisclosure = $guide.Contains('the mediated advertising partner Unity Ads') -and
        $guide.Contains('unity.com/legal/privacy-policy') -and
        $guide.Contains('Google User Messaging Platform, Unity Ads')
}

$passed = $true
foreach ($value in $checks.Values) {
    if (-not [bool]$value) { $passed = $false; break }
}

$result = [ordered]@{
    generatedAt = (Get-Date).ToString('o')
    passed = $passed
    mode = 'AdMob in-app bidding with Unity Ads demand'
    checks = $checks
    dashboardStillRequired = @(
        'Unity Monetization: Google AdMob mediation partner and Android bidding placement',
        'AdMob: Unity Ads bidding source mapped to the existing interstitial unit',
        'UMP: Unity Ads added to EU and US state regulation ad partners'
    )
}
$result | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $OutputPath -Encoding utf8
$result | ConvertTo-Json -Depth 5
if (-not $passed) { throw "CROWNFRONT Unity Ads mediation QA failed. See $OutputPath" }
