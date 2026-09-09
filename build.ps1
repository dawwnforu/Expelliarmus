param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [string]$PeakDir = $env:PEAK_DIR,

    [string]$CompilerPath = (
        Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
    ),

    [string]$OutputDir = $PSScriptRoot,

    [switch]$Install
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($PeakDir)) {
    throw @"
PEAK game directory is required. Set it with either:
  `$env:PEAK_DIR = 'D:\SteamLibrary\steamapps\common\PEAK'
  .\build.ps1 -PeakDir 'D:\SteamLibrary\steamapps\common\PEAK'
"@
}

$PeakDir = [System.IO.Path]::GetFullPath($PeakDir)
$OutputDir = [System.IO.Path]::GetFullPath($OutputDir)
$sourceDir = $PSScriptRoot
$managedDir = Join-Path $PeakDir "PEAK_Data\Managed"
$bepInExCore = Join-Path $PeakDir "BepInEx\core"
$pluginsDir = Join-Path $PeakDir "BepInEx\plugins\Expelliarmus"

if (-not (Test-Path -LiteralPath $CompilerPath -PathType Leaf)) {
    throw "C# compiler not found: $CompilerPath"
}

$references = @(
    "Assembly-CSharp.dll",
    "Zorro.Core.Runtime.dll",
    "PhotonUnityNetworking.dll",
    "PhotonRealtime.dll",
    "Photon3Unity3D.dll",
    "UnityEngine.dll",
    "UnityEngine.CoreModule.dll",
    "UnityEngine.UI.dll",
    "UnityEngine.UIModule.dll",
    "UnityEngine.IMGUIModule.dll",
    "UnityEngine.InputLegacyModule.dll",
    "UnityEngine.PhysicsModule.dll",
    "UnityEngine.TextRenderingModule.dll",
    "UnityEngine.TextCoreTextEngineModule.dll",
    "UnityEngine.ImageConversionModule.dll",
    "netstandard.dll"
) | ForEach-Object { Join-Path $managedDir $_ }

$references += @(
    "0Harmony.dll",
    "BepInEx.dll",
    "BepInEx.Harmony.dll"
) | ForEach-Object { Join-Path $bepInExCore $_ }

$sources = @(
    (Join-Path $sourceDir "Plugin.cs"),
    (Join-Path $sourceDir "ExpelliarmusBehaviour.cs")
)

$missing = @($references + $sources | Where-Object {
    -not (Test-Path -LiteralPath $_ -PathType Leaf)
})
if ($missing.Count -gt 0) {
    throw "Build dependencies are missing:`n  $($missing -join "`n  ")"
}

New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
$outputDll = Join-Path $OutputDir "Expelliarmus.dll"
$outputPdb = Join-Path $OutputDir "Expelliarmus.pdb"

$compilerArgs = @(
    "/target:library",
    "/out:$outputDll",
    "/platform:x64",
    "/warn:3",
    "/debug+"
)
if ($Configuration -eq "Release") {
    $compilerArgs += "/optimize+"
} else {
    $compilerArgs += @("/debug+", "/optimize-")
}
$compilerArgs += $references | ForEach-Object { "/reference:$_" }
$compilerArgs += $sources

Write-Host "Building Expelliarmus ($Configuration)"
Write-Host "PEAK:   $PeakDir"
Write-Host "Source: $sourceDir"
Write-Host "Output: $outputDll"

& $CompilerPath @compilerArgs
if ($LASTEXITCODE -ne 0) {
    throw "Compilation failed with exit code $LASTEXITCODE"
}
if (-not (Test-Path -LiteralPath $outputDll -PathType Leaf)) {
    throw "Compiler did not create the expected output: $outputDll"
}

Write-Host "Build successful: $outputDll"

if ($Install) {
    New-Item -ItemType Directory -Path $pluginsDir -Force | Out-Null
    Copy-Item -LiteralPath $outputDll -Destination (
        Join-Path $pluginsDir "Expelliarmus.dll"
    ) -Force
    if (Test-Path -LiteralPath $outputPdb -PathType Leaf) {
        Copy-Item -LiteralPath $outputPdb -Destination (
            Join-Path $pluginsDir "Expelliarmus.pdb"
        ) -Force
    }
    Write-Host "Installed to: $pluginsDir"
} else {
    Write-Host "The game directory was not modified. Re-run with -Install to install."
}
