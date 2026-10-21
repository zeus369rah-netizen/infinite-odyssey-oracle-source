using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace OdysseyOracle.Foundation.World
{
    [StructLayout(LayoutKind.Sequential)]
    public struct BlossomTreePlan48
    {
        public float3 Root;public float YawDegrees;
        public float Height,CrownRadius,TrunkRadius,RootRadius;
        public uint Seed,Flags,Lane,Theme;
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct BlossomTreeSeed96
    {
        public float4x4 LocalToWorld; // Exactly one3xscale; every dimension below is unscaled.
        public float Height,CrownRadius,TrunkRadius,RootRadius;
        public uint Seed,Flags,Lane,Theme; // Flags bit0: blossom companion.
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct BlossomTreeQuad64
    {
        public float3 A,B,C,D,Normal;
        public uint Surface; //1bark,3..5leaf tiers.
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct BlossomTreeResult
    {
        public int Status,Quads,SolidCells,LeafCells;
        public float3 Minimum,Maximum;
        public float MaximumRadius,BarkRadius,BarkHeight,MinimumLeafHeight;
    }
    public static class BlossomTreeMath
    {
        public const float Scale=3f,Cell=.125f,WorldCell=.375f,GridHalf=3f;
        public const int SizeX=48,SizeY=96,SizeZ=48,ChunkSlots=256,CellsPerChunk=4096,CellsPerTree=ChunkSlots*CellsPerChunk,LeavesPerTree=ChunkSlots*64,MaximumQuads=1024,MaskCells=SizeY*SizeX;
        public static uint Mix(uint value){value^=value>>16;value*=0x7feb352du;value^=value>>15;value*=0x846ca68bu;return value^(value>>16);}
        public static uint Spread(uint value){value&=15;value=(value|(value<<4))&0x0f0fu;value=(value|(value<<2))&0x3333u;value=(value|(value<<1))&0x5555u;return value;}
        public static int Morton3(int x,int y,int z)
        {
            uint a=(uint)x,b=(uint)y,c=(uint)z,result=0;
            for(int bit=0;bit<4;bit++)result|=((a>>bit)&1u)<<(bit*3)|((b>>bit)&1u)<<(bit*3+1)|((c>>bit)&1u)<<(bit*3+2);
            return (int)result;
        }
        public static int Chunk(int3 cell)=>Morton3(cell.x>>4,cell.y>>4,cell.z>>4);
        public static int Local(int3 cell)=>Morton3(cell.x&15,cell.y&15,cell.z&15);
        public static int Index(int3 cell)=>Chunk(cell)*CellsPerChunk+Local(cell);
        public static bool Inside(int3 p)=>math.all(p>=0)&&p.x<SizeX&&p.y<SizeY&&p.z<SizeZ;
        public static float3 Corner(int3 p)=>new float3(p.x*Cell-GridHalf,p.y*Cell,p.z*Cell-GridHalf);
        public static float Octagon(float x,float z){float2 a=math.abs(new float2(x,z));return math.max(a.x,a.y)+math.min(a.x,a.y)*.41421356237f;}
        public static byte Classify(BlossomTreeSeed96 seed,int3 cell)
        {
            float3 p=Corner(cell)+Cell*.5f;
            if(p.y<=seed.Height*.84f)
            {
                float t=math.saturate(p.y/(seed.Height*.84f));
                float radius=seed.TrunkRadius*math.lerp(1,.50f,t);
                // Three-quarter metre buttress strips remain joined to the taper and floor.
                float flare=math.max(0,1-p.y/.875f)*(seed.RootRadius-seed.TrunkRadius);
                if(Octagon(p.x,p.z)<=radius||
                    (math.min(math.abs(p.x),math.abs(p.z))<=.125f&&math.max(math.abs(p.x),math.abs(p.z))<=radius+flare))return 1;
                // Two diagonal forks remain within the declared root/proxy horizontal envelope.
                if(t>=.70f)
                {
                    float travel=(t-.70f)/.30f*(seed.RootRadius-.18f);
                    if(math.abs(p.z)<.125f&&math.abs(math.abs(p.x)-travel)<.125f)return 1;
                }
            }
            // Group four microcells for a small1.5m leaf-block silhouette, then greedy merge.
            float3 leaf=Corner((cell/4)*4)+Cell*2;
            float offset=((Mix(seed.Seed)&255u)/255f-.5f)*.26f;
            for(int tier=2;tier>=0;tier--)
            {
                float center=seed.Height*(tier==0?.78f:tier==1?.875f:.96f);
                float half=seed.Height*(tier==0?.075f:tier==1?.070f:.060f);
                float vertical=math.abs(leaf.y-center)/half;if(vertical>1)continue;
                float radius=seed.CrownRadius*(tier==0?1f:tier==1?.90f:.68f)*(1-.38f*vertical);
                float shift=offset*(tier-1),x=leaf.x-shift,z=leaf.z+shift*.65f;
                if(Octagon(x,z)<=radius)return (byte)(3+tier);
            }
            // Leaf-only union: fill the existing radial envelope down into a broad crown.
            // Original bark, top tiers, height and one3xmatrix remain untouched.
            float lushVertical=math.abs(leaf.y-seed.Height*.64f)/(seed.Height*.38f);
            if(lushVertical<=1f&&leaf.y>=1.5f)
            {
                float radius=seed.CrownRadius*(1f-.18f*lushVertical);
                if(Octagon(leaf.x-offset,leaf.z+offset*.65f)<=radius)
                    return (byte)(leaf.y<seed.Height*.70f?3:4);
            }
            return 0;
        }
        public static BlossomTreeSeed96 CreateSeed(BlossomTreePlan48 plan)=>new BlossomTreeSeed96{
            LocalToWorld=float4x4.TRS(plan.Root,quaternion.RotateY(math.radians(plan.YawDegrees)),new float3(Scale)),
            Height=plan.Height,CrownRadius=plan.CrownRadius,TrunkRadius=plan.TrunkRadius,RootRadius=plan.RootRadius,Seed=plan.Seed,Flags=plan.Flags,Lane=plan.Lane,Theme=plan.Theme};
        public static bool ValidateMeshBounds(BlossomTreeSeed96 seed,BlossomTreeResult result)
        {
            float3 root=seed.LocalToWorld.c3.xyz;
            return result.Quads>0&&result.Quads<=MaximumQuads&&result.SolidCells>0&&result.LeafCells>0&&
                math.all(math.isfinite(result.Minimum))&&math.all(math.isfinite(result.Maximum))&&result.Minimum.y==0&&
                result.Maximum.y<=seed.Height*Scale*1.05f&&result.MaximumRadius<=8.75f&&result.BarkRadius<=math.ceil(seed.RootRadius/Cell)*Cell*Scale&&result.BarkHeight<=seed.Height*Scale*.84f+WorldCell&&result.MinimumLeafHeight>=4.5f;
        }
        public static bool ValidateSeed(BlossomTreeSeed96 seed)
        {
            float4x4 m=seed.LocalToWorld;
            return math.all(math.isfinite(m.c0))&&math.all(math.isfinite(m.c1))&&math.all(math.isfinite(m.c2))&&math.all(math.isfinite(m.c3))&&
                math.abs(math.length(m.c0.xyz)-Scale)<.0001f&&math.abs(math.length(m.c1.xyz)-Scale)<.0001f&&math.abs(math.length(m.c2.xyz)-Scale)<.0001f&&
                math.abs(math.dot(m.c0.xyz,m.c1.xyz))<.0001f&&math.abs(math.dot(m.c0.xyz,m.c2.xyz))<.0001f&&math.abs(math.dot(m.c1.xyz,m.c2.xyz))<.0001f&&
                m.c3.y==0&&m.c3.w==1&&m.c0.w==0&&m.c1.w==0&&m.c2.w==0&&
                math.isfinite(seed.Height)&&seed.Height>=6&&seed.Height<=10.667f&&seed.CrownRadius>=2&&seed.CrownRadius<=2.7f&&
                seed.TrunkRadius>=.35f&&seed.TrunkRadius<=.6f&&seed.RootRadius>=seed.TrunkRadius&&seed.RootRadius<=.85f&&seed.Lane<8&&seed.Theme<8&&(seed.Flags&~1u)==0;
        }
    }
    [BurstCompile]
    public struct BlossomTreeMeshJob:IJobParallelFor
    {
        [ReadOnly] public NativeArray<BlossomTreePlan48> Plans;
        public NativeArray<BlossomTreeSeed96> Seeds;
        // Each tree index owns disjoint fixed strides below; no shared writer or raw pointer.
        [NativeDisableParallelForRestriction] public NativeArray<byte> Cells;
        [NativeDisableParallelForRestriction] public NativeArray<ulong> Leaves,Summaries;
        [NativeDisableParallelForRestriction] public NativeArray<int> Mask;
        [NativeDisableParallelForRestriction] public NativeArray<BlossomTreeQuad64> Quads;
        public NativeArray<BlossomTreeResult> Results;
        public void Execute(int tree)
        {
            var seed=BlossomTreeMath.CreateSeed(Plans[tree]);Seeds[tree]=seed;var result=new BlossomTreeResult{Minimum=new float3(float.PositiveInfinity),Maximum=new float3(float.NegativeInfinity),MinimumLeafHeight=float.PositiveInfinity};
            if(!BlossomTreeMath.ValidateSeed(seed)){result.Status=-1;Results[tree]=result;return;}
            int cb=tree*BlossomTreeMath.CellsPerTree,lb=tree*BlossomTreeMath.LeavesPerTree,sb=tree*BlossomTreeMath.ChunkSlots;
            for(int z=0;z<BlossomTreeMath.SizeZ;z++)for(int y=0;y<BlossomTreeMath.SizeY;y++)for(int x=0;x<BlossomTreeMath.SizeX;x++)
            {
                int3 p=new int3(x,y,z);byte surface=BlossomTreeMath.Classify(seed,p);if(surface==0)continue;
                int chunk=BlossomTreeMath.Chunk(p),local=BlossomTreeMath.Local(p),leaf=local>>6;ulong bit=1UL<<(local&63);
                // Classification is unique per coordinate; stale duplicate data is a rejected contract.
                if(Cells[cb+chunk*4096+local]!=0){result.Status=-2;Results[tree]=result;return;}
                Cells[cb+chunk*4096+local]=surface;Leaves[lb+chunk*64+leaf]|=bit;Summaries[sb+chunk]|=1UL<<leaf;result.SolidCells++;if(surface>=3)result.LeafCells++;
            }
            for(int z=1;z<BlossomTreeMath.SizeZ-1;z++)for(int y=1;y<BlossomTreeMath.SizeY-1;y++)for(int x=1;x<BlossomTreeMath.SizeX-1;x++)
            {
                int3 p=new int3(x,y,z);if(Read(tree,p)!=0)continue;
                if(Read(tree,p+new int3(1,0,0))!=0&&Read(tree,p-new int3(1,0,0))!=0&&Read(tree,p+new int3(0,1,0))!=0&&Read(tree,p-new int3(0,1,0))!=0&&Read(tree,p+new int3(0,0,1))!=0&&Read(tree,p-new int3(0,0,1))!=0)
                {result.Status=-3;Results[tree]=result;return;}
            }
            for(int axis=0;axis<3;axis++)
            {
                int u=(axis+1)%3,v=(axis+2)%3,width=Dimension(u),height=Dimension(v),depth=Dimension(axis),mb=tree*BlossomTreeMath.MaskCells;
                for(int slice=-1;slice<depth;slice++)
                {
                    for(int j=0;j<height;j++)for(int i=0;i<width;i++)
                    {
                        int3 a=Coordinate(axis,slice,u,i,v,j),b=a;b[axis]++;
                        int back=Read(tree,a),front=Read(tree,b);
                        Mask[mb+i+j*width]=back!=0&&front==0?back:back==0&&front!=0?-front:0;
                    }
                    for(int j=0;j<height;j++)for(int i=0;i<width;)
                    {
                        int group=Mask[mb+i+j*width];if(group==0){i++;continue;}
                        int w=1;while(i+w<width&&Mask[mb+i+w+j*width]==group)w++;
                        int h=1;bool row=true;while(j+h<height&&row){for(int k=0;k<w;k++)if(Mask[mb+i+k+(j+h)*width]!=group){row=false;break;}if(row)h++;}
                        if(result.Quads==BlossomTreeMath.MaximumQuads){result.Status=-4;Results[tree]=result;return;}
                        int3 p=Coordinate(axis,slice+1,u,i,v,j);float3 a=BlossomTreeMath.Corner(p),du=float3.zero,dv=float3.zero;du[u]=w*BlossomTreeMath.Cell;dv[v]=h*BlossomTreeMath.Cell;
                        float3 aa=math.transform(seed.LocalToWorld,a),bb=math.transform(seed.LocalToWorld,a+du),cc=math.transform(seed.LocalToWorld,a+du+dv),dd=math.transform(seed.LocalToWorld,a+dv);
                        if(group<0){float3 swap=bb;bb=dd;dd=swap;}float3 normal=float3.zero;normal[axis]=group>0?1:-1;normal=math.normalizesafe(math.rotate(seed.LocalToWorld,normal));
                        Quads[tree*BlossomTreeMath.MaximumQuads+result.Quads++]=new BlossomTreeQuad64{A=aa,B=bb,C=cc,D=dd,Normal=normal,Surface=(uint)math.abs(group)};
                        result.Minimum=math.min(result.Minimum,math.min(math.min(aa,bb),math.min(cc,dd)));result.Maximum=math.max(result.Maximum,math.max(math.max(aa,bb),math.max(cc,dd)));
                        float3 root=seed.LocalToWorld.c3.xyz;
                        float radial=math.sqrt(math.max(math.max(math.distancesq(aa.xz,root.xz),math.distancesq(bb.xz,root.xz)),math.max(math.distancesq(cc.xz,root.xz),math.distancesq(dd.xz,root.xz))));
                        result.MaximumRadius=math.max(result.MaximumRadius,radial);
                        if(math.abs(group)==1){result.BarkRadius=math.max(result.BarkRadius,radial);result.BarkHeight=math.max(result.BarkHeight,math.max(math.max(aa.y,bb.y),math.max(cc.y,dd.y)));}
                        else result.MinimumLeafHeight=math.min(result.MinimumLeafHeight,math.min(math.min(aa.y,bb.y),math.min(cc.y,dd.y)));

                        for(int yy=0;yy<h;yy++)for(int xx=0;xx<w;xx++)Mask[mb+i+xx+(j+yy)*width]=0;i+=w;
                    }
                }
            }
            result.Status=BlossomTreeMath.ValidateMeshBounds(seed,result)?1:-5;Results[tree]=result;
        }
        byte Read(int tree,int3 p)
        {
            if(!BlossomTreeMath.Inside(p))return 0;int chunk=BlossomTreeMath.Chunk(p),local=BlossomTreeMath.Local(p),leaf=local>>6;
            if((Summaries[tree*BlossomTreeMath.ChunkSlots+chunk]&(1UL<<leaf))==0||(Leaves[tree*BlossomTreeMath.LeavesPerTree+chunk*64+leaf]&(1UL<<(local&63)))==0)return 0;
            return Cells[tree*BlossomTreeMath.CellsPerTree+chunk*4096+local];
        }
        static int Dimension(int axis)=>axis==0?BlossomTreeMath.SizeX:axis==1?BlossomTreeMath.SizeY:BlossomTreeMath.SizeZ;
        static int3 Coordinate(int axis,int slice,int u,int i,int v,int j){int3 p=default;p[axis]=slice;p[u]=i;p[v]=j;return p;}
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: tree-local0.125grid, Morton16cells+Morton chunk keys, exactfloor0; one3xTRS yields0.375worldmicrocells and preserves19..32m treeheight.
// Array Formats: blittable48Bplan/96Bseed/64Bquad/56Bresult; fixed256chunk slots,64ulong occupancy leaves+one summary/chunk, flat bytecell slab, disjoint per-tree mask/output strides; no hash duplicates/rawpointers/UnityObject APIs.
// Dependencies/Pointers: caller cold-allocates cleared TempJob slabs, Schedule/Completes before publishing/reading/disposal and rejects all negative status/caps; shader wind is bounded separately. Full native/Burst/render/LOS/browser acceptance belongs to root.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing arena roots/height/one3xTRS and original floor, wider reserved grass silhouette; no new world coordinate owner.
// Array Formats: Existing16tree Morton slabs/greedy64Bquads and cold combined weather lists, same fixed triangle/bucket/resource caps.
// Dependencies/Pointers: Original palette/math only; no global random, per-leaf Objects, hot owner, collision/route mutation, new shader or provider. Root native visual/performance and fresh menu catalog acceptance pending.
