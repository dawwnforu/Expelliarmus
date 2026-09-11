# 1.0.9

- New PEAK-inspired DAwwN watermark covers and a bilingual gameplay comic. No gameplay changes.
- 更新 DAwwN 风格水印封面，加入中英双语玩法漫画；玩法不变。

# 1.0.8

- Short bilingual listing description focused on playful co-op fun. Controls and testing details remain in the full description. No gameplay changes.
- 首页简介改为简短中英双语，操作与测试说明保留在详情页；玩法不变。

# 1.0.7

- Documentation update: bilingual beginner instructions for Windows manager/manual installation, updates, duplicate DLLs and troubleshooting.
- Explain experimental Mac compatibility-layer installation and the absence of verified native macOS support.
- Use generic example paths; gameplay code is unchanged.

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
