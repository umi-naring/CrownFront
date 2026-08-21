param(
    [string]$ProjectPath,
    [string]$AndroidNdkPath,
    [string]$UnityPath
)

$ErrorActionPreference = 'Stop'
$workspacePath = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$builder = Join-Path $PSScriptRoot 'Build-CrownfrontPlayBundleFromSavedKey.ps1'
$stdout = Join-Path $workspacePath 'android-aab-build-100-code5-launch.log'
$stderr = Join-Path $workspacePath 'android-aab-build-100-code5-launch.err.log'
$arguments = @(
    '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', ('"{0}"' -f $builder),
    '-ProjectPath', ('"{0}"' -f $ProjectPath),
    '-AndroidNdkPath', ('"{0}"' -f $AndroidNdkPath),
    '-UnityPath', ('"{0}"' -f $UnityPath)
)
$process = Start-Process -FilePath 'C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe' `
    -ArgumentList $arguments -WindowStyle Hidden -RedirectStandardOutput $stdout `
    -RedirectStandardError $stderr -PassThru
Write-Output ("CROWNFRONT_ASYNC_BUILD_PID={0}" -f $process.Id)
