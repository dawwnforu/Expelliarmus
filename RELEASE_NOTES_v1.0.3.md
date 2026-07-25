# Expelliarmus v1.0.3

This release fixes multiplayer item ownership during a disarm.

## Changes

- The target teammate's inventory slot is removed by the master client.
- The stolen item is picked up through PEAK's native master-client pickup RPC.
- If the caster is already holding an item, that item is dropped to the ground first.
- Removed the direct local attach path that could leave both players holding the same network item.
- Item slots are matched by `ItemInstanceData` GUID when available.

## Expected result

After a successful right-click disarm, the target's hand is empty, the target item is equipped in the caster's hand, and the caster's previous held item is on the ground.
