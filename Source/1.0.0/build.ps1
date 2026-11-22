param(
    [string]$GamePath = "${env:ProgramFiles(x86)}\Steam\steamapps\common\Car Mechanic Simulator 2018"
)

$ErrorActionPreference = 'Stop'
$managed = Join-Path $GamePath 'cms2018_Data\Managed'
$core = Join-Path $GamePath 'BepInEx\core'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$source = Join-Path $PSScriptRoot 'CameraPresets.cs'
$output = Join-Path $PSScriptRoot 'build\CameraPresets.dll'
$references = @(
    (Join-Path $managed 'mscorlib.dll'),
    (Join-Path $managed 'System.dll'),
    (Join-Path $managed 'System.Core.dll'),
    (Join-Path $managed 'Assembly-CSharp-firstpass.dll'),
    (Join-Path $managed 'UnityEngine.dll'),
    (Join-Path $managed 'UnityEngine.CoreModule.dll'),
    (Join-Path $managed 'UnityEngine.JSONSerializeModule.dll'),
    (Join-Path $managed 'UnityEngine.InputModule.dll'),
    (Join-Path $core 'BepInEx.dll'),
    (Join-Path $core '0Harmony.dll')
)

foreach ($path in @($compiler, $source) + $references) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Missing build dependency at $path"
    }
}

New-Item -ItemType Directory -Path (Split-Path -Parent $output) -Force | Out-Null
$compilerArgs = @('/nologo', '/noconfig', '/nostdlib+', '/target:library', '/optimize+', "/out:$output")
$compilerArgs += $references | ForEach-Object { "/reference:$_" }
& $compiler @compilerArgs $source
if ($LASTEXITCODE -ne 0) {
    throw "C# compiler exited with code $LASTEXITCODE"
}
Write-Host "Built $output"
