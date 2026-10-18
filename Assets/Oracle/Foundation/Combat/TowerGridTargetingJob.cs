using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace OdysseyOracle.Foundation.Combat
{
    public static class GridTargetingRules
    {
        public const int MaximumCandidates = 48;

        public static bool IsEligible(in TargetCandidate candidate) =>
            candidate.Active == 1u && candidate.Generation != 0u &&
            candidate.Status == (uint)TargetAvailabilityStatus.Normal &&
            math.isfinite(candidate.Health) && candidate.Health > 0f;

        public static ulong Distance(int2 from, int2 to, uint metric)
        {
            long x = (long)to.x - from.x;
            long z = (long)to.y - from.y;
            ulong dx = (ulong)(x < 0 ? -x : x);
            ulong dz = (ulong)(z < 0 ? -z : z);
            return metric == (uint)GridDistanceMetric.Chebyshev ? (dx > dz ? dx : dz) :
                metric == (uint)GridDistanceMetric.Manhattan ? dx + dz : ulong.MaxValue;
        }

        public static bool IsInRange(in TowerGridTargetState tower, in TargetCandidate candidate)
        {
            return tower.Range >= 0 && tower.DistanceMetric <= (uint)GridDistanceMetric.Manhattan &&
                IsEligible(candidate) && Distance(tower.GridPosition, candidate.GridPosition, tower.DistanceMetric) <= (ulong)tower.Range;
        }

        public static TowerTargetQueryStatus ValidateInputs(NativeArray<TargetCandidate> candidates,
            int count, in CombatConfig config, double now, in TowerGridTargetState tower,
            uint snapshotGeneration, uint requiredSnapshotGeneration)
        {
            if (!config.IsValid || !math.isfinite(now) || now < 0d ||
                !math.isfinite(tower.NextAcceptedShotAt) || tower.NextAcceptedShotAt < 0d ||
                tower.Range < 0 || tower.DistanceMetric > (uint)GridDistanceMetric.Manhattan ||
                requiredSnapshotGeneration == 0u)
                return TowerTargetQueryStatus.InvalidInput;
            if (count == -1 || snapshotGeneration == 0u) return TowerTargetQueryStatus.Unloaded;
            if (!candidates.IsCreated || count < 0 || count > MaximumCandidates || count > candidates.Length)
                return TowerTargetQueryStatus.InvalidInput;
            if (snapshotGeneration != requiredSnapshotGeneration) return TowerTargetQueryStatus.StaleSnapshot;
            return TowerTargetQueryStatus.NoTarget;
        }
    }

    [BurstCompile]
    public partial struct TowerGridTargetingJob : IJobEntity
    {
        [ReadOnly] public readonly NativeArray<TargetCandidate> Candidates;
        public readonly int CandidateCount;
        public readonly CombatConfig Config;
        public readonly double SimulationSeconds;
        public readonly uint SnapshotGeneration;
        public readonly uint RequiredSnapshotGeneration;

        public TowerGridTargetingJob(NativeArray<TargetCandidate> candidates, int candidateCount,
            in CombatConfig config, double simulationSeconds, uint snapshotGeneration, uint requiredSnapshotGeneration)
            : this()
        {
            Candidates = candidates; CandidateCount = candidateCount; Config = config;
            SimulationSeconds = simulationSeconds; SnapshotGeneration = snapshotGeneration;
            RequiredSnapshotGeneration = requiredSnapshotGeneration;
        }

        void Execute(ref TowerGridTargetState tower)
        {
            tower.TargetId = 0u;
            tower.TargetGeneration = 0u;
            tower.TargetDistance = ulong.MaxValue;
            tower.HasTarget = tower.FireEligible = 0u;
            tower.SelectionSnapshotGeneration = 0u;
            TowerTargetQueryStatus status = GridTargetingRules.ValidateInputs(Candidates, CandidateCount,
                Config, SimulationSeconds, tower, SnapshotGeneration, RequiredSnapshotGeneration);
            tower.QueryStatus = (uint)status;
            if (status != TowerTargetQueryStatus.NoTarget) return;
            bool chosenIdAmbiguous = false;
            for (int i = 0; i < CandidateCount; i++)
            {
                TargetCandidate candidate = Candidates[i];
                if (!GridTargetingRules.IsEligible(candidate)) continue;
                ulong distance = GridTargetingRules.Distance(tower.GridPosition, candidate.GridPosition, tower.DistanceMetric);
                if (distance > (ulong)tower.Range) continue;
                // Constant-work defensive check for the currently chosen ID only.
                // Full identity uniqueness belongs to the immutable snapshot publisher.
                if (tower.HasTarget != 0u && candidate.StableId == tower.TargetId &&
                    candidate.Generation != tower.TargetGeneration) chosenIdAmbiguous = true;
                if (tower.HasTarget != 0u && (distance > tower.TargetDistance ||
                    distance == tower.TargetDistance && candidate.StableId >= tower.TargetId)) continue;
                if (tower.HasTarget == 0u || candidate.StableId != tower.TargetId) chosenIdAmbiguous = false;
                tower.TargetId = candidate.StableId;
                tower.TargetGeneration = candidate.Generation;
                tower.TargetDistance = distance;
                tower.HasTarget = 1u;
            }
            if (tower.HasTarget == 0u) return;
            if (chosenIdAmbiguous)
            {
                tower.TargetId = 0u; tower.TargetGeneration = 0u;
                tower.TargetDistance = ulong.MaxValue; tower.HasTarget = 0u;
                tower.QueryStatus = (uint)TowerTargetQueryStatus.InvalidSnapshot;
                return;
            }
            tower.QueryStatus = (uint)TowerTargetQueryStatus.Selected;
            tower.SelectionSnapshotGeneration = SnapshotGeneration;
            tower.FireEligible = SimulationSeconds >= tower.NextAcceptedShotAt ? 1u : 0u;
        }
    }

    public static class TowerShotAcceptance
    {
        public static bool CanAccept(in TowerGridTargetState tower, in TargetCandidate currentCandidate,
            uint currentSnapshotGeneration, in CombatConfig config, double now)
        {
            return config.IsValid && math.isfinite(now) && now >= 0d &&
                math.isfinite(tower.NextAcceptedShotAt) && tower.NextAcceptedShotAt >= 0d &&
                now >= tower.NextAcceptedShotAt && tower.HasTarget == 1u && tower.FireEligible == 1u &&
                tower.QueryStatus == (uint)TowerTargetQueryStatus.Selected && currentSnapshotGeneration != 0u &&
                tower.SelectionSnapshotGeneration == currentSnapshotGeneration &&
                tower.TargetId == currentCandidate.StableId && tower.TargetGeneration == currentCandidate.Generation &&
                GridTargetingRules.IsInRange(tower, currentCandidate);
        }

        public static bool TryGetNextDeadline(double acceptedAt, in CombatConfig config, out double nextDeadline)
        {
            nextDeadline = 0d;
            if (!config.IsValid || !math.isfinite(acceptedAt) || acceptedAt < 0d) return false;
            double proposed = acceptedAt + config.TowerCooldown;
            if (!math.isfinite(proposed) || proposed <= acceptedAt) return false;
            nextDeadline = proposed;
            return true;
        }

        // Pure trusted-adapter commit only. An actual accepted shot must precede accepted=true.
        // It never applies damage, allocates a projectile or accepts a client authority claim.
        public static bool TryCommitAcceptedShot(ref TowerGridTargetState tower, in TargetCandidate currentCandidate,
            uint currentSnapshotGeneration, in CombatConfig config, double now, uint shotSequence, bool accepted)
        {
            if (!accepted || shotSequence == 0u || shotSequence <= tower.AcceptedShotSequence ||
                !CanAccept(tower, currentCandidate, currentSnapshotGeneration, config, now) ||
                !TryGetNextDeadline(now, config, out double nextDeadline)) return false;
            tower.NextAcceptedShotAt = nextDeadline;
            tower.AcceptedShotSequence = shotSequence;
            tower.FireEligible = 0u;
            return true;
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: signed XZ grid cells, widened long/ulong grid distances, accepted simulation-seconds deadline.
// Array Formats: borrowed read-only TargetCandidate32B[<=48]; per-entity64B target state; shared16B config.
// Dependencies/Pointers: IJobEntity/Burst plus native snapshot lease through all reader fences; job never disposes storage.
// No Physics/UnityObject/damage/allocation/default World/registration; only explicit accepted-shot commit advances deadline.
