param(
    [string]$BundlePath = '',
    [string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.0.82f1\Editor\Unity.exe'
)

$ErrorActionPreference = 'Stop'
$projectPath = Split-Path -Parent $PSScriptRoot
$workspacePath = Split-Path -Parent $projectPath
if ([string]::IsNullOrWhiteSpace($BundlePath)) {
    $BundlePath = Join-Path $workspacePath 'outputs\Crownfront-v1.00-code15.aab'
}
$androidRoot = Join-Path (Split-Path -Parent $UnityPath) 'Data\PlaybackEngines\AndroidPlayer'
$java = Join-Path $androidRoot 'OpenJDK\bin\java.exe'
$jarsigner = Join-Path $androidRoot 'OpenJDK\bin\jarsigner.exe'
$keytool = Join-Path $androidRoot 'OpenJDK\bin\keytool.exe'
$bundletool = Join-Path $androidRoot 'Tools\bundletool-all-1.17.2.jar'
$summaryPath = Join-Path $workspacePath 'qa-logs\v1.00-code15\play-bundle-summary.json'
$projectVersionPath = Join-Path $projectPath 'ProjectSettings\ProjectVersion.txt'
New-Item -ItemType Directory -Path (Split-Path -Parent $summaryPath) -Force | Out-Null

foreach ($required in @($BundlePath, $java, $jarsigner, $keytool, $bundletool, $projectVersionPath)) {
    if (-not (Test-Path -LiteralPath $required)) { throw "Required file not found: $required" }
}

# PowerShell 5.1 can promote native stderr records to terminating errors and
# does not reliably retain LASTEXITCODE in all hosted invocations. Capture both
# streams with Start-Process and use the real process exit code instead.
function Invoke-NativeProcess([string]$FilePath, [string[]]$Arguments) {
    $stdoutPath = [IO.Path]::GetTempFileName()
    $stderrPath = [IO.Path]::GetTempFileName()
    try {
        $process = Start-Process -FilePath $FilePath -ArgumentList $Arguments `
            -NoNewWindow -PassThru -Wait `
            -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath
        $stdout = if (Test-Path -LiteralPath $stdoutPath) { Get-Content -LiteralPath $stdoutPath -Raw } else { '' }
        $stderr = if (Test-Path -LiteralPath $stderrPath) { Get-Content -LiteralPath $stderrPath -Raw } else { '' }
        return [pscustomobject]@{ ExitCode = $process.ExitCode; Output = ($stdout + "`n" + $stderr) }
    }
    finally {
        Remove-Item -LiteralPath $stdoutPath, $stderrPath -Force -ErrorAction SilentlyContinue
    }
}

$bundletoolQuoted = '"' + $bundletool + '"'
$bundleQuoted = '"' + $BundlePath + '"'
$validateResult = Invoke-NativeProcess $java @('-jar', $bundletoolQuoted, 'validate', "--bundle=$bundleQuoted")
$bundleValid = $validateResult.ExitCode -eq 0
$manifestResult = Invoke-NativeProcess $java @('-jar', $bundletoolQuoted, 'dump', 'manifest', "--bundle=$bundleQuoted", '--module=base')
$manifest = $manifestResult.Output
$packageValid = $manifest.Contains('package="com.toykingdom.jellygate"')
$versionNameValid = $manifest.Contains('android:versionName="1.00"')
$versionCodeValid = $manifest.Contains('android:versionCode="15"')
$signatureResult = Invoke-NativeProcess $jarsigner @('-verify', $bundleQuoted)
$signatureValid = $signatureResult.ExitCode -eq 0
$certificateResult = Invoke-NativeProcess $keytool @('-J-Duser.language=en', '-J-Duser.country=US', '-printcert', '-jarfile', $bundleQuoted)
$certificate = $certificateResult.Output
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
    package='com.toykingdom.jellygate'; versionCode=15; sha256=$sha256
    bundlePath=(Resolve-Path -LiteralPath $BundlePath).Path
} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $summaryPath -Encoding utf8

if (-not $passed) { throw "CROWNFRONT Play Bundle QA failed. See $summaryPath" }
Write-Output "CROWNFRONT Play Bundle QA passed: $summaryPath"
