param(
    [string]$ExportRoot = 'C:\Users\Administrator\Documents\Codex\2026-07-22\new-chat\tmp\Crownfront-QA-263\qa-exported-boss-frames-v263\runtime',
    [string]$OutputRoot = 'C:\Users\Administrator\Documents\Codex\2026-07-22\new-chat\qa-artifacts\v2.63\boss-contact-sheets-runtime'
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null

$bosses = Get-ChildItem -LiteralPath $ExportRoot -Filter '*.bmp' |
    ForEach-Object {
        if ($_.BaseName -match '^runtime-(.+?)-(walk|attack|skill)-r') { $matches[1] }
    } | Sort-Object -Unique

$states = @('walk', 'attack', 'skill')
$directions = @('front', 'front-diagonal', 'side', 'back-diagonal', 'back')
$tileWidth = 360
$tileHeight = 340
$headerHeight = 56

foreach ($boss in $bosses) {
    $canvas = [System.Drawing.Bitmap]::new($tileWidth * 5, $headerHeight + $tileHeight * 3)
    $graphics = [System.Drawing.Graphics]::FromImage($canvas)
    $graphics.Clear([System.Drawing.Color]::FromArgb(21, 27, 37))
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $titleFont = [System.Drawing.Font]::new('Segoe UI', 20, [System.Drawing.FontStyle]::Bold)
    $labelFont = [System.Drawing.Font]::new('Segoe UI', 13, [System.Drawing.FontStyle]::Bold)
    $brush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::White)
    $mutedBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(180, 205, 220))
    $gridPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(72, 91, 113), 2)
    $graphics.DrawString($boss, $titleFont, $brush, 18, 10)

    for ($stateIndex = 0; $stateIndex -lt $states.Count; $stateIndex++) {
        $state = $states[$stateIndex]
        for ($directionIndex = 0; $directionIndex -lt $directions.Count; $directionIndex++) {
            $x = $directionIndex * $tileWidth
            $y = $headerHeight + $stateIndex * $tileHeight
            $graphics.DrawRectangle($gridPen, $x, $y, $tileWidth - 1, $tileHeight - 1)
            $graphics.DrawString("$state / $($directions[$directionIndex])", $labelFont, $mutedBrush, $x + 10, $y + 8)
            $candidate = Get-ChildItem -LiteralPath $ExportRoot -Filter "runtime-$boss-$state-r$directionIndex-c2.bmp" |
                Select-Object -First 1
            if ($null -eq $candidate) { continue }
            $sprite = [System.Drawing.Image]::FromFile($candidate.FullName)
            try {
                $availableWidth = $tileWidth - 30
                $availableHeight = $tileHeight - 52
                $scale = [Math]::Min($availableWidth / $sprite.Width, $availableHeight / $sprite.Height)
                $drawWidth = [Math]::Max(1, [int]($sprite.Width * $scale))
                $drawHeight = [Math]::Max(1, [int]($sprite.Height * $scale))
                $drawX = $x + [int](($tileWidth - $drawWidth) / 2)
                $drawY = $y + 42 + [int](($availableHeight - $drawHeight) / 2)
                $graphics.DrawImage($sprite, $drawX, $drawY, $drawWidth, $drawHeight)
            }
            finally { $sprite.Dispose() }
        }
    }

    $output = Join-Path $OutputRoot "boss-$boss-contact-sheet-v263.png"
    $canvas.Save($output, [System.Drawing.Imaging.ImageFormat]::Png)
    $gridPen.Dispose(); $mutedBrush.Dispose(); $brush.Dispose(); $labelFont.Dispose(); $titleFont.Dispose()
    $graphics.Dispose(); $canvas.Dispose()
    Write-Output $output
}
