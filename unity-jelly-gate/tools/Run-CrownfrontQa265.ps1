param(
    [string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.0.34f1\Editor\Unity.exe',
    [switch]$ReuseBuild,
    [switch]$FocusedOnly
)

$invoke = @{ UnityPath = $UnityPath }
if ($ReuseBuild) { $invoke.ReuseBuild = $true }
if ($FocusedOnly) { $invoke.FocusedOnly = $true }
& (Join-Path $PSScriptRoot 'Run-CrownfrontQa264.ps1') @invoke
exit $LASTEXITCODE
