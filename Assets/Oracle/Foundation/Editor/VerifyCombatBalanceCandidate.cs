using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using OdysseyOracle.Foundation;
using OdysseyOracle.Foundation.Combat;

namespace OdysseyOracle.Diagnostics
{
    [Serializable]
    public sealed class CombatBalanceVerificationResult
    {
        public bool Passed;
        public string UnityVersion;
        public int Assertions;
        public int ScheduledJobsCompleted;
        public bool BurstMachineCodeVerified;
        public bool CleanupRetained;
        public string Error;
    }

    // External, opt-in, cold diagnostic. No runtime registration or default-world access.
    public static class VerifyCombatBalanceCandidate
    {
        private static Fixture pendingCleanup;

        public static CombatBalanceVerificationResult Run()
        {
            var result = new CombatBalanceVerificationResult
            {
                UnityVersion = UnityEngine.Application.unityVersion,
                BurstMachineCodeVerified = false,
                Error = string.Empty
            };
            if (pendingCleanup != null)
            {
                result.CleanupRetained = true;
                result.Error = "Prior diagnostic cleanup requires explicit RetryCleanup().";
                return result;
            }
            var fixture = new Fixture(result);
            pendingCleanup = fixture; // Retain the owner before any native allocation.
            try
            {
                fixture.Initialize();
                CheckLayouts(fixture);
                CheckSelection(fixture);
                CheckInvalidInputs(fixture);
                CheckAcceptance(fixture);
                CheckExtremes(fixture);
                result.Passed = true;
            }
            catch (Exception error) { result.Error = error.ToString(); }
            finally
            {
                try { fixture.Dispose(); pendingCleanup = null; }
                catch (Exception cleanupError)
                {
                    result.Passed = false;
                    result.CleanupRetained = true;
                    result.Error += "\nCleanup retained: " + cleanupError;
                }
            }
            return result;
        }

        public static void RetryCleanup()
        {
            if (pendingCleanup == null) return;
            pendingCleanup.Dispose();
            pendingCleanup = null;
        }

        private static TargetCandidate Row(int x, int z, ulong id, uint generation = 1u) =>
            new TargetCandidate
            {
                GridPosition = new int2(x, z), StableId = id, Generation = generation,
                Health = 100f, Active = 1u, Status = 0u
            };

        private static TowerGridTargetState Tower(int x = 0, int z = 0, int range = 3,
            uint metric = 0u, double deadline = 0d, uint sequence = 0u) =>
            new TowerGridTargetState
            {
                GridPosition = new int2(x, z), Range = range, DistanceMetric = metric,
                NextAcceptedShotAt = deadline, AcceptedShotSequence = sequence,
                TargetId = 999u, TargetGeneration = 999u, QueryStatus = 999u,
                TargetDistance = 999u, HasTarget = 1u, FireEligible = 1u,
                SelectionSnapshotGeneration = 999u
            };

        private static void CheckLayouts(Fixture f)
        {
            f.Check(UnsafeUtility.IsBlittable<CombatConfig>() && UnsafeUtility.SizeOf<CombatConfig>() == 16, "config blittable16");
            f.Check(UnsafeUtility.IsBlittable<TargetCandidate>() && UnsafeUtility.SizeOf<TargetCandidate>() == 32, "candidate blittable32");
            f.Check(UnsafeUtility.IsBlittable<TowerGridTargetState>() && UnsafeUtility.SizeOf<TowerGridTargetState>() == 64, "state blittable64");
            Offset<CombatConfig>(f, "MonsterHealth", 0); Offset<CombatConfig>(f, "PlayerDamage", 4);
            Offset<CombatConfig>(f, "TowerDamage", 8); Offset<CombatConfig>(f, "TowerCooldown", 12);
            Offset<TargetCandidate>(f, "GridPosition", 0); Offset<TargetCandidate>(f, "StableId", 8);
            Offset<TargetCandidate>(f, "Generation", 16); Offset<TargetCandidate>(f, "Health", 20);
            Offset<TargetCandidate>(f, "Status", 24); Offset<TargetCandidate>(f, "Active", 28);
            Offset<TowerGridTargetState>(f, "GridPosition", 0); Offset<TowerGridTargetState>(f, "Range", 8);
            Offset<TowerGridTargetState>(f, "DistanceMetric", 12); Offset<TowerGridTargetState>(f, "TargetId", 16);
            Offset<TowerGridTargetState>(f, "TargetGeneration", 24); Offset<TowerGridTargetState>(f, "QueryStatus", 28);
            Offset<TowerGridTargetState>(f, "TargetDistance", 32); Offset<TowerGridTargetState>(f, "NextAcceptedShotAt", 40);
            Offset<TowerGridTargetState>(f, "HasTarget", 48); Offset<TowerGridTargetState>(f, "FireEligible", 52);
            Offset<TowerGridTargetState>(f, "AcceptedShotSequence", 56); Offset<TowerGridTargetState>(f, "SelectionSnapshotGeneration", 60);
            CombatConfig config = CombatConfig.Default;
            f.Check(config.IsValid && config.MonsterHealth == 100f && config.PlayerDamage == 25f &&
                config.TowerDamage == 25f && config.TowerCooldown == 1.5f, "exact balance defaults");
            f.Check(GridTargetingRules.MaximumCandidates == 48, "preserve48 cap");
        }

