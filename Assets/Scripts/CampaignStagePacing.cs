using System;

namespace OdysseyOracle
{
    public enum CampaignStageOutcome { Running, Succeeded, Failed }

    // Pure scalar schedule. Main-thread owner commits ONLY after a real pooled
    // spawn succeeds. Time is the existing simulation clock, never wall time.
    public static class CampaignStagePacing
    {
        public const int StageCount = 3, StageSeconds = 150, MaximumStageSeconds = 180;
        public const int AdmissionsPerStage = 100, PhaseCount = 5, MaximumAlive = 48;
        public const double FirstAdmissionAt = 3d, AdmissionSpacing = 1.25d;

        public static bool ValidStage(int stage) => stage >= 1 && stage <= StageCount;
        public static int StageRoadOffset(int stage)
        {
            if (!ValidStage(stage)) throw new ArgumentOutOfRangeException(nameof(stage));
            return stage - 1;
        }
        public static int LaneForAdmissionSlot(int slot, int stage)
        {
            int lane = CoopPartyCapacity.LaneForAdmissionSlot(slot);
            return lane < 0 || !ValidStage(stage) ? -1 : (lane + StageRoadOffset(stage)) & 7;
        }
        public static int RotateMask(int canonicalMask, int stage)
        {
            if (!CoopPartyCapacity.ValidLaneMask(canonicalMask) || !ValidStage(stage)) return 0;
            int offset = StageRoadOffset(stage);
            return ((canonicalMask << offset) | (canonicalMask >> (8 - offset))) & 255;
        }
        public static string StageTheme(int stage)
        {
            switch (stage)
            {
                case 1: return "Ember";
                case 2: return "Tide";
                case 3: return "Storm";
                default: throw new ArgumentOutOfRangeException(nameof(stage));
            }
        }
        public static float FacingYawDegrees(float spawnX, float spawnZ, float portalX, float portalZ)
        {
            if (!Finite(spawnX) || !Finite(spawnZ) || !Finite(portalX) || !Finite(portalZ) ||
                spawnX == portalX && spawnZ == portalZ) throw new ArgumentOutOfRangeException();
            return (float)(Math.Atan2((double)portalX - spawnX, (double)portalZ - spawnZ) * (180d / Math.PI));
        }
        public static int PhaseAt(double elapsed)
        {
            if (!Finite(elapsed) || elapsed < 0d) return -1;
            return Math.Min(PhaseCount - 1, (int)Math.Min(StageSeconds, elapsed) / 30);
        }
        public static CampaignStageOutcome Outcome(double elapsed, int accepted, bool shrineIntact)
        {
            if (!Finite(elapsed) || elapsed < 0d || accepted < 0 || accepted > AdmissionsPerStage || !shrineIntact)
                return CampaignStageOutcome.Failed;
            if (elapsed >= StageSeconds && accepted == AdmissionsPerStage) return CampaignStageOutcome.Succeeded;
            return elapsed >= MaximumStageSeconds ? CampaignStageOutcome.Failed : CampaignStageOutcome.Running;
        }
        // Lowest live pressure wins, then lowest admitted total; cursor breaks
        // ties. No per-lane quota can silently multiply/shrink the stage100 total.
        public static int FindLane(int[] alive, int[] admitted, int mask, int cursor)
        {
            if (alive == null || admitted == null || alive.Length < 8 || admitted.Length < 8 ||
                !CoopPartyCapacity.ValidLaneMask(mask) || cursor < 0) return -1;
            int limit = (MaximumAlive + CoopPartyCapacity.LaneCount(mask) - 1) / CoopPartyCapacity.LaneCount(mask);
            int best = -1, leastAlive = int.MaxValue, leastAdmitted = int.MaxValue;
            for (int step = 0; step < 8; step++)
            {
                int lane = ((cursor & 7) + step) & 7;
                if (alive[lane] < 0 || alive[lane] > MaximumAlive || admitted[lane] < 0 || admitted[lane] > AdmissionsPerStage)
                    return -1;
                if ((mask & (1 << lane)) == 0 || alive[lane] >= limit) continue;
                if (alive[lane] < leastAlive || alive[lane] == leastAlive && admitted[lane] < leastAdmitted)
                { best = lane; leastAlive = alive[lane]; leastAdmitted = admitted[lane]; }
            }
            return best;
        }
        public static CurrentDefensePacing.EnemyProfile EnemyAt(int stage, int admitted)
        {
            if (!ValidStage(stage) || admitted < 0 || admitted >= AdmissionsPerStage)
                throw new ArgumentOutOfRangeException();
            // Ten-admission motif: all three existing archetypes appear in every
            // stage; progressively more spiders and flying shades, no new art.
            int motif = admitted % 10;
            int stalkers = 6 - stage;
            int archetype = motif < stalkers ? 0 : motif < stalkers + 3 ? 1 : 2;
            bool heavy = archetype == 0 && admitted >= 80 && motif == 0;
            float health = heavy ? 46f + stage * 4f : 18f + stage * 4f + admitted / 25 * 2f;
            float speed = heavy ? .65f : .72f + stage * .06f;
            float damage = heavy ? 1.05f + stage * .1f : .45f + stage * .12f;
            bool flying = archetype == 1 && (stage >= 2 || admitted >= 50);
            return new CurrentDefensePacing.EnemyProfile(archetype, flying, health, speed, damage);
        }
        internal static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }

    public struct CampaignStageAdmissions
    {
        public int Count { get; private set; }
        double nextAt;
        public double NextAt => Count == 0 ? CampaignStagePacing.FirstAdmissionAt : nextAt;
        public bool CanAccept(double elapsed, int alive) => CampaignStagePacing.Finite(elapsed) &&
            elapsed >= NextAt && elapsed <= CampaignStagePacing.MaximumStageSeconds &&
            Count < CampaignStagePacing.AdmissionsPerStage && alive >= 0 && alive < CampaignStagePacing.MaximumAlive;
        public bool CommitAccepted(double elapsed)
        {
            if (!CanAccept(elapsed, 0)) return false;
            Count++;
            // Missed time never creates a timer-debt burst. Failed spawn/pool
            // attempts never call this method and consume no admission or ID.
            nextAt = elapsed + CampaignStagePacing.AdmissionSpacing;
            return true;
        }
        public void Reset() { Count = 0; nextAt = 0d; }
    }
}
// # ARCHITECTURE SNAPSHOT
// Unity metres/Y-up/XZ remain with World; canonical lane bits E..SE0..7 rotate
// by stage0/1/2 independently of stable roster slots. Fixed8 scalar int arrays
// are borrowed/read-only; CampaignStageAdmissions owns count/time scalars only.
// No UnityEngine.Object, transform, native pointer, allocation or disposal here.
