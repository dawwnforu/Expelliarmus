param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$peakDir = "D:\steam\steamapps\common\PEAK"
$managedDir = "$peakDir\PEAK_Data\Managed"
$bepInExCore = "$peakDir\BepInEx\core"
$pluginsDir = "$peakDir\BepInEx\plugins\Expelliarmus"
$srcDir = "D:\trae projects\1\Expelliarmus"

$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) {
    Write-Error "C# compiler not found at $csc"
    exit 1
}

$outputDll = "$srcDir\Expelliarmus.dll"
$outputPdb = "$srcDir\Expelliarmus.pdb"

$references = @(
    "$managedDir\UnityEngine.dll",
    "$managedDir\UnityEngine.CoreModule.dll",
    "$managedDir\UnityEngine.UI.dll",
    "$managedDir\UnityEngine.UIModule.dll",
    "$managedDir\UnityEngine.IMGUIModule.dll",
    "$managedDir\UnityEngine.InputLegacyModule.dll",
    "$managedDir\UnityEngine.PhysicsModule.dll",
    "$managedDir\UnityEngine.TextRenderingModule.dll",
    "$managedDir\UnityEngine.TextCoreTextEngineModule.dll",
    "$managedDir\UnityEngine.ImageConversionModule.dll",
    "$managedDir\netstandard.dll",
    "$bepInExCore\0Harmony.dll",
    "$bepInExCore\BepInEx.dll",
    "$bepInExCore\BepInEx.Harmony.dll"
)

$refPaths = ""
foreach ($ref in $references) {
    if (Test-Path $ref) {
        $refPaths += "`"/reference:$ref`" "
    } else {
        Write-Warning "Reference not found: $ref"
    }
}

$sources = @(
    "$srcDir\Plugin.cs",
    "$srcDir\ExpelliarmusBehaviour.cs"
)

$srcPaths = ""
foreach ($src in $sources) {
    if (Test-Path $src) {
        $srcPaths += "`"$src`" "
    } else {
        Write-Error "Source not found: $src"
        exit 1
    }
}

$args = @(
    "/target:library",
    "/out:`"$outputDll`"",
    "/debug",
    "/optimize",
    "/platform:x64",
    "/warn:3"
)

$compilerArgs = "$($args -join ' ') $refPaths $srcPaths"

Write-Host "========================================="
Write-Host "Building Expelliarmus Mod"
Write-Host "========================================="
Write-Host "Compiler: $csc"
Write-Host "Output:   $outputDll"
Write-Host ""

$cmd = "& `"$csc`" $compilerArgs"
Write-Host "Command:"
Write-Host $cmd
Write-Host ""

try {
    Invoke-Expression $cmd
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Compilation failed with exit code $LASTEXITCODE"
        exit $LASTEXITCODE
    }
} catch {
    Write-Error "Compilation error: $_"
    exit 1
}

if (Test-Path $outputDll) {
    $dllSize = (Get-Item $outputDll).Length
    Write-Host ""
    Write-Host "========================================="
    Write-Host "BUILD SUCCESSFUL!"
    Write-Host "Output: $outputDll"
    Write-Host "Size:   $dllSize bytes"
    Write-Host "========================================="

    Write-Host ""
    Write-Host "Copying to plugins folder..."

    if (-not (Test-Path $pluginsDir)) {
        New-Item -ItemType Directory -Path $pluginsDir -Force | Out-Null
    }

    Copy-Item -Path $outputDll -Destination "$pluginsDir\Expelliarmus.dll" -Force
    if (Test-Path $outputPdb) {
        Copy-Item -Path $outputPdb -Destination "$pluginsDir\Expelliarmus.pdb" -Force
    }

    Write-Host "Installed to: $pluginsDir\Expelliarmus.dll"
    Write-Host ""
    Write-Host "Ready! Launch PEAK to test the mod."
    Write-Host "  - Aim at a teammate's held item"
    Write-Host "  - Right click to steal it into your hand"
} else {
    Write-Error "Output DLL not created!"
    exit 1
}
