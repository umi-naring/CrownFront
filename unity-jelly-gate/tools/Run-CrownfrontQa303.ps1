param(
    [string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.0.82f1\Editor\Unity.exe',
    [switch]$ReuseBuild
)

$ErrorActionPreference = 'Stop'
$projectPath = Split-Path -Parent $PSScriptRoot
$workspacePath = Split-Path -Parent $projectPath
$qaDirectory = Join-Path $workspacePath 'qa-artifacts\Crownfront-QA-303'
$qaExecutable = Join-Path $qaDirectory 'Crownfront-QA.exe'
$logDirectory = Join-Path $workspacePath 'qa-logs\v1.00-code11'
$buildLog = Join-Path $logDirectory 'windows-player-build.log'
$summaryPath = Join-Path $logDirectory 'qa-summary.json'
New-Item -ItemType Directory -Path $qaDirectory, $logDirectory -Force | Out-Null

if (-not $ReuseBuild -or -not (Test-Path -LiteralPath $qaExecutable)) {
    $build = Start-Process -FilePath $UnityPath -ArgumentList @(
        '-batchmode', '-nographics', '-projectPath', $projectPath,
        '-executeMethod', 'JellyGate.Editor.JellyGateBuild.BuildWindowsQa',
        '-outputPath', $qaExecutable, '-logFile', $buildLog, '-quit'
    ) -WindowStyle Hidden -Wait -PassThru
    if ($build.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $qaExecutable) -or
        @(Select-String -LiteralPath $buildLog -Pattern 'error CS|Scripts have compiler errors|BuildFailedException').Count -gt 0) {
        throw "CROWNFRONT code 11 QA build failed. See $buildLog"
    }
}

$probes = @(
    @{ Name='release-303'; Argument='-qaRelease303'; Pattern='QA_RELEASE_303 passed=True' },
    @{ Name='hud-regression-302'; Argument='-qaRelease302'; Pattern='QA_RELEASE_302 passed=True' },
    @{ Name='economy-300'; Argument='-qaEconomy300'; Pattern='QA_ECONOMY_300 passed=True' }
)
$results = @()
foreach ($probe in $probes) {
    $runtimeLog = Join-Path $logDirectory "$($probe.Name).log"
    $process = Start-Process -FilePath $qaExecutable -ArgumentList @(
        '-batchmode', '-nographics', $probe.Argument, '-logFile', $runtimeLog
    ) -WindowStyle Hidden -Wait -PassThru
    $match = Get-Content -LiteralPath $runtimeLog | Select-String -SimpleMatch $probe.Pattern | Select-Object -Last 1
    $exceptions = @(Select-String -LiteralPath $runtimeLog -Pattern 'NullReferenceException|ArgumentException|IndexOutOfRangeException')
    $passed = $process.ExitCode -eq 0 -and $null -ne $match -and $exceptions.Count -eq 0
    $results += [ordered]@{ name=$probe.Name; passed=$passed; exitCode=$process.ExitCode;
        exceptions=$exceptions.Count; result=if ($match) { $match.Line.Trim() } else { '' }; runtimeLog=$runtimeLog }
    Write-Output "$($probe.Name): passed=$passed exit=$($process.ExitCode)"
}
$passed = -not ($results.passed -contains $false)
[ordered]@{ version='1.00'; versionCode=11; generatedAt=(Get-Date).ToString('o');
    passed=$passed; buildLog=$buildLog; probes=$results } |
    ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $summaryPath -Encoding utf8
if (-not $passed) { throw "CROWNFRONT code 11 QA failed. See $summaryPath" }
Write-Output "CROWNFRONT code 11 QA completed: $summaryPath"
