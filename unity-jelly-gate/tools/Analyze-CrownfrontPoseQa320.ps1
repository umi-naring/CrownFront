param(
    [string]$CsvPath = '..\qa-artifacts\Crownfront-QA-320\qa-artifacts\Crownfront-QA-320\all-unit-pose-audit.csv'
)

$ErrorActionPreference = 'Stop'
$rows = Import-Csv -LiteralPath $CsvPath | Where-Object { $_.category -eq 'player' }
$targets = @(
    @{ Id='Tank'; Variant='0'; Hero='1' },
    @{ Id='Archer'; Variant='0'; Hero='0' },
    @{ Id='SingleMage'; Variant='0'; Hero='1' },
    @{ Id='Lancer'; Variant='0'; Hero='1' },
    @{ Id='Oracle'; Variant='0'; Hero='0' },
    @{ Id='Oracle'; Variant='0'; Hero='1' }
)

foreach ($target in $targets) {
    Write-Output "PRESENTATION $($target.Id) v$($target.Variant) hero=$($target.Hero)"
    $presentation = $rows | Where-Object {
        $_.id -eq $target.Id -and $_.variant -eq $target.Variant -and $_.hero -eq $target.Hero
    }
    foreach ($direction in ($presentation.direction | Sort-Object -Unique)) {
        $poses = @($presentation | Where-Object { $_.direction -eq $direction })
        $areas = @($poses | ForEach-Object { [double]$_.opaqueArea } | Sort-Object)
        $median = $areas[[int][math]::Floor($areas.Count / 2)]
        $minimum = $areas[0]
        $minPose = $poses | Sort-Object { [double]$_.opaqueArea } | Select-Object -First 1
        $maxPose = $poses | Sort-Object { [double]$_.opaqueArea } -Descending | Select-Object -First 1
        $xs = @($poses | ForEach-Object { [double]$_.centreX })
        $ys = @($poses | ForEach-Object { [double]$_.centreY })
        $spreadX = ($xs | Measure-Object -Maximum).Maximum - ($xs | Measure-Object -Minimum).Minimum
        $spreadY = ($ys | Measure-Object -Maximum).Maximum - ($ys | Measure-Object -Minimum).Minimum
        Write-Output ("  {0,-10} ratio={1:N3} spread=({2:N3},{3:N3}) min=s{4}f{5} max=s{6}f{7}" -f
            $direction, ($minimum / [math]::Max(.0001, $median)), $spreadX, $spreadY,
            $minPose.state, $minPose.phase, $maxPose.state, $maxPose.phase)
    }
}
