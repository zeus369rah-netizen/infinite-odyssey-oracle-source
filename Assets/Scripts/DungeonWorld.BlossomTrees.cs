using OdysseyOracle.Foundation.World;
using UnityEngine;
namespace OdysseyOracle
{
    public sealed partial class DungeonWorld
    {
        void AppendRefinedArenaTrees(LaneBlossomTreeBuild build)
        {
            for(int tree=0;tree<build.Count;tree++)
            {
                BlossomTreeSeed96 seed=build.Seed(tree);int count=build.Result(tree).Quads;
                for(int index=0;index<count;index++)
                {
                    BlossomTreeQuad64 face=build.Quad(tree,index);Vector3 a=TreePoint(face.A),b=TreePoint(face.B),c=TreePoint(face.C),d=TreePoint(face.D),center=(a+b+c+d)*.25f;
                    bool leaf=face.Surface>=3;
                    if(leaf&&laneVignetteRenderer.ShaderReady){laneVignetteRenderer.AppendTreeFace(face,seed,LaneBlossomTreeBuild.LeafTint(seed,face.Surface));continue;}
                    Surface surface;
                    if(!leaf)surface=seed.Theme==0||seed.Theme==7||seed.Theme==1?Surface.DarkStone:seed.Theme==5||seed.Theme==4||seed.Theme==2?Surface.PaleStone:Surface.Wood;
                    else if((seed.Flags&1u)!=0&&seed.Theme==6)surface=face.Surface==3?Surface.Blossom:Surface.BlossomPale;
                    else surface=seed.Theme==0?Surface.Autumn:seed.Theme==7?Surface.Ember:seed.Theme==6?Surface.Green:seed.Theme==5?Surface.PaleStone:seed.Theme==3?Surface.Water:seed.Theme==4?Surface.Snow:seed.Theme==2?Surface.PaleStone:Surface.Azure;
                    Batch batch=GetGeometryBatch(surface,center,out Vector3 origin);batch.Quad(a-origin,b-origin,c-origin,d-origin);
                }
            }
        }
        static Vector3 TreePoint(Unity.Mathematics.float3 p)=>new Vector3(p.x,p.y,p.z);
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: completed arena-local faces->existing10m root-owned geometry/weather chunks. Exact integer-grid planes share one3xTRS, hidden interfaces omitted; groundbase0.
// Array Formats: bounded completed64Bquads copied into existing shared-material cold lists;16existingtrunk proxies stay separate, no canopy collision/newLOSobjects.
// Dependencies/Pointers: this is the main-thread Unity adapter ONLY; all classification/occupancy/meshing/transformloops ran in Burst jobs. No percellObjects/materialclone/lights/Update or gameplay/save changes.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing arena roots/height/one3xTRS and original floor, wider reserved grass silhouette; no new world coordinate owner.
// Array Formats: Existing16tree Morton slabs/greedy64Bquads and cold combined weather lists, same fixed triangle/bucket/resource caps.
// Dependencies/Pointers: Original palette/math only; no global random, per-leaf Objects, hot owner, collision/route mutation, new shader or provider. Root native visual/performance and fresh menu catalog acceptance pending.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: existing8appearance regions and one3xTRS; original root/grass/proxy/culling envelopes unchanged, stone-theme5crown is presentation-rigid.
// Array Formats: unchanged fixed tree plans/native slabs/greedy faces and mesh COLOR/UV1 streams; cold scalar TreePalette is shared by trees and grass, no new light/material/texture/particle owner.
// Dependencies/Pointers: original bounded wind/shader alpha emission and World shared materials; prior trunk widening composed once. Root checks actual current-arena colors/fallback and later rebakes menu catalog; source-only evidence is not pixel/performance acceptance.
