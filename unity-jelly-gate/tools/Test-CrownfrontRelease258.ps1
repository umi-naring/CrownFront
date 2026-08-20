param(
    [string]$ApkPath = 'C:\Users\Administrator\Documents\Codex\2026-07-22\new-chat\outputs\Crownfront-v2.58.0.apk',
    [switch]$RequireProductionConfiguration
)

$ErrorActionPreference = 'Stop'
$projectPath = Split-Path -Parent $PSScriptRoot
$workspacePath = Split-Path -Parent $projectPath
$legacyChecker = Join-Path $PSScriptRoot 'Test-CrownfrontRelease257.ps1'
$legacyArguments = @{ ApkPath = $ApkPath }
if ($RequireProductionConfiguration) { $legacyArguments.RequireProductionConfiguration = $true }
& $legacyChecker @legacyArguments | Out-Null

$legacyReportPath = Join-Path $workspacePath 'qa-logs\v2.57\release-preflight.json'
$report = Get-Content -LiteralPath $legacyReportPath -Raw -Encoding utf8 | ConvertFrom-Json
$sdkRoot = Join-Path $env:LOCALAPPDATA 'Android\Sdk'
$buildTools = Get-ChildItem -LiteralPath (Join-Path $sdkRoot 'build-tools') -Directory |
    Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
$aapt = if ($buildTools) { Join-Path $buildTools.FullName 'aapt.exe' } else { '' }
$badging = if (Test-Path -LiteralPath $aapt) { @(& $aapt dump badging $ApkPath) } else { @() }
$versionNameMatches = $null -ne ($badging | Select-String -SimpleMatch "versionName='2.58.0'" |
    Select-Object -First 1)
$versionCodeMatches = $null -ne ($badging | Select-String -Pattern "versionCode='99'" |
    Select-Object -First 1)

$result = [ordered]@{}
foreach ($property in $report.PSObject.Properties) { $result[$property.Name] = $property.Value }
$result.version = '2.58.0'
$result.versionNameMatches = $versionNameMatches
$result.versionCodeMatches = $versionCodeMatches
$result.releasePassed = [bool]$report.structuralFlowPassed -and $versionNameMatches -and $versionCodeMatches
$outputDirectory = Join-Path $workspacePath 'qa-logs\v2.58'
$outputPath = Join-Path $outputDirectory 'release-preflight.json'
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$result | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $outputPath -Encoding utf8
$result | ConvertTo-Json -Depth 6
if (-not $result.releasePassed) { throw "Crownfront v2.58 release preflight failed. See $outputPath" }
