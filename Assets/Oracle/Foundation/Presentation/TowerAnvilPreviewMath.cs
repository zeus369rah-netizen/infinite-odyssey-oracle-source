using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
namespace OdysseyOracle.Foundation.Presentation
{
    [StructLayout(LayoutKind.Sequential)]
    public struct AnvilPreviewBounds40
    {
        public float3 Center,Extents,Pivot;public int Present;
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct AnvilPreviewFrame32
    {
        public float3 Center;public float Radius,Scale;public int Status;public float2 Reserved;
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct AnvilPreviewPose32
    {
        public float3 Position;public float Scale;public quaternion Rotation;
    }
    public static class TowerAnvilPreviewMath
    {
        public const int Types=4,Tiers=3,Count=12,Groups=6,Width=768,Height=1024;
        public const float TileSize=4f,SafeRadius=1.48f,Period=20f,InitialDegrees=150f;
        public static float3 TileCenter(int index)=>new float3((index%3-1)*TileSize,(1.5f-index/3)*TileSize,0);
        public static float4 TileUv(int index)=>new float4((index%3)/3f,(3-index/3)/4f,1f/3f,.25f);
        public static float Advance(float time,float delta,bool motion)
        {
            if(!math.isfinite(time)||time<0)time=0;
            if(motion&&math.isfinite(delta)&&delta>0)time+=math.min(delta,.1f);
            return time%Period;
        }
        public static AnvilPreviewPose32 Pose(int index,AnvilPreviewFrame32 frame,float time)
        {
            float finite=math.isfinite(time)?math.max(0,time):0;
            float angle=math.radians(InitialDegrees+math.fmod(finite,Period)*(360f/Period));
            return new AnvilPreviewPose32{Position=TileCenter(index),Scale=frame.Scale,Rotation=math.mul(quaternion.RotateX(math.radians(-20f)),quaternion.RotateY(angle))};
        }
        public static AnvilPreviewFrame32 CompleteFrame(float3 lo,float3 hi,int present)
        {
            float radius=math.length((hi-lo)*.5f);bool valid=present>0&&math.all(math.isfinite(lo))&&math.all(math.isfinite(hi))&&math.all(hi>=lo)&&math.isfinite(radius)&&radius>.0001f;
            return valid?new AnvilPreviewFrame32{Center=(lo+hi)*.5f,Radius=radius,Scale=SafeRadius/radius,Status=1}:new AnvilPreviewFrame32{Status=-2};
        }
        public static bool Unlocked(int tier,int accepted,bool ready)=>ready&&accepted>=1&&accepted<=3&&tier>=1&&tier<=accepted;
    }
    [BurstCompile]
    public struct TowerAnvilFrameJob:IJobParallelFor
    {
        [ReadOnly] public NativeArray<AnvilPreviewBounds40> Bounds;
        public NativeArray<AnvilPreviewFrame32> Frames;
        public void Execute(int index)
        {
            float3 lo=new float3(float.PositiveInfinity),hi=new float3(float.NegativeInfinity);int present=0;
            for(int group=0;group<6;group++)
            {
                var input=Bounds[index*6+group];if(input.Present==0)continue;
                if(!math.all(math.isfinite(input.Center))||!math.all(math.isfinite(input.Extents))||!math.all(math.isfinite(input.Pivot))||math.any(input.Extents<0)){Frames[index]=new AnvilPreviewFrame32{Status=-1};return;}
                float3 center=input.Center+input.Pivot;lo=math.min(lo,center-input.Extents);hi=math.max(hi,center+input.Extents);present++;
            }
            Frames[index]=TowerAnvilPreviewMath.CompleteFrame(lo,hi,present);
        }
    }
    [BurstCompile]
    public struct TowerAnvilPoseJob:IJobParallelFor
    {
        [ReadOnly] public NativeArray<AnvilPreviewFrame32> Frames;
        public NativeArray<AnvilPreviewPose32> Poses;
        public float Time;
        public void Execute(int index){Poses[index]=TowerAnvilPreviewMath.Pose(index,Frames[index],Time);}
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: actual saved mesh-local bounds plus HeadPivot -> centered unit sphere fit in3x4private4m atlas tiles;20degree presentation tilt and smooth18degree/second common turntable.
// Array Formats:72blittable40Bbound records,12frame32B and12pose32B; no UnityObject/Physics/delegate/string/heap inside either Burst job. Caller owns and fences every buffer.
// Dependencies/Pointers: actual saved immutable models and accepted Forge tiers remain external authorities. Stateless UV/unlock policies and clock never purchase/unlock/mutate gameplay; reduced motion holds private time exactly.
