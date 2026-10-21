using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using OdysseyOracle.Foundation.Pools015;

namespace OdysseyOracle
{
    [BurstCompile]
    public struct CopyXpMatrices015 : IJob
    {
        [ReadOnly] public NativeArray<float4x4> Input;
        [ReadOnly] public NativeArray<int> Count;
        [ReadOnly] public NativeArray<uint> SourceIds;
        [ReadOnly] public NativeArray<XpSource64> Sources;
        public NativeArray<Matrix4x4> Output;
        public void Execute()
        {
            int count = Count[0]; if (count < 0 || count > Input.Length || count > Output.Length) return;
            for (int i = 0; i < count; i++)
            {
                var m = Input[i]; var result = new Matrix4x4();
                int sourceIndex=(int)SourceIds[i]-1;
                if(sourceIndex<0||sourceIndex>=Sources.Length)return;
                // The original octahedron has only +/-axis vertices. Its exact
                // lowest transformed Y uses max(abs(rowY)), not a box extent.
                // Large-value views may lift visually; the claim point never moves.
                float extent=math.max(math.abs(m.c0.y),math.max(math.abs(m.c1.y),math.abs(m.c2.y)));
                m.c3.y=math.max(m.c3.y,Sources[sourceIndex].Y+.08f+extent);
                result.m00=m.c0.x;result.m10=m.c0.y;result.m20=m.c0.z;result.m30=m.c0.w;
                result.m01=m.c1.x;result.m11=m.c1.y;result.m21=m.c1.z;result.m31=m.c1.w;
                result.m02=m.c2.x;result.m12=m.c2.y;result.m22=m.c2.z;result.m32=m.c2.w;
                result.m03=m.c3.x;result.m13=m.c3.y;result.m23=m.c3.z;result.m33=m.c3.w;
                Output[i] = result;
            }
        }
    }
    [BurstCompile]
    public struct FindNearestXpSource015 : IJob
    {
        [ReadOnly] public NativeArray<XpSource64> Sources;
        [ReadOnly] public NativeParallelMultiHashMap<ulong,int> Cells;
        [ReadOnly] public NativeArray<float> ReadyAt, RetryAt;
        public NativeArray<int> Result;
        public float3 Feet;
        public float Radius, Now;
        public ulong Recipient;
        public int MaximumBaseXp;
        public void Execute()
        {
            Result[0] = 0;
            if (!math.all(math.isfinite(Feet)) || !math.isfinite(Now) || !math.isfinite(Radius) || Radius <= 0 || Radius > 8f) return;
            int cx=(int)math.floor(Feet.x/2f),cy=(int)math.floor(Feet.y/2f),cz=(int)math.floor(Feet.z/2f);
            int reach=(int)math.ceil(Radius/2f); float best=Radius*Radius; int chosen=-1;
            for(int x=cx-reach;x<=cx+reach;x++)for(int y=cy-reach;y<=cy+reach;y++)for(int z=cz-reach;z<=cz+reach;z++)
            {
                if(!PoolXpMath015.TryMortonCoordinates(x,y,z,out ulong key)||!Cells.TryGetFirstValue(key,out int index,out var iterator))continue;
                int budget=0;
                do
                {
                    if(++budget>PoolXpMath015.SourceCapacity)return;
                    var source=Sources[index];
                    uint bit=(source.EligibleMask&1u)!=0&&source.Owner0==Recipient?1u:
                        (source.EligibleMask&2u)!=0&&source.Owner1==Recipient?2u:0u;
                    if(bit==0||(source.ClaimedMask&bit)!=0||Now<ReadyAt[index]||Now<RetryAt[index]||
                        !PoolXpMath015.SourceValid(source,MaximumBaseXp))continue;
                    float distance=math.lengthsq(Feet-new float3(source.X,source.Y,source.Z));
                    if(distance>best||distance==best&&chosen>=0&&index>=chosen)continue;
                    best=distance;chosen=index;
                }while(Cells.TryGetNextValue(out index,ref iterator));
            }
            Result[0]=chosen+1;
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Original source world metres, recipient feet and signedMorton63 two-metre cells; matrix columns copied explicitly.
// Array Formats: Persistent4096source/map/matrix/ready/retry stores and one scalar nearest result; maximum9cubed cells, returned-source cost remains bounded4096.
// Dependencies/Pointers: Source owner completes jobs before claim LOS/Object/render calls. No Unity Object API, heap creation or distance-authority change inside numeric loops.
