# 1.0.6

- Move the temporary transfer object to the receiving hand and suspend its physics while waiting for owner release.
- Restore physics on cancellation, timeout or mod disable; keep original ownership checks.
- Record successful grabber-only guest playtesting of 1.0.5. New 1.0.6 visuals still need multiplayer testing.

# 1.0.5

- Match held items by selected slot and unique instance ID, including inventories containing identical item types.
- Drop through the host, wait for the owner's held object to be released, then request pickup of the room-owned ground item.
- Stop on missing acknowledgements without creating replacement copies or retrying pickup.
- Support aiming after reaching, one transfer per press, and menu/consciousness/climbing checks.
- First Thunderstore beta package; two-player gameplay testing is pending.
