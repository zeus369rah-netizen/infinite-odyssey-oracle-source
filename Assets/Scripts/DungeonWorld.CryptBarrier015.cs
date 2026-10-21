using System;
using OdysseyOracle.Foundation.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class DungeonWorld
    {
        [BurstCompile]
        struct CryptBarrierJob015:IJob,ICryptBarrierPlacement015 {
            [ReadOnly]public NativeArray<CryptBarrierSolid32> Solids;public NativeArray<CryptBarrierPose32> Pose;public float4 Room;
            public int Count=>Solids.Length;public CryptBarrierSolid32 Solid(int i)=>Solids[i];
            public void Execute(){var data=this;RefugeCryptBarrierLayout015.Evaluate(ref data,Room.x,Room.y,Room.z,Room.w,out var value);Pose[0]=value;}
        }
        // Called only by the existing cold reset owner, before changing/allocating the retained wall.
        public void ResolveBreakableCryptPose015(BreakableVoxelWall retained,out Vector3 lower,out Quaternion rotation)
        {
            lower=new Vector3(9.55f,.05f,-4);rotation=Quaternion.Euler(0,-90,0);
            if(!IsTutorialRefuge)return; // Preserve original non-Refuge placement contract.
            if(transform.position.sqrMagnitude>1e-8f||Quaternion.Angle(transform.rotation,Quaternion.identity)>.001f||(transform.lossyScale-Vector3.one).sqrMagnitude>1e-8f)throw new InvalidOperationException("Crypt panel certification requires original stationary World frame.");
            var layout=RoomV2Layout;if(layout==null||layout.Rooms.Length<3||layout.PropPlacements.Length>32)throw new InvalidOperationException("Missing bounded actual refuge layout.");
            var room=layout.Rooms[2];if(Mathf.Abs(room.MinU-layout.HalfWidth)>.0001f)throw new InvalidOperationException("First east-alcove identity changed.");
            var colliders=GetComponentsInChildren<Collider>(true);if(colliders.Length>4096)throw new InvalidOperationException("Crypt panel solid inventory exceeded4096cap.");
            Collider self=retained!=null?retained.GetComponent<MeshCollider>():null;int count=layout.PropPlacements.Length;
            for(int i=0;i<colliders.Length;i++){var c=colliders[i];if(c!=self&&c.enabled&&c.gameObject.activeInHierarchy&&!c.isTrigger)count++;}
            if(count>RefugeCryptBarrierLayout015.MaximumSolids)throw new InvalidOperationException("Crypt panel total solid capacity exceeded.");
            NativeArray<CryptBarrierSolid32> solids=default;NativeArray<CryptBarrierPose32> poses=default;NativeArray<float4> rectangles=default;NativeArray<RefugeFloorQuery32> queries=default;NativeArray<ulong> occupancy=default;NativeArray<RefugeFloorResult32> floor=default;JobHandle fence=default;
            try{
                solids=new NativeArray<CryptBarrierSolid32>(count,Allocator.TempJob);int at=0;
                for(int i=0;i<colliders.Length;i++){var c=colliders[i];if(c==self||!c.enabled||!c.gameObject.activeInHierarchy||c.isTrigger)continue;Bounds b=c.bounds;solids[at++]=new CryptBarrierSolid32{MinX=b.min.x,MinY=b.min.y,MinZ=b.min.z,MaxX=b.max.x,MaxY=b.max.y,MaxZ=b.max.z,Flags=c.name=="RoomV2 continuous floor"?RefugeCryptBarrierLayout015.ActualContinuousFloor:0u};}
                for(int i=0;i<layout.PropPlacements.Length;i++){var b=RefugePlacementPolicy.At(layout.PropPlacements[i]);solids[at++]=new CryptBarrierSolid32{MinX=b.Min.X,MinY=b.Min.Y,MinZ=b.Min.Z,MaxX=b.Max.X,MaxY=b.Max.Y,MaxZ=b.Max.Z};}
                poses=new NativeArray<CryptBarrierPose32>(1,Allocator.TempJob);fence=new CryptBarrierJob015{Solids=solids,Pose=poses,Room=new float4(room.MinU,room.MinV,room.MaxU,room.MaxV)}.Schedule();fence.Complete();var pose=poses[0];
                if(pose.Status!=0||pose.SizeX!=2||pose.SizeY!=1.5f||pose.SizeZ!=.5f)throw new InvalidOperationException("Crypt panel certified reservation failed: "+pose.Status);
                rectangles=new NativeArray<float4>(layout.Rooms.Length,Allocator.TempJob);for(int i=0;i<rectangles.Length;i++){var r=layout.Rooms[i];rectangles[i]=new float4(r.MinU,r.MinV,r.MaxU,r.MaxV);}
                queries=new NativeArray<RefugeFloorQuery32>(1,Allocator.TempJob);queries[0]=new RefugeFloorQuery32{Origin=new float3(pose.X+1,2,pose.Z+.25f),MaximumDistance=2,HalfFootprintXZ=new float2(1,.25f),Flags=RefugeFloorSupportMath.CheckFootprintCorners,QueryId=0};
                fence=RefugeFloorSupportCold.Schedule(rectangles,queries,Allocator.TempJob,out _,out occupancy,out floor);fence.Complete();
                if(floor[0].Status!=1||floor[0].QueryId!=0||Mathf.Abs(floor[0].Position.y-pose.Y)>.0001f)throw new InvalidOperationException("Crypt panel actual native floor/corners unsupported.");
                lower=new Vector3(pose.X,pose.Y,pose.Z);rotation=Quaternion.Euler(0,pose.Yaw,0);
            }finally{fence.Complete();if(floor.IsCreated)floor.Dispose();if(occupancy.IsCreated)occupancy.Dispose();if(queries.IsCreated)queries.Dispose();if(rectangles.IsCreated)rectangles.Dispose();if(poses.IsCreated)poses.Dispose();if(solids.IsCreated)solids.Dispose();}
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Actual stationary unit World/Y-up collider and canonical prop AABBs; native downward floor/corner resultY0, first east south-edge panel pose.
// Array Formats: At most4096colliders+32prop Solid32 records, one Pose32/result and existing native Morton floor query buffers; cold bounded allocations only.
// Dependencies/Pointers: Excludes only exact retained wall collider for repeat reset. Completes/disposes all native readers before existing GameSession publishes; preserves BreakableVoxelWall damage/mesh/collider lifetime and no gameplay/room-node authority changes.
