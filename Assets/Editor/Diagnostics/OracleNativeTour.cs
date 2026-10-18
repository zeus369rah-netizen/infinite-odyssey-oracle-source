#if UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace OdysseyOracle.Diagnostics
{
    // # ARCHITECTURE SNAPSHOT
    // Main-thread native PhysX/Camera fixture, not a replacement player controller.
    // World coordinates/metres; fixed caller-owned managed sample buffers; no jobs or NativeArrays.
    // Unity queries use fixed NonAlloc buffers at 5Hz. Receipt arrays/strings/JSON allocate only
    // outside timed traversal. Two disabled calibration colliders and one contact probe are owned
    // by the fixture and retired on every exit. No scene rebuild, damage, reward or network writes.
    [DisallowMultipleComponent]
    public sealed class OracleNativeTour : MonoBehaviour
    {
        public const int MaximumLegs = 64, MaximumSamples = 8192, MaximumFramesPerPreset = 32768;
        private const int Presets = 4, QueryCapacity = 64, MaximumFindings = 256;
        private enum Phase { Warmup, Settle, Walk }
        private struct Leg { public string Label; public Vector3 Start, End; public bool WallProbe; }
        private struct BehaviourState { public Behaviour Target; public bool Enabled; }
        private struct Preference { public string Key; public bool Present, Float; public int IntValue; public float FloatValue; }
        private struct Finding { public int Preset, Leg; public string Kind; public UnityEngine.Object Target; public Vector3 Feet; public float Value; }

        [Serializable]
        public sealed class Options
        {
            public float WarmupSeconds = 2f;
            public float MaximumSecondsPerPreset = 90f;
            public float SpeedMetresPerSecond = 4.2f;
            public int FrameCapOverride = -1; // -1 preserves requested cap; 0 requests uncapped; 30/60 explicit.
            public bool AllowUnfocusedGeometryOnly = false;
        }

        [Serializable]
        public struct Sample
        {
            public int Preset, Leg, ScreenWidth, ScreenHeight, CameraPixelWidth, CameraPixelHeight;
            public int CameraReportedScaledWidth, CameraReportedScaledHeight, EnabledFireLights, FireLightBudget;
            public int CulledChunks, RegisteredChunks, CollisionFlags;
            public float SecondsInPreset, FrameIntervalMs, FloorGapMetres, MaximumPenetrationMetres;
            public float SimulatedSecondsInLeg;
            public float SceneRenderScale, FieldOfView, FarClipMetres;
            public Vector3 Feet, CameraPosition, CameraEuler, FloorNormal;
            public bool FloorFound, OverlapQuerySaturated, FloorQuerySaturated, DrawCountersAvailable;
            public bool ApplicationFocused;
            public long ObservedDrawCalls, ObservedSetPassCalls;
        }

        [Serializable]
        public sealed class LegResult
        {
            public int Preset, Leg;
            public string Label, Outcome = "unvisited", ContactName = "";
            public Vector3 RequestedStart, RequestedEnd, ActualEnd;
            public bool WallProbe;
            public float Seconds, SimulatedSeconds, TravelledMetres;
        }

        [Serializable]
        public sealed class PresetResult
        {
            public int Index, RequestedFrameCap, NativeTargetFrameRate, VSyncCount, RecordedFrames, DroppedFrames;
            public int CompletedLegs, PlannedLegs, RenderedCameraFrames;
            public int NativeControllerMoveCalls;
            public string Name, Outcome = "unvisited";
            public float SceneScale, ShadowDistanceMetres, Seconds;
            public bool Adaptive;
            public TourFrameSummary FrameIntervals;
        }

        [Serializable]
        public sealed class FindingReceipt
        {
            public int Preset, Leg;
            public string Kind, ObjectName;
            public Vector3 Feet;
            public float Value;
        }

        [Serializable]
        public sealed class Receipt
        {
            public string Format = "oracle.native-tour.v1";
            public string StartedUtc, FinishedUtc, Termination, Scene, UnityVersion, Platform, GraphicsApi;
            public string TimingScope = "Observed native frame intervals during direct CharacterController.Move traversal of a prepared, static-actor scene. Includes Editor/player pacing, rendering, waits and fixture overhead; not isolated CPU/GPU work or browser FPS.";
            public string CoverageScope = "Each named leg is independently positioned onto checked floor. Reposition/warmup/settle frames are excluded. Side contact records its actual collider for review, not a blanket wall-correctness pass. No keyboard/mouse, normal acceleration/headbob, combat, networking, screenshots or visible-light-contribution acceptance.";
            public bool ReviewRequired = true, Arena, AllPresetsAttempted, PenetrationMetricCalibrated;
            public bool GeometryOnly, AllRequestedPresetsAttempted, FrameTimingCollected;
            public bool FrameTimingIsPerformanceAcceptance = false, PooledLightSelectionVerified = false;
            public int RequestedPresetCount;
            public bool SamplesTruncated, FindingsTruncated, RestoredGraphicsAndPreferenceKeys;
            public string CleanupError = "";
            public Options Configuration;
            public PresetResult[] Presets;
            public LegResult[] Legs;
            public Sample[] Samples;
            public FindingReceipt[] Findings;
        }

        private readonly Leg[] legs = new Leg[MaximumLegs];
        private readonly Sample[] samples = new Sample[MaximumSamples];
        private readonly Finding[] findings = new Finding[MaximumFindings];
        private readonly Collider[] overlaps = new Collider[QueryCapacity];
        private readonly RaycastHit[] floorHits = new RaycastHit[QueryCapacity];
        private readonly BehaviourState[] controls = new BehaviourState[16];
        private readonly float[][] intervals = { new float[MaximumFramesPerPreset], new float[MaximumFramesPerPreset], new float[MaximumFramesPerPreset], new float[MaximumFramesPerPreset] };
        private readonly int[] intervalCounts = new int[Presets], droppedIntervals = new int[Presets];
        private Preference[] preferences;
        private Canvas[] canvases;
        private bool[] canvasEnabled;
        private TutorialFireLightPool[] firePools;
        private GameSession session;
        private DungeonWorld world;
        private OraclePlayer player;
        private CharacterController controller;
        private Camera camera, openingCamera;
        private OracleTourContactProbe contact;
        private GameObject calibrationRoot;
        private CapsuleCollider capsule;
        private BoxCollider calibrationBox;
        private PresetResult[] results;
        private LegResult[] legResults;
        private Options configuration;
        private Receipt receipt;
        private string receiptPath;
        private Phase phase;
        private int legCount, sampleCount, findingCount, controlCount, preset, legIndex, skipIntervals, presetPass, completedPresets;
        private bool arena, sampleOverflow, findingOverflow, calibrated, captured, cameraEnabled, openingEnabled;
        private bool priorStarted, priorPaused;
        private int savedPreset, savedParticles, savedFrameCap, savedBlocks, savedVSync, savedTargetRate;
        private float savedScale, savedBrightness, savedDetail, savedStepOffset, savedFar, savedNear, savedFov;
        private bool savedBloom, savedAdaptive;
        private Vector3 savedPosition, savedViewPosition, priorFeet;
        private Quaternion savedRotation, savedViewRotation;
        private double previousFrameTime, presetStarted, phaseStarted, legStarted, nextSample;
        private float verticalSpeed, stalledSeconds, travelled, simulatedLegSeconds, nextGeometrySample;
        private CollisionFlags collisionFlags;

        public bool IsRunning { get; private set; }
        public bool ReceiptWritten { get; private set; }
        public string Status { get; private set; } = "Not started";
        public Receipt LastReceipt => receipt;

        /// <summary>Root invokes explicitly in stable, focused Play mode after preparing one world.</summary>
        public bool BeginPreparedWorld(GameSession owner, string absoluteReceiptPath, Options options = null)
        {
            if (IsRunning) { Status = "A tour is already running."; return false; }
            Options chosen = Sanitize(options);
            if (!Application.isPlaying || !Application.isFocused && !chosen.AllowUnfocusedGeometryOnly || Mathf.Abs(Time.timeScale - 1f) > .001f)
                return Refuse("Native Play at timeScale1 is required; focus is required unless explicit geometry-only mode is selected.");
            if (owner == null || owner.World == null || owner.Player == null || owner.Graphics == null ||
                owner.Player.ViewCamera == null || owner.Player.ViewRoot == null)
                return Refuse("Prepare the world, player camera and graphics settings first.");
            if (GraphicsPreferences.PresetNames == null || GraphicsPreferences.PresetNames.Length != Presets)
                return Refuse("This receipt format expects the current four-preset catalog.");
            if (owner.IsDowned || owner.IsDead || owner.Victory || owner.Rooms != null && (owner.Rooms.Connected || owner.Rooms.Busy) ||
                owner.Defense != null && owner.Defense.Running || owner.Loading != null && owner.Loading.IsLoading ||
                owner.Opening != null && (owner.Opening.IsIntroPlaying || owner.Opening.IsTransitioning) ||
                owner.Builder != null && owner.Builder.IsPlacing || GameActionBindings.IsRebinding)
                return Refuse("Use a stable disconnected world without combat defeat, placement, loading, intro or rebinding.");
            CharacterController candidate = owner.Player.GetComponent<CharacterController>();
            Vector3 scale = owner.Player.transform.lossyScale;
            if (candidate == null || !candidate.enabled || !candidate.detectCollisions || (scale - Vector3.one).sqrMagnitude > .000001f)
                return Refuse("An enabled, collision-detecting, unit-scale player CharacterController is required.");
            if (string.IsNullOrWhiteSpace(absoluteReceiptPath) || !Path.IsPathRooted(absoluteReceiptPath))
                return Refuse("Provide a new absolute JSON receipt path in an existing output directory.");
            string fullPath;
            try { fullPath = Path.GetFullPath(absoluteReceiptPath); }
            catch (Exception error) { return Refuse("Invalid receipt path: " + error.Message); }
            if (File.Exists(fullPath) || !Directory.Exists(Path.GetDirectoryName(fullPath)) ||
                !string.Equals(Path.GetExtension(fullPath), ".json", StringComparison.OrdinalIgnoreCase))
                return Refuse("Receipt must be a new .json file; its parent directory must already exist.");

            session = owner; world = owner.World; player = owner.Player; controller = candidate;
            camera = player.ViewCamera; receiptPath = fullPath; arena = world.IsCoopArena;
            configuration = chosen;
            legCount = sampleCount = findingCount = controlCount = 0;
            presetPass = completedPresets = 0;
            preset = legIndex = -1;
            sampleOverflow = findingOverflow = calibrated = false;
            ReceiptWritten = false; receipt = null;
            Array.Clear(intervalCounts, 0, intervalCounts.Length); Array.Clear(droppedIntervals, 0, droppedIntervals.Length);
            try { BuildRoute(); }
            catch (Exception error) { return Refuse("Route metadata is unsuitable: " + error.Message); }
            if (legCount == 0) return Refuse("No valid route could be derived from current world metadata.");
            results = new PresetResult[Presets];
            legResults = new LegResult[Presets * legCount];
            for (int p = 0; p < Presets; p++)
            {
                results[p] = new PresetResult { Index = p, Name = GraphicsPreferences.PresetNames[p], PlannedLegs = legCount };
                for (int l = 0; l < legCount; l++) legResults[p * legCount + l] = new LegResult {
                    Preset = p, Leg = l, Label = legs[l].Label, RequestedStart = legs[l].Start,
                    RequestedEnd = legs[l].End, WallProbe = legs[l].WallProbe
                };
                if (configuration.AllowUnfocusedGeometryOnly && p != session.Graphics.Preset)
                {
                    results[p].Outcome = "not-requested; single current-preset geometry mode";
                    for (int l = 0; l < legCount; l++) legResults[p * legCount + l].Outcome = "not-requested";
                }
            }
            receipt = new Receipt {
                StartedUtc = DateTime.UtcNow.ToString("o"), Scene = gameObject.scene.name,
                UnityVersion = Application.unityVersion, Platform = Application.platform.ToString(),
                GraphicsApi = SystemInfo.graphicsDeviceType.ToString(), Arena = arena, Configuration = configuration,
                GeometryOnly = configuration.AllowUnfocusedGeometryOnly,
                FrameTimingCollected = !configuration.AllowUnfocusedGeometryOnly,
                RequestedPresetCount = configuration.AllowUnfocusedGeometryOnly ? 1 : Presets
            };
            if (configuration.AllowUnfocusedGeometryOnly)
            {
                receipt.TimingScope = "GEOMETRY ONLY / TIMINGS NOT ACCEPTANCE: caller explicitly allowed an unfocused native tour. One existing preset is retained; collision traversal uses up to eight native1/60s substeps per update. No frame intervals or draw counters are collected. Rendering and focus-gated pooled-light selection are unverified.";
                receipt.CoverageScope += " Background-capable mode does not verify camera rendering or nearest light selection and ignores FrameCapOverride.";
            }
            try
            {
                CaptureAndFreeze();
                CreateCalibration();
                RenderPipelineManager.endCameraRendering += CameraRendered;
                IsRunning = true;
                previousFrameTime = Time.realtimeSinceStartupAsDouble;
                preset = -1;
                BeginNextPreset();
                return true;
            }
            catch (Exception error)
            {
                IsRunning = true; // Route all partially initialized exits through one cleanup path.
                Finish("Setup failed: " + error.GetType().Name + ": " + error.Message);
                return false;
            }
        }

        public void Cancel() { if (IsRunning) Finish("Cancelled by caller; unvisited legs remain explicit."); }

        private bool Refuse(string message) { Status = message; return false; }
        private static Options Sanitize(Options source)
        {
            source = source ?? new Options();
            return new Options {
                WarmupSeconds = Finite(source.WarmupSeconds) ? Mathf.Clamp(source.WarmupSeconds, 1f, 10f) : 2f,
                MaximumSecondsPerPreset = Finite(source.MaximumSecondsPerPreset) ? Mathf.Clamp(source.MaximumSecondsPerPreset, 10f, 240f) : 90f,
                SpeedMetresPerSecond = Finite(source.SpeedMetresPerSecond) ? Mathf.Clamp(source.SpeedMetresPerSecond, 1f, 6.2f) : 4.2f,
                FrameCapOverride = source.FrameCapOverride == 0 || source.FrameCapOverride == 30 || source.FrameCapOverride == 60 ? source.FrameCapOverride : -1,
                AllowUnfocusedGeometryOnly = source.AllowUnfocusedGeometryOnly
            };
        }

        private void CaptureAndFreeze()
        {
            priorStarted = session.HasStarted; priorPaused = session.Paused;
            GraphicsPreferences g = session.Graphics;
            savedPreset = g.Preset; savedScale = g.ResolutionScale; savedBloom = g.BloomEnabled;
            savedAdaptive = g.AutoResolutionEnabled; savedBrightness = g.Brightness; savedParticles = g.ParticleQuality;
            savedFrameCap = g.FrameRateCap; savedBlocks = g.RenderDistanceBlocks; savedDetail = g.FarDetailPixelation;
            savedVSync = QualitySettings.vSyncCount; savedTargetRate = Application.targetFrameRate;
            preferences = CapturePreferences();
            savedPosition = player.transform.position; savedRotation = player.transform.rotation;
            savedViewPosition = player.ViewRoot.localPosition; savedViewRotation = player.ViewRoot.localRotation;
            savedStepOffset = controller.stepOffset;
            savedFar = camera.farClipPlane; savedNear = camera.nearClipPlane; savedFov = camera.fieldOfView;
            cameraEnabled = camera.enabled;
            openingCamera = session.Opening != null ? session.Opening.ActiveCamera : null;
            if (openingCamera == camera) openingCamera = null;
            if (openingCamera != null) openingEnabled = openingCamera.enabled;
            canvases = session.Hud != null ? session.Hud.GetComponentsInChildren<Canvas>(true) : Array.Empty<Canvas>();
            canvasEnabled = new bool[canvases.Length];
            for (int i = 0; i < canvases.Length; i++) canvasEnabled[i] = canvases[i].enabled;
            firePools = FindObjectsByType<TutorialFireLightPool>(FindObjectsInactive.Include);
            captured = true;
            // Leave GameSession enabled: disabling it clears the public singleton.
            // Its own HasStarted gates input/portal/outcomes; actors see SimulationActive=false.
            session.HasStarted = false; session.Paused = true;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            Hold(player);
            // OnDisable removes transient damage feedback. Preserve that base view
            // rather than reintroducing a stale shake offset when the tour returns.
            savedViewPosition = player.ViewRoot.localPosition; savedViewRotation = player.ViewRoot.localRotation;
            Hold(session.Viewmodel); Hold(session.Tutorial); Hold(session.Opening);
            Hold(session.Revival); Hold(session.Builder); Hold(session.Books); Hold(session.Progression);
            Hold(session.Combat); Hold(session.Hud);
            if (openingCamera != null) openingCamera.enabled = false;
            camera.enabled = true;
            for (int i = 0; i < canvases.Length; i++) canvases[i].enabled = false;
            contact = player.gameObject.AddComponent<OracleTourContactProbe>();
        }

        private void Hold(Behaviour value)
        {
            if (value == null) return;
            if (controlCount >= controls.Length) throw new InvalidOperationException("Control snapshot capacity exceeded.");
            controls[controlCount++] = new BehaviourState { Target = value, Enabled = value.enabled };
            value.enabled = false;
        }

        private void CreateCalibration()
        {
            calibrationRoot = new GameObject("Disabled native tour collision calibration") { hideFlags = HideFlags.HideAndDontSave, layer = 2 };
            capsule = calibrationRoot.AddComponent<CapsuleCollider>();
            capsule.enabled = false; capsule.direction = 1; capsule.center = controller.center;
            capsule.height = controller.height; capsule.radius = controller.radius;
            calibrationBox = calibrationRoot.AddComponent<BoxCollider>();
            calibrationBox.enabled = false; calibrationBox.size = Vector3.one * .4f;
            calibrated = Physics.ComputePenetration(capsule, Vector3.zero, Quaternion.identity, calibrationBox,
                new Vector3(controller.radius * .5f, controller.center.y, 0f), Quaternion.identity, out _, out float depth) && depth > .01f;
            if (!calibrated) AddFinding("penetration-metric-unavailable", null, 0f);
        }

        private void Update()
        {
            if (!IsRunning) return;
            try
            {
                if (!Application.isFocused && !configuration.AllowUnfocusedGeometryOnly || Mathf.Abs(Time.timeScale - 1f) > .001f || session == null || world == null ||
                    session.World != world || world.IsCoopArena != arena || player == null || camera == null || !camera.enabled ||
                    session.Rooms != null && (session.Rooms.Connected || session.Rooms.Busy) || session.Defense != null && session.Defense.Running)
                { Finish("Interrupted: focus, time scale, world, camera or live-session context changed."); return; }
                if (configuration.AllowUnfocusedGeometryOnly && session.Graphics.Preset != savedPreset)
                { Finish("Interrupted: the caller changed the retained geometry-only preset."); return; }
                double now = Time.realtimeSinceStartupAsDouble;
                double elapsed = now - previousFrameTime;
                previousFrameTime = now;
                if (phase == Phase.Walk && !configuration.AllowUnfocusedGeometryOnly)
                {
                    if (skipIntervals > 0) skipIntervals--;
                    else if (intervalCounts[preset] < MaximumFramesPerPreset) intervals[preset][intervalCounts[preset]++] = (float)elapsed;
                    else droppedIntervals[preset]++;
                }
                if (now - presetStarted >= configuration.MaximumSecondsPerPreset)
                {
                    if (phase != Phase.Warmup && legIndex < legCount) EndLeg("preset-time-limit", now);
                    EndPreset("time-limit; remaining legs unvisited", now);
                    BeginNextPreset(); return;
                }
                if (phase == Phase.Warmup)
                {
                    if (now - phaseStarted >= configuration.WarmupSeconds) { results[preset].RenderedCameraFrames = 0; legIndex = -1; StartNextLeg(now); }
                    return;
                }
                float dt = (float)Math.Max(0d, Math.Min(.033333d, elapsed));
                if (phase == Phase.Settle)
                {
                    collisionFlags = controller.Move(Vector3.down * 2f * dt);
                    results[preset].NativeControllerMoveCalls++;
                    if (now - phaseStarted >= .25d) { phase = Phase.Walk; legStarted = now; simulatedLegSeconds = nextGeometrySample = 0f; skipIntervals = 2; previousFrameTime = now; nextSample = now; }
                    return;
                }
                if (configuration.AllowUnfocusedGeometryOnly)
                {
                    // Background Editor pacing can be sparse. These are bounded
                    // real PhysX Move calls at a fixed simulation step, not elapsed
                    // playback time or rendered frames. Never feed them to FPS data.
                    for (int step = 0; step < 8 && IsRunning && phase == Phase.Walk; step++)
                        Traverse(now, 1f / 60f, -1f);
                    return;
                }
                Traverse(now, dt, configuration.AllowUnfocusedGeometryOnly ? -1f : (float)(elapsed * 1000d));
            }
            catch (Exception error) { Finish("Runtime fixture error: " + error.GetType().Name + ": " + error.Message); }
        }

        private void BeginNextPreset()
        {
            if (presetPass >= (configuration.AllowUnfocusedGeometryOnly ? 1 : Presets))
            {
                Finish(configuration.AllowUnfocusedGeometryOnly ?
                    "Current-preset geometry tour attempted; timing, rendering and light selection are unverified." :
                    "All four presets attempted; review coverage and findings.");
                return;
            }
            preset = configuration.AllowUnfocusedGeometryOnly ? savedPreset : presetPass;
            presetPass++;
            GraphicsPreferences g = session.Graphics;
            if (!configuration.AllowUnfocusedGeometryOnly)
            {
                g.SetPreset(preset);
                if (configuration.FrameCapOverride >= 0) g.SetFrameRateCap(configuration.FrameCapOverride);
            }
            PresetResult result = results[preset];
            result.Outcome = "running"; result.SceneScale = g.EffectiveResolutionScale;
            result.ShadowDistanceMetres = g.ShadowDistance; result.Adaptive = g.AutoResolutionEnabled;
            result.RequestedFrameCap = g.FrameRateCap; result.NativeTargetFrameRate = Application.targetFrameRate;
            result.VSyncCount = QualitySettings.vSyncCount;
            presetStarted = phaseStarted = previousFrameTime = Time.realtimeSinceStartupAsDouble;
            phase = Phase.Warmup; legIndex = -1;
            Status = result.Name + ": warming prepared scene";
        }

        private void StartNextLeg(double now)
        {
            while (++legIndex < legCount)
            {
                Leg leg = legs[legIndex];
                if (!TryFloor(leg.Start, out RaycastHit floor, out bool saturated) || saturated)
                { legResults[preset * legCount + legIndex].Outcome = saturated ? "unsafe-start-query-saturated" : "unsafe-start-no-floor"; continue; }
                Vector3 feet = new Vector3(leg.Start.x, floor.point.y + .06f, leg.Start.z);
                float depth = InspectPenetration(feet, out bool overlapSaturated, out Collider offender);
                if (overlapSaturated || calibrated && depth > PenetrationThreshold)
                { legResults[preset * legCount + legIndex].Outcome = "unsafe-start-overlap"; AddFinding("unsafe-start-overlap", offender, depth); continue; }
                bool enabled = controller.enabled;
                controller.enabled = false;
                Vector3 direction = leg.End - leg.Start; direction.y = 0f;
                player.transform.SetPositionAndRotation(feet, direction.sqrMagnitude > .001f ? Quaternion.LookRotation(direction) : savedRotation);
                player.ViewRoot.localPosition = Vector3.up * 1.65f;
                player.ViewRoot.localRotation = Quaternion.Euler(8f, 0f, 0f);
                controller.enabled = enabled; Physics.SyncTransforms();
                contact.Clear(); verticalSpeed = -2f; stalledSeconds = travelled = simulatedLegSeconds = nextGeometrySample = 0f;
                priorFeet = feet; phase = Phase.Settle; phaseStarted = now; legStarted = now;
                Status = results[preset].Name + ": " + leg.Label;
                return;
            }
            EndPreset("route attempted", now);
            BeginNextPreset();
        }

        private void Traverse(double now, float dt, float frameMs)
        {
            Leg leg = legs[legIndex];
            Vector3 feet = player.transform.position;
            Vector3 delta = leg.End - feet; delta.y = 0f;
            if (delta.sqrMagnitude < .09f)
            { EndLeg(leg.WallProbe ? "wall-probe-end-reached; inspect missing barrier" : "end-reached", now); StartNextLeg(now); return; }
            if (controller.isGrounded && verticalSpeed < 0f) verticalSpeed = -2f;
            verticalSpeed = Mathf.Max(-38f, verticalSpeed - 24f * dt);
            Vector3 move = delta.normalized * Mathf.Min(configuration.SpeedMetresPerSecond * dt, delta.magnitude);
            collisionFlags = controller.Move(move + Vector3.up * (verticalSpeed * dt));
            results[preset].NativeControllerMoveCalls++;
            simulatedLegSeconds += dt;
            Vector3 after = player.transform.position;
            Vector3 horizontal = after - priorFeet; horizontal.y = 0f;
            travelled += horizontal.magnitude;
            bool side = (collisionFlags & CollisionFlags.Sides) != 0;
            stalledSeconds = horizontal.sqrMagnitude < .000004f ? stalledSeconds + dt : 0f;
            priorFeet = after;
            if (configuration.AllowUnfocusedGeometryOnly)
            {
                if (simulatedLegSeconds >= nextGeometrySample) { Observe(now, -1f); nextGeometrySample = simulatedLegSeconds + .2f; }
            }
            else if (now >= nextSample) { Observe(now, frameMs); nextSample = now + .2d; }
            if (after.y < world.PlayerSpawn.y - 2f)
            { AddFinding("fell-below-world-floor", contact.LastCollider, after.y); EndLeg("fell-below-floor", now); StartNextLeg(now); return; }
            double legClock = configuration.AllowUnfocusedGeometryOnly ? simulatedLegSeconds : now - legStarted;
            if (stalledSeconds >= .5f && legClock > .6d)
            {
                EndLeg(side || contact.LastSideFrame >= Time.frameCount - 2 ?
                    (leg.WallProbe ? "side-contact-observed; inspect collider" : "blocked-before-end") : "stalled-without-side-contact", now);
                StartNextLeg(now); return;
            }
            if (legClock >= OracleNativeTourMath.LegDeadlineSeconds((leg.End - leg.Start).magnitude, configuration.SpeedMetresPerSecond))
            { EndLeg("leg-time-limit", now); StartNextLeg(now); }
        }

        private void EndLeg(string outcome, double now)
        {
            if (preset < 0 || preset >= Presets || legIndex < 0 || legIndex >= legCount) return;
            LegResult result = legResults[preset * legCount + legIndex];
            result.Outcome = outcome; result.ActualEnd = player != null ? player.transform.position : Vector3.zero;
            result.Seconds = (float)(now - legStarted); result.SimulatedSeconds = simulatedLegSeconds; result.TravelledMetres = travelled;
            result.ContactName = contact != null && contact.LastCollider != null ? contact.LastCollider.name : "";
            results[preset].CompletedLegs++;
        }

        private void EndPreset(string outcome, double now)
        {
            results[preset].Outcome = outcome; results[preset].Seconds = (float)(now - presetStarted);
            completedPresets++;
            if (!configuration.AllowUnfocusedGeometryOnly && results[preset].RenderedCameraFrames == 0)
                AddFinding("no-traversal-camera-render-observed", camera, 0f);
        }

        private float PenetrationThreshold => Mathf.Max(.03f, controller.skinWidth * 1.5f);
        private bool Ignore(Collider value) => value == null || value.transform.IsChildOf(player.transform) ||
            calibrationRoot != null && value.transform.IsChildOf(calibrationRoot.transform);

        private bool TryFloor(Vector3 feet, out RaycastHit selected, out bool saturated)
        {
            int count = Physics.RaycastNonAlloc(feet + Vector3.up * .55f, Vector3.down, floorHits, 1.8f, ~0, QueryTriggerInteraction.Ignore);
            saturated = count >= floorHits.Length; selected = default; float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = floorHits[i];
                if (Ignore(hit.collider) || hit.normal.y < .65f || hit.distance >= best) continue;
                best = hit.distance; selected = hit;
            }
            return best < float.MaxValue;
        }

        private float InspectPenetration(Vector3 feet, out bool saturated, out Collider offender)
        {
            offender = null; saturated = false;
            if (!calibrated || capsule == null) return -1f;
            Vector3 center = feet + controller.center;
            float reach = Mathf.Max(0f, controller.height * .5f - controller.radius);
            int count = Physics.OverlapCapsuleNonAlloc(center + Vector3.up * reach, center - Vector3.up * reach,
                controller.radius, overlaps, ~0, QueryTriggerInteraction.Ignore);
            saturated = count >= overlaps.Length;
            float maximum = 0f;
            for (int i = 0; i < count; i++)
            {
                Collider other = overlaps[i];
                if (Ignore(other)) continue;
                if (Physics.ComputePenetration(capsule, feet, Quaternion.identity, other, other.transform.position,
                    other.transform.rotation, out _, out float depth) && depth > maximum)
                { maximum = depth; offender = other; }
            }
            return maximum;
        }

        private void Observe(double now, float frameMs)
        {
            Vector3 feet = player.transform.position;
            bool floor = TryFloor(feet, out RaycastHit hit, out bool floorSaturated);
            float depth = InspectPenetration(feet, out bool overlapSaturated, out Collider offender);
            if (!floor) AddFinding("floor-support-not-found", null, 0f);
            else if (feet.y - hit.point.y > .45f) AddFinding("large-floor-gap", hit.collider, feet.y - hit.point.y);
            if (calibrated && depth > PenetrationThreshold) AddFinding("deep-capsule-penetration", offender, depth);
            if (floorSaturated || overlapSaturated) AddFinding("physics-query-buffer-saturated", null, 0f);
            int activeLights = 0, budgets = 0;
            for (int i = 0; i < firePools.Length; i++)
            {
                TutorialFireLightPool pool = firePools[i];
                if (pool == null || !pool.isActiveAndEnabled) continue;
                activeLights += pool.ActiveLightCount; budgets += pool.LightBudget;
                if (pool.ActiveLightCount > pool.LightBudget) AddFinding("fire-pool-exceeds-live-budget", pool, pool.ActiveLightCount);
            }
            if (sampleCount >= samples.Length) { sampleOverflow = true; return; }
            OraclePerformanceTelemetry telemetry = session.Graphics.PerformanceTelemetry;
            bool counters = !configuration.AllowUnfocusedGeometryOnly && telemetry != null && telemetry.IsObserving && telemetry.DrawCalls >= 0;
            samples[sampleCount++] = new Sample {
                Preset = preset, Leg = legIndex, SecondsInPreset = (float)(now - presetStarted), FrameIntervalMs = frameMs,
                SimulatedSecondsInLeg = simulatedLegSeconds,
                Feet = feet, CameraPosition = camera.transform.position, CameraEuler = camera.transform.eulerAngles,
                FloorFound = floor, FloorGapMetres = floor ? feet.y - hit.point.y : -1f, FloorNormal = floor ? hit.normal : Vector3.zero,
                MaximumPenetrationMetres = depth, OverlapQuerySaturated = overlapSaturated, FloorQuerySaturated = floorSaturated,
                CollisionFlags = (int)collisionFlags, SceneRenderScale = session.Graphics.EffectiveResolutionScale,
                FieldOfView = camera.fieldOfView, FarClipMetres = camera.farClipPlane,
                ApplicationFocused = Application.isFocused,
                ScreenWidth = Screen.width, ScreenHeight = Screen.height, CameraPixelWidth = camera.pixelWidth, CameraPixelHeight = camera.pixelHeight,
                CameraReportedScaledWidth = camera.scaledPixelWidth, CameraReportedScaledHeight = camera.scaledPixelHeight,
                EnabledFireLights = activeLights, FireLightBudget = budgets, CulledChunks = session.Graphics.CulledChunkRenderers,
                RegisteredChunks = session.Graphics.RegisteredChunkRenderers, DrawCountersAvailable = counters,
                ObservedDrawCalls = counters ? telemetry.DrawCalls : -1, ObservedSetPassCalls = counters ? telemetry.SetPassCalls : -1
            };
        }

        private void AddFinding(string kind, UnityEngine.Object target, float value)
        {
            for (int i = 0; i < findingCount; i++)
                if (findings[i].Preset == preset && findings[i].Leg == legIndex && findings[i].Kind == kind && findings[i].Target == target)
                { if (value > findings[i].Value) findings[i].Value = value; return; }
            if (findingCount >= findings.Length) { findingOverflow = true; return; }
            findings[findingCount++] = new Finding { Preset = preset, Leg = legIndex, Kind = kind, Target = target,
                Feet = player != null ? player.transform.position : Vector3.zero, Value = value };
        }

        private void CameraRendered(ScriptableRenderContext context, Camera rendered)
        { if (IsRunning && rendered == camera && preset >= 0 && preset < Presets) results[preset].RenderedCameraFrames++; }

        private void Finish(string termination)
        {
            if (!IsRunning) return;
            IsRunning = false;
            RenderPipelineManager.endCameraRendering -= CameraRendered;
            double now = Time.realtimeSinceStartupAsDouble;
            if (preset >= 0 && preset < Presets && results[preset].Outcome == "running")
            { if (legIndex >= 0 && legIndex < legCount) EndLeg("interrupted", now); EndPreset("interrupted", now); }
            string cleanupError = Restore();
            receipt.Termination = termination; receipt.FinishedUtc = DateTime.UtcNow.ToString("o");
            receipt.AllPresetsAttempted = !configuration.AllowUnfocusedGeometryOnly && completedPresets >= Presets;
            receipt.AllRequestedPresetsAttempted = completedPresets >= (configuration.AllowUnfocusedGeometryOnly ? 1 : Presets);
            receipt.PenetrationMetricCalibrated = calibrated;
            receipt.SamplesTruncated = sampleOverflow; receipt.FindingsTruncated = findingOverflow;
            receipt.CleanupError = cleanupError; receipt.RestoredGraphicsAndPreferenceKeys = cleanupError.Length == 0;
            for (int p = 0; p < Presets; p++)
            {
                results[p].RecordedFrames = intervalCounts[p]; results[p].DroppedFrames = droppedIntervals[p];
                results[p].FrameIntervals = OracleNativeTourMath.Summarize(intervals[p], intervalCounts[p]);
            }
            receipt.Presets = results; receipt.Legs = legResults;
            receipt.Samples = new Sample[sampleCount]; Array.Copy(samples, receipt.Samples, sampleCount);
            receipt.Findings = new FindingReceipt[findingCount];
            for (int i = 0; i < findingCount; i++) receipt.Findings[i] = new FindingReceipt {
                Preset = findings[i].Preset, Leg = findings[i].Leg, Kind = findings[i].Kind,
                ObjectName = findings[i].Target != null ? findings[i].Target.name : "", Feet = findings[i].Feet, Value = findings[i].Value
            };
            try
            {
                using (var file = new FileStream(receiptPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                using (var writer = new StreamWriter(file)) writer.Write(JsonUtility.ToJson(receipt, true));
                ReceiptWritten = true;
                Status = termination + " Receipt written; caller review required.";
            }
            catch (Exception error) { Status = termination + " Receipt write failed: " + error.Message; }
        }

        private string Restore()
        {
            string error = "";
            try
            {
                if (captured && !configuration.AllowUnfocusedGeometryOnly && session != null && session.Graphics != null)
                {
                    GraphicsPreferences g = session.Graphics;
                    g.SetPreset(savedPreset); g.SetResolutionScale(savedScale); g.SetBloom(savedBloom);
                    g.SetBrightness(savedBrightness); g.SetParticleQuality(savedParticles); g.SetFrameRateCap(savedFrameCap);
                    g.SetRenderDistanceBlocks(savedBlocks); g.SetFarDetailPixelation(savedDetail); g.SetAutoResolution(savedAdaptive);
                    if (g.Preset != savedPreset || !Mathf.Approximately(g.ResolutionScale, savedScale) || g.BloomEnabled != savedBloom ||
                        !Mathf.Approximately(g.Brightness, savedBrightness) || g.ParticleQuality != savedParticles ||
                        g.FrameRateCap != savedFrameCap || g.RenderDistanceBlocks != savedBlocks ||
                        !Mathf.Approximately(g.FarDetailPixelation, savedDetail) || g.AutoResolutionEnabled != savedAdaptive)
                        error = "Configured graphics readback differs from the initial snapshot.";
                }
                else if (captured && !configuration.AllowUnfocusedGeometryOnly) error = "Graphics component was destroyed before configured-value restoration.";
            }
            catch (Exception failure) { error = "Graphics restore: " + failure.Message; }
            finally
            {
                try
                {
                    if (captured)
                    {
                        if (!configuration.AllowUnfocusedGeometryOnly) RestorePreferences(preferences);
                        if (!PreferencesMatch(preferences)) error += " Exact preference readback differs from the initial snapshot.";
                    }
                }
                catch (Exception failure) { error += " Preference restore: " + failure.Message; }
                if (captured)
                {
                    try
                    {
                        if (!configuration.AllowUnfocusedGeometryOnly)
                        { QualitySettings.vSyncCount = savedVSync; Application.targetFrameRate = savedTargetRate; }
                        if (player != null && controller != null)
                        {
                            bool enabled = controller.enabled; controller.enabled = false;
                            player.transform.SetPositionAndRotation(savedPosition, savedRotation);
                            if (player.ViewRoot != null) { player.ViewRoot.localPosition = savedViewPosition; player.ViewRoot.localRotation = savedViewRotation; }
                            controller.stepOffset = savedStepOffset; controller.enabled = enabled; Physics.SyncTransforms();
                        }
                        if (camera != null) { camera.enabled = cameraEnabled; camera.fieldOfView = savedFov; camera.nearClipPlane = savedNear; camera.farClipPlane = savedFar; }
                        if (openingCamera != null) openingCamera.enabled = openingEnabled;
                        for (int i = 0; canvases != null && i < canvases.Length; i++) if (canvases[i] != null) canvases[i].enabled = canvasEnabled[i];
                    }
                    catch (Exception failure) { error += " Pose/camera restore: " + failure.Message; }
                    finally
                    {
                        if (session != null) { session.HasStarted = priorStarted; session.Paused = priorStarted ? true : priorPaused; }
                        for (int i = controlCount - 1; i >= 0; i--)
                        {
                            try { if (controls[i].Target != null) controls[i].Target.enabled = controls[i].Enabled; }
                            catch (Exception failure) { error += " Component restore: " + failure.Message; }
                        }
                        // Never synthesize a user gesture or re-lock the pointer. Root resumes explicitly.
                        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
                    }
                }
                if (contact != null) { contact.enabled = false; Destroy(contact); }
                if (calibrationRoot != null) Destroy(calibrationRoot);
                captured = false;
            }
            return error;
        }

        private void BuildRoute()
        {
            if (!arena)
            {
                if (world.MapRooms == null || world.MapRooms.Length == 0) return;
                Rect hall = world.MapRooms[0]; float near = Mathf.Max(hall.yMin + 3f, world.PlayerSpawn.z);
                float far = hall.yMax - 5f;
                AddLeg("West hall floor seams", new Vector3(-2.8f, 0f, near), new Vector3(-2.8f, 0f, far), false);
                AddLeg("East hall floor seams", new Vector3(2.8f, 0f, far), new Vector3(2.8f, 0f, near), false);
                for (int i = 1; i < world.MapRooms.Length; i++)
                {
                    Rect room = world.MapRooms[i]; float sign = Mathf.Sign(room.center.x);
                    AddLeg("Alcove threshold " + i, new Vector3(sign * 3f, 0f, room.center.y),
                        new Vector3(room.center.x, 0f, room.center.y), false);
                }
                AddLeg("West masonry stop", new Vector3(-2.8f, 0f, 12.5f), new Vector3(-6.8f, 0f, 12.5f), true);
                AddLeg("East masonry stop", new Vector3(2.8f, 0f, 12.5f), new Vector3(6.8f, 0f, 12.5f), true);
                AddLeg("Entry wall stop", new Vector3(2.8f, 0f, near), new Vector3(2.8f, 0f, hall.yMin - 2f), true);
                AddLeg("Rear wall stop", new Vector3(2.8f, 0f, far), new Vector3(2.8f, 0f, hall.yMax + 2f), true);
                return;
            }
            Vector3[] portals = world.InvasionPortals;
            for (int p = 0; portals != null && p < Math.Min(8, portals.Length); p++)
            {
                Vector3 portal = portals[p]; portal.y = 0f;
                Vector3 direction = portal.normalized;
                if (direction.sqrMagnitude < .5f) continue;
                AddLeg("Active lane " + p + " floor", direction * 8.5f, portal - direction * 4f, false);
                Vector3[] pads = world.TowerBuildPads;
                for (int n = 0; pads != null && n < pads.Length; n++)
                {
                    Vector3 pad = pads[n]; pad.y = 0f;
                    if (!world.IsBuildPadActive(pad) || Vector3.Dot(direction, pad.normalized) < .9f) continue;
                    AddLeg("Active pad " + n + " approach", direction * Vector3.Dot(pad, direction), pad, false);
                }
                float outer = Mathf.Min(world.MapBounds.width, world.MapBounds.height) * .5f;
                // Portal frames can stop this leg first: actual collider is recorded for review.
                AddLeg("Active lane " + p + " outer stop", portal - direction * 4f, direction * (outer + 2f), true);
            }
        }

        private void AddLeg(string label, Vector3 start, Vector3 end, bool wall)
        {
            if (legCount >= legs.Length) throw new InvalidOperationException("Route exceeds the fixed64-leg capacity.");
            legs[legCount++] = new Leg { Label = label, Start = start, End = end, WallProbe = wall };
        }

        private static Preference[] CapturePreferences()
        {
            string[] keys = { "Oracle.Graphics.Preset", "Oracle.Graphics.Scale", "Oracle.Graphics.Bloom", "Oracle.Graphics.AutoResolution",
                "Oracle.Graphics.Brightness", "Oracle.Graphics.Particles", "Oracle.Graphics.FrameCap", "Oracle.Graphics.PresetSchema",
                "Oracle.Graphics.RenderDistanceBlocks", "Oracle.Graphics.FarDetailPixelation" };
            var saved = new Preference[keys.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                bool floating = i == 1 || i == 4 || i == 9;
                saved[i] = new Preference { Key = keys[i], Present = PlayerPrefs.HasKey(keys[i]), Float = floating,
                    FloatValue = floating ? PlayerPrefs.GetFloat(keys[i]) : 0f, IntValue = floating ? 0 : PlayerPrefs.GetInt(keys[i]) };
            }
            return saved;
        }

        private static void RestorePreferences(Preference[] saved)
        {
            if (saved == null) return;
            foreach (Preference value in saved)
                if (!value.Present) PlayerPrefs.DeleteKey(value.Key);
                else if (value.Float) PlayerPrefs.SetFloat(value.Key, value.FloatValue);
                else PlayerPrefs.SetInt(value.Key, value.IntValue);
            PlayerPrefs.Save();
        }

        private static bool PreferencesMatch(Preference[] saved)
        {
            if (saved == null) return false;
            foreach (Preference value in saved)
            {
                if (PlayerPrefs.HasKey(value.Key) != value.Present) return false;
                if (value.Present && (value.Float ? !PlayerPrefs.GetFloat(value.Key).Equals(value.FloatValue) : PlayerPrefs.GetInt(value.Key) != value.IntValue))
                    return false;
            }
            return true;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private void OnApplicationPause(bool paused) { if (paused && IsRunning && !configuration.AllowUnfocusedGeometryOnly) Finish("Interrupted by application pause."); }
        private void OnDisable() { if (IsRunning) Finish("Interrupted: fixture disabled or Play stopped."); }
        private void OnDestroy() { RenderPipelineManager.endCameraRendering -= CameraRendered; }
    }

    // Attached temporarily to the actual CharacterController GameObject. It observes
    // contacts from real Move calls; it adds no collider, force, damage or input source.
    public sealed class OracleTourContactProbe : MonoBehaviour
    {
        public Collider LastCollider { get; private set; }
        public int LastSideFrame { get; private set; } = -100;
        public void Clear() { LastCollider = null; LastSideFrame = -100; }
        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (!enabled || hit == null) return;
            if (Mathf.Abs(hit.normal.y) < .65f) { LastCollider = hit.collider; LastSideFrame = Time.frameCount; }
        }
    }
}
#endif
