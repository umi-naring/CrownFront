param(
    [switch]$Rebuild,
    [int]$TimeoutSeconds = 180
)

$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
$project = Join-Path $workspace 'unity-jelly-gate'
$qaDirectory = Join-Path $workspace 'tmp\Crownfront-QA-251'
$qaExecutable = Join-Path $qaDirectory 'Crownfront-QA.exe'
$logDirectory = Join-Path $workspace 'qa-logs\v2.51'
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.0.34f1\Editor\Unity.exe'
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null

if ($Rebuild -or -not (Test-Path -LiteralPath $qaExecutable)) {
    New-Item -ItemType Directory -Path $qaDirectory -Force | Out-Null
    $buildLog = Join-Path $logDirectory 'build.log'
    $arguments = @('-batchmode','-nographics','-projectPath',$project,
        '-executeMethod','JellyGate.Editor.JellyGateBuild.BuildWindowsQa',
        '-outputPath',$qaExecutable,'-logFile',$buildLog,'-quit')
    $build = Start-Process -FilePath $unity -ArgumentList $arguments -PassThru -Wait -WindowStyle Hidden
    if ($build.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $qaExecutable)) {
        throw "Windows QA build failed. See $buildLog"
    }
}

$balanceRequired = @('cards=68','pools=12:13:15:14:14','support=True',
    'power=True:0.24-1.28','chance=True:0.0200:0.0365:0.0300','roles=True',
    'unique=True:24','locale=True:True:True')
$checks = @(
    @{ Name='balance-pass1'; Flag='-qaAugmentBalance251'; Marker='QA_AUGMENT_BALANCE_251'; Required=$balanceRequired },
    @{ Name='balance-pass2'; Flag='-qaAugmentBalance251'; Marker='QA_AUGMENT_BALANCE_251'; Required=$balanceRequired },
    @{ Name='balance-pass3'; Flag='-qaAugmentBalance251'; Marker='QA_AUGMENT_BALANCE_251'; Required=$balanceRequired },
    @{ Name='revamp-regression'; Flag='-qaAugmentRevamp250'; Marker='QA_AUGMENT_REVAMP_250';
       Required=@('gate=True','roles=True','tiers=True:0.24-1.28','unique=True:24','pools=True','effects=True','locale=True:True','cards=68') },
    @{ Name='offer-rules'; Flag='-qaAugmentRules'; Marker='QA_AUGMENT_RULES';
       Required=@('sameTier=True','removedAfterUnlock=True','recruitsUnlockOnce=True') },
    @{ Name='hill-augments'; Flag='-qaHillAugments'; Marker='QA_HILL_AUGMENTS';
       Required=@('cards=True','stats=True','ground=True','stacking=True','separated=True','liveLock=True') },
    @{ Name='guide'; Flag='-qaGuide'; Marker='QA_GUIDE';
       Required=@('modal=True','tabs=True','units=10','regular=40','boss=10','augments=68','back=True') },
    @{ Name='combat'; Flag='-qaCombatSystems'; Marker='QA_COMBAT_SYSTEMS';
       Required=@('heroEffects=True','detection=True','hillTarget=True','gateLock=True','bossProfiles=10','passives=10:True') },
    @{ Name='performance'; Flag='-qaPerformance240'; Marker='QA_PERFORMANCE_240';
       Required=@('bounded=True','cache=True','frame=True','memory=True') }
)

$results = @()
foreach ($check in $checks) {
    $log = Join-Path $logDirectory ($check.Name + '.log')
    if (Test-Path -LiteralPath $log) { Remove-Item -LiteralPath $log -Force }
    $process = Start-Process -FilePath $qaExecutable `
        -ArgumentList @('-batchmode','-nographics',$check.Flag,'-logFile',$log) `
        -PassThru -WindowStyle Hidden
    if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
        $process.Kill()
        throw "QA timed out: $($check.Name)"
    }
    if ($process.ExitCode -ne 0) {
        throw "QA failed: $($check.Name), exit $($process.ExitCode). See $log"
    }
    $line = Select-String -LiteralPath $log -Pattern ('^' + [regex]::Escape($check.Marker) + ' ') |
        Select-Object -Last 1 -ExpandProperty Line
    if ([string]::IsNullOrWhiteSpace($line)) { throw "QA marker missing: $($check.Marker)" }
    foreach ($required in $check.Required) {
        if (-not $line.Contains($required)) {
            throw "QA assertion '$required' missing in $($check.Name): $line"
        }
    }
    $results += [pscustomobject]@{ Test=$check.Name; Result='PASS'; Evidence=$line }
    Write-Host "[PASS] $($check.Name)"
}

$summary = Join-Path $logDirectory 'summary.json'
$results | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath $summary -Encoding UTF8
Write-Host "CROWNFRONT v2.51 QA: PASS ($($results.Count)/$($checks.Count))"
Write-Host "Summary: $summary"

