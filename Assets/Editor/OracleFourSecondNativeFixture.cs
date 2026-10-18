#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OdysseyOracle.Editor
{
    /// <summary>Root-run, single-owner native timing fixture. No input, source or profile mutation.</summary>
    public static class OracleFourSecondNativeFixture
    {
        // # ARCHITECTURE SNAPSHOT
        // Editor-only adapter owns one update callback and a bounded receipt.
        // Public game-clock/cooldown values observe the actual runtime; no private
        // timestamp, wallet, network ledger or gameplay source is rewritten.
        // Each public Cast is synchronously bracketed by AutoFire eligibility.
        const double MaximumPairInterval = 1.1d, ClockStallSeconds = 1d, WallTimeoutSeconds = 24d;
        const float Epsilon = .002f;
        enum Phase { Warmup, WaitWater, WaitPairRecovery }

        [Serializable] public sealed class Observation
        {
            public string label, utc;
            public double gameTime, editorTime;
            public int frame, selectedElement, combo, activeProjectiles;
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
            public int originalSelectedElement, initialWallet = -1, finalWallet = -1, initialXp = -1, finalXp = -1;
            public int initialLevel = -1, finalLevel = -1, initialPoints = -1, finalPoints = -1;
            public int initialKills, finalKills, editorTicks, distinctGameFrames;
            public double elapsedGameSeconds, fireAcceptedAt, waterAcceptedAt, pairIntervalSeconds, pairRecoveryObservedSeconds;
            public float pairIntervalFromFireCooldown;
            public List<Observation> observations = new List<Observation>(16);
            public List<Assertion> assertions = new List<Assertion>(16);
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
                if (!AllReady(run.Combat) || CountActive(run) != 0 || run.Combat.CurrentCombo != 0)
                {
                    if (game - run.WarmupStarted > 4.5d) Finish(run, "INCOMPLETE", "Fresh recovery/pool state did not settle within the bounded warmup.");
                    return;
                }
                // Free Earth -> Fire starts a fresh four-second selection window
                // without a pair. No private combo clock or cooldown is rewritten.
                var before = Observe(run, "before_free_chain_setup");
                bool earth = run.Combat.TrySelectElement(2), fire = run.Combat.TrySelectElement(0);
                var selected = Observe(run, "free_earth_to_fire");
                if (!Assert(run, "selection_setup_spends_no_recovery", earth && fire && SameRecovery(before, selected) && run.Combat.CurrentCombo == 0,
                    "Two public selections preserve every ready slot and start Fire without an accidental pair.")) return;
                int poolBefore = CountActive(run);
                EligibleCast(run.Combat);
                var cast = Observe(run, "fire_accepted");
                if (!Assert(run, "fire_launch_and_four_second_recovery", NearFour(cast.cooldowns[0]) && NearFour(cast.recovery[0]) &&
                    cast.activeProjectiles == poolBefore + 1 && NearFour(run.Combat.CooldownDuration[0]),
                    "The public pool gained one active projectile and Fire recovery became four seconds.")) return;
                if (!Assert(run, "fire_does_not_lock_unused_elements", IsZero(cast.cooldowns[1]) && IsZero(cast.cooldowns[2]) && IsZero(cast.cooldowns[3]),
                    "Unused Water, Earth and Air remain ready.")) return;
                run.Report.nativeExecuted = true;
                run.Report.fireAcceptedAt = game;
                run.Phase = Phase.WaitWater;
                Save(run.Report);
                return;
            }
            if (run.Phase == Phase.WaitWater)
            {
                double elapsed = game - run.Report.fireAcceptedAt;
                float fromCooldown = OracleCombat.AbilityCooldownSeconds - run.Combat.CooldownRemaining[0];
                if (elapsed > MaximumPairInterval + .0001d || fromCooldown > MaximumPairInterval + .0001f)
                { Finish(run, "INCOMPLETE", "Native scheduling overshot the 1.1-second pair window; this is not evidence of a gameplay bug."); return; }
                if (elapsed < 1d || fromCooldown < 1f) return;
                var retryBefore = Observe(run, "fire_retry_after_global_cadence");
                EligibleCast(run.Combat);
                var retryAfter = Observe(run, "fire_retry_rejected");
                if (!Assert(run, "same_fire_retry_changes_nothing", SameRecovery(retryBefore, retryAfter) &&
                    retryBefore.activeProjectiles == retryAfter.activeProjectiles,
                    "After the global one-second gate, Fire still rejects a retry without extending recovery or adding a projectile.")) return;
                bool selected = run.Combat.TrySelectElement(1);
                var waterSelected = Observe(run, "water_selected_free");
                if (!Assert(run, "water_selection_is_free_and_steam_ready", selected && run.Combat.SelectedSpell == 1 &&
                    run.Combat.CurrentCombo == (int)OracleCombat.ElementCombo.Steam && SameRecovery(retryAfter, waterSelected),
                    "Water selection leaves all recovery values unchanged and exposes the real Steam pair.")) return;
                int poolBefore = CountActive(run);
                EligibleCast(run.Combat);
                var water = Observe(run, "water_accepted_steam");
                if (!Assert(run, "water_launch_within_one_point_one_seconds", water.activeProjectiles == poolBefore + 1 &&
                    NearFour(water.cooldowns[0]) && NearFour(water.cooldowns[1]) && NearFour(water.recovery[0]) && NearFour(water.recovery[1]),
                    "One Water projectile is accepted inside the native window; Steam charges Fire and Water for four seconds.")) return;
                if (!Assert(run, "steam_locks_only_its_ingredients", IsZero(water.cooldowns[2]) && IsZero(water.cooldowns[3]),
                    "Earth and Air remain usable.")) return;
                run.Report.waterAcceptedAt = run.WaterGame = game;
                run.Report.pairIntervalSeconds = elapsed;
                run.Report.pairIntervalFromFireCooldown = fromCooldown;
                run.Phase = Phase.WaitPairRecovery;
                Save(run.Report);
                return;
            }
            double recoveredAfter = game - run.WaterGame;
            if (recoveredAfter > 4.2d)
            { Finish(run, "INCOMPLETE", "Pair recovery observation missed its bounded native deadline."); return; }
            if (recoveredAfter < 3.99d || run.Combat.CooldownRemaining[0] > 0f || run.Combat.CooldownRemaining[1] > 0f) return;
            var recovered = Observe(run, "pair_recovered");
            if (!Assert(run, "pair_recovers_after_four_game_seconds", recoveredAfter >= 3.99d && recoveredAfter <= 4.1d &&
                IsZero(recovered.recovery[0]) && IsZero(recovered.recovery[1]), "Both real ingredient slots return after four game seconds.")) return;
            run.Report.pairRecoveryObservedSeconds = recoveredAfter;
            Finish(run, "PASSED_NATIVE_SOLO_FOUR_SECOND_PAIR", "Actual pooled Fire, rejected retry, free Water selection, Steam recovery and expiry observed; no Director-host claim.");
        }
        static void EligibleCast(OracleCombat combat)
        {
            combat.SetAutoFire(true);
            try { combat.Cast(); }
            finally { combat.SetAutoFire(false); }
        }
        static bool Assert(Run run, string name, bool passed, string detail)
        {
            run.Report.assertions.Add(new Assertion { name = name, passed = passed, detail = detail });
            if (!passed) Finish(run, "FAILED_NATIVE_SOLO_FOUR_SECOND_PAIR", name + ": " + detail);
            return passed;
        }
        static Observation Observe(Run run, string label)
        {
            var result = new Observation { label = label, utc = Utc(), gameTime = Time.timeAsDouble,
                editorTime = EditorApplication.timeSinceStartup, frame = Time.frameCount, selectedElement = run.Combat.SelectedSpell,
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
                    if (!run.Owner.IsDead && !run.Owner.IsDowned && run.Owner.HasStarted)
                    {
                        run.Owner.Paused = false;
                        report.restoredSelection = run.Combat.TrySelectElement(report.originalSelectedElement) && run.Combat.SelectedSpell == report.originalSelectedElement;
                    }
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
                active = null; last = report;
                run.Owner = null; run.Combat = null; run.Projectiles = null;
                Save(report);
            }
        }
        static string Reject(Receipt report, string reason)
        { report.status = "INCOMPLETE"; report.reason = reason; report.endedUtc = Utc(); report.callbacksDetached = true; last = report; Save(report); return JsonUtility.ToJson(report, true); }
        static void OnPlayMode(PlayModeStateChange state)
        { if (active != null && (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode)) Finish(active, "INCOMPLETE", "Play stopped before acceptance completed."); }
        static void OnReload() { if (active != null) Finish(active, "INCOMPLETE", "Assembly reload interrupted the fixture."); }
        static void OnQuit() { if (active != null) Finish(active, "INCOMPLETE", "Editor quit interrupted the fixture."); }
        static string Utc() => DateTime.UtcNow.ToString("o");
        static string ValidateReceiptPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Provide an absolute work receipt JSON path.", nameof(path));
            if (!Path.IsPathRooted(path)) throw new ArgumentException("Receipt path must be absolute.", nameof(path));
            string full = Path.GetFullPath(path), assets = Path.GetFullPath(Application.dataPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!full.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || full.StartsWith(assets, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Use a JSON receipt outside Assets; the fixture never triggers a source import.", nameof(path));
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
