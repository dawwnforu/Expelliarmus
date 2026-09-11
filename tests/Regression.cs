// Runs the production behaviour with a small deterministic Unity/Photon stand-in.
// Network handlers model the inspected PEAK methods; this is not a multiplayer test.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Expelliarmus;
using Photon.Pun;
using UnityEngine;

class Regression
{
    static int checks;
    static ExpelliarmusBehaviour mod;
    static bool delayDrop, denyPickup, keepOwnerView;
    static Action beforeDrop;
    static Character target;
    static Item targetItem, ground;
    static int pickups, drops, freezes, releases;
    static bool kinematic;
    static void Check(bool value, string name)
    {
        if (!value) throw new Exception(name);
        checks++;
    }
    static object Call(string name, params object[] args)
    {
        return typeof(ExpelliarmusBehaviour).GetMethod(name, BindingFlags.NonPublic |
            BindingFlags.Static | BindingFlags.Instance).Invoke(mod, args);
    }
    static void Set(string name, object value)
    {
        typeof(ExpelliarmusBehaviour).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(mod, value);
    }
    static Character MakeCharacter(bool local)
    {
        var c = new Character { IsLocal = local };
        c.refs.items.character = c;
        c.photonView = new PhotonView { Character = c };
        c.refs.items.photonView = c.photonView;
        return c;
    }
    static Item Hold(Character c, byte slot, ushort kind)
    {
        var item = new Item { itemID = kind, data = new ItemInstanceData { guid = Guid.NewGuid() },
            itemState = ItemState.Held, trueHolderCharacter = c };
        item.photonView = new PhotonView { Item = item, IsRoomView = false };
        c.player.GetItemSlot(slot).prefab = item;
        c.player.GetItemSlot(slot).data = item.data;
        c.refs.items.currentSelectedSlot = Zorro.Core.Optionable<byte>.Some(slot);
        c.data.currentItem = item;
        Item.ALL_ITEMS.Add(item);
        return item;
    }
    static void Reset()
    {
        Time.unscaledTime = 0;
        Item.ALL_ITEMS.Clear(); PhotonView.Views.Clear();
        pickups = drops = 0; delayDrop = denyPickup = keepOwnerView = false;
        freezes = releases = 0; kinematic = false;
        beforeDrop = null; ground = null;
        mod = new ExpelliarmusBehaviour();
        Set("logger", new BepInEx.Logging.ManualLogSource());
        Character.localCharacter = MakeCharacter(true);
        target = MakeCharacter(false);
        targetItem = Hold(target, 1, 7);
        PhotonView.Dispatch = Rpc;
        Physics.hits = new[] { new RaycastHit { collider = new Collider { Parent = target }, distance = 1 } };
        GUIManager.instance = new GUIManager(); PhotonNetwork.InRoom = true;
    }
    static void Rpc(PhotonView view, string method, RpcTarget destination, object[] args)
    {
        if (method == "DropItemFromSlotRPC")
        {
            Check(destination == RpcTarget.MasterClient, "drop must run on host"); drops++;
            if (delayDrop) return;
            if (beforeDrop != null) { var action = beforeDrop; beforeDrop = null; action(); }
            var slot = view.Character.player.GetItemSlot((byte)args[0]);
            // PEAK uses the host's CURRENT slot data, not data supplied by the caster.
            if (slot.IsEmpty()) return;
            ground = new Item { data = slot.data, itemID = slot.prefab.itemID, itemState = ItemState.Ground };
            ground.transform.position = (Vector3)args[1];
            ground.photonView = new PhotonView { Item = ground, IsRoomView = true };
            Item.ALL_ITEMS.Add(ground);
            slot.prefab = null; // PEAK EmptyOut retains data. Empty slots must be ignored.
        }
        else if (method == "SetKinematicRPC")
        {
            Check(destination == RpcTarget.AllViaServer && view.IsRoomView,
                "handoff physics is synchronized to unmodded clients");
            kinematic = (bool)args[0];
            if (kinematic) freezes++; else releases++;
        }
        else if (method == "EquipSlotRpc")
        {
            Check(destination == RpcTarget.AllViaServer, "remote release uses native ordered broadcast");
            if (!keepOwnerView) view.Character.refs.items.EquipSlot(Zorro.Core.Optionable<byte>.None);
        }
        else if (method == "RequestPickup")
        {
            Check(destination == RpcTarget.MasterClient && view.IsRoomView, "pickup must use a room-owned object");
            Check(view.Item.itemState == ItemState.Ground, "must never pick up a held view");
            Check(PhotonNetwork.GetPhotonView(targetItem.photonView.ViewID) == null, "wait for owner's held-view destruction");
            Check(kinematic, "handoff must stay suspended until pickup");
            pickups++;
            if (denyPickup) return;
            var receiver = ((PhotonView)args[0]).Character;
            var slot = receiver.player.FirstEmpty();
            if (slot == null) return;
            slot.prefab = view.Item; slot.data = view.Item.data;
            PhotonView.Views.Remove(view.ViewID); Item.ALL_ITEMS.Remove(view.Item);
        }
        else throw new Exception("Unexpected RPC: " + method);
    }
    static void Run(IEnumerator action)
    {
        int steps = 0;
        while (action.MoveNext())
        {
            if (++steps > 500) throw new Exception("coroutine did not finish");
            var nested = action.Current as IEnumerator;
            if (nested != null) Run(nested);
            else Time.unscaledTime += 0.05f;
        }
    }
    static void Transfer()
    {
        Run((IEnumerator)Call("Transfer", Character.localCharacter, target, targetItem));
    }
    static int Main()
    {
        Reset();
        var first = new Item { itemID = 7, data = new ItemInstanceData { guid = Guid.NewGuid() } };
        target.player.itemSlots[0].prefab = first; target.player.itemSlots[0].data = first.data;
        Check(Call("HeldSlot", target, targetItem) == target.player.itemSlots[1], "same type must use selected GUID");
        Transfer();
        Check(pickups == 1 && target.player.itemSlots[0].prefab == first, "same-type first slot untouched");
        Check(target.player.itemSlots[1].IsEmpty() && target.data.currentItem == null, "source released");
        Check((bool)Call("InventoryContains", Character.localCharacter.player, targetItem.data.guid), "destination received exact instance");
        Check(!(bool)Call("InventoryContains", target.player, targetItem.data.guid), "stale empty-slot data is not ownership");
        Check(freezes == 1 && releases == 0, "successful handoff freezes once and never touches destroyed pickup");
        Check(Math.Abs(ground.transform.position.z - 0.6f) < 0.001f &&
            Math.Abs(ground.transform.position.y + 0.25f) < 0.001f,
            "transfer spawns in front of receiving camera, not at teammate feet");

        Reset();
        var pending = (IEnumerator)Call("DropHeldItem", target, targetItem, (Vector3?)new Vector3(0, 1, 0));
        keepOwnerView = true; pending.MoveNext(); Call("OnDisable");
        Check(freezes == 1 && releases == 1 && !kinematic, "disabling mod releases pending handoff");

        Reset(); target.refs.items.currentSelectedSlot = Zorro.Core.Optionable<byte>.Some(0);
        Check(Call("HeldSlot", target, targetItem) == null, "selection mismatch must cancel");
        Reset(); target.player.itemSlots[1].data = new ItemInstanceData { guid = Guid.NewGuid() };
        Check(Call("HeldSlot", target, targetItem) == null, "GUID mismatch must cancel");
        Reset(); target.player.itemSlots[1].prefab = null;
        Check(Call("HeldSlot", target, targetItem) == null, "empty slot cannot be stolen again");

        Reset(); delayDrop = true; Transfer();
        Check(pickups == 0 && target.data.currentItem == targetItem && !target.player.itemSlots[1].IsEmpty(), "drop timeout preserves source");
        Reset(); keepOwnerView = true; Transfer();
        Check(pickups == 0 && ground != null && Item.ALL_ITEMS.Contains(ground), "release timeout leaves ground item and forbids pickup");
        Check(releases == 1 && !kinematic, "owner timeout restores gravity");
        Reset(); denyPickup = true; Transfer();
        Check(pickups == 1 && Item.ALL_ITEMS.Contains(ground), "denial retains ground item; no retry/copy");
        Check(releases == 1 && !kinematic, "pickup timeout restores gravity");

        Reset(); var replacement = new Item { itemID = 11, data = new ItemInstanceData { guid = Guid.NewGuid() } };
        beforeDrop = delegate { target.player.itemSlots[1].prefab = replacement; target.player.itemSlots[1].data = replacement.data; };
        Transfer();
        Check(pickups == 0 && ground.data.guid == replacement.data.guid && ground.itemID == 11,
            "host slot changed: never mix cached data with a different prefab or pick up wrong GUID");

        Reset(); var localOld = Hold(Character.localCharacter, 2, 9); Transfer();
        Check(drops == 2 && pickups == 1, "own held item is dropped before target transfer");
        Check(Call("FindGroundItem", localOld.data.guid) != null, "own previous item remains on ground");
        Check(freezes == 1, "own dropped item must not be suspended");

        Reset(); Character.localCharacter.input.useSecondaryIsPressed = true;
        GUIManager.instance.windowBlockingInput = true; Call("Update");
        Check(drops == 0, "menu blocks steal");
        GUIManager.instance.windowBlockingInput = false; GUIManager.instance.wheelActive = true; Call("Update");
        Check(drops == 0, "wheel blocks steal");
        GUIManager.instance.wheelActive = false; Character.localCharacter.data.fullyConscious = false; Call("Update");
        Check(drops == 0, "unconscious player cannot steal");
        Character.localCharacter.data.fullyConscious = true; Character.localCharacter.data.isClimbingAnything = true; Call("Update");
        Check(drops == 0, "climbing player cannot steal");

        Reset(); Character.localCharacter.input.useSecondaryIsPressed = true;
        Physics.hits = new RaycastHit[0]; Call("Update");
        Check(drops == 0, "reaching before aim does not consume attempt");
        Physics.hits = new[] { new RaycastHit { collider = new Collider { Parent = target }, distance = 1 } };
        Time.unscaledTime += 0.2f; Call("Update");
        Check(pickups == 1, "aiming while already reaching triggers steal");
        Hold(target, 0, 12); Time.unscaledTime += 1; Call("Update");
        Check(pickups == 1, "one transfer per held press");

        Reset(); Set("busy", true); Character.localCharacter.input.useSecondaryIsPressed = true; Call("Update");
        Check(drops == 0, "no overlapping transaction");
        Reset(); var held = targetItem;
        Check(!(bool)Call("IsGroundItem", held, held.data.guid), "held item is never pickup candidate");
        held.itemState = ItemState.Ground; held.trueHolderCharacter = null;
        Check(!(bool)Call("IsGroundItem", held, held.data.guid), "player-owned ground view is excluded");
        Console.WriteLine("PASS: " + checks + " regression assertions (production behaviour, simulated RPCs).");
        return 0;
    }
    public static void RunCoroutine(IEnumerator action) { Run(action); }
}

