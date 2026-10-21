using System;

namespace OdysseyOracle
{
    public enum CampaignStageOutcome { Running, Succeeded, Failed }
    public static class CampaignStagePacing
    {
        public const int StageCount = 8, WavesPerStage = 5, TotalWaves = 40;
        public const int StageSeconds = 145, MaximumStageSeconds = 295;
        public const int PhaseCount = WavesPerStage, MaximumAlive = 48;
        // Elapsed host simulation continues through the bounded post-countdown clear.
        // A finite countdown alone cannot recover that elapsed time after zero.
        public static bool ValidClock(double elapsed, float remaining, bool started) =>
            Finite(elapsed) && elapsed >= 0d && elapsed <= MaximumStageSeconds &&
            Finite(remaining) && remaining >= 0f && remaining <= StageSeconds &&
            (started || elapsed == 0d) &&
            Math.Abs(remaining - Math.Max(0d, StageSeconds - elapsed)) <= .01d;
        // Compatibility upper bound only; each real wave/stage has its own quota.
        public const int AdmissionsPerStage = 346;
        public const double FirstAdmissionAt = .5d, InterWaveSeconds = 5d, WaveGraceSeconds = 30d;
        public static bool ValidStage(int stage) => stage >= 1 && stage <= StageCount;
        public static int GlobalWave(int stage, int localWave)
        {
            if (!ValidStage(stage) || localWave < 0 || localWave >= WavesPerStage) throw new ArgumentOutOfRangeException();
            return (stage - 1) * WavesPerStage + localWave + 1;
        }
        public static int AdmissionsForWave(int globalWave) => CampaignBalance015.Admissions(globalWave);
        public static int XpBudgetForWave(int globalWave) => CampaignBalance015.XpBudget(globalWave);
        public static int BaseXpForAdmission(int globalWave, int ordinal) => CampaignBalance015.BaseXpForAdmission(globalWave, ordinal);
        public static int MaximumBaseXp => BaseXpForAdmission(TotalWaves, 0);
        public static int MaximumClaimXp => MaximumBaseXp * 4;
        public static int StageAdmissions(int stage)
        {
            if (!ValidStage(stage)) throw new ArgumentOutOfRangeException(nameof(stage));
            int count = 0; for (int local = 0; local < WavesPerStage; local++) count += AdmissionsForWave(GlobalWave(stage, local));
            return count;
        }
        public static int LastRewardId(int stage)
        {
            if (!ValidStage(stage)) throw new ArgumentOutOfRangeException(nameof(stage));
            int count = 0; for (int s = 1; s <= stage; s++) count += StageAdmissions(s); return count;
        }
        public static int FirstRewardId(int stage)
        { if (!ValidStage(stage)) throw new ArgumentOutOfRangeException(nameof(stage)); return stage == 1 ? 1 : LastRewardId(stage - 1) + 1; }
        public static int StageRoadOffset(int stage)
        { if (!ValidStage(stage)) throw new ArgumentOutOfRangeException(nameof(stage)); return 0; }
        public static int LaneForAdmissionSlot(int slot, int stage) => !ValidStage(stage) || slot < 0 || slot >= 8 ? -1 :
            CoopPartyCapacity.LaneForAdmissionSlot(Math.Min(slot, stage - 1));
        public static int ActiveLaneMask(int stage)
        {
            if (!ValidStage(stage)) return 0;
            int mask = 0; for (int slot = 0; slot < stage; slot++) mask |= 1 << CoopPartyCapacity.LaneForAdmissionSlot(slot); return mask;
        }
        public static int RotateMask(int canonicalMask, int stage) =>
            CoopPartyCapacity.ValidLaneMask(canonicalMask) && ValidStage(stage) ? canonicalMask : 0;
        public static string StageTheme(int stage)
        {
            switch (stage) { case 1: return "Ember"; case 2: return "Tide"; case 3: return "Storm";
                case 4: return "Four Roads"; case 5: return "Five Roads"; case 6: return "Six Roads";
                case 7: return "Seven Roads"; case 8: return "All Roads"; default: throw new ArgumentOutOfRangeException(nameof(stage)); }
        }
        public static float FacingYawDegrees(float spawnX, float spawnZ, float portalX, float portalZ)
        {
            if (!Finite(spawnX) || !Finite(spawnZ) || !Finite(portalX) || !Finite(portalZ) || spawnX == portalX && spawnZ == portalZ)
                throw new ArgumentOutOfRangeException();
            return (float)(Math.Atan2((double)portalX - spawnX, (double)portalZ - spawnZ) * 180d / Math.PI);
        }
        public static double WaveSeconds(int globalWave)
        {
            if (!CampaignBalance015.ValidWave(globalWave)) throw new ArgumentOutOfRangeException(nameof(globalWave));
            int local = (globalWave - 1) % WavesPerStage; return local == 4 ? 40d : local == 2 ? 10d : 25d;
        }
        public static double AdmissionSpacingForWave(int globalWave) => (WaveSeconds(globalWave) - 1d) / (AdmissionsForWave(globalWave) - 1);
        public static int PhaseAt(double elapsed)
        {
            if (!Finite(elapsed) || elapsed < 0d) return -1;
            // Planned display compatibility only. Real wave progression uses accepted state below.
            if (elapsed < 30d) return 0; if (elapsed < 60d) return 1;
            if (elapsed < 75d) return 2; return elapsed < 105d ? 3 : 4;
        }
        // Old timer/count-only signature cannot certify actual wave clears.
        public static CampaignStageOutcome Outcome(double elapsed, int accepted, bool shrineIntact) =>
            !Finite(elapsed) || elapsed < 0d || accepted < 0 || accepted > AdmissionsPerStage || !shrineIntact || elapsed >= MaximumStageSeconds
                ? CampaignStageOutcome.Failed : CampaignStageOutcome.Running;
        public static int FindLane(int[] alive, int[] admitted, int mask, int cursor)
        {
            if (alive == null || admitted == null || alive.Length < 8 || admitted.Length < 8 || !CoopPartyCapacity.ValidLaneMask(mask) || cursor < 0) return -1;
            int limit = (MaximumAlive + CoopPartyCapacity.LaneCount(mask) - 1) / CoopPartyCapacity.LaneCount(mask);
            int best = -1, leastAlive = int.MaxValue, leastAdmitted = int.MaxValue;
            for (int step = 0; step < 8; step++)
            {
                int lane = ((cursor & 7) + step) & 7;
                if (alive[lane] < 0 || alive[lane] > MaximumAlive || admitted[lane] < 0 || admitted[lane] > AdmissionsPerStage) return -1;
                if ((mask & (1 << lane)) == 0 || alive[lane] >= limit) continue;
                if (alive[lane] < leastAlive || alive[lane] == leastAlive && admitted[lane] < leastAdmitted)
                { best = lane; leastAlive = alive[lane]; leastAdmitted = admitted[lane]; }
            }
            return best;
        }
        public static bool HeavyAdmission(int globalWave, int ordinal) => globalWave % WavesPerStage == 0 && ordinal == AdmissionsForWave(globalWave) - 1;
        public static CurrentDefensePacing.EnemyProfile EnemyForWave(int globalWave, int ordinal)
        {
            int quota = AdmissionsForWave(globalWave);
            if (ordinal < 0 || ordinal >= quota) throw new ArgumentOutOfRangeException(nameof(ordinal));
            bool heavy = HeavyAdmission(globalWave, ordinal), swarm = (globalWave - 1) % WavesPerStage == 2;
            int archetype = heavy ? 0 : swarm ? 2 : ordinal % 10 < 5 ? 0 : ordinal % 10 < 8 ? 1 : 2;
            return new CurrentDefensePacing.EnemyProfile(archetype, archetype == 1 && globalWave >= 3,
                CampaignBalance015.EnemyHealth(globalWave), heavy ? .65f : swarm ? .9f : .78f, heavy ? 1.15f : .65f);
        }
        public static CurrentDefensePacing.EnemyProfile EnemyAt(int stage, int admitted)
        {
            if (!ValidStage(stage) || admitted < 0 || admitted >= StageAdmissions(stage)) throw new ArgumentOutOfRangeException();
            for (int local = 0; local < WavesPerStage; local++)
            { int wave = GlobalWave(stage, local), quota = AdmissionsForWave(wave); if (admitted < quota) return EnemyForWave(wave, admitted); admitted -= quota; }
            throw new InvalidOperationException();
        }
        internal static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }

