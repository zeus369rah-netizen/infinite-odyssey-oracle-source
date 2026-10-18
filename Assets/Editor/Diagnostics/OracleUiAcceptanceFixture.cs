#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace OdysseyOracle.Editor
{
    // # ARCHITECTURE SNAPSHOT
    // Root-run Editor observer. Bounds are actual Canvas screen pixels, not an OS-window model.
    // The only writes are the three public local-interface preference setters, one per playing
    // frame, including normal restoration. No button, input, economy, combat or save action runs.
    // SessionState holds the original values until restoration succeeds, including across reload.
    public static class OracleUiAcceptanceFixture
    {
        const string PendingKey = "Oracle.UiAcceptance.PendingRestore.v1";
        const string ReportKey = "Oracle.UiAcceptance.Report.v1";
        const int MaxIssuesPerSample = 64, MaxTextPerSample = 320, MaxSamples = 36;
        static readonly float[] Scales = { .75f, 1f, 1.35f };
        static readonly float[] Opacities = { .2f, 1f };
        static readonly string[] Panels = { "StartGamePanel", "CampaignAndRoomsPanel", "PausePanel", "SettingsPanel", "LiveMinimapPreferences", "CompleteControlsPanel", "GraphicsSettingsPanel", "AudioSettingsPanel", "FourElementPassiveTree", "ElementShrineBuildMenu", "PortalJourneyChoices", "RefugeLoreBook", "OutcomePanel" };
        static readonly HashSet<string> ScrollAllowlist = new HashSet<string>(StringComparer.Ordinal)
        { "DesktopSettingsScrollView", "AllActionsScrollView", "GraphicsPreferencesScrollView", "AllTowerTypes", "FocusedDetailsScrollView", "BookScrollView" };
        static readonly Vector3[] Corners = new Vector3[4];
        static Report report;
        static RestoreSnapshot original;
        static GameSession session;
        static OdysseyHud hud;
        static OracleInterfacePreferences preferences;
        static Canvas canvas;
        static RectTransform scope;
        static RectTransform[] transforms;
        static Text[] textComponents;
        static Button[] buttonComponents;
        static int phase, caseIndex, settleFrames, lastFrame;
        static bool running, restoring, restoreOnly;
        static double deadline, restoreReadyAt;
        static string initialScope;
        static int initialWidth, initialHeight;

        [Serializable] public sealed class RestoreSnapshot
        {
            public string capturedUtc;
            public float uiScale, mapOpacity;
            public int mapSize;
        }
        [Serializable] public sealed class Issue
        {
            public string severity, kind, path, text, detail;
        }
        [Serializable] public sealed class AbilityObservation
        {
            public int slot;
            public string name, expectedName, key, expectedKey, state, cooldownCheck;
            public float remaining, fontPixels;
            public bool visible, selected;
        }
        [Serializable] public sealed class PreferenceWrite
        {
            public int frame;
            public string property;
            public float value;
            public bool restoration;
        }
        [Serializable] public sealed class Sample
        {
            public string label, observedUtc, scope, cooldownScope;
            public int frame, screenWidth, screenHeight, textChecked, textMaskedDeferred, textCoveredDeferred, buttonsChecked;
            public float uiScale, mapOpacity, canvasScale, mapWidthPixels, mapHeightPixels, mapGroupAlpha, smallestTextPixels;
            public int mapSize, errors, warnings;
            public bool truncated, mapVisible;
            public Rect viewport;
            public List<Issue> issues = new List<Issue>();
            public List<AbilityObservation> abilities = new List<AbilityObservation>();
        }
        [Serializable] public sealed class Report
        {
            public string startedUtc, finishedUtc, label, status, driverFailure, restoreSaveStatus;
            public bool matrixCompleted, preferencesRestored, restorePending, visitedSurfacePassed;
            public int errors, warnings, driverErrors;
            public RestoreSnapshot original;
            public List<string> visitedPanels = new List<string>();
            public List<string> deferredPanels = new List<string>();
            public List<Sample> samples = new List<Sample>();
            public List<PreferenceWrite> preferenceWrites = new List<PreferenceWrite>();
            public string scopeLimit = "Actual current viewport and visible surface only. No OS resize, panel navigation, auto-click, visual screenshot acceptance, scene-pixel contrast proof or synthesized combat state.";
        }
        [Serializable] sealed class Summary
        {
            public string status, scopeLimit;
            public int samples, errors, warnings;
            public bool running, matrixCompleted, preferencesRestored, restorePending, visitedSurfacePassed;
            public List<string> visitedPanels, deferredPanels;
        }

        public static string BeginMatrix(string label = "current visible UI")
        {
            RequireRuntime();
            if (running) throw new InvalidOperationException("A UI matrix is already running. CancelMatrix() and wait for restoration.");
            if (!string.IsNullOrEmpty(SessionState.GetString(PendingKey, ""))) throw new InvalidOperationException("An earlier preference restore is pending. Run RestorePendingPreferences() first.");
            BindRuntime();
            original = new RestoreSnapshot { capturedUtc = DateTime.UtcNow.ToString("O"), uiScale = preferences.UiScale, mapSize = preferences.MinimapSize, mapOpacity = preferences.MinimapOpacity };
            SessionState.SetString(PendingKey, JsonUtility.ToJson(original));
            report = NewReport(label);
            report.original = original;
            report.restorePending = true;
            restoreOnly = false;
            StartCallbacks();
            return CurrentSummaryJson();
        }

        public static string RunGeometrySelfChecks()
        {
            int assertions = 0;
            void Check(bool value, string label) { assertions++; if (!value) throw new InvalidOperationException("Fixture geometry check failed: " + label); }
            var viewport = new Rect(0, 0, 960, 540);
            Check(Contains(viewport, new Rect(10, 10, 200, 40), 0), "contained label");
            Check(!Contains(viewport, new Rect(-2, 10, 200, 40), 1), "outside label");
            Check(Contains(viewport, new Rect(-1, 10, 200, 40), 1.5f), "rounding tolerance");
            var clipped = Intersection(new Rect(-20, 10, 40, 30), new Rect(0, 0, 100, 100));
            Check(Near(clipped.width, 20) && Near(clipped.height, 30), "partial mask clip");
            Check(!Overlaps(Intersection(new Rect(200, 0, 20, 20), new Rect(0, 0, 100, 100)), viewport), "fully masked content");
            Check(Mathf.Abs(Contrast(Color.white, Color.black) - 21f) < .001f, "black/white contrast");
            Check(Near(Contrast(Color.white, Color.white), 1f), "same-color contrast");
            var over = Over(new Color(1, 1, 1, .5f), Color.black);
            Check(Near(over.r, .5f) && Near(over.g, .5f) && Near(over.b, .5f), "alpha compositing");
            Check(Mathf.Abs(Luminance(new Color(.5f, .5f, .5f, 1f)) - .214041f) < .00001f, "sRGB luminance");
            Check(ScrollAllowlist.Contains("BookScrollView") && ScrollAllowlist.Contains("FocusedDetailsScrollView") && !ScrollAllowlist.Contains("Content"), "narrow mask allowlist");
            return "PASS: " + assertions + " pure fixture math/allowlist assertions; no scene or preference mutation.";
        }

        public static string RestorePendingPreferences()
        {
            RequireRuntime();
            if (running) throw new InvalidOperationException("The current run owns restoration; use CancelMatrix().");
            string snapshot = SessionState.GetString(PendingKey, "");
            if (string.IsNullOrEmpty(snapshot)) return "No pending interface preference restore.";
            BindRuntime();
            original = JsonUtility.FromJson<RestoreSnapshot>(snapshot);
            if (original == null || !Finite(original.uiScale) || original.uiScale < .75f || original.uiScale > 1.35f || original.mapSize < 0 || original.mapSize > 2 || !Finite(original.mapOpacity) || original.mapOpacity < .2f || original.mapOpacity > 1f)
                throw new InvalidOperationException("Pending restore is malformed; no preference was changed.");
            report = NewReport("explicit recovery of interrupted interface preferences");
            report.original = original;
            report.restorePending = true;
            restoreOnly = true;
            StartCallbacks();
            BeginRestore("restoring interrupted preferences");
            return CurrentSummaryJson();
        }

        public static string CancelMatrix()
        {
            if (!running) return CurrentSummaryJson();
            BeginRestore("cancelled; restoring original interface values");
            return CurrentSummaryJson();
        }

        // Read-only one-shot observation, useful after a real shot or a manually visited panel.
        public static string CaptureVisibleNow(string label = "explicit visible observation")
        {
            RequireRuntime();
            if (running) throw new InvalidOperationException("Wait for the active matrix/restoration to finish.");
            BindRuntime();
            report = NewReport(label);
            report.samples.Add(Capture(label));
            report.status = "read-only observation; no preference matrix executed";
            CompleteReport();
            ReleaseReferences();
            return CurrentReportJson();
        }

        public static string CurrentReportJson()
        {
            return report == null ? SessionState.GetString(ReportKey, "{}") : JsonUtility.ToJson(report, true);
        }
        public static string PendingRestoreJson() { return SessionState.GetString(PendingKey, ""); }
        public static string CurrentSummaryJson()
        {
            if (report == null)
            {
                string stored = SessionState.GetString(ReportKey, "");
                if (string.IsNullOrEmpty(stored)) return "No UI audit report. Pending restore: " + !string.IsNullOrEmpty(SessionState.GetString(PendingKey, ""));
                report = JsonUtility.FromJson<Report>(stored);
            }
            return JsonUtility.ToJson(new Summary { status = report.status, running = running, samples = report.samples.Count, errors = report.errors, warnings = report.warnings,
                matrixCompleted = report.matrixCompleted, preferencesRestored = report.preferencesRestored, restorePending = !string.IsNullOrEmpty(SessionState.GetString(PendingKey, "")),
                visitedSurfacePassed = report.visitedSurfacePassed, visitedPanels = report.visitedPanels, deferredPanels = report.deferredPanels, scopeLimit = report.scopeLimit }, true);
        }

        static Report NewReport(string label)
        {
            return new Report { startedUtc = DateTime.UtcNow.ToString("O"), label = string.IsNullOrWhiteSpace(label) ? "visible UI" : label, status = "armed; native observations pending" };
        }
        static void BindRuntime()
        {
            try
            {
            session = GameSession.Instance;
            hud = session != null ? session.Hud : null;
            if (!hud) throw new InvalidOperationException("No live initialized session HUD.");
            var matches = hud.GetComponentsInChildren<OracleInterfacePreferences>(true);
            if (matches.Length != 1 || !matches[0].isActiveAndEnabled) throw new InvalidOperationException("Need exactly one active public interface-preference component.");
            preferences = matches[0];
            canvas = preferences.GetComponent<Canvas>();
            if (!canvas || !canvas.isActiveAndEnabled || canvas.renderMode != RenderMode.ScreenSpaceOverlay) throw new InvalidOperationException("This fixture supports the actual active overlay Canvas only.");
            if (session.Loading != null && session.Loading.IsLoading || session.Opening != null && (session.Opening.IsIntroPlaying || session.Opening.IsTransitioning) || session.Shop != null && session.Shop.Visible)
                throw new InvalidOperationException("Loading, opening transitions and the separate shop modal are outside this stable HUD audit. Wait or close through ordinary UI.");
            transforms = canvas.GetComponentsInChildren<RectTransform>(true);
            textComponents = canvas.GetComponentsInChildren<Text>(true);
            buttonComponents = canvas.GetComponentsInChildren<Button>(true);
            if (transforms.Length > 8192 || textComponents.Length > 2048 || buttonComponents.Length > 512) throw new InvalidOperationException("Current hierarchy exceeds the finite audit bound.");
            scope = CurrentScope();
            initialScope = scope.name;
            initialWidth = Screen.width; initialHeight = Screen.height;
            }
            catch { ReleaseReferences(); throw; }
        }
        static void RequireRuntime()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPaused)
                throw new InvalidOperationException("Run in a settled, unpaused Editor Play loop. Game pause menus are supported; Editor Pause is not.");
        }
        static void StartCallbacks()
        {
            phase = 0; caseIndex = 0; settleFrames = 0; lastFrame = -1; restoring = false; running = true;
            deadline = EditorApplication.timeSinceStartup + 90d;
            EditorApplication.update -= Update;
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged -= PlayStateChanged;
            EditorApplication.playModeStateChanged += PlayStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= BeforeReload;
            AssemblyReloadEvents.beforeAssemblyReload += BeforeReload;
            SaveReport();
        }
        static void Update()
        {
            if (!running) return;
            if (!EditorApplication.isPlaying || !preferences || !hud || !canvas) { Interrupted("Runtime objects disappeared; original values remain in PendingRestoreJson."); return; }
            if (EditorApplication.timeSinceStartup > deadline) { Interrupted("Frame/restore deadline reached. Restore explicitly after Play resumes; no success claimed."); return; }
            if (EditorApplication.isPaused || Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;
            try
            {
                if (restoring) { RestoreFrame(); return; }
                if (CurrentScope().name != initialScope) throw new InvalidOperationException("Visible panel changed during the matrix. Run each panel separately.");
                if (Screen.width != initialWidth || Screen.height != initialHeight) throw new InvalidOperationException("Actual viewport changed during this matrix; do not combine separate viewport evidence.");
                if (session.Loading != null && session.Loading.IsLoading || session.Shop != null && session.Shop.Visible) throw new InvalidOperationException("Loading/shop modal interrupted the visible HUD surface.");
                int scaleIndex = caseIndex / 6, mapSize = caseIndex / 2 % 3, opacityIndex = caseIndex % 2;
                switch (phase)
                {
                    case 0: WriteScale(Scales[scaleIndex]); phase++; break;
                    case 1: WriteMapSize(mapSize); phase++; break;
                    case 2: WriteOpacity(Opacities[opacityIndex]); settleFrames = 2; phase++; break;
                    case 3: if (--settleFrames <= 0) phase++; break;
                    case 4:
                        var sample = Capture("scale " + Scales[scaleIndex].ToString("0.00", CultureInfo.InvariantCulture) + " / map " + mapSize + " / opacity " + Opacities[opacityIndex].ToString("0.00", CultureInfo.InvariantCulture));
                        if (!Near(sample.uiScale, Scales[scaleIndex]) || sample.mapSize != mapSize || !Near(sample.mapOpacity, Opacities[opacityIndex])) Add(sample, "error", "preference-application", "OracleInterface", "Actual public preference getters do not match the requested case.");
                        report.samples.Add(sample);
                        caseIndex++; phase = 0;
                        if (caseIndex >= 18) { report.matrixCompleted = true; BeginRestore("matrix sampled; restoring original interface values"); }
                        SaveReport();
                        break;
                }
            }
            catch (Exception ex)
            {
                if (restoring) Interrupted("Restore failed: " + ex.Message);
                else { report.driverErrors++; report.driverFailure = ex.Message; BeginRestore("audit interrupted: " + ex.Message + "; restoring originals"); }
            }
        }
        static void BeginRestore(string status)
        {
            restoring = true; phase = 0; report.status = status;
            deadline = EditorApplication.timeSinceStartup + 20d;
            SaveReport();
        }
        static void WriteScale(float value) { preferences.SetUiScale(value); RecordWrite("UiScale", value); }
        static void WriteMapSize(int value) { preferences.SetMinimapSize(value); RecordWrite("MinimapSize", value); }
        static void WriteOpacity(float value) { preferences.SetMinimapOpacity(value); RecordWrite("MinimapOpacity", value); }
        static void RecordWrite(string property, float value)
        {
            if (report.preferenceWrites.Count >= 72) throw new InvalidOperationException("Preference-write bound reached.");
            if (report.preferenceWrites.Count > 0 && report.preferenceWrites[report.preferenceWrites.Count - 1].frame == Time.frameCount)
                throw new InvalidOperationException("More than one preference setter reached the same engine frame.");
            report.preferenceWrites.Add(new PreferenceWrite { frame = Time.frameCount, property = property, value = value, restoration = restoring });
        }
        static void RestoreFrame()
        {
            switch (phase)
            {
                case 0: WriteScale(original.uiScale); phase++; break;
                case 1: WriteMapSize(original.mapSize); phase++; break;
                case 2: WriteOpacity(original.mapOpacity); restoreReadyAt = EditorApplication.timeSinceStartup + .4d; phase++; break;
                case 3:
                    if (EditorApplication.timeSinceStartup < restoreReadyAt) return;
                    Canvas.ForceUpdateCanvases();
                    if (!Near(preferences.UiScale, original.uiScale) || preferences.MinimapSize != original.mapSize || !Near(preferences.MinimapOpacity, original.mapOpacity))
                        throw new InvalidOperationException("Public interface values did not restore.");
                    var scaler = canvas.GetComponent<CanvasScaler>();
                    if (!scaler || Vector2.Distance(scaler.referenceResolution, OracleInterfacePreferences.BaseReferenceResolution / original.uiScale) > .01f)
                        throw new InvalidOperationException("Actual Canvas reference resolution did not restore.");
                    var map = Find(canvas.transform, "CatacombMap");
                    var group = map ? map.GetComponent<CanvasGroup>() : null;
                    if (!group || !Near(group.alpha, original.mapOpacity)) throw new InvalidOperationException("Actual map alpha did not restore.");
                    report.preferencesRestored = true;
                    report.restorePending = false;
                    report.restoreSaveStatus = preferences.SaveStatus;
                    SessionState.EraseString(PendingKey);
                    report.status = restoreOnly ? "original interface values restored; no matrix claimed" : report.matrixCompleted ? "matrix completed and original interface values restored" : "audit cancelled/interrupted; original interface values restored";
                    CompleteReport();
                    Detach();
                    break;
            }
        }
        static void Interrupted(string why)
        {
            if (report != null) { report.status = why; report.restorePending = true; report.visitedSurfacePassed = false; report.finishedUtc = DateTime.UtcNow.ToString("O"); SaveReport(); }
            Detach();
        }
        static void PlayStateChanged(PlayModeStateChange state) { if (state == PlayModeStateChange.ExitingPlayMode) Interrupted("Play stopped before normal restoration completed. Original values retained for explicit recovery."); }
        static void BeforeReload() { if (running) Interrupted("Assembly reload interrupted the audit. Original values retained for explicit recovery."); }
        static void Detach()
        {
            EditorApplication.update -= Update;
            EditorApplication.playModeStateChanged -= PlayStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= BeforeReload;
            running = false; restoring = false;
            ReleaseReferences();
        }
        static void ReleaseReferences() { session = null; hud = null; preferences = null; canvas = null; scope = null; transforms = null; textComponents = null; buttonComponents = null; }

        static RectTransform CurrentScope()
        {
            Transform root = canvas.transform;
            foreach (string name in Panels)
            {
                var panel = Find(root, name);
                if (panel && panel.gameObject.activeInHierarchy && EffectiveAlpha(panel) > .01f) return (RectTransform)panel;
            }
            var adventure = Find(root, "AdventureHUD");
            return adventure && adventure.gameObject.activeInHierarchy ? (RectTransform)adventure : (RectTransform)root;
        }
        static Sample Capture(string label)
        {
            Canvas.ForceUpdateCanvases();
            scope = CurrentScope();
            var sample = new Sample { label = label, observedUtc = DateTime.UtcNow.ToString("O"), frame = Time.frameCount, screenWidth = Screen.width, screenHeight = Screen.height,
                viewport = canvas.pixelRect, scope = scope.name, uiScale = preferences.UiScale, mapSize = preferences.MinimapSize, mapOpacity = preferences.MinimapOpacity,
                canvasScale = canvas.scaleFactor, smallestTextPixels = float.MaxValue };
            if (!report.visitedPanels.Contains(scope.name)) report.visitedPanels.Add(scope.name);
            if (sample.viewport.width <= 0 || sample.viewport.height <= 0) Add(sample, "error", "viewport", scope.name, "Actual Canvas pixelRect is empty.");
            if (!Contains(sample.viewport, ScreenRect(scope), 1.5f)) Add(sample, "error", "surface-outside-viewport", Path(scope), "Visible panel/surface extends beyond the actual viewport.");
            foreach (var text in textComponents)
            {
                if (!text || !text.isActiveAndEnabled || string.IsNullOrWhiteSpace(text.text) || EffectiveAlpha(text.transform) * text.color.a <= .01f) continue;
                if (!text.transform.IsChildOf(scope) && text.transform != scope) { sample.textCoveredDeferred++; continue; }
                Rect full = ScreenRect(text.rectTransform), clipped = ClipByMasks(text.transform, full);
                if (!Overlaps(clipped, sample.viewport) || text.canvasRenderer.cull) { sample.textMaskedDeferred++; continue; }
                if (++sample.textChecked > MaxTextPerSample) { sample.truncated = true; Add(sample, "error", "text-bound", scope.name, "Text-check bound reached; this surface is incomplete."); break; }
                bool scrollContent = InAllowedScrollContent(text.transform);
                if (!Contains(sample.viewport, full, 1.5f) && !scrollContent) Add(sample, "error", "text-outside-viewport", Path(text.transform), "Unmasked text rect extends outside the actual viewport.", text.text);
                float pixelSize = FontPixels(text);
                sample.smallestTextPixels = Mathf.Min(sample.smallestTextPixels, pixelSize);
                if (pixelSize < 11f) Add(sample, "warning", "small-rendered-type", Path(text.transform), "Estimated rendered font height " + pixelSize.ToString("0.0", CultureInfo.InvariantCulture) + "px; inspect at the actual viewport.", text.text);
                float available = text.rectTransform.rect.height;
                float preferred = text.preferredHeight;
                if (text.verticalOverflow == VerticalWrapMode.Truncate && preferred > available + 1.5f)
                    Add(sample, "error", "text-truncated-in-own-rect", Path(text.transform), "Preferred height " + preferred.ToString("0.0", CultureInfo.InvariantCulture) + " exceeds own rect height " + available.ToString("0.0", CultureInfo.InvariantCulture) + ". Intentional outer scrolling does not excuse clipping inside a label.", text.text);
                if (text.horizontalOverflow == HorizontalWrapMode.Overflow && text.preferredWidth > text.rectTransform.rect.width + 1.5f)
                    Add(sample, "warning", "text-spills-own-width", Path(text.transform), "Unwrapped text exceeds its own label width.", text.text);
                if (HasUnknownMask(text.transform)) Add(sample, "warning", "unreviewed-mask", Path(text.transform), "Mask clipping is not in the narrow known scroll/map allowlist; inspect it rather than declaring overflow or success.", text.text);
                CheckContrast(text, sample);
            }
            if (sample.smallestTextPixels == float.MaxValue) sample.smallestTextPixels = 0f;
            foreach (var button in buttonComponents)
            {
                if (!button.isActiveAndEnabled || EffectiveAlpha(button.transform) <= .01f) continue;
                if (button.transform != scope && !button.transform.IsChildOf(scope)) continue;
                Rect bounds = ScreenRect((RectTransform)button.transform), clipped = ClipByMasks(button.transform, bounds);
                if (!Overlaps(clipped, sample.viewport)) continue;
                sample.buttonsChecked++;
                if (!Contains(sample.viewport, bounds, 1.5f) && !InAllowedScrollContent(button.transform)) Add(sample, "error", "button-outside-viewport", Path(button.transform), "Visible button extends beyond the actual viewport. No click was sent.");
            }
            CheckMap(sample);
            CheckAbilities(sample);
            CheckHudSeparation(sample);
            CheckDetailsMask(sample);
            return sample;
        }

        static void CheckMap(Sample sample)
        {
            var map = Find(canvas.transform, "CatacombMap") as RectTransform;
            if (!map) { Add(sample, "error", "map-missing", "CatacombMap", "Expected actual minimap hierarchy is missing."); return; }
            Rect rect = ScreenRect(map);
            sample.mapWidthPixels = rect.width; sample.mapHeightPixels = rect.height;
            var group = map.GetComponent<CanvasGroup>();
            sample.mapGroupAlpha = group ? group.alpha : 1f;
            sample.mapVisible = map.gameObject.activeInHierarchy && scope.name == "AdventureHUD" && Overlaps(rect, sample.viewport);
            if (!Near(sample.mapGroupAlpha, preferences.MinimapOpacity)) Add(sample, "error", "map-opacity-not-applied", Path(map), "Actual map CanvasGroup alpha does not match the public preference.");
            if (sample.mapVisible && !Contains(sample.viewport, rect, 1.5f)) Add(sample, "error", "map-outside-viewport", Path(map), "The real map frame extends beyond this viewport.");
            if (!sample.mapVisible) sample.cooldownScope = "Map/ability display may be covered by the active menu; geometry/state is recorded without a visual-readability pass.";
        }
        static void CheckAbilities(Sample sample)
        {
            var abilities = Find(canvas.transform, "ElementAbilities");
            bool visible = abilities && abilities.gameObject.activeInHierarchy && scope.name == "AdventureHUD" && EffectiveAlpha(abilities) > .01f;
            bool observedCooldown = false;
            if (!abilities || session.Progression == null || session.Combat == null) { sample.cooldownScope = "Deferred: actual element/combat hierarchy is unavailable."; return; }
            for (int i = 0; i < 4; i++)
            {
                Transform slot = abilities.Find("Spell" + (i + 1));
                var name = slot ? ChildText(slot, "SpellName") : null;
                var key = slot ? ChildText(slot, "Key") : null;
                var state = slot ? ChildText(slot, "CooldownOrCharge") : null;
                float remaining = session.Combat.ElementSwitchCooldowns != null && i < session.Combat.ElementSwitchCooldowns.Length ? session.Combat.ElementSwitchCooldowns[i] : 0f;
                var item = new AbilityObservation { slot = i, expectedName = session.Progression.ElementNames[i], expectedKey = AbilityBindings.GetLabel(i), name = name ? name.text : "", key = key ? key.text : "", state = state ? state.text : "", remaining = remaining,
                    selected = session.Progression.SelectedElement == i, visible = visible, fontPixels = name ? FontPixels(name) : 0f };
                sample.abilities.Add(item);
                if (!visible) { item.cooldownCheck = "deferred: ability strip inactive or covered"; continue; }
                if (!name || !key || !state) { Add(sample, "error", "ability-label-missing", slot ? Path(slot) : "Spell" + (i + 1), "Expected public Text components are missing."); continue; }
                if (!name.text.StartsWith(item.expectedName + "  Lv ", StringComparison.Ordinal)) Add(sample, "error", "ability-name", Path(name.transform), "Element name/level label does not match the live progression element.", name.text);
                if (key.text != item.expectedKey) Add(sample, "error", "ability-binding", Path(key.transform), "Displayed key does not match the current binding.", key.text);
                bool preview = name.text.Contains("GUIDE PREVIEW") || name.text.Contains("AVAILABLE");
                if (preview) { item.cooldownCheck = "read-only guide preview; no combat cooldown claimed"; continue; }
                if (remaining > .12f)
                {
                    observedCooldown = true;
                    float shown;
                    bool number = float.TryParse(state.text, NumberStyles.Float, CultureInfo.CurrentCulture, out shown) || float.TryParse(state.text, NumberStyles.Float, CultureInfo.InvariantCulture, out shown);
                    if (!number || Mathf.Abs(shown - remaining) > Mathf.Max(.2f, Time.unscaledDeltaTime * 2f + .11f)) Add(sample, "error", "ability-cooldown-hidden-or-stale", Path(state.transform), "Real recovery is " + remaining.ToString("0.00", CultureInfo.InvariantCulture) + "s but the visible number is absent/stale; selected=" + item.selected + ".", state.text);
                    if (remaining > OracleCombat.AbilityCooldownSeconds + .11f || !Near(OracleCombat.AbilityCooldownSeconds, 4f)) Add(sample, "error", "ability-four-second-contract", Path(state.transform), "Observed recovery exceeds or differs from the actual four-second contract.");
                    item.cooldownCheck = number ? "actual recovery observed; numeric display compared" : "actual recovery observed; display missing";
                }
                else item.cooldownCheck = "deferred: no live recovery at this sample; no shot was synthesized";
            }
            sample.cooldownScope = !visible ? "Ability strip inactive/covered; name/key state recorded without a visual pass." : observedCooldown ? "Real recovery observed, including selected-slot comparison; no combat state was changed." : "No live recovery observed. Trigger a legitimate shot separately and use CaptureVisibleNow for that actual state.";
        }
        static void CheckHudSeparation(Sample sample)
        {
            if (scope.name != "AdventureHUD") return;
            string[,] pairs = { { "OracleVitals", "ElementAbilities" }, { "ShrineHealth", "ElementAbilities" }, { "OracleExperienceSlider", "OracleVitals" }, { "OracleExperienceSlider", "ElementAbilities" } };
            for (int i = 0; i < pairs.GetLength(0); i++)
            {
                var a = Find(scope, pairs[i, 0]) as RectTransform; var b = Find(scope, pairs[i, 1]) as RectTransform;
                if (!a || !b || !a.gameObject.activeInHierarchy || !b.gameObject.activeInHierarchy) continue;
                Rect overlap = Intersection(ScreenRect(a), ScreenRect(b));
                if (overlap.width > 2f && overlap.height > 2f) Add(sample, "error", "major-hud-overlap", Path(a), "Overlaps " + Path(b) + " by " + overlap.width.ToString("0.0", CultureInfo.InvariantCulture) + "x" + overlap.height.ToString("0.0", CultureInfo.InvariantCulture) + "px.");
            }
        }
        static void CheckDetailsMask(Sample sample)
        {
            if (scope.name != "FourElementPassiveTree") return;
            var view = Find(scope, "FocusedDetailsScrollView");
            var viewport = view ? view.Find("Viewport") : null;
            var mask = viewport ? viewport.GetComponent<Mask>() : null;
            var image = viewport ? viewport.GetComponent<Image>() : null;
            if (!mask || !image || image.color.a < .99f || image.color.r < .99f || image.color.g < .99f || image.color.b < .99f || mask.showMaskGraphic) Add(sample, "error", "tree-details-stencil-regression", viewport ? Path(viewport) : scope.name, "The known details viewport requires the real opaque white, hidden mask graphic; low alpha previously hid body text despite valid Text bounds.");
        }

        static void CheckContrast(Text text, Sample sample)
        {
            var stack = new List<Image>();
            bool textured = false;
            for (Transform current = text.transform.parent; current != null; current = current.parent)
            {
                var image = current.GetComponent<Image>();
                var mask = current.GetComponent<Mask>();
                if (image && image.isActiveAndEnabled && (!mask || mask.showMaskGraphic))
                {
                    if (image.sprite != null) textured = true;
                    else stack.Add(image);
                }
                if (current == canvas.transform) break;
            }
            Color dark = Color.black, light = Color.white;
            for (int i = stack.Count - 1; i >= 0; i--)
            {
                var image = stack[i];
                Color color = image.color;
                color.a *= EffectiveAlpha(image.transform);
                dark = Over(color, dark); light = Over(color, light);
            }
            Color ink = text.color;
            ink.a *= EffectiveAlpha(text.transform);
            float onDark = Contrast(Over(ink, dark), dark), onLight = Contrast(Over(ink, light), light);
            if (!textured && Mathf.Min(onDark, onLight) < 4.5f)
                Add(sample, "warning", "estimated-contrast-risk", Path(text.transform), "Logical ancestor-panel estimate: " + onDark.ToString("0.00", CultureInfo.InvariantCulture) + ":1 over black, " + onLight.ToString("0.00", CultureInfo.InvariantCulture) + ":1 over white. Scene pixels, sprites, shadows, reveal alpha and color pipeline are not measured; screenshot review remains required.", text.text);
        }
        static Color Over(Color foreground, Color background)
        {
            float alpha = Mathf.Clamp01(foreground.a);
            return new Color(foreground.r * alpha + background.r * (1f - alpha), foreground.g * alpha + background.g * (1f - alpha), foreground.b * alpha + background.b * (1f - alpha), 1f);
        }
        static float Contrast(Color a, Color b) { float x = Luminance(a), y = Luminance(b); return (Mathf.Max(x, y) + .05f) / (Mathf.Min(x, y) + .05f); }
        static float Luminance(Color color) { return .2126f * Linear(color.r) + .7152f * Linear(color.g) + .0722f * Linear(color.b); }
        static float Linear(float value) { value = Mathf.Clamp01(value); return value <= .04045f ? value / 12.92f : Mathf.Pow((value + .055f) / 1.055f, 2.4f); }
        static float EffectiveAlpha(Transform transform)
        {
            float alpha = 1f;
            for (var current = transform; current != null; current = current.parent)
            {
                bool stop = false;
                foreach (var group in current.GetComponents<CanvasGroup>())
                { if (!group.enabled) continue; alpha *= group.alpha; stop |= group.ignoreParentGroups; }
                if (stop) break;
            }
            return alpha;
        }
        static float FontPixels(Text text)
        {
            int size = text.resizeTextForBestFit ? text.cachedTextGenerator.fontSizeUsedForBestFit : text.fontSize;
            Vector2 a = RectTransformUtility.WorldToScreenPoint(null, text.rectTransform.TransformPoint(Vector3.zero));
            Vector2 b = RectTransformUtility.WorldToScreenPoint(null, text.rectTransform.TransformPoint(Vector3.up * size));
            return Vector2.Distance(a, b);
        }
        static Rect ScreenRect(RectTransform rect)
        {
            rect.GetWorldCorners(Corners);
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < 4; i++) { var point = RectTransformUtility.WorldToScreenPoint(null, Corners[i]); min = Vector2.Min(min, point); max = Vector2.Max(max, point); }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
        static Rect ClipByMasks(Transform transform, Rect rect)
        {
            for (var current = transform.parent; current != null; current = current.parent)
            {
                var mask = current.GetComponent<Mask>(); var rectMask = current.GetComponent<RectMask2D>();
                if (mask && mask.isActiveAndEnabled || rectMask && rectMask.isActiveAndEnabled) rect = Intersection(rect, ScreenRect((RectTransform)current));
                if (current == canvas.transform) break;
            }
            return rect;
        }
        static bool InAllowedScrollContent(Transform transform)
        {
            for (var current = transform.parent; current != null; current = current.parent)
            {
                var scroll = current.GetComponent<ScrollRect>();
                if (scroll && scroll.isActiveAndEnabled && ScrollAllowlist.Contains(scroll.name) && scroll.viewport && scroll.content && (transform == scroll.content || transform.IsChildOf(scroll.content))) return true;
                if (current == canvas.transform) break;
            }
            return false;
        }
        static bool HasUnknownMask(Transform transform)
        {
            if (InAllowedScrollContent(transform)) return false;
            for (var current = transform.parent; current != null; current = current.parent)
            {
                var mask = current.GetComponent<Mask>(); var rectMask = current.GetComponent<RectMask2D>();
                if ((mask && mask.isActiveAndEnabled || rectMask && rectMask.isActiveAndEnabled) && current.name != "ActualDungeonFootprint") return true;
                if (current == canvas.transform) break;
            }
            return false;
        }
        static Rect Intersection(Rect a, Rect b) { float x = Mathf.Max(a.xMin, b.xMin), y = Mathf.Max(a.yMin, b.yMin); return new Rect(x, y, Mathf.Max(0, Mathf.Min(a.xMax, b.xMax) - x), Mathf.Max(0, Mathf.Min(a.yMax, b.yMax) - y)); }
        static bool Contains(Rect outside, Rect inside, float tolerance) { return inside.xMin >= outside.xMin - tolerance && inside.yMin >= outside.yMin - tolerance && inside.xMax <= outside.xMax + tolerance && inside.yMax <= outside.yMax + tolerance; }
        static bool Overlaps(Rect a, Rect b) { return a.width > 0 && a.height > 0 && b.width > 0 && b.height > 0 && a.Overlaps(b); }
        static bool Finite(float number) { return !float.IsNaN(number) && !float.IsInfinity(number); }
        static bool Near(float a, float b) { return Mathf.Abs(a - b) <= .001f; }
        static Text ChildText(Transform parent, string child) { var item = parent.Find(child); return item ? item.GetComponent<Text>() : null; }
        static Transform Find(Transform root, string name)
        {
            if (transforms == null) return null;
            foreach (var child in transforms) if (child && child.name == name && (child == root || child.IsChildOf(root))) return child;
            return null;
        }
        static string Path(Transform transform)
        {
            string path = transform.name;
            for (var parent = transform.parent; parent != null && parent != canvas.transform; parent = parent.parent) path = parent.name + "/" + path;
            return path;
        }
        static void Add(Sample sample, string severity, string kind, string path, string detail, string text = "")
        {
            if (severity == "error") sample.errors++; else sample.warnings++;
            if (sample.issues.Count >= MaxIssuesPerSample) { sample.truncated = true; return; }
            sample.issues.Add(new Issue { severity = severity, kind = kind, path = path, detail = detail, text = text.Length > 140 ? text.Substring(0, 140) + "..." : text });
        }
        static void CompleteReport()
        {
            report.finishedUtc = DateTime.UtcNow.ToString("O");
            report.deferredPanels.Clear();
            foreach (string panel in Panels) if (!report.visitedPanels.Contains(panel)) report.deferredPanels.Add(panel);
            report.errors = report.driverErrors; report.warnings = 0;
            foreach (var sample in report.samples) { report.errors += sample.errors; report.warnings += sample.warnings; if (sample.truncated) report.errors++; }
            if (report.matrixCompleted && report.samples.Count == 18)
            {
                for (int scale = 0; scale < 3; scale++) for (int size = 0; size < 3; size++)
                {
                    var low = report.samples[scale * 6 + size * 2]; var full = report.samples[scale * 6 + size * 2 + 1];
                    if (Mathf.Abs(low.mapWidthPixels - full.mapWidthPixels) > 1f || Mathf.Abs(low.mapHeightPixels - full.mapHeightPixels) > 1f)
                    { Add(full, "error", "map-opacity-changed-geometry", "CatacombMap", "Changing only map alpha unexpectedly changed its dimensions."); report.errors++; }
                    if (size > 0)
                    {
                        var previous = report.samples[scale * 6 + (size - 1) * 2];
                        if (low.mapWidthPixels + 1f < previous.mapWidthPixels || low.mapHeightPixels + 1f < previous.mapHeightPixels)
                        { Add(low, "error", "map-size-order", "CatacombMap", "A larger requested map became smaller. Equal dimensions are allowed when the actual viewport fit caps it."); report.errors++; }
                    }
                }
            }
            report.visitedSurfacePassed = report.matrixCompleted && report.preferencesRestored && report.errors == 0 && report.warnings == 0;
            SaveReport();
        }
        static void SaveReport()
        {
            if (report == null) return;
            if (report.samples.Count > MaxSamples) { report.samples.RemoveRange(MaxSamples, report.samples.Count - MaxSamples); report.status = "sample bound exceeded; incomplete"; report.visitedSurfacePassed = false; }
            report.errors = report.driverErrors; report.warnings = 0;
            foreach (var sample in report.samples) { report.errors += sample.errors; report.warnings += sample.warnings; if (sample.truncated) report.errors++; }
            if (report.errors > 0 || report.warnings > 0) report.visitedSurfacePassed = false;
            SessionState.SetString(ReportKey, JsonUtility.ToJson(report));
        }
    }
}
#endif
