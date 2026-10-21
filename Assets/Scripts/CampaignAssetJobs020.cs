// PSEUDOCODE: create exact pilot support samples; sort by Morton; group numeric poses; classify conservative bounds and hysteretic LOD into a distinct completed output bank.
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
namespace OdysseyOracle
{
    public struct CampaignAssetItem020 {public float4x4 Matrix;public float3 Feet,Center,Extents;public uint Morton,Key;}
    public struct CampaignAssetBucket020 {public int First,Count;public float3 Center,Extents;}
    public struct CampaignAssetView020 {public float3 Eye;public float4x4 Clip;public float Tangent,Near,Reach;}
    public struct CampaignAssetSupport020 {public float3 Origin;public int FloorSlot;}
    public struct CampaignAssetFrame020 {public ulong Sequence;public int Ready,Visible,Buckets;}
    public static class CampaignAssetMath020
    {
        public static int Lod(float size,int previous,float3 threshold,byte allowed)
        {
            if(!math.isfinite(size)||size<0||!math.all(math.isfinite(threshold))||threshold.x<=threshold.y||threshold.y<=threshold.z||threshold.z<=0||(allowed!=1&&allowed!=3&&allowed!=7))return -1;
            int last=(allowed&4)!=0?2:(allowed&2)!=0?1:0;
            if(previous<0||previous>last)return math.min(last,size>=threshold.x?0:size>=threshold.y?1:last);
            int next=previous;
            for(int finer=0;finer<next;finer++)if(size>=1.1f*(finer==0?threshold.x:threshold.y)){next=finer;break;}
            while(next<last&&size<.9f*(next==0?threshold.x:threshold.y))next++;
            return next;
        }
        public static float Height(float3 eye,float3 center,float3 extents,float tangent,float near)
        {float radius=math.length(extents);return radius/(math.max(math.distance(eye,center)-radius,near)*tangent);}
        static bool Plane(float4 p,float3 c,float3 e)=>math.dot(p.xyz,c)+p.w+math.dot(math.abs(p.xyz),e)>=0;
        public static bool Valid(in CampaignAssetView020 v)=>math.all(math.isfinite(v.Eye))&&math.all(math.isfinite(v.Clip.c0))&&math.all(math.isfinite(v.Clip.c1))&&math.all(math.isfinite(v.Clip.c2))&&math.all(math.isfinite(v.Clip.c3))&&math.abs(math.determinant(v.Clip))>1e-12f&&math.isfinite(v.Reach)&&math.isfinite(v.Tangent)&&math.isfinite(v.Near)&&v.Reach>0&&v.Tangent>0&&v.Near>0;
        public static bool Visible(in CampaignAssetView020 view,float3 center,float3 extents)
        {
            if(!Valid(view)||!math.all(math.isfinite(center))||!math.all(math.isfinite(extents))||math.any(extents<0))return false;
            float3 gap=math.max(math.abs(center-view.Eye)-extents,0);if(math.lengthsq(gap)>view.Reach*view.Reach)return false;
            float4x4 m=math.transpose(view.Clip);float4 a=m.c0,b=m.c1,c=m.c2,d=m.c3;
            return Plane(d+a,center,extents)&&Plane(d-a,center,extents)&&Plane(d+b,center,extents)&&Plane(d-b,center,extents)&&Plane(d+c,center,extents)&&Plane(d-c,center,extents);
        }
    }
    [BurstCompile] public struct CampaignAssetSupportJob020:IJobParallelFor
    {
        public NativeArray<CampaignAssetSupport020> Rays;
        public void Execute(int index)
        {
            int tile=index/9,sample=index%9;float x=-57+2*(tile%13),z=-29+2*(tile/13);
            float3 point=new float3(x+(sample%3-1)*.98f,0,z+(sample/3-1)*.98f);int slot=-1;
            for(int i=0;i<PortalLandingLayout020.SurfaceCount;i++)if(PortalLandingLayout020.TrySurface(i,out var f)&&f.Key==10&&f.MinX==-58&&f.MaxX==-32&&f.MinZ==-30&&f.MaxZ==-24&&f.MaxY==0){slot=i;break;}
            Rays[index]=new CampaignAssetSupport020{Origin=point+new float3(0,.25f,0),FloorSlot=slot};
        }
    }
    [BurstCompile] public struct CampaignAssetPartitionJob020:IJob
    {
        public NativeArray<CampaignAssetItem020> Items;public NativeArray<CampaignAssetBucket020> Buckets;public NativeArray<int> Count;public NativeArray<byte> FirstLods,SecondLods;public int Length;
        public void Execute()
        {
            Count[0]=-1;if(Length<1||Length>Items.Length||Length>CampaignAssetCatalog020.MaximumInstances||FirstLods.Length!=SecondLods.Length)return;
            for(int i=0;i<FirstLods.Length;i++)FirstLods[i]=SecondLods[i]=255;
            for(int i=0;i<Length;i++){var row=Items[i];row.Matrix=float4x4.TRS(row.Feet,quaternion.identity,new float3(1));row.Center+=row.Feet;if(!PortalLandingLayout020.TryMorton(row.Center.x,row.Center.z,out row.Morton))return;Items[i]=row;}
            for(int i=1;i<Length;i++){var row=Items[i];int j=i-1;while(j>=0&&(Items[j].Morton>row.Morton||(Items[j].Morton==row.Morton&&Items[j].Key>row.Key))){Items[j+1]=Items[j];j--;}Items[j+1]=row;}
            int at=0,groups=0;while(at<Length)
            {
                if(groups>=Buckets.Length)return;int first=at;uint key=Items[at].Morton>>10;float3 low=new float3(float.MaxValue),high=new float3(float.MinValue);
                while(at<Length&&(Items[at].Morton>>10)==key){var row=Items[at++];low=math.min(low,row.Center-row.Extents);high=math.max(high,row.Center+row.Extents);}
                Buckets[groups++]=new CampaignAssetBucket020{First=first,Count=at-first,Center=(low+high)*.5f,Extents=(high-low)*.5f};
            }
            Count[0]=groups;
        }
    }
    [BurstCompile] public struct CampaignAssetMaskJob020:IJob
    {
        [ReadOnly]public NativeArray<int> Source;public NativeArray<int> Destination;public int Length,Skip;
        public void Execute(){int at=0;for(int i=0;i<Length;i++)if(i<Skip||i>=Skip+6)Destination[at++]=Source[i];}
    }
    [BurstCompile] public struct CampaignAssetResetLodJob020:IJob
    {public NativeArray<byte> First,Second;public void Execute(){for(int i=0;i<First.Length;i++)First[i]=Second[i]=255;}}
    [BurstCompile] public struct CampaignAssetFrameJob020:IJob
    {
        [ReadOnly]public NativeArray<CampaignAssetItem020> Items;[ReadOnly]public NativeArray<CampaignAssetBucket020> Buckets;
        [ReadOnly]public NativeArray<byte> Previous;public NativeArray<byte> Next;public NativeArray<int> Counts,Starts;
        public NativeArray<float4x4> Matrices;public NativeArray<CampaignAssetFrame020> Result;
        public CampaignAssetView020 View;public float3 Thresholds;public byte Allowed;public int BucketCount,ItemCount;public ulong Sequence;
        public void Execute()
        {
            Result[0]=default;if(!CampaignAssetMath020.Valid(View)||ItemCount<1||ItemCount>Items.Length||BucketCount<1||BucketCount>Buckets.Length||BucketCount>Counts.Length||BucketCount>Starts.Length||BucketCount>Next.Length||BucketCount>Previous.Length)return;
            // All39tiles replace one original face; they form one atomic coverage group.
            // A partly admitted band must never remove the whole base while leaving undrawn holes.
            bool admitted=false;for(int b=0;b<BucketCount;b++)
            {
                var bucket=Buckets[b];Counts[b]=0;Starts[b]=0;Next[b]=Previous[b];
                if(bucket.First<0||bucket.Count<1||bucket.First+bucket.Count>ItemCount)return;
                if(CampaignAssetMath020.Visible(View,bucket.Center,bucket.Extents))admitted=true;
            }
            if(!admitted){Result[0]=new CampaignAssetFrame020{Sequence=Sequence,Ready=1,Visible=0,Buckets=0};return;}
            int used=0,visible=0;for(int b=0;b<BucketCount;b++)
            {
                var bucket=Buckets[b];Starts[b]=used;
                float size=0;for(int i=0;i<bucket.Count;i++){var row=Items[bucket.First+i];size=math.max(size,CampaignAssetMath020.Height(View.Eye,row.Center,row.Extents,View.Tangent,View.Near));}
                int lod=CampaignAssetMath020.Lod(size,Previous[b],Thresholds,Allowed);if(lod<0)return;Next[b]=(byte)lod;
                if(used+bucket.Count>Matrices.Length)return;
                for(int i=0;i<bucket.Count;i++)Matrices[used++]=Items[bucket.First+i].Matrix;
                Counts[b]=bucket.Count;visible++;
            }
            if(used!=ItemCount)return;Result[0]=new CampaignAssetFrame020{Sequence=Sequence,Ready=1,Visible=used,Buckets=visible};
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: World metres, existing quarter-metre Morton keys and8m buckets; exact floor key10 support.
// Array Formats: Fixed39pilot items,351support rays,<=256buckets, two distinct matrix/LOD banks; pure numeric worker loops.
// Dependencies/Pointers: No Object APIs in jobs; caller completes each fence before input mutation, publication or disposal. Pilot shadow casting remains off; no offscreen caster promise.
