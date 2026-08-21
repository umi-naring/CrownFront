param(
    [string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.0.82f1\Editor\Unity.exe',
    [string]$ProjectPath = '',
    [string]$AndroidNdkPath = '',
    [string]$KeystorePath = '',
    [Parameter(Mandatory=$true)][string]$KeystorePassword,
    [string]$Alias = 'crownfront-upload',
    [Parameter(Mandatory=$true)][string]$AliasPassword,
    [string]$OutputPath = ''
)

$ErrorActionPreference = 'Stop'
$sourceProjectPath = Split-Path -Parent $PSScriptRoot
$projectPath = if ([string]::IsNullOrWhiteSpace($ProjectPath)) { $sourceProjectPath } else { (Resolve-Path -LiteralPath $ProjectPath).Path }
$workspacePath = Split-Path -Parent $sourceProjectPath
$userProfilePath = [Environment]::GetFolderPath('UserProfile')
if ([string]::IsNullOrWhiteSpace($userProfilePath)) { $userProfilePath = 'C:\Users\Administrator' }
$localApplicationData = [Environment]::GetFolderPath('LocalApplicationData')
if ([string]::IsNullOrWhiteSpace($localApplicationData)) { $localApplicationData = Join-Path $userProfilePath 'AppData\Local' }
$roamingApplicationData = [Environment]::GetFolderPath('ApplicationData')
if ([string]::IsNullOrWhiteSpace($roamingApplicationData)) { $roamingApplicationData = Join-Path $userProfilePath 'AppData\Roaming' }
$temporaryPath = Join-Path $localApplicationData 'Temp'
$env:USERPROFILE = $userProfilePath
$env:HOME = $userProfilePath
$env:LOCALAPPDATA = $localApplicationData
$env:APPDATA = $roamingApplicationData
$env:TEMP = $temporaryPath
$env:TMP = $temporaryPath
if ([string]::IsNullOrWhiteSpace($env:SystemRoot)) { $env:SystemRoot = 'C:\Windows' }
if ([string]::IsNullOrWhiteSpace($env:SystemDrive)) { $env:SystemDrive = 'C:' }
if ([string]::IsNullOrWhiteSpace($env:PROGRAMDATA)) { $env:PROGRAMDATA = 'C:\ProgramData' }
if ([string]::IsNullOrWhiteSpace($env:ALLUSERSPROFILE)) { $env:ALLUSERSPROFILE = 'C:\ProgramData' }
if ([string]::IsNullOrWhiteSpace($env:ProgramFiles)) { $env:ProgramFiles = 'C:\Program Files' }
if ([string]::IsNullOrWhiteSpace($env:CommonProgramFiles)) { $env:CommonProgramFiles = 'C:\Program Files\Common Files' }
$env:ComSpec = 'C:\Windows\System32\cmd.exe'
$env:PATHEXT = '.COM;.EXE;.BAT;.CMD'
if ([string]::IsNullOrWhiteSpace($env:PATH)) {
    $env:PATH = 'C:\Windows\System32;C:\Windows;C:\Windows\System32\Wbem;C:\Windows\System32\WindowsPowerShell\v1.0'
}
if ([string]::IsNullOrWhiteSpace($KeystorePath)) {
    $KeystorePath = Join-Path $workspacePath 'release-keys\Crownfront-upload.keystore'
}
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $workspacePath 'outputs\Crownfront-v1.00-code4.aab'
}
$logPath = Join-Path $workspacePath 'android-aab-build-100-code4.log'

if (-not (Test-Path -LiteralPath $UnityPath)) { throw "Unity executable not found: $UnityPath" }
if (-not (Test-Path -LiteralPath $KeystorePath)) { throw "Upload keystore not found: $KeystorePath" }

$env:CROWNFRONT_UPLOAD_KEYSTORE = (Resolve-Path -LiteralPath $KeystorePath).Path
$env:CROWNFRONT_UPLOAD_KEYSTORE_PASS = $KeystorePassword
$env:CROWNFRONT_UPLOAD_ALIAS = $Alias
$env:CROWNFRONT_UPLOAD_ALIAS_PASS = $AliasPassword
if (-not [string]::IsNullOrWhiteSpace($AndroidNdkPath)) {
    $env:CROWNFRONT_ANDROID_NDK = (Resolve-Path -LiteralPath $AndroidNdkPath).Path
}
$unityPreferences = 'HKCU:\Software\Unity Technologies\Unity Editor 5.x'
# Unity 6000.0.60f1+ has a detector-cache defect that can report Platform Tools 0.0
# after Android external-tool preferences are opened or rewritten. Select Unity's
# version-matched embedded SDK before launching a fresh batch process and do not
# touch the external-tool settings from inside that process.
New-ItemProperty -Path $unityPreferences -Name 'SdkUseEmbedded_h968012308' -PropertyType DWord -Value 1 -Force | Out-Null
try {
    $arguments = @(
        # Keep Package Manager enabled: Unity's Android platform module initializes part of
        # its SDK detector through the package service. Disabling UPM makes a valid Platform
        # Tools 36 install appear as version 0.0 in Unity 6000.0.82f1 batch mode.
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
    Remove-Item Env:CROWNFRONT_ANDROID_NDK -ErrorAction SilentlyContinue
}
