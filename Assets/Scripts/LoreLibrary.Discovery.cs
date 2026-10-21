using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OdysseyOracle
{
    public sealed partial class LoreLibrary
    {
        NativeArray<DiscoveryFocusSlot48> discoveryFocus;
        NativeArray<DiscoveryFocusResult16> discoveryResult;
        JobHandle discoveryFence;
        GameObject arenaLibraryRoot, twigPickupRoot, twigPlacedRoot;
        readonly string[] discoveryHints = new string[14];
        Key discoveryInteractKey;
        bool discoveryBindingReady, discoveryModeKnown, discoveryArena, discoveryArenaAttempted;
        public string DiscoveryPlacementStatus { get; private set; } = "";
        void InitializeDiscoveryLibrary()
        {
            try
            {
                discoveryFocus = new NativeArray<DiscoveryFocusSlot48>(16, Allocator.Persistent);
                discoveryResult = new NativeArray<DiscoveryFocusResult16>(1, Allocator.Persistent);
            }
            catch { DisposeDiscoveryLibrary(); throw; }
            RegisterDiscoveryBook(0); RegisterDiscoveryBook(1); RegisterDiscoveryBook(2);
            if (session.World.TryGetRefugeDiscoveryFloors(out Vector3 hidden, out Vector3 pickup, out Vector3 hearth))
            {
                books[3] = CreateBook(DiscoveryCatalog.Title(3), DiscoveryCatalog.AddedBody(3), hidden + Vector3.up * .07f,
                    12f, new Color(.22f, .16f, .06f));
                books[3].Collider.isTrigger = true; // Low floor page cannot obstruct a walking route.
                RegisterDiscoveryBook(3);
                Material wood = CreateMaterial("Original small hearth twig", new Color(.24f, .13f, .055f));
                twigPickupRoot = CreateHearthTwig("Small stick beside reading table", pickup, wood);
                twigPlacedRoot = CreateHearthTwig("Small stick left beside hearth", hearth, wood);
                discoveryFocus[12] = TwigSlot(12, pickup); discoveryFocus[13] = TwigSlot(13, hearth);
                twigPlacedRoot.SetActive(false);
            }
            else DiscoveryPlacementStatus = "The new refuge book and small hearth stick need verified floor support; they were not placed.";
        }
        GameObject CreateHearthTwig(string label, Vector3 floor, Material wood)
        {
            var root = new GameObject(label); root.transform.SetParent(libraryRoot.transform, false); root.transform.position = floor;
            Batch(root.transform, "Original dry twig", wood, new[]
            {
                Part(new Vector3(0,.02f,0), new Vector3(.38f,.04f,.045f)),
                Part(new Vector3(.10f,.024f,.035f), new Vector3(.13f,.032f,.035f), Quaternion.Euler(0,-32f,0))
            });
            return root;
        }
        static DiscoveryFocusSlot48 TwigSlot(uint id, Vector3 floor) => new DiscoveryFocusSlot48
        {
            Center = new float3(floor.x, floor.y + .06f, floor.z), Slot = id,
            HalfExtents = new float3(.23f,.06f,.11f), Rotation = quaternion.identity
        };
        void RegisterDiscoveryBook(int id)
        {
            Book book = books[id]; if (book == null) return;
            Transform t = book.Collider.transform; Vector3 p = t.position; Quaternion q = t.rotation;
            discoveryFocus[id] = new DiscoveryFocusSlot48 { Center = new float3(p.x,p.y,p.z), Slot = (uint)id,
                HalfExtents = new float3(.33f,.08f,.38f), Rotation = new quaternion(q.x,q.y,q.z,q.w) };
        }
        public void PrepareArenaDiscoveryBooks()
        {
            if (discoveryArenaAttempted || session == null || session.World == null || !session.World.IsCoopArena) return;
            discoveryArenaAttempted = true;
            var floors = new Vector3[8];
            if (!session.World.TryGetArenaDiscoveryFloors(floors))
            { DiscoveryPlacementStatus = "Eight arena pages require all eight supported, reserved scenery-clear sites; none were added."; return; }
            arenaLibraryRoot = new GameObject("Eight original arena discovery pages");
            arenaLibraryRoot.transform.SetParent(session.World.transform, false);
            for (int lane = 0; lane < 8; ++lane)
            {
                int id = 4 + lane;
                books[id] = CreateBook(DiscoveryCatalog.Title(id), DiscoveryCatalog.AddedBody(id), floors[lane] + Vector3.up * .07f,
                    90f - lane * 45f, new Color(.16f + (lane % 3)*.035f, .12f, .10f + (lane % 2)*.06f), arenaLibraryRoot.transform);
                books[id].Collider.isTrigger = true;
                RegisterDiscoveryBook(id);
            }
        }
        void UpdateDiscoveryLibrary()
        {
            Hint = "";
            if (session == null || libraryRoot == null || session.World == null || !discoveryFocus.IsCreated) return;
            if(!session.LegacyCombatWorld020)
            {
                if(libraryRoot.activeSelf)libraryRoot.SetActive(false);
                if(arenaLibraryRoot!=null&&arenaLibraryRoot.activeSelf)arenaLibraryRoot.SetActive(false);
                discoveryModeKnown=false;return;
            }
            bool arena = session.World.IsCoopArena;
            if (!discoveryModeKnown || discoveryArena != arena)
            {
                discoveryModeKnown = true; discoveryArena = arena;
                libraryRoot.SetActive(!arena); if (arenaLibraryRoot != null) arenaLibraryRoot.SetActive(arena);
                for (int i = 0; i < 12; ++i)
                { DiscoveryFocusSlot48 s = discoveryFocus[i]; s.Active = books[i] != null && (i < 4 ? !arena : arena) ? 1u : 0; discoveryFocus[i] = s; }
            }
            var progress = session.RefugeActivities;
            bool held = progress != null && progress.HearthStickHeld, placed = progress != null && progress.HearthStickPlaced;
            if (twigPickupRoot != null && twigPickupRoot.activeSelf != (!held && !placed)) twigPickupRoot.SetActive(!held && !placed);
            if (twigPlacedRoot != null && twigPlacedRoot.activeSelf != placed) twigPlacedRoot.SetActive(placed);
            DiscoveryFocusSlot48 pickup = discoveryFocus[12], hearth = discoveryFocus[13];
            pickup.Active = !arena && twigPickupRoot != null && !held && !placed ? 1u : 0;
            hearth.Active = !arena && twigPlacedRoot != null && held && !placed ? 1u : 0;
            discoveryFocus[12] = pickup; discoveryFocus[13] = hearth;
            if (!session.IsPlaying || (session.Revival != null && session.Revival.IsHelping) || session.Player == null || session.Player.ViewCamera == null ||
                AbilityBindings.IsRebinding || GameActionBindings.IsRebinding || Cursor.lockState != CursorLockMode.Locked) return;
            Key key = GameActionBindings.GetKey("Interact");
            if (!discoveryBindingReady || key != discoveryInteractKey)
            {
                discoveryBindingReady = true; discoveryInteractKey = key;
                string prefix = "[" + GameActionBindings.GetLabel("Interact") + "] ";
                for (int i = 0; i < 12; ++i) discoveryHints[i] = prefix + "Read " + DiscoveryCatalog.Title(i);
                discoveryHints[12] = prefix + "Take small hearth stick"; discoveryHints[13] = prefix + "Leave small stick beside hearth";
            }
            Transform eye = session.Player.ViewCamera.transform;
            Vector3 origin = eye.position, direction = eye.forward;
            discoveryFence = new DiscoveryFocusJob { Slots = discoveryFocus, Result = discoveryResult,
                Origin = new float3(origin.x,origin.y,origin.z), Direction = new float3(direction.x,direction.y,direction.z), Range = 2.5f }.Schedule();
            discoveryFence.Complete();
            DiscoveryFocusResult16 selected = discoveryResult[0]; if (selected.Found == 0) return;
            int slot = selected.Slot;
            // One selected main-thread engine adapter, never an Object call in the numeric candidate loop.
            if (Physics.Raycast(origin, direction, out RaycastHit hit, selected.Distance, ~(1<<2), QueryTriggerInteraction.Ignore) &&
                (slot >= 12 || hit.collider != books[slot].Collider)) return;
            Hint = discoveryHints[slot];
            if (Keyboard.current == null || !Keyboard.current[key].wasPressedThisFrame || progress == null) return;
            if (slot < 12)
            { Book book = books[slot]; if (session.Hud != null && session.Hud.TryOpenPhysicalDiscoveryBook(book.Title,book.Body)) progress.RecordPhysicalBookRead(slot); }
            else if (slot == 12) progress.TryPickupHearthStick();
            else progress.TryPlaceHearthStick();
        }
        void DisposeDiscoveryLibrary()
        {
            discoveryFence.Complete();
            if (discoveryResult.IsCreated) discoveryResult.Dispose();
            if (discoveryFocus.IsCreated) discoveryFocus.Dispose();
            if (arenaLibraryRoot != null) Destroy(arenaLibraryRoot);
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Static supported Y-up world book/twig poses; eye pose copied once before scheduled numeric OBB focus.
// Array Formats: Persistent16x48B slots plus1x16B result;12 book refs,14 cached hints; cold bounded12book geometry and2 cosmetic twig roots.
// Dependencies/Pointers: Explicit arena loading seam builds books; last focus fence completes before writes/dispose. World supplies support/reservations; existing modal acceptance precedes local award. No weapon, collision, light, economy or host authority added.
