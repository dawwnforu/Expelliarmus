# Expelliarmus v1.0.4

## Compatibility

- Verified against PEAK `1.65.a` and Unity `6000.3.15`.
- Verified all required inventory methods and Photon RPC attributes still exist.
- Verified BepInEx `5.4.23.5` loads the plugin on the current game build.

## Multiplayer reliability

- Replaced the fixed 120 ms delay with host-synchronized inventory confirmation.
- Waits up to 3 seconds for the caster's dropped slot and the target's stolen slot to become empty.
- Sends the native pickup request only after both authoritative inventory changes are visible.
- Logs room state, host/client role, ping, RPC stages, synchronization time, and timeout details.

## Installation note

Steam game updates can remove BepInEx and all installed plugins. If `BepInEx/LogOutput.log` is missing, reinstall the loader and Mod before troubleshooting gameplay logic.
