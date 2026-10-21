// PSEUDOCODE: propose finite capped XZ travel in one flat pass; insert current
// body points into a Morton grid; admit only solid-floor and swept-neighbor-safe
// proposals, then consume exactly that distance or cancel the blocked command.
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
namespace OdysseyOracle
{
    public struct EnemyFeedbackPose022
    {
        public float3 Position;
        public float Radius,CrowdRadius;
        public uint Encounter,Revision,Generation;
        public int Id;
        public byte Active,Flying,Movable;
    }
    public struct EnemyFeedbackState022
    {
        public float3 Direction;
        public float Remaining,Speed,Age;
        public uint Encounter,Revision,Generation;
        public int Id;
    }
    public struct EnemyFeedbackStep022
    {
        public float3 Move;
        public float Distance;
        public byte Wanted,Clear,Accepted;
    }
    public struct EnemyFeedbackFloor022
    {
        public float3 Center,AxisX,AxisY,AxisZ;
        public float HalfX,HalfZ,Top;
        public byte Valid;
    }
    public static class EnemyFeedbackMath022
    {
        public const int Actors=48,Poses=56,Floors=16;
        public const float Distance=.12f,MaximumDistance=.36f,MaximumStep=.05f,Duration=.12f,MaximumDelta=.15f,MaximumAge=1.25f;
        public static bool Finite(float v)=>math.isfinite(v);
        public static bool Point(float3 p)=>math.all(math.isfinite(p))&&math.all(math.abs(p)<=1024f);
        public static bool Radius(float v)=>Finite(v)&&v>0&&v<=4;
        public static uint Cell(float3 p)=>Morton((int2)math.floor(p.xz*.25f)+256);
        public static uint Morton(int2 c)=>Spread((uint)c.x)|(Spread((uint)c.y)<<1);
        static uint Spread(uint v)
        {v&=1023;v=(v|(v<<8))&0x00ff00ff;v=(v|(v<<4))&0x0f0f0f0f;v=(v|(v<<2))&0x33333333;return(v|(v<<1))&0x55555555;}
        public static bool Same(in EnemyFeedbackPose022 p,in EnemyFeedbackState022 s)=>p.Active==1&&p.Movable==1&&p.Id>0&&p.Id==s.Id&&
            p.Encounter!=0&&p.Revision!=0&&p.Generation!=0&&p.Encounter==s.Encounter&&p.Revision==s.Revision&&p.Generation==s.Generation;
        public static bool Request(in EnemyFeedbackPose022 p,float3 direction,int ingredients,out EnemyFeedbackState022 s)
        {
            s=default;direction.y=0;
            if(p.Active!=1||p.Movable!=1||p.Id<=0||p.Encounter==0||p.Revision==0||p.Generation==0||!Point(p.Position)||!Radius(p.Radius)||
                !math.all(math.isfinite(direction))||ingredients<0||ingredients>15)return false;
            float length=math.length(direction);if(!Finite(length)||length<.0001f)return false;
            float amount=(ingredients&8)!=0?MaximumDistance:Distance;
            s=new EnemyFeedbackState022{Direction=direction/length,Remaining=amount,Speed=amount/Duration,
                Encounter=p.Encounter,Revision=p.Revision,Generation=p.Generation,Id=p.Id};return true;
        }
        public static EnemyFeedbackStep022 Propose(in EnemyFeedbackPose022 p,ref EnemyFeedbackState022 s,float delta)
        {
            if(!Same(p,s)||!Point(p.Position)||!Radius(p.Radius)||!Point(s.Direction)||!Finite(s.Remaining)||s.Remaining<=0||s.Remaining>MaximumDistance||
                !Finite(s.Speed)||s.Speed<=0||s.Speed>3.001f||!Finite(s.Age)||s.Age<0||!Finite(delta)||delta<0||delta>MaximumDelta)
            {s=default;return default;}
            if(delta==0)return default;
            s.Age+=delta;if(s.Age>MaximumAge){s=default;return default;}
            float distance=math.min(s.Remaining,math.min(s.Speed*delta,MaximumStep));
            float3 move=s.Direction*distance;
            if(!Point(p.Position+move)||math.abs(move.y)>.000001f||math.abs(math.lengthsq(s.Direction)-1)>.0001f){s=default;return default;}
            return new EnemyFeedbackStep022{Move=move,Distance=distance,Wanted=1};
        }
        public static bool FloorContains(in EnemyFeedbackPose022 p,in EnemyFeedbackStep022 step,in EnemyFeedbackFloor022 f)
        {
            if(f.Valid!=1||!Point(f.Center)||!Point(f.AxisX)||!Point(f.AxisY)||!Point(f.AxisZ)||!Finite(f.Top)||!Finite(f.HalfX)||!Finite(f.HalfZ)||
                f.HalfX<=0||f.HalfZ<=0||math.abs(f.Top-p.Position.y)>.04f)return false;
            float lx=math.length(f.AxisX),ly=math.length(f.AxisY),lz=math.length(f.AxisZ);
            if(lx<.0001f||ly<.0001f||lz<.0001f)return false;
            float3 x=f.AxisX/lx,y=f.AxisY/ly,z=f.AxisZ/lz;
            if(math.abs(math.dot(x,z))>.0001f||math.abs(x.y)>.0001f||math.abs(z.y)>.0001f||math.lengthsq(y-new float3(0,1,0))>.00000001f)return false;
            // Disk Minkowski erosion of a convex oriented rectangle. Both ends
            // inside prove every point of the swept footprint lies on this solid.
            float3 a=p.Position-f.Center,b=a+step.Move;float pad=p.Radius+.015f;
            return math.max(math.abs(math.dot(a,x)),math.abs(math.dot(b,x)))+pad<=f.HalfX*lx&&
                math.max(math.abs(math.dot(a,z)),math.abs(math.dot(b,z)))+pad<=f.HalfZ*lz;
        }
        public static bool NeighborClear(float3 origin,float3 move,float radius,float3 other,float otherRadius)
        {
            float2 relative=origin.xz-other.xz,travel=move.xz;float length=math.lengthsq(travel);
            float projection=math.dot(relative,travel);
            // Nonnegative radial derivative makes the initial distance the minimum.
            // Keep .05m for any concurrent neighbor step, including tangent travel.
            if(projection>=0)
            {
                float startGap=radius+otherRadius+.05f;
                return math.lengthsq(relative)>=startGap*startGap;
            }
            float t=length>0?math.clamp(-projection/length,0,1):0;
            // Each simultaneously admitted move is <=.05m: reserve another .05
            // for that neighbor's move and .04 for the ordinary separation skin.
            float gap=radius+otherRadius+.09f;
            return math.lengthsq(relative+travel*t)>=gap*gap;
        }
    }
    [BurstCompile]
    public struct EnemyFeedbackProposeJob022:IJob
    {
        [ReadOnly]public NativeArray<EnemyFeedbackPose022> Poses;
        public NativeArray<EnemyFeedbackState022> States;
        public NativeArray<EnemyFeedbackStep022> Steps;
        public NativeParallelMultiHashMap<uint,int> Cells;
        public float Delta;
        public void Execute()
        {
            Cells.Clear();bool invalid=false;
            for(int i=0;i<EnemyFeedbackMath022.Poses;i++)
            {var p=Poses[i];if(p.Active!=1)continue;if(!EnemyFeedbackMath022.Point(p.Position)||!EnemyFeedbackMath022.Radius(p.Radius)||!EnemyFeedbackMath022.Radius(p.CrowdRadius)){invalid=true;continue;}Cells.Add(EnemyFeedbackMath022.Cell(p.Position),i);}
            for(int i=0;i<EnemyFeedbackMath022.Actors;i++)
            {var s=States[i];if(invalid){States[i]=default;Steps[i]=default;continue;}Steps[i]=EnemyFeedbackMath022.Propose(Poses[i],ref s,Delta);States[i]=s;}
        }
    }
    [BurstCompile]
    public struct EnemyFeedbackAdmitJob022:IJob
    {
        [ReadOnly]public NativeArray<EnemyFeedbackPose022> Poses;
        [ReadOnly]public NativeArray<EnemyFeedbackFloor022> Supports;
        [ReadOnly]public NativeParallelMultiHashMap<uint,int> Cells;
        public NativeArray<EnemyFeedbackState022> States;
        public NativeArray<EnemyFeedbackStep022> Steps;
        public void Execute()
        {
            for(int i=0;i<EnemyFeedbackMath022.Actors;i++)
            {
                var step=Steps[i];if(step.Wanted!=1)continue;
                var p=Poses[i];var s=States[i];bool admitted=step.Clear==1&&EnemyFeedbackMath022.Same(p,s);
                if(admitted&&p.Flying==0)
                {bool floor=false;for(int f=0;f<EnemyFeedbackMath022.Floors;f++)floor|=EnemyFeedbackMath022.FloorContains(p,step,Supports[i*EnemyFeedbackMath022.Floors+f]);admitted=floor;}
                int2 root=(int2)math.floor(p.Position.xz*.25f)+256;
                for(int z=-3;admitted&&z<=3;z++)for(int x=-3;admitted&&x<=3;x++)
                {
                    int2 cell=root+new int2(x,z);if(math.any(cell<0)||math.any(cell>1023)){admitted=false;break;}
                    if(!Cells.TryGetFirstValue(EnemyFeedbackMath022.Morton(cell),out int other,out var it))continue;
                    do{if(other==i)continue;var q=Poses[other];if(!EnemyFeedbackMath022.NeighborClear(p.Position,step.Move,math.max(p.Radius,p.CrowdRadius),q.Position,math.max(q.Radius,q.CrowdRadius))){admitted=false;break;}}while(Cells.TryGetNextValue(out other,ref it));
                }
                if(admitted){step.Accepted=1;s.Remaining=math.max(0,s.Remaining-step.Distance);}
                else{s=default;step.Move=0;step.Distance=0;}
                States[i]=s;Steps[i]=step;
            }
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Current world metres/Y-up, horizontal-only fixed-floor movement.
// Array Formats:48 states/steps,56 poses,768 floor values and56-entry Morton map; .05m step/.36m replacement cap.
// Dependencies/Pointers: Only numeric Native containers enter jobs. Main-thread owner copies exact actor/Physics facts and rechecks generations before applying; no damage/Object/Physics calls occur here.
