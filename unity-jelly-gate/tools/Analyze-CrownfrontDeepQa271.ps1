$ErrorActionPreference = 'Stop'
$projectPath = Split-Path -Parent $PSScriptRoot
$workspacePath = Split-Path -Parent $projectPath
$logDirectory = Join-Path $workspacePath 'qa-logs\deep-audit-271'
$summaryPath = Join-Path $logDirectory 'qa-findings.json'
$reportPath = Join-Path $logDirectory 'deep-qa-findings.md'

if (-not (Test-Path -LiteralPath $logDirectory)) { throw "QA logs not found: $logDirectory" }
$logs = @(Get-ChildItem -LiteralPath $logDirectory -Filter '*.log' | Sort-Object Name)

function Last-Match([string]$pattern) {
    foreach ($file in ($logs | Sort-Object Name -Descending)) {
        $match = Select-String -LiteralPath $file.FullName -Pattern $pattern | Select-Object -Last 1
        if ($null -ne $match) {
            return [pscustomobject]@{ File = $file.FullName; Line = $match.Line.Trim() }
        }
    }
    return $null
}

$findings = @()
function Add-Finding([string]$severity, [string]$department, [string]$id,
    [string]$title, [string]$evidence, [string]$impact, [string]$recommendation) {
    $script:findings += [pscustomobject][ordered]@{
        severity = $severity; department = $department; id = $id; title = $title
        evidence = $evidence; impact = $impact; recommendation = $recommendation
    }
}

$nav = Last-Match '^NAV_AUDIT samples='
if ($null -ne $nav -and -not $nav.Line.Contains('samples=1014/1014')) {
    Add-Finding 'S1' 'Level/Navigation' 'NAV-001' 'Four spawn-corridor samples are disconnected' `
        "$($nav.Line); blocked at x=+-4.82 y=-6.72 for lanes 0-3" `
        'Enemies may detour or hesitate immediately after spawning.' `
        'Repaint both lower spawn mouths with radius clearance and require 1014/1014.'
}

$release268 = Last-Match '^QA_RELEASE_268 '
if ($null -ne $release268 -and $release268.Line.Contains('passed=False')) {
    Add-Finding 'S1' 'Animation/Sprite' 'SPR-001' 'Jelly front-cell leak and bomber art misassignment' `
        $release268.Line 'Adjacent frames can bleed during motion/attack and mage/bomber identity is mixed.' `
        'Separate jelly mage/bomber directional sources and add transparent cell padding.'
}

$release263 = Last-Match '^QA_RELEASE_263 '
if ($null -ne $release263 -and $release263.Line.Contains('passed=False')) {
    Add-Finding 'S1' 'Animation/Sprite' 'SPR-002' 'Jelly mage front/rear identity mismatch' `
        $release263.Line 'Hat, silhouette, and body change between facing directions.' `
        'Replace generic jelly cells for South through East with authored matching rear directions.'
}

$spriteRange = Last-Match '^QA_SPRITE_RANGE_252 '
if ($null -ne $spriteRange -and $spriteRange.Line.Contains('passed=False')) {
    Add-Finding 'S1' 'Animation/Art' 'SPR-003' 'Three enemy profiles fail sprite bounds/identity audit' `
        $spriteRange.Line 'jelly_mage, armor_render, and silence_shroud are not consistently sized/grounded.' `
        'Re-author opaque bounds, pivots, PPU, and rear silhouettes to one standard.'
}

$movement = Last-Match '^QA_MOVEMENT_POLISH '
if ($null -ne $movement -and $movement.Line.Contains('side=False')) {
    Add-Finding 'S1' 'Animation' 'ANI-001' 'Side-walk animation diversity is insufficient' `
        $movement.Line 'Some side directions expose only 2-3 unique frames, producing skating.' `
        'Require at least six unique side/diagonal poses with torso counter-motion and foot plants.'
}

$rangedGate = Last-Match '^QA_RANGED_GATE '
if ($null -ne $rangedGate -and $rangedGate.Line.Contains('distant=False')) {
    Add-Finding 'S1' 'Combat AI' 'AI-001' 'Ranged enemy does not retain gate distance' `
        $rangedGate.Line 'It damages the gate but enters contact range.' `
        'Separate gate preferred range from arrival tolerance and require distant=True.'
}

$recruit = Last-Match '^QA_RECRUIT_HERO_SETUP_FAILED'
if ($null -ne $recruit) {
    Add-Finding 'S2' 'Roster/Placement' 'ROS-001' 'Recruited-hero QA setup fails' `
        "QA_RECRUIT_HERO_SETUP_FAILED in $($recruit.File)" `
        'Hero evolution for augment-recruited units cannot be certified.' `
        'Audit current placement points against the latest mask and log each failed unit.'
}

