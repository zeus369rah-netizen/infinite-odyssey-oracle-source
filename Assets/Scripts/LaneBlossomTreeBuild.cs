using System;
using System.Collections.Generic;
using OdysseyOracle.Foundation.World;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace OdysseyOracle
{
    public delegate void BlossomTreeWriter(BlossomTreePlan48 plan);
    // Cold native build lease. The World alone publishes completed geometry and owns final resources.
    public sealed class LaneBlossomTreeBuild:IDisposable
    {
        NativeArray<BlossomTreePlan48> plans;NativeArray<BlossomTreeSeed96> seeds;
        NativeArray<byte> cells;NativeArray<ulong> leaves,summaries;NativeArray<int> mask;
        NativeArray<BlossomTreeQuad64> quads;NativeArray<BlossomTreeResult> results;
        JobHandle fence;bool scheduled,disposed;
        public int Count=>seeds.IsCreated?seeds.Length:0;
        public BlossomTreeSeed96 Seed(int tree)=>seeds[tree];
        public BlossomTreeResult Result(int tree)=>results[tree];
        public BlossomTreeQuad64 Quad(int tree,int quad)=>quads[tree*BlossomTreeMath.MaximumQuads+quad];
        public static BlossomTreePlan48 CreatePlan(int lane,bool hero,int theme,double height,VignettePoint root,double yaw)
        {
            return new BlossomTreePlan48{
                Root=new float3((float)root.X,(float)root.Y,(float)root.Z),YawDegrees=(float)yaw,
                Height=(float)height/BlossomTreeMath.Scale,CrownRadius=hero?2.40f:2.60f,
                TrunkRadius=hero?.60f:.50f,RootRadius=hero?.80f:.68f,
                Seed=BlossomTreeMath.Mix(671904u^(uint)(lane*73856093)^(hero?0x92d68ca2u:0x4f1bbcdcu)),
                Flags=hero?0u:1u,Lane=(uint)lane,Theme=(uint)theme};
        }
        public static LaneBlossomTreeBuild Build(IReadOnlyList<BlossomTreePlan48> source,LaneVignetteBuildResult budget)
        {
            if(source==null||source.Count!=16||budget==null)throw new ArgumentException("Exactly16declared current-arena tree roles required.");
            var build=new LaneBlossomTreeBuild();
            try
            {
                int count=source.Count;build.plans=new NativeArray<BlossomTreePlan48>(count,Allocator.TempJob);build.seeds=new NativeArray<BlossomTreeSeed96>(count,Allocator.TempJob);
                int blossom=0;var perLane=new int[8];
                for(int i=0;i<count;i++){var seed=source[i];if(seed.Lane>=8||(seed.Flags&~1u)!=0)throw new InvalidOperationException("Invalid tree role declaration.");perLane[seed.Lane]++;if((seed.Flags&1u)!=0)blossom++;build.plans[i]=seed;}
                if(blossom!=8)throw new InvalidOperationException("One blossom companion per road required.");for(int i=0;i<8;i++)if(perLane[i]!=2)throw new InvalidOperationException("Exactlytwo existing tree roles per road required.");
                build.cells=new NativeArray<byte>(count*BlossomTreeMath.CellsPerTree,Allocator.TempJob);
                build.leaves=new NativeArray<ulong>(count*BlossomTreeMath.LeavesPerTree,Allocator.TempJob);
                build.summaries=new NativeArray<ulong>(count*BlossomTreeMath.ChunkSlots,Allocator.TempJob);
                build.mask=new NativeArray<int>(count*BlossomTreeMath.MaskCells,Allocator.TempJob,NativeArrayOptions.UninitializedMemory);
                build.quads=new NativeArray<BlossomTreeQuad64>(count*BlossomTreeMath.MaximumQuads,Allocator.TempJob,NativeArrayOptions.UninitializedMemory);
                build.results=new NativeArray<BlossomTreeResult>(count,Allocator.TempJob);
                build.fence=new BlossomTreeMeshJob{Plans=build.plans,Seeds=build.seeds,Cells=build.cells,Leaves=build.leaves,Summaries=build.summaries,Mask=build.mask,Quads=build.quads,Results=build.results}.Schedule(count,1);build.scheduled=true;build.fence.Complete();build.scheduled=false;
                var added=new int[8];int total=0;
                for(int i=0;i<count;i++)
                {
                    var seed=build.seeds[i];var result=build.results[i];
                    if(result.Status!=1)throw new InvalidOperationException("Tree greedy mesh rejected classification/bounds/cell/output cap: "+result.Status);
                    int triangles=result.Quads*2;added[seed.Lane]+=triangles;total+=triangles;
                }
                for(int lane=0;lane<8;lane++)if(budget.Triangles[lane]+added[lane]>LaneVignetteRecipes.MaximumTrianglesPerLane)throw new InvalidOperationException("Refined tree exceeds existing per-road triangle budget.");
                if(budget.TotalTriangles+total>LaneVignetteRecipes.MaximumTrianglesTotal)throw new InvalidOperationException("Refined tree exceeds existing global triangle budget.");
                for(int lane=0;lane<8;lane++)budget.Triangles[lane]+=added[lane];budget.Pieces+=total/2;
                return build;
            }
            catch{build.Dispose();throw;}
        }
        public static Color LeafTint(BlossomTreeSeed96 seed,uint surface)
        {
            int tier=Mathf.Clamp((int)surface-3,0,2);
            LaneVignetteRecipes.TreePalette((int)seed.Theme,tier,(seed.Flags&1u)!=0,out double r,out double g,out double b,out double glow);
            // Multiplicative brightness preserves the authored hue instead of whitening every region.
            float gain=.97f+.06f*((BlossomTreeMath.Mix(unchecked(seed.Seed+17u+(uint)tier*101u))&255u)/255f);
            return new Color(Mathf.Clamp01((float)r*gain),Mathf.Clamp01((float)g*gain),Mathf.Clamp01((float)b*gain),(float)glow);
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;if(scheduled){fence.Complete();scheduled=false;}
            if(results.IsCreated)results.Dispose();if(quads.IsCreated)quads.Dispose();if(mask.IsCreated)mask.Dispose();if(summaries.IsCreated)summaries.Dispose();if(leaves.IsCreated)leaves.Dispose();if(cells.IsCreated)cells.Dispose();if(seeds.IsCreated)seeds.Dispose();if(plans.IsCreated)plans.Dispose();
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: existing root/yaw in arena-local metres; seed dimensions divided by3 once and one3xTRS only; deterministic671904 role hashes do not consume World.random.
// Array Formats: exactly16seeds,8blossom companions, one3xmatrix constructed only inside the job; bounded cleared TempJob Mortoncell/occupancy slabs and capped greedy output. One parallel job completes before read/publish and finally disposal; original2400/road19200globaltriangle caps retained.
// Dependencies/Pointers: cold-only managed source/budget adapters and original authored color values; no perframe owner/alloc/Physics/UnityObject math in Burst. No external media, new material/light, actors or reward/collision authority. Root verifies native/Burst/render/LOS/performance.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing arena roots/height/one3xTRS and original floor, wider reserved grass silhouette; no new world coordinate owner.
// Array Formats: Existing16tree Morton slabs/greedy64Bquads and cold combined weather lists, same fixed triangle/bucket/resource caps.
// Dependencies/Pointers: Original palette/math only; no global random, per-leaf Objects, hot owner, collision/route mutation, new shader or provider. Root native visual/performance and fresh menu catalog acceptance pending.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Same tree-local dimensions and one3xTRS, unchanged roots/crowns/floor and full rotated trunk-proxy reservation.
// Array Formats: Existing16cold48Bplans and96Bseeds; only tapered trunk radius changes inside the original fixed native cell/quad budgets.
// Dependencies/Pointers: Existing Burst classifier/greedy pass, complete-before-read/dispose owner, regional leaf palette and Wind14 remain unchanged; root verifies actual render/LOD/Web cost.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: existing8appearance regions and one3xTRS; original root/grass/proxy/culling envelopes unchanged, stone-theme5crown is presentation-rigid.
// Array Formats: unchanged fixed tree plans/native slabs/greedy faces and mesh COLOR/UV1 streams; cold scalar TreePalette is shared by trees and grass, no new light/material/texture/particle owner.
// Dependencies/Pointers: original bounded wind/shader alpha emission and World shared materials; prior trunk widening composed once. Root checks actual current-arena colors/fallback and later rebakes menu catalog; source-only evidence is not pixel/performance acceptance.
