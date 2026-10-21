using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace OdysseyOracle
{
    [BurstCompile]
    public struct CampaignPortalReachJob:IJob,ICampaignPortalPoints
    {
        [ReadOnly]public NativeArray<CampaignPortalPoint16> Points;
        [WriteOnly]public NativeArray<int> Result;
        public int Mask;public float X,Y,Z,Reach;
        public int Count=>Points.Length;public CampaignPortalPoint16 At(int index)=>Points[index];
        public void Execute(){var data=this;Result[0]=CampaignPortalMath.Select(ref data,Mask,X,Y,Z,Reach);}
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Immutable cold world portal/accepted feet scalars; no Scene/Object/physics APIs.
// Array Formats:8readonly PortalPoint16 inputs+one int selected-lane result,132bytes TempJob payload.
// Dependencies/Pointers: Main-thread caller completes before reading and completes/disposes scratch in finally; no persistent/native or gameplay lifetime owner added.
