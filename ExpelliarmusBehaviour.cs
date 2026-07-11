using System;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace Expelliarmus
{
    /// <summary>
    /// Right-click disarm mechanic:
    /// when the center crosshair is aimed at another player's held item, steal it into local hand.
    /// The code intentionally uses reflection because PEAK's gameplay types are game-private.
    /// </summary>
    public class ExpelliarmusBehaviour : MonoBehaviour
    {
        private const float MAX_RANGE = 4.0f;
        private const float COOLDOWN = 0.35f;
        private const float PULL_SPEED = 18.0f;

        private static ManualLogSource logger;

        private Type characterType;
        private Type photonViewType;
        private object rpcTargetAll;
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
            if (characterType == null && Time.frameCount % 300 == 0)
            {
                ResolveTypes();
            }

            if (!Input.GetMouseButtonDown(1)) return;
            if (Time.time < nextUseTime) return;

            nextUseTime = Time.time + COOLDOWN;
            TryDisarmFromCrosshair();
        }

        private void ResolveTypes()
        {
            Assembly gameAsm = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.GetName().Name == "Assembly-CSharp")
                {
                    gameAsm = asm;
                    break;
                }
            }

            if (gameAsm != null)
            {
                characterType = gameAsm.GetType("Character");
                if (characterType == null)
                {
                    foreach (var t in gameAsm.GetTypes())
                    {
                        if (t.Name == "Character" || t.Name == "Player")
                        {
                            characterType = t;
                            break;
                        }
                    }
                }
            }

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var name = asm.GetName().Name;
                if (name == "PhotonUnityNetworking" || name == "PhotonRealtime" ||
                    name == "Photon3Unity3D" || name.Contains("Photon"))
                {
                    if (photonViewType == null)
                    {
                        photonViewType = asm.GetType("Photon.Pun.PhotonView");
                    }
                }
            }

            if (photonViewType == null)
            {
                photonViewType = Type.GetType("Photon.Pun.PhotonView, PhotonUnityNetworking");
            }

            if (photonViewType != null)
            {
                var rpcTargetType = photonViewType.Assembly.GetType("Photon.Pun.RpcTarget");
                if (rpcTargetType == null)
                {
                    rpcTargetType = Type.GetType("Photon.Pun.RpcTarget, PhotonUnityNetworking");
                }
                if (rpcTargetType != null)
                {
                    var allField = AccessTools.Field(rpcTargetType, "All");
                    if (allField != null) rpcTargetAll = allField.GetValue(null);
                    if (rpcTargetAll == null)
                    {
                        var viaServer = AccessTools.Field(rpcTargetType, "AllViaServer");
                        if (viaServer != null) rpcTargetAll = viaServer.GetValue(null);
                    }
                }
            }

            if (characterType != null)
            {
                logger.LogInfo("Character type: " + characterType.FullName);
            }
            else
            {
                logger.LogWarning("Character type NOT FOUND - will retry");
            }

            if (photonViewType != null)
            {
                logger.LogInfo("PhotonView type: " + photonViewType.FullName);
            }
            else
            {
                logger.LogWarning("PhotonView type NOT FOUND - RPC sync fallback disabled");
            }
        }

        private void TryDisarmFromCrosshair()
        {
            try
            {
                var localPlayer = FindLocalPlayerGameObject();
                if (localPlayer == null)
                {
                    logger.LogDebug("No local player found.");
                    return;
                }

                var camera = Camera.main;
                if (camera == null)
                {
                    logger.LogDebug("No main camera found.");
                    return;
                }

                Ray ray = new Ray(camera.transform.position, camera.transform.forward);
                RaycastHit hit;
                if (!Physics.Raycast(ray, out hit, MAX_RANGE, ~0, QueryTriggerInteraction.Collide))
                {
                    return;
                }

                var itemObject = FindLikelyItemRoot(hit.collider.gameObject);
                if (itemObject == null)
                {
                    logger.LogDebug("Ray hit object is not an item: " + hit.collider.gameObject.name);
                    return;
                }

                var owner = FindOwningCharacter(itemObject);
                if (owner == null || owner == localPlayer)
                {
                    logger.LogDebug("Item has no other-character owner: " + itemObject.name);
                    return;
                }

                if (!IsLikelyHeldItem(itemObject, owner))
                {
                    logger.LogDebug("Item is not currently held: " + itemObject.name);
                    return;
                }

                logger.LogInfo("Expelliarmus target: item=" + itemObject.name + ", owner=" + owner.name);
                bool success = StealItem(localPlayer, owner, itemObject);
                logger.LogInfo(success ? "Expelliarmus succeeded." : "Expelliarmus fallback completed with uncertain state.");
            }
            catch (Exception ex)
            {
                logger.LogError("TryDisarmFromCrosshair error: " + ex.Message + "\n" + ex.StackTrace);
            }
        }

        private GameObject FindLocalPlayerGameObject()
        {
            if (photonViewType != null)
            {
                var allObjs = FindObjectsOfType<GameObject>();
                foreach (var go in allObjs)
                {
                    try
                    {
                        var pv = go.GetComponent(photonViewType);
                        if (pv == null) continue;

                        var isMine = Traverse.Create(pv).Property("IsMine").GetValue<bool>();
                        if (!isMine) continue;

                        if (characterType != null)
                        {
                            var ch = go.GetComponent(characterType);
                            if (ch != null) return go;
                            ch = go.GetComponentInChildren(characterType);
                            if (ch != null) return ((Component)ch).gameObject;
                        }
                        return go;
                    }
                    catch { }
                }
            }

            if (characterType != null)
            {
                var names = new[] {
                    "localPlayer", "LocalPlayer", "Local", "Instance",
                    "local", "LocalInstance", "PlayerInstance"
                };

                foreach (var name in names)
                {
                    try
                    {
                        var prop = characterType.GetProperty(name,
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                        if (prop != null)
                        {
                            var val = prop.GetValue(null, null);
                            if (val is Component) return ((Component)val).gameObject;
                            if (val is GameObject) return (GameObject)val;
                        }

                        var field = characterType.GetField(name,
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                        if (field != null)
                        {
                            var val = field.GetValue(null);
                            if (val is Component) return ((Component)val).gameObject;
                            if (val is GameObject) return (GameObject)val;
                        }
                    }
                    catch { }
                }
            }

            return null;
        }

        private GameObject FindLikelyItemRoot(GameObject hitObject)
        {
            Transform t = hitObject.transform;
            GameObject best = null;
            while (t != null)
            {
                if (HasItemLikeComponent(t.gameObject) || LooksLikeItemName(t.name))
                {
                    best = t.gameObject;
                }

                if (HasCharacterComponent(t.gameObject))
                {
                    break;
                }
                t = t.parent;
            }
            return best;
        }

        private bool HasItemLikeComponent(GameObject go)
        {
            var comps = go.GetComponents<Component>();
            foreach (var comp in comps)
            {
                if (comp == null) continue;
                string n = comp.GetType().Name;
                if (n == "Item" || n.EndsWith("Item") || n.Contains("Pickup") ||
                    n.Contains("Interactable") || n.Contains("Throwable"))
                {
                    return true;
                }
            }
            return false;
        }

        private bool LooksLikeItemName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string n = name.ToLowerInvariant();
            return n.Contains("item") || n.Contains("weapon") || n.Contains("tool") ||
                   n.Contains("held") || n.Contains("prop") || n.Contains("pickup");
        }

        private bool HasCharacterComponent(GameObject go)
        {
            if (go == null || characterType == null) return false;
            try
            {
                return go.GetComponent(characterType) != null;
            }
            catch { return false; }
        }

        private GameObject FindOwningCharacter(GameObject itemObject)
        {
            Transform t = itemObject.transform.parent;
            while (t != null)
            {
                if (HasCharacterComponent(t.gameObject))
                {
                    return t.gameObject;
                }
                t = t.parent;
            }
            return null;
        }

        private bool IsLikelyHeldItem(GameObject itemObject, GameObject owner)
        {
            if (itemObject == null || owner == null) return false;

            Transform t = itemObject.transform;
            while (t != null && t != owner.transform)
            {
                string n = t.name.ToLowerInvariant();
                if (n.Contains("hand") || n.Contains("right") || n.Contains("left") ||
                    n.Contains("slot") || n.Contains("hotbar") || n.Contains("hold") ||
                    n.Contains("inventory"))
                {
                    return true;
                }
                t = t.parent;
            }

            return Vector3.Distance(itemObject.transform.position, owner.transform.position) < 2.5f;
        }

        private bool StealItem(GameObject localPlayer, GameObject owner, GameObject itemObject)
        {
            bool anySuccess = false;

            // Ask the game systems first. If one of these matches the current PEAK build,
            // multiplayer ownership and inventory sync should be handled by the game itself.
            anySuccess |= TryInvokeKnownMethods(itemObject, localPlayer, owner);
            anySuccess |= TryInvokeKnownMethods(localPlayer, itemObject, owner);
            anySuccess |= TryInvokeKnownMethods(owner, itemObject, localPlayer);
            anySuccess |= TryKnownRPCs(localPlayer, owner, itemObject);

            // Physical and hierarchy fallback. This makes the item visibly leave the target hand,
            // then tries to parent it to our hand/inventory transform.
            ForceDetachFromOwner(itemObject);
            MoveItemToLocalHand(localPlayer, itemObject);
            anySuccess = true;

            return anySuccess;
        }

        private bool TryInvokeKnownMethods(GameObject receiver, GameObject item, GameObject other)
        {
            if (receiver == null) return false;

            var methods = receiver.GetComponents<Component>();
            bool success = false;
            foreach (var comp in methods)
            {
                if (comp == null) continue;

                success |= TryInvokeMethod(comp, "RequestPickup", item);
                success |= TryInvokeMethod(comp, "RequestPickup", item, other);
                success |= TryInvokeMethod(comp, "OnPickupAccepted", item);
                success |= TryInvokeMethod(comp, "OnPickupAccepted", item, other);
                success |= TryInvokeMethod(comp, "Pickup", item);
                success |= TryInvokeMethod(comp, "PickUp", item);
                success |= TryInvokeMethod(comp, "Grab", item);
                success |= TryInvokeMethod(comp, "TakeItem", item);
                success |= TryInvokeMethod(comp, "AddItem", item);
                success |= TryInvokeMethod(comp, "RPCAddItemToCharacterBackpack", item);
                success |= TryInvokeMethod(comp, "SyncInventoryRPC");
            }
            return success;
        }

        private bool TryInvokeMethod(object target, string methodName, params object[] args)
        {
            if (target == null) return false;

            try
            {
                var methods = target.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (var method in methods)
                {
                    if (method.Name != methodName) continue;
                    var ps = method.GetParameters();
                    if (ps.Length != args.Length) continue;

                    object[] converted = new object[args.Length];
                    bool canUse = true;
                    for (int i = 0; i < args.Length; i++)
                    {
                        converted[i] = ConvertArgument(args[i], ps[i].ParameterType);
                        if (converted[i] == null && ps[i].ParameterType.IsValueType)
                        {
                            canUse = false;
                            break;
                        }
                    }

                    if (!canUse) continue;

                    method.Invoke(target, converted);
                    logger.LogInfo("Invoked " + target.GetType().Name + "." + methodName);
                    return true;
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug("TryInvokeMethod " + methodName + ": " + ex.Message);
            }

            return false;
        }

        private object ConvertArgument(object value, Type parameterType)
        {
            if (value == null) return null;
            if (parameterType.IsInstanceOfType(value)) return value;

            var go = value as GameObject;
            if (go != null)
            {
                if (parameterType == typeof(GameObject)) return go;
                if (typeof(Component).IsAssignableFrom(parameterType))
                {
                    return go.GetComponent(parameterType) ?? go.GetComponentInChildren(parameterType);
                }
            }

            var comp = value as Component;
            if (comp != null)
            {
                if (parameterType == typeof(GameObject)) return comp.gameObject;
                if (parameterType.IsInstanceOfType(comp)) return comp;
            }

            return null;
        }

        private bool TryKnownRPCs(GameObject localPlayer, GameObject owner, GameObject itemObject)
        {
            if (photonViewType == null) return false;

            bool success = false;
            success |= TryRPCOnObject(localPlayer, "RPCAddItemToCharacterBackpack", itemObject);
            success |= TryRPCOnObject(localPlayer, "RPC_RequestFakeItemPickup", itemObject);
            success |= TryRPCOnObject(localPlayer, "RPCA_GrabCharacter", itemObject);
            success |= TryRPCOnObject(owner, "RPCA_Throw", Vector3.zero);
            success |= TryRPCOnObject(owner, "RPCA_LetGo");
            return success;
        }

        private bool TryRPCOnObject(GameObject obj, string rpcName, params object[] args)
        {
            if (obj == null || photonViewType == null) return false;

            try
            {
                var pv = obj.GetComponent(photonViewType);
                if (pv == null) pv = obj.GetComponentInChildren(photonViewType);
                if (pv == null) return false;

                var type = pv.GetType();
                var rpcMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                foreach (var method in rpcMethods)
                {
                    if (method.Name != "RPC") continue;
                    var ps = method.GetParameters();
                    if (ps.Length == 3 && ps[0].ParameterType == typeof(string))
                    {
                        method.Invoke(pv, new object[] { rpcName, rpcTargetAll ?? 1, args });
                        logger.LogInfo("RPC call attempted: " + rpcName);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug("TryRPCOnObject " + rpcName + ": " + ex.Message);
            }

            return false;
        }

        private void ForceDetachFromOwner(GameObject itemObject)
        {
            if (itemObject == null) return;

            try
            {
                itemObject.transform.SetParent(null, true);

                var rb = itemObject.GetComponent<Rigidbody>();
                if (rb == null) rb = itemObject.GetComponentInChildren<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = false;
                    rb.velocity = Vector3.zero;
                }

                var colliders = itemObject.GetComponentsInChildren<Collider>();
                foreach (var col in colliders)
                {
                    col.enabled = true;
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug("ForceDetachFromOwner: " + ex.Message);
            }
        }

        private void MoveItemToLocalHand(GameObject localPlayer, GameObject itemObject)
        {
            if (localPlayer == null || itemObject == null) return;

            try
            {
                Transform hand = FindHandTransform(localPlayer);
                if (hand != null)
                {
                    itemObject.transform.SetParent(hand, false);
                    itemObject.transform.localPosition = Vector3.zero;
                    itemObject.transform.localRotation = Quaternion.identity;
                    logger.LogInfo("Moved item to local hand: " + hand.name);
                    return;
                }

                var camera = Camera.main;
                Vector3 targetPos = camera != null
                    ? camera.transform.position + camera.transform.forward * 0.75f + camera.transform.right * 0.25f - camera.transform.up * 0.15f
                    : localPlayer.transform.position + localPlayer.transform.forward * 0.75f + Vector3.up;

                itemObject.transform.position = targetPos;
                itemObject.transform.rotation = localPlayer.transform.rotation;

                var rb = itemObject.GetComponent<Rigidbody>();
                if (rb == null) rb = itemObject.GetComponentInChildren<Rigidbody>();
                if (rb != null && camera != null)
                {
                    rb.velocity = (targetPos - itemObject.transform.position) * PULL_SPEED;
                }

                logger.LogInfo("Moved item near local hand/camera.");
            }
            catch (Exception ex)
            {
                logger.LogWarning("MoveItemToLocalHand error: " + ex.Message);
            }
        }

        private Transform FindHandTransform(GameObject player)
        {
            string[] handNames = {
                "RightHand", "rightHand", "Hand_R", "hand_r", "R_Hand", "r_hand",
                "ItemHolder", "itemHolder", "HoldPoint", "holdPoint",
                "GrabPoint", "grabPoint", "WeaponPoint", "weaponPoint"
            };

            foreach (var name in handNames)
            {
                var found = FindChildByExactOrContains(player.transform, name);
                if (found != null) return found;
            }

            return null;
        }

        private Transform FindChildByExactOrContains(Transform parent, string name)
        {
            if (parent.name == name ||
                parent.name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return parent;
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                var found = FindChildByExactOrContains(parent.GetChild(i), name);
                if (found != null) return found;
            }

            return null;
        }
    }
}
