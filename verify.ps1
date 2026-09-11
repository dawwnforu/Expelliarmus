param(
    [string]$PeakDir = "D:\steam\steamapps\common\PEAK",
    [switch]$RequireRuntimeLoad
)

$ErrorActionPreference = "Stop"
$sourceDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$managedDir = Join-Path $PeakDir "PEAK_Data\Managed"
$pluginPath = Join-Path $PeakDir "BepInEx\plugins\Expelliarmus\Expelliarmus.dll"
$sourcePluginPath = Join-Path $sourceDir "Expelliarmus.dll"
$cecilPath = Join-Path $PeakDir "BepInEx\core\Mono.Cecil.dll"
$results = New-Object System.Collections.Generic.List[object]

function Add-Result([string]$Name, [bool]$Passed, [string]$Detail) {
    $results.Add([PSCustomObject]@{
        Check = $Name
        Passed = $Passed
        Detail = $Detail
    })
}

function Get-Type([Mono.Cecil.AssemblyDefinition]$Assembly, [string]$FullName) {
    return $Assembly.MainModule.Types | Where-Object FullName -eq $FullName | Select-Object -First 1
}

Add-Result "PEAK executable" (Test-Path (Join-Path $PeakDir "PEAK.exe")) $PeakDir
Add-Result "Doorstop proxy" (Test-Path (Join-Path $PeakDir "winhttp.dll")) "winhttp.dll"
Add-Result "BepInEx core" (Test-Path (Join-Path $PeakDir "BepInEx\core\BepInEx.dll")) "BepInEx.dll"
Add-Result "Installed plugin" (Test-Path $pluginPath) $pluginPath

if (-not (Test-Path $cecilPath)) {
    $fallbackCecil = Join-Path $sourceDir "release_pkg\Expelliarmus\BepInEx\core\Mono.Cecil.dll"
    if (Test-Path $fallbackCecil) {
        $cecilPath = $fallbackCecil
    }
}

Add-Type -Path $cecilPath
$gameAssemblyPath = Join-Path $managedDir "Assembly-CSharp.dll"
$gameAssembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($gameAssemblyPath)

$contracts = @(
    @{ Type = "Character"; Method = $null; Args = -1; Rpc = $false },
    @{ Type = "CharacterItems"; Method = "DropItemFromSlotRPC"; Args = 2; Rpc = $true },
    @{ Type = "CharacterItems"; Method = "EquipSlotRpc"; Args = 2; Rpc = $true },
    @{ Type = "Player"; Method = "RPCRemoveItemFromSlot"; Args = 1; Rpc = $true },
    @{ Type = "Player"; Method = "GetItemSlot"; Args = 1; Rpc = $false },
    @{ Type = "Item"; Method = "RequestPickup"; Args = 1; Rpc = $true }
)

foreach ($contract in $contracts) {
    $type = Get-Type $gameAssembly $contract.Type
    if ($null -eq $type) {
        Add-Result "Type $($contract.Type)" $false "Missing"
        continue
    }

    if ($null -eq $contract.Method) {
        Add-Result "Type $($contract.Type)" $true "Present"
        continue
    }

    $method = $type.Methods | Where-Object {
        $_.Name -eq $contract.Method -and $_.Parameters.Count -eq $contract.Args
    } | Select-Object -First 1
    $methodPresent = $null -ne $method
    Add-Result "$($contract.Type).$($contract.Method)" $methodPresent $(if ($methodPresent) { $method.FullName } else { "Missing" })

    if ($methodPresent -and $contract.Rpc) {
        $hasPunRpc = $null -ne ($method.CustomAttributes | Where-Object {
            $_.AttributeType.FullName -eq "Photon.Pun.PunRPC"
        } | Select-Object -First 1)
        Add-Result "$($contract.Method) PunRPC" $hasPunRpc $(if ($hasPunRpc) { "Present" } else { "Missing" })
    }
}

if ((Test-Path $pluginPath) -and (Test-Path $sourcePluginPath)) {
    $installedHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $pluginPath).Hash
    $sourceHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $sourcePluginPath).Hash
    Add-Result "Installed DLL hash" ($installedHash -eq $sourceHash) $installedHash

    $pluginAssembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($pluginPath)
    $pluginInfo = Get-Type $pluginAssembly "Expelliarmus.PluginInfo"
    $versionField = $pluginInfo.Fields | Where-Object Name -eq "Version" | Select-Object -First 1
    Add-Result "Installed plugin version" ($versionField.Constant -eq "1.0.7") ([string]$versionField.Constant)
}

$logPath = Join-Path $PeakDir "BepInEx\LogOutput.log"
if ($RequireRuntimeLoad -and (Test-Path $logPath)) {
    $loaded = Select-String -LiteralPath $logPath -Pattern "Loading \[Expelliarmus 1.0.7\]" -Quiet
    Add-Result "Runtime load log" $loaded $logPath
} elseif ($RequireRuntimeLoad) {
    Add-Result "Runtime load log" $false "LogOutput.log missing"
}

$results | Format-Table -AutoSize
$failed = @($results | Where-Object { -not $_.Passed })
if ($failed.Count -gt 0) {
    Write-Error "$($failed.Count) verification check(s) failed."
    exit 1
}

Write-Host "All Expelliarmus compatibility checks passed."
