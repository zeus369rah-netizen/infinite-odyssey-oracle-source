using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using OdysseyOracle.Foundation.Pools015;

namespace OdysseyOracle
{
    [StructLayout(LayoutKind.Sequential,Pack=4)]
    public struct XpSolid32
    { public float3 Minimum;public uint Active;public float3 Maximum;public uint Reserved; }
    [StructLayout(LayoutKind.Sequential,Pack=4)]
    public struct XpSolidInput96
    {public float4x4 Matrix;public float3 Center;public uint Active;public float3 Extents;public uint FullSize;}
    public static class XpSolidLimits015
    { public const int Colliders=4096,Towers=8,Solids=4104,Cells=131072,CellsPerSolid=16384,Visits=2048; }
    [BurstCompile]
    public struct ProjectXpSolids015:IJobParallelFor
    {
        [ReadOnly]public NativeArray<XpSolidInput96> Input;
        [WriteOnly]public NativeArray<XpSolid32> Output;
        public void Execute(int index)
        {
            var value=Input[index];if(value.Active==0){Output[index]=default;return;}
            var m=value.Matrix;float3 ext=value.Extents*(value.FullSize==1?.5f:1f);
            if(value.Active!=1||value.FullSize>1||!math.all(math.isfinite(value.Center))||!math.all(math.isfinite(ext))||math.any(ext<=0)||
                !math.all(math.isfinite(m.c0))||!math.all(math.isfinite(m.c1))||!math.all(math.isfinite(m.c2))||!math.all(math.isfinite(m.c3))||
                m.c0.w!=0||m.c1.w!=0||m.c2.w!=0||m.c3.w!=1)
            {Output[index]=new XpSolid32{Active=2};return;}
            float3 center=math.transform(m,value.Center);
            float3 half=math.abs(m.c0.xyz)*ext.x+math.abs(m.c1.xyz)*ext.y+math.abs(m.c2.xyz)*ext.z;
            Output[index]=new XpSolid32{Minimum=center-half-.001f,Maximum=center+half+.001f,Active=1};
        }
    }
    [BurstCompile]
    public struct CompareXpSolids015:IJob
    {
        [ReadOnly]public NativeArray<XpSolid32> Current;
        public NativeArray<XpSolid32> Previous;
        public NativeArray<int> Status;
        public int CoverageKnown;
        public void Execute()
        {
            int changed=0,valid=CoverageKnown;
            for(int i=0;i<Current.Length;i++)
            {
                XpSolid32 a=Current[i],b=Previous[i];
                if(a.Active!=b.Active||math.any(a.Minimum!=b.Minimum)||math.any(a.Maximum!=b.Maximum))changed=1;
                if(a.Active!=0&&(a.Active!=1||a.Reserved!=0||!math.all(math.isfinite(a.Minimum))||!math.all(math.isfinite(a.Maximum))||
                    math.any(a.Maximum<=a.Minimum)||math.any(math.abs(a.Minimum)>8192)||math.any(math.abs(a.Maximum)>8192)))valid=0;
                Previous[i]=a;
            }
            Status[0]=changed;Status[1]=valid;
        }
    }
    [BurstCompile]
    public struct BuildXpSolidCells015:IJob
    {
        [ReadOnly]public NativeArray<XpSolid32> Solids;
        public NativeParallelMultiHashMap<ulong,int> Cells;
        public NativeArray<int> Status;
        public void Execute()
        {
            Cells.Clear();Status[0]=0;int used=0;
            for(int i=0;i<Solids.Length;i++)
            {
                var box=Solids[i];if(box.Active==0)continue;
                int3 low=(int3)math.floor(box.Minimum/XpClearanceMath015.Cell),high=(int3)math.floor(box.Maximum/XpClearanceMath015.Cell);
                int3 span=high-low+1;
                long total=(long)span.x*span.y*span.z;
                if(math.any(span<=0)||total>XpSolidLimits015.CellsPerSolid||total>Cells.Capacity-used)return;
                for(int z=low.z;z<=high.z;z++)for(int y=low.y;y<=high.y;y++)for(int x=low.x;x<=high.x;x++)
                {
                    if(!PoolXpMath015.TryMortonCoordinates(x,y,z,out ulong key))return;
                    Cells.Add(key,i);used++;
                }
            }
            Status[0]=1;
        }
    }
    [BurstCompile]
    public struct CertifyXpPairs015:IJobParallelFor
    {
        [ReadOnly]public NativeArray<XpSource64> Sources;
        [ReadOnly]public NativeArray<XpMergePair16> Pairs;
        [ReadOnly]public NativeArray<int> PairCount,GridStatus;
        [ReadOnly]public NativeArray<XpSolid32> Solids;
        [ReadOnly]public NativeParallelMultiHashMap<ulong,int> Cells;
        [WriteOnly]public NativeArray<XpMergeClearance8> Output;
        public uint WorldGeneration,SourceRevision,ExpectedSourceRevision;
        public int MaximumBaseXp;
        public void Execute(int index)
        {
            var result=new XpMergeClearance8{Status=0,WorldGeneration=WorldGeneration};
            if(WorldGeneration==0||SourceRevision==0||SourceRevision!=ExpectedSourceRevision||GridStatus[0]!=1||index>=PairCount[0]){Output[index]=result;return;}
            var pair=Pairs[index];
            if(pair.A<0||pair.B<0||pair.A>=Sources.Length||pair.B>=Sources.Length||pair.A==pair.B){result.Status=3;Output[index]=result;return;}
            var a=Sources[pair.A];var b=Sources[pair.B];
            if(!PoolXpMath015.SourceValid(a,MaximumBaseXp)||!PoolXpMath015.SourceValid(b,MaximumBaseXp)||
                a.SourceId!=pair.A+1u||b.SourceId!=pair.B+1u||a.RunEpoch!=pair.RunEpoch||b.RunEpoch!=pair.RunEpoch||
                a.RewardGeneration!=pair.RewardGeneration||b.RewardGeneration!=pair.RewardGeneration)
            {result.Status=3;Output[index]=result;return;}
            float3 p=new float3(a.X,a.Y+XpClearanceMath015.CenterY,a.Z),q=new float3(b.X,b.Y+XpClearanceMath015.CenterY,b.Z);
            int3 low=(int3)math.floor((math.min(p,q)-XpClearanceMath015.Half)/XpClearanceMath015.Cell);
            int3 high=(int3)math.floor((math.max(p,q)+XpClearanceMath015.Half)/XpClearanceMath015.Cell);
            int3 span=high-low+1;
            if(math.any(span<=0)||math.any(span>2)){Output[index]=result;return;}
            int visits=0;
            for(int z=low.z;z<=high.z;z++)for(int y=low.y;y<=high.y;y++)for(int x=low.x;x<=high.x;x++)
            {
                if(!PoolXpMath015.TryMortonCoordinates(x,y,z,out ulong key)){Output[index]=result;return;}
                if(!Cells.TryGetFirstValue(key,out int solid,out var cursor))continue;
                do
                {
                    if(++visits>XpSolidLimits015.Visits){Output[index]=result;return;}
                    XpSolid32 box=Solids[solid];
                    if(XpClearanceMath015.Blocked(a.X,a.Y,a.Z,b.X,b.Y,b.Z,box.Minimum.x,box.Minimum.y,box.Minimum.z,box.Maximum.x,box.Maximum.y,box.Maximum.z))
                    {result.Status=2;Output[index]=result;return;}
                }while(Cells.TryGetNextValue(out solid,ref cursor));
            }
            result.Status=1;Output[index]=result;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Closed world AABBs and original pair anchors; signed21-bit Morton63 keys with8m cells, conservative concave/rotated-solid supersets.
// Array Formats: XpSolidInput96[4104] -> XpSolid32[4104],131072 fixed Morton references,4095 pair certificates;2048 candidate visits per pair, overflow UNKNOWN.
// Dependencies/Pointers: World owns solids/grid/fence; Director owns sources/pairs/output and source revision. Main thread completes both owners before writes/reuse/dispose; no Unity Object or Physics inside jobs.