        private static void Offset<T>(Fixture f, string name, int expected) where T : struct =>
            f.Check(Marshal.OffsetOf<T>(name).ToInt64() == expected, typeof(T).Name + "." + name + " offset");

        private static void CheckSelection(Fixture f)
        {
            f.Rows();
            Empty(f, f.Select(Tower(deadline: 7d, sequence: 4u), 0), TowerTargetQueryStatus.NoTarget, "loaded empty");
            f.Rows(Row(3, 3, 9));
            TowerGridTargetState state = f.Select(Tower(), 1);
            Selected(f, state, 9u, 1u, 3u, 1u, "Chebyshev boundary");
            Empty(f, f.Select(Tower(metric: 1u), 1), TowerTargetQueryStatus.NoTarget, "Manhattan excludes diagonal");
            f.Rows(Row(1, 2, 8));
            Selected(f, f.Select(Tower(metric: 1u), 1), 8u, 1u, 3u, 1u, "Manhattan boundary");
            f.Rows(Row(2, 0, 99), Row(0, -2, 2));
            Selected(f, f.Select(Tower(), 2), 2u, 1u, 2u, 1u, "ID tie");
            f.Rows(Row(0, -2, 2), Row(2, 0, 99));
            Selected(f, f.Select(Tower(), 2), 2u, 1u, 2u, 1u, "reordered ID tie");
            TargetCandidate inactive = Row(0, 0, 1); inactive.Active = 0u;
            TargetCandidate dead = Row(0, 0, 2); dead.Health = 0f;
            TargetCandidate nan = Row(0, 0, 3); nan.Health = float.NaN;
            TargetCandidate inf = Row(0, 0, 4); inf.Health = float.PositiveInfinity;
            TargetCandidate staleGeneration = Row(0, 0, 5, 0u);
            TargetCandidate statusDead = Row(0, 0, 6); statusDead.Status = (uint)TargetAvailabilityStatus.Dead;
            TargetCandidate untargetable = Row(0, 0, 7); untargetable.Status = (uint)TargetAvailabilityStatus.Untargetable;
            TargetCandidate unloaded = Row(0, 0, 8); unloaded.Status = (uint)TargetAvailabilityStatus.Unloaded;
            f.Rows(inactive, dead, nan, inf, staleGeneration, statusDead, untargetable, unloaded, Row(1, 0, 0));
            Selected(f, f.Select(Tower(), 9), 0u, 1u, 1u, 1u, "reject ineligible and preserve valid ID0");
            for (int i = 0; i < GridTargetingRules.MaximumCandidates; i++) f.Candidates[i] = Row(i + 1, 0, (ulong)(100 - i));
            Selected(f, f.Select(Tower(range: 48), 48), 100u, 1u, 1u, 1u, "full fixed pool");
            f.Rows(Row(1, 0, 3, 1), Row(1, 0, 3, 2));
            Empty(f, f.Select(Tower(), 2), TowerTargetQueryStatus.InvalidSnapshot, "observed chosen-ID generation ambiguity");
            f.Rows(Row(0, 0, 10));
            state = f.Select(Tower(), 1);
            TowerGridTargetState other = f.SecondState;
            f.Check(other.HasTarget == 0u && other.NextAcceptedShotAt == 123d && other.AcceptedShotSequence == 17u,
                "parallel second entity owns separate output and deadline");
            f.Check(f.Candidates[0].Health == 100f && f.Candidates[0].Generation == 1u, "selection never damages candidate");
        }

