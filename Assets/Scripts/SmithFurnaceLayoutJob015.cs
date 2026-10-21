using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace OdysseyOracle
{
    [BurstCompile]
    public struct SmithFurnaceLayoutJob015:IJob,ISmithFurnaceLayout015
    {
        [ReadOnly]public NativeArray<float4> Rooms;
        [ReadOnly]public NativeArray<SmithFurnaceBounds32> Props;
        [ReadOnly]public NativeArray<float4> PropFrames;
        [ReadOnly]public NativeArray<float> Piers;
        public NativeArray<SmithFurnacePose32> Pose;
        public NativeArray<int> Result;
        public float X,Y,Z,Yaw,HalfWidth,Ceiling;public int UnitScale;
        public int RoomCount=>Rooms.Length;public int PropCount=>Props.Length;public int PierCount=>Piers.Length;
        public SmithFurnaceRect16 Room(int i){var v=Rooms[i];return new SmithFurnaceRect16{MinX=v.x,MinZ=v.y,MaxX=v.z,MaxZ=v.w};}
        public SmithFurnaceBounds32 Prop(int i){var frame=PropFrames[i];return SmithFurnaceLayout015.Project(Props[i],frame.x,frame.y,frame.z,frame.w);}public float Pier(int i)=>Piers[i];
        public void Execute(){if(PropFrames.Length!=Props.Length){Result[0]=2;Pose[0]=default;return;}var storage=this;Result[0]=SmithFurnaceLayout015.Evaluate(ref storage,X,Y,Z,Yaw,UnitScale!=0,HalfWidth,Ceiling,out var p);Pose[0]=p;}
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Immutable actual refuge/Smith layout in stationary world metres; pure numeric placement only.
// Array Formats: Read-only bounded native rooms/local props/scalar prop frames/pier arrays -> one32Bpose and one integer failure code; all prop rotations occur in this job.
// Dependencies/Pointers: No Object API in the Burst job; cold adapter completes fence before floor certification/publication and disposes all snapshots.