$roster = Last-Match '^QA_FULL_ROSTER '
if ($null -ne $roster -and $roster.Line.Contains('frames=False')) {
    Add-Finding 'S1' 'Roster/Animation/Balance' 'ROS-002' 'Roster frame coverage and price curve fail' `
        $roster.Line 'Some roster animation sets/core hero upgrades are incomplete and 7-coin efficiency trails 6-coin.' `
        'Log failures per archetype, restore core hero sets, and make average efficiency monotonic by cost.'
}

$hill = Last-Match '^QA_HILL_AUGMENTS '
if ($null -ne $hill -and $hill.Line.Contains('stats=False')) {
    Add-Finding 'S1' 'Augment/Combat' 'AUG-001' 'Hill augment cards do not apply promised combat stats' `
        $hill.Line 'Cards, stacking, region separation, and movement lock work, but damage/range/armor application fails.' `
        'Trace StackPower keys into ApplyUnitAugments and test each promised value independently.'
}

$battle = Last-Match '^QA_BATTLE_DESIGN_258 '
if ($null -ne $battle -and $battle.Line.Contains('curve=False')) {
    Add-Finding 'S1' 'Level/Balance' 'BAL-001' 'Fifty-round pressure curve is discontinuous' `
        $battle.Line 'Family composition is valid, but chapter entries dip and R6-R7 spikes; late rounds flatten/reverse.' `
        'Retune per-round counts/HP multipliers and constrain adjacent pressure ratios.'
}

$lancerFailure = $null -ne $battle -and $battle.Line.Contains('lancer=False')
if ($lancerFailure) {
    Add-Finding 'S1' 'Combat/Ultimate' 'ULT-001' 'Lancer ultimate fails recovery and retarget transition' `
        $battle.Line 'After the dive the lancer is alive but inactive, does not move, and gains no distance.' `
        'End the ultimate state explicitly, clear forced motion, then run target reacquisition.'
}

$presentation = Last-Match '^QA_PRESENTATION_253 '
if ($null -ne $presentation -and $presentation.Line.Contains('portrait=False')) {
    Add-Finding 'S1' 'UI/Portrait' 'UI-001' 'Boss and jelly-mage information portraits clip' `
        $presentation.Line 'Boss bodies and jelly-mage cells do not fit the current portrait layout.' `
        'Normalize portrait source bounds and apply framed contain-fit with reserved bottom margin.'
}

$ultimateAudit = Last-Match '^QA_ULTIMATE_SPRITE_AUDIT_256 '
if ($null -ne $ultimateAudit -and $ultimateAudit.Line.Contains('passed=False')) {
    Add-Finding 'S1' 'Boss/Animation' 'BOSS-001' 'All ten boss directional frame sets fail strict audit' `
        $ultimateAudit.Line 'Boss frame count/source invariants fail despite no unsafe opaque edge in the fallback path.' `
        'Regenerate ten boss sheets with fixed padded cells and remove fallback/repaired sources.'
}

$bossGoogle = Last-Match '^QA_BOSS_ARCHER_GOOGLE_254 '
if ($null -ne $bossGoogle -and $bossGoogle.Line.Contains('passed=False')) {
    Add-Finding 'S1' 'Boss/VFX' 'BOSS-002' 'Boss source repair count and archer arrow audit fail' `
        $bossGoogle.Line 'Boss directional cells rely on hundreds of repairs; archer target arrows are 0/7.' `
        'Replace repaired boss sources and restore one visible skin-aware arrow on every target.'
}

$polish243 = Last-Match '^QA_POLISH_243 '
if ($null -ne $polish243 -and $polish243.Line.Contains('lifecycle=True/False')) {
    Add-Finding 'S2' 'Presentation/VFX' 'VFX-001' 'Transient hit effect lifecycle does not fully finish' `
        $polish243.Line 'Anticipation and peak frames render, but one cleanup/end-state assertion fails.' `
        'Audit transient-effect completion and pool return after its final frame.'
}

$special = Last-Match '^QA_SPECIAL_ENEMIES '
if ($null -ne $special -and $special.Line.Contains('flyerRules=False')) {
    Add-Finding 'S1' 'Enemy/Targeting' 'AIR-001' 'Flying-enemy target eligibility fails' `
        $special.Line 'Damage resistances pass, but only permitted ranged targeting rules are not enforced.' `
        'Separate air-target capability from range and reject melee target acquisition.'
}

$performance = Last-Match '^QA_PERFORMANCE_240 '
if ($null -ne $performance -and $performance.Line.Contains('frame=False')) {
    Add-Finding 'S2' 'Performance' 'PERF-001' '96-enemy stress frame target missed' `
        $performance.Line 'Late-wave performance risk; Null-GPU headless FPS is not release evidence.' `
        'Profile on a mid-tier Android device and require 30fps with a bounded p95 frame time.'
}

