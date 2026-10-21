using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace OdysseyOracle
{
    [System.Serializable,StructLayout(LayoutKind.Sequential)] public struct MenuShowcaseSegment32 {public float3 A;public float Begin;public float3 B;public float End;}
    [StructLayout(LayoutKind.Sequential)] public struct MenuShowcaseSeed48 {public float3 Position;public uint Kind;public quaternion Rotation;public float3 Scale;public uint Index;}
    [StructLayout(LayoutKind.Sequential)] public struct MenuShowcasePose48 {public float3 Position;public uint Visible;public quaternion Rotation;public float3 Scale;public uint Reserved;}
    [StructLayout(LayoutKind.Sequential)] public struct MenuShowcaseTriangle48 {public float3 A;public uint P0;public float3 B;public uint P1;public float3 C;public uint P2;}
    [StructLayout(LayoutKind.Sequential)] public struct MenuShowcaseInterval32 {public float4 Plane;public float Begin,End;public uint P0,P1;}

    public static class MenuShowcaseMath
    {
        public const int MaximumPoses=256,MaximumMonsters=12,MaximumBolts=16;
        public static int DemoBuildIndex(int build,int demo)=>(build+(demo==1?2:0))&3;
        public static float3 Sample(NativeArray<MenuShowcaseSegment32> segments,int2 range,float progress)
        {
            progress=math.saturate(progress);
            for(int n=0;n<range.y;n++)
            {
                var s=segments[range.x+n];
                if(progress<=s.End||n==range.y-1)return math.lerp(s.A,s.B,math.saturate((progress-s.Begin)/math.max(.000001f,s.End-s.Begin)));
            }
            return float3.zero;
        }
        public static quaternion Face(float3 direction)=>quaternion.LookRotationSafe(math.normalizesafe(direction,new float3(0,0,1)),new float3(0,1,0));
        public static float Cross(float2 a,float2 b)=>a.x*b.y-a.y*b.x;
    }
    [BurstCompile] public struct MenuShowcasePoseJob:IJob
    {
        [ReadOnly] public NativeArray<MenuShowcaseSeed48> Seeds;
        [ReadOnly] public NativeArray<MenuShowcaseSegment32> Segments;
        [ReadOnly] public NativeArray<int2> Ranges;
        [ReadOnly] public NativeArray<float3> Pads;
        [ReadOnly] public NativeArray<float3> Muzzles;
        [ReadOnly] public NativeArray<float3> HeadPivots;
        public NativeArray<MenuShowcasePose48> Poses;
        public float Time;
        public int MonsterCount;
        public void Execute()
        {
            float clock=math.fmod(math.max(0,Time),32f);int build=(int)(clock/8f);float local=clock-build*8f;
            for(int n=0;n<Seeds.Length;n++)
            {
                var s=Seeds[n];var p=new MenuShowcasePose48{Position=s.Position,Rotation=s.Rotation,Scale=s.Scale,Visible=1};
                int id=(int)(s.Index&255),part=(int)(s.Index>>8);float wave=math.sin(Time*7f+id*1.7f+part*math.PI);
                if(s.Kind==1)
                {
                    int actorBuild=MenuShowcaseMath.DemoBuildIndex(build,id);int2 route=Ranges[8+actorBuild];float u=math.saturate(local/5f);
                    p.Position=MenuShowcaseMath.Sample(Segments,route,u);
                    float3 ahead=MenuShowcaseMath.Sample(Segments,route,math.min(1,u+.01f));
                    float3 forward=local<5f?ahead-p.Position:Pads[actorBuild]-p.Position;forward.y=0;p.Rotation=MenuShowcaseMath.Face(forward);
                }
                else if(s.Kind==2)
                {
                    float angle=local<5f?wave*9f:part==1?-55f*math.sin(math.saturate((local-5f)/3f)*math.PI):0f;
                    p.Rotation=math.mul(s.Rotation,quaternion.RotateX(math.radians(angle)));
                }
                else if(s.Kind==3)
                {
                    int2 route=Ranges[id&7];float u=math.frac(Time/16f+id*.081f);
                    p.Position=MenuShowcaseMath.Sample(Segments,route,u);
                    float3 ahead=MenuShowcaseMath.Sample(Segments,route,math.min(1,u+.01f)),forward=ahead-p.Position;forward.y=0;p.Rotation=MenuShowcaseMath.Face(forward);
                    p.Visible=id<MonsterCount?1u:0u;
                }
                else if(s.Kind==4)p.Rotation=math.mul(s.Rotation,quaternion.RotateX(wave*.12f));
                else if(s.Kind==5||s.Kind==6)p.Position.y+=math.max(0,wave)*.06f;
                else if(s.Kind==7)p.Visible=clock>=5.5f+(id&1)*8f?1u:0u;
                else if(s.Kind==8)
                {
                    float3 goal=MenuShowcaseMath.Sample(Segments,Ranges[id&7],math.frac(Time/16f+id*.081f));
                    float3 forward=goal-Pads[id];forward.y=0;p.Rotation=MenuShowcaseMath.Face(forward);
                }
                else if(s.Kind==9){float shotPhase=math.frac((Time+id*.23f)/1.5f);p.Position.z-=shotPhase<.2f?math.sin(shotPhase/.2f*math.PI)*.06f:0;}
                else if(s.Kind==10){float angle=Time*.6f+id*.7f;p.Position.y+=.025f*math.sin(Time*2+id);p.Rotation=quaternion.RotateY(angle);p.Reserved=math.asuint(math.max(0,math.sin(angle)));}
                else if(s.Kind==11)p.Rotation=quaternion.RotateY(Time*1.2f);
                else if(s.Kind==12)
                {
                    int source=id&3,pools=MonsterCount>6?4:2;float cycle=(Time+source*.23f)/1.5f,shot=math.frac(cycle);
                    float3 target=MenuShowcaseMath.Sample(Segments,Ranges[source],math.frac(Time/16f+source*.081f));target.y+=1.2f;
                    float3 aim=target-Pads[source];aim.y=0;
                    float3 from=Pads[source]+HeadPivots[source]+math.rotate(MenuShowcaseMath.Face(aim),Muzzles[source]-HeadPivots[source]),direction=target-from;
                    p.Position=math.lerp(from,target,math.saturate(shot/.25f));p.Rotation=MenuShowcaseMath.Face(direction);
                    p.Visible=(id>>2)==((int)math.floor(cycle)%pools)&&shot<.25f&&clock>=5.5f+(source&1)*8f&&source<MonsterCount?1u:0u;
                }
                Poses[n]=p;
            }
        }
    }

    // Editor/cold capture: clip routes to real upward-facing source triangles.
    // Each resulting segment stays on one source floor plane, including exact steps.
    [BurstCompile] public struct MenuShowcaseRouteBakeJob:IJob
    {
        [ReadOnly] public NativeArray<MenuShowcaseTriangle48> Triangles;
        [ReadOnly] public NativeArray<float3> Endpoints;
        public NativeArray<MenuShowcaseSegment32> Segments;
        public NativeArray<int> Counts;
        public NativeArray<byte> Failed;
        public NativeArray<MenuShowcaseInterval32> Intervals;
        public NativeArray<float> Breaks;
        public void Execute()
        {
            for(int route=0;route<12;route++)
            {
                float3 a=Endpoints[route*2],b=Endpoints[route*2+1];int count=0,breakCount=2;Breaks[0]=0;Breaks[1]=1;
                for(int t=0;t<Triangles.Length;t++)
                {
                    var tri=Triangles[t];float3 normal=math.cross(tri.B-tri.A,tri.C-tri.A);
                    if(math.lengthsq(normal)<.0000000001f||normal.y<.65f*math.length(normal)||math.cmax(new float3(tri.A.y,tri.B.y,tri.C.y))>3f)continue;
                    float lo=0,hi=1;float2 start=a.xz,delta=b.xz-a.xz;float sign=math.sign(MenuShowcaseMath.Cross(tri.B.xz-tri.A.xz,tri.C.xz-tri.A.xz));
                    if(!Clip(tri.A.xz,tri.B.xz,start,delta,sign,ref lo,ref hi)||!Clip(tri.B.xz,tri.C.xz,start,delta,sign,ref lo,ref hi)||!Clip(tri.C.xz,tri.A.xz,start,delta,sign,ref lo,ref hi)||hi-lo<.000001f)continue;
                    if(count==Intervals.Length||breakCount+2>Breaks.Length){Failed[route]=1;break;}
                    Intervals[count++]=new MenuShowcaseInterval32{Plane=new float4(normal,-math.dot(normal,tri.A)),Begin=lo,End=hi};Breaks[breakCount++]=lo;Breaks[breakCount++]=hi;
                }
                if(Failed[route]!=0)continue;
                // Fixed cold scratch, insertion sort; no allocator or Object API in the job.
                for(int i=1;i<breakCount;i++){float v=Breaks[i];int j=i-1;while(j>=0&&Breaks[j]>v){Breaks[j+1]=Breaks[j];j--;}Breaks[j+1]=v;}
                int emitted=0;float last=0;float3 previousPlane=0;
                for(int i=1;i<breakCount;i++)
                {
                    float end=math.saturate(Breaks[i]);if(end-last<.000001f)continue;
                    float mid=(last+end)*.5f,best=float.NegativeInfinity;int selected=-1;
                    for(int k=0;k<count;k++)
                    {var interval=Intervals[k];if(mid<interval.Begin-.000001f||mid>interval.End+.000001f)continue;float3 point=math.lerp(a,b,mid);float y=Y(interval.Plane,point);if(y>=-2f&&y>best){best=y;selected=k;}}
                    if(selected<0){Failed[route]=1;break;}
                    float3 from=math.lerp(a,b,last),to=math.lerp(a,b,end);float4 plane=Intervals[selected].Plane;from.y=Y(plane,from);to.y=Y(plane,to);
                    float3 coefficients=new float3(plane.x,plane.z,plane.w)/plane.y;
                    // A tiled floor may create many triangle boundaries on the
                    // same plane. Preserve that plane and merge only contiguous
                    // coplanar spans before enforcing the route segment budget.
                    if(emitted>0&&math.cmax(math.abs(coefficients-previousPlane))<=.00001f&&math.lengthsq(Segments[route*128+emitted-1].B-from)<=.00000001f)
                    {var previous=Segments[route*128+emitted-1];previous.B=to;previous.End=end;Segments[route*128+emitted-1]=previous;}
                    else
                    {if(emitted==128){Failed[route]=1;break;}Segments[route*128+emitted++]=new MenuShowcaseSegment32{A=from,B=to,Begin=last,End=end};previousPlane=coefficients;}
                    last=end;
                }
                if(last<.999999f||emitted==0)Failed[route]=1;Counts[route]=emitted;
            }
        }
        static float Y(float4 plane,float3 point)=>-(plane.x*point.x+plane.z*point.z+plane.w)/plane.y;
        static bool Clip(float2 a,float2 b,float2 start,float2 delta,float sign,ref float lo,ref float hi)
        {
            float2 edge=b-a;float p=sign*MenuShowcaseMath.Cross(edge,start-a),d=sign*MenuShowcaseMath.Cross(edge,delta);
            if(math.abs(d)<.000001f)return p>=-.000001f;
            float t=-p/d;if(d>0)lo=math.max(lo,t);else hi=math.min(hi,t);return lo<=hi;
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Parent-local Y-up metres/radians; routes are clipped to actual captured upward floor planes, not interpolated invented HP/path state.
// Array Formats: Sequential32B segments/intervals,48B seeds/poses/triangles; fixed256 poses/12 routes/128 segments per route. Jobs contain only native/value math.
// Dependencies/Pointers: Private runtime owner completes its fence before adapter access/disposal; Editor capture owns/disposes TempJob scratch. No Unity Object, room, save, economy or real combat pointers enter jobs.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: actual full catalog-local map bounds to private RT; real menu-left safe viewport, no scenic crop.
// Array Formats: existing bounded poses plus48B camera frame[1], two cold original avatar leases; no hot objects/strings.
// Dependencies/Pointers: actual MenuCoop009/rendering-layer7 and saved gameplay settings preserved; root native/fresh bake/browser acceptance pending.