        private static void CheckInvalidInputs(Fixture f)
        {
            f.Rows(Row(0, 0, 1));
            Empty(f, f.Select(Tower(), -1), TowerTargetQueryStatus.Unloaded, "unloaded count sentinel");
            Empty(f, f.Select(Tower(), -2), TowerTargetQueryStatus.InvalidInput, "negative count");
            Empty(f, f.Select(Tower(), 49), TowerTargetQueryStatus.InvalidInput, "over cap count");
            Empty(f, f.Select(Tower(range: -1), 1), TowerTargetQueryStatus.InvalidInput, "negative range");
            Empty(f, f.Select(Tower(metric: 2u), 1), TowerTargetQueryStatus.InvalidInput, "invalid metric");
            Empty(f, f.Select(Tower(), 1, snapshot: 0u), TowerTargetQueryStatus.Unloaded, "unloaded header");
            Empty(f, f.Select(Tower(), 1, snapshot: 8u), TowerTargetQueryStatus.StaleSnapshot, "stale snapshot");
            Empty(f, f.Select(Tower(), 1, required: 0u), TowerTargetQueryStatus.InvalidInput, "invalid required generation");
            Empty(f, f.Select(Tower(), 1, now: double.NaN), TowerTargetQueryStatus.InvalidInput, "NaN time");
            Empty(f, f.Select(Tower(), 1, now: double.PositiveInfinity), TowerTargetQueryStatus.InvalidInput, "infinite time");
            Empty(f, f.Select(Tower(), 1, now: -1d), TowerTargetQueryStatus.InvalidInput, "negative time");
            Empty(f, f.Select(Tower(deadline: double.PositiveInfinity), 1), TowerTargetQueryStatus.InvalidInput, "infinite deadline");
            Empty(f, f.Select(Tower(deadline: -1d), 1), TowerTargetQueryStatus.InvalidInput, "negative deadline");
            f.Config = CombatConfig.Default; f.Config.MonsterHealth = float.NaN;
            Empty(f, f.Select(Tower(), 1), TowerTargetQueryStatus.InvalidInput, "NaN config");
            f.Config = CombatConfig.Default; f.Config.TowerCooldown = 0f;
            Empty(f, f.Select(Tower(), 1), TowerTargetQueryStatus.InvalidInput, "zero cooldown config");
            f.Config = CombatConfig.Default;
            TowerGridTargetState tower = Tower();
            f.Check(GridTargetingRules.ValidateInputs(default, 0, f.Config, 10d, tower, 7u, 7u) ==
                TowerTargetQueryStatus.InvalidInput, "uncreated array preflight only; never schedule it");
            using (var shortArray = new NativeArray<TargetCandidate>(2, Allocator.TempJob))
                f.Check(GridTargetingRules.ValidateInputs(shortArray, 3, f.Config, 10d, tower, 7u, 7u) ==
                    TowerTargetQueryStatus.InvalidInput, "count exceeds backing length");
        }

