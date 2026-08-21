$ErrorActionPreference = 'Stop'
$workspacePath = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$keyInfoPath = Join-Path $workspacePath 'release-keys\IMPORTANT-Crownfront-upload-key.txt'

if (-not (Test-Path -LiteralPath $keyInfoPath)) {
    throw "Saved upload-key metadata was not found."
}

foreach ($line in Get-Content -LiteralPath $keyInfoPath) {
    if ([string]::IsNullOrWhiteSpace($line)) {
        continue
    }
    if ($line -match '^\s*([^:=]+?)\s*[:=]\s*(.+)$') {
        $label = $Matches[1].Trim()
        $valueLength = $Matches[2].Trim().Length
        Write-Output ("{0}=<redacted,length:{1}>" -f $label, $valueLength)
        continue
    }
    Write-Output ("<unlabeled-redacted,length:{0}>" -f $line.Trim().Length)
}
