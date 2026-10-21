using System;
using OdysseyOracle.Foundation.World;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace OdysseyOracle
{
    public static class SmithFurnacePlacementCold015
    {
        public static SmithFurnacePose32 Resolve(DungeonWorld world,Transform smith)
        {
            if(world==null||smith==null||!world.IsTutorialRefuge||world.IsCoopArena||world.RoomV2Layout==null)throw new InvalidOperationException("Original workshop requires the supported refuge.");
            if(world.transform.position.sqrMagnitude>1e-8f||Quaternion.Angle(world.transform.rotation,Quaternion.identity)>.001f||(world.transform.lossyScale-Vector3.one).sqrMagnitude>1e-8f)throw new InvalidOperationException("Furnace floor proof requires original stationary World frame.");
            var layout=world.RoomV2Layout;NativeArray<float4> rooms=default,propFrames=default;NativeArray<SmithFurnaceBounds32> props=default;NativeArray<float> piers=default;
            NativeArray<SmithFurnacePose32> pose=default;NativeArray<int> result=default;NativeArray<RefugeFloorQuery32> queries=default;
            NativeArray<ulong> occupancy=default;NativeArray<RefugeFloorResult32> support=default;JobHandle fence=default;
            try{
                rooms=new NativeArray<float4>(layout.Rooms.Length,Allocator.TempJob);props=new NativeArray<SmithFurnaceBounds32>(layout.PropPlacements.Length,Allocator.TempJob);propFrames=new NativeArray<float4>(layout.PropPlacements.Length,Allocator.TempJob);piers=new NativeArray<float>(layout.PierRows.Length,Allocator.TempJob);
                for(int i=0;i<rooms.Length;i++){var r=layout.Rooms[i];rooms[i]=new float4(r.MinU,r.MinV,r.MaxU,r.MaxV);}
                // Copy intrinsic prototype bounds/frames only. The job performs
                // all corner rotation/mount offset and spatial clearance math.
                for(int i=0;i<props.Length;i++){var placement=layout.PropPlacements[i];var e=RefugePlacementPolicy.Expected(placement.Id);bool mount=!string.IsNullOrEmpty(placement.AlignAnchor);if(mount&&(placement.Id!=TutorialRoomV2Layout.PropId.WallLantern||placement.AlignAnchor!="Anchor_Mount"))throw new InvalidOperationException("Unknown original prop mount.");props[i]=new SmithFurnaceBounds32{MinX=e.Min.X,MinY=e.Min.Y,MinZ=e.Min.Z,MaxX=e.Max.X,MaxY=e.Max.Y,MaxZ=e.Max.Z,Reserved0=mount?1u:0u};propFrames[i]=new float4(placement.Position.X,placement.Position.Y,placement.Position.Z,placement.Yaw);}
                for(int i=0;i<piers.Length;i++)piers[i]=layout.PierRows[i];
                pose=new NativeArray<SmithFurnacePose32>(1,Allocator.TempJob);result=new NativeArray<int>(1,Allocator.TempJob);
                Vector3 p=smith.position;
                fence=new SmithFurnaceLayoutJob015{Rooms=rooms,Props=props,PropFrames=propFrames,Piers=piers,Pose=pose,Result=result,X=p.x,Y=p.y,Z=p.z,Yaw=Quaternion.Angle(smith.rotation,Quaternion.identity),UnitScale=(smith.lossyScale-Vector3.one).sqrMagnitude<=1e-8f?1:0,HalfWidth=layout.HalfWidth,Ceiling=layout.CeilingY}.Schedule();fence.Complete();
                if(result[0]!=0)throw new InvalidOperationException("Original enlarged furnace failed exact workshop clearance: "+result[0]);
                SmithFurnacePose32 found=pose[0];queries=new NativeArray<RefugeFloorQuery32>(1,Allocator.TempJob);
                queries[0]=new RefugeFloorQuery32{Origin=new float3(found.X,2f,found.Z),MaximumDistance=2f,HalfFootprintXZ=new float2(found.HalfX,found.HalfZ),Flags=RefugeFloorSupportMath.CheckFootprintCorners,QueryId=0};
                fence=RefugeFloorSupportCold.Schedule(rooms,queries,Allocator.TempJob,out _,out occupancy,out support);fence.Complete();
                if(support[0].Status!=1||support[0].QueryId!=0)throw new InvalidOperationException("Original enlarged furnace floor/corners unsupported.");
                found.Y=support[0].Position.y;return found;
            }finally{
                fence.Complete();if(support.IsCreated)support.Dispose();if(occupancy.IsCreated)occupancy.Dispose();if(queries.IsCreated)queries.Dispose();if(result.IsCreated)result.Dispose();if(pose.IsCreated)pose.Dispose();if(piers.IsCreated)piers.Dispose();if(propFrames.IsCreated)propFrames.Dispose();if(props.IsCreated)props.Dispose();if(rooms.IsCreated)rooms.Dispose();
            }
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Actual immutable stationary World and Smith poses; numeric room layout then downward solid-floor support produces exact bottomY0.
// Array Formats: Bounded TempJob room/local-prop/prop-frame/pier inputs, one32Bpose, one32Bfloor query/result and reused quarter-metre Morton occupancy; no main-thread corner rotation.
// Dependencies/Pointers: Existing saved-prop envelope adapter, Burst placement and floor jobs; each fence completed before read/publication/disposal in finally, no active-frame calls.
