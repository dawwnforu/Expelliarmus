using System;
using System.Collections;
using BepInEx.Logging;
using Photon.Pun;
using UnityEngine;
using Zorro.Core;

namespace Expelliarmus
{
    public class ExpelliarmusBehaviour : MonoBehaviour
    {
        private const float MaxRange = 7.5f;
        private const float AimAngle = 25f;
        private const float SyncTimeout = 4f;
        private static ExpelliarmusBehaviour instance;
        private ManualLogSource logger;
        private bool busy;
        private bool usedThisPress;
        private float nextAttempt;
        private Item droppedItem;

        public static void Initialize(ManualLogSource log)
        {
            if (instance != null) return;
            var go = new GameObject("ExpelliarmusManager");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<ExpelliarmusBehaviour>();
            instance.logger = log;
        }

        public static void Shutdown()
        {
            if (instance != null) Destroy(instance.gameObject);
            instance = null;
        }

        private void Update()
        {
            var local = Character.localCharacter;
            if (local == null || local.input == null) return;
            if (!local.input.useSecondaryIsPressed)
            {
                usedThisPress = false;
                return;
            }
            if (busy || usedThisPress || Time.unscaledTime < nextAttempt || !CanAct(local)) return;
            nextAttempt = Time.unscaledTime + 0.1f;

            var target = FindTarget(local);
            if (target == null) return;
            var item = target.data.currentItem;
            var slot = HeldSlot(target, item);
            if (slot == null || slot.itemSlotID > 2) return;
            usedThisPress = true;
            busy = true;
            StartCoroutine(Transfer(local, target, item));
        }

        private static bool CanAct(Character character)
        {
            return PhotonNetwork.InRoom && character != null && character.IsLocal &&
                character.data != null && character.data.fullyConscious &&
                !character.data.isClimbingAnything && character.refs != null &&
                character.refs.items != null && character.player != null &&
                GUIManager.instance != null && !GUIManager.instance.windowBlockingInput &&
                !GUIManager.instance.wheelActive;
        }

        private static Character FindTarget(Character local)
        {
            var camera = Camera.main;
            if (camera == null) return null;
            var hits = Physics.RaycastAll(new Ray(camera.transform.position, camera.transform.forward),
                MaxRange, ~0, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, delegate(RaycastHit a, RaycastHit b) { return a.distance.CompareTo(b.distance); });
            foreach (var hit in hits)
            {
                var hitItem = hit.collider.GetComponentInParent<Item>();
                var target = hit.collider.GetComponentInParent<Character>();
                if (hitItem != null && hitItem.trueHolderCharacter != null)
                    target = hitItem.trueHolderCharacter;
                if (target == local) continue;
                // The nearest non-self collider still blocks the ray, including walls.
                if (target == null || target.data == null) return null;
                var item = target.data.currentItem;
                if (item == null || item.itemState != ItemState.Held) return null;
                var toItem = item.transform.position - camera.transform.position;
                if (toItem.magnitude > MaxRange) return null;
                return hitItem == item || Vector3.Angle(camera.transform.forward, toItem) <= AimAngle
                    ? target : null;
            }
            return null;
        }

        private static ItemSlot HeldSlot(Character character, Item item)
        {
            if (character == null || character.refs == null || character.refs.items == null ||
                character.player == null || item == null || item.data == null ||
                item.data.guid == Guid.Empty || character.data.currentItem != item ||
                item.trueHolderCharacter != character || !item.UIData.canDrop) return null;
            var selected = character.refs.items.currentSelectedSlot;
            if (selected.IsNone) return null;
            var slot = character.player.GetItemSlot(selected.Value);
            return slot != null && !slot.IsEmpty() && slot.data != null &&
                slot.prefab.itemID == item.itemID && slot.data.guid == item.data.guid ? slot : null;
        }

