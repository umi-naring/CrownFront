param(
    [string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.0.34f1\Editor\Unity.exe',
    [int]$TimeoutSeconds = 150,
    [int]$StartAt = 1,
    [switch]$SkipCompile
)

$ErrorActionPreference = 'Stop'
$projectPath = Split-Path -Parent $PSScriptRoot
$workspacePath = Split-Path -Parent $projectPath
$qaDirectory = Join-Path $workspacePath 'qa-artifacts\Crownfront-QA-270'
$qaExecutable = Join-Path $qaDirectory 'Crownfront-QA.exe'
$managedAssembly = Join-Path $projectPath 'Temp\bin\Debug\Assembly-CSharp.dll'
$playerAssembly = Join-Path $qaDirectory 'Crownfront-QA_Data\Managed\Assembly-CSharp.dll'
$logDirectory = Join-Path $workspacePath 'qa-logs\deep-audit-271'
$summaryPath = Join-Path $logDirectory 'qa-summary.json'
$reportPath = Join-Path $logDirectory 'deep-qa-report.md'

New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null

if (-not $SkipCompile) {
    & (Join-Path $PSScriptRoot 'Run-CrownfrontQa270.ps1') -UnityPath $UnityPath
    $baselineSummaryPath = Join-Path $workspacePath 'qa-logs\v2.70\qa-summary.json'
    $baselinePassed = if (Test-Path -LiteralPath $baselineSummaryPath) {
        (Get-Content -LiteralPath $baselineSummaryPath -Raw | ConvertFrom-Json).passed -eq $true
    } else { $false }
    if (-not $baselinePassed) { throw 'Current-source compile and baseline QA failed.' }
}
if (-not (Test-Path -LiteralPath $qaExecutable)) { throw "QA player not found: $qaExecutable" }

$staticResults = @()
function Add-StaticResult([string]$name, [bool]$passed, [string]$detail) {
    $script:staticResults += [pscustomobject][ordered]@{ category = 'Static'; name = $name; passed = $passed; detail = $detail }
    Write-Output "STATIC $name passed=$passed $detail"
}

$compileHash = if (Test-Path -LiteralPath $managedAssembly) {
    (Get-FileHash -LiteralPath $managedAssembly -Algorithm SHA256).Hash
} else { '' }
$playerHash = if (Test-Path -LiteralPath $playerAssembly) {
    (Get-FileHash -LiteralPath $playerAssembly -Algorithm SHA256).Hash
} else { '' }
Add-StaticResult 'managed-assembly-identity' ($compileHash -ne '' -and $compileHash -eq $playerHash) `
    "compiled=$compileHash player=$playerHash"

$projectSettings = Get-Content -LiteralPath (Join-Path $projectPath 'ProjectSettings\ProjectSettings.asset') -Raw
$versionValid = $projectSettings.Contains('bundleVersion: 2.70.0') -and
    $projectSettings.Contains('AndroidBundleVersionCode: 113')
Add-StaticResult 'version-and-android-code' $versionValid 'expected=2.70.0/113'

$guidRows = Get-ChildItem (Join-Path $projectPath 'Assets') -Recurse -Filter '*.meta' | ForEach-Object {
    $match = Select-String -LiteralPath $_.FullName -Pattern '^guid: ([0-9a-f]{32})$' | Select-Object -First 1
    if ($null -ne $match) { [pscustomobject]@{ Guid = $match.Matches[0].Groups[1].Value; File = $_.FullName } }
}
$duplicateGuids = @($guidRows | Group-Object Guid | Where-Object Count -gt 1)
Add-StaticResult 'unity-asset-guid-uniqueness' ($duplicateGuids.Count -eq 0) "duplicates=$($duplicateGuids.Count) assets=$($guidRows.Count)"

$sourceFiles = Get-ChildItem (Join-Path $projectPath 'Assets\Scripts') -Recurse -Filter '*.cs'
$sourceText = ($sourceFiles | Get-Content -Raw) -join "`n"
$combatPlumbing = $sourceText.Contains('PhysicalPenetration') -and $sourceText.Contains('MagicPenetration') -and
    $sourceText.Contains('CombatMath.MitigatedDamage')
Add-StaticResult 'penetration-plumbing-present' $combatPlumbing 'player/enemy/projectile/combat math symbols'

$probes = @(
    @{ Category='Navigation'; Name='startup-navigation-audit'; Argument='-qaCombatStats270'; Marker='NAV_AUDIT '; Expect='samples=1014/1014' },
    @{ Category='Combat'; Name='combat-stats-layout'; Argument='-qaCombatStats270'; Marker='QA_COMBAT_STATS_270'; Expect='passed=True' },
    @{ Category='Animation'; Name='every-enemy-state'; Argument='-qaEnemyPresentation269'; Marker='QA_ENEMY_PRESENTATION_269'; Expect='passed=True' },
    @{ Category='Release'; Name='release-blockers'; Argument='-qaRelease268'; Marker='QA_RELEASE_268'; Expect='passed=True' },
    @{ Category='Performance'; Name='shield-movement'; Argument='-qaTankMovement267'; Marker='QA_TANK_MOVEMENT_267'; Expect='passed=True'; Timeout=600 },
    @{ Category='Save'; Name='forced-close-checkpoint'; Argument='-qaCheckpoint266'; Marker='QA_CHECKPOINT_266'; Expect='write=True battleStable=True restore=True choice=True'; Timeout=90 },
    @{ Category='UI'; Name='boss-guide-frame'; Argument='-qaGuideBossFrame266'; Marker='QA_GUIDE_BOSS_FRAME_266'; Expect='layout=True bodies=True' },
    @{ Category='Animation'; Name='sprite-exhaustive'; Argument='-qaSprite264'; Marker='QA_SPRITE_264'; Expect='passed=True' },
    @{ Category='Level'; Name='battlefield-density'; Argument='-qaBattlefield264'; Marker='QA_BATTLEFIELD_264'; Expect='passed=True' },
    @{ Category='Augment'; Name='every-augment-runtime'; Argument='-qaAugmentRuntime264'; Marker='QA_AUGMENT_RUNTIME_264'; Expect='passed=True' },
    @{ Category='Animation'; Name='boss-pose-export-audit'; Argument='-qaRelease263'; Marker='QA_RELEASE_263'; Expect='passed=True' },
    @{ Category='Animation'; Name='sprite-terrain-chairman-regression'; Argument='-qaRelease262'; Marker='QA_RELEASE_262'; Expect='passed=True' },
    @{ Category='Level'; Name='all-round-runtime'; Argument='-qaRelease261'; Marker='QA_RELEASE_261'; Expect='passed=True' },
    @{ Category='Animation'; Name='sprite-regression-260'; Argument='-qaRelease260'; Marker='QA_RELEASE_260'; Expect='passed=True' },
    @{ Category='Animation'; Name='all-unit-directions-ultimates'; Argument='-qaUltimateSpriteAudit256'; Marker='QA_ULTIMATE_SPRITE_AUDIT_256'; Expect='passed=True' },
    @{ Category='VFX'; Name='all-skin-combat-vfx'; Argument='-qaSkinCombatVfx255'; Marker='QA_SKIN_COMBAT_VFX_255'; Expect='passed=True' },
    @{ Category='Commerce'; Name='default-skin-commerce'; Argument='-qaDefaultSkinCommerce257'; Marker='QA_DEFAULT_SKIN_COMMERCE_257'; Expect='passed=True' },
    @{ Category='UI'; Name='presentation-localization'; Argument='-qaPresentation253'; Marker='QA_PRESENTATION_253'; Expect='passed=True' },
    @{ Category='Boss'; Name='boss-archer-google-structure'; Argument='-qaBossArcherGoogle254'; Marker='QA_BOSS_ARCHER_GOOGLE_254'; Expect='passed=True' },
    @{ Category='Combat'; Name='sprite-range-sanity'; Argument='-qaSpriteRange252'; Marker='QA_SPRITE_RANGE_252'; Expect='passed=True' },
    @{ Category='Enemy'; Name='special-enemy-schedule'; Argument='-qaSpecialEnemies252'; Marker='QA_SPECIAL_ENEMIES_252'; Expect='passed=True' },
    @{ Category='Commerce'; Name='monetization-structure'; Argument='-qaMonetization252'; Marker='QA_MONETIZATION_252'; Expect='structural=True' },
    @{ Category='Level'; Name='50-round-battle-design'; Argument='-qaBattleDesign258'; Marker='QA_BATTLE_DESIGN_258'; Expect='passed=True' },
    @{ Category='UI'; Name='guide-unit-drag-scroll'; Argument='-qaGuideUnitScroll2682'; Marker='QA_GUIDE_UNIT_SCROLL_2682'; Expect='passed=True' },
    @{ Category='Animation'; Name='jelly-front-rear-identity'; Argument='-qaJellyIdentity268'; Marker='QA_JELLY_IDENTITY_268'; Expect='ready=True' },
    @{ Category='Control'; Name='selection-orders-hold'; Argument='-qaControl'; Marker='QA_CONTROL'; Expect='' },
    @{ Category='Augment'; Name='offer-unique-rules'; Argument='-qaAugmentRules'; Marker='QA_AUGMENT_RULES'; Expect='sameTier=True removedAfterUnlock=True recruitsUnlockOnce=True' },
    @{ Category='Combat'; Name='area-mage-splash'; Argument='-qaAreaMage'; Marker='QA_AREA_MAGE'; Expect='primaryHit=True nearbyHit=True' },
    @{ Category='Roster'; Name='recruit-hero'; Argument='-qaRecruitHero'; Marker='QA_RECRUIT_HERO'; Expect='ready=True' },
    @{ Category='Roster'; Name='full-roster-art-stats'; Argument='-qaFullRoster'; Marker='QA_FULL_ROSTER'; Expect='' },
    @{ Category='Augment'; Name='hill-augment'; Argument='-qaHillAugments'; Marker='QA_HILL_AUGMENTS'; Expect='' },
    @{ Category='Combat'; Name='hero-detection-boss-combat'; Argument='-qaCombatSystems'; Marker='QA_COMBAT_SYSTEMS'; Expect='' },
    @{ Category='UI'; Name='back-menu-modal'; Argument='-qaBackMenu'; Marker='QA_BACK_MENU'; Expect='confirmDismissed=True menuDismissed=True' },
    @{ Category='Navigation'; Name='navigation-mask-combat'; Argument='-qaNavigationCombat'; Marker='QA_NAVIGATION_COMBAT'; Expect='leaks=0' },
    @{ Category='Navigation'; Name='enemy-flow-stall-recovery'; Argument='-qaEnemyFlow'; Marker='QA_ENEMY_FLOW'; Expect='' },
    @{ Category='Combat'; Name='ranged-gate'; Argument='-qaRangedGate'; Marker='QA_RANGED_GATE'; Expect='distant=True damaged=True' },
    @{ Category='Enemy'; Name='flying-resistance-rules'; Argument='-qaSpecialEnemies'; Marker='QA_SPECIAL_ENEMIES'; Expect='wispPhysical=True wispMagic=True flyerRules=True' },
    @{ Category='Animation'; Name='movement-grounding'; Argument='-qaMovementPolish'; Marker='QA_MOVEMENT_POLISH'; Expect='' },
    @{ Category='Animation'; Name='weapon-consistency'; Argument='-qaWeaponConsistency'; Marker='QA_WEAPON_CONSISTENCY'; Expect='provenance=True stableCoverage=True' },
    @{ Category='VFX'; Name='combat-presentation'; Argument='-qaCombatPresentation236'; Marker='QA_COMBAT_PRESENTATION_236'; Expect='' },
    @{ Category='VFX'; Name='vfx-full-mapping'; Argument='-qaVfx246'; Marker='QA_VFX_246'; Expect='' },
    @{ Category='Performance'; Name='camera-shake-budget'; Argument='-qaCameraShakeBudget237'; Marker='QA_CAMERA_SHAKE_BUDGET_237'; Expect='' },
    @{ Category='Performance'; Name='96-enemy-stress'; Argument='-qaPerformance240'; Marker='QA_PERFORMANCE_240'; Expect='frame=True memory=True' },
    @{ Category='UI'; Name='restart-modal-portrait-rail'; Argument='-qaPolish242'; Marker='QA_POLISH_242'; Expect='' },
    @{ Category='Presentation'; Name='loading-vfx-polish'; Argument='-qaPolish243'; Marker='QA_POLISH_243'; Expect='' },
    @{ Category='UI'; Name='guide-modal-tabs'; Argument='-qaGuide'; Marker='QA_GUIDE'; Expect='modal=True tabs=True' },
    @{ Category='Augment'; Name='bronze-monte-carlo'; Argument='-qaAugmentBronze252'; Marker='QA_AUGMENT_TIER_252'; Expect='tier=Bronze passed=True' },
    @{ Category='Augment'; Name='silver-monte-carlo'; Argument='-qaAugmentSilver252'; Marker='QA_AUGMENT_TIER_252'; Expect='tier=Silver passed=True' },
    @{ Category='Augment'; Name='gold-monte-carlo'; Argument='-qaAugmentGold252'; Marker='QA_AUGMENT_TIER_252'; Expect='tier=Gold passed=True' },
    @{ Category='Augment'; Name='platinum-monte-carlo'; Argument='-qaAugmentPlatinum252'; Marker='QA_AUGMENT_TIER_252'; Expect='tier=Platinum passed=True' },
    @{ Category='Augment'; Name='diamond-monte-carlo'; Argument='-qaAugmentDiamond252'; Marker='QA_AUGMENT_TIER_252'; Expect='tier=Diamond passed=True' }
)

$results = @()
$index = 0
foreach ($probe in $probes) {
    $index++
    if ($index -lt $StartAt) { continue }
    $runtimeLog = Join-Path $logDirectory ('{0:D2}-{1}.log' -f $index, $probe.Name)
    $started = Get-Date
    $process = Start-Process -FilePath $qaExecutable `
        -ArgumentList @('-batchmode', '-nographics', $probe.Argument, '-logFile', $runtimeLog) `
        -WindowStyle Hidden -PassThru
    $probeTimeout = if ($probe.ContainsKey('Timeout')) { [int]$probe.Timeout } else { $TimeoutSeconds }
    $finished = $process.WaitForExit($probeTimeout * 1000)
    $timedOut = -not $finished
    if ($timedOut) {
        try { $process.Kill() } catch { }
        $process.WaitForExit()
    }
    $elapsed = [Math]::Round(((Get-Date) - $started).TotalSeconds, 2)
    $content = if (Test-Path -LiteralPath $runtimeLog) { Get-Content -LiteralPath $runtimeLog } else { @() }
    $match = $content | Select-String -SimpleMatch $probe.Marker | Select-Object -Last 1
    $resultLine = if ($null -ne $match) { $match.Line.Trim() } else { '' }
    $exceptions = @($content | Select-String -Pattern 'NullReferenceException|ArgumentException|ArgumentOutOfRangeException|IndexOutOfRangeException|MissingReferenceException|StackOverflowException|PlayerPrefsException')
    # Headless QA intentionally has no graphics device, so Unity emits unsupported-shader
    # errors. The startup navigation audit is also shared by every probe and is reported once
    # by the dedicated startup-navigation-audit probe above instead of poisoning all departments.
    $errorLogs = @($content | Select-String -Pattern 'QA_.*FAILED' | Where-Object {
        -not $_.Line.Contains('NAV_AUDIT_FAILED')
    })
    $expected = [string]$probe.Expect
    $expectationMet = [string]::IsNullOrEmpty($expected) -or $resultLine.Contains($expected)
    $exitCode = if ($timedOut) { -999 } else { $process.ExitCode }
    $passed = -not $timedOut -and $exitCode -eq 0 -and $null -ne $match -and
        $expectationMet -and $exceptions.Count -eq 0 -and $errorLogs.Count -eq 0
    $peakMb = if ($timedOut) { 0 } else { [Math]::Round($process.PeakWorkingSet64 / 1MB, 1) }
    $results += [pscustomobject][ordered]@{
        category = $probe.Category; name = $probe.Name; argument = $probe.Argument
        passed = $passed; timedOut = $timedOut; exitCode = $exitCode
        exceptions = $exceptions.Count; errorLogs = $errorLogs.Count
        elapsedSeconds = $elapsed; peakWorkingSetMb = $peakMb
        expectation = $expected; result = $resultLine; log = $runtimeLog
    }
    Write-Output ("[{0}/{1}] {2}/{3}: passed={4} exit={5} time={6}s exceptions={7} {8}" -f `
        $index, $probes.Count, $probe.Category, $probe.Name, $passed, $exitCode, $elapsed, $exceptions.Count, $resultLine)
    Get-Process 'UnityCrashHandler64' -ErrorAction SilentlyContinue |
        Wait-Process -Timeout 5 -ErrorAction SilentlyContinue
}

$allResults = @($staticResults) + @($results)
$failures = @($allResults | Where-Object { -not $_.passed })
$byCategory = @($results | Group-Object category | Sort-Object Name | ForEach-Object {
    [pscustomobject][ordered]@{
        category = $_.Name; total = $_.Count
        passed = @($_.Group | Where-Object passed).Count
        failed = @($_.Group | Where-Object { -not $_.passed }).Count
        elapsedSeconds = [Math]::Round(($_.Group | Measure-Object elapsedSeconds -Sum).Sum, 2)
    }
})
$external = @(
    [ordered]@{ area='Google Play Games'; status='DEVICE_REQUIRED'; reason='Real account consent, SHA/package registration and Play Console publication cannot be proven by the offline Windows player.' },
    [ordered]@{ area='Google Play Billing'; status='DEVICE_REQUIRED'; reason='Purchase UI structure is audited, but a real licensed Play account and published test product are required.' },
    [ordered]@{ area='Rewarded/interstitial ads'; status='DEVICE_REQUIRED'; reason='Configuration is audited; actual fill, close callback and remove-ads suppression require an Android device/network.' },
    [ordered]@{ area='Audio subjective mix'; status='HUMAN_REVIEW'; reason='File/runtime presence can be automated, but natural voice acting and perceived loudness require listening.' }
)
$summary = [ordered]@{
    audit = 'Crownfront deep QA 271'; version = '2.70.0'; generatedAt = (Get-Date).ToString('o')
    passed = $failures.Count -eq 0; static = $staticResults; categories = $byCategory
    runtimeProbes = $results; failures = $failures; externalValidation = $external
    totalRuntimeSeconds = [Math]::Round(($results | Measure-Object elapsedSeconds -Sum).Sum, 2)
}
$summary | ConvertTo-Json -Depth 9 | Set-Content -LiteralPath $summaryPath -Encoding utf8

$report = @()
$report += '# Crownfront Deep QA 271'
$report += ''
$report += "- Generated: $($summary.generatedAt)"
$report += "- Runtime probes: $($results.Count)"
$report += "- Static probes: $($staticResults.Count)"
$report += "- Automated failures: $($failures.Count)"
$report += "- Runtime seconds: $($summary.totalRuntimeSeconds)"
$report += ''
$report += '## Department summary'
$report += ''
$report += '| Department | Passed | Failed | Runtime(s) |'
$report += '|---|---:|---:|---:|'
foreach ($row in $byCategory) { $report += "| $($row.category) | $($row.passed) | $($row.failed) | $($row.elapsedSeconds) |" }
$report += ''
$report += '## Automated defects'
$report += ''
if ($failures.Count -eq 0) { $report += 'No automated failures were detected.' }
else {
    foreach ($failure in $failures) {
        $detail = if ($failure.Contains('result')) { $failure.result } else { $failure.detail }
        $report += "- **$($failure.category) / $($failure.name)**: $detail"
    }
}
$report += ''
$report += '## External or human validation still required'
$report += ''
foreach ($item in $external) { $report += "- **$($item.area)** [$($item.status)]: $($item.reason)" }
$report += ''
$report += '## Runtime probe details'
$report += ''
foreach ($item in $results) {
    $report += "- [$($item.passed)] $($item.category) / $($item.name) — $($item.elapsedSeconds)s — $($item.result)"
}
$report | Set-Content -LiteralPath $reportPath -Encoding utf8

Write-Output "Deep QA complete. passed=$($summary.passed) failures=$($failures.Count) summary=$summaryPath report=$reportPath"
if ($failures.Count -gt 0) { exit 2 }
