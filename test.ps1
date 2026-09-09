param([string]$PeakDir = 'D:\steam\steamapps\common\PEAK')
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$output = Join-Path $PSScriptRoot 'artifacts\Regression.exe'
New-Item -ItemType Directory -Path (Split-Path $output) -Force | Out-Null
& $compiler /nologo "/out:$output" (Join-Path $PSScriptRoot 'ExpelliarmusBehaviour.cs') (Join-Path $PSScriptRoot 'tests\Regression.cs')
if ($LASTEXITCODE -ne 0) { throw 'Regression compilation failed.' }
& $output
if ($LASTEXITCODE -ne 0) { throw 'Regression checks failed.' }

Add-Type -Path (Join-Path $PeakDir 'BepInEx\core\Mono.Cecil.dll')
$game = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $PeakDir 'PEAK_Data\Managed\Assembly-CSharp.dll'))
try {
    foreach ($entry in @(
        @('CharacterItems','DropItemFromSlotRPC','System.Byte,UnityEngine.Vector3'),
        @('CharacterItems','EquipSlotRpc','System.Int32,System.Int32'),
        @('Item','RequestPickup','Photon.Pun.PhotonView')
    )) {
        $type = $game.MainModule.Types | Where-Object Name -eq $entry[0]
        $method = $type.Methods | Where-Object Name -eq $entry[1]
        if (($method.Parameters.ParameterType.FullName -join ',') -ne $entry[2] -or
            'Photon.Pun.PunRPC' -notin $method.CustomAttributes.AttributeType.FullName) {
            throw "Changed native RPC: $($entry[0]).$($entry[1])"
        }
    }
    Write-Host 'PASS: all three RPC signatures and PunRPC attributes match installed PEAK.'
} finally { $game.Dispose() }
