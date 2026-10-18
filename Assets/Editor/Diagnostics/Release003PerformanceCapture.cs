#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace OdysseyOracle.Diagnostics
{
    // # ARCHITECTURE SNAPSHOT
    // Root-invoked Editor-only observer. Owns four native ProfilerRecorders and
    // one240-frame ring, disposed on finish/cancel/play-exit/reload. No game state,
    // profile files, preferences, physics objects, network messages or spawns are written.
    // Inventory/JSON allocate cold before/after the window; Tick reuses scalar state.
    public static class Release003PerformanceCapture
    {
        [Serializable] public sealed class ActorRow
        {
            public int id, renderers, activeRenderers, activeShadowCasters, activePropertyBlocks;
            public int frustumCandidates, uniqueMaterials, uniqueMeshes, nonTriangleSubmeshes;
            public long activeTriangles, allocatedTriangles;
            public bool alive, targetable, flying;
            public string archetype;
            public Vector3 root;
        }
        [Serializable] public sealed class Inventory
        {
            public int frame, actors, alive, flying, activeRenderers, activeShadowCasters;
            public int activePropertyBlocks, uniqueMaterials, instancingEnabledMaterials;
            public int allocatedProjectileComponents, activeProjectileComponents;
            public int projectileActiveRenderers, projectileActiveShadowCasters;
            public int floorProbeQueries, floorProbeMisses, floorProbeFullEightUnknown;
            public long activeActorTriangles, activeProjectileTriangles;
            public ActorRow[] rows;
            public string floorProbeScope = "One cold8-hit floor-query replay per living ground actor; no private support-cache observation or gameplay query counters.";
            public string rendererScope = "MeshRenderer inventory/triangle instances, NOT actual draw calls or shadow-pass work. FrustumAABB candidates include no occlusion test.";
        }
        [Serializable] public sealed class CounterResult
        {
            public string name;
            public bool available;
            public int samples;
            public double mean = -1;
            public long maximum = -1;
        }
        [Serializable] public sealed class IntervalResult
        {
            public int Count, OverBudgetFrames;
            public double WindowSeconds, AverageMs, P95Ms, MinimumMs, MaximumMs, FramesPerSecond;
            public long IgnoredIntervals;
            internal static IntervalResult Copy(FrameTimeSnapshot value) => new IntervalResult {
                Count = value.Count, OverBudgetFrames = value.OverBudgetFrames, WindowSeconds = value.WindowSeconds,
                AverageMs = value.AverageMs, P95Ms = value.P95Ms, MinimumMs = value.MinimumMs,
                MaximumMs = value.MaximumMs, FramesPerSecond = value.FramesPerSecond, IgnoredIntervals = value.IgnoredIntervals };
        }
        [Serializable] public sealed class Receipt
        {
            public string status, reason, unityVersion, graphicsApi, pipeline, preset;
            public int requestedFrames, sampledFrames, cameraRenderedSamples, frameCap, particleQuality;
            public float resolutionScale;
            public bool bloom, coop, editorIncludesOverhead = true;
            public double startedAt, endedAt;
            public IntervalResult intervals;
            public CounterResult draws, setPass, triangles, gcBytes;
            public Inventory before, after;
            public string scope = "Passive Editor observation of already-spawned wave/current preset. Global engine counters include all cameras/Editor/control overhead. No isolated GPU/gameplay-CPU or browser FPS claim.";
            public string unavailable = "Missing/unrendered counters are unavailable, never zero. Exact gameplay ray/capsule/sphere fallback counts are not publicly exposed; no invented measurements.";
        }
        sealed class Counter
        {
            internal ProfilerRecorder Recorder;
            internal string Name;
            internal int Count;
            internal double Sum;
            internal long Max;
            internal Counter(ProfilerCategory category, string name)
            {
                Name = name;
                try { Recorder = ProfilerRecorder.StartNew(category, name, 1); }
                catch (Exception) { Recorder = default; }
            }
            internal void Observe()
            {
                if (!Recorder.Valid || !Recorder.IsRunning || Recorder.Count == 0) return;
                long value = Recorder.LastValue;
                if (value >= 0) { Count++; Sum += value; Max = Math.Max(Max, value); }
            }
            internal CounterResult Result() => new CounterResult { name = Name, available = Count > 0,
                samples = Count, mean = Count > 0 ? Sum / Count : -1, maximum = Count > 0 ? Max : -1 };
            internal void Dispose() { if (Recorder.Valid) Recorder.Dispose(); Recorder = default; }
        }
        static readonly FrameTimeWindow window = new FrameTimeWindow();
        static GameSession owner;
        static Receipt receipt;
        static Counter draws, passes, triangles, gc;
        static int lastFrame, warmup, startPreset;
        static int lastRenderedFrame, lastSampledRenderFrame;
        static Camera observedCamera;
        static bool running;
        public static bool IsRunning => running;

        public static bool Begin(int frameCount = 120)
        {
            Cancel();
            owner = GameSession.Instance;
            if (!EditorApplication.isPlaying || owner == null || owner.Combat == null || owner.Graphics == null)
                throw new InvalidOperationException("An already-started runtime with Combat/Graphics is required; this observer never starts a game or spawns a wave.");
            GraphicsPreferences g = owner.Graphics;
            receipt = new Receipt { status = "observing", requestedFrames = Mathf.Clamp(frameCount, 30, FrameTimeWindow.Capacity),
                unityVersion = Application.unityVersion, graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                pipeline = GraphicsSettings.currentRenderPipeline != null ? GraphicsSettings.currentRenderPipeline.name : "Built-in",
                preset = g.PresetName, frameCap = g.FrameRateCap, particleQuality = g.ParticleQuality,
                resolutionScale = g.EffectiveResolutionScale, bloom = g.BloomEnabled, coop = owner.CoopMode };
            receipt.before = ReadInventory(owner); // All cold allocations precede recorders/window.
            startPreset = g.Preset; window.Clear(); lastFrame = Time.frameCount; warmup = 2;
            observedCamera = owner.Player != null ? owner.Player.ViewCamera : null;
            lastRenderedFrame = lastSampledRenderFrame = -1;
            draws = new Counter(ProfilerCategory.Render, "Draw Calls Count");
            passes = new Counter(ProfilerCategory.Render, "SetPass Calls Count");
            triangles = new Counter(ProfilerCategory.Render, "Triangles Count");
            gc = new Counter(ProfilerCategory.Memory, "GC Allocated In Frame");
            receipt.startedAt = EditorApplication.timeSinceStartup;
            running = true;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnPlayState;
            AssemblyReloadEvents.beforeAssemblyReload += Cancel;
            RenderPipelineManager.endCameraRendering += OnCameraRendered;
            return true;
        }

        static void Tick()
        {
            if (!running) return;
            if (!EditorApplication.isPlaying || owner == null || owner.Graphics == null || GameSession.Instance != owner)
            { Stop("incomplete", "Runtime owner ended/changed."); return; }
            GraphicsPreferences g = owner.Graphics;
            if (g.Preset != startPreset || g.FrameRateCap != receipt.frameCap || g.ParticleQuality != receipt.particleQuality ||
                g.BloomEnabled != receipt.bloom || Mathf.Abs(g.EffectiveResolutionScale - receipt.resolutionScale) > .0001f || owner.CoopMode != receipt.coop)
            { Stop("incomplete", "Preset/resolution/mode changed during the sample; compare separate stable windows."); return; }
            if (EditorApplication.timeSinceStartup - receipt.startedAt > 30)
            { Stop("incomplete", "30-second observer limit reached before requested distinct game frames."); return; }
            if (Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;
            if (EditorApplication.isPaused) return;
            if (warmup > 0) { warmup--; lastSampledRenderFrame = lastRenderedFrame; return; }
            if (!window.Push(Time.unscaledDeltaTime)) return;
            if (lastRenderedFrame > lastSampledRenderFrame)
            {
                lastSampledRenderFrame = lastRenderedFrame;
                receipt.cameraRenderedSamples++;
                draws.Observe(); passes.Observe(); triangles.Observe(); gc.Observe();
            }
            if (window.Count >= receipt.requestedFrames) Stop("complete", "Requested distinct game-loop frames observed; rendered-counter availability is separate.");
        }
        static void OnCameraRendered(ScriptableRenderContext context, Camera camera)
        { if (running && observedCamera != null && camera == observedCamera) lastRenderedFrame = Time.frameCount; }
        static void OnPlayState(PlayModeStateChange state)
        { if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode) Stop("incomplete", "Play mode ended."); }
        static void Stop(string status, string reason)
        {
            if (!running) return;
            running = false;
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= OnPlayState;
            AssemblyReloadEvents.beforeAssemblyReload -= Cancel;
            RenderPipelineManager.endCameraRendering -= OnCameraRendered;
            receipt.status = status; receipt.reason = reason; receipt.endedAt = EditorApplication.timeSinceStartup;
            receipt.sampledFrames = window.Count;
            receipt.intervals = IntervalResult.Copy(window.Snapshot(receipt.frameCap > 0 ? 1000d / receipt.frameCap : 0d));
            receipt.draws = draws.Result(); receipt.setPass = passes.Result(); receipt.triangles = triangles.Result(); receipt.gcBytes = gc.Result();
            draws.Dispose(); passes.Dispose(); triangles.Dispose(); gc.Dispose();
            draws = passes = triangles = gc = null;
            if (status == "complete" && owner != null && EditorApplication.isPlaying)
            {
                try { receipt.after = ReadInventory(owner); } // After recorders stop.
                catch (Exception ex) { receipt.status = "incomplete"; receipt.reason = "Post-window inventory failed: " + ex.GetType().Name + ": " + ex.Message; }
            }
        }
        public static void Cancel() { Stop("cancelled", "Observer cancelled or managed assembly reloaded."); }
        public static string ResultJson()
        {
            if (running) throw new InvalidOperationException("Observation still running; wait without polling/control work in the sample window, or Cancel explicitly.");
            return receipt == null ? "{\"status\":\"not_started\"}" : JsonUtility.ToJson(receipt, true);
        }
        public static string CaptureLiveWave()
        {
            if (running) throw new InvalidOperationException("Cold inventory must not contaminate an active measurement window.");
            GameSession s = GameSession.Instance;
            if (!EditorApplication.isPlaying || s == null || s.Combat == null) throw new InvalidOperationException("An existing runtime is required.");
            return JsonUtility.ToJson(ReadInventory(s), true);
        }

        static Inventory ReadInventory(GameSession s)
        {
            var result = new Inventory { frame = Time.frameCount };
            IReadOnlyList<OracleEnemy> enemies = s.Combat.ActiveEnemies;
            var rows = new List<ActorRow>(enemies.Count);
            var renderers = new List<MeshRenderer>(32);
            var materials = new List<Material>(8);
            var actorMaterials = new HashSet<Material>(); var actorMeshes = new HashSet<Mesh>();
            var allMaterials = new HashSet<Material>();
            Camera camera = s.Player != null ? s.Player.ViewCamera : null;
            Plane[] planes = camera != null ? GeometryUtility.CalculateFrustumPlanes(camera) : null;
            var floorHits = new RaycastHit[8];
            for (int i = 0; i < enemies.Count; i++)
            {
                OracleEnemy e = enemies[i]; if (e == null) continue;
                var row = new ActorRow { id = e.TargetId, alive = e.Alive, targetable = e.Targetable,
                    flying = e.IsFlying, archetype = e.Archetype.ToString(), root = e.transform.position };
                result.actors++; if (e.Alive) result.alive++; if (e.IsFlying) result.flying++;
                renderers.Clear(); e.GetComponentsInChildren(true, renderers); actorMaterials.Clear(); actorMeshes.Clear();
                for (int r = 0; r < renderers.Count; r++)
                {
                    MeshRenderer renderer = renderers[r]; row.renderers++;
                    MeshFilter filter = renderer.GetComponent<MeshFilter>(); Mesh mesh = filter != null ? filter.sharedMesh : null;
                    long tris = CountTriangles(mesh, out int nonTriangles); row.nonTriangleSubmeshes += nonTriangles;
                    row.allocatedTriangles += tris; if (mesh != null) actorMeshes.Add(mesh);
                    materials.Clear(); renderer.GetSharedMaterials(materials);
                    foreach (Material material in materials) if (material != null)
                    { actorMaterials.Add(material); allMaterials.Add(material); }
                    if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                    row.activeRenderers++; row.activeTriangles += tris;
                    if (renderer.shadowCastingMode != ShadowCastingMode.Off) row.activeShadowCasters++;
                    if (renderer.HasPropertyBlock()) row.activePropertyBlocks++;
                    if (planes != null && GeometryUtility.TestPlanesAABB(planes, renderer.bounds)) row.frustumCandidates++;
                }
                row.uniqueMaterials = actorMaterials.Count; row.uniqueMeshes = actorMeshes.Count;
                result.activeRenderers += row.activeRenderers; result.activeActorTriangles += row.activeTriangles;
                result.activeShadowCasters += row.activeShadowCasters; result.activePropertyBlocks += row.activePropertyBlocks;
                if (e.Alive && !e.IsFlying)
                {
                    result.floorProbeQueries++;
                    int count = Physics.RaycastNonAlloc(e.transform.position + Vector3.up * 1.5f, Vector3.down, floorHits, 3f, ~0, QueryTriggerInteraction.Ignore);
                    if (count == 0) result.floorProbeMisses++;
                    if (count == floorHits.Length) result.floorProbeFullEightUnknown++;
                }
                rows.Add(row);
            }
            result.uniqueMaterials = allMaterials.Count;
            foreach (Material material in allMaterials) if (material.enableInstancing) result.instancingEnabledMaterials++;
            result.rows = rows.ToArray();
            OracleProjectile[] projectiles = UnityEngine.Object.FindObjectsByType<OracleProjectile>(FindObjectsInactive.Include);
            result.allocatedProjectileComponents = projectiles.Length;
            foreach (OracleProjectile p in projectiles)
            {
                if (!p.gameObject.activeInHierarchy) continue; result.activeProjectileComponents++;
                renderers.Clear(); p.GetComponentsInChildren(false, renderers);
                foreach (MeshRenderer renderer in renderers)
                {
                    if (!renderer.enabled) continue;
                    result.projectileActiveRenderers++;
                    if (renderer.shadowCastingMode != ShadowCastingMode.Off) result.projectileActiveShadowCasters++;
                    MeshFilter filter = renderer.GetComponent<MeshFilter>();
                    result.activeProjectileTriangles += CountTriangles(filter != null ? filter.sharedMesh : null, out _);
                }
            }
            return result;
        }
        static long CountTriangles(Mesh mesh, out int other)
        {
            other = 0; if (mesh == null) return 0;
            long count = 0;
            for (int i = 0; i < mesh.subMeshCount; i++)
                if (mesh.GetTopology(i) == MeshTopology.Triangles) count += (long)mesh.GetIndexCount(i) / 3;
                else other++;
            return count;
        }
    }
}
#endif
