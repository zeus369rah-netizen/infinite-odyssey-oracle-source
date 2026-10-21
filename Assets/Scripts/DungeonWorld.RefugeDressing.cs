using System;
using OdysseyOracle.Foundation.World;
using OdysseyOracle.VisualDressing;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace OdysseyOracle
{
    // Explicit cold World adapter; no automatic scene/system registration or gameplay authority.
    public sealed partial class DungeonWorld
    {
        private GrassSeed[] refugeGrass;
        private CandleFlameSeed[] refugeCandles;
        private CrystalEmitterSeed[] refugeCrystals;
        private int refugeGrassCount, refugeCandleCount, refugeCrystalCount;
        private bool refugeCollectDressing;
        private Material refugeSoftMaterial, refugeMoteMaterial, refugeRaykonMaterial;
        private RaykonPresentation refugeRaykon;
        private Transform refugeRaykonInteractionRoot;
        private float refugeRaykonTalkStart;
        private bool refugeRaykonTalking;
        public int RefugeGrassBladeCount { get; private set; }
        public int RefugeCandleFlameCount { get; private set; }
        public int RefugeCrystalEmitterCount { get; private set; }
        public int RefugeUnsupportedGrassCount { get; private set; }
        public bool RefugeRaykonReady => refugeRaykon != null;

        private void BeginRefugeDressing()
        {
            if (tutorialRefuge == null || refugeCollectDressing) return;
            if ((geometryRoot.lossyScale - Vector3.one).sqrMagnitude > 1e-8f || Vector3.Dot(geometryRoot.up, Vector3.up) < .99999f)
                throw new InvalidOperationException("Refuge dressing requires a Y-up, yaw-only, unit-scale geometry root.");
            LaneVignetteRenderer.RefreshWeatherSettings015();
            refugeSoftMaterial = MakeRefugePresentationMaterial("OracleRefugeSoftDressing", "Oracle/Presentation/SoftDressingCandidate");
            refugeMoteMaterial = MakeRefugePresentationMaterial("OracleRefugeCrystalMotes", "Oracle/Presentation/CrystalMotesCandidate");
            refugeRaykonMaterial = MakeRefugePresentationMaterial("OracleRefugeRaykon", "Oracle/Presentation/RaykonRigidCandidate");
            refugeGrass = new GrassSeed[DressingMeshBuilder.MaximumGrassBlades];
            refugeCandles = new CandleFlameSeed[DressingMeshBuilder.MaximumCandleFlames];
            refugeCrystals = new CrystalEmitterSeed[DressingMeshBuilder.MaximumCrystalEmitters];
            refugeGrassCount = refugeCandleCount = refugeCrystalCount = 0;
            refugeCollectDressing = true;
        }

        private Material MakeRefugePresentationMaterial(string resource, string shaderName)
        {
            // Prefer a Resources material asset for build inclusion. Resource assets remain borrowed.
            Material template = Resources.Load<Material>(resource);
            Shader shader = template != null ? template.shader : Shader.Find(shaderName);
            if (shader == null || !shader.isSupported) return null;
            Material owned = template != null ? new Material(template) : new Material(shader);
            owned.name = resource + " World owned";
            resources.Add(owned);
            return owned;
        }

        private bool TryRegisterRefugeGrass(Vector3 requestedFloor, float patchSize)
        {
            if (!refugeCollectDressing || refugeSoftMaterial == null || requestedFloor.y < -.08f || requestedFloor.y > .08f) return false;
            // Fixed 4x6 staggered blades per former low fern patch. No new random-stream consumption.
            for (int row = 0; row < 6; ++row)
            for (int column = 0; column < 4; ++column)
            {
                if (refugeGrassCount == refugeGrass.Length) return true;
                int id = refugeGrassCount;
                float x = (column - 1.5f + ((row & 1) != 0 ? .23f : -.23f)) * patchSize * .15f;
                float z = (row - 2.5f) * patchSize * .12f;
                refugeGrass[id] = new GrassSeed {
                    RootLocal = new Vector3(requestedFloor.x + x, requestedFloor.y, requestedFloor.z + z),
                    YawRadians = id * 2.39996323f, Height = Mathf.Min(.72f,patchSize * (.36f + (id % 5) * .045f)),
                    Width = .022f + (id % 3) * .004f, PhaseRadians = id * .73f,
                    Rgba = (id & 1) == 0 ? 0xff3d783du : 0xff498a4cu };
                ++refugeGrassCount;
            }
            return true;
        }

        private bool TryRegisterRefugeCandle(Vector3 wick, float height, float radius)
        {
            if (!refugeCollectDressing || refugeSoftMaterial == null || height < .03f || height > .3f || radius < .005f || radius > .08f) return false;
            // Wicks borrow the authored wax/brass/fixture support, never the floor Y.
            for (int i = 0; i < refugeCandleCount; ++i)
                if ((refugeCandles[i].WickLocal - wick).sqrMagnitude == 0f) return true;
            if (refugeCandleCount == refugeCandles.Length) return false; // Existing static flame remains.
            int id = refugeCandleCount++;
            refugeCandles[id] = new CandleFlameSeed { WickLocal = wick, Height = height, Radius = radius,
                PhaseRadians = id * 1.61803399f, StableId = (uint)(id + 1), ReservedZero = 0u };
            return true;
        }

        private bool TryRegisterRefugeChandelierFlame(Vector3 center, Vector3 size, float yaw, float pitch, float roll)
        {
            // Only the 12 exact RoomV2 chandelier FlameCore boxes; no generic geometry interception.
            if (yaw != 0f || pitch != 0f || roll != 0f || size.x != .065f || size.y != .11f || size.z != .065f) return false;
            return TryRegisterRefugeCandle(center - Vector3.up * (size.y * .5f), .15f, .0325f);
        }

        private void RegisterRefugeCrystalTip(Vector3 actualTip)
        {
            if (!refugeCollectDressing || refugeMoteMaterial == null || refugeCrystalCount == refugeCrystals.Length) return;
            int id = refugeCrystalCount++;
            refugeCrystals[id] = new CrystalEmitterSeed { RootLocal = actualTip, OrbitRadius = .22f,
                RiseHeight = .8f, ParticleSize = .025f, CyclesPerSecond = .18f, StableId = (uint)(6000 + id) };
        }

        private void FinishRefugeDressing()
        {
            if (!refugeCollectDressing) return;
            refugeCollectDressing = false;
            NativeArray<float4> rectangles = default;
            NativeArray<RefugeFloorQuery32> queries = default;
            NativeArray<ulong> occupancy = default;
            NativeArray<RefugeFloorResult32> results = default;
            JobHandle fence = default;
            bool raykonSupported = false;
            Vector3 raykonFeet = default;
            try
            {
                // Rooms are the exact authored rectangle union, including non-grid-aligned alcove edges.
                rectangles = new NativeArray<float4>(tutorialRefuge.Rooms.Length, Allocator.TempJob);
                for (int i = 0; i < rectangles.Length; ++i)
                {
                    TutorialRoomV2Layout.R2 r = tutorialRefuge.Rooms[i];
                    rectangles[i] = new float4(r.MinU, r.MinV, r.MaxU, r.MaxV);
                }
                int raykonIndex = refugeGrassCount;
                queries = new NativeArray<RefugeFloorQuery32>(refugeGrassCount + 1, Allocator.TempJob);
                for (int i = 0; i < refugeGrassCount; ++i)
                {
                    Vector3 p = refugeGrass[i].RootLocal;
                    queries[i] = FloorQuery(p.x, p.z, .04f, (uint)i);
                }
                Vector3 desired = TutorialRaykonPosition;
                queries[raykonIndex] = FloorQuery(desired.x, desired.z, 1.2f, (uint)raykonIndex);
                fence = RefugeFloorSupportCold.Schedule(rectangles, queries, Allocator.TempJob,
                    out _, out occupancy, out results);
                fence.Complete();
                int accepted = 0;
                for (int i = 0; i < refugeGrassCount; ++i)
                {
                    RefugeFloorResult32 r = results[i];
                    if (r.Status != 1u || r.QueryId != (uint)i) { ++RefugeUnsupportedGrassCount; continue; }
                    GrassSeed seed = refugeGrass[i];
                    seed.RootLocal = new Vector3(r.Position.x, r.Position.y, r.Position.z);
                    refugeGrass[accepted++] = seed;
                }
                refugeGrassCount = accepted;
                RefugeFloorResult32 rr = results[raykonIndex];
                raykonSupported = rr.Status == 1u && rr.QueryId == (uint)raykonIndex;
                if (raykonSupported) raykonFeet = new Vector3(rr.Position.x, rr.Position.y, rr.Position.z);
            }
            finally
            {
                fence.Complete();
                if (results.IsCreated) results.Dispose();
                if (occupancy.IsCreated) occupancy.Dispose();
                if (queries.IsCreated) queries.Dispose();
                if (rectangles.IsCreated) rectangles.Dispose();
            }
            // All Unity Object calls are outside the numeric support/seed loops and after their fence.
            if (refugeGrassCount != 0) AttachRefugeDressingBatch("Supported soft 3D grass", DressingMeshBuilder.Grass(new ArraySegment<GrassSeed>(refugeGrass, 0, refugeGrassCount)), refugeSoftMaterial);
            if (refugeCandleCount != 0){var renderer=AttachRefugeDressingBatch("Batched living candle flames", DressingMeshBuilder.CandleFlames(new ArraySegment<CandleFlameSeed>(refugeCandles, 0, refugeCandleCount)), refugeSoftMaterial);RegisterRoomReactionCandles(renderer,refugeCandles,refugeCandleCount);}
            if (refugeCrystalCount != 0) AttachRefugeDressingBatch("Batched amethyst motes", DressingMeshBuilder.CrystalMotes(new ArraySegment<CrystalEmitterSeed>(refugeCrystals, 0, refugeCrystalCount)), refugeMoteMaterial);
            RefugeGrassBladeCount = refugeGrassCount; RefugeCandleFlameCount = refugeCandleCount; RefugeCrystalEmitterCount = refugeCrystalCount;
            if (raykonSupported && refugeRaykonMaterial != null)
            {
                BuildRefugeRaykon(raykonFeet);
                try{BuildRoomReactionDiscoveries(raykonFeet);}catch(System.InvalidOperationException rejectedSite){Debug.LogWarning("Optional elemental discoveries omitted: "+rejectedSite.Message);}
                try { BuildRefugeAlchemyTable(raykonFeet); }
                catch (System.InvalidOperationException rejectedSite)
                { Debug.LogWarning("Optional alchemy table omitted: " + rejectedSite.Message); }
                try { BuildRefugeRaykonAlcove(raykonFeet); }
                catch (System.InvalidOperationException rejectedSite)
                { Debug.LogWarning("Optional Raykon library alcove omitted: " + rejectedSite.Message); }
            }
            refugeGrass = null; refugeCandles = null; refugeCrystals = null;
        }

        private static RefugeFloorQuery32 FloorQuery(float x, float z, float halfFootprint, uint id)
        {
            return new RefugeFloorQuery32 { Origin = new float3(x, 2f, z), MaximumDistance = 4f,
                HalfFootprintXZ = new float2(halfFootprint), Flags = RefugeFloorSupportMath.CheckFootprintCorners, QueryId = id };
        }

        private MeshRenderer AttachRefugeDressingBatch(string label, Mesh mesh, Material material)
        {
            resources.Add(mesh); // Current World owns the mesh/material until its existing OnDestroy.
            var go = new GameObject(label, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(geometryRoot, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            return renderer;
        }

        private void BuildRefugeRaykon(Vector3 supportedFeet)
        {
            var actor = new GameObject("Raykon supported original presentation");
            actor.transform.SetParent(geometryRoot, false);
            actor.transform.localPosition = supportedFeet; actor.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            try
            {
                RaykonPalette palette = new RaykonPalette { Robe = RefugePaletteColor(Surface.Cloth), Bone = RefugePaletteColor(Surface.Bone),
                    Metal = RefugePaletteColor(Surface.Iron), Rune = RefugePaletteColor(Surface.PortalCrystal) };
                refugeRaykon = RaykonPresentation.Create(actor.transform, refugeRaykonMaterial, palette, actor.transform.position.y);
                RaykonPresentation.ConfigureStaticBody(actor.AddComponent<UnityEngine.BoxCollider>());
                var cauldron = actor.AddComponent<UnityEngine.BoxCollider>();
                Bounds b = RaykonMeshes.CauldronColliderBounds;
                cauldron.center = b.center; cauldron.size = b.size; cauldron.isTrigger = false;
                refugeRaykonInteractionRoot=actor.transform;
            }
            catch { refugeRaykonInteractionRoot=null; refugeRaykon?.Dispose(); refugeRaykon = null; actor.SetActive(false); Destroy(actor); throw; }
        }

        private Color RefugePaletteColor(Surface surface)
        {
            // MakeMaterial bakes tint into pixels, then leaves the shared material multiplier white.
            // Raykon uses vertex colors and therefore needs the original authored palette.
            if (authoredSurfaceTints.TryGetValue(surface, out Color tint)) return tint;
            Material material = batches[surface].Material;
            return material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : material.color;
        }

        public void BeginRaykonTalk()
        {
            if (!IsTutorialRefuge || refugeRaykon == null) return;
            refugeRaykonTalkStart = Time.unscaledTime; refugeRaykonTalking = true; refugeRaykon.TalkBlend = 0f;
            refugeRaykon.NotifyAcceptedTalk009();
        }
        public void EndRaykonTalk()
        {
            refugeRaykonTalking = false;
            if (refugeRaykon != null) refugeRaykon.TalkBlend = 0f;
        }
        private void TickRefugeRaykonTalk(float now)
        {
            // One cached MPB write while the explicit 1.2s presentation transition is active.
            // Vertex positions/normals still use the existing Foundation rigid shader math.
            if (!refugeRaykonTalking || IsCoopArena || refugeRaykon == null) return;
            float progress = Mathf.Clamp01((Time.unscaledTime - refugeRaykonTalkStart) / 1.2f);
            refugeRaykon.TalkBlend = progress;
            if (progress >= 1f) refugeRaykonTalking = false;
        }
        private void DisposeRefugeDressing()
        {
            DisposeRefugeRaykonAlcove();
            refugeRaykonTalking = false; refugeCollectDressing = false; refugeRaykonInteractionRoot=null;
            refugeRaykon?.Dispose(); refugeRaykon = null; // Mesh lease owns only Raykon's three meshes.
            refugeGrass = null; refugeCandles = null; refugeCrystals = null;
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Exact RoomV2 local XZ rectangle union, Y-up metres; floor job supplies support Y. Raykon local+Z faces root yaw90/+X, shader receives world support Y.
// Array Formats: Cold fixed seed arrays grass512/candles256/crystals8, <=3 dressing batch renderers; TempJob rect float4[7]/query32/result32[<=513]/Morton occupancy ulong[1024], completed and disposed before mesh binding.
// Dependencies/Pointers: Existing World owns dressing meshes/materials/children, Raykon lease owns its3 meshes; borrowed Resources assets/catalogs untouched. Foundation.World cold jobs are numeric only. No actor Objects inside jobs, no extra Lights, health/quest/SpatialGrid/target/projectile authority remains existing owners. Full structural/prop-overlap gate is pending root acceptance.
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: World metres/Y-up/XZ; existing station mesh/pivots and canonical host roles remain.
// Array Formats: Bounded cached roots plus optional two-case recipe; no new economy/native hot buffers or quest bits.
// Dependencies/Pointers: World alone binds/disposes the supported original library alcove and existing light factory; root accepts native clearance/visuals.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing refuge geometry/world axes and support pivots unchanged; elemental numeric state applies only to the supported refuge.
// Array Formats: Existing owner arrays remain canonical; fixed512-node/8word reaction state supplies bounded scalar presentation gains.
// Dependencies/Pointers: Host accepted single Fire/Earth/Wind shots only; existing Object adapters retain materials/lights/lifetime, no rewards/saves or shrine/portal gameplay mutation.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing supported Y-up refuge local/unit-yaw and arena-world/chunk anchor contract; no root/floor/actor/pivot change.
// Array Formats: Existing immutable vertex/normal/UV streams and32B grass seeds; shared bounded shader wind,16original3x trees, unchanged counts/resources.
// Dependencies/Pointers: Existing World owns cold materials/meshes; LaneVignetteRenderer publishes weather policy. No new Update/CPU spatial loop/compute dependency; root native/GPU/browser acceptance pending.
