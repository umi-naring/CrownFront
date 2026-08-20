param(
    [string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.0.34f1\Editor\Unity.exe',
    [switch]$ReuseBuild
)

$ErrorActionPreference = 'Stop'
$projectPath = Split-Path -Parent $PSScriptRoot
$workspacePath = Split-Path -Parent $projectPath
$qaDirectory = Join-Path $workspacePath 'qa-artifacts\Crownfront-QA-2682'
$qaExecutable = Join-Path $qaDirectory 'Crownfront-QA.exe'
$logDirectory = Join-Path $workspacePath 'qa-logs\v2.68.2'
$buildLog = Join-Path $logDirectory 'runner-windows-player-build.log'
$summaryPath = Join-Path $logDirectory 'qa-summary.json'

if (-not (Test-Path -LiteralPath $UnityPath)) { throw "Unity executable not found: $UnityPath" }
New-Item -ItemType Directory -Path $qaDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null

if (-not $ReuseBuild -or -not (Test-Path -LiteralPath $qaExecutable)) {
    $arguments = @(
        '-batchmode', '-nographics', '-projectPath', $projectPath,
        '-executeMethod', 'JellyGate.Editor.JellyGateBuild.BuildWindowsQa',
        '-outputPath', $qaExecutable, '-logFile', $buildLog, '-quit'
    )
    $build = Start-Process -FilePath $UnityPath -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
    $compileErrors = if (Test-Path -LiteralPath $buildLog) {
        @(Select-String -LiteralPath $buildLog -Pattern 'error CS|Scripts have compiler errors|BuildFailedException')
    } else { @('missing build log') }
    if ($build.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $qaExecutable) -or $compileErrors.Count -gt 0) {
        throw "Crownfront v2.68.2 QA player build failed. See $buildLog"
    }
}

$probes = @(
    @{ Name = 'guide-unit-scroll'; Argument = '-qaGuideUnitScroll2682'; Pattern = 'QA_GUIDE_UNIT_SCROLL_2682 passed=True' },
    @{ Name = 'guide-regression'; Argument = '-qaGuide'; Pattern = 'QA_GUIDE modal=True' }
)
$results = @()
foreach ($probe in $probes) {
    $runtimeLog = Join-Path $logDirectory "$($probe.Name).log"
    $process = Start-Process -FilePath $qaExecutable `
        -ArgumentList @('-batchmode', '-nographics', $probe.Argument, '-logFile', $runtimeLog) `
        -WindowStyle Hidden -Wait -PassThru
    $match = Get-Content -LiteralPath $runtimeLog |
        Select-String -SimpleMatch $probe.Pattern | Select-Object -Last 1
    $exceptions = @(Select-String -LiteralPath $runtimeLog `
        -Pattern 'NullReferenceException|ArgumentException|ArgumentOutOfRangeException|IndexOutOfRangeException')
    $passed = $process.ExitCode -eq 0 -and $null -ne $match -and $exceptions.Count -eq 0
    $results += [ordered]@{
        name = $probe.Name
        passed = $passed
        exitCode = $process.ExitCode
        exceptions = $exceptions.Count
        result = if ($null -ne $match) { $match.Line.Trim() } else { '' }
        runtimeLog = $runtimeLog
    }
    Write-Output "$($probe.Name): passed=$passed exit=$($process.ExitCode) exceptions=$($exceptions.Count) $($results[-1].result)"
}
$passed = -not ($results.passed -contains $false)
$summary = [ordered]@{
    version = '2.68.2'
    generatedAt = (Get-Date).ToString('o')
    passed = $passed
    buildLog = $buildLog
    probes = $results
}
$summary | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $summaryPath -Encoding utf8
if (-not $passed) { throw "Crownfront v2.68.2 guide scroll QA failed. See $summaryPath" }
Write-Output "Crownfront v2.68.2 QA completed. Summary: $summaryPath"
