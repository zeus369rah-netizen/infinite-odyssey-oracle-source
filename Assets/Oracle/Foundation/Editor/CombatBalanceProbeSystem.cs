using OdysseyOracle.Foundation.Combat;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace OdysseyOracle.Diagnostics
{
    // Source-generated scheduling requires a system context. This system is created only by the isolated diagnostic World.
    [DisableAutoCreation]
    public partial class CombatBalanceProbeSystem : SystemBase
    {
        public EntityQuery TargetQuery;
        public NativeArray<TargetCandidate> Candidates;
        public int CandidateCount;
        public OdysseyOracle.Foundation.CombatConfig Config;
        public double SimulationSeconds;
        public uint SnapshotGeneration;
        public uint RequiredSnapshotGeneration;
        public JobHandle LastScheduled;

        protected override void OnUpdate()
        {
            var job = new TowerGridTargetingJob(Candidates, CandidateCount, Config, SimulationSeconds,
                SnapshotGeneration, RequiredSnapshotGeneration);
            LastScheduled = job.ScheduleParallel(TargetQuery, Dependency);
            Dependency = LastScheduled;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Isolated cold diagnostic World, signed XZ grid and simulation seconds.
// Array Formats: Borrowed immutable32B candidates and64B tower components; generated IJobEntity parallel scheduling.
// Dependencies/Pointers: DisableAutoCreation; no default-World registration. Fixture owns all data and completes LastScheduled before writes/disposal.
