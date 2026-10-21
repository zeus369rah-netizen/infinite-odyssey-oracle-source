using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// PSEUDOCODE
// 1. A serialized pipeline reference is only a hint; establish private ownership by cloning the active quality asset.
// 2. Mutate that clone only while it is still bound to the same quality level that this component acquired.
// 3. On release, restore only quality slots that still point to this clone; preserve foreign replacements.
// 4. For each light, remember its baseline and last value written; restore only while the live value is still ours.

namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // Main thread may own one private URP clone and companion render/telemetry components.
    // Pipeline writes require the clone to remain bound at its recorded quality level.
    // Preset/distance policies are pure value math; Unity Object calls stay outside sampling loops.
    // No jobs/native containers are owned here; steady adaptive sampling reuses scalar state.
    /// <summary>Saved PC presets. Fixed scale by default; optional adaptive scale uses frame intervals, not GPU timing.</summary>
    [DisallowMultipleComponent]
    public sealed partial class GraphicsPreferences : MonoBehaviour
    {
        private const string PresetKey = "Oracle.Graphics.Preset";
        private const string ScaleKey = "Oracle.Graphics.Scale";
        private const string BloomKey = "Oracle.Graphics.Bloom";
        private const string ShadowKey020 = "Oracle.Graphics.SoftShadows020";
        private static readonly int WindowShadowId020 = Shader.PropertyToID("_OracleSoftWindowShadows020");
        private float originalWindowShadow020;
        private const string AutoKey = "Oracle.Graphics.AutoResolution";
        private const string BrightnessKey = "Oracle.Graphics.Brightness";
        private const string ParticleKey = "Oracle.Graphics.Particles";
        private const string FrameCapKey = "Oracle.Graphics.FrameCap";
        private const string PresetSchemaKey = "Oracle.Graphics.PresetSchema";
        private const string RenderDistanceKey = "Oracle.Graphics.RenderDistanceBlocks";
        private const string FarDetailKey = "Oracle.Graphics.FarDetailPixelation";
        private const float MinimumScale = .5f, ScaleStep = .05f, AdjustmentInterval = 2f;
        private readonly Dictionary<Light, LightShadowState020> lightShadowStates020 = new Dictionary<Light, LightShadowState020>();
        private readonly Dictionary<Volume, VolumeProfile> ownedProfiles = new Dictionary<Volume, VolumeProfile>();
        private readonly Dictionary<ColorAdjustments, ExposureDefaults> exposures = new Dictionary<ColorAdjustments, ExposureDefaults>();
        private readonly Dictionary<ParticleSystem, ParticleDefaults> particleDefaults = new Dictionary<ParticleSystem, ParticleDefaults>();
        private readonly List<Camera> cameras = new List<Camera>(8);
        private PortalAnomaly[] portals;
        // Serialized Unity references survive a managed reload until OnDisable
        // restores the quality override. Never retain a destroyed private clone.
        [SerializeField] private UniversalRenderPipelineAsset originalQualityPipeline, runtimePipeline;
        [SerializeField] private int runtimeQualityLevel = -1;
        [SerializeField] private bool ownsRuntimePipeline;
        private const string RuntimePipelineSuffix = " (Oracle runtime quality)";
        private GameSession session;
        private DungeonWorld lastWorld;
        private CampfireOpening lastOpening;
        private bool initialized, lastCoopArena, refreshRequested, applicationPaused;
        private WorldMode020 lastWorldMode020;
        private int originalFrameRate, originalVsync;
        private float frameCostEma, warmupRemaining, adjustmentRemaining, stableBudgetSeconds;
        private VoxelDistanceDetailController distanceDetail;
        private bool ownsDistanceDetail, ownsTelemetry;

        private struct ExposureDefaults { public float Value; public bool Override; }
        private struct ParticleDefaults { public float TimeRate, DistanceRate; public int Maximum; }
        private struct LightShadowState020
        {
            public LightShadows Baseline;
            public LightShadows LastWrite;
            public bool HasLastWrite;
        }

        public const int PresetCount = GraphicsPresetCatalog.Count;
        public static readonly string[] PresetNames = GraphicsPresetCatalog.Names;
        public string PresetName => GraphicsPresetCatalog.Get(Preset).Name;
        public bool PresetPreservesResolutionPolicy => Preset == UltraBetaPreset022;
        public bool PresetIsCustomized => (!PresetPreservesResolutionPolicy &&
            (!Mathf.Approximately(ResolutionScale, PresetScale(Preset)) || AutoResolutionEnabled)) ||
            BloomEnabled != GraphicsPresetCatalog.Get(Preset).Bloom ||
            ParticleQuality != GraphicsPresetCatalog.Get(Preset).ParticleQuality || ShadowQuality020 != GraphicsShadowQuality020.DefaultForPreset(Preset);
        public int Preset { get; private set; } = GraphicsPresetCatalog.DefaultPreset;
        public float ResolutionScale { get; private set; } = 1f;
        public float EffectiveResolutionScale { get; private set; } = 1f;
        public bool AutoResolutionEnabled { get; private set; }
        public bool BloomEnabled { get; private set; } = true;
        // Compatibility getter: existing consumers use this as the shadow-enabled policy.
        public bool SoftShadowsEnabled => ShadowQuality020 > 0;
        public int ActiveShadowLights020 { get; private set; }
        public float Brightness { get; private set; } = 1f;
        public int ParticleQuality { get; private set; } = 2;
        public int FrameRateCap { get; private set; } = 60;
        public int AdaptiveTargetFrameRate => FrameRateCap == 30 ? 30 : 60;
        public float ShadowDistance => runtimePipeline != null ? runtimePipeline.shadowDistance : 0f;
        public int ShadowMapResolution => runtimePipeline != null ? runtimePipeline.mainLightShadowmapResolution : 0;
        public int AdditionalLightsPerObject => runtimePipeline != null ? runtimePipeline.maxAdditionalLightsCount : 0;
        public bool OpaqueTextureEnabled => runtimePipeline != null && runtimePipeline.supportsCameraOpaqueTexture;
        public float EffectiveParticleFactor => GraphicsPresetCatalog.ParticleFactor(Preset, ParticleQuality);
        public const float MetresPerBlock = VoxelDistanceMath.BlockMetres;
        public int RenderDistanceBlocks { get; private set; } = VoxelDistanceMath.DefaultBlocks;
        public float RenderDistanceMetres => RenderDistanceBlocks * MetresPerBlock;
        public float EffectiveRenderDistanceMetres => VoxelDistanceMath.EffectiveReach(RenderDistanceBlocks,
            session != null && session.World != null && session.World.Mode020==WorldMode020.CampaignHub ? session.World.CampaignHubFarClip020 : session != null && session.World != null && session.World.IsCoopArena ? GraphicsPresetCatalog.Get(Preset).ArenaFarClip : 60f);
        public float FarDetailPixelation { get; private set; } = VoxelDistanceMath.DefaultFarDetail;
        public bool TextureDetailAvailable => distanceDetail != null && distanceDetail.TextureDetailAvailable;
        public int RegisteredChunkRenderers => distanceDetail != null ? distanceDetail.RegisteredChunkRenderers : 0;
        public int CulledChunkRenderers => distanceDetail != null ? distanceDetail.CulledChunkRenderers : 0;
        public int PrivateDistanceMaterialCount => distanceDetail != null ? distanceDetail.PrivateMaterialCount : 0;
        public bool UsingFixedResolution => !AutoResolutionEnabled;
        public string ResolutionMode => AutoResolutionEnabled ? "Adaptive scene scale" : "Fixed scene scale";
        public OraclePerformanceTelemetry PerformanceTelemetry { get; private set; }
        public bool PerformanceOverlayVisible => PerformanceTelemetry != null && PerformanceTelemetry.OverlayVisible;
        public string PerformanceCompactText => PerformanceTelemetry != null ? PerformanceTelemetry.CompactText : "";
        public string PerformanceDetailsText => PerformanceTelemetry != null ? PerformanceTelemetry.DetailsText : "";

        public void Initialize(GameSession owner)
        {
            session = owner;
            if (initialized) return;
            initialized = true;
            originalFrameRate = Application.targetFrameRate;
            originalVsync = QualitySettings.vSyncCount;
            originalWindowShadow020 = Shader.GetGlobalFloat(WindowShadowId020);
            CaptureReflectionPolicy020();
            AcquireRuntimePipeline();
            int schema = PlayerPrefs.GetInt(PresetSchemaKey, 0);
            Preset = GraphicsPresetCatalog.MigratePreset(PlayerPrefs.GetInt(PresetKey, 0), PlayerPrefs.HasKey(PresetKey), schema);
            GraphicsPresetSpec spec = GraphicsPresetCatalog.Get(Preset);
            ResolutionScale = ValidFloat(PlayerPrefs.GetFloat(ScaleKey, PresetScale(Preset)), PresetScale(Preset), MinimumScale, 1f);
            BloomEnabled = PlayerPrefs.GetInt(BloomKey, spec.Bloom ? 1 : 0) != 0;
            bool shadowPreferencesMigrated020 = LoadShadowWeatherPreferences020();
            LoadReflectionPreference020();
            // One migration turns the old default-on policy off. Later explicit
            // adaptive opt-in is preserved. Regular presets turn it off;
            // Ultra (Beta) keeps the user's resolution policy.
            AutoResolutionEnabled = GraphicsPresetCatalog.ResolveAdaptiveDefault(PlayerPrefs.GetInt(AutoKey, 0) != 0, schema);
            Brightness = ValidFloat(PlayerPrefs.GetFloat(BrightnessKey, 1f), 1f, .7f, 1.5f);
            ParticleQuality = Mathf.Clamp(PlayerPrefs.GetInt(ParticleKey, spec.ParticleQuality), 0, 2);
            FrameRateCap = ValidFrameCap(PlayerPrefs.GetInt(FrameCapKey, 60));
            RenderDistanceBlocks = VoxelDistanceMath.ClampBlocks(PlayerPrefs.GetInt(RenderDistanceKey, VoxelDistanceMath.DefaultBlocks));
            FarDetailPixelation = VoxelDistanceMath.ClampDetail(PlayerPrefs.GetFloat(FarDetailKey, VoxelDistanceMath.DefaultFarDetail));
            if (schema < GraphicsPresetCatalog.SchemaVersion || shadowPreferencesMigrated020) Save();
            RenderPipelineManager.beginCameraRendering += BeforeCamera;
            SceneManager.sceneLoaded += SceneLoaded;
            // Quality's new-camera baseline must precede the distance render scope.
            distanceDetail = GetComponent<VoxelDistanceDetailController>();
            ownsDistanceDetail = distanceDetail == null;
            if (ownsDistanceDetail) distanceDetail = gameObject.AddComponent<VoxelDistanceDetailController>();
            distanceDetail.Initialize(owner);
            distanceDetail.Configure(RenderDistanceBlocks, FarDetailPixelation);
            PerformanceTelemetry = GetComponent<OraclePerformanceTelemetry>();
            ownsTelemetry = PerformanceTelemetry == null;
            if (ownsTelemetry) PerformanceTelemetry = gameObject.AddComponent<OraclePerformanceTelemetry>();
            PerformanceTelemetry.Initialize(owner);
            Apply(true);
        }

        public void SetPreset(int preset)
        {
            Preset = GraphicsPresetCatalog.Clamp(preset);
            bool preserveScaleCeiling = PresetPreservesResolutionPolicy;
            GraphicsPresetSpec spec = GraphicsPresetCatalog.Get(Preset);
            if (!preserveScaleCeiling) ResolutionScale = PresetScale(Preset);
            BloomEnabled = spec.Bloom;
            ShadowQuality020 = GraphicsShadowQuality020.DefaultForPreset(Preset);
            ParticleQuality = spec.ParticleQuality;
            if (!preserveScaleCeiling) AutoResolutionEnabled = false;
            Save();
            Apply(!preserveScaleCeiling);
        }

        public void SetResolutionScale(float scale)
        {
            ResolutionScale = ValidFloat(scale, PresetScale(Preset), MinimumScale, 1f);
            Save();
            SetEffectiveScale(AutoResolutionEnabled ? Mathf.Min(EffectiveResolutionScale, ResolutionScale) : ResolutionScale);
            ResetSampling();
        }

        public void SetAutoResolution(bool enabled)
        {
            AutoResolutionEnabled = enabled;
            Save();
            SetEffectiveScale(ResolutionScale);
            ResetSampling();
        }

        public void SetSoftShadows(bool enabled)
        {
            SetShadowQuality020(enabled ? System.Math.Max(1, GraphicsShadowQuality020.DefaultForPreset(Preset)) : 0);
        }

        public void SetBloom(bool enabled)
        {
            BloomEnabled = enabled;
            Save();
            ApplyPostProcessing();
        }

        public void SetBrightness(float brightness)
        {
            Brightness = ValidFloat(brightness, 1f, .7f, 1.5f);
            Save();
            ApplyPostProcessing();
        }

        public void SetParticleQuality(int quality)
        {
            ParticleQuality = Mathf.Clamp(quality, 0, 2);
            Save();
            ApplyParticles(true);
        }

        public void SetFrameRateCap(int cap)
        {
            FrameRateCap = ValidFrameCap(cap);
            Save();
            ApplyFrameCap();
            ResetSampling();
        }

        public void ResetGraphicsDefaults()
        {
            Preset = GraphicsPresetCatalog.DefaultPreset; ResolutionScale = 1f;
            BloomEnabled = true; ShadowQuality020 = GraphicsShadowQuality020.DefaultForPreset(Preset); CloudsEnabled020 = WeatherEffectsEnabled020 = true; ReflectionsEnabled020 = true; AutoResolutionEnabled = false;
            Brightness = 1f; ParticleQuality = 2; FrameRateCap = 60;
            RenderDistanceBlocks = VoxelDistanceMath.DefaultBlocks;
            FarDetailPixelation = VoxelDistanceMath.DefaultFarDetail;
            PerformanceTelemetry?.SetOverlayVisible(false);
            Save();
            Apply(true);
        }

        public void SetRenderDistanceBlocks(int blocks)
        {
            RenderDistanceBlocks = VoxelDistanceMath.ClampBlocks(blocks);
            Save();
            distanceDetail?.Configure(RenderDistanceBlocks, FarDetailPixelation);
        }

        public void SetFarDetailPixelation(float intensity)
        {
            FarDetailPixelation = VoxelDistanceMath.ClampDetail(intensity);
            Save();
            distanceDetail?.Configure(RenderDistanceBlocks, FarDetailPixelation);
        }

        public void SetPerformanceOverlayVisible(bool visible) { PerformanceTelemetry?.SetOverlayVisible(visible); }
        public void SetPerformanceMenuOpen(bool open) { PerformanceTelemetry?.SetMenuOpen(open); }

        private static int ValidFrameCap(int cap) => cap == 30 || cap == 60 ? cap : 0;
        private static float PresetScale(int preset) => GraphicsPresetCatalog.Get(preset).SceneScale;
        private static float ValidFloat(float value, float fallback, float minimum, float maximum) =>
            float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, minimum, maximum);

        private void Save()
        {
            PlayerPrefs.SetInt(PresetKey, Preset);
            PlayerPrefs.SetInt(PresetSchemaKey, Mathf.Max(GraphicsPresetCatalog.SchemaVersion, PlayerPrefs.GetInt(PresetSchemaKey, 0)));
            PlayerPrefs.SetFloat(ScaleKey, ResolutionScale);
            PlayerPrefs.SetInt(BloomKey, BloomEnabled ? 1 : 0);
            PlayerPrefs.SetInt(ShadowKey020, SoftShadowsEnabled ? 1 : 0);
            SaveShadowWeatherPreferences020();
            SaveReflectionPreference020();
            PlayerPrefs.SetInt(AutoKey, AutoResolutionEnabled ? 1 : 0);
            PlayerPrefs.SetFloat(BrightnessKey, Brightness);
            PlayerPrefs.SetInt(ParticleKey, ParticleQuality);
            PlayerPrefs.SetInt(FrameCapKey, FrameRateCap);
            PlayerPrefs.SetInt(RenderDistanceKey, RenderDistanceBlocks);
            PlayerPrefs.SetFloat(FarDetailKey, FarDetailPixelation);
            PlayerPrefs.Save();
            PerformanceTelemetry?.ResetObservationWindow();
        }

        // Pure budget kernel, also extracted verbatim by work-only CPU checks.
        public static void ResolveShadowBudget020(int preset, bool enabled, out float distance, out int resolution)
        {
            GraphicsPresetSpec spec = GraphicsPresetCatalog.Get(preset);
            distance = !enabled ? 0f : spec.ShadowDistance > 0f ? System.Math.Min(25f, spec.ShadowDistance) : 12f;
            resolution = !enabled ? 512 : spec.ShadowDistance > 0f ? System.Math.Min(1024, spec.ShadowResolution) : 512;
        }

        private void ApplyShadowBudget020()
        {
            GraphicsShadowBudget020 budget = GraphicsShadowQuality020.Get(ShadowQuality020);
            float shadowDistance = budget.Distance;
            int shadowResolution = budget.Resolution;
            ResolveUltraBetaShadowBudget022(ref shadowDistance, ref shadowResolution);
            // Only this owner's private clone is writable. Shared pipeline assets and foreign bindings stay untouched.
            if (OwnsCurrentQualityPipeline020())
            {
                runtimePipeline.shadowDistance = shadowDistance;
                runtimePipeline.shadowCascadeCount = budget.Cascades;
                runtimePipeline.mainLightShadowmapResolution = shadowResolution;
                runtimePipeline.cascade2Split = .35f;
                runtimePipeline.cascade4Split = new Vector3(.10f,.25f,.50f);
                runtimePipeline.cascadeBorder = .10f;
            }
            Shader.SetGlobalFloat(WindowShadowId020, SoftShadowsEnabled ? 1f : 0f);
        }

        private void Apply(bool resetScale)
        {
            GraphicsPresetSpec spec = GraphicsPresetCatalog.Get(Preset);
            if (OwnsCurrentQualityPipeline020())
            {
                runtimePipeline.additionalLightsShadowmapResolution = spec.AdditionalShadowResolution;
                runtimePipeline.msaaSampleCount = 1;
                runtimePipeline.maxAdditionalLightsCount = spec.AdditionalLights;
                runtimePipeline.supportsCameraOpaqueTexture = spec.Opaque;
            }
            if (resetScale) SetEffectiveScale(ResolutionScale);
            RefreshWorldSettings(false);
            ApplyFrameCap();
            ResetSampling();
        }

        /// <summary>Call after a procedural world replacement if it does not change arena mode.</summary>
        public void RefreshWorldSettings() => RefreshWorldSettings(true);

        private void RefreshWorldSettings(bool forceGeometryRediscovery)
        {
            if (!initialized) return;
            lastWorld = session != null ? session.World : null;
            lastOpening = session != null ? session.Opening : null;
            lastCoopArena = lastWorld != null && lastWorld.IsCoopArena;
            lastWorldMode020=lastWorld!=null?lastWorld.Mode020:WorldMode020.None;
            distanceDetail?.RefreshWorld(forceGeometryRediscovery);
            distanceDetail?.Configure(RenderDistanceBlocks, FarDetailPixelation);
            PerformanceTelemetry?.ResetObservationWindow();
            ApplyShadowBudget020();
            ApplyReflectionPolicy020();
            GraphicsPresetSpec spec = GraphicsPresetCatalog.Get(Preset);
            var lights = FindObjectsByType<Light>();
            Light primary = null, sun = RenderSettings.sun;
            foreach (var light in lights)
            {
                if (!lightShadowStates020.TryGetValue(light, out var state))
                {
                    state = new LightShadowState020 { Baseline = light.shadows };
                    lightShadowStates020.Add(light, state);
                }
                if (!light.isActiveAndEnabled || light.type != LightType.Directional || state.Baseline == LightShadows.None ||
                    light.intensity <= 0f || float.IsNaN(light.intensity) || float.IsInfinity(light.intensity)) continue;
                if (light == sun || primary == null || primary != sun &&
                    (light.intensity > primary.intensity)) primary = light;
            }
            bool shadows = SoftShadowsEnabled && primary != null;
            bool canReadOwnedPipeline = OwnsCurrentQualityPipeline020();
            bool softFilter = GraphicsShadowQuality020.Get(ShadowQuality020).Soft && canReadOwnedPipeline && runtimePipeline.supportsSoftShadows;
            foreach (var light in lights)
            {
                var state = lightShadowStates020[light];
                LightShadows desired = shadows && light == primary ? (softFilter ? LightShadows.Soft : LightShadows.Hard) : LightShadows.None;
                if (light.shadows != desired)
                {
                    state.LastWrite = desired;
                    state.HasLastWrite = true;
                    lightShadowStates020[light] = state;
                    // Store the attempted value first so cleanup can still recover
                    // if Unity mutates the property and then reports a setter failure.
                    light.shadows = desired;
                }
            }
            ActiveShadowLights020 = shadows ? 1 : 0;
            foreach (var firePool in FindObjectsByType<TutorialFireLightPool>(FindObjectsInactive.Include))
                firePool.SetLightBudget(Mathf.Min(4, spec.AdditionalLights));
            cameras.Clear();
            foreach (var camera in FindObjectsByType<Camera>(FindObjectsInactive.Include))
            {
                if (camera.cameraType != CameraType.Game) continue;
                cameras.Add(camera);
                ApplyCamera(camera);
            }
            portals = FindObjectsByType<PortalAnomaly>(FindObjectsInactive.Include);
            for (int i = 0; i < portals.Length; i++) if (portals[i] != null) portals[i].SetQuality(GraphicsPresetCatalog.Get(Preset).PortalDetail);
            ApplyPostProcessing();
            ApplyParticles(true);
            refreshRequested = false;
            ResetSampling();
        }

        private void ApplyCamera(Camera camera)
        {
            camera.farClipPlane = lastWorldMode020==WorldMode020.CampaignHub ? VoxelDistanceMath.EffectiveReach(RenderDistanceBlocks,lastWorld.CampaignHubFarClip020) : !lastCoopArena ? 60 : GraphicsPresetCatalog.Get(Preset).ArenaFarClip;
            camera.allowDynamicResolution = false;
            var data = camera.GetUniversalAdditionalCameraData();
            if (IsUltraBetaDesktop022 && OwnsCurrentQualityPipeline020())
            {
                // Preserve this owner's current SMAA lease so foreign changes can
                // be detected before any base AA write replaces their evidence.
                ApplyUltraBetaCameraQuality022(camera, data);
            }
            else
            {
                data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
                data.antialiasingQuality = Preset <= 1 || Preset == UltraBetaPreset022
                    ? AntialiasingQuality.Medium : AntialiasingQuality.Low;
                ApplyUltraBetaCameraQuality022(camera, data);
            }
            data.renderPostProcessing = true;
            data.requiresColorTexture = GraphicsPresetCatalog.Get(Preset).Opaque && data.renderType == CameraRenderType.Base;
            // Native ScreenSpaceOverlay UI is outside the scaled scene target.
        }

        private void BeforeCamera(ScriptableRenderContext context, Camera camera)
        {
            if (!initialized || camera == null || camera.cameraType != CameraType.Game || cameras.Contains(camera)) return;
            // Opening cameras can be created after Initialize; no scene search per frame.
            cameras.Add(camera);
            ApplyCamera(camera);
        }

        private void SceneLoaded(Scene scene, LoadSceneMode mode) { refreshRequested = true; ResetSampling(); }

        private void ApplyPostProcessing()
        {
            foreach (var volume in FindObjectsByType<Volume>())
            {
                bool hadProfile = volume.HasInstantiatedProfile();
                var profile = volume.profile;
                if (!hadProfile)
                {
                    profile.hideFlags = HideFlags.HideAndDontSave;
                    ownedProfiles[volume] = profile;
                }
                if (profile.TryGet<Bloom>(out var bloom)) bloom.active = BloomEnabled;
                if (!profile.TryGet<ColorAdjustments>(out var grade))
                {
                    grade = profile.Add<ColorAdjustments>(false);
                    grade.postExposure.value = .65f;
                }
                if (!exposures.TryGetValue(grade, out var baseline))
                {
                    baseline = new ExposureDefaults { Value = grade.postExposure.value, Override = grade.postExposure.overrideState };
                    exposures.Add(grade, baseline);
                }
                grade.postExposure.overrideState = true;
                grade.postExposure.value = baseline.Value + Brightness - 1f;
                ApplyUltraBetaVolumeRefinements022(profile, grade);
                if (profile.TryGet<MotionBlur>(out var blur)) blur.active = false;
            }
            if (!IsUltraBetaDesktop022) RestoreUltraBetaVolumeRefinements022();
        }

        private bool IsOwned(Transform candidate) => session != null &&
            (candidate.IsChildOf(session.transform) ||
             session.World != null && candidate.IsChildOf(session.World.transform) ||
             session.Player != null && candidate.IsChildOf(session.Player.transform) ||
             session.SpellLights != null && candidate.IsChildOf(session.SpellLights.transform));

        private void ApplyParticles(bool discover)
        {
            if (discover)
                foreach (var particles in FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include))
                {
                    if (!IsOwned(particles.transform) || particleDefaults.ContainsKey(particles)) continue;
                    var main = particles.main;
                    var emission = particles.emission;
                    particleDefaults.Add(particles, new ParticleDefaults {
                        Maximum = main.maxParticles, TimeRate = emission.rateOverTimeMultiplier, DistanceRate = emission.rateOverDistanceMultiplier
                    });
                }
            float factor = EffectiveParticleFactor;
            foreach (var pair in particleDefaults)
            {
                if (pair.Key == null) continue;
                var main = pair.Key.main;
                var emission = pair.Key.emission;
                main.maxParticles = pair.Value.Maximum <= 0 ? 0 : Mathf.Max(1, Mathf.RoundToInt(pair.Value.Maximum * factor));
                emission.rateOverTimeMultiplier = pair.Value.TimeRate * factor;
                emission.rateOverDistanceMultiplier = pair.Value.DistanceRate * factor;
            }
        }

        private void ApplyFrameCap()
        {
            QualitySettings.vSyncCount = 0;
            // Web uses browser requestAnimationFrame pacing; this is a requested
            // cap, not proof that a device sustains or precisely schedules it.
            Application.targetFrameRate = FrameRateCap == 0 ? -1 : FrameRateCap;
        }

        private void SetEffectiveScale(float scale)
        {
            if (!OwnsCurrentQualityPipeline020()) { EffectiveResolutionScale = 1f; return; }
            float bounded = Mathf.Clamp(scale, MinimumScale, ResolutionScale);
            if (!Mathf.Approximately(runtimePipeline.renderScale, bounded)) runtimePipeline.renderScale = bounded;
            EffectiveResolutionScale = runtimePipeline.renderScale;
        }

        private void ResetSampling()
        {
            frameCostEma = stableBudgetSeconds = 0f;
            warmupRemaining = 1f;
            adjustmentRemaining = AdjustmentInterval;
        }

        private void Update()
        {
            if (!initialized) return;
            bool coop = session != null && session.World != null && session.World.IsCoopArena;
            if (refreshRequested || session != null && (session.World != lastWorld || session.Opening != lastOpening || coop != lastCoopArena || (session.World!=null?session.World.Mode020:WorldMode020.None)!=lastWorldMode020))
                RefreshWorldSettings();
            // No scene searches, allocations, pipeline recreation or PlayerPrefs
            // writes occur in the steady-state adaptive loop.
            if (!AutoResolutionEnabled || !OwnsCurrentQualityPipeline020() || Application.isMobilePlatform || applicationPaused ||
                !Application.isFocused || session == null || !session.IsPlaying ||
                session.Rooms != null && session.Rooms.Busy)
            { ResetSampling(); return; }
            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f || float.IsNaN(dt) || float.IsInfinity(dt) || dt > .25f)
            { ResetSampling(); return; }
            if (warmupRemaining > 0f) { warmupRemaining = Mathf.Max(0f, warmupRemaining - dt); return; }
            frameCostEma = frameCostEma <= 0f ? dt : Mathf.Lerp(frameCostEma, dt, dt / (.75f + dt));
            adjustmentRemaining = Mathf.Max(0f, adjustmentRemaining - dt);
            float budget = 1f / AdaptiveTargetFrameRate;
            stableBudgetSeconds = frameCostEma <= budget * 1.08f ? stableBudgetSeconds + dt : 0f;
            if (adjustmentRemaining > 0f) return;
            float budgetRatio = 60f / AdaptiveTargetFrameRate;
            float scale = EffectiveResolutionScale;
            if (frameCostEma > .024f * budgetRatio) scale -= ScaleStep;
            else if (frameCostEma < .014f * budgetRatio || stableBudgetSeconds >= 8f) scale += ScaleStep;
            float bounded = Mathf.Clamp(scale, MinimumScale, ResolutionScale);
            if (Mathf.Abs(bounded - EffectiveResolutionScale) < .001f) return;
            SetEffectiveScale(bounded);
            adjustmentRemaining = AdjustmentInterval;
            stableBudgetSeconds = 0f;
        }

        private void OnApplicationFocus(bool focused) { ResetSampling(); }
        private void OnApplicationPause(bool paused) { applicationPaused = paused; ResetSampling(); }

        private void OnEnable()
        {
            // Initialize owns the first acquisition. Re-enabling an initialized
            // component reacquires after its disable-time cleanup.
            if (initialized && Application.isPlaying)
            {
                AcquireRuntimePipeline();ApplyReflectionPolicy020();
                refreshRequested = true;
            }
        }

        private void RestoreOwnedLightShadows020()
        {
            foreach (var pair in lightShadowStates020)
                if (pair.Key != null && pair.Value.HasLastWrite && pair.Key.shadows == pair.Value.LastWrite)
                    pair.Key.shadows = pair.Value.Baseline;
            lightShadowStates020.Clear();
        }

        private void OnDisable()
        {
            RestoreReflectionPolicy020();
            if (initialized) Shader.SetGlobalFloat(WindowShadowId020, originalWindowShadow020);
            RestoreUltraBetaVolumeRefinements022();
            RestoreUltraBetaCameraQuality022();
            RestoreOwnedLightShadows020();
            ReleaseRuntimePipeline();
        }

        private static bool IsOwnedRuntimePipeline(UniversalRenderPipelineAsset asset) =>
            asset != null && (asset.hideFlags & HideFlags.DontSave) != 0 &&
            asset.name.EndsWith(RuntimePipelineSuffix, System.StringComparison.Ordinal);

        private bool OwnsCurrentQualityPipeline020()
        {
            if (!ownsRuntimePipeline || runtimePipeline == null || runtimeQualityLevel < 0 ||
                QualitySettings.GetQualityLevel() != runtimeQualityLevel) return false;
            var current = QualitySettings.renderPipeline;
            return ReferenceEquals(current, runtimePipeline) || current == runtimePipeline;
        }

        private void AcquireRuntimePipeline()
        {
            if (ownsRuntimePipeline)
            {
                if (runtimePipeline != null) return;
                // A destroyed serialized clone can leave its old quality slot null.
                // Retire that exact owner before any replacement can be acquired.
                ReleaseRuntimePipeline();
                if (ownsRuntimePipeline) return;
            }
            // Never adopt or mutate a serialized reference that this instance did not create.
            runtimePipeline = null;
            runtimeQualityLevel = -1;
            // Repair an orphan from an interrupted old play session by using the
            // persistent default, never by cloning another Oracle private clone.
            originalQualityPipeline = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
            if (IsOwnedRuntimePipeline(originalQualityPipeline)) originalQualityPipeline = null;
            var source = originalQualityPipeline != null ? originalQualityPipeline
                : GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if (source == null || IsOwnedRuntimePipeline(source)) return;
            runtimeQualityLevel = QualitySettings.GetQualityLevel();
            runtimePipeline = Instantiate(source);
            if (runtimePipeline == null) { runtimeQualityLevel = -1; return; }
            // Record ownership as soon as the clone exists so later setup failures
            // remain recoverable by ReleaseRuntimePipeline.
            ownsRuntimePipeline = true;
            runtimePipeline.name = source.name + RuntimePipelineSuffix;
            runtimePipeline.hideFlags = HideFlags.HideAndDontSave;
            QualitySettings.renderPipeline = runtimePipeline;
        }

        private void ReleaseRuntimePipeline()
        {
            if (!ownsRuntimePipeline)
            {
                runtimePipeline = null;
                originalQualityPipeline = null;
                runtimeQualityLevel = -1;
                return;
            }
            var owned = runtimePipeline;
            int activeLevel = QualitySettings.GetQualityLevel();
            bool referencesRestored = false;
            try
            {
                // renderPipeline is per-quality-level. A different active level
                // at teardown must not leave the original level pointing at a
                // soon-to-be-destroyed clone. Unity exposes a getter per level;
                // switching levels only occurs on this cold cleanup path.
                int count = QualitySettings.names.Length;
                for (int i = 0; i < count; i++)
                {
                    var current = QualitySettings.GetRenderPipelineAssetAt(i);
                    bool sameReference = !ReferenceEquals(owned, null) && ReferenceEquals(current, owned);
                    bool sameLiveObject = owned != null && current == owned;
                    bool lostSerializedClone = ownsRuntimePipeline && owned == null && i == runtimeQualityLevel && current == null;
                    if (!sameReference && !sameLiveObject && !lostSerializedClone) continue;
                    if (QualitySettings.GetQualityLevel() != i) QualitySettings.SetQualityLevel(i, false);
                    // A missing/destroyed original means the normal global
                    // default, never a reference to a dead ScriptableObject.
                    QualitySettings.renderPipeline = originalQualityPipeline != null ? originalQualityPipeline : null;
                }
                referencesRestored = true;
            }
            finally
            {
                if (QualitySettings.GetQualityLevel() != activeLevel) QualitySettings.SetQualityLevel(activeLevel, false);
                // If a Unity setter failed, preserve ownership and a live clone
                // for a later cleanup rather than destroy a referenced asset.
                if (referencesRestored)
                {
                    runtimePipeline = null;
                    ownsRuntimePipeline = false;
                    runtimeQualityLevel = -1;
                    if (owned != null) Destroy(owned);
                }
            }
        }

        private void OnDestroy()
        {
            RestoreUltraBetaVolumeRefinements022();
            RestoreUltraBetaCameraQuality022();
            RestoreReflectionPolicy020();
            if (ownsDistanceDetail && distanceDetail != null) Destroy(distanceDetail);
            if (ownsTelemetry && PerformanceTelemetry != null) Destroy(PerformanceTelemetry);
            RenderPipelineManager.beginCameraRendering -= BeforeCamera;
            SceneManager.sceneLoaded -= SceneLoaded;
            RestoreOwnedLightShadows020();
            foreach (var pair in exposures)
                if (pair.Key != null) { pair.Key.postExposure.value = pair.Value.Value; pair.Key.postExposure.overrideState = pair.Value.Override; }
            foreach (var pair in particleDefaults)
            {
                if (pair.Key == null) continue;
                var main = pair.Key.main; var emission = pair.Key.emission;
                main.maxParticles = pair.Value.Maximum;
                emission.rateOverTimeMultiplier = pair.Value.TimeRate;
                emission.rateOverDistanceMultiplier = pair.Value.DistanceRate;
            }
            if (initialized) { Application.targetFrameRate = originalFrameRate; QualitySettings.vSyncCount = originalVsync; Shader.SetGlobalFloat(WindowShadowId020, originalWindowShadow020); }
            ReleaseRuntimePipeline();
            foreach (var pair in ownedProfiles)
            {
                if (pair.Key != null) pair.Key.profile = null;
                if (pair.Value == null) continue;
                foreach (var component in pair.Value.components) if (component != null) Destroy(component);
                Destroy(pair.Value);
            }
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing quality-level URP binding and world-light settings; no structural, camera, or gameplay transform changes.
// Array Formats: One owner-tracked private pipeline clone plus a cold light baseline/last-write dictionary; no new per-frame collections.
// Dependencies/Pointers: Apply/scale/shadow-budget writes require the exact clone at its acquired quality level; disable and destroy restore light values only while they still match the last write. Native tier/reload behavior remains for root verification.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing graphics Canvas and world-metre shadow settings; palette/actor/world geometry unchanged.
// Array Formats: Four scalar tiers and two weather flags; native pipeline/resource buffers retain their existing owner.
// Dependencies/Pointers: Private clone/public APIs only; existing input/menu, light restoration and save owner retained; atmosphere consumes instance flags.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing graphics preference scalars and fitted HUD copy; no world/camera transforms.
// Array Formats: One derived Beta policy boolean and existing cached UI snapshot; no new runtime store or collection.
// Dependencies/Pointers: Existing catalog/schema/defaults, saved scale/auto policy and private render owners remain authoritative; root alone applies/imports and verifies native UI.
