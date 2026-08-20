param(
    [string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.0.34f1\Editor\Unity.exe',
    [string]$KeystorePath = '',
    [Parameter(Mandatory=$true)][string]$KeystorePassword,
    [string]$Alias = 'crownfront-upload',
    [Parameter(Mandatory=$true)][string]$AliasPassword,
    [string]$OutputPath = ''
)

$ErrorActionPreference = 'Stop'
$projectPath = Split-Path -Parent $PSScriptRoot
$workspacePath = Split-Path -Parent $projectPath
if ([string]::IsNullOrWhiteSpace($KeystorePath)) {
    $KeystorePath = Join-Path $workspacePath 'release-keys\Crownfront-upload.keystore'
}
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $workspacePath 'outputs\Crownfront-v1.00.aab'
}
$logPath = Join-Path $workspacePath 'android-aab-build-100.log'

if (-not (Test-Path -LiteralPath $UnityPath)) { throw "Unity executable not found: $UnityPath" }
if (-not (Test-Path -LiteralPath $KeystorePath)) { throw "Upload keystore not found: $KeystorePath" }

$env:CROWNFRONT_UPLOAD_KEYSTORE = (Resolve-Path -LiteralPath $KeystorePath).Path
$env:CROWNFRONT_UPLOAD_KEYSTORE_PASS = $KeystorePassword
$env:CROWNFRONT_UPLOAD_ALIAS = $Alias
$env:CROWNFRONT_UPLOAD_ALIAS_PASS = $AliasPassword
try {
    $arguments = @(
        '-batchmode', '-nographics', '-projectPath', $projectPath,
        '-executeMethod', 'JellyGate.Editor.JellyGateBuild.BuildAndroidAppBundle',
        '-outputPath', $OutputPath, '-logFile', $logPath, '-quit'
    )
    $build = Start-Process -FilePath $UnityPath -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
    $errors = @(Select-String -LiteralPath $logPath -Pattern 'error CS|BuildFailedException|AAB build failed')
    if ($build.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $OutputPath) -or $errors.Count -gt 0) {
        throw "CROWNFRONT Google Play AAB build failed. See $logPath"
    }
    Write-Output "CROWNFRONT Google Play AAB built: $OutputPath"
}
finally {
    Remove-Item Env:CROWNFRONT_UPLOAD_KEYSTORE -ErrorAction SilentlyContinue
    Remove-Item Env:CROWNFRONT_UPLOAD_KEYSTORE_PASS -ErrorAction SilentlyContinue
    Remove-Item Env:CROWNFRONT_UPLOAD_ALIAS -ErrorAction SilentlyContinue
    Remove-Item Env:CROWNFRONT_UPLOAD_ALIAS_PASS -ErrorAction SilentlyContinue
}