Add-Finding 'S2' 'Sound' 'AUD-001' 'No authored audio assets are present' `
    'Asset audit found 0 WAV/MP3/OGG files; ToyVoiceBarks synthesizes cues and music at runtime.' `
    'Procedural tones cannot meet the requested natural voice acting and polished soundtrack quality.' `
    'Produce authored menu/battle music, UI cues, unit barks, boss cues, and run loudness/listening QA.'

$googleConfigPath = Join-Path $projectPath 'Assets\Resources\crownfront-google-services.json'
$googleConfig = if (Test-Path -LiteralPath $googleConfigPath) {
    Get-Content -LiteralPath $googleConfigPath -Raw | ConvertFrom-Json
} else { $null }
if ($null -eq $googleConfig -or $googleConfig.useTestAds -eq $true -or
    [string]$googleConfig.interstitialAdUnitId -like 'ca-app-pub-3940256099942544/*') {
    Add-Finding 'S1' 'Monetization/Release' 'MON-001' 'Release configuration still uses Google test ads' `
        "useTestAds=$($googleConfig.useTestAds), interstitial=$($googleConfig.interstitialAdUnitId)" `
        'A published build will not use the studio production ad inventory and cannot generate intended ad revenue.' `
        'Create production AdMob units, switch only release builds to production IDs, and retain test IDs for QA builds.'
}

$prefsLogs = @($logs | Where-Object {
    Select-String -LiteralPath $_.FullName -SimpleMatch 'PlayerPrefsException' -Quiet
})
if ($prefsLogs.Count -gt 0) {
    Add-Finding 'ENV' 'Test Environment/Save' 'ENV-001' 'PlayerPrefs write denial blocks multiple probes' `
        "Affected log files: $($prefsLogs.Count); Could not store preference value" `
        'Save, skin equip, level-up, and round-completion probes are inconclusive in this sandbox.' `
        'Rerun on writable Windows or Android storage before release approval.'
}

$passingEvidence = @()
foreach ($pattern in @(
    '^QA_COMBAT_STATS_270 passed=True', '^QA_ENEMY_PRESENTATION_269 passed=True',
    '^QA_SPRITE_264 passed=True', '^QA_BATTLEFIELD_264 passed=True',
    '^QA_AUGMENT_RUNTIME_264 passed=True', '^QA_GUIDE_BOSS_FRAME_266 layout=True',
    '^QA_GUIDE_UNIT_SCROLL_2682 passed=True', '^QA_NAVIGATION_COMBAT .*leaks=0',
    '^QA_BACK_MENU confirmDismissed=True', '^QA_SPECIAL_ENEMIES_252 passed=True',
    '^QA_AUGMENT_RULES sameTier=True', '^QA_GUIDE modal=True tabs=True',
    '^QA_AUGMENT_TIER_252 tier=Bronze passed=True', '^QA_AUGMENT_TIER_252 tier=Silver passed=True',
    '^QA_AUGMENT_TIER_252 tier=Gold passed=True', '^QA_AUGMENT_TIER_252 tier=Platinum passed=True',
    '^QA_AUGMENT_TIER_252 tier=Diamond passed=True')) {
    $match = Last-Match $pattern
    if ($null -ne $match) { $passingEvidence += $match.Line }
}

$summary = [ordered]@{
    generatedAt = (Get-Date).ToString('o'); logCount = $logs.Count
    findings = $findings; severity = [ordered]@{
        S1 = @($findings | Where-Object severity -eq 'S1').Count
        S2 = @($findings | Where-Object severity -eq 'S2').Count
        ENV = @($findings | Where-Object severity -eq 'ENV').Count
    }
    verifiedPasses = $passingEvidence
    deviceRequired = @(
        'Google Play Games real sign-in', 'Google Play Billing purchase/restore',
        'Ad fill/close callback/remove-ads suppression', 'Android 96-enemy performance',
        'Human voice and BGM listening review')
}
$summary | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath $summaryPath -Encoding utf8

$report = @('# Crownfront Deep QA 271 Findings', '',
    "- Generated: $($summary.generatedAt)", "- Logs analyzed: $($logs.Count)",
    "- S1 critical findings: $($summary.severity.S1)",
    "- S2 major findings: $($summary.severity.S2)",
    "- Environment blockers: $($summary.severity.ENV)", '', '## Findings', '')
foreach ($item in $findings) {
    $report += "### [$($item.severity)] $($item.id) - $($item.title)"
    $report += ''
    $report += "- Department: $($item.department)"
    $report += "- Evidence: $($item.evidence)"
    $report += "- Impact: $($item.impact)"
    $report += "- Recommendation: $($item.recommendation)"
    $report += ''
}
$report += '## Verified core passes'
$report += ''
foreach ($line in $passingEvidence) { $report += "- $line" }
$report += ''
$report += '## Device or external-service validation required'
$report += ''
foreach ($item in $summary.deviceRequired) { $report += "- $item" }
$report | Set-Content -LiteralPath $reportPath -Encoding utf8

Write-Output "Findings report created: $reportPath"
Write-Output "S1=$($summary.severity.S1) S2=$($summary.severity.S2) ENV=$($summary.severity.ENV) passes=$($passingEvidence.Count)"
