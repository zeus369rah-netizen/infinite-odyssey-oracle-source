using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using OdysseyOracle.Foundation.Pools015;

namespace OdysseyOracle
{
    [BurstCompile]
    public struct ProjectXpCatchupSources015:IJob
    {
        [ReadOnly] public NativeArray<XpSource64> Sources;
        [ReadOnly] public NativeArray<XpEntitlement32> Entitlements;
        public NativeArray<XpSource64> Projection;
        public void Execute()
        {
            for(int index=0;index<Projection.Length;index++)
            {
                XpSource64 source=Sources[index];int parentId=XpCatchupOrbs015.ParentId((int)source.SourceId);
                if(parentId==0){Projection[index]=default;continue;}
                var parent=Sources[parentId-1];uint eligible=XpCatchupOrbs015.EffectiveMask(source,parent,Entitlements[parentId-1]);
                if(eligible==0){Projection[index]=default;continue;}
                source.EligibleMask=eligible;source.ClaimedMask&=eligible;Projection[index]=source;
            }
        }
    }
    [BurstCompile]
    public struct ClearXpCatchup015:IJob
    {
        public NativeArray<XpEntitlement32> Entitlements;
        public NativeArray<uint> Dirty;
        public void Execute(){for(int i=0;i<Entitlements.Length;i++){Entitlements[i]=default;Dirty[i]=0;}}
    }
    [BurstCompile]
    public struct RequeueXpCatchup015:IJob
    {
        [ReadOnly] public NativeArray<XpEntitlement32> Entitlements;
        public NativeArray<uint> Dirty;
        public void Execute(){for(int i=0;i<Entitlements.Length;i++)Dirty[i]=XpCatchupOrbs015.Valid(Entitlements[i])?Entitlements[i].OwnerMask:0u;}
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Original source anchors unchanged; readonly effective recipient projection, not a second reward owner.
// Array Formats: Fixed4096source64 projection and905entitlement32/dirty uints; Burst numeric-only source/sidecar loops.
// Dependencies/Pointers: Director completes the source-reader fence before transactions/reset/disposal and chains projection before existing structural pair/merge/matrix/nearest jobs.
