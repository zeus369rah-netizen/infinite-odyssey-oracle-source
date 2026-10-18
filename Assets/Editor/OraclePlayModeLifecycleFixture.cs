#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.Compilation;
using UnityEngine;
using OdysseyOracle.NextRelease;

namespace OdysseyOracle.Editor
{
    /// <summary>Explicit native audit only. Observes root's play/compile guard; never edits source or saves.</summary>
    [InitializeOnLoad]
    public static class OraclePlayModeLifecycleFixture
    {
        const string Key = "Oracle.NativeLifecycleAudit.v1";
        [Serializable] public sealed class Checkpoint
        {
            public string label, utc, failure = "";
            public bool playing, singletonStrictNull, singletonMatchesActive, panelReady;
            public int sessions, activeSessions, panels, players, huds, worlds, staleDelegateTargets, panelInvalidations;
        }
        [Serializable] public sealed class Audit
        {
            public bool armed, passed, enterPlayOptionsEnabled;
            public string startedUtc, lastUtc, status = "not armed";
            public int enteredPlay, exitedPlay, enteredEdit, compilationStarts, compilationFinishes;
            public int beforeReloads, afterReloads, reloadWhilePlaying, compileProbes, forcedCompileProbes, errors, warnings, enterPlayOptions;
            public string[] failures = Array.Empty<string>();
            public Checkpoint[] checkpoints = Array.Empty<Checkpoint>();
        }
        static Audit audit;
        static OraclePlayModeLifecycleFixture()
        {
            Restore();
            // Exact remove/add keeps a single handler in the current domain.
            EditorApplication.playModeStateChanged -= OnPlayMode;
            EditorApplication.playModeStateChanged += OnPlayMode;
            CompilationPipeline.compilationStarted -= OnCompilationStarted;
            CompilationPipeline.compilationStarted += OnCompilationStarted;
            CompilationPipeline.compilationFinished -= OnCompilationFinished;
            CompilationPipeline.compilationFinished += OnCompilationFinished;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeReload;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeReload;
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
        }

        public static string BeginAudit()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Arm the lifecycle audit only after Play has stopped and compilation has settled.");
            audit = new Audit { armed = true, startedUtc = DateTime.UtcNow.ToString("o"), status = "armed; native observations required",
                enterPlayOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled, enterPlayOptions = (int)EditorSettings.enterPlayModeOptions };
            Save();
            return JsonUtility.ToJson(audit, true);
        }

        public static string RecordCheckpoint(string label)
        {
            RequireArmed();
            var record = Capture(string.IsNullOrWhiteSpace(label) ? "checkpoint" : label);
            var records = new List<Checkpoint>(audit.checkpoints);
            if (records.Count < 16) records.Add(record);
            else AddFailure("Checkpoint bound reached; keep this audit to at most 16 explicit samples.");
            audit.checkpoints = records.ToArray();
            if (!string.IsNullOrEmpty(record.failure)) AddFailure(record.label + ": " + record.failure);
            Save();
            return JsonUtility.ToJson(record, true);
        }

