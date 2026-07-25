using System;
using System.Collections;
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

            object targetPlayer = GetMemberValue(targetCharacter, "player");
            object targetSlot = FindSlotContainingItem(targetPlayer, item);
            if (targetPlayer == null || targetSlot == null)
            {
                logger.LogInfo("Expelliarmus: could not resolve the target inventory slot.");
                return false;
            }

            byte targetSlotID = Convert.ToByte(GetMemberValue(targetSlot, "itemSlotID"));
            if (!DropLocalHeldItem(localCharacter))
            {
                logger.LogInfo("Expelliarmus: could not drop the local held item.");
                return false;
            }

            object targetPlayerView = GetMemberValue(targetPlayer, "view");
            bool removeSent = SendPhotonRpc(
                targetPlayerView,
                "RPCRemoveItemFromSlot",
                "MasterClient",
                targetSlotID);

            if (!removeSent)
            {
                logger.LogInfo("Expelliarmus: failed to send the target inventory removal RPC.");
                return false;
            }

            StartCoroutine(FinishStealAfterInventorySync(localView, item));
            logger.LogInfo("Expelliarmus: transfer requested for target slot " + targetSlotID + ".");
            return true;
        }

        private bool DropLocalHeldItem(object localCharacter)
        {
            object currentItem = GetCurrentItem(localCharacter);
            if (currentItem == null)
            {
                return true;
            }

            object localPlayer = GetMemberValue(localCharacter, "player");
            object localSlot = FindSlotContainingItem(localPlayer, currentItem);
            if (localSlot == null)
            {
                logger.LogInfo("Expelliarmus: local current item has no inventory slot.");
                return false;
            }

            byte localSlotID = Convert.ToByte(GetMemberValue(localSlot, "itemSlotID"));
            Component currentItemComponent = currentItem as Component;
            Vector3 dropPosition = currentItemComponent != null
                ? currentItemComponent.transform.position + Vector3.down * 0.2f
                : ((Component)localCharacter).transform.position + Vector3.up;

            object localItems = GetComponentFromCharacter(localCharacter, "CharacterItems");
            object localItemsView = GetMemberValue(localItems, "photonView");
            bool dropSent = SendPhotonRpc(
                localItemsView,
                "DropItemFromSlotRPC",
                "MasterClient",
                localSlotID,
                dropPosition);
            bool unequipSent = SendPhotonRpc(
                localItemsView,
                "EquipSlotRpc",
                "All",
                -1,
                -1);

            if (dropSent && unequipSent)
            {
                logger.LogInfo("Expelliarmus: dropped local held slot " + localSlotID + ".");
            }
            return dropSent && unequipSent;
        }

        private IEnumerator FinishStealAfterInventorySync(object localView, object item)
        {
            yield return new WaitForSeconds(0.12f);

            Component itemComponent = item as Component;
            if (itemComponent == null)
            {
                logger.LogInfo("Expelliarmus: target item disappeared before pickup request.");
                yield break;
            }

            object itemView = GetMemberValue(item, "view");
            if (itemView == null)
            {
                itemView = GetMemberValue(item, "photonView");
            }

            bool requestSent = SendPhotonRpc(
                itemView,
                "RequestPickup",
                "MasterClient",
                localView);
            logger.LogInfo(requestSent
                ? "Expelliarmus: master pickup request sent."
                : "Expelliarmus: failed to send master pickup request.");
        }

        private bool SendPhotonRpc(
            object photonView,
            string rpcMethodName,
            string rpcTargetName,
            params object[] rpcArguments)
        {
            if (photonView == null) return false;

            try
            {
                Type rpcTargetType = photonView.GetType().Assembly.GetType("Photon.Pun.RpcTarget");
                if (rpcTargetType == null) return false;

                object rpcTarget = Enum.Parse(rpcTargetType, rpcTargetName);
                foreach (MethodInfo method in photonView.GetType().GetMethods(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    if (method.Name != "RPC") continue;
                    ParameterInfo[] parameters = method.GetParameters();
                    if (parameters.Length != 3) continue;
                    if (parameters[0].ParameterType != typeof(string)) continue;
                    if (parameters[1].ParameterType != rpcTargetType) continue;
                    if (parameters[2].ParameterType != typeof(object[])) continue;

                    method.Invoke(
                        photonView,
                        new object[] { rpcMethodName, rpcTarget, rpcArguments });
                    logger.LogInfo(
                        "Sent RPC " + rpcMethodName + " -> " + rpcTargetName + ".");
                    return true;
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug("SendPhotonRpc " + rpcMethodName + ": " + ex.Message);
            }
            return false;
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
                if (itemData != null && slotData != null)
                {
                    object itemGuid = GetMemberValue(itemData, "guid");
                    object slotGuid = GetMemberValue(slotData, "guid");
                    if (itemGuid != null && itemGuid.Equals(slotGuid)) return slot;
                }
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
