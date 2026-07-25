using System;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace Expelliarmus
{
    public class ExpelliarmusBehaviour : MonoBehaviour
    {
        private const float MAX_RANGE = 7.5f;
        private const float AIM_ITEM_ANGLE = 25f;
        private const float COOLDOWN = 0.4f;

        private static ManualLogSource logger;

        private Type characterType;
        private Type itemType;
        private float nextUseTime;

        public static void Initialize(ManualLogSource log)
        {
            logger = log;
            var go = new GameObject("ExpelliarmusManager");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.AddComponent<ExpelliarmusBehaviour>();
            logger.LogInfo("ExpelliarmusBehaviour initialized");
        }

        private void Start()
        {
            ResolveTypes();
        }

        private void Update()
        {
            if ((characterType == null || itemType == null) && Time.frameCount % 300 == 0)
            {
                ResolveTypes();
            }

            object localCharacter = GetLocalCharacter();
            if (localCharacter == null) return;

            bool rightClick = Input.GetMouseButtonDown(1) || CharacterInputFlag(localCharacter, "useSecondaryWasPressed");
            if (!rightClick) return;
            if (Time.time < nextUseTime) return;

            nextUseTime = Time.time + COOLDOWN;
            TryExpelliarmus(localCharacter);
        }

        private void ResolveTypes()
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.GetName().Name != "Assembly-CSharp") continue;
                characterType = asm.GetType("Character");
                itemType = asm.GetType("Item");
                break;
            }

            logger.LogInfo("Character type: " + (characterType != null ? characterType.FullName : "NOT FOUND"));
            logger.LogInfo("Item type: " + (itemType != null ? itemType.FullName : "NOT FOUND"));
        }

        private object GetLocalCharacter()
        {
            if (characterType == null) return null;
            try
            {
                var field = AccessTools.Field(characterType, "localCharacter");
                if (field != null)
                {
                    var value = field.GetValue(null);
                    if (value != null) return value;
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug("GetLocalCharacter: " + ex.Message);
            }
            return null;
        }

        private void TryExpelliarmus(object localCharacter)
        {
            try
            {
                if (Camera.main == null)
                {
                    logger.LogInfo("Expelliarmus: no main camera.");
                    return;
                }

                Ray ray = new Ray(Camera.main.transform.position, Camera.main.transform.forward);
                RaycastHit hit;
                if (!Physics.Raycast(ray, out hit, MAX_RANGE, ~0, QueryTriggerInteraction.Collide))
                {
                    logger.LogInfo("Expelliarmus: right click, but raycast hit nothing.");
                    return;
                }

                object targetCharacter = FindCharacterFromTransform(hit.collider.transform);
                object hitItem = FindItemFromTransform(hit.collider.transform);

                if (targetCharacter == null && hitItem != null)
                {
                    targetCharacter = GetItemHolder(hitItem);
                }

                if (targetCharacter == null || targetCharacter == localCharacter)
                {
                    logger.LogInfo("Expelliarmus: no teammate target. hit=" + hit.collider.name);
                    return;
                }

                object targetItem = GetCurrentItem(targetCharacter);
                if (targetItem == null)
                {
                    logger.LogInfo("Expelliarmus: target has no currentItem.");
                    return;
                }

                if (!IsAimAcceptable(hitItem, targetItem))
                {
                    logger.LogInfo("Expelliarmus: target currentItem exists, but crosshair is not close enough to item.");
                    return;
                }

                logger.LogInfo("Expelliarmus target character=" + CharacterName(targetCharacter) + " item=" + ItemName(targetItem));
                bool success = StealCurrentItem(localCharacter, targetCharacter, targetItem);
                logger.LogInfo(success ? "Expelliarmus succeeded." : "Expelliarmus failed.");
            }
            catch (Exception ex)
            {
                logger.LogError("TryExpelliarmus error: " + ex.Message + "\n" + ex.StackTrace);
            }
        }

        private object FindCharacterFromTransform(Transform t)
        {
            while (t != null)
            {
                if (characterType != null)
                {
                    var comp = t.GetComponent(characterType);
                    if (comp != null) return comp;
                }
                t = t.parent;
            }
            return null;
        }

        private object FindItemFromTransform(Transform t)
        {
            while (t != null)
            {
                if (itemType != null)
                {
                    var comp = t.GetComponent(itemType);
                    if (comp != null) return comp;
                }
                t = t.parent;
            }
            return null;
        }

        private object GetItemHolder(object item)
        {
            object holder = GetMemberValue(item, "holderCharacter");
            if (holder != null) return holder;
            holder = GetMemberValue(item, "trueHolderCharacter");
            if (holder != null) return holder;
            holder = GetMemberValue(item, "_holderCharacter");
            if (holder != null) return holder;
            return GetMemberValue(item, "lastHolderCharacter");
        }

        private object GetCurrentItem(object character)
        {
            object data = GetMemberValue(character, "data");
            if (data == null) return null;

            object current = GetMemberValue(data, "currentItem");
            if (current != null) return current;
            return GetMemberValue(data, "_currentitem");
        }

        private bool IsAimAcceptable(object hitItem, object targetItem)
        {
            if (hitItem != null && ReferenceEquals(hitItem, targetItem)) return true;

            var itemComp = targetItem as Component;
            if (itemComp == null || Camera.main == null) return false;

            Vector3 toItem = itemComp.transform.position - Camera.main.transform.position;
            float angle = Vector3.Angle(Camera.main.transform.forward, toItem);
            return angle <= AIM_ITEM_ANGLE;
        }

        private bool StealCurrentItem(object localCharacter, object targetCharacter, object item)
        {
            object localView = GetMemberValue(localCharacter, "view");
            if (localView == null)
            {
                logger.LogInfo("Expelliarmus: local character has no PhotonView.");
                return false;
            }

            bool success = false;

            // First break the original owner's hand/inventory relationship.
            // Without this, PEAK can leave both characters referencing the same currentItem.
            DisarmTargetOwner(targetCharacter, item);

            // Transfer the inventory slot data before trying the visual hand attach.
            success |= TryTransferInventorySlot(localCharacter, targetCharacter, item);

            // Let PEAK's native pickup path run after the target is no longer holding it.
            success |= TryInvoke(item, "RequestPickup", localView);

            // Visual/state path: force equip/attach if the pickup RPC did not immediately win.
            object localItems = GetComponentFromCharacter(localCharacter, "CharacterItems");
            if (localItems != null)
            {
                success |= TryInvoke(localItems, "Equip", item);
                success |= TryInvoke(localItems, "AttachItem", item);
                TryInvoke(localItems, "HoldItem", item);
            }

            ForceItemStateHeld(item, localCharacter);
            SetCurrentItem(localCharacter, item);
            return success;
        }

        private void DisarmTargetOwner(object targetCharacter, object item)
        {
            object targetItems = GetComponentFromCharacter(targetCharacter, "CharacterItems");
            if (targetItems != null)
            {
                TryInvoke(targetItems, "UnAttachEquippedItem");
                TryInvoke(targetItems, "UnAttachItem");
            }

            SetCurrentItem(targetCharacter, null);
            ClearItemHolderFields(item, targetCharacter);
            ForceItemStateGround(item);
        }

        private bool TryTransferInventorySlot(object localCharacter, object targetCharacter, object item)
        {
            try
            {
                object localPlayer = GetMemberValue(localCharacter, "player");
                object targetPlayer = GetMemberValue(targetCharacter, "player");
                if (localPlayer == null || targetPlayer == null) return false;

                object targetSlot = FindSlotContainingItem(targetPlayer, item);
                object localSlot = FindEmptySlot(localPlayer);
                if (targetSlot == null || localSlot == null)
                {
                    logger.LogInfo("Expelliarmus: slot transfer skipped. targetSlot=" + (targetSlot != null) + " localSlot=" + (localSlot != null));
                    return TryAddToLocalInventory(localCharacter, targetCharacter, item);
                }

                object prefab = GetMemberValue(targetSlot, "prefab");
                object data = GetMemberValue(targetSlot, "data");
                byte targetSlotID = Convert.ToByte(GetMemberValue(targetSlot, "itemSlotID"));
                byte localSlotID = Convert.ToByte(GetMemberValue(localSlot, "itemSlotID"));

                if (!TryInvoke(localSlot, "SetItem", prefab, data))
                {
                    return TryAddToLocalInventory(localCharacter, targetCharacter, item);
                }

                TryInvoke(targetSlot, "EmptyOut");
                TryInvoke(targetPlayer, "RPCRemoveItemFromSlot", targetSlotID);

                object localItems = GetComponentFromCharacter(localCharacter, "CharacterItems");
                if (localItems != null)
                {
                    TryInvoke(localItems, "OnPickupAccepted", localSlotID);
                    TryInvoke(localItems, "Equip", item);
                    TryInvoke(localItems, "AttachItem", item);
                    SetSelectedSlot(localItems, localSlotID);
                }

                TryInvoke(localPlayer, "SyncInventoryRPC", new byte[0], true);
                TryInvoke(targetPlayer, "SyncInventoryRPC", new byte[0], true);
                logger.LogInfo("Expelliarmus transferred slot target=" + targetSlotID + " -> local=" + localSlotID);
                return true;
            }
            catch (Exception ex)
            {
                logger.LogDebug("TryTransferInventorySlot: " + ex.Message);
                return false;
            }
        }

        private bool TryAddToLocalInventory(object localCharacter, object targetCharacter, object item)
        {
            try
            {
                object localPlayer = GetMemberValue(localCharacter, "player");
                object targetPlayer = GetMemberValue(targetCharacter, "player");
                if (localPlayer == null || targetPlayer == null) return false;

                ushort itemID = Convert.ToUInt16(GetMemberValue(item, "itemID"));
                object data = GetMemberValue(item, "data");

                object[] addArgs = new object[] { itemID, data, null };
                var addMethod = FindMethod(localPlayer.GetType(), "AddItem", 3);
                if (addMethod == null) return false;

                bool added = (bool)addMethod.Invoke(localPlayer, addArgs);
                if (!added) return false;

                object addedSlot = addArgs[2];
                byte targetSlot = GetSelectedSlot(targetCharacter);
                TryInvoke(targetPlayer, "RPCRemoveItemFromSlot", targetSlot);

                object localItems = GetComponentFromCharacter(localCharacter, "CharacterItems");
                if (localItems != null && addedSlot != null)
                {
                    byte localSlot = Convert.ToByte(GetMemberValue(addedSlot, "itemSlotID"));
                    TryInvoke(localItems, "OnPickupAccepted", localSlot);
                    SetSelectedSlot(localItems, localSlot);
                }

                logger.LogInfo("Expelliarmus used Player.AddItem + RPCRemoveItemFromSlot. targetSlot=" + targetSlot);
                return true;
            }
            catch (Exception ex)
            {
                logger.LogDebug("TryAddToLocalInventory: " + ex.Message);
                return false;
            }
        }

        private void ForceItemStateHeld(object item, object holder)
        {
            try
            {
                TryInvoke(item, "SetState", Enum.Parse(GetMemberValue(item, "itemState").GetType(), "Held"), holder);
                TryInvoke(item, "SetKinematicNetworked", true);
            }
            catch (Exception ex)
            {
                logger.LogDebug("ForceItemStateHeld: " + ex.Message);
            }
        }

        private void ForceItemStateGround(object item)
        {
            try
            {
                object state = GetMemberValue(item, "itemState");
                if (state != null)
                {
                    TryInvoke(item, "SetState", Enum.Parse(state.GetType(), "Ground"), null);
                }
                TryInvoke(item, "SetKinematicNetworked", false);
            }
            catch (Exception ex)
            {
                logger.LogDebug("ForceItemStateGround: " + ex.Message);
            }
        }

        private void ClearItemHolderFields(object item, object oldHolder)
        {
            string[] names = {
                "_holderCharacter", "overrideHolderCharacter", "holderCharacter",
                "wearerCharacter", "lastHolderCharacter", "lastThrownCharacter"
            };

            foreach (var name in names)
            {
                object current = GetMemberValue(item, name);
                if (current == null || ReferenceEquals(current, oldHolder))
                {
                    SetMemberValue(item, name, null);
                }
            }
        }

        private void SetCurrentItem(object character, object item)
        {
            object data = GetMemberValue(character, "data");
            if (data == null) return;
            SetMemberValue(data, "_currentitem", item);
        }

        private object FindSlotContainingItem(object player, object item)
        {
            object slotsObject = GetMemberValue(player, "itemSlots");
            Array slots = slotsObject as Array;
            if (slots == null) return null;

            object itemData = GetMemberValue(item, "data");
            object itemID = GetMemberValue(item, "itemID");

            foreach (object slot in slots)
            {
                if (slot == null) continue;
                object slotData = GetMemberValue(slot, "data");
                object prefab = GetMemberValue(slot, "prefab");

                if (itemData != null && ReferenceEquals(slotData, itemData)) return slot;
                if (prefab != null && itemID != null)
                {
                    object prefabID = GetMemberValue(prefab, "itemID");
                    if (prefabID != null && prefabID.Equals(itemID)) return slot;
                }
            }

            return null;
        }

        private object FindEmptySlot(object player)
        {
            object slotsObject = GetMemberValue(player, "itemSlots");
            Array slots = slotsObject as Array;
            if (slots == null) return null;

            foreach (object slot in slots)
            {
                if (slot == null) continue;
                object id = GetMemberValue(slot, "itemSlotID");
                if (id != null && Convert.ToByte(id) > 2) continue;

                if (TryInvokeBool(slot, "IsEmpty"))
                {
                    return slot;
                }
            }

            foreach (object slot in slots)
            {
                if (slot != null && TryInvokeBool(slot, "IsEmpty")) return slot;
            }

            return null;
        }

        private void SetSelectedSlot(object characterItems, byte slotID)
        {
            object selected = GetMemberValue(characterItems, "currentSelectedSlot");
            if (selected == null) return;

            if (!SetMemberValue(selected, "value", slotID))
            {
                SetFirstByteField(selected, slotID);
            }
        }

        private byte GetSelectedSlot(object character)
        {
            object items = GetComponentFromCharacter(character, "CharacterItems");
            object selected = GetMemberValue(items, "currentSelectedSlot");
            object value = GetOptionableValue(selected);
            if (value == null) return 0;
            return Convert.ToByte(value);
        }

        private object GetOptionableValue(object optionable)
        {
            if (optionable == null) return null;
            object value = GetMemberValue(optionable, "Value");
            if (value != null) return value;
            value = GetMemberValue(optionable, "value");
            if (value != null) return value;

            foreach (var field in optionable.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (field.FieldType == typeof(byte) || field.FieldType == typeof(int))
                {
                    return field.GetValue(optionable);
                }
            }
            return null;
        }

        private object GetComponentFromCharacter(object character, string typeName)
        {
            var comp = character as Component;
            if (comp == null) return null;

            foreach (var c in comp.GetComponents<Component>())
            {
                if (c != null && c.GetType().Name == typeName) return c;
            }
            foreach (var c in comp.GetComponentsInChildren<Component>())
            {
                if (c != null && c.GetType().Name == typeName) return c;
            }
            return null;
        }

        private bool CharacterInputFlag(object character, string fieldName)
        {
            try
            {
                object input = GetMemberValue(character, "input");
                object value = GetMemberValue(input, fieldName);
                return value is bool && (bool)value;
            }
            catch
            {
                return false;
            }
        }

        private bool TryInvoke(object target, string methodName, params object[] args)
        {
            if (target == null) return false;
            try
            {
                var method = FindMethod(target.GetType(), methodName, args.Length);
                if (method == null) return false;
                method.Invoke(target, args);
                logger.LogInfo("Invoked " + target.GetType().Name + "." + methodName);
                return true;
            }
            catch (Exception ex)
            {
                logger.LogDebug("TryInvoke " + methodName + ": " + ex.Message);
            }
            return false;
        }

        private bool TryInvokeBool(object target, string methodName)
        {
            if (target == null) return false;
            try
            {
                var method = FindMethod(target.GetType(), methodName, 0);
                if (method == null) return false;
                object result = method.Invoke(target, null);
                return result is bool && (bool)result;
            }
            catch
            {
                return false;
            }
        }
        private MethodInfo FindMethod(Type type, string name, int argCount)
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var method in methods)
            {
                if (method.Name == name && method.GetParameters().Length == argCount)
                {
                    return method;
                }
            }
            return null;
        }

        private object GetMemberValue(object target, string name)
        {
            if (target == null) return null;
            var type = target.GetType();
            var field = AccessTools.Field(type, name);
            if (field != null) return field.GetValue(target);
            var prop = AccessTools.Property(type, name);
            if (prop != null) return prop.GetValue(target, null);
            return null;
        }

        private bool SetMemberValue(object target, string name, object value)
        {
            if (target == null) return false;
            try
            {
                var type = target.GetType();
                var field = AccessTools.Field(type, name);
                if (field != null)
                {
                    field.SetValue(target, value);
                    return true;
                }

                var prop = AccessTools.Property(type, name);
                if (prop != null && prop.CanWrite)
                {
                    prop.SetValue(target, value, null);
                    return true;
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug("SetMemberValue " + name + ": " + ex.Message);
            }
            return false;
        }

        private bool SetFirstByteField(object target, byte value)
        {
            if (target == null) return false;
            try
            {
                foreach (var field in target.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    if (field.FieldType == typeof(byte))
                    {
                        field.SetValue(target, value);
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }

        private string CharacterName(object character)
        {
            object name = GetMemberValue(character, "characterName");
            return name != null ? name.ToString() : ((Component)character).name;
        }

        private string ItemName(object item)
        {
            if (item == null) return "null";
            try
            {
                var method = FindMethod(item.GetType(), "GetName", 0);
                if (method != null)
                {
                    object result = method.Invoke(item, null);
                    if (result != null) return result.ToString();
                }
            }
            catch { }
            var comp = item as Component;
            return comp != null ? comp.name : item.ToString();
        }
    }
}
