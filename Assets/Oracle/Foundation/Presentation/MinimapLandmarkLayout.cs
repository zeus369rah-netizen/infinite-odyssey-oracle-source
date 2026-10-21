using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace OdysseyOracle.Foundation.Presentation
{
    [StructLayout(LayoutKind.Sequential)]
    public struct MinimapLandmark32
    {
        public float2 Anchor,Badge;
        public float LeaderLength,LeaderDegrees;
        public int Valid,RoomIndex;
    }
    public static class MinimapLandmarkMath
    {
        public static float Scale(float4 bounds)=>math.min(182f/math.max(1f,bounds.z-bounds.x),186f/math.max(1f,bounds.w-bounds.y));
        public static float2 Project(float2 xz,float4 bounds)
        {
            float scale=Scale(bounds);float2 extent=bounds.zw-bounds.xy;
            return (new float2(182,186)-extent*scale)*.5f+(xz-bounds.xy)*scale;
        }
        public static MinimapLandmark32 Layout(float4 input,float4 bounds,float4 room,int roomIndex)
        {
            var output=new MinimapLandmark32{RoomIndex=-1};
            if(input.w!=1f||!math.all(math.isfinite(input))||!math.all(math.isfinite(bounds))||
                !math.all(math.isfinite(room))||math.any(bounds.zw<=bounds.xy)||math.any(room.zw<=room.xy)||
                math.any(input.xz<bounds.xy)||math.any(input.xz>bounds.zw))return output;
            float2 anchor=Project(input.xz,bounds),low=Project(room.xy,bounds),high=Project(room.zw,bounds);
            if(!math.all(math.isfinite(anchor))||!math.all(math.isfinite(low))||!math.all(math.isfinite(high))||
                math.any(input.xz<room.xy)||math.any(input.xz>room.zw)||math.any(high-low<16f))return output;
            float2 badge=math.clamp(anchor,low+8f,high-8f),delta=anchor-badge;
            output.Anchor=anchor;output.Badge=badge;output.LeaderLength=math.length(delta);
            output.LeaderDegrees=math.degrees(math.atan2(delta.y,delta.x));output.Valid=1;output.RoomIndex=roomIndex;
            return output;
        }
    }
    [BurstCompile]
    public struct MinimapLandmarkLayoutJob:IJob
    {
        [ReadOnly] public NativeArray<float4> Anchors;
        [ReadOnly] public NativeArray<float4> Rooms;
        public float4 Bounds;
        [WriteOnly] public NativeArray<MinimapLandmark32> Results;
        public void Execute()
        {
            for(int station=0;station<Anchors.Length;station++)
            {
                float4 anchor=Anchors[station];int selected=-1;float bestArea=float.PositiveInfinity;
                for(int roomIndex=0;roomIndex<Rooms.Length;roomIndex++)
                {
                    float4 room=Rooms[roomIndex];float2 size=room.zw-room.xy;
                    if(!math.all(math.isfinite(room))||math.any(size<=0)||math.any(anchor.xz<room.xy)||math.any(anchor.xz>room.zw))continue;
                    float area=size.x*size.y;if(area<bestArea){bestArea=area;selected=roomIndex;}
                }
                Results[station]=selected<0?new MinimapLandmark32{RoomIndex=-1}:
                    MinimapLandmarkMath.Layout(anchor,Bounds,Rooms[selected],selected);
            }
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: World XZ/Y-up metres -> existing north-up182x186 minimap reference pixels; exact anchor and inset badge remain separate.
// Array Formats: float4 anchor[4], rectangle[1..32], result32[4]; sequential32B payload; bounded cold Burst job only.
// Dependencies/Pointers: caller validates capacities, completes all input writers/readers before disposal, and retains sole map/world authority. No UnityEngine.Object access or native owner inside math/job.
