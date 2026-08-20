param(
    [string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.0.34f1\Editor\Unity.exe',
    [string]$ApkPath = '',
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$projectPath = Split-Path -Parent $PSScriptRoot
$workspacePath = Split-Path -Parent $projectPath
$logDirectory = Join-Path $workspacePath 'qa-logs\v2.70.9-icon'
$configureLog = Join-Path $logDirectory 'icon-settings.log'
$buildLog = Join-Path $logDirectory 'android-build.log'
$summaryPath = Join-Path $logDirectory 'qa-summary.json'
$sourceIcon = Join-Path $projectPath 'Assets\Resources\app-icon-hero-shield-v1.png'
if ([string]::IsNullOrWhiteSpace($ApkPath)) {
    $ApkPath = Join-Path $workspacePath 'outputs\Crownfront-v2.70.9.apk'
}

if (-not (Test-Path -LiteralPath $UnityPath)) { throw "Unity executable not found: $UnityPath" }
if (-not (Test-Path -LiteralPath $sourceIcon)) { throw "Source icon not found: $sourceIcon" }
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null

$configure = Start-Process -FilePath $UnityPath -ArgumentList @(
    '-batchmode', '-nographics', '-projectPath', $projectPath,
    '-executeMethod', 'JellyGate.Editor.JellyGateBuild.VerifyAndroidAppIcon279',
    '-logFile', $configureLog, '-quit'
) -WindowStyle Hidden -Wait -PassThru
$settingsPass = $configure.ExitCode -eq 0 -and
    $null -ne (Select-String -LiteralPath $configureLog -SimpleMatch 'QA_APP_ICON_279_PASS' | Select-Object -Last 1)
if (-not $settingsPass) { throw "Android app icon settings QA failed. See $configureLog" }

if (-not $SkipBuild) {
    $build = Start-Process -FilePath $UnityPath -ArgumentList @(
        '-batchmode', '-nographics', '-projectPath', $projectPath,
        '-executeMethod', 'JellyGate.Editor.JellyGateBuild.BuildAndroid',
        '-outputPath', $ApkPath, '-logFile', $buildLog, '-quit'
    ) -WindowStyle Hidden -Wait -PassThru
    if ($build.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $ApkPath)) {
        throw "Android APK build failed. See $buildLog"
    }
}
if (-not (Test-Path -LiteralPath $ApkPath)) { throw "APK not found: $ApkPath" }

Add-Type -AssemblyName System.Drawing
$sourceBitmap = [System.Drawing.Bitmap]::new($sourceIcon)
try {
    $sourceSquare = $sourceBitmap.Width -eq $sourceBitmap.Height -and $sourceBitmap.Width -ge 1024
    $cornerSamples = @(
        $sourceBitmap.GetPixel(0, 0),
        $sourceBitmap.GetPixel($sourceBitmap.Width - 1, 0),
        $sourceBitmap.GetPixel(0, $sourceBitmap.Height - 1),
        $sourceBitmap.GetPixel($sourceBitmap.Width - 1, $sourceBitmap.Height - 1)
    )
    $edgeToEdge = -not ($cornerSamples | Where-Object { $_.R -lt 5 -and $_.G -lt 5 -and $_.B -lt 5 })
} finally {
    $sourceBitmap.Dispose()
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$apk = [System.IO.Compression.ZipFile]::OpenRead($ApkPath)
try {
    $resourceEntries = @($apk.Entries | Where-Object { $_.FullName -match '^res/' })
} finally {
    $apk.Dispose()
}

$aapt2 = Get-ChildItem -LiteralPath (Join-Path $env:ANDROID_HOME 'build-tools') -Recurse -Filter aapt2.exe |
    Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
if ([string]::IsNullOrWhiteSpace($aapt2)) { throw 'aapt2.exe was not found in ANDROID_HOME.' }
$resourceDump = & $aapt2 dump resources $ApkPath
$mipmapStart = ($resourceDump | Select-String 'type mipmap' | Select-Object -First 1).LineNumber
if ($null -eq $mipmapStart) { throw 'APK does not contain a mipmap resource table.' }
$mipmapSection = $resourceDump[($mipmapStart - 1)..([Math]::Min($mipmapStart + 44, $resourceDump.Count - 1))]
$mipmapText = $mipmapSection -join "`n"
$requiredNames = @('mipmap/app_icon', 'mipmap/app_icon_round', 'mipmap/ic_launcher_background', 'mipmap/ic_launcher_foreground')
$requiredEntries = @()
foreach ($requiredName in $requiredNames) {
    if ($mipmapText -notmatch [regex]::Escape($requiredName)) { throw "Missing APK icon resource: $requiredName" }
    $requiredEntries += $requiredName
}
$iconFiles = @($mipmapSection | Select-String -Pattern 'res/[A-Za-z0-9_-]+\.(png|xml)' -AllMatches |
    ForEach-Object { $_.Matches.Value } | Sort-Object -Unique)
$apkIconsPass = $requiredEntries.Count -eq 4 -and $iconFiles.Count -ge 20
$passed = $settingsPass -and $sourceSquare -and $edgeToEdge -and $apkIconsPass
[ordered]@{
    version = '2.70.9'
    generatedAt = (Get-Date).ToString('o')
    passed = $passed
    sourceIcon = $sourceIcon
    sourceSquare = $sourceSquare
    edgeToEdgeNoBlackCorners = $edgeToEdge
    apk = $ApkPath
    apkBytes = (Get-Item -LiteralPath $ApkPath).Length
    requiredMipmapResources = $requiredEntries
    packagedIconFileCount = $iconFiles.Count
    packagedIconFiles = $iconFiles
    settingsLog = $configureLog
    buildLog = $buildLog
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $summaryPath -Encoding utf8

if (-not $passed) { throw "Crownfront v2.70.9 app icon QA failed. See $summaryPath" }
Write-Output "Crownfront v2.70.9 app icon QA passed. Summary: $summaryPath"
