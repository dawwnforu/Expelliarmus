# Expelliarmus / 除你武器

PEAK BepInEx mod: aim at a teammate's held item and right-click to steal it into your hand.

## Features

- Right-click based disarm, matching PEAK's right-hand grab interaction.
- Crosshair raycast targeting: only triggers when aiming at a teammate's held item.
- Designed for items currently held from hotbar slots 1/2/3.
- Attempts game-native pickup/inventory/RPC paths first.
- Falls back to direct detach + move-to-hand if the current PEAK build uses private method names.

## Controls

| Input | Action |
| --- | --- |
| Right mouse button | Steal the targeted teammate-held item |

## Install

1. Install BepInEx 5 for PEAK.
2. Copy `Expelliarmus.dll` into:

```text
PEAK\BepInEx\plugins\Expelliarmus\Expelliarmus.dll
```

3. Start PEAK.

## Build

```powershell
powershell -ExecutionPolicy Bypass -File "D:\trae projects\1\Expelliarmus\build.ps1"
```

The build script compiles the DLL and copies it into the PEAK BepInEx plugins folder.

## Notes

This mod uses reflection because PEAK's gameplay classes are private inside `Assembly-CSharp.dll`.
If PEAK updates and changes method names, check `BepInEx\LogOutput.log`; the mod logs the method/RPC paths it successfully found.