        private IEnumerator Transfer(Character local, Character target, Item original)
        {
            try
            {
                var guid = original.data.guid;
                var localItem = local.data.currentItem;
                if (localItem != null)
                {
                    yield return DropHeldItem(local, localItem);
                    if (droppedItem == null) yield break;
                }
                // Recheck after waiting: the target may have switched or consumed the item.
                if (!CanAct(local) || local.data.currentItem != null || original == null ||
                    original.data.guid != guid || HeldSlot(target, original) == null ||
                    !local.player.HasEmptySlot(original.itemID)) yield break;

                yield return DropHeldItem(target, original);
                var ground = droppedItem;
                if (ground == null || !CanAct(local) || local.data.currentItem != null ||
                    !local.player.HasEmptySlot(ground.itemID) || !IsGroundItem(ground, guid)) yield break;

                // This is the new room-owned object, never the teammate-owned held view.
                ground.photonView.RPC("RequestPickup", RpcTarget.MasterClient, local.photonView);
                var deadline = Time.unscaledTime + SyncTimeout;
                while (Time.unscaledTime < deadline && local != null && local.player != null)
                {
                    if (InventoryContains(local.player, guid))
                    {
                        logger.LogInfo("Expelliarmus: inventory pickup confirmed.");
                        yield break;
                    }
                    yield return new WaitForSecondsRealtime(0.05f);
                }
                logger.LogWarning("Expelliarmus: pickup not confirmed; no retry or replacement item created.");
            }
            finally
            {
                busy = false;
                nextAttempt = Time.unscaledTime + 0.4f;
            }
        }

        private IEnumerator DropHeldItem(Character character, Item original)
        {
            droppedItem = null;
            var slot = HeldSlot(character, original);
            if (slot == null) yield break;
            var guid = original.data.guid;
            var originalViewID = original.photonView.ViewID;
            if (originalViewID <= 0) yield break;
            var characterItems = character.refs.items;
            // The host drops its authoritative slot data and only then empties the slot.
            // Do not clear a slot ourselves or create a copy from a cached client snapshot.
            // ponytail: vanilla RPC has no expected-GUID argument; rejecting an in-flight
            // slot replacement requires a cooperating mod on the host/owner.
            characterItems.photonView.RPC("DropItemFromSlotRPC", RpcTarget.MasterClient,
                slot.itemSlotID, original.transform.position + Vector3.down * 0.2f);

            var deadline = Time.unscaledTime + SyncTimeout;
            Item ground = null;
            while (Time.unscaledTime < deadline)
            {
                ground = FindGroundItem(guid);
                if (ground != null && character != null && character.player != null &&
                    !InventoryContains(character.player, guid)) break;
                yield return new WaitForSecondsRealtime(0.05f);
            }
            if (ground == null || character == null || character.player == null ||
                InventoryContains(character.player, guid))
            {
                logger.LogWarning("Expelliarmus: drop not confirmed; held object was not removed by this mod.");
                yield break;
            }

            if (character.data.currentItem == original && original != null &&
                original.photonView.ViewID == originalViewID)
            {
                if (character.IsLocal)
                    characterItems.EquipSlot(Optionable<byte>.None);
                else
                    characterItems.photonView.RPC("EquipSlotRpc", RpcTarget.AllViaServer, -1, -1);
            }
            // The owner's native unequip destroys its held view. Wait for that network
            // acknowledgement before allowing pickup of the replacement ground object.
            deadline = Time.unscaledTime + SyncTimeout;
            while (PhotonNetwork.GetPhotonView(originalViewID) != null && Time.unscaledTime < deadline)
                yield return new WaitForSecondsRealtime(0.05f);
            if (PhotonNetwork.GetPhotonView(originalViewID) != null)
            {
                logger.LogWarning("Expelliarmus: owner release not confirmed; pickup cancelled, ground item retained.");
                yield break;
            }
            droppedItem = FindGroundItem(guid);
        }

        private static bool InventoryContains(Player player, Guid guid)
        {
            foreach (var slot in player.itemSlots)
                if (SlotContains(slot, guid)) return true;
            return SlotContains(player.GetItemSlot(3), guid) || SlotContains(player.GetItemSlot(250), guid);
        }

        private static bool SlotContains(ItemSlot slot, Guid guid)
        {
            return slot != null && !slot.IsEmpty() && slot.data != null && slot.data.guid == guid;
        }

        private static Item FindGroundItem(Guid guid)
        {
            foreach (var item in Item.ALL_ITEMS)
                if (IsGroundItem(item, guid)) return item;
            return null;
        }

        private static bool IsGroundItem(Item item, Guid guid)
        {
            return item != null && item.data != null && item.data.guid == guid &&
                item.itemState == ItemState.Ground && item.trueHolderCharacter == null &&
                item.photonView != null && item.photonView.IsRoomView && item.photonView.ViewID > 0;
        }
    }
}