    // Main-thread host-owned scalars only. No new objects/arrays per wave.
    public struct CampaignStageAdmissions
    {
        public int Stage { get; private set; }
        public int Count { get; private set; }
        public int WaveIndex { get; private set; }
        public int WaveAccepted { get; private set; }
        public int CompletedWaves { get; private set; }
        public int GlobalWave => CampaignStagePacing.GlobalWave(Stage, WaveIndex);
        public int WaveQuota => CampaignStagePacing.AdmissionsForWave(GlobalWave);
        public double WaveStartedAt { get; private set; }
        public double LastAdmissionAt { get; private set; }
        double nextAt;
        public double NextAt => nextAt;
        public double AdmissionDeadline => CampaignWaveDeadline015.AdmissionCutoff(GlobalWave,WaveStartedAt);
        public double WaveDeadline => WaveAccepted==WaveQuota
            ?CampaignWaveDeadline015.ClearDeadline(GlobalWave,WaveStartedAt,LastAdmissionAt):AdmissionDeadline;
        public bool CanAccept(double elapsed, int alive) => CampaignStagePacing.ValidStage(Stage) &&
            CompletedWaves < CampaignStagePacing.WavesPerStage && CampaignStagePacing.Finite(elapsed) && elapsed >= nextAt &&
            elapsed <= AdmissionDeadline && elapsed <= CampaignStagePacing.MaximumStageSeconds &&
            WaveAccepted < WaveQuota && alive >= 0 && alive < CampaignStagePacing.MaximumAlive;
        public bool CommitAccepted(double elapsed)
        {
            if (!CanAccept(elapsed, 0)) return false;
            Count++; WaveAccepted++; LastAdmissionAt=elapsed; nextAt = elapsed + CampaignStagePacing.AdmissionSpacingForWave(GlobalWave); return true;
        }
        public CampaignStageOutcome Tick(double elapsed, int alive, bool shrineIntact)
        {
            var outcome = Outcome(elapsed, alive, shrineIntact);
            if (outcome != CampaignStageOutcome.Running) return outcome;
            if (WaveAccepted == WaveQuota && alive == 0 && elapsed >= WaveStartedAt + CampaignStagePacing.WaveSeconds(GlobalWave))
            {
                CompletedWaves++;
                if (CompletedWaves == CampaignStagePacing.WavesPerStage) return Outcome(elapsed, alive, shrineIntact);
                WaveIndex++; WaveAccepted = 0; LastAdmissionAt=0d; WaveStartedAt = elapsed + CampaignStagePacing.InterWaveSeconds;
                nextAt = WaveStartedAt + CampaignStagePacing.FirstAdmissionAt;
            }
            return CampaignStageOutcome.Running;
        }
        public CampaignStageOutcome Outcome(double elapsed, int alive, bool shrineIntact)
        {
            if (!CampaignStagePacing.ValidStage(Stage) || !CampaignStagePacing.Finite(elapsed) || elapsed < 0d || elapsed > CampaignStagePacing.MaximumStageSeconds ||
                alive < 0 || alive > CampaignStagePacing.MaximumAlive || !shrineIntact) return CampaignStageOutcome.Failed;
            if (CompletedWaves == CampaignStagePacing.WavesPerStage)
                return Count == CampaignStagePacing.StageAdmissions(Stage) && alive == 0 ? CampaignStageOutcome.Succeeded : CampaignStageOutcome.Failed;
            // At the exact deadline, fully admitted/cleared waves may commit; underfill cannot.
            bool ready = WaveAccepted == WaveQuota && alive == 0 && elapsed >= WaveStartedAt + CampaignStagePacing.WaveSeconds(GlobalWave);
            double deadline=WaveDeadline;
            return elapsed>deadline || (elapsed>=CampaignStagePacing.MaximumStageSeconds || elapsed>=deadline)&&!ready
                ? CampaignStageOutcome.Failed : CampaignStageOutcome.Running;
        }
        public void Reset(int stage = 1)
        {
            if (!CampaignStagePacing.ValidStage(stage)) throw new ArgumentOutOfRangeException(nameof(stage));
            this = default; Stage = stage; nextAt = CampaignStagePacing.FirstAdmissionAt;
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing eight stage/lane identities, five actual quota/clear waves per milestone and simulation seconds.
// Array Formats: Existing borrowed eight-lane scalar arrays,48alive cap,905 logical source IDs within4096; state is a bounded value struct.
// Dependencies/Pointers: Host commits only real pooled admissions and real clear counts; no Object access/allocations/pointers; existing readiness/refuge/restart owners remain.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Accepted host milestone/wave elapsed simulation time and actual last admission;295second hard cap retained.
// Array Formats: Existing scalar admissions value state adds one double timestamp; no wire-layout/resource/buffer changes.
// Dependencies/Pointers: Separate admission cutoff reserves integrated travel plus30seconds combat grace; Director/XP/travel/Animator/clock owners unchanged, native fullwave viability pending.
