using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
namespace OdysseyOracle
{
    public struct LandingStandQuery020 {public LandingPoint020 Feet;public float Radius;}
    public struct LandingStandResult020 {public float Height;public byte Supported;}
    [BurstCompile]
    public struct PortalLandingStandJob020:IJobParallelFor
    {
        public LandingAdmission020 Admission;
        [ReadOnly]public NativeArray<LandingStandQuery020> Queries;
        [WriteOnly]public NativeArray<LandingStandResult020> Results;
        public void Execute(int index)
        {
            var query=Queries[index];bool supported=PortalLandingQueries020.TryStand(Admission,query.Feet,query.Radius,out float height);
            Results[index]=new LandingStandResult020{Height=height,Supported=supported?(byte)1:(byte)0};
        }
    }
    [BurstCompile]
    public struct PortalLandingMoveJob020:IJobParallelFor
    {
        public LandingAdmission020 Admission;
        [ReadOnly]public NativeArray<LandingPoint020> From,To;
        [WriteOnly]public NativeArray<byte> Allowed;
        public void Execute(int index){Allowed[index]=PortalLandingQueries020.TryMove(Admission,From[index],To[index])?(byte)1:(byte)0;}
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Explicit third-mode metre/Y-up numeric queries; no UnityEngine.Object or Transform access.
// Array Formats: Borrowed fixed query/result/point NativeArrays with one output index per scheduled worker; no allocation or disposal inside jobs.
// Dependencies/Pointers: Caller validates matching array lengths/count/admission and completes job readers before changing source generation, publishing results or disposing buffers. No scheduling, activation or native ownership is hidden here.
