# Expelliarmus / 除你武器

Your teammate has a banana. You have a reaching hand.

**Hold right-click and aim at a teammate's held item to take it.** You can start reaching before aiming. Release the button before grabbing again.

## How to play

- Uses PEAK's secondary-use/reach binding (right mouse button by default), including custom bindings.
- Targets droppable items held from slots 1, 2 or 3, up to 7.5 metres away.
- If you are holding something, your previous item is dropped on the ground first.
- One grab per button hold. Menus, the action wheel, unconsciousness and climbing block activation.
- Only the grabber is intended to need this mod. It uses existing game network messages; no custom mod is required by those messages on teammates' machines.

## Beta status and limitations

Version 1.0.6 passes 70 regression assertions with simulated networking and builds against the installed PEAK assemblies. The author tested 1.0.5 as a guest in a friend's lobby with only the grabber installing the mod: grabbing worked. **The new handoff visuals in 1.0.6 and the host-player scenario still need in-game testing.**

The transfer now starts near the receiving hand and pauses the temporary item's physics while waiting for the original holder to release it. This aims to remove the visible fall-to-floor step. A native world object is still used for compatibility with unmodded teammates; latency may cause a brief pop or movement. If the transfer fails, normal physics resumes and the item remains available; another player may pick it up first.

If a teammate replaces the same inventory slot while the request is travelling, the replacement item may be dropped. The mod will not automatically pick up an item with a different unique ID. Switching equipment during transfer may also be interrupted. Preventing these races completely would require cooperating logic on the host and item owner.

## Installation

Use a Thunderstore-compatible mod manager; the BepInEx dependency will be installed with the mod.

For manual installation, install BepInEx for PEAK and copy `plugins/Expelliarmus/Expelliarmus.dll` into `PEAK/BepInEx/plugins/Expelliarmus/`. Restart the game after updating.

## 中文说明

按住右键伸手，再瞄准队友手里的物品即可抢夺；松开后才能再次抢。支持第 1/2/3 格拿出的可丢弃物品，最大距离 7.5 米。自己手里有东西时，原物品先落地。

1.0.5 已实测：加入朋友房间、仅自己安装时可以抢夺。1.0.6 将交接位置移到自己手边，并在同步等待期间暂停物品下落；失败后恢复物理运动并保留物品。新版视觉效果及房主场景仍待实测，高延迟下仍可能短暂跳动。队友在网络传输期间替换同一格时，新物品可能掉地，但不会自动拾取编号不符的物品。转移时切换装备也可能被打断。

## Source and feedback

[GitHub source and issues](https://github.com/dawwnforu/Expelliarmus)

Icon created with AI assistance. Unofficial PEAK mod.
