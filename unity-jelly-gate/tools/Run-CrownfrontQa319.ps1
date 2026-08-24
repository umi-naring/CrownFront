param(
    [string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.0.82f1\Editor\Unity.exe',
    [switch]$ReuseBuild
)

$ErrorActionPreference = 'Stop'
$projectPath = Split-Path -Parent $PSScriptRoot
$workspacePath = Split-Path -Parent $projectPath
$qaDirectory = Join-Path $workspacePath 'qa-artifacts\Crownfront-QA-319'
$qaExecutable = Join-Path $qaDirectory 'Crownfront-QA.exe'
$logDirectory = Join-Path $workspacePath 'qa-logs\v1.00-code19'
$buildLog = Join-Path $logDirectory 'windows-player-build.log'
$summaryPath = Join-Path $logDirectory 'qa-summary.json'
New-Item -ItemType Directory -Path $qaDirectory, $logDirectory -Force | Out-Null
$env:USERPROFILE = 'C:\Users\Administrator'
$env:LOCALAPPDATA = 'C:\Users\Administrator\AppData\Local'
$env:APPDATA = 'C:\Users\Administrator\AppData\Roaming'
$env:TEMP = 'C:\Users\Administrator\AppData\Local\Temp'
$env:TMP = $env:TEMP
$env:BEE_CACHE_DIRECTORY = Join-Path $workspacePath 'qa-artifacts\bee-cache'
New-Item -ItemType Directory -Path $env:BEE_CACHE_DIRECTORY -Force | Out-Null

if (-not $ReuseBuild -or -not (Test-Path -LiteralPath $qaExecutable)) {
    $buildArguments = @(
        '-batchmode', '-nographics', '-noUpm', '-projectPath', $projectPath,
        '-executeMethod', 'JellyGate.Editor.JellyGateBuild.BuildWindowsQa',
        '-outputPath', $qaExecutable, '-logFile', $buildLog, '-quit'
    )
    $buildProcess = Start-Process -FilePath $UnityPath -ArgumentList $buildArguments `
        -WindowStyle Hidden -Wait -PassThru
    $buildSucceeded = $buildProcess.ExitCode -eq 0 -and
        (Test-Path -LiteralPath $buildLog) -and (Test-Path -LiteralPath $qaExecutable) -and
        $null -ne (Select-String -LiteralPath $buildLog -SimpleMatch 'Build Finished, Result: Success.' | Select-Object -Last 1) -and
        @(Select-String -LiteralPath $buildLog -Pattern 'error CS|Scripts have compiler errors|BuildFailedException').Count -eq 0
    if (-not $buildSucceeded) { throw "CROWNFRONT code 19 QA build failed. See $buildLog" }
}

$probes = @(
    @{ Name='release-319'; Argument='-qaRelease319'; Pattern='QA_RELEASE_319 passed=True'; Graphics=$true },
    @{ Name='battlefield-sprite-307'; Argument='-qaBattlefieldSprite307'; Pattern='QA_BATTLEFIELD_SPRITE_307 passed=True'; Graphics=$false },
    @{ Name='ui-review-309'; Argument='-qaUiReview309'; Pattern='QA_UI_REVIEW_309 passed=True'; Graphics=$true },
    @{ Name='spawn-pool-310'; Argument='-qaSpawnPool310'; Pattern='QA_SPAWN_POOL_310 passed=True'; Graphics=$false }
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
[ordered]@{ version='1.00'; versionCode=19; generatedAt=(Get-Date).ToString('o');
    passed=$passed; buildLog=$buildLog; probes=$results } |
    ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $summaryPath -Encoding utf8
if (-not $passed) { throw "CROWNFRONT code 19 QA failed. See $summaryPath" }
Write-Output "CROWNFRONT code 19 QA completed: $summaryPath"
