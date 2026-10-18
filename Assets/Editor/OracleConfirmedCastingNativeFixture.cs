#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OdysseyOracle.Editor
{
    /// <summary>Root-run, single-owner native control fixture. No OS input, gameplay-source or profile mutation.</summary>
    public static class OracleConfirmedCastingNativeFixture
    {
        // # ARCHITECTURE SNAPSHOT
        // Editor-only adapter owns one update callback and a bounded receipt.
        // Public game-clock/cooldown values observe the actual runtime; no private
        // timestamp, wallet, network ledger or gameplay source is rewritten.
        // Manual casts run with optional auto-fire off; one pending-state probe enables it briefly.
        const double MaximumPairInterval = 1.1d, ClockStallSeconds = 1d, WallTimeoutSeconds = 24d;
        const float Epsilon = .002f;
        enum Phase { Warmup, WaitPending, WaitPair, WaitPairRecovery }

        [Serializable] public sealed class Observation
        {
            public string label, utc;
            public double gameTime, editorTime;
            public int frame, selectedElement, combo, activeProjectiles, preparedElement, confirmedMask, confirmedElement;
            public bool autoFire, paused, focused;
            public float[] cooldowns = new float[4], recovery = new float[4];
        }
        [Serializable] public sealed class Assertion
        {
            public string name, detail;
            public bool passed;
        }
        [Serializable] public sealed class Receipt
        {
            public string status = "NOT_STARTED", reason = "", startedUtc, endedUtc, path, mode = "solo_or_hub_native_combat";
            public bool nativeExecuted, directorHostExecuted, profileWritesRequested, osInputRequested, focusChangedByFixture;
            public bool originalAutoFire, originalPaused, restoredAutoFire, restoredPaused, restoredSelection, callbacksDetached;
            public bool focusedAtStart, focusedAtFinish;
            public int originalSelectedElement, originalPrepared, originalMask, originalConfirmed, initialWallet = -1, finalWallet = -1, initialXp = -1, finalXp = -1;
            public int initialLevel = -1, finalLevel = -1, initialPoints = -1, finalPoints = -1;
            public int initialKills, finalKills, editorTicks, distinctGameFrames;
            public double elapsedGameSeconds, fireAcceptedAt, waterAcceptedAt, pairIntervalSeconds, pairRecoveryObservedSeconds;
            public float pairIntervalFromFireCooldown;
            public List<Observation> observations = new List<Observation>(16);
            public List<Assertion> assertions = new List<Assertion>(64);
        }
        sealed class Run
        {
            internal GameSession Owner;
            internal OracleCombat Combat;
            internal OracleProjectile[] Projectiles;
            internal Receipt Report;
            internal Phase Phase;
            internal double StartedGame, StartedWall, LastGame, LastProgressWall, WarmupStarted, WaterGame;
            internal int LastFrame = -1;
            internal bool Finished;
        }
        static Run active;
        static Receipt last = new Receipt();
        public static bool IsRunning => active != null;
        public static string CurrentReceiptJson() => JsonUtility.ToJson(active != null ? active.Report : last, true);

        /// <summary>Import this Editor file, then call once during a fresh empty Play session. It finishes itself.</summary>
        public static string Begin(GameSession owner, string receiptPath)
        {
            if (active != null) Finish(active, "INCOMPLETE", "Superseded by a new explicit fixture run.");
            string path = ValidateReceiptPath(receiptPath);
            var report = new Receipt { status = "RUNNING", path = path, startedUtc = Utc(),
                focusedAtStart = Application.isFocused };
            last = report;
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isPaused)
                return Reject(report, "Begin requires stable, unpaused Editor Play; the fixture never starts or focuses Play.");
            if (owner == null || !ReferenceEquals(GameSession.Instance, owner) || owner.Combat == null || owner.World == null || owner.Player == null)
                return Reject(report, "A healthy current GameSession with initialized world/player/combat is required.");
            if (!owner.HasStarted || !owner.CombatUnlocked || owner.Victory || owner.IsDead || owner.IsDowned)
                return Reject(report, "Use a fresh started solo/hub combat-unlocked session; the fixture does not skip or alter lessons.");
            if (owner.Defense != null && owner.Defense.Running)
                return Reject(report, "Running Director matches are excluded: autonomous casts/spawns cannot be isolated through public APIs. No host acceptance is claimed.");
            if (HasLiveEnemies(owner.Combat) || owner.Defense != null && owner.Defense.PlacedCount != 0 || HasActivePickups())
                return Reject(report, "Fresh empty session required: no live enemies, towers, XP motes or Heartcrystals may be present.");
            if (AbilityBindings.IsRebinding || owner.Builder != null && owner.Builder.IsPlacing)
                return Reject(report, "Finish rebinding/build placement before this explicit fixture.");
            var run = new Run { Owner = owner, Combat = owner.Combat, Report = report,
                Projectiles = owner.World.GetComponentsInChildren<OracleProjectile>(true),
                StartedGame = Time.timeAsDouble, StartedWall = EditorApplication.timeSinceStartup,
                LastGame = Time.timeAsDouble, LastProgressWall = EditorApplication.timeSinceStartup,
                WarmupStarted = Time.timeAsDouble, Phase = Phase.Warmup };
            report.originalAutoFire = run.Combat.AutoFireEnabled;
            report.originalPaused = owner.Paused;
            report.originalSelectedElement = run.Combat.SelectedSpell;
            report.originalPrepared = run.Combat.PreparedElement;
            report.originalMask = run.Combat.ConfirmedMask;
            report.originalConfirmed = run.Combat.ConfirmedElement;
            if (report.originalAutoFire) return Reject(report, "Use a fresh default-off manual encounter; this fixture does not claim to observe initialization after a setting was changed.");
            CaptureProfile(run, false);
            if (run.Projectiles.Length == 0) return Reject(report, "No initialized projectile pool exists under this world.");
            active = run;
            owner.Paused = false; // Public transient field; does not invoke cursor/focus/input APIs.
            run.Combat.SetAutoFire(false);
            if (!owner.IsPlaying)
            {
                Finish(run, "INCOMPLETE", "Loading, arrival, guide or another gate prevents actual gameplay.");
                return CurrentReceiptJson();
            }
            Observe(run, "armed_auto_fire_off");
            Attach();
            Save(report);
            return CurrentReceiptJson();
        }

        public static string Cancel()
        {
            if (active != null) Finish(active, "INCOMPLETE", "Cancelled explicitly by root.");
            return CurrentReceiptJson();
        }
        static void Attach()
        {
            Detach();
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorApplication.quitting += OnQuit;
            AssemblyReloadEvents.beforeAssemblyReload += OnReload;
        }
        static void Detach()
        {
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= OnPlayMode;
            EditorApplication.quitting -= OnQuit;
            AssemblyReloadEvents.beforeAssemblyReload -= OnReload;
        }
        static void Tick()
        {
            var run = active;
            if (run == null) { Detach(); return; }
            try { Advance(run); }
            catch (Exception error) { Finish(run, "INCOMPLETE", "Fixture interrupted by " + error.GetType().Name + ": " + error.Message); }
        }
        static void Advance(Run run)
        {
            run.Report.editorTicks++;
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isPaused ||
                run.Owner == null || run.Combat == null || !ReferenceEquals(GameSession.Instance, run.Owner))
            { Finish(run, "INCOMPLETE", "Play, compilation, Editor pause or session lifetime interrupted the run."); return; }
            double wall = EditorApplication.timeSinceStartup, game = Time.timeAsDouble;
            if (wall - run.StartedWall > WallTimeoutSeconds)
            { Finish(run, "INCOMPLETE", "Bounded Editor timeout; no timing result is assumed."); return; }
            if (run.Owner.Paused || !run.Owner.IsPlaying || !run.Owner.CombatUnlocked ||
                run.Owner.Defense != null && run.Owner.Defense.Running || HasLiveEnemies(run.Combat))
            { Finish(run, "INCOMPLETE", "Gameplay paused, gated, joined a match, or acquired a live enemy."); return; }
            if (!ProfileUnchanged(run))
            { Finish(run, "INCOMPLETE", "An external reward/profile counter changed; the fixture never rolls it back."); return; }
            if (game > run.LastGame + .0000001d)
            { run.LastGame = game; run.LastProgressWall = wall; }
            else if (wall - run.LastProgressWall > ClockStallSeconds)
            { Finish(run, "INCOMPLETE", "Game clock stopped (focus pause or Editor interruption); no false cooldown failure is reported."); return; }
            if (run.LastFrame == Time.frameCount) return;
            run.LastFrame = Time.frameCount; run.Report.distinctGameFrames++;
            if (run.Phase == Phase.Warmup)
            {
                if (!AllReady(run.Combat) || CountActive(run) != 0)
                {
                    if (game - run.WarmupStarted > 4.5d) Finish(run, "INCOMPLETE", "Recovery/pool state did not settle in bounded warmup.");
                    return;
                }
                if (!Assert(run, "fresh_auto_fire_default_off", !run.Report.originalAutoFire, "Fresh encounter began with optional auto-fire off.")) return;
                if (!run.Combat.ApplyRemoteSelectionState(-1, 0, -1, 0))
                { Finish(run, "INCOMPLETE", "Cannot isolate a public solo selection state."); return; }
                string[] pairNames = { "", "Combustion", "Binding Mud", "Steam", "Shrapnel", "Magma", "Frost Gale" };
                for (int first = 0; first < 4; first++) for (int second = 0; second < 4; second++)
                {
                    int count = CountActive(run);
                    bool tap = run.Combat.TrySelectElement(first);
                    run.Combat.ManualCast();
                    if (!Assert(run, "first_tap_prepares_" + first + second, tap && run.Combat.SelectionPending &&
                        !run.Combat.HasConfirmedSelection && run.Combat.PreparedElement == first && run.Combat.ConfirmedMask == 0 &&
                        CountActive(run) == count && AllReady(run.Combat), "Preparation and explicit manual request cannot launch or charge recovery.")) return;
                    bool confirm = run.Combat.TrySelectElement(second);
                    int mask = (1 << first) | (1 << second), combo = ConfirmedSelection.ComboForMask(mask);
                    if (!Assert(run, "second_tap_confirms_" + first + second, confirm && !run.Combat.SelectionPending &&
                        run.Combat.HasConfirmedSelection && run.Combat.ConfirmedElement == second && run.Combat.ConfirmedMask == mask &&
                        run.Combat.CurrentCombo == combo && (combo == 0 || run.Combat.SelectionStatus.Contains(pairNames[combo])) &&
                        CountActive(run) == count && AllReady(run.Combat), "Same tap confirms single; different tap confirms its named pair; neither fires.")) return;
                }
                run.Combat.TrySelectElement(0); run.Combat.TrySelectElement(0);
                int before = CountActive(run); run.Combat.ManualCast();
                var shot = Observe(run, "manual_fire_accepted_auto_off");
                if (!Assert(run, "manual_single_launch_four_seconds", !run.Combat.AutoFireEnabled && CountActive(run) == before + 1 &&
                    NearFour(shot.cooldowns[0]) && NearFour(shot.recovery[0]) && IsZero(shot.cooldowns[1]) && IsZero(shot.cooldowns[2]) && IsZero(shot.cooldowns[3]),
                    "One actual pooled Fire projectile with auto-fire off; only Fire receives four seconds.")) return;
                var retryBefore = Observe(run, "same_frame_retry_before"); run.Combat.ManualCast();
                var retryAfter = Observe(run, "same_frame_retry_after");
                if (!Assert(run, "duplicate_manual_request_no_charge", SameRecovery(retryBefore, retryAfter) && retryBefore.activeProjectiles == retryAfter.activeProjectiles,
                    "Rejected duplicate launch changes no recovery/pool count.")) return;
                run.Report.nativeExecuted = true; run.Report.fireAcceptedAt = game;
                run.Phase = Phase.WaitPending; Save(run.Report); return;
            }
            double elapsed = game - run.Report.fireAcceptedAt;
            if (run.Phase == Phase.WaitPending)
            {
                if (elapsed < .45d) return;
                if (elapsed > .8d) { Finish(run, "INCOMPLETE", "Native pending/global observation missed its subsecond window."); return; }
                var before = Observe(run, "before_new_preparation");
                run.Combat.TrySelectElement(1); run.Combat.SetAutoFire(true); run.Combat.ManualCast();
                var after = Observe(run, "pending_blocks_manual_with_optional_auto_on");
                if (!Assert(run, "pending_blocks_cast_and_selection_is_free", run.Combat.SelectionPending && !run.Combat.HasConfirmedSelection &&
                    run.Combat.ConfirmedMask == 0 && SameRecovery(before, after) && before.activeProjectiles == after.activeProjectiles,
                    "New preparation invalidates confirmation; no explicit launch or recovery while optional auto is enabled. Host auto-loop is separately exact-source tested.")) return;
                run.Combat.SetAutoFire(false); run.Combat.TrySelectElement(3);
                int count = CountActive(run); var gateBefore = Observe(run, "unused_pair_before_global_second"); run.Combat.ManualCast();
                var gateAfter = Observe(run, "unused_pair_rejected_before_global_second");
                if (!Assert(run, "global_one_second_gate_for_unused_pair", run.Combat.CurrentCombo == 6 && run.Combat.ConfirmedMask == 10 &&
                    SameRecovery(gateBefore, gateAfter) && CountActive(run) == count, "Ready Water/Air pair still cannot bypass the accepted primary one-second cadence.")) return;
                run.Phase = Phase.WaitPair; return;
            }
            if (run.Phase == Phase.WaitPair)
            {
                float fromCooldown = OracleCombat.AbilityCooldownSeconds - run.Combat.CooldownRemaining[0];
                if (elapsed > MaximumPairInterval + .0001d || fromCooldown > MaximumPairInterval + .0001f)
                { Finish(run, "INCOMPLETE", "Native scheduler overshot1.1-second window; not evidence of a gameplay bug."); return; }
                if (elapsed < 1d || fromCooldown < 1f) return;
                run.Combat.TrySelectElement(0); run.Combat.TrySelectElement(0);
                var ingredientBefore = Observe(run, "fire_retry_after_global_before"); run.Combat.ManualCast();
                var ingredientAfter = Observe(run, "fire_retry_after_global_rejected");
                if (!Assert(run, "ingredient_recovery_rejects_after_global", SameRecovery(ingredientBefore, ingredientAfter) &&
                    ingredientBefore.activeProjectiles == ingredientAfter.activeProjectiles, "The expired global gate cannot bypass Fire recovery or extend it.")) return;
                run.Combat.TrySelectElement(1); run.Combat.TrySelectElement(3);
                int count = CountActive(run); run.Combat.ManualCast();
                var pair = Observe(run, "manual_frost_gale_accepted");
                if (!Assert(run, "accepted_pair_locks_only_water_air", CountActive(run) == count + 1 && NearFour(pair.cooldowns[1]) &&
                    NearFour(pair.cooldowns[3]) && NearFour(pair.recovery[1]) && NearFour(pair.recovery[3]) && IsZero(pair.cooldowns[2]) &&
                    pair.cooldowns[0] < 3.01f && pair.cooldowns[0] > 2.8f, "Frost Gale accepts within1.1s; Water/Air recover4s and unrelated Fire recovery is not refreshed.")) return;
                var retryBefore = Observe(run, "pair_retry_before"); run.Combat.ManualCast(); var retryAfter = Observe(run, "pair_retry_after");
                if (!Assert(run, "pair_retry_unchanged", SameRecovery(retryBefore, retryAfter) && retryBefore.activeProjectiles == retryAfter.activeProjectiles,
                    "Repeated manual request cannot add a projectile or extend either ingredient recovery.")) return;
                run.Report.waterAcceptedAt = run.WaterGame = game; run.Report.pairIntervalSeconds = elapsed;
                run.Report.pairIntervalFromFireCooldown = fromCooldown; run.Phase = Phase.WaitPairRecovery; Save(run.Report); return;
            }
            double recoveredAfter = game - run.WaterGame;
            if (recoveredAfter > 4.2d) { Finish(run, "INCOMPLETE", "Recovery observation missed bounded native deadline."); return; }
            if (recoveredAfter < 3.99d || run.Combat.CooldownRemaining[1] > 0f || run.Combat.CooldownRemaining[3] > 0f) return;
            if (!Assert(run, "pair_recovers_after_four_game_seconds", recoveredAfter >= 3.99d && recoveredAfter <= 4.1d && AllReady(run.Combat),
                "Both real pair ingredient slots expire after four simulation seconds.")) return;
            run.Report.pairRecoveryObservedSeconds = recoveredAfter;
            Finish(run, "PASSED_NATIVE_CONFIRMED_MANUAL_CURRENT002", "Actual solo pool/cooldowns observed with all16 confirmations, six pair names, first-tap rejection, manual launch, global cadence and recovery. No Director/network or forced-pool-failure claim.");
        }
        static bool Assert(Run run, string name, bool passed, string detail)
        {
            run.Report.assertions.Add(new Assertion { name = name, passed = passed, detail = detail });
            if (!passed) Finish(run, "FAILED_NATIVE_CONFIRMED_MANUAL_CURRENT002", name + ": " + detail);
            return passed;
        }
        static Observation Observe(Run run, string label)
        {
            var result = new Observation { label = label, utc = Utc(), gameTime = Time.timeAsDouble,
                editorTime = EditorApplication.timeSinceStartup, frame = Time.frameCount, selectedElement = run.Combat.SelectedSpell,
                preparedElement = run.Combat.PreparedElement, confirmedMask = run.Combat.ConfirmedMask, confirmedElement = run.Combat.ConfirmedElement,
                combo = run.Combat.CurrentCombo, activeProjectiles = CountActive(run), autoFire = run.Combat.AutoFireEnabled,
                paused = run.Owner.Paused, focused = Application.isFocused };
            for (int i = 0; i < 4; i++) { result.cooldowns[i] = run.Combat.CooldownRemaining[i]; result.recovery[i] = run.Combat.ElementSwitchCooldowns[i]; }
            if (run.Report.observations.Count < 16) run.Report.observations.Add(result);
            return result;
        }
        static bool SameRecovery(Observation a, Observation b)
        {
            for (int i = 0; i < 4; i++) if (!Close(a.cooldowns[i], b.cooldowns[i]) || !Close(a.recovery[i], b.recovery[i])) return false;
            return true;
        }
        static bool AllReady(OracleCombat combat)
        { for (int i = 0; i < 4; i++) if (!IsZero(combat.CooldownRemaining[i]) || !IsZero(combat.ElementSwitchCooldowns[i])) return false; return true; }
        static bool NearFour(float value) => Close(value, OracleCombat.AbilityCooldownSeconds);
        static bool IsZero(float value) => Finite(value) && Mathf.Abs(value) <= Epsilon;
        static bool Close(float a, float b) => Finite(a) && Finite(b) && Mathf.Abs(a - b) <= Epsilon;
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static int CountActive(Run run)
        { int count = 0; foreach (var projectile in run.Projectiles) if (projectile != null && projectile.gameObject.activeInHierarchy) count++; return count; }
        static bool HasLiveEnemies(OracleCombat combat)
        { foreach (var enemy in combat.ActiveEnemies) if (enemy != null && enemy.Alive) return true; return false; }
        static bool HasActivePickups()
        {
            return Object.FindObjectsByType<XpPickup>(FindObjectsInactive.Exclude).Length != 0 ||
                Object.FindObjectsByType<HeartcrystalPickup>(FindObjectsInactive.Exclude).Length != 0;
        }
        static void CaptureProfile(Run run, bool final)
        {
            var owner = run.Owner; var report = run.Report;
            int wallet = owner.Shop != null ? owner.Shop.Heartcrystals : -1;
            int xp = owner.Progression != null ? owner.Progression.Experience : -1;
            int level = owner.Progression != null ? owner.Progression.Level : -1;
            int points = owner.Progression != null ? owner.Progression.SkillPoints : -1;
            if (!final) { report.initialWallet = wallet; report.initialXp = xp; report.initialLevel = level; report.initialPoints = points; report.initialKills = owner.Defeated; }
            else { report.finalWallet = wallet; report.finalXp = xp; report.finalLevel = level; report.finalPoints = points; report.finalKills = owner.Defeated; }
        }
        static bool ProfileUnchanged(Run run)
        {
            CaptureProfile(run, true); var r = run.Report;
            return r.initialWallet == r.finalWallet && r.initialXp == r.finalXp && r.initialLevel == r.finalLevel &&
                r.initialPoints == r.finalPoints && r.initialKills == r.finalKills;
        }
        static void Finish(Run run, string status, string reason)
        {
            if (run == null || run.Finished) return;
            run.Finished = true;
            Detach();
            var report = run.Report;
            report.status = status; report.reason = reason; report.endedUtc = Utc();
            report.elapsedGameSeconds = Math.Max(0d, Time.timeAsDouble - run.StartedGame);
            report.focusedAtFinish = Application.isFocused;
            try
            {
                if (run.Owner != null && run.Combat != null && ReferenceEquals(GameSession.Instance, run.Owner))
                {
                    run.Combat.SetAutoFire(false);
                    report.restoredSelection = run.Combat.ApplyRemoteSelectionState(report.originalPrepared,
                        report.originalMask, report.originalConfirmed, 0) && run.Combat.PreparedElement == report.originalPrepared &&
                        run.Combat.ConfirmedMask == report.originalMask && run.Combat.ConfirmedElement == report.originalConfirmed;
                    run.Owner.Progression?.SetSelectedElementDirect(report.originalSelectedElement);
                    run.Combat.SetAutoFire(report.originalAutoFire);
                    report.restoredAutoFire = run.Combat.AutoFireEnabled == report.originalAutoFire;
                    run.Owner.Paused = report.originalPaused;
                    report.restoredPaused = run.Owner.Paused == report.originalPaused;
                    CaptureProfile(run, true);
                }
            }
            catch (Exception error) { report.reason += " Restore was partial: " + error.GetType().Name + "."; }
            finally
            {
                report.callbacksDetached = true;
                if (report.status.StartsWith("PASSED", StringComparison.Ordinal) &&
                    (!report.restoredAutoFire || !report.restoredPaused || !report.restoredSelection))
                { report.status = "INCOMPLETE"; report.reason += " Public transient state restoration was partial."; }
                active = null; last = report;
                run.Owner = null; run.Combat = null; run.Projectiles = null;
                Save(report);
            }
        }
        static string Reject(Receipt report, string reason)
        { Detach(); report.status = "INCOMPLETE"; report.reason = reason; report.endedUtc = Utc(); report.callbacksDetached = true; last = report; Save(report); return JsonUtility.ToJson(report, true); }
        static void OnPlayMode(PlayModeStateChange state)
        { if (active != null && (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode)) Finish(active, "INCOMPLETE", "Play stopped before acceptance completed."); }
        static void OnReload() { if (active != null) Finish(active, "INCOMPLETE", "Assembly reload interrupted the fixture."); }
        static void OnQuit() { if (active != null) Finish(active, "INCOMPLETE", "Editor quit interrupted the fixture."); }
        static string Utc() => DateTime.UtcNow.ToString("o");
        static string ValidateReceiptPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Provide an absolute work receipt JSON path.", nameof(path));
            if (!Path.IsPathRooted(path)) throw new ArgumentException("Receipt path must be absolute.", nameof(path));
            string full = Path.GetFullPath(path);
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."))
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!full.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || full.StartsWith(project, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Use a work JSON receipt outside the Unity project; the fixture never changes project files.", nameof(path));
            return full;
        }
        static void Save(Receipt report)
        {
            try { string parent = Path.GetDirectoryName(report.path); if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent); File.WriteAllText(report.path, JsonUtility.ToJson(report, true)); }
            catch (Exception error) { report.reason += " Receipt write failed: " + error.GetType().Name + "."; }
        }
    }
}
#endif
