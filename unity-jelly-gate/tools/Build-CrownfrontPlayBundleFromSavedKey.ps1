param(
    [string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.0.82f1\Editor\Unity.exe',
    [string]$ProjectPath = '',
    [string]$AndroidNdkPath = ''
)

$ErrorActionPreference = 'Stop'
$workspacePath = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$keyInfoPath = Join-Path $workspacePath 'release-keys\IMPORTANT-Crownfront-upload-key.txt'
$buildScript = Join-Path $PSScriptRoot 'Build-CrownfrontPlayBundle.ps1'
$outputPath = Join-Path $workspacePath 'outputs\Crownfront-v1.00-code26.aab'

if (-not (Test-Path -LiteralPath $keyInfoPath)) {
    throw 'Saved upload-key metadata was not found.'
}

$saved = @{}
foreach ($line in Get-Content -LiteralPath $keyInfoPath) {
    if ($line -match '^\s*([^:=]+?)\s*[:=]\s*(.+)$') {
        $saved[$Matches[1].Trim()] = $Matches[2].Trim()
    }
}

$keystorePassword = $saved['Keystore password']
$aliasPassword = $saved['Alias password']
$alias = $saved['Alias']
if ([string]::IsNullOrWhiteSpace($keystorePassword) -or
    [string]::IsNullOrWhiteSpace($aliasPassword) -or
    [string]::IsNullOrWhiteSpace($alias)) {
    throw 'Saved upload-key metadata is incomplete.'
}

try {
    & $buildScript -UnityPath $UnityPath -ProjectPath $ProjectPath -AndroidNdkPath $AndroidNdkPath -KeystorePassword $keystorePassword -AliasPassword $aliasPassword -Alias $alias -OutputPath $outputPath
    if (-not (Test-Path -LiteralPath $outputPath)) {
        throw 'Signed AAB was not produced.'
    }
    Write-Output "SIGNED_AAB_READY=$outputPath"
}
finally {
    $keystorePassword = $null
    $aliasPassword = $null
    $saved.Clear()
}