        private static void CheckAcceptance(Fixture f)
        {
            f.Rows(Row(1, 0, 8, 3));
            TowerGridTargetState state = f.Select(Tower(deadline: 10d), 1, now: 9.999d);
            Selected(f, state, 8u, 3u, 1u, 0u, "deadline before boundary");
            state = f.Select(Tower(deadline: 10d), 1, now: 10d);
            Selected(f, state, 8u, 3u, 1u, 1u, "deadline exact boundary");
            TargetCandidate current = f.Candidates[0];
            f.Check(!TowerShotAcceptance.TryCommitAcceptedShot(ref state, current, 7u, f.Config, 10d, 1u, false), "rejected shot cannot commit");
            f.Check(state.NextAcceptedShotAt == 10d && state.AcceptedShotSequence == 0u && state.FireEligible == 1u, "rejection leaves cadence unchanged");
            TargetCandidate reused = current; reused.Generation++;
            f.Check(!TowerShotAcceptance.TryCommitAcceptedShot(ref state, reused, 7u, f.Config, 10d, 1u, true), "reject reused target generation");
            f.Check(!TowerShotAcceptance.TryCommitAcceptedShot(ref state, current, 8u, f.Config, 10d, 1u, true), "reject changed snapshot");
            f.Check(!TowerShotAcceptance.TryCommitAcceptedShot(ref state, current, 7u, f.Config, 10d, 0u, true), "reject zero sequence");
            f.Check(state.NextAcceptedShotAt == 10d && state.AcceptedShotSequence == 0u, "all stale rejections preserve deadline");
            f.Check(TowerShotAcceptance.TryCommitAcceptedShot(ref state, current, 7u, f.Config, 10d, 1u, true), "accepted shot commits");
            f.Check(state.NextAcceptedShotAt == 11.5d && state.AcceptedShotSequence == 1u && state.FireEligible == 0u, "accepted shot alone adds1.5 seconds");
            state = f.Select(state, 1, now: 10.1d);
            f.Check(state.FireEligible == 0u, "cooldown retained on reselection");
            state = f.Select(state, 1, now: 11.5d);
            f.Check(state.FireEligible == 1u, "cooldown boundary eligible");
            f.Check(!TowerShotAcceptance.TryCommitAcceptedShot(ref state, current, 7u, f.Config, 11.5d, 1u, true), "replay sequence rejected");
            f.Check(TowerShotAcceptance.TryCommitAcceptedShot(ref state, current, 7u, f.Config, 11.5d, 2u, true) && state.NextAcceptedShotAt == 13d,
                "next unique accepted sequence commits");
            f.Check(f.Candidates[0].Health == 100f && current.Health == 100f, "acceptance helper never damages");
            f.Check(!TowerShotAcceptance.TryGetNextDeadline(double.MaxValue, f.Config, out _), "unrepresentable deadline rejected");
            f.Check(!TowerShotAcceptance.TryGetNextDeadline(double.NaN, f.Config, out _), "NaN accepted time rejected");
            f.Check(TowerShotAcceptance.TryGetNextDeadline(20d, f.Config, out double next) && next == 21.5d, "pure deadline helper");
            f.Rows(reused);
            Selected(f, f.Select(Tower(), 1), 8u, 4u, 1u, 1u, "fresh published generation can be selected");
        }

        private static void CheckExtremes(Fixture f)
        {
            int2 minimum = new int2(int.MinValue, int.MinValue);
            int2 maximum = new int2(int.MaxValue, int.MaxValue);
            f.Check(GridTargetingRules.Distance(minimum, maximum, 0u) == 4294967295UL, "widened Chebyshev extremes");
            f.Check(GridTargetingRules.Distance(minimum, maximum, 1u) == 8589934590UL, "widened Manhattan extremes");
            f.Check(GridTargetingRules.Distance(maximum, minimum, 1u) == 8589934590UL, "reverse Manhattan extremes");
            f.Rows(Row(int.MaxValue, int.MaxValue, 1));
            Empty(f, f.Select(Tower(int.MinValue, int.MinValue, int.MaxValue), 1), TowerTargetQueryStatus.NoTarget, "scheduled extreme Chebyshev cannot wrap");
            Empty(f, f.Select(Tower(int.MinValue, int.MinValue, int.MaxValue, 1u), 1), TowerTargetQueryStatus.NoTarget, "scheduled extreme Manhattan cannot wrap");
            f.Rows(Row(int.MinValue + 1, int.MinValue, 2));
            Selected(f, f.Select(Tower(int.MinValue, int.MinValue, 1, 1u), 1), 2u, 1u, 1u, 1u, "scheduled near-min boundary");
        }

        private static void Empty(Fixture f, in TowerGridTargetState state, TowerTargetQueryStatus status, string name)
        {
            f.Check(state.QueryStatus == (uint)status && state.HasTarget == 0u && state.FireEligible == 0u &&
                state.TargetId == 0u && state.TargetGeneration == 0u && state.TargetDistance == ulong.MaxValue &&
                state.SelectionSnapshotGeneration == 0u, name);
        }

