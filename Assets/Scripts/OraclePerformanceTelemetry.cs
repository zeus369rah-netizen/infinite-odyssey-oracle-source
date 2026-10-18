using System.Globalization;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // Fixed 240-frame data buffers; two capacity-one native engine recorders are disposed on shutdown.
    // Frame ingestion reuses managed state; Unity context reads stay main thread, counters/formatting run at2Hz.
    // No jobs or NativeArray ownership; frame pacing is not isolated CPU/GPU execution time.
    /// <summary>Optional read-only diagnostics. Frame intervals include pacing/waits, not isolated GPU work.</summary>
    [DisallowMultipleComponent]
    public sealed class OraclePerformanceTelemetry : MonoBehaviour
    {
        private const string VisibleKey = "Oracle.Diagnostics.PerformanceVisible";
        private const double RefreshSeconds = .5d;
        private readonly FrameTimeWindow window = new FrameTimeWindow();
        private readonly StringBuilder text = new StringBuilder(640);
        private GameSession session;
        private ProfilerRecorder drawCalls, setPassCalls;
        private TelemetryRenderWindow renderWindow;
        private bool initialized, menuOpen, observing, applicationPaused;
        private int skipIntervals, blocks, registeredChunks, hiddenChunks, materialClones;
        private double refreshAt;
        private string scope = "";

        public bool OverlayVisible { get; private set; }
        public bool IsObserving => observing;
        public int Revision { get; private set; }
        public FrameTimeSnapshot Snapshot { get; private set; }
        public string CompactText { get; private set; } = "Performance display off";
        public string DetailsText { get; private set; } = "Enable the optional performance display to collect a local sample.";
        public long DrawCalls { get; private set; } = -1;
        public long SetPassCalls { get; private set; } = -1;
        public bool HasFreshRenderSample { get; private set; }

        public void Initialize(GameSession owner)
        {
            session = owner;
            if (!initialized)
            {
                initialized = true;
                OverlayVisible = PlayerPrefs.GetInt(VisibleKey, 0) == 1;
            }
            RefreshObservationState();
        }

        public void SetOverlayVisible(bool visible)
        {
            if (OverlayVisible == visible) return;
            OverlayVisible = visible;
            PlayerPrefs.SetInt(VisibleKey, visible ? 1 : 0);
            PlayerPrefs.Save();
            RefreshObservationState();
            refreshAt = 0d;
            Touch();
        }

        public void SetMenuOpen(bool open)
        {
            if (menuOpen == open) return;
            menuOpen = open;
            RefreshObservationState();
            refreshAt = 0d;
        }

        public void ResetObservationWindow()
        {
            window.Clear(); Snapshot = default;
            scope = ""; skipIntervals = 2; refreshAt = 0d;
            DrawCalls = SetPassCalls = -1;
            ResetCounterWindow();
            Touch();
        }

        /// <summary>Optional bridge from the staged distance controller, without a dependency on that feature.</summary>
        public void SetDistanceDiagnostics(int renderDistanceBlocks, int registeredChunkRenderers, int culledChunkRenderers, int privateMaterialCount)
        {
            blocks = Mathf.Clamp(renderDistanceBlocks, 32, 128);
            registeredChunks = Mathf.Clamp(registeredChunkRenderers, 0, 16384);
            hiddenChunks = Mathf.Clamp(culledChunkRenderers, 0, registeredChunks);
            materialClones = Mathf.Clamp(privateMaterialCount, 0, 32);
        }

        private void Update()
        {
            RefreshObservationState();
            if (!observing) return;
            string current = CurrentScope();
            if (current != scope)
            {
                scope = current;
                window.Clear();
                Snapshot = default;
                skipIntervals = 2; // Never attribute the previous mode's final interval to this one.
                DrawCalls = SetPassCalls = -1;
                ResetCounterWindow();
                refreshAt = 0d;
            }
            if (skipIntervals > 0) skipIntervals--;
            else window.Push(Time.unscaledDeltaTime);
            double now = Time.realtimeSinceStartupAsDouble;
            if (now < refreshAt) return;
            refreshAt = now + RefreshSeconds;
            RefreshText();
        }

        private string CurrentScope()
        {
            if (session == null) return "Runtime";
            if (session.Loading != null && session.Loading.IsLoading) return "Preparing arena";
            if (session.Opening != null && (session.Opening.IsIntroPlaying || session.Opening.IsTransitioning)) return "Opening";
            if (!session.HasStarted) return "Main menu";
            if (session.Paused) return "Paused menu";
            if (session.IsDowned) return "Downed";
            return session.CoopMode ? "Shared arena" : "Solo tutorial";
        }

        private void RefreshText()
        {
            GraphicsPreferences graphics = session != null ? session.Graphics : null;
            if (graphics != null) SetDistanceDiagnostics(graphics.RenderDistanceBlocks, graphics.RegisteredChunkRenderers,
                graphics.CulledChunkRenderers, graphics.PrivateDistanceMaterialCount);
            int cap = session != null && session.Graphics != null ? session.Graphics.FrameRateCap : 0;
            Snapshot = window.Snapshot(cap > 0 ? 1000d / cap : 0d);
            HasFreshRenderSample = renderWindow.ConsumeFresh();
            DrawCalls = ReadCounter(drawCalls, HasFreshRenderSample);
            SetPassCalls = ReadCounter(setPassCalls, HasFreshRenderSample);
            var sample = Snapshot;
            text.Clear();
            if (sample.Count == 0) text.Append("Collecting frame intervals...");
            else
            {
                text.Append(sample.FramesPerSecond.ToString("F0", CultureInfo.InvariantCulture)).Append(" FPS  |  ")
                    .Append(sample.AverageMs.ToString("F1", CultureInfo.InvariantCulture)).Append(" ms avg  |  ")
                    .Append(sample.P95Ms.ToString("F1", CultureInfo.InvariantCulture)).Append(" ms P95");
            }
            text.Append('\n').Append(scope).Append("  |  Draws: ");
            AppendCounter(DrawCalls);
            CompactText = text.ToString();
            text.Append("  |  Pass changes: "); AppendCounter(SetPassCalls);
            text.Append('\n').Append(sample.Count).Append('/').Append(FrameTimeWindow.Capacity).Append(" frames over ")
                .Append(sample.WindowSeconds.ToString("F1", CultureInfo.InvariantCulture)).Append(" s; peak ")
                .Append(sample.MaximumMs.ToString("F1", CultureInfo.InvariantCulture)).Append(" ms.");
            text.Append("\nRequested frame cap: ").Append(cap > 0 ? cap.ToString(CultureInfo.InvariantCulture) : "uncapped");
            if (session != null && session.Graphics != null)
                text.Append("; ").Append(session.Graphics.ResolutionMode).Append(": ")
                    .Append((session.Graphics.EffectiveResolutionScale * 100f).ToString("F0", CultureInfo.InvariantCulture)).Append('%');
            if (blocks > 0)
                text.Append("\nDistance requested: ").Append(blocks).Append(" blocks = ").Append(blocks).Append(" m; effective reach ")
                    .Append(graphics != null ? graphics.EffectiveRenderDistanceMetres.ToString("F0", CultureInfo.InvariantCulture) : "unavailable")
                    .Append(" m; chunks hidden: ").Append(hiddenChunks).Append('/').Append(registeredChunks)
                    .Append("; private materials: ").Append(materialClones).Append("/32.");
            text.Append(Application.isEditor ? "\nEditor preview includes Editor overhead/cameras." : "\nRuntime engine counters include all cameras, shadows and UI.");
            text.Append("\nFrame intervals include pacing/waits; they do not identify a GPU bottleneck.");
            if (sample.IgnoredIntervals > 0) text.Append(" Invalid or >5 s intervals excluded: ").Append(sample.IgnoredIntervals).Append('.');
            if (!HasFreshRenderSample) text.Append("\nRender counters await a fresh game-camera frame in this scope.");
            DetailsText = text.ToString();
            Touch();
        }

        private void AppendCounter(long value)
        { if (value < 0) text.Append("unavailable"); else text.Append(value); }

        private static long ReadCounter(ProfilerRecorder recorder, bool freshRender)
        {
            if (!freshRender || !recorder.Valid || !recorder.IsRunning || recorder.Count == 0) return -1;
            long value = recorder.LastValue;
            return value < 0 ? -1 : value;
        }

        private void RefreshObservationState()
        {
            bool wanted = initialized && isActiveAndEnabled && (OverlayVisible || menuOpen) && Application.isFocused && !applicationPaused;
            if (wanted == observing) return;
            observing = wanted;
            window.Clear(); Snapshot = default;
            scope = ""; skipIntervals = 2; refreshAt = 0d;
            DrawCalls = SetPassCalls = -1;
            StopRecorders();
            if (wanted)
            {
                drawCalls = StartCounter("Draw Calls Count");
                setPassCalls = StartCounter("SetPass Calls Count");
                RenderPipelineManager.endCameraRendering += OnGameCameraRendered;
                CompactText = "Collecting frame intervals...";
                DetailsText = CompactText;
            }
            else
            {
                CompactText = OverlayVisible || menuOpen ? "Performance sampling paused" : "Performance display off";
                DetailsText = CompactText;
            }
            Touch();
        }

        // Reset clears samples AND stops collection. Deliberate scope/window resets resume the same owned handle.
        private static void RestartCounter(ref ProfilerRecorder recorder)
        {
            if (!recorder.Valid) return;
            recorder.Reset();
            recorder.Start();
        }
        private void ResetCounterWindow()
        {
            renderWindow.ResetWindow(); HasFreshRenderSample = false;
            RestartCounter(ref drawCalls); RestartCounter(ref setPassCalls);
        }
        private void OnGameCameraRendered(ScriptableRenderContext context, Camera camera)
        {
            if (observing && camera != null && camera.cameraType == CameraType.Game && camera.isActiveAndEnabled)
                renderWindow.RecordGameFrame(Time.frameCount);
        }

        private static ProfilerRecorder StartCounter(string name)
        {
            try { return ProfilerRecorder.StartNew(ProfilerCategory.Render, name, 1); }
            catch (System.Exception) { return default; } // Missing platform counters are an explicit unavailable value.
        }

        private void StopRecorders()
        {
            RenderPipelineManager.endCameraRendering -= OnGameCameraRendered;
            renderWindow.ResetWindow(); HasFreshRenderSample = false;
            if (drawCalls.Valid) drawCalls.Dispose();
            if (setPassCalls.Valid) setPassCalls.Dispose();
            drawCalls = setPassCalls = default;
        }
        private void Touch() { Revision = Revision == int.MaxValue ? 1 : Revision + 1; }
        private void OnEnable() { RefreshObservationState(); }
        private void OnDisable() { RefreshObservationState(); StopRecorders(); }
        private void OnDestroy() { StopRecorders(); }
        private void OnApplicationFocus(bool focused) { RefreshObservationState(); }
        private void OnApplicationPause(bool paused) { applicationPaused = paused; RefreshObservationState(); }
    }

    // # ARCHITECTURE SNAPSHOT
    // Pure frame-epoch gate; no Unity Objects, callbacks, native ownership or allocations.
    // Window reset invalidates old rendered data. One frame can be consumed only once,
    // even if several game cameras render; fresh data must follow both reset and last report.
    internal struct TelemetryRenderWindow
    {
        ulong epoch, requiredEpoch, consumedEpoch;
        int lastFrame;
        bool hasFrame;
        internal void RecordGameFrame(int frame)
        {
            if (hasFrame && frame == lastFrame) return;
            hasFrame = true; lastFrame = frame;
            if (epoch == ulong.MaxValue) { epoch = requiredEpoch = consumedEpoch = 0; }
            epoch++;
        }
        internal void ResetWindow() { requiredEpoch = consumedEpoch = epoch; }
        internal bool ConsumeFresh()
        {
            if (epoch <= requiredEpoch || epoch == consumedEpoch) return false;
            consumedEpoch = epoch;
            return true;
        }
    }

}