        /// <summary>No code write. Root invokes this explicitly to exercise its compile→stop guard.</summary>
        public static string RequestCompilationProbe(bool forceRecompile = false)
        {
            RequireArmed();
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling)
                throw new InvalidOperationException("Request the probe only in a stable playing native scene.");
            RecordCheckpoint("before explicit compilation probe");
            audit.compileProbes++;
            if (forceRecompile) audit.forcedCompileProbes++;
            Save();
            CompilationPipeline.RequestScriptCompilation(forceRecompile ? RequestScriptCompilationOptions.CleanBuildCache : RequestScriptCompilationOptions.None);
            return "Compilation requested without writing source. Observe root guard stop, reload and fresh Play; no success is assumed.";
        }

        public static string FinishAudit()
        {
            RequireArmed();
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Finish after the final Stop and settled compilation.");
            RecordCheckpoint("final stopped scene");
            if (audit.enteredPlay < 3 || audit.exitedPlay < 3 || audit.enteredEdit < 3)
                AddFailure("Need three actual fresh Play / Stop cycles, including a fresh run after the compilation probe.");
            if (audit.compileProbes != 1 || audit.compilationStarts < 1 || audit.compilationFinishes < 1 || audit.beforeReloads < 1 || audit.afterReloads < 1)
                AddFailure("One explicit compile probe and actual compile/reload callbacks are required.");
            var playingChecks = 0;
            foreach (var record in audit.checkpoints) if (record.playing && string.IsNullOrEmpty(record.failure)) playingChecks++;
            if (playingChecks < 3) AddFailure("Need at least three explicit stable playing checkpoints with healthy shop bindings and singleton.");
            if (audit.reloadWhilePlaying != 0) AddFailure("An assembly reload began while Play was still active.");
            if (audit.errors != 0) AddFailure("Native error/exception/assert logs were observed; zero-error acceptance failed.");
            audit.passed = audit.failures.Length == 0;
            audit.status = audit.passed ? "PASSED_NATIVE_LIFECYCLE_AUDIT" : "FAILED_NATIVE_LIFECYCLE_AUDIT";
            audit.armed = false;
            Save();
            return JsonUtility.ToJson(audit, true);
        }

        public static string CurrentAuditJson() { Restore(); return JsonUtility.ToJson(audit, true); }

        static Checkpoint Capture(string label)
        {
            var record = new Checkpoint { label = label, utc = DateTime.UtcNow.ToString("o"), playing = EditorApplication.isPlaying };
            var sessions = UnityEngine.Object.FindObjectsByType<GameSession>(FindObjectsInactive.Include);
            GameSession active = null;
            foreach (var item in sessions)
            {
                if (!item.gameObject.scene.IsValid()) continue;
                record.sessions++;
                if (item.isActiveAndEnabled) { record.activeSessions++; active = item; }
            }
            record.singletonStrictNull = ReferenceEquals(GameSession.Instance, null);
            record.singletonMatchesActive = record.activeSessions == 1 && ReferenceEquals(GameSession.Instance, active);
            var panels = UnityEngine.Object.FindObjectsByType<BlacksmithShopPanel>(FindObjectsInactive.Include);
            foreach (var panel in panels) if (panel.gameObject.scene.IsValid()) record.panels++;
            record.players = CountSceneComponents<OraclePlayer>();
            record.huds = CountSceneComponents<OdysseyHud>();
            record.worlds = CountSceneComponents<DungeonWorld>();
            if (record.playing)
            {
                if (!record.singletonMatchesActive) record.failure = "Singleton does not match exactly one active session.";
                record.panelReady = true;
                foreach (var panel in panels)
                {
                    if (!panel.gameObject.scene.IsValid()) continue;
                    record.panelReady &= ReadBool(panel, "RuntimeBindingsReady");
                    record.panelInvalidations += ReadInt(panel, "LifecycleInvalidations");
                    record.staleDelegateTargets += CountStaleTargets(panel);
                }
                if (record.panels != 1 || !record.panelReady || record.panelInvalidations != 0 || record.staleDelegateTargets != 0)
                    record.failure += " Shop view is missing, invalidated, unbound or retains a destroyed callback target.";
            }
            else
            {
                if (!record.singletonStrictNull) record.failure = "Stopped scene retains a static session reference, including a destroyed Unity wrapper.";
                if (record.panels != 0 || record.players != 0 || record.huds != 0 || record.worlds != 0)
                    record.failure += " Stopped bootstrap scene retains generated runtime objects.";
            }
            return record;
        }

        static int CountSceneComponents<T>() where T : Component
        {
            var count = 0;
            foreach (var item in UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include))
                if (item.gameObject.scene.IsValid()) count++;
            return count;
        }

        static int CountStaleTargets(BlacksmithShopPanel panel)
        {
            var field = typeof(BlacksmithShopPanel).GetField("data", BindingFlags.Instance | BindingFlags.NonPublic);
            var bindings = field != null ? field.GetValue(panel) : null;
            if (bindings == null) return 0; // RuntimeBindingsReady reports the missing graph separately.
            var count = 0;
            foreach (var member in bindings.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (!(member.GetValue(bindings) is Delegate callback)) continue;
                foreach (var entry in callback.GetInvocationList())
                    if (entry.Target is UnityEngine.Object owner && owner == null) count++;
            }
            return count;
        }

        static bool ReadBool(object owner, string name)
        {
            var property = owner.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            return property != null && property.PropertyType == typeof(bool) && (bool)property.GetValue(owner);
        }
        static int ReadInt(object owner, string name)
        {
            var property = owner.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            return property != null && property.PropertyType == typeof(int) ? (int)property.GetValue(owner) : -1;
        }
        static void OnPlayMode(PlayModeStateChange change)
        {
            if (audit == null || !audit.armed) return;
            if (change == PlayModeStateChange.EnteredPlayMode) audit.enteredPlay++;
            else if (change == PlayModeStateChange.ExitingPlayMode) audit.exitedPlay++;
            else if (change == PlayModeStateChange.EnteredEditMode) audit.enteredEdit++;
            Save();
        }
        static void OnCompilationStarted(object context) { if (audit?.armed == true) { audit.compilationStarts++; Save(); } }
        static void OnCompilationFinished(object context) { if (audit?.armed == true) { audit.compilationFinishes++; Save(); } }
        static void OnBeforeReload()
        {
            if (audit?.armed != true) return;
            audit.beforeReloads++;
            if (EditorApplication.isPlaying) audit.reloadWhilePlaying++;
            Save();
        }
        [DidReloadScripts]
        static void OnAfterReload() { Restore(); if (audit.armed) { audit.afterReloads++; Save(); } }
        static void OnLog(string condition, string stack, LogType type)
        {
            if (audit?.armed != true) return;
            if (type == LogType.Warning) audit.warnings++;
            else if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                audit.errors++;
                AddFailure("Native log " + type + ": " + (condition ?? "").Substring(0, Math.Min(384, (condition ?? "").Length)));
            }
            else return;
            Save();
        }
        static void AddFailure(string message)
        {
            var failures = new List<string>(audit.failures);
            if (failures.Count < 32) failures.Add(message);
            audit.failures = failures.ToArray();
        }
        static void RequireArmed() { if (audit?.armed != true) throw new InvalidOperationException("BeginAudit must be called in stopped mode first."); }
        static void Save() { audit.lastUtc = DateTime.UtcNow.ToString("o"); SessionState.SetString(Key, JsonUtility.ToJson(audit)); }
        static void Restore()
        {
            var json = SessionState.GetString(Key, "");
            audit = string.IsNullOrEmpty(json) ? new Audit() : JsonUtility.FromJson<Audit>(json) ?? new Audit();
        }
    }
    // # ARCHITECTURE SNAPSHOT: Editor-only explicit observer, bounded SessionState
    // receipts survive domain reload; no build-player code, source edits, save-data
    // writes, play toggling, purchase calls, mouse control or automatic compilation.
}
#endif
