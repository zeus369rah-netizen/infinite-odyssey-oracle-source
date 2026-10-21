using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace OdysseyOracle
{
    // Cold main-thread value owner. Never reset the pending slot during World teardown.
    public struct PortalLandingScratch020
    {
        NativeArray<LandingDraw020> draws;
        NativeArray<int> count,written,indices,assetFloorSpan;
        NativeArray<LandingVertex020> vertices;
        NativeArray<float3> extents;
        NativeArray<LandingStandQuery020> support;
        NativeArray<LandingStandResult020> supported;
        NativeArray<CampaignPlantSupportRay020> plantRays;
        NativeArray<CampaignPlantPatchAdmission020> plantAdmissions;
        JobHandle writer;
        bool completed,released;
        static PortalLandingScratch020 pending;
        static byte phase; // 0 idle,1 one canonical cold build,2 one retained pending scope.
        public static bool HasPending=>phase==2;
        internal static bool TryBeginBuild()
        {if(phase!=0)return false;phase=1;return true;}
        internal PortalLandingScratch020(ref NativeArray<LandingDraw020> sourceDraws,ref NativeArray<int> sourceCount,ref NativeArray<int> sourceWritten,ref NativeArray<int> sourceIndices,
            ref NativeArray<LandingVertex020> sourceVertices,ref NativeArray<float3> sourceExtents,ref NativeArray<LandingStandQuery020> sourceSupport,ref NativeArray<LandingStandResult020> sourceSupported,ref NativeArray<CampaignPlantSupportRay020> sourcePlantRays,ref NativeArray<CampaignPlantPatchAdmission020> sourcePlantAdmissions,ref NativeArray<int> sourceAssetFloorSpan,ref JobHandle sourceWriter)
        {
            draws=sourceDraws;count=sourceCount;written=sourceWritten;indices=sourceIndices;vertices=sourceVertices;extents=sourceExtents;support=sourceSupport;supported=sourceSupported;plantRays=sourcePlantRays;plantAdmissions=sourcePlantAdmissions;assetFloorSpan=sourceAssetFloorSpan;writer=sourceWriter;completed=released=false;
            sourceDraws=default;sourceCount=sourceWritten=sourceIndices=default;sourceVertices=default;sourceExtents=default;sourceSupport=default;sourceSupported=default;sourcePlantRays=default;sourcePlantAdmissions=default;sourceAssetFloorSpan=default;sourceWriter=default;
        }
        static void Release<T>(ref NativeArray<T> value,ref Exception firstError) where T:unmanaged
        {
            if(!value.IsCreated)return;
            try{value.Dispose();value=default;}
            catch(Exception error){if(firstError==null)firstError=error;}
        }
        public bool TryRelease(out Exception error)
        {
            error=null;released=false;
            if(!completed)
            {
                try{writer.Complete();completed=true;writer=default;}
                catch(Exception failure){error=failure;return false;}
            }
            Release(ref assetFloorSpan,ref error);Release(ref plantAdmissions,ref error);Release(ref plantRays,ref error);
            Release(ref supported,ref error);Release(ref support,ref error);
            Release(ref extents,ref error);Release(ref indices,ref error);Release(ref vertices,ref error);
            Release(ref written,ref error);Release(ref count,ref error);Release(ref draws,ref error);
            released=error==null;return released;
        }
        internal static void FinishBuild(ref PortalLandingScratch020 value)
        {
            if(phase!=1)throw new InvalidOperationException("Landing scratch requires its single cold build reservation.");
            if(value.released){value=default;phase=0;return;}
            pending=value;value=default;phase=2;
        }
        public static bool RetryPending(out Exception error)
        {
            error=null;
            if(phase==0)return true;
            if(phase!=2){error=new InvalidOperationException("Landing scratch retry is unavailable during a cold build.");return false;}
            if(!pending.TryRelease(out error))return false;
            pending=default;phase=0;return true;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: unchanged World-local geometry; eight preexisting plus two garden typed native scratch handles and one optional catalogue span and their writer fence move into this value owner.
// Array Formats: one bounded eleven-array/JobHandle scope, one static pending scope and idle/building/pending phase; two added bounded garden native stores, no managed collection, Object reference or frame callback.
// Dependencies/Pointers: canonical main-thread Prepare reserves before allocation and moves locals in finally. Completion failure retains every array/fence; disposal failure retains only failed containers. Explicit cold RetryPending alone retires pending ownership across World teardown; root owns actual native acceptance.
