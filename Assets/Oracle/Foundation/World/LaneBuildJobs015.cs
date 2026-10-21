using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace OdysseyOracle.Foundation.World
{
    [BurstCompile]
    public struct LaneBuildTerrainQuery015 : IJob
    {
        [ReadOnly] public NativeArray<LaneBuildTerrain8> Terrain;
        [ReadOnly] public NativeArray<LaneBuildStamp32> Stamps;
        [ReadOnly] public NativeArray<LaneBuildBody32> Bodies;
        public NativeArray<LaneBuildPoint16> Output;
        public float3 Point,Feet;public float EnvelopeRadius,EnvelopeTop;
        public int Lane,StampCount,BodyCount;public uint IgnoreTower;
        public void Execute(){Output[0]=new LaneBuildPoint16{Position=Point,Reason=(uint)Evaluate(Point)};}
        public LaneBuildReason015 Evaluate(float3 p)
        {
            if(!LaneBuildMath015.Canonical(p,out _)||!LaneBuildMath015.Finite(Feet)||Terrain.Length!=LaneBuildMath015.TerrainCount||
                !math.isfinite(EnvelopeRadius)||EnvelopeRadius<LaneBuildMath015.SoleRadius||EnvelopeRadius>4||
                !math.isfinite(EnvelopeTop)||EnvelopeTop<=0||EnvelopeTop>12||StampCount<0||StampCount>Stamps.Length||BodyCount<0||BodyCount>Bodies.Length)return LaneBuildReason015.Uneven;
            if(!LaneBuildMath015.InBand(p,Lane))
            {float2 direction=LaneBuildMath015.Direction(Lane);float along=math.dot(p.xz,direction),side=math.abs(math.dot(p.xz,new float2(-direction.y,direction.x)));
                return along>=7.8f&&along<=70&&side<=LaneBuildMath015.HalfRoad?LaneBuildReason015.Road:LaneBuildReason015.OutsideBand;}
            if(math.lengthsq(p-Feet)>LaneBuildMath015.Reach*LaneBuildMath015.Reach)return LaneBuildReason015.Reach;
            float r=EnvelopeRadius;
            int2 low=(int2)math.floor(p.xz-r+.5f)+128,high=(int2)math.floor(p.xz+r+.5f)+128;
            if(math.any(low<0)||math.any(high>=256))return LaneBuildReason015.Uneven;
            for(int z=low.y;z<=high.y;z++)for(int x=low.x;x<=high.x;x++)
            {
                var cell=Terrain[(int)LaneBuildMath015.Morton(x,z,8)];
                if((cell.Flags&LaneBuildMath015.Water)!=0)return LaneBuildReason015.Water;
                if((cell.Flags&LaneBuildMath015.Lava)!=0)return LaneBuildReason015.Lava;
                if(cell.Flags!=LaneBuildMath015.Land||cell.SupportY!=p.y)return LaneBuildReason015.Uneven;
            }
            for(int i=0;i<StampCount;i++)
            {
                var stamp=Stamps[i];if(!LaneBuildMath015.Touches(stamp,p.xz,EnvelopeRadius))continue;
                return (stamp.Flags&LaneBuildMath015.Lava)!=0?LaneBuildReason015.Lava:
                    (stamp.Flags&LaneBuildMath015.Water)!=0?LaneBuildReason015.Water:
                    (stamp.Flags&LaneBuildMath015.Road)!=0?LaneBuildReason015.Road:LaneBuildReason015.Blocked;
            }
            for(int i=0;i<BodyCount;i++)
            {
                var body=Bodies[i];if((body.Kind&1)!=0&&body.Identity==IgnoreTower)continue;
                if(!LaneBuildMath015.Finite(body.Center)||!math.isfinite(body.Radius)||body.Radius<0||body.Radius>8||!math.isfinite(body.MinimumY)||!math.isfinite(body.MaximumY)||body.MaximumY<=body.MinimumY||body.Kind>3)return LaneBuildReason015.Blocked;
                if(body.MaximumY<=p.y||body.MinimumY>=p.y+EnvelopeTop)continue;
                float full=body.Radius+EnvelopeRadius+.02f;if(math.lengthsq(body.Center.xz-p.xz)<=full*full)return LaneBuildReason015.Blocked;
            }
            return LaneBuildReason015.Legal;
        }
    }
    [BurstCompile]
    public struct LaneBuildEnumerate015 : IJob
    {
        public LaneBuildTerrainQuery015 Query;
        public NativeArray<LaneBuildPoint16> Points;
        public NativeArray<int> Count;
        public void Execute()
        {
            int2 origin=(int2)math.floor(Query.Feet.xz*4)-128;int count=0;
            // Fixed256-square Morton traversal covers64m around the25m reachable disk.
            for(uint key=0;key<65536;key++)
            {
                int2 q=LaneBuildMath015.Decode(key,8)+origin;float3 p=new float3(q.x*.25f,0,q.y*.25f);
                if(!LaneBuildMath015.InBand(p,Query.Lane)||math.lengthsq(p-Query.Feet)>625)continue;
                if(Query.Evaluate(p)!=LaneBuildReason015.Legal)continue;
                if(count>=Points.Length){Count[0]=-1;return;}Points[count++]=new LaneBuildPoint16{Position=p};
            }
            Count[0]=count;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Actual world XZ/Y-up, quarter-metre root candidates and complete conservative sole/animation envelopes on exact Y=0 support.
// Array Formats: Borrowed fixed Terrain8/Stamp32/Body32 plus Point16[8192]/one count; fixed Morton traversal, no heap/Object APIs.
// Dependencies/Pointers: World captures chosen semantics and caller supplies current actor snapshots; all read/write jobs complete before mutation/publication/disposal. Structural solid query is a separate shared-World lease and host revalidation.
