using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // Main thread owns one private URP asset and the companion render/telemetry components.
    // Preset/distance policies are pure value math; Unity Object calls stay outside sampling loops.
    // No jobs/native containers are owned here; steady adaptive sampling reuses scalar state.
    /// <summary>Four saved PC presets. Fixed scale by default; optional legacy adaptive scale uses frame intervals, not GPU timing.</summary>
    [DisallowMultipleComponent]
    public sealed class GraphicsPreferences : MonoBehaviour
    {
        private const string PresetKey = "Oracle.Graphics.Preset";
        private const string ScaleKey = "Oracle.Graphics.Scale";
        private const string BloomKey = "Oracle.Graphics.Bloom";
        private const string AutoKey = "Oracle.Graphics.AutoResolution";
        private const string BrightnessKey = "Oracle.Graphics.Brightness";
        private const string ParticleKey = "Oracle.Graphics.Particles";
        private const string FrameCapKey = "Oracle.Graphics.FrameCap";
        private const string PresetSchemaKey = "Oracle.Graphics.PresetSchema";
        private const string RenderDistanceKey = "Oracle.Graphics.RenderDistanceBlocks";
        private const string FarDetailKey = "Oracle.Graphics.FarDetailPixelation";
        private const float MinimumScale = .5f, ScaleStep = .05f, AdjustmentInterval = 2f;
        private readonly Dictionary<Light, LightShadows> lightShadows = new Dictionary<Light, LightShadows>();
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
        private int originalFrameRate, originalVsync;
        private float frameCostEma, warmupRemaining, adjustmentRemaining, stableBudgetSeconds;
        private VoxelDistanceDetailController distanceDetail;
        private bool ownsDistanceDetail, ownsTelemetry;

        private struct ExposureDefaults { public float Value; public bool Override; }
        private struct ParticleDefaults { public float TimeRate, DistanceRate; public int Maximum; }

        public const int PresetCount = GraphicsPresetCatalog.Count;
        public static readonly string[] PresetNames = GraphicsPresetCatalog.Names;
        public string PresetName => GraphicsPresetCatalog.Get(Preset).Name;
        public bool PresetIsCustomized => !Mathf.Approximately(ResolutionScale, PresetScale(Preset)) ||
            BloomEnabled != GraphicsPresetCatalog.Get(Preset).Bloom ||
            ParticleQuality != GraphicsPresetCatalog.Get(Preset).ParticleQuality || AutoResolutionEnabled;
        public int Preset { get; private set; } = GraphicsPresetCatalog.DefaultPreset;
        public float ResolutionScale { get; private set; } = 1f;
        public float EffectiveResolutionScale { get; private set; } = 1f;
        public bool AutoResolutionEnabled { get; private set; }
        public bool BloomEnabled { get; private set; } = true;
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
            session != null && session.World != null && session.World.IsCoopArena ? GraphicsPresetCatalog.Get(Preset).ArenaFarClip : 60f);
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
            AcquireRuntimePipeline();
            int schema = PlayerPrefs.GetInt(PresetSchemaKey, 0);
            Preset = GraphicsPresetCatalog.MigratePreset(PlayerPrefs.GetInt(PresetKey, 0), PlayerPrefs.HasKey(PresetKey), schema);
            GraphicsPresetSpec spec = GraphicsPresetCatalog.Get(Preset);
            ResolutionScale = ValidFloat(PlayerPrefs.GetFloat(ScaleKey, PresetScale(Preset)), PresetScale(Preset), MinimumScale, 1f);
            BloomEnabled = PlayerPrefs.GetInt(BloomKey, spec.Bloom ? 1 : 0) != 0;
            // One migration turns the old default-on policy off. Later explicit
            // adaptive opt-in is preserved, while selecting any preset turns it off.
            AutoResolutionEnabled = GraphicsPresetCatalog.ResolveAdaptiveDefault(PlayerPrefs.GetInt(AutoKey, 0) != 0, schema);
            Brightness = ValidFloat(PlayerPrefs.GetFloat(BrightnessKey, 1f), 1f, .7f, 1.5f);
            ParticleQuality = Mathf.Clamp(PlayerPrefs.GetInt(ParticleKey, spec.ParticleQuality), 0, 2);
            FrameRateCap = ValidFrameCap(PlayerPrefs.GetInt(FrameCapKey, 60));
            RenderDistanceBlocks = VoxelDistanceMath.ClampBlocks(PlayerPrefs.GetInt(RenderDistanceKey, VoxelDistanceMath.DefaultBlocks));
            FarDetailPixelation = VoxelDistanceMath.ClampDetail(PlayerPrefs.GetFloat(FarDetailKey, VoxelDistanceMath.DefaultFarDetail));
            if (schema < GraphicsPresetCatalog.SchemaVersion) Save();
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
            GraphicsPresetSpec spec = GraphicsPresetCatalog.Get(Preset);
            ResolutionScale = PresetScale(Preset);
            BloomEnabled = spec.Bloom;
            ParticleQuality = spec.ParticleQuality;
            AutoResolutionEnabled = false;
            Save();
            Apply(true);
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
            BloomEnabled = true; AutoResolutionEnabled = false;
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
            PlayerPrefs.SetInt(AutoKey, AutoResolutionEnabled ? 1 : 0);
            PlayerPrefs.SetFloat(BrightnessKey, Brightness);
            PlayerPrefs.SetInt(ParticleKey, ParticleQuality);
            PlayerPrefs.SetInt(FrameCapKey, FrameRateCap);
            PlayerPrefs.SetInt(RenderDistanceKey, RenderDistanceBlocks);
            PlayerPrefs.SetFloat(FarDetailKey, FarDetailPixelation);
            PlayerPrefs.Save();
            PerformanceTelemetry?.ResetObservationWindow();
        }

        private void Apply(bool resetScale)
        {
            GraphicsPresetSpec spec = GraphicsPresetCatalog.Get(Preset);
            if (runtimePipeline != null)
            {
                runtimePipeline.shadowDistance = spec.ShadowDistance;
                runtimePipeline.shadowCascadeCount = spec.Cascades;
                runtimePipeline.mainLightShadowmapResolution = spec.ShadowResolution;
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
            distanceDetail?.RefreshWorld(forceGeometryRediscovery);
            distanceDetail?.Configure(RenderDistanceBlocks, FarDetailPixelation);
            PerformanceTelemetry?.ResetObservationWindow();
            GraphicsPresetSpec spec = GraphicsPresetCatalog.Get(Preset);
            foreach (var light in FindObjectsByType<Light>())
            {
                if (!lightShadows.ContainsKey(light)) lightShadows.Add(light, light.shadows);
                light.shadows = spec.ShadowDistance <= 0f || light.type != LightType.Directional && !spec.LocalShadows
                    ? LightShadows.None : lightShadows[light];
            }
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
            camera.farClipPlane = !lastCoopArena ? 60 : GraphicsPresetCatalog.Get(Preset).ArenaFarClip;
            camera.allowDynamicResolution = false;
            var data = camera.GetUniversalAdditionalCameraData();
            data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            data.antialiasingQuality = Preset <= 1 ? AntialiasingQuality.Medium : AntialiasingQuality.Low;
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
                if (profile.TryGet<MotionBlur>(out var blur)) blur.active = false;
            }
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
            if (runtimePipeline == null) { EffectiveResolutionScale = 1f; return; }
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
            if (refreshRequested || session != null && (session.World != lastWorld || session.Opening != lastOpening || coop != lastCoopArena))
                RefreshWorldSettings();
            // No scene searches, allocations, pipeline recreation or PlayerPrefs
            // writes occur in the steady-state adaptive loop.
            if (!AutoResolutionEnabled || runtimePipeline == null || Application.isMobilePlatform || applicationPaused ||
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
                AcquireRuntimePipeline();
                refreshRequested = true;
            }
        }

        private void OnDisable() { ReleaseRuntimePipeline(); }

        private static bool IsOwnedRuntimePipeline(UniversalRenderPipelineAsset asset) =>
            asset != null && (asset.hideFlags & HideFlags.DontSave) != 0 &&
            asset.name.EndsWith(RuntimePipelineSuffix, System.StringComparison.Ordinal);

        private void AcquireRuntimePipeline()
        {
            if (runtimePipeline != null) return;
            // Repair an orphan from an interrupted old play session by using the
            // persistent default, never by cloning another Oracle private clone.
            originalQualityPipeline = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
            if (IsOwnedRuntimePipeline(originalQualityPipeline)) originalQualityPipeline = null;
            var source = originalQualityPipeline != null ? originalQualityPipeline
                : GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if (source == null || IsOwnedRuntimePipeline(source)) return;
            runtimeQualityLevel = QualitySettings.GetQualityLevel();
            runtimePipeline = Instantiate(source);
            runtimePipeline.name = source.name + RuntimePipelineSuffix;
            runtimePipeline.hideFlags = HideFlags.HideAndDontSave;
            ownsRuntimePipeline = true;
            QualitySettings.renderPipeline = runtimePipeline;
        }

        private void ReleaseRuntimePipeline()
        {
            if (!ownsRuntimePipeline && ReferenceEquals(runtimePipeline, null)) return;
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
            if (ownsDistanceDetail && distanceDetail != null) Destroy(distanceDetail);
            if (ownsTelemetry && PerformanceTelemetry != null) Destroy(PerformanceTelemetry);
            RenderPipelineManager.beginCameraRendering -= BeforeCamera;
            SceneManager.sceneLoaded -= SceneLoaded;
            foreach (var pair in lightShadows) if (pair.Key != null) pair.Key.shadows = pair.Value;
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
            if (initialized) { Application.targetFrameRate = originalFrameRate; QualitySettings.vSyncCount = originalVsync; }
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