        private static void Selected(Fixture f, in TowerGridTargetState state, ulong id, uint generation,
            ulong distance, uint ready, string name)
        {
            f.Check(state.QueryStatus == (uint)TowerTargetQueryStatus.Selected && state.HasTarget == 1u &&
                state.TargetId == id && state.TargetGeneration == generation && state.TargetDistance == distance &&
                state.FireEligible == ready && state.SelectionSnapshotGeneration == 7u, name);
        }

        private sealed class Fixture : IDisposable
        {
            private readonly CombatBalanceVerificationResult result;
            private World world;
            private CombatBalanceProbeSystem scheduler;
            private EntityQuery query;
            private bool hasQuery;
            private Entity tower;
            private Entity second;
            private JobHandle reader;
            public NativeArray<TargetCandidate> Candidates;
            public CombatConfig Config = CombatConfig.Default;
            public TowerGridTargetState SecondState => world.EntityManager.GetComponentData<TowerGridTargetState>(second);

            public Fixture(CombatBalanceVerificationResult result) { this.result = result; }

            public void Initialize()
            {
                world = new World("Oracle combat candidate isolated diagnostic", WorldFlags.None);
                scheduler = world.CreateSystemManaged<CombatBalanceProbeSystem>();
                EntityManager manager = world.EntityManager;
                tower = manager.CreateEntity(typeof(TowerGridTargetState));
                second = manager.CreateEntity(typeof(TowerGridTargetState));
                query = manager.CreateEntityQuery(ComponentType.ReadWrite<TowerGridTargetState>());
                hasQuery = true;
                Candidates = new NativeArray<TargetCandidate>(GridTargetingRules.MaximumCandidates, Allocator.Persistent);
            }

            public void Check(bool passed, string name)
            {
                result.Assertions++;
                if (!passed) throw new InvalidOperationException(name);
            }

            public void Rows(params TargetCandidate[] rows)
            {
                reader.Complete();
                for (int i = 0; i < Candidates.Length; i++) Candidates[i] = default;
                for (int i = 0; i < rows.Length; i++) Candidates[i] = rows[i];
            }

            public TowerGridTargetState Select(in TowerGridTargetState input, int count, double now = 10d,
                uint snapshot = 7u, uint required = 7u)
            {
                reader.Complete();
                EntityManager manager = world.EntityManager;
                manager.SetComponentData(tower, input);
                manager.SetComponentData(second, Tower(111111, 111111, 0, 0u, 123d, 17u));
                scheduler.TargetQuery = query;
                scheduler.Candidates = Candidates;
                scheduler.CandidateCount = count;
                scheduler.Config = Config;
                scheduler.SimulationSeconds = now;
                scheduler.SnapshotGeneration = snapshot;
                scheduler.RequiredSnapshotGeneration = required;
                scheduler.Update();
                reader = scheduler.LastScheduled;
                reader.Complete();
                result.ScheduledJobsCompleted++;
                TowerGridTargetState output = manager.GetComponentData<TowerGridTargetState>(tower);
                Check(output.NextAcceptedShotAt.Equals(input.NextAcceptedShotAt) && output.AcceptedShotSequence == input.AcceptedShotSequence,
                    "selection preserves accepted deadline and sequence");
                Check(output.GridPosition.Equals(input.GridPosition) && output.Range == input.Range && output.DistanceMetric == input.DistanceMetric,
                    "selection preserves tower query inputs");
                return output;
            }

            public void Dispose()
            {
                reader.Complete(); // Never mutate or release candidate storage before its reader fence.
                if (Candidates.IsCreated) Candidates.Dispose();
                if (hasQuery) { query.Dispose(); hasQuery = false; }
                if (world != null) { world.Dispose(); world = null; }
            }
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: numeric signed XZ cells and accepted simulation seconds; no world/metre conversion.
// Array Formats: isolated World with two64B tower components and one owned NativeArray<TargetCandidate32B>[48].
// Dependencies/Pointers: actual generated IJobEntity ScheduleParallel(query)+Complete; fences precede writes/disposal.
// Sealed fixture owns native storage/World; failed cleanup retains owner. EntityManager/managed params are cold fixture-only.
// No default World/runtime hook/damage/owner transfer or assertion of Burst machine-code/whole-game ZeroGC.
