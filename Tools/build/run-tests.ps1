# Lance les tests EditMode puis la simulation économique (Unity CLI requis).
# Usage : powershell -File Tools/build/run-tests.ps1 [-SkipSimulation]
param([switch]$SkipSimulation)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$results = Join-Path $root 'Tools\build\results'
New-Item -ItemType Directory -Force $results | Out-Null

$filter = @()
if ($SkipSimulation) { $filter = @('--', '-testCategory', '!Simulation') }

unity test $root --mode EditMode --report-format nunit,junit --output (Join-Path $results 'editmode.xml') --timeout 1800 --non-interactive --no-banner @filter
$testsExit = $LASTEXITCODE

if (-not $SkipSimulation) {
    unity run $root --non-interactive --no-banner -- -executeMethod Gargote.EditorTools.SimulationRunner.RunBatch -logFile (Join-Path $results 'simulation.log')
    $simExit = $LASTEXITCODE
} else { $simExit = 0 }

Write-Host "EditMode tests exit=$testsExit, simulation exit=$simExit (CSV : Tools/simulation/output)"
if ($testsExit -ne 0 -or $simExit -ne 0) { exit 1 }
