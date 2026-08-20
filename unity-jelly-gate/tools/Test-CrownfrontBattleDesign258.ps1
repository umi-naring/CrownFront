$ErrorActionPreference = 'Stop'
$projectPath = Split-Path -Parent $PSScriptRoot
$workspacePath = Split-Path -Parent $projectPath
$logDirectory = Join-Path $workspacePath 'qa-logs\v2.58'
$outputPath = Join-Path $logDirectory 'static-battle-design.json'
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null

$gamePath = Join-Path $projectPath 'Assets\Scripts\Runtime\JellyGateGame.cs'
$playerPath = Join-Path $projectPath 'Assets\Scripts\Runtime\PlayerUnit.cs'
$variantPath = Join-Path $projectPath 'Assets\Scripts\Runtime\EnemyVariantCatalog.cs'
$guidePath = Join-Path $projectPath 'Assets\Scripts\Runtime\JellyGateGame.Guide.cs'
$game = Get-Content -LiteralPath $gamePath -Raw -Encoding utf8
$player = Get-Content -LiteralPath $playerPath -Raw -Encoding utf8
$variants = Get-Content -LiteralPath $variantPath -Raw -Encoding utf8
$guide = Get-Content -LiteralPath $guidePath -Raw -Encoding utf8

$checks = [ordered]@{
    mixedWaveLineups = $variants.Contains('MixedWaveLineups') -and $variants.Contains('ForWaveMember')
    roleUltimateContexts = $game.Contains('HasValidUltimateContext') -and
        $game.Contains('FindAllyNeedingProtection') -and $player.Contains('game.HasValidUltimateContext(this)')
    lancerRecovery = $game.Contains('CompleteUltimateRecoveryAndRetarget') -and
        $player.Contains('CompleteUltimateRecoveryAndRetarget')
    archerSkinVfx = $game.Contains('ArcherSkillVolleyEffectRoutine') -and
        $game.Contains('RoyalArrowRainRoutine')
    arrowBarrage = $game.Contains('RoyalArrowBarrageRoutine') -and
        $game.Contains('selected.Count < 3')
    augmentTouchScroll = $game.Contains('HandleAugmentSummaryTouchDrag') -and
        $game.Contains('augmentSummaryScroll = GUI.BeginScrollView')
    guideBossOnly = -not $guide.Contains('GuideEnemies[guide') -and
        $guide.Contains('DrawGuideBossRoster')
}

$pressure = @()
for ($round = 1; $round -le 50; $round++) {
    $count = 15 + ($round - 1) + [math]::Floor(($round - 1) / 3)
    $chapter = [math]::Min(9, [math]::Floor(($round - 1) / 5))
    $stage = ($round - 1) % 5
    $healthMultiplier = switch ($chapter) {
        0 { 1.14 } 1 { 1.12 } 2 { 1.10 } 3 { 1.08 } 4 { 1.06 }
        5 { 1.04 } 6 { 1.02 } 7 { .99 + $stage * .0125 } 8 { 1.0 } default { 1.02 }
    }
    $damageMultiplier = switch ($chapter) {
        { $_ -le 1 } { 1.08; break } { $_ -le 3 } { 1.05; break }
        { $_ -le 5 } { 1.02; break } 6 { 1.0 } 7 { .98 + $stage * .005 } default { 1.0 }
    }
    $health = (46 + $round * 10.4 + [math]::Pow($round, 1.22) * 1.75) * $healthMultiplier
    $pressure += [pscustomobject]@{
        round = $round; count = $count; health = [math]::Round($health, 2)
        damageMultiplier = $damageMultiplier
        pressure = [math]::Round($count * $health * $damageMultiplier, 2)
    }
}
$steps = for ($index = 1; $index -lt $pressure.Count; $index++) {
    [pscustomobject]@{ round = $pressure[$index].round; ratio =
        [math]::Round($pressure[$index].pressure / $pressure[$index - 1].pressure, 4) }
}
$laterSteps = @($steps | Where-Object round -ge 6)
$curvePassed = ($laterSteps | Where-Object { $_.ratio -lt .985 -or $_.ratio -gt 1.22 }).Count -eq 0
$checks.pressureCurve = $curvePassed

$result = [ordered]@{
    version = '2.58.0'; generatedAt = (Get-Date).ToString('o')
    passed = -not ($checks.Values -contains $false); checks = $checks
    curve = [ordered]@{
        round1 = $pressure[0]; round35 = $pressure[34]; round36 = $pressure[35]
        round40 = $pressure[39]; round50 = $pressure[49]
        minimumLaterStep = ($laterSteps.ratio | Measure-Object -Minimum).Minimum
        maximumLaterStep = ($laterSteps.ratio | Measure-Object -Maximum).Maximum
    }
}
$result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $outputPath -Encoding utf8
if (-not $result.passed) { throw "Battle design static QA failed. See $outputPath" }
Write-Output "Crownfront v2.58 static battle design QA passed: $outputPath"
