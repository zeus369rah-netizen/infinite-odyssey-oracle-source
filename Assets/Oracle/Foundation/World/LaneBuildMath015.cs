using System;
using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace OdysseyOracle.Foundation.World
{
    public enum LaneBuildReason015 : uint { Legal, Unavailable, Stale, OutsideBand, Road, Water, Lava, Uneven, Blocked, Reach, Capacity, Tokens, Ownership, Replay, Model }
    [StructLayout(LayoutKind.Sequential, Pack=4)]
    public struct LaneBuildTerrain8 { public uint Flags; public float SupportY; }
    [StructLayout(LayoutKind.Sequential, Pack=4)]
    public struct LaneBuildStamp32 { public float2 Center, Half, Axis; public uint Flags, Reserved; }
    [StructLayout(LayoutKind.Sequential, Pack=4)]
    public struct LaneBuildBody32 { public float3 Center; public float Radius, MinimumY, MaximumY; public uint Identity, Kind; }
    [StructLayout(LayoutKind.Sequential, Pack=4)]
    public struct LaneBuildPoint16 { public float3 Position; public uint Reason; }
    [StructLayout(LayoutKind.Sequential, Pack=4)]
    public struct LaneBuildFrame64
    {
        public uint Version, Kind;
        public ulong RunA, RunB;
        public int Epoch; public uint Nonce, Cell;
        public int Element, TowerId, TowerRevision, Lane;
        public uint Semantic, Result, Context;
    }
    [StructLayout(LayoutKind.Sequential,Pack=4)]
    public struct LaneBuildPending96
    {
        public LaneBuildFrame64 Command;
        public double Started,Next,Deadline;
        public uint Attempts,Active;
    }
    public static class LaneBuildDelivery015
    {
        public const double TimeoutSeconds=5;
        public const uint MaximumAttempts=4;
        static bool Clock(double now)=>!double.IsNaN(now)&&!double.IsInfinity(now)&&now>=0;
        public static bool Begin(ref LaneBuildPending96 state,in LaneBuildFrame64 command,double now)
        {
            if(state.Active!=0||!Clock(now)||!Clock(now+TimeoutSeconds)||now+TimeoutSeconds<=now||!LaneBuildMath015.FrameShape(command)||
                command.Kind==LaneBuildMath015.Close||command.Kind==LaneBuildMath015.Context||command.Kind==LaneBuildMath015.Receipt)return false;
            state=new LaneBuildPending96{Command=command,Started=now,Next=now,Deadline=now+TimeoutSeconds,Active=1};return true;
        }
        public static bool Expired(in LaneBuildPending96 state,double now)
            =>state.Active!=0&&(!Clock(now)||now<state.Started||now>=state.Deadline);
        public static bool TakeDue(ref LaneBuildPending96 state,double now,out LaneBuildFrame64 command)
        {
            command=default;if(state.Active==0||Expired(state,now)||now<state.Next||state.Attempts>=MaximumAttempts)return false;
            command=state.Command;state.Attempts++;
            // Delay from the actual attempt prevents a catch-up burst after a delayed frame.
            state.Next=state.Attempts==1?now+.75:state.Attempts==2?now+1:state.Attempts==3?now+1.5:state.Deadline;return true;
        }
        public static void Clear(ref LaneBuildPending96 state){state=default;}
        public static bool Same(in LaneBuildFrame64 a,in LaneBuildFrame64 b)
            =>a.Version==b.Version&&a.Kind==b.Kind&&a.RunA==b.RunA&&a.RunB==b.RunB&&a.Epoch==b.Epoch&&a.Nonce==b.Nonce&&a.Cell==b.Cell&&
                a.Element==b.Element&&a.TowerId==b.TowerId&&a.TowerRevision==b.TowerRevision&&a.Lane==b.Lane&&a.Semantic==b.Semantic&&a.Result==b.Result&&a.Context==b.Context;
        public static bool Matches(in LaneBuildPending96 state,in LaneBuildFrame64 reply)
        {
            if(state.Active==0||!LaneBuildMath015.FrameShape(reply))return false;var request=state.Command;
            if(reply.Version!=request.Version||reply.RunA!=request.RunA||reply.RunB!=request.RunB||reply.Epoch!=request.Epoch||reply.Nonce!=request.Nonce)return false;
            if(request.Kind==LaneBuildMath015.Open)
            {
                if(reply.Kind!=LaneBuildMath015.Context||reply.Context!=request.Nonce)return false;
                // Trusted correlated denial may report a recomputed host lane/semantic. It never establishes a lease.
                if(reply.Result!=0)return true;
                return reply.Lane==request.Lane&&reply.Semantic==request.Semantic&&reply.Cell==request.Cell&&reply.Element==request.Element&&
                    reply.TowerId==request.TowerId&&reply.TowerRevision==request.TowerRevision;
            }
            if(reply.Kind!=LaneBuildMath015.Receipt||reply.Context!=request.Context||reply.Cell!=request.Cell||reply.Element!=request.Element||
                reply.Lane!=request.Lane||reply.Semantic!=request.Semantic||reply.TowerRevision!=request.TowerRevision)return false;
            bool generated=reply.Result==0&&(request.Kind==LaneBuildMath015.Build||request.Kind==LaneBuildMath015.Replace);
            return generated?reply.TowerId>0&&reply.TowerId<=65535:reply.TowerId==request.TowerId;
        }
    }
    public static class LaneBuildMath015
    {
        public const int TerrainSide=256, TerrainCount=65536, RootSide=1024, HighlightCapacity=8192, StampCapacity=4096, BodyCapacity=54;
        public const uint Land=1, Water=2, Lava=4, Road=8, Reserved=16;
        public const uint Version=1, Open=1, Context=2, Build=3, Remove=4, Replace=5, Close=6, Receipt=7;
        public const float MacroMetres=1, RootStep=.25f, HalfRoad=2.945f, EdgeBand=2, Reach=25, SoleRadius=1.131371f;
        public static bool Finite(float3 p) => math.all(math.isfinite(p)) && math.all(math.abs(p)<=128);
        public static uint Morton(int x,int z,int bits)
        { uint key=0; for(int b=0;b<bits;b++){key|=((uint)x>>b&1u)<<(2*b);key|=((uint)z>>b&1u)<<(2*b+1);}return key; }
        public static int2 Decode(uint key,int bits)
        { int x=0,z=0;for(int b=0;b<bits;b++){x|=(int)(key>>(2*b)&1u)<<b;z|=(int)(key>>(2*b+1)&1u)<<b;}return new int2(x,z); }
        public static bool TryCell(float3 point,out uint key,out float3 canonical)
        {
            key=0;canonical=default;if(!Finite(point))return false;
            int2 p=(int2)math.round(point.xz*4)+512;
            if(math.any(p<0)||math.any(p>=RootSide))return false;
            key=Morton(p.x,p.y,10);canonical=new float3((p.x-512)*RootStep,0,(p.y-512)*RootStep);return true;
        }
        public static bool FromCell(uint key,out float3 p)
        { p=default;if(key>=1048576)return false;int2 q=Decode(key,10)-512;p=new float3(q.x*RootStep,0,q.y*RootStep);return true; }
        public static bool Canonical(float3 p,out uint key)
        { key=0;return TryCell(p,out key,out float3 q)&&math.all(q==p); }
        public static float2 Direction(int lane)
        {
            const float d=.7071067811865475f;
            switch(lane){case 0:return new float2(1,0);case 1:return new float2(d,d);case 2:return new float2(0,1);case 3:return new float2(-d,d);case 4:return new float2(-1,0);case 5:return new float2(-d,-d);case 6:return new float2(0,-1);case 7:return new float2(d,-d);default:return default;}
        }
        public static int NearestLane(float3 feet,int mask)
        {
            if(!Finite(feet)||mask<=0||(mask&~255)!=0)return -1;
            int chosen=-1;float score=float.NegativeInfinity;
            for(int lane=0;lane<8;lane++){if((mask&(1<<lane))==0)continue;float dot=math.dot(feet.xz,Direction(lane));if(dot>score){score=dot;chosen=lane;}}
            return chosen;
        }
        public static bool InBand(float3 p,int lane)
        {
            if(!Finite(p)||lane<0||lane>7)return false;
            float2 d=Direction(lane);float along=math.dot(p.xz,d),side=math.abs(math.dot(p.xz,new float2(-d.y,d.x)));
            return along>=7.8f&&along<=70&&side>HalfRoad&&side<=HalfRoad+EdgeBand;
        }
        // Closed circle/OBB contact is blocked; the outer root band is a separate rule.
        public static float RoadFacingYaw(float3 point,int lane)
        {float2 d=Direction(lane),toward=d*math.dot(point.xz,d)-point.xz;return math.degrees(math.atan2(toward.x,toward.y));}
        public static bool Touches(in LaneBuildStamp32 s,float2 p,float radius)
        {
            if(!math.all(math.isfinite(s.Center))||!math.all(math.isfinite(s.Half))||math.any(s.Half<0)||
                !math.all(math.isfinite(s.Axis))||math.abs(math.lengthsq(s.Axis)-1)>.001f||!math.isfinite(radius)||radius<0)return true;
            float2 delta=p-s.Center;float2 local=new float2(math.dot(delta,s.Axis),math.dot(delta,new float2(-s.Axis.y,s.Axis.x)));
            float2 outside=math.max(math.abs(local)-s.Half,0);float conservativeRadius=radius+.001f;return math.lengthsq(outside)<=conservativeRadius*conservativeRadius;
        }
        public static bool FrameShape(in LaneBuildFrame64 f)
        {
            if(f.Version!=Version||f.Kind<Open||f.Kind>Receipt||(f.RunA==0&&f.RunB==0)||f.Epoch<0||f.Nonce==0||f.Lane< -1||f.Lane>7||f.Result>(uint)LaneBuildReason015.Model)return false;
            bool towerIdentity=f.TowerId==0?f.TowerRevision==0:f.TowerId>0&&f.TowerId<=65535&&f.TowerRevision>0;
            if(f.Kind==Open)return f.Cell==0&&f.Element==0&&towerIdentity&&f.Result==0&&f.Context==0;
            if(f.Kind==Context)return f.Cell==0&&f.Element==0&&towerIdentity&&f.Context==f.Nonce;
            if(f.Kind==Close)return f.Cell==0&&f.Element==0&&f.TowerId==0&&f.TowerRevision==0&&f.Result==0&&f.Context!=0;
            if(f.Kind==Receipt)return f.Cell<1048576&&f.TowerId>=0&&f.TowerId<=65535&&f.Context!=0;
            if(f.Cell>=1048576||f.Context==0||f.Lane<0||f.Semantic==0||f.Result!=0||f.TowerRevision<0)return false;
            return f.Kind==Build?f.Element>=0&&f.Element<4&&f.TowerId==0&&f.TowerRevision==0:
                f.Kind==Remove?f.Element==0&&f.TowerId>0&&f.TowerId<=65535:f.Element>=0&&f.Element<4&&f.TowerId>0&&f.TowerId<=65535;
        }
        public static bool RunKey(string run,out ulong a,out ulong b)
        {
            a=b=0;if(run==null||run.Length!=32)return false;
            for(int i=0;i<32;i++){char c=run[i];uint v=c>='0'&&c<='9'?(uint)(c-'0'):c>='a'&&c<='f'?(uint)(c-'a'+10):16;if(v>15)return false;if(i<16)a=(a<<4)|v;else b=(b<<4)|v;}
            return a!=0||b!=0;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Unity metre/Y-up/XZ,1m terrain cells and .25m roots; bounded Morton16/20 with separate authenticated run/tower identities.
// Array Formats: Terrain8/Stamp32/Body32/Point16 and exact Frame64; fixed65536/4096/54/8192 capacities, no Object or allocation inside numeric methods.
// Dependencies/Pointers: World owns storage; host owns context/transactions. Closed forbidden contact, exact support and invalid inputs fail closed; native/browser/ABI acceptance remains root-owned.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Current authenticated run/epoch/context and monotonic real-time pending request ages; placement/world coordinates unchanged.
// Array Formats: Immutable Frame64, fixed Pending96 scalar state and one cached Frame64 per existing peer; no per-frame heap container or ABI change.
// Dependencies/Pointers: Bounded same-nonce delivery, exact host replay and response-controlled UI only. Root alone imports/native-tests; frozen original packet remains unchanged.
