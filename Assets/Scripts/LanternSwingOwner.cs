using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace OdysseyOracle
{
    [DefaultExecutionOrder(-20)]
    public sealed class LanternSwingOwner : MonoBehaviour
    {
        struct Frame { public float3 Pivot, Center, Half; public quaternion Base; }
        struct Pose { public float3 Pivot; public quaternion Rotation, Local; }
        struct Contact { public int Index; public float Distance; }
        [BurstCompile] struct Advance : IJobParallelFor
        {
            public NativeArray<LanternPendulumState> States;
            [ReadOnly] public NativeArray<Frame> Frames;
            public NativeArray<Pose> Poses;
            public float Delta;
            public void Execute(int index)
            {
                var state = States[index]; LanternSwingMath.Step(ref state, Delta); States[index] = state;
                Poses[index] = new Pose { Pivot = Frames[index].Pivot, Rotation = math.mul(Frames[index].Base, quaternion.EulerXYZ(state.X, 0f, state.Z)), Local = quaternion.EulerXYZ(state.X, 0f, state.Z) };
            }
        }
        [BurstCompile] struct Sweep : IJob
        {
            [ReadOnly] public NativeArray<Frame> Frames;
            [ReadOnly] public NativeArray<Pose> Poses;
            public NativeArray<Contact> Result;
            public float3 Origin, Direction;
            public float Distance, Radius;
            public int Count;
            public void Execute()
            {
                var nearest = new Contact { Index = -1, Distance = Distance };
                for (int i = 0; i < Count; i++)
                {
                    var pose = Poses[i]; var frame = Frames[i]; quaternion inverse = math.conjugate(pose.Rotation);
                    float3 origin = math.rotate(inverse, Origin - pose.Pivot) - frame.Center;
                    float3 direction = math.rotate(inverse, Direction), half = frame.Half + Radius;
                    float near = 0f, far = Distance; bool hit = true;
                    for (int axis = 0; axis < 3; axis++)
                    {
                        if (math.abs(direction[axis]) < 1e-7f) { if (math.abs(origin[axis]) > half[axis]) { hit = false; break; } }
                        else {
                            float a = (-half[axis] - origin[axis]) / direction[axis], b = (half[axis] - origin[axis]) / direction[axis];
                            near = math.max(near, math.min(a, b)); far = math.min(far, math.max(a, b));
                            if (near > far) { hit = false; break; }
                        }
                    }
                    // Existing wall/enemy obstruction wins equal-distance ties.
                    if (hit && near < nearest.Distance - .0001f) nearest = new Contact { Index = i, Distance = near };
                }
                Result[0] = nearest;
            }
        }
        readonly Transform[] bodies = new Transform[LanternSwingMath.Capacity];
        NativeArray<LanternPendulumState> states;
        NativeArray<Frame> frames;
        NativeArray<Pose> poses;
        NativeArray<Contact> result;
        DungeonWorld world;
        int count;
        bool wasEligible;
        internal int Count => count;
        void Initialize(DungeonWorld owner)
        {
            world = owner;
            states = new NativeArray<LanternPendulumState>(LanternSwingMath.Capacity, Allocator.Persistent);
            frames = new NativeArray<Frame>(LanternSwingMath.Capacity, Allocator.Persistent);
            poses = new NativeArray<Pose>(LanternSwingMath.Capacity, Allocator.Persistent);
            result = new NativeArray<Contact>(1, Allocator.Persistent);
        }
        internal static bool TryPrepare(Transform root, Transform room, LanternSwingCatalog catalog, ref LanternSwingOwner owner)
        {
            if (catalog == null) return false;
            var source = root.GetComponent<MeshFilter>(); var renderer = root.GetComponent<MeshRenderer>();
            if (source == null || renderer == null || !catalog.Supports(source.sharedMesh)) return false;
            var mount = root.Find("Anchor_HangingLoop");
            if (mount == null || (mount.localPosition - catalog.HingeLocal).sqrMagnitude > 1e-8f ||
                (root.lossyScale - Vector3.one).sqrMagnitude > 1e-8f) return false;
            Material[] shared = renderer.sharedMaterials;
            if (shared.Length != source.sharedMesh.subMeshCount) return false;
            for (int i = 0; i < shared.Length; i++) if (shared[i] == null) return false;
            DungeonWorld world = room.GetComponentInParent<DungeonWorld>(); if (world == null) return false;
            if (owner == null) { owner = room.gameObject.AddComponent<LanternSwingOwner>(); owner.Initialize(world); }
            if (owner.count >= LanternSwingMath.Capacity) return false;
            var support = new GameObject("Fixed original lantern support").transform; support.SetParent(root, false);
            support.gameObject.AddComponent<MeshFilter>().sharedMesh = catalog.FixedSupport;
            var supportRenderer=support.gameObject.AddComponent<MeshRenderer>();CopyRenderer(supportRenderer,renderer,shared);
            var body = new GameObject("Hinged original lantern cage").transform; body.SetParent(root, false); body.localPosition = catalog.HingeLocal;
            body.gameObject.AddComponent<MeshFilter>().sharedMesh = catalog.HangingBody;
            var bodyRenderer=body.gameObject.AddComponent<MeshRenderer>();CopyRenderer(bodyRenderer,renderer,shared);
            renderer.enabled = false;
            int slot = owner.count++; owner.bodies[slot] = body;
            quaternion rotation = root.rotation;
            float3 pivot = root.TransformPoint(catalog.HingeLocal);
            owner.frames[slot] = new Frame { Base = rotation, Pivot = pivot, Center = catalog.HangingBody.bounds.center, Half = catalog.HangingBody.bounds.extents };
            owner.poses[slot] = new Pose { Pivot = pivot, Rotation = rotation, Local = quaternion.identity };
            owner.world.RegisterRoomReactionLantern(slot,root.TransformPoint(catalog.HingeLocal+catalog.HangingBody.bounds.center),supportRenderer,bodyRenderer,shared);
            return true;
        }
        static void CopyRenderer(MeshRenderer target, MeshRenderer source, Material[] shared)
        {
            target.sharedMaterials = shared; target.shadowCastingMode = source.shadowCastingMode;
            target.receiveShadows = source.receiveShadows; target.renderingLayerMask = source.renderingLayerMask;
        }
        bool Eligible(GameSession session) => isActiveAndEnabled && count > 0 && session != null && session.World == world && world != null &&
            world.LanternSwings == this && world.Mode020==WorldMode020.TutorialRefuge && session.HasStarted && SupportsLocalPresentation(session) &&
            !session.Victory && !session.IsDead;
        static bool SupportsLocalPresentation(GameSession session)
        {
            var rooms=session.Rooms;
            if(rooms==null||!rooms.Connected)return !session.CoopMode;
            // No guest can observe divergent state; pending admission invalidates/reset motion.
            // Two-player support requires a separate host-accepted decorative event contract.
            return rooms.IsHost&&rooms.PlayerCount==1&&rooms.CampaignRosterSettled;
        }
        void Update()
        {
            if (!states.IsCreated) return;
            var session = GameSession.Instance;
            if (!Eligible(session)) { if (wasEligible) ResetMotion(); wasEligible = false; return; }
            wasEligible = true;
            if (session.Paused || !session.IsPlaying) return;
            new Advance { States = states, Frames = frames, Poses = poses, Delta = Time.deltaTime }.Schedule(count, 1).Complete();
            ApplyAll();
        }
        internal bool TryHit(Vector3 origin, Vector3 direction, float distance, float radius, float strength, out float contact)
        {
            contact = distance; var session = GameSession.Instance;
            if (!Eligible(session) || session.Paused || session.Combat == null || !session.Combat.Playing ||
                !math.all(math.isfinite((float3)origin)) || !math.all(math.isfinite((float3)direction)) ||
                !LanternSwingMath.Finite(distance) || distance <= 0f || !LanternSwingMath.Finite(radius) || radius < 0f || radius > .6f ||
                direction.sqrMagnitude < .99f || direction.sqrMagnitude > 1.01f) return false;
            new Sweep { Frames = frames, Poses = poses, Result = result, Origin = origin, Direction = direction,
                Distance = distance, Radius = radius, Count = count }.Schedule().Complete();
            Contact hit = result[0]; if (hit.Index < 0) return false;
            float3 local = math.rotate(math.conjugate(frames[hit.Index].Base), (float3)direction);
            var state = states[hit.Index];
            if (!LanternSwingMath.Kick(ref state, local.x, local.z, strength)) return false;
            states[hit.Index] = state; contact = hit.Distance; return true;
        }
        void ApplyAll() { Apply(0); Apply(1); Apply(2); Apply(3); Apply(4); Apply(5); Apply(6); Apply(7); }
        void Apply(int slot)
        {
            if (slot >= count || bodies[slot] == null) return;
            bodies[slot].localRotation = poses[slot].Local;
        }
        void ResetMotion()
        {
            if (!states.IsCreated) return;
            for (int i = 0; i < count; i++) { states[i] = default; poses[i] = new Pose { Pivot = frames[i].Pivot, Rotation = frames[i].Base, Local = quaternion.identity }; }
            ApplyAll();
        }
        void OnDisable() { ResetMotion(); wasEligible = false; }
        void OnDestroy()
        {
            if (world != null && world.LanternSwings == this) world.LanternSwings = null;
            if (states.IsCreated) states.Dispose(); if (frames.IsCreated) frames.Dispose();
            if (poses.IsCreated) poses.Dispose(); if (result.IsCreated) result.Dispose();
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: immutable original wall/root and exact hanging pivot; two cage-only local angles, world-pose numeric query.
// Array Formats: eight Persistent blittable state/frame/pose slots plus one Contact; one room owner and three current cached cage transforms.
// Dependencies/Pointers: split output slots preserve original packed5 material order and cold preparation rejects missing/mismatched shared slots before creating owners. Pure Burst numeric jobs contain no Unity objects; bounded main-thread presentation is unrolled. No player colliders/Rigidbodies/per-shot objects; existing accepted-projectile permissions retained; offline or a settled connected single host only; pending joins/guests/2+members remain static/reset until host hit events exist.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing refuge geometry/world axes and support pivots unchanged; elemental numeric state applies only to the supported refuge.
// Array Formats: Existing owner arrays remain canonical; fixed512-node/8word reaction state supplies bounded scalar presentation gains.
// Dependencies/Pointers: Host accepted single Fire/Earth/Wind shots only; existing Object adapters retain materials/lights/lifetime, no rewards/saves or shrine/portal gameplay mutation.
