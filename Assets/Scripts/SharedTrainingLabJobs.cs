using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace OdysseyOracle
{
    [BurstCompile]
    public struct SharedLabReadyJob:IJobParallelFor
    {
        [ReadOnly] public NativeArray<SharedLabCastState> Peers;
        [WriteOnly] public NativeArray<byte> Ready;
        public double Now;
        public void Execute(int index){var peer=Peers[index];Ready[index]=(byte)(peer.Generation!=0&&SharedLabCastMath.Ready(peer,Now,true)?1:0);}
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Pure host simulation seconds; no scene coordinates queried by job.
// Array Formats: Read-only2 cast records, exactly2 readiness bytes; owner carries/completes JobHandle before launch/reset/dispose.
// Dependencies/Pointers: No Unity Object calls, transport, allocation, pool mutation or HP access in Burst; actual launching stays existing main-thread Combat owner.
