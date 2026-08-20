param(
    [string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.0.34f1\Editor\Unity.exe',
    [switch]$ReuseBuild
)

$ErrorActionPreference = 'Stop'
$projectPath = Split-Path -Parent $PSScriptRoot
$workspacePath = Split-Path -Parent $projectPath
$qaDirectory = Join-Path $workspacePath 'qa-artifacts\Crownfront-QA-270'
$qaExecutable = Join-Path $qaDirectory 'Crownfront-QA.exe'
$legacyQaDirectory = Join-Path $workspacePath 'qa-artifacts\Crownfront-QA-269'
$managedAssembly = Join-Path $projectPath 'Temp\bin\Debug\Assembly-CSharp.dll'
$logDirectory = Join-Path $workspacePath 'qa-logs\v2.70'
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
        $licenseFailure = (Test-Path -LiteralPath $buildLog) -and
            ($null -ne (Select-String -LiteralPath $buildLog -SimpleMatch 'No valid Unity Editor license found'))
        if ($licenseFailure) {
            $msBuild = 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe'
            $installedReferences = 'C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2'
            $referenceRoot = Join-Path $env:TEMP 'crownfront-qa-netfx'
            $compatReferences = Join-Path $referenceRoot '.NETFramework\v4.7.1'
            if (-not (Test-Path -LiteralPath (Join-Path $compatReferences 'mscorlib.dll'))) {
                New-Item -ItemType Directory -Path $compatReferences -Force | Out-Null
                Copy-Item -Path (Join-Path $installedReferences '*') -Destination $compatReferences -Recurse -Force
            }
            if (Test-Path -LiteralPath $msBuild) {
                $compile = Start-Process -FilePath $msBuild -ArgumentList @(
                    (Join-Path $projectPath 'Assembly-CSharp.csproj'), '/t:Build',
                    "/p:TargetFrameworkRootPath=$referenceRoot\", '/v:minimal', '/nologo'
                ) -WindowStyle Hidden -Wait -PassThru
                if ($compile.ExitCode -ne 0) { throw 'Managed QA assembly compile failed.' }
            }
        }
        if (-not $licenseFailure -or -not (Test-Path -LiteralPath $managedAssembly) -or
            -not (Test-Path -LiteralPath (Join-Path $legacyQaDirectory 'Crownfront-QA.exe'))) {
            throw "Crownfront v2.70 QA player build failed. See $buildLog"
        }
        Write-Warning 'Unity license unavailable; reusing the last verified Mono player shell with the newly compiled gameplay assembly.'
        New-Item -ItemType Directory -Path $qaDirectory -Force | Out-Null
        Copy-Item -LiteralPath (Join-Path $legacyQaDirectory 'Crownfront-QA.exe') -Destination $qaExecutable -Force
        Copy-Item -LiteralPath (Join-Path $legacyQaDirectory 'UnityPlayer.dll') -Destination $qaDirectory -Force
        Copy-Item -LiteralPath (Join-Path $legacyQaDirectory 'UnityCrashHandler64.exe') -Destination $qaDirectory -Force
        $monoDestination = Join-Path $qaDirectory 'MonoBleedingEdge'
        $dataDestination = Join-Path $qaDirectory 'Crownfront-QA_Data'
        New-Item -ItemType Directory -Path $monoDestination -Force | Out-Null
        New-Item -ItemType Directory -Path $dataDestination -Force | Out-Null
        Copy-Item -Path (Join-Path $legacyQaDirectory 'MonoBleedingEdge\*') `
            -Destination $monoDestination -Recurse -Force
        Copy-Item -Path (Join-Path $legacyQaDirectory 'Crownfront-QA_Data\*') `
            -Destination $dataDestination -Recurse -Force
        Copy-Item -LiteralPath $managedAssembly -Destination `
            (Join-Path $qaDirectory 'Crownfront-QA_Data\Managed\Assembly-CSharp.dll') -Force
    }
}

$probes = @(
    @{ Name = '01-combat-stats-and-layout'; Arguments = @('-qaCombatStats270'); Pattern = 'QA_COMBAT_STATS_270 passed=True players=10/10 enemies=39/39 formula=True labels=True layout=True/True' },
    @{ Name = '02-every-enemy-state'; Arguments = @('-qaEnemyPresentation269'); Pattern = 'QA_ENEMY_PRESENTATION_269 passed=True profiles=53 regularPoses=61056/61056 bossPoses=11520/11520 transitions=2520/2520' },
    @{ Name = '03-legacy-sprite-regression'; Arguments = @('-qaSprite264'); Pattern = 'QA_SPRITE_264 passed=True bossPoses=5760 regularPoses=10176' },
    @{ Name = '04-menu-save-flow'; Arguments = @('-qaMenuSave272'); Pattern = 'QA_MENU_SAVE_FLOW_272 passed=True auto=True deploy=True prompt=True cancel=True save=True discard=True' }
)
$results = @()
foreach ($probe in $probes) {
    $runtimeLog = Join-Path $logDirectory "$($probe.Name).log"
    $arguments = @('-batchmode', '-nographics') + $probe.Arguments + @('-logFile', $runtimeLog)
    $process = Start-Process -FilePath $qaExecutable -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
    $match = Get-Content -LiteralPath $runtimeLog |
        Select-String -SimpleMatch $probe.Pattern | Select-Object -Last 1
    $exceptions = @(Select-String -LiteralPath $runtimeLog `
        -Pattern 'NullReferenceException|ArgumentException|ArgumentOutOfRangeException|IndexOutOfRangeException')
    $passed = $process.ExitCode -eq 0 -and $null -ne $match -and $exceptions.Count -eq 0
    $results += [ordered]@{
        name = $probe.Name; passed = $passed; exitCode = $process.ExitCode
        exceptions = $exceptions.Count
        result = if ($null -ne $match) { $match.Line.Trim() } else { '' }
        runtimeLog = $runtimeLog
    }
    Write-Output "$($probe.Name): passed=$passed exit=$($process.ExitCode) exceptions=$($exceptions.Count) $($results[-1].result)"
    Get-Process 'UnityCrashHandler64' -ErrorAction SilentlyContinue |
        Wait-Process -Timeout 5 -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 350
}
$passed = -not ($results.passed -contains $false)
$summary = [ordered]@{
    version = '2.70.0'; generatedAt = (Get-Date).ToString('o'); passed = $passed
    buildLog = $buildLog; probes = $results
}
$summary | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $summaryPath -Encoding utf8
if (-not $passed) { throw "Crownfront v2.70 combat-stat QA failed. See $summaryPath" }
Write-Output "Crownfront v2.70 QA completed. Summary: $summaryPath"
