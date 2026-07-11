# Expelliarmus v1.0.0

Initial release of **Expelliarmus / 除你武器** for PEAK.

## What it does

- Aim at a teammate's held item.
- Right-click with the grab hand.
- The item is pulled out of their hand and moved into yours.

## Included in the package

- `Expelliarmus.dll`
- BepInEx 5 runtime files already used by the local PEAK install
- Chinese install guide
- README

## Known limitations

- PEAK's gameplay methods are private, so this release uses reflection and several fallback paths.
- If a future PEAK update changes item ownership internals, the mod may need a small method-name update.
- Check `PEAK\BepInEx\LogOutput.log` for `Expelliarmus` entries when reporting issues.
