param(
    [switch]$WhatIf
)

$ErrorActionPreference = 'Stop'
$workspacePath = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$projectPath = Join-Path $workspacePath 'unity-jelly-gate'

if (-not (Test-Path -LiteralPath $projectPath -PathType Container)) {
    throw "Refusing cleanup because the CROWNFRONT Unity project was not found under: $workspacePath"
}

function Assert-WorkspacePath {
    param([Parameter(Mandatory = $true)][string]$LiteralPath)

    $resolved = (Resolve-Path -LiteralPath $LiteralPath).Path
    $prefix = $workspacePath.TrimEnd('\') + '\'
    if (-not $resolved.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to modify a path outside the workspace: $resolved"
    }
    return $resolved
}

$removedFiles = [System.Collections.Generic.List[string]]::new()
$removedDirectories = [System.Collections.Generic.List[string]]::new()
$reclaimedBytes = 0L

function Remove-WorkspaceFile {
    param([Parameter(Mandatory = $true)][System.IO.FileInfo]$File)

    $target = Assert-WorkspacePath -LiteralPath $File.FullName
    $script:reclaimedBytes += $File.Length
    $script:removedFiles.Add($target)
    if (-not $WhatIf) {
        Remove-Item -LiteralPath $target -Force
    }
}

function Remove-WorkspaceDirectory {
    param([Parameter(Mandatory = $true)][System.IO.DirectoryInfo]$Directory)

    $target = Assert-WorkspacePath -LiteralPath $Directory.FullName
    $size = (Get-ChildItem -LiteralPath $target -File -Recurse -Force -ErrorAction SilentlyContinue |
        Measure-Object -Property Length -Sum).Sum
    if ($null -ne $size) { $script:reclaimedBytes += [long]$size }
    $script:removedDirectories.Add($target)
    if (-not $WhatIf) {
        Remove-Item -LiteralPath $target -Recurse -Force
    }
}

# Root-level build and QA logs are transient. Keep only the latest signed-AAB build log.
Get-ChildItem -LiteralPath $workspacePath -File -Filter '*.log' |
    Where-Object { $_.Name -ne 'android-aab-build-100-code25.log' } |
    ForEach-Object { Remove-WorkspaceFile -File $_ }

$outputsPath = Join-Path $workspacePath 'outputs'
if (Test-Path -LiteralPath $outputsPath -PathType Container) {
    # Logs, raw BMP captures, obsolete packages, and the PPM probe are superseded artifacts.
    Get-ChildItem -LiteralPath $outputsPath -File |
        Where-Object {
            $_.Extension -in '.log', '.bmp', '.apk' -or
            ($_.Extension -eq '.aab' -and $_.Name -ne 'Crownfront-v1.00-code25.aab') -or
            $_.Name -eq 'ppm-head.txt'
        } |
        ForEach-Object { Remove-WorkspaceFile -File $_ }
}

# Preserve only the currently released code-25 evidence and current full-pose runner artifacts.
$qaLogsPath = Join-Path $workspacePath 'qa-logs'
if (Test-Path -LiteralPath $qaLogsPath -PathType Container) {
    Get-ChildItem -LiteralPath $qaLogsPath -Force |
        Where-Object { $_.Name -ne 'v1.00-code25' } |
        ForEach-Object {
            if ($_.PSIsContainer) { Remove-WorkspaceDirectory -Directory $_ }
            else { Remove-WorkspaceFile -File $_ }
        }
}

$qaArtifactsPath = Join-Path $workspacePath 'qa-artifacts'
if (Test-Path -LiteralPath $qaArtifactsPath -PathType Container) {
    Get-ChildItem -LiteralPath $qaArtifactsPath -File -Force |
        ForEach-Object { Remove-WorkspaceFile -File $_ }
    Get-ChildItem -LiteralPath $qaArtifactsPath -Directory -Force |
        Where-Object { $_.Name -ne 'Crownfront-QA-320' } |
        ForEach-Object { Remove-WorkspaceDirectory -Directory $_ }
}

[ordered]@{
    whatIf = [bool]$WhatIf
    removedFiles = $removedFiles.Count
    removedDirectories = $removedDirectories.Count
    reclaimedBytes = $reclaimedBytes
    reclaimedMB = [math]::Round($reclaimedBytes / 1MB, 2)
    preservedAab = (Join-Path $outputsPath 'Crownfront-v1.00-code25.aab')
    preservedBuildLog = (Join-Path $workspacePath 'android-aab-build-100-code25.log')
    preservedQa = (Join-Path $qaLogsPath 'v1.00-code25')
} | ConvertTo-Json
