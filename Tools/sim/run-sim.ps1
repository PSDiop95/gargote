# Compile et lance la simulation hors Unity avec le compilateur Roslyn et le runtime .NET embarqués dans l'éditeur.
# Usage : powershell -File Tools/sim/run-sim.ps1 [-UnityVersion 6000.3.27f1]
param([string]$UnityVersion = '6000.3.27f1')

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$data = "C:\Program Files\Unity\Hub\Editor\$UnityVersion\Editor\Data"
$dotnet = Join-Path $data 'NetCoreRuntime\dotnet.exe'
$csc = Join-Path $data 'DotNetSdkRoslyn\csc.dll'
$fx = Get-ChildItem (Join-Path $data 'NetCoreRuntime\shared\Microsoft.NETCore.App') | Select-Object -First 1
$bin = Join-Path $PSScriptRoot 'bin'
New-Item -ItemType Directory -Force $bin | Out-Null

$refs = Get-ChildItem $fx.FullName -Filter *.dll |
    Where-Object { $_.Name -match '^(System|Microsoft\.(CSharp|VisualBasic|Win32))|^netstandard|^mscorlib' -and $_.Name -notmatch 'Native' } |
    ForEach-Object { "-r:$($_.FullName)" }
$sources = @(Join-Path $PSScriptRoot 'Program.cs') +
    (Get-ChildItem (Join-Path $root 'Assets\_Game\Scripts') -Recurse -Filter *.cs |
        Where-Object { $_.FullName -match '\\Scripts\\(Core|Systems|Simulation)\\' } | ForEach-Object { $_.FullName })

$out = Join-Path $bin 'GargoteSim.dll'
& $dotnet $csc -nologo -nostdlib -noconfig -langversion:9.0 -optimize+ -target:exe "-out:$out" @refs @sources
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

@"
{ "runtimeOptions": { "tfm": "net6.0", "framework": { "name": "Microsoft.NETCore.App", "version": "$($fx.Name)" } } }
"@ | Set-Content -Encoding ascii (Join-Path $bin 'GargoteSim.runtimeconfig.json')

& $dotnet $out $root
exit $LASTEXITCODE
