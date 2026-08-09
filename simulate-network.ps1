param()

$ErrorActionPreference = "Stop"
$sourceDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourcePath = Join-Path $sourceDir "tools\NetworkSimulation.cs"
$outputPath = Join-Path $sourceDir "tools\NetworkSimulation.exe"
$compiler = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if (-not (Test-Path $compiler)) {
    throw "C# compiler not found: $compiler"
}

& $compiler /nologo /optimize /out:$outputPath $sourcePath
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

& $outputPath
exit $LASTEXITCODE
