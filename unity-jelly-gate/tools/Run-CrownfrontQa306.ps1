param(
    [string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.0.82f1\Editor\Unity.exe',
    [switch]$ReuseBuild
)

$ErrorActionPreference = 'Stop'
$projectPath = Split-Path -Parent $PSScriptRoot
$workspacePath = Split-Path -Parent $projectPath
$qaDirectory = Join-Path $workspacePath 'qa-artifacts\Crownfront-QA-306'
$qaExecutable = Join-Path $qaDirectory 'Crownfront-QA.exe'
$logDirectory = Join-Path $workspacePath 'qa-logs\v1.00-code15'
$buildLog = Join-Path $logDirectory 'windows-player-build.log'
$summaryPath = Join-Path $logDirectory 'qa-summary.json'
New-Item -ItemType Directory -Path $qaDirectory, $logDirectory -Force | Out-Null
$env:USERPROFILE = 'C:\Users\Administrator'
$env:LOCALAPPDATA = 'C:\Users\Administrator\AppData\Local'
$env:BEE_CACHE_DIRECTORY = Join-Path $workspacePath 'qa-artifacts\bee-cache'
New-Item -ItemType Directory -Path $env:BEE_CACHE_DIRECTORY -Force | Out-Null

if (-not $ReuseBuild -or -not (Test-Path -LiteralPath $qaExecutable)) {
    & $UnityPath @(
        '-batchmode', '-nographics', '-noUpm', '-projectPath', $projectPath,
        '-executeMethod', 'JellyGate.Editor.JellyGateBuild.BuildWindowsQa',
        '-outputPath', $qaExecutable, '-logFile', $buildLog, '-quit'
    )
    $buildSucceeded = (Test-Path -LiteralPath $qaExecutable) -and
        $null -ne (Select-String -LiteralPath $buildLog -SimpleMatch 'Build Finished, Result: Success.' | Select-Object -Last 1) -and
        @(Select-String -LiteralPath $buildLog -Pattern 'error CS|Scripts have compiler errors|BuildFailedException').Count -eq 0
    if (-not $buildSucceeded) {
        throw "CROWNFRONT code 15 QA build failed. See $buildLog"
    }
}

$probes = @(
    @{ Name='roster-range-305'; Argument='-qaRosterRange305'; Pattern='QA_ROSTER_RANGE_305 passed=True'; Graphics=$false },
    @{ Name='interaction-visual-306'; Argument='-qaInteractionVisual306'; Pattern='QA_INTERACTION_VISUAL_306 passed=True'; Graphics=$true },
    @{ Name='balance-304'; Argument='-qaBalance304'; Pattern='QA_BALANCE_304 passed=True'; Graphics=$false },
    @{ Name='release-303'; Argument='-qaRelease303'; Pattern='QA_RELEASE_303 passed=True'; Graphics=$false }
)
$results = @()
foreach ($probe in $probes) {
    $runtimeLog = Join-Path $logDirectory "$($probe.Name).log"
    $arguments = @('-batchmode', $probe.Argument, '-logFile', $runtimeLog)
    if (-not $probe.Graphics) { $arguments = @('-batchmode', '-nographics', $probe.Argument, '-logFile', $runtimeLog) }
    $process = Start-Process -FilePath $qaExecutable -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
    $match = Get-Content -LiteralPath $runtimeLog | Select-String -SimpleMatch $probe.Pattern | Select-Object -Last 1
    $exceptions = @(Select-String -LiteralPath $runtimeLog -Pattern 'NullReferenceException|ArgumentException|IndexOutOfRangeException')
    $passed = $process.ExitCode -eq 0 -and $null -ne $match -and $exceptions.Count -eq 0
    $results += [ordered]@{ name=$probe.Name; passed=$passed; exitCode=$process.ExitCode;
        exceptions=$exceptions.Count; result=if ($match) { $match.Line.Trim() } else { '' }; runtimeLog=$runtimeLog }
    Write-Output "$($probe.Name): passed=$passed exit=$($process.ExitCode)"
}
$passed = -not ($results.passed -contains $false)
[ordered]@{ version='1.00'; versionCode=15; generatedAt=(Get-Date).ToString('o');
    passed=$passed; buildLog=$buildLog; probes=$results } |
    ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $summaryPath -Encoding utf8
if (-not $passed) { throw "CROWNFRONT code 15 QA failed. See $summaryPath" }
Write-Output "CROWNFRONT code 15 QA completed: $summaryPath"
