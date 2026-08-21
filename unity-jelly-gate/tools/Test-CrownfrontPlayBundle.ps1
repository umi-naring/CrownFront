param(
    [string]$BundlePath = '',
    [string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.0.82f1\Editor\Unity.exe'
)

$ErrorActionPreference = 'Stop'
$projectPath = Split-Path -Parent $PSScriptRoot
$workspacePath = Split-Path -Parent $projectPath
if ([string]::IsNullOrWhiteSpace($BundlePath)) {
    $BundlePath = Join-Path $workspacePath 'outputs\Crownfront-v1.00-code3-security.aab'
}
$androidRoot = Join-Path (Split-Path -Parent $UnityPath) 'Data\PlaybackEngines\AndroidPlayer'
$java = Join-Path $androidRoot 'OpenJDK\bin\java.exe'
$jarsigner = Join-Path $androidRoot 'OpenJDK\bin\jarsigner.exe'
$keytool = Join-Path $androidRoot 'OpenJDK\bin\keytool.exe'
$bundletool = Join-Path $androidRoot 'Tools\bundletool-all-1.17.2.jar'
$summaryPath = Join-Path $workspacePath 'qa-logs\v1.00-code3-security\qa-summary.json'
$projectVersionPath = Join-Path $projectPath 'ProjectSettings\ProjectVersion.txt'
New-Item -ItemType Directory -Path (Split-Path -Parent $summaryPath) -Force | Out-Null

foreach ($required in @($BundlePath, $java, $jarsigner, $keytool, $bundletool, $projectVersionPath)) {
    if (-not (Test-Path -LiteralPath $required)) { throw "Required file not found: $required" }
}

$null = & $java -jar $bundletool validate --bundle=$BundlePath 2>&1
$bundleValid = $LASTEXITCODE -eq 0
$manifest = (& $java -jar $bundletool dump manifest --bundle=$BundlePath --module=base 2>&1) -join "`n"
$packageValid = $manifest.Contains('package="com.toykingdom.jellygate"')
$versionNameValid = $manifest.Contains('android:versionName="1.00"')
$versionCodeValid = $manifest.Contains('android:versionCode="3"')
$null = & $jarsigner -verify $BundlePath 2>&1
$signatureValid = $LASTEXITCODE -eq 0
$certificate = (& $keytool '-J-Duser.language=en' '-J-Duser.country=US' `
    -printcert -jarfile $BundlePath 2>&1) -join "`n"
$releaseCertificate = $certificate.Contains('CN=CROWNFRONT Upload') -and
                      -not $certificate.Contains('CN=Android Debug')
$projectVersion = Get-Content -LiteralPath $projectVersionPath -Raw
$patchedEditorValid = $projectVersion.Contains('6000.0.82f1')
$sha256 = (Get-FileHash -LiteralPath $BundlePath -Algorithm SHA256).Hash
$passed = $bundleValid -and $packageValid -and $versionNameValid -and $versionCodeValid -and
          $signatureValid -and $releaseCertificate -and $patchedEditorValid

[ordered]@{
    version='1.00'; generatedAt=(Get-Date).ToString('o'); passed=$passed
    bundleValid=$bundleValid; packageValid=$packageValid
    versionNameValid=$versionNameValid; versionCodeValid=$versionCodeValid
    signatureValid=$signatureValid; releaseCertificate=$releaseCertificate
    patchedEditorValid=$patchedEditorValid; unityEditorVersion='6000.0.82f1'
    package='com.toykingdom.jellygate'; versionCode=3; sha256=$sha256
    bundlePath=(Resolve-Path -LiteralPath $BundlePath).Path
} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $summaryPath -Encoding utf8

if (-not $passed) { throw "CROWNFRONT Play Bundle QA failed. See $summaryPath" }
Write-Output "CROWNFRONT Play Bundle QA passed: $summaryPath"
