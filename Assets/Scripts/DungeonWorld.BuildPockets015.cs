using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using OdysseyOracle.Foundation.World;
#if !DRY_POCKET_CPU_ONLY
using UnityEngine;
#endif

namespace OdysseyOracle
{
    internal static class DryPocketCode015
    {
        public const int Ready=1, Profiles=2, Envelope=3, Buffers=4, Centre=5, Bounds=6, Sector=7, Road=8, Roots=9, Keepouts=10, Publication=11, Applied=12, Allocation=13;
    }
    [StructLayout(LayoutKind.Sequential,Pack=4)]
    internal struct DryPocket32
    {
        public float2 Centre;
        public float KeepoutRadius;
        public int Lane,Roots,Cells;
        public uint Cell,Reserved;
    }
    [BurstCompile]
    internal struct DryPocketUnion015:IJob
    {
        public const int PocketCount=18,WordCount=1024,RootCount=450;
        public NativeArray<ulong> Mask;
        public NativeArray<DryPocket32> Pockets;
        // Status, accepted roots, unique selected cells, largest pocket cell count.
        public NativeArray<int> State;
        public float Radius,ArenaRadius,OuterFloorRadius;
        public static int Sector(float2 p)
            =>((int)math.round(math.atan2(p.y,p.x)*57.29578f/45f)+8)%8;
        public static bool Root(float3 p,int lane,float radius)
        {
            if(!LaneBuildMath015.Canonical(p,out _)||!LaneBuildMath015.InBand(p,lane))return false;
            float2 d=LaneBuildMath015.Direction(lane);
            float side=math.abs(math.dot(p.xz,new float2(-d.y,d.x)));
            return side>LaneBuildMath015.HalfRoad+radius+.001f;
        }
        public void Execute()
        {
            if(Mask.Length!=WordCount||Pockets.Length!=PocketCount||State.Length!=4)
            {if(State.Length>0)State[0]=DryPocketCode015.Buffers;return;}
            Clear(0);
            if(!math.isfinite(Radius)||Radius<LaneBuildMath015.SoleRadius||Radius>4||!math.isfinite(ArenaRadius)||ArenaRadius<=0||ArenaRadius>128||
                !math.isfinite(OuterFloorRadius)||OuterFloorRadius<=8.5f||OuterFloorRadius>ArenaRadius)
            {Clear(DryPocketCode015.Envelope);return;}
            for(int i=0;i<PocketCount;i++)
            {
                int lane=i<6?1:i<12?4:5;
                float along=20+16*((i%6)/2),side=(i%2==0?-1:1)*4.5f;
                float2 d=LaneBuildMath015.Direction(lane),n=new float2(-d.y,d.x),raw=d*along+n*side;
                if(!LaneBuildMath015.TryCell(new float3(raw.x,0,raw.y),out uint cell,out float3 centre))
                {Clear(DryPocketCode015.Centre);return;}
                if(!Root(centre,lane,Radius)){Clear(DryPocketCode015.Road);return;}
                int roots=0,cells=0;
                int2 minimum=new int2(int.MaxValue),maximum=new int2(int.MinValue);
                for(int oz=-2;oz<=2;oz++)for(int ox=-2;ox<=2;ox++)
                {
                    float3 p=centre+new float3(ox*.25f,0,oz*.25f);
                    if(!Root(p,lane,Radius))continue;
                    roots++;
                    // Identical inclusive support-cell convention to LaneBuildTerrainQuery015.
                    int2 low=(int2)math.floor(p.xz-Radius+.5f),high=(int2)math.floor(p.xz+Radius+.5f);
                    if(math.any(low< -128)||math.any(high>=128)||math.any(high-low>9))
                    {Clear(DryPocketCode015.Bounds);return;}
                    minimum=math.min(minimum,low);maximum=math.max(maximum,high);
                    for(int z=low.y;z<=high.y;z++)for(int x=low.x;x<=high.x;x++)
                    {
                        float2 position=new float2(x,z);
                        float distance=math.sqrt(math.lengthsq(position));
                        if(distance>ArenaRadius+.2f||distance<8.5f||distance>OuterFloorRadius)
                        {Clear(DryPocketCode015.Bounds);return;}
                        if(Sector(position)!=lane){Clear(DryPocketCode015.Sector);return;}
                        uint key=LaneBuildMath015.Morton(x+128,z+128,8);
                        int word=(int)(key>>6);ulong bit=1UL<<(int)(key&63);
                        if((Mask[word]&bit)==0){Mask[word]|=bit;cells++;State[2]++;}
                    }
                }
                if(roots==0||cells==0){Clear(DryPocketCode015.Roots);return;}
                // Cover the entire visible cell union, including tile corners and float rounding.
                float2 extent=math.max(math.abs((float2)minimum-.5f-centre.xz),math.abs((float2)maximum+.5f-centre.xz));
                float keepout=math.sqrt(math.lengthsq(extent))+.001f;
                if(!math.isfinite(keepout)||keepout<=0){Clear(DryPocketCode015.Bounds);return;}
                Pockets[i]=new DryPocket32{Centre=centre.xz,KeepoutRadius=keepout,Lane=lane,Roots=roots,Cells=cells,Cell=cell};
                State[1]+=roots;State[3]=math.max(State[3],cells);
            }
            if(State[1]>RootCount||State[2]>1800){Clear(DryPocketCode015.Bounds);return;}
            State[0]=DryPocketCode015.Ready;
        }
        void Clear(int status)
        {
            for(int i=0;i<Mask.Length;i++)Mask[i]=0;
            for(int i=0;i<Pockets.Length;i++)Pockets[i]=default;
            for(int i=0;i<State.Length;i++)State[i]=0;
            State[0]=status;
        }
    }

#if !DRY_POCKET_CPU_ONLY
    public sealed partial class DungeonWorld
    {
        readonly DryPocket32[] dryPocketReadback015=new DryPocket32[DryPocketUnion015.PocketCount];
        int dryPocketStatus015,dryPocketRoots015,dryPocketCells015,dryPocketApplied015,dryPocketKeepouts015;
        float dryPocketRadius015;
        bool dryPocketPrepared015,dryPocketGroundComplete015;
        public int DryBuildPocketStatus015=>dryPocketStatus015;
        public bool DryBuildPocketsReady015=>dryPocketStatus015==DryPocketCode015.Ready&&dryPocketGroundComplete015;
        public int DryBuildPocketCount015=>dryPocketPrepared015?DryPocketUnion015.PocketCount:0;
        public int DryBuildPocketRoots015=>dryPocketRoots015;
        public int DryBuildPocketCells015=>dryPocketCells015;
        public int DryBuildPocketAppliedCells015=>dryPocketApplied015;
        public int DryBuildPocketKeepouts015=>dryPocketKeepouts015;
        public float DryBuildPocketRadius015=>dryPocketRadius015;
        public bool TryGetDryBuildPocket015(int index,out Vector3 centre,out int lane,out int roots,out int cells,out float keepoutRadius)
        {
            centre=default;lane=-1;roots=cells=0;keepoutRadius=0;
            if(!DryBuildPocketsReady015||index<0||index>=dryPocketReadback015.Length)return false;
            var p=dryPocketReadback015[index];centre=new Vector3(p.Centre.x,0,p.Centre.y);lane=p.Lane;roots=p.Roots;cells=p.Cells;keepoutRadius=p.KeepoutRadius;return true;
        }
        struct DryPocketLease015:IDisposable
        {
            public NativeArray<ulong> Mask;
            public NativeArray<DryPocket32> Pockets;
            public NativeArray<int> State;
            public JobHandle Reader;
            public bool Ready=>State.IsCreated&&State.Length==4&&State[0]==DryPocketCode015.Ready;
            public void Dispose()
            {
                Reader.Complete();
                if(Mask.IsCreated)Mask.Dispose();if(Pockets.IsCreated)Pockets.Dispose();if(State.IsCreated)State.Dispose();
            }
        }
        DryPocketLease015 PrepareDryBuildPockets015()
        {
            dryPocketPrepared015=dryPocketGroundComplete015=false;dryPocketStatus015=0;
            dryPocketRoots015=dryPocketCells015=dryPocketApplied015=dryPocketKeepouts015=0;dryPocketRadius015=0;
            for(int i=0;i<dryPocketReadback015.Length;i++)dryPocketReadback015[i]=default;
            float radius=LaneBuildMath015.SoleRadius;
            for(int type=0;type<4;type++)
            {
                // Existing helper validates all three saved tiers, including animated silhouette bounds.
                if(!LaneBuildEnvelope015(type,out float r,out _)){dryPocketStatus015=DryPocketCode015.Profiles;return default;}
                radius=Mathf.Max(radius,r);
            }
            dryPocketRadius015=radius;
            if(vignetteKeepoutOverflow||vignetteKeepouts.Count>4096-DryPocketUnion015.PocketCount)
            {dryPocketStatus015=DryPocketCode015.Keepouts;return default;}
            DryPocketLease015 lease=default;int keepoutStart=vignetteKeepouts.Count;
            try
            {
                lease.Mask=new NativeArray<ulong>(DryPocketUnion015.WordCount,Allocator.TempJob);
                lease.Pockets=new NativeArray<DryPocket32>(DryPocketUnion015.PocketCount,Allocator.TempJob);
                lease.State=new NativeArray<int>(4,Allocator.TempJob);
                lease.Reader=new DryPocketUnion015{Mask=lease.Mask,Pockets=lease.Pockets,State=lease.State,Radius=radius,
                    ArenaRadius=ArenaRadius,OuterFloorRadius=ArenaRingRadius-2.5f}.Schedule();
                lease.Reader.Complete();dryPocketStatus015=lease.State[0];
                if(!lease.Ready)return lease; // Caller finally disposes even rejected cold plans.
                // Capacity/all18 preflight completed before the first reservation or ground replacement.
                if(vignetteKeepouts.Capacity<keepoutStart+DryPocketUnion015.PocketCount)
                    vignetteKeepouts.Capacity=keepoutStart+DryPocketUnion015.PocketCount;
                for(int i=0;i<DryPocketUnion015.PocketCount;i++)
                {
                    var p=lease.Pockets[i];
                    vignetteKeepouts.Add(new VignetteKeepout(p.Centre.x,p.Centre.y,p.KeepoutRadius));
                }
                lease.Pockets.CopyTo(dryPocketReadback015);
                dryPocketRoots015=lease.State[1];dryPocketCells015=lease.State[2];dryPocketKeepouts015=DryPocketUnion015.PocketCount;
                dryPocketPrepared015=true;return lease;
            }
            catch(Exception)
            {
                // No ground publication has begun. Roll back only this cold owner's added reservations.
                if(vignetteKeepouts.Count>keepoutStart)vignetteKeepouts.RemoveRange(keepoutStart,vignetteKeepouts.Count-keepoutStart);
                dryPocketStatus015=lease.State.IsCreated?DryPocketCode015.Publication:DryPocketCode015.Allocation;
                dryPocketPrepared015=false;dryPocketRoots015=dryPocketCells015=dryPocketKeepouts015=0;
                for(int i=0;i<dryPocketReadback015.Length;i++)dryPocketReadback015[i]=default;
                lease.Dispose();return default;
            }
        }
        Surface SelectDryBuildPocketSurface015(Surface selected,int x,int z,int sector,in DryPocketLease015 lease)
        {
            if(!dryPocketPrepared015||!lease.Ready)return selected;
            uint key=LaneBuildMath015.Morton(x+128,z+128,8);
            if((lease.Mask[(int)(key>>6)]&(1UL<<(int)(key&63)))==0)return selected;
            dryPocketApplied015++;
            switch(sector)
            {
                case 1:return Surface.Soil;
                case 4:return Surface.Sand;
                case 5:return Surface.Snow;
                default:throw new InvalidOperationException("Dry ground sector differs from its validated cell union.");
            }
        }
        void CompleteDryBuildPocketGround015(in DryPocketLease015 lease)
        {
            if(!dryPocketPrepared015)return;
            if(!lease.Ready||dryPocketApplied015!=dryPocketCells015)
            {dryPocketStatus015=DryPocketCode015.Applied;dryPocketPrepared015=false;throw new InvalidOperationException("Dry ground publication did not match its complete validated cell union.");}
            dryPocketGroundComplete015=true;
        }
    }
#endif
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: world-XZ quarter roots and complete1m support cells at exactY0; existing raised roads/curbs/water-lava stamps and permissions are unchanged.
// Array Formats: one cold ulong[1024] Morton union,Pocket32[18],int[4]; <=450 root candidates/1800 unique cells; bounded18-record scalar readback copy after completion.
// Dependencies/Pointers: World owns current model envelopes, ground batches and vignette reservations; numeric job has no Object API. Reader completes before main-thread use/finally disposal; one chosen Surface drives both visible ground and terrain semantics.
