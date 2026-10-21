using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
namespace OdysseyOracle
{
    [StructLayout(LayoutKind.Sequential,Pack=4)] public struct WorkshopBuildClock32
    {public double Started;public uint Lease;public int Id,Revision,Type;public uint Active;public float Height;}
    [StructLayout(LayoutKind.Sequential,Pack=4)] public struct WorkshopBuildPose32
    {public float4 Build,Detail;}
    [StructLayout(LayoutKind.Sequential,Pack=4)] public struct WorkshopBuildBounds32
    {public float3 Center,Extents;public float PivotY;public uint Present;}
    public static class WorkshopBuildMath015
    {
        public const int Capacity=4;public const double Duration=.75;
        public static bool Valid(double now,int id,int revision,int type,float height)=>math.isfinite(now)&&now>=0&&id>0&&revision>=0&&type>=0&&type<4&&math.isfinite(height)&&height>.01f&&height<=8;
        public static bool NextLease(uint current,out uint next){next=current;if(current==uint.MaxValue)return false;next=current+1;return true;}
        public static bool CanRebind(int previousId,int previousRevision,int previousTier,int id,int revision,int tier)=>id>0&&revision>=0&&tier>=1&&tier<=3&&(previousId==0||previousId==id&&revision>previousRevision&&tier>=previousTier);
        public static bool AnimateAccepted(int previousId,int previousTier,int tier,bool accepted)=>accepted&&(previousId==0||tier>previousTier);
        public static WorkshopBuildPose32 Evaluate(WorkshopBuildClock32 c,double now,bool reduced)
        {
            if(c.Active!=1||c.Lease==0||!Valid(c.Started,c.Id,c.Revision,c.Type,c.Height)||!math.isfinite(now)||now<c.Started)return new WorkshopBuildPose32{Build=new float4(1,1,0,1)};
            float p=(float)math.clamp((now-c.Started)/Duration,0,1);float smooth=p*p*(3-2*p);
            return new WorkshopBuildPose32{Build=new float4(p,reduced?1:math.lerp(.08f,1,smooth),p<1?1:0,reduced?1:0),Detail=new float4(c.Height,c.Type,c.Lease,0)};
        }
    }
    [BurstCompile] public struct WorkshopBuildPoseJob015:IJobParallelFor
    {
        [ReadOnly]public NativeArray<WorkshopBuildClock32> Clocks;public NativeArray<WorkshopBuildPose32> Poses;public double Now;public bool Reduced;
        public void Execute(int i){Poses[i]=WorkshopBuildMath015.Evaluate(Clocks[i],Now,Reduced);}
    }
    [BurstCompile] public struct WorkshopBuildBoundsJob015:IJobParallelFor
    {
        [ReadOnly]public NativeArray<WorkshopBuildBounds32> Bounds;public NativeArray<float2> Ranges;
        public void Execute(int tile)
        {
            float lo=float.PositiveInfinity,hi=float.NegativeInfinity;bool any=false;
            for(int g=0;g<6;g++){var b=Bounds[tile*6+g];if(b.Present==0)continue;if(!math.all(math.isfinite(b.Center))||!math.all(math.isfinite(b.Extents))||!math.isfinite(b.PivotY)||math.any(b.Extents<0)){Ranges[tile]=default;return;}float y=b.Center.y+b.PivotY;lo=math.min(lo,y-b.Extents.y);hi=math.max(hi,y+b.Extents.y);any=true;}
            Ranges[tile]=any&&hi>lo&&hi-lo<=8?new float2(lo,hi-lo):default;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Numeric saved model-local floor/height and real monotonic elapsed presentation time; no root/collision/gameplay state.
// Array Formats: Clock32/Pose32 persistent[4], cold Bounds32[72]→float2[12]. All loop math is Burst-isolated with explicit finite/lease bounds.
// Dependencies/Pointers: Caller owns and completes buffers; no UnityObject, allocations, authority, audio or assets in jobs. Duration0.75s is cosmetic only.
