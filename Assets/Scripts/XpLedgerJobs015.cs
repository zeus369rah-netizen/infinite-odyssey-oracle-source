using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using OdysseyOracle.Foundation.Pools015;

namespace OdysseyOracle
{
    [BurstCompile]
    public struct ClearXpVersions015:IJob
    {
        public NativeArray<uint> Versions;
        public void Execute(){for(int i=0;i<Versions.Length;i++)Versions[i]=0;}
    }
    [BurstCompile]
    public struct ClearXpLedgerJob015 : IJob
    {
        public NativeArray<XpSource64> Sources;
        public NativeArray<uint> Dirty;
        public void Execute()
        { for (int i = 0; i < Sources.Length; i++) { Sources[i] = default; Dirty[i] = 0; } }
    }
    [BurstCompile]
    public struct RebindXpLeaseJob015 : IJob
    {
        public NativeArray<XpSource64> Sources;
        public NativeArray<uint> Dirty;
        public int Epoch, Generation;
        public void Execute()
        {
            if (Epoch <= 0 || Generation <= 0) return;
            for (int i = 0; i < Sources.Length; i++)
            {
                var source = Sources[i]; if (source.SourceId == 0) continue;
                source.RunEpoch = Epoch; source.RewardGeneration = Generation; Sources[i] = source;
                Dirty[i] = source.Flags == PoolXpMath015.Issued ? source.EligibleMask : 0u;
            }
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Accepted cold normal-milestone lease epochs only; original source positions/value/owners/claim masks stay unchanged.
// Array Formats: Exact4096 logical source records/dirty words; reserved Flags0 stay unclaimable until actual death.
// Dependencies/Pointers: Source owner schedules/completes before publication; no Unity Object APIs, per-source allocation or competing world/reward ownership.
