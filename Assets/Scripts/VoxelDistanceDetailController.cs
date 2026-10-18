using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // Renderer/material discovery allocates at explicit world refresh only; <=32 shared private materials.
    // Bounds queries and render scope are main-thread Unity work, never job/Object calls mixed together.
    // No native buffers/jobs owned; scalar visibility math is separate and per-camera flags are restored.
    /// <summary>Local view only. Never disables a GameObject, collider, actor, tower or tutorial material batch.</summary>
    [DisallowMultipleComponent]
    public sealed class VoxelDistanceDetailController : MonoBehaviour
    {
        public const int MaximumPrivateMaterials = 32;
        private const string ChunkPrefix = "Arena chunk ";
        private static readonly int DetailEnabled = Shader.PropertyToID("_OracleLocalDistanceDetailEnabled");
        private static readonly int DetailStart = Shader.PropertyToID("_OracleDetailStart");
        private static readonly int DetailEnd = Shader.PropertyToID("_OracleDetailEnd");
        private static readonly int DetailStrength = Shader.PropertyToID("_OracleDetailStrength");
        private readonly List<Chunk> chunks = new List<Chunk>(256);
        private readonly Dictionary<Material, Material> materials = new Dictionary<Material, Material>(MaximumPrivateMaterials);
        private GameSession session;
        private DungeonWorld world;
        private Shader detailShader;
        private bool initialized, arena, scopeActive;
        private Camera scopedCamera;
        private float savedFar, savedGlobalDetail;
        private FogState savedFog;
        private int blocks = VoxelDistanceMath.DefaultBlocks;
        private float farDetail = VoxelDistanceMath.DefaultFarDetail;

        private sealed class Chunk
        {
            public MeshRenderer Renderer;
            public Material[] Original, Applied;
            public bool Hidden, SavedForcedOff, RadialFog;
        }

        private struct FogState
        {
            public bool Enabled;
            public FogMode Mode;
            public float Start, End, Density;
            public Color Color;
        }

        public int RegisteredChunkRenderers => chunks.Count;
        public int PrivateMaterialCount => materials.Count;
        public int CulledChunkRenderers { get; private set; }
        public bool TextureDetailAvailable => detailShader != null;

        public void Initialize(GameSession owner)
        {
            session = owner;
            if (!initialized)
            {
                initialized = true;
                // Promote the staged shader to Resources/OracleVoxelDistanceLit.shader
                // so a browser build retains it; the fallback supports native authoring.
                detailShader = Resources.Load<Shader>("OracleVoxelDistanceLit") ?? Shader.Find("OdysseyOracle/VoxelDistanceLit");
                if (detailShader != null && !detailShader.isSupported) detailShader = null;
                RenderPipelineManager.beginCameraRendering += BeginCamera;
                RenderPipelineManager.endCameraRendering += EndCamera;
            }
            RefreshWorld();
        }

        public void Configure(int renderDistanceBlocks, float farDetailPixelation)
        {
            EndScope();
            blocks = VoxelDistanceMath.ClampBlocks(renderDistanceBlocks);
            farDetail = VoxelDistanceMath.ClampDetail(farDetailPixelation);
            foreach (Material material in materials.Values) ConfigureMaterial(material);
            // Applying a longer view starts from visible and computes actual bounds next camera.
            for (int i = 0; i < chunks.Count; i++) chunks[i].Hidden = false;
        }

        /// <summary>Call after same-instance arena rebuilds; ordinary mode/world changes are detected without searches.</summary>
        public void RefreshWorld() => RefreshWorld(true);

        public void RefreshWorld(bool forceRediscovery)
        {
            DungeonWorld current = session != null ? session.World : null;
            bool currentArena = current != null && current.IsCoopArena;
            if (!forceRediscovery && world == current && arena == currentArena) return;
            EndScope();
            ReleaseChunks();
            world = current;
            arena = currentArena;
            if (!arena) return; // Tutorial is combined by material, not spatially; never distance-hide it.
            foreach (MeshRenderer renderer in world.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer == null || !renderer.gameObject.name.StartsWith(ChunkPrefix, System.StringComparison.Ordinal)) continue;
                // Only DungeonWorld's direct arena render buckets. Gate assemblies,
                // actors and future similarly named nested objects are not registered.
                if (renderer.transform.parent == null || renderer.transform.parent.parent != world.transform) continue;
                var chunk = new Chunk { Renderer = renderer, Original = renderer.sharedMaterials, RadialFog = detailShader != null };
                chunk.Applied = (Material[])chunk.Original.Clone();
                for (int slot = 0; slot < chunk.Applied.Length; slot++)
                {
                    chunk.Applied[slot] = PrivateMaterial(chunk.Original[slot]);
                    if (chunk.Applied[slot] == null || chunk.Applied[slot].shader != detailShader) chunk.RadialFog = false;
                }
                renderer.sharedMaterials = chunk.Applied;
                chunks.Add(chunk);
            }
        }

        private Material PrivateMaterial(Material source)
        {
            if (source == null || detailShader == null || source.shader == null || source.shader.name != "Universal Render Pipeline/Lit" ||
                source.renderQueue > 2500 || source.HasProperty("_Surface") && source.GetFloat("_Surface") > .5f ||
                source.HasProperty("_AlphaClip") && source.GetFloat("_AlphaClip") > .5f) return source;
            if (materials.TryGetValue(source, out Material existing)) return existing;
            if (materials.Count >= MaximumPrivateMaterials) return source;
            // One clone per shared world material, never renderer.material or texture mutation.
            Material clone = new Material(detailShader) { name = source.name + " (local distance detail)", hideFlags = HideFlags.HideAndDontSave };
            clone.CopyPropertiesFromMaterial(source);
            clone.renderQueue = source.renderQueue;
            ConfigureMaterial(clone);
            materials.Add(source, clone);
            return clone;
        }

        private void ConfigureMaterial(Material material)
        {
            material.SetFloat(DetailStart, VoxelDistanceMath.DetailStart(blocks));
            material.SetFloat(DetailEnd, VoxelDistanceMath.DetailEnd(blocks));
            material.SetFloat(DetailStrength, farDetail);
        }

        private void BeginCamera(ScriptableRenderContext context, Camera camera)
        {
            EndScope(); // Other cameras never inherit the local camera's render flags or fog.
            if (!isActiveAndEnabled || session == null || session.Player == null || camera != session.Player.ViewCamera || camera.cameraType != CameraType.Game) return;
            if (world != session.World || arena != (session.World != null && session.World.IsCoopArena)) RefreshWorld();
            Vector3 position = camera.transform.position;
            if (!VoxelDistanceMath.Finite(position.x) || !VoxelDistanceMath.Finite(position.z)) return;
            savedFar = camera.farClipPlane;
            savedGlobalDetail = Shader.GetGlobalFloat(DetailEnabled);
            savedFog = new FogState { Enabled = RenderSettings.fog, Mode = RenderSettings.fogMode, Start = RenderSettings.fogStartDistance,
                End = RenderSettings.fogEndDistance, Density = RenderSettings.fogDensity, Color = RenderSettings.fogColor };
            scopedCamera = camera;
            scopeActive = true;
            float distance = VoxelDistanceMath.EffectiveReach(blocks, savedFar);
            camera.farClipPlane = Mathf.Max(camera.nearClipPlane + .1f, distance);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = Mathf.Min(VoxelDistanceMath.FogStart(blocks), distance * .68f);
            RenderSettings.fogEndDistance = Mathf.Min(VoxelDistanceMath.FogEnd(blocks), distance * .96f);
            RenderSettings.fogColor = savedFog.Enabled ? savedFog.Color : camera.backgroundColor;
            // Retain authored fog colour, or blend into the existing background.
            Shader.SetGlobalFloat(DetailEnabled, arena ? 1f : 0f);
            CulledChunkRenderers = 0;
            for (int i = 0; i < chunks.Count; i++)
            {
                Chunk chunk = chunks[i];
                if (chunk.Renderer == null) continue;
                Bounds bounds = chunk.Renderer.bounds;
                float squared = VoxelDistanceMath.DistanceSquaredToBoundsXZ(position.x, position.z, bounds.min.x, bounds.min.z, bounds.max.x, bounds.max.z);
                // Radial clipping requires matching radial fog. Unsupported/over-budget
                // materials retain ordinary camera clipping, avoiding an unfogged pop.
                chunk.Hidden = chunk.RadialFog && VoxelDistanceMath.ShouldHide(squared, distance, chunk.Hidden);
                chunk.SavedForcedOff = chunk.Renderer.forceRenderingOff;
                chunk.Renderer.forceRenderingOff = chunk.SavedForcedOff || chunk.Hidden;
                if (chunk.Hidden) CulledChunkRenderers++;
            }
        }

        private void EndCamera(ScriptableRenderContext context, Camera camera) { if (camera == scopedCamera) EndScope(); }

        private void EndScope()
        {
            if (!scopeActive) return;
            for (int i = 0; i < chunks.Count; i++) if (chunks[i].Renderer != null) chunks[i].Renderer.forceRenderingOff = chunks[i].SavedForcedOff;
            if (scopedCamera != null) scopedCamera.farClipPlane = savedFar;
            RenderSettings.fog = savedFog.Enabled;
            RenderSettings.fogMode = savedFog.Mode;
            RenderSettings.fogStartDistance = savedFog.Start;
            RenderSettings.fogEndDistance = savedFog.End;
            RenderSettings.fogDensity = savedFog.Density;
            RenderSettings.fogColor = savedFog.Color;
            Shader.SetGlobalFloat(DetailEnabled, savedGlobalDetail);
            scopedCamera = null;
            scopeActive = false;
        }

        private void ReleaseChunks()
        {
            for (int i = 0; i < chunks.Count; i++)
            {
                Chunk chunk = chunks[i];
                if (chunk.Renderer == null) continue;
                Material[] current = chunk.Renderer.sharedMaterials;
                int count = Mathf.Min(current.Length, chunk.Original.Length);
                for (int slot = 0; slot < count; slot++) if (current[slot] == chunk.Applied[slot]) current[slot] = chunk.Original[slot];
                chunk.Renderer.sharedMaterials = current;
            }
            chunks.Clear();
            foreach (Material material in materials.Values) if (material != null) Destroy(material);
            materials.Clear();
            CulledChunkRenderers = 0;
        }

        private void OnEnable() { if (initialized) RefreshWorld(); }
        private void OnDisable() { EndScope(); ReleaseChunks(); }
        private void OnDestroy()
        {
            RenderPipelineManager.beginCameraRendering -= BeginCamera;
            RenderPipelineManager.endCameraRendering -= EndCamera;
            EndScope();
            ReleaseChunks();
        }
    }
}