namespace UnityEngine
{
    public class Object { public static void DontDestroyOnLoad(Object o) {} public static void Destroy(Object o) {} }
    public class GameObject : Object { public GameObject(string name) {} public T AddComponent<T>() where T : new() { return new T(); } }
    public class Component : Object
    {
        public Transform transform = new Transform(); public GameObject gameObject;
        public PhotonView photonView;
    }
    public class MonoBehaviour : Component { public void StartCoroutine(IEnumerator a) { Regression.RunCoroutine(a); } public void StopAllCoroutines() {} }
    public struct Quaternion {}
    public class Transform { public Vector3 position; public Quaternion rotation; public Vector3 forward = new Vector3(0, 0, 1); }
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float a, float b, float c) { x = a; y = b; z = c; }
        public static Vector3 down = new Vector3(0, -1, 0); public float magnitude { get { return 1; } }
        public static float Angle(Vector3 a, Vector3 b) { return 0; }
        public static Vector3 operator +(Vector3 a, Vector3 b) { return new Vector3(a.x+b.x,a.y+b.y,a.z+b.z); }
        public static Vector3 operator -(Vector3 a, Vector3 b) { return new Vector3(a.x-b.x,a.y-b.y,a.z-b.z); }
        public static Vector3 operator *(Vector3 a, float b) { return new Vector3(a.x*b,a.y*b,a.z*b); }
    }
    public class Camera : Component { public static Camera main = new Camera(); }
    public class Collider { public object Parent; public T GetComponentInParent<T>() where T : class { return Parent as T; } }
    public struct Ray { public Ray(Vector3 a, Vector3 b) {} }
    public struct RaycastHit { public Collider collider; public float distance; }
    public enum QueryTriggerInteraction { Ignore }
    public static class Physics { public static RaycastHit[] hits; public static RaycastHit[] RaycastAll(Ray r, float d, int m, QueryTriggerInteraction q) { return hits; } }
    public static class Time { public static float unscaledTime; }
    public class WaitForSecondsRealtime { public WaitForSecondsRealtime(float f) {} }
}
namespace BepInEx.Logging { public class ManualLogSource { public void LogInfo(string s) {} public void LogWarning(string s) {} } }
namespace Zorro.Core
{
    public struct Optionable<T>
    {
        public T Value; public bool IsNone;
        public static Optionable<T> None { get { return new Optionable<T> { IsNone = true }; } }
        public static Optionable<T> Some(T v) { return new Optionable<T> { Value = v }; }
    }
}
namespace Photon.Pun
{
    public enum RpcTarget { MasterClient, AllViaServer }
    public class PhotonView
    {
        static int next;
        public static Dictionary<int, PhotonView> Views = new Dictionary<int, PhotonView>();
        public static Action<PhotonView, string, RpcTarget, object[]> Dispatch;
        public int ViewID; public bool IsRoomView; public Character Character; public Item Item;
        public PhotonView() { ViewID = ++next; Views.Add(ViewID, this); }
        public void RPC(string method, RpcTarget target, params object[] args) { Dispatch(this, method, target, args); }
    }
    public static class PhotonNetwork
    {
        public static bool InRoom;
        public static PhotonView GetPhotonView(int id) { PhotonView v; return PhotonView.Views.TryGetValue(id, out v) ? v : null; }
    }
}
public enum ItemState { Ground, Held }
public class ItemInstanceData { public Guid guid; }
public class UIData { public bool canDrop = true; }
public class Item : Component
{
    public static List<Item> ALL_ITEMS = new List<Item>();
    public ushort itemID; public ItemInstanceData data; public ItemState itemState;
    public Character trueHolderCharacter; public UIData UIData = new UIData();
}
public class ItemSlot
{
    public byte itemSlotID; public Item prefab; public ItemInstanceData data;
    public bool IsEmpty() { return prefab == null; }
}
public class Player
{
    public ItemSlot[] itemSlots = { new ItemSlot { itemSlotID = 0 }, new ItemSlot { itemSlotID = 1 }, new ItemSlot { itemSlotID = 2 } };
    ItemSlot backpack = new ItemSlot { itemSlotID = 3 }, temp = new ItemSlot { itemSlotID = 250 };
    public ItemSlot GetItemSlot(byte id) { return id == 3 ? backpack : id == 250 ? temp : itemSlots[id]; }
    public ItemSlot FirstEmpty() { foreach (var s in itemSlots) if (s.IsEmpty()) return s; return temp.IsEmpty() ? temp : null; }
    public bool HasEmptySlot(ushort id) { return FirstEmpty() != null; }
}
public class CharacterData { public Item currentItem; public bool fullyConscious = true, isClimbingAnything; }
public class CharacterInput { public bool useSecondaryIsPressed; }
public class CharacterRefs { public CharacterItems items = new CharacterItems(); }
public class Character : Component
{
    public static Character localCharacter;
    public bool IsLocal; public CharacterData data = new CharacterData();
    public CharacterRefs refs = new CharacterRefs(); public CharacterInput input = new CharacterInput(); public Player player = new Player();
}
public class CharacterItems : Component
{
    public Character character; public Zorro.Core.Optionable<byte> currentSelectedSlot;
    public void EquipSlot(Zorro.Core.Optionable<byte> slot)
    {
        if (character.data.currentItem != null)
        {
            PhotonView.Views.Remove(character.data.currentItem.photonView.ViewID);
            Item.ALL_ITEMS.Remove(character.data.currentItem);
        }
        character.data.currentItem = null; currentSelectedSlot = slot;
    }
}
public class GUIManager { public static GUIManager instance = new GUIManager(); public bool windowBlockingInput, wheelActive; }
