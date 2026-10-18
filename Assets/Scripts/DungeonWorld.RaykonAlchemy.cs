using System;
using OdysseyOracle.Foundation.World;
using OdysseyOracle.VisualDressing;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class DungeonWorld
    {
        private GameObject refugeAlchemyTable;
        public bool RefugeAlchemyTableReady => refugeAlchemyTable != null && refugeAlchemyTable.activeInHierarchy;
        private void BuildRefugeAlchemyTable(Vector3 raykonFeet)
        {
            if (refugeAlchemyTable != null) return;
            Vector3 desired = raykonFeet + new Vector3(-.15f,0,1.55f);
            float3 low=RaykonAlchemyTableRecipe.Minimum,high=RaykonAlchemyTableRecipe.Maximum;
            Vector3 min = desired + new Vector3(low.x,low.y,low.z), max = desired + new Vector3(high.x,high.y,high.z);
            bool inside = false;
            foreach (TutorialRoomV2Layout.R2 room in tutorialRefuge.Rooms)
                if (min.x-.2f >= room.MinU && max.x+.2f <= room.MaxU && min.z-.2f >= room.MinV && max.z+.2f <= room.MaxV) { inside=true;break; }
            if (!inside) throw new InvalidOperationException("Alchemy table footprint lacks a complete room rectangle/wall clearance.");
            foreach (TutorialRoomV2Layout.Placement placement in tutorialRefuge.PropPlacements)
            {
                var p = RefugePlacementPolicy.At(placement);
                if (min.x < p.Max.X && max.x > p.Min.X && min.z < p.Max.Z && max.z > p.Min.Z && min.y < p.Max.Y && max.y > p.Min.Y)
                    throw new InvalidOperationException("Alchemy table overlaps saved prop: "+placement.Id);
            }
            NativeArray<float4> rectangles=default;NativeArray<RefugeFloorQuery32> queries=default;
            NativeArray<ulong> occupancy=default;NativeArray<RefugeFloorResult32> results=default;JobHandle fence=default;
            Vector3 supported;
            try
            {
                rectangles=new NativeArray<float4>(tutorialRefuge.Rooms.Length,Allocator.TempJob);
                for(int i=0;i<rectangles.Length;++i){var r=tutorialRefuge.Rooms[i];rectangles[i]=new float4(r.MinU,r.MinV,r.MaxU,r.MaxV);}
                queries=new NativeArray<RefugeFloorQuery32>(1,Allocator.TempJob);
                queries[0]=new RefugeFloorQuery32{Origin=new float3(desired.x,2,desired.z),MaximumDistance=4,
                    HalfFootprintXZ=new float2(high.x,high.z),Flags=RefugeFloorSupportMath.CheckFootprintCorners,QueryId=0};
                fence=RefugeFloorSupportCold.Schedule(rectangles,queries,Allocator.TempJob,out _,out occupancy,out results);fence.Complete();
                if(results[0].Status!=1u||results[0].QueryId!=0u||results[0].Position.y!=0f)throw new InvalidOperationException("Alchemy table has no exact floor support.");
                supported=new Vector3(results[0].Position.x,results[0].Position.y,results[0].Position.z);
            }
            finally
            {fence.Complete();if(results.IsCreated)results.Dispose();if(occupancy.IsCreated)occupancy.Dispose();if(queries.IsCreated)queries.Dispose();if(rectangles.IsCreated)rectangles.Dispose();}
            Mesh mesh=RaykonAlchemyTableBuilder.Build();resources.Add(mesh);
            var owner=new GameObject("Original Raykon six-potion three-crystal table",typeof(MeshFilter),typeof(MeshRenderer));
            owner.transform.SetParent(geometryRoot,false);owner.transform.localPosition=supported;
            owner.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=owner.GetComponent<MeshRenderer>();renderer.sharedMaterial=refugeRaykonMaterial;
            // Exact four legs and tabletop only. Small bottles/crystals are visual dressing.
            for(int part=0;part<5;++part)
            {
                AlchemyBox32 box=RaykonAlchemyTableRecipe.Box(part);float3 a=(float3)box.MinimumMillimetres*.001f,b=(float3)box.MaximumMillimetres*.001f;
                var solid=owner.AddComponent<UnityEngine.BoxCollider>();solid.center=new Vector3((a.x+b.x)*.5f,(a.y+b.y)*.5f,(a.z+b.z)*.5f);
                solid.size=new Vector3(b.x-a.x,b.y-a.y,b.z-a.z);solid.isTrigger=false;
            }
            refugeAlchemyTable=owner;
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing refuge local/common Y-up metres; siteRaykonFeet+(-.15,0,1.55),
// exact floorY0 from existing Morton support job; footprint1.5x.7m plus.2m wall-clearance proof.
// Array Formats: One cold mesh702vertices/1026indices,5colliders on1owned object; TempJob
// rectangle float4[7],query32/result32[1],1024ulong occupancy completed/disposed before binding.
// Dependencies/Pointers: Existing World alone owns mesh/material/geometry-root lifetime; borrow
// existing Raykon material and saved layout. No lights/UI/ActivityDirector/NPCrole/save changes.
