using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace OdysseyOracle.Foundation.Presentation
{
    public enum CompassDestination { None,MovementArea,Shrine,Raykon,Smith,TowerPractice,TowerDemonstration,MonsterPractice,Portal,Guardian,NovaTarget,ReturnPortal,DefendShrine,Anvil }
    [StructLayout(LayoutKind.Sequential)]
    public struct ObjectiveCompass32
    {
        public float CameraHeading,TargetHeading,RelativeHeading,Metres;
        public float2 Waypoint;
        public int Flags,Destination;
    }
    public static class ObjectiveCompassMath
    {
        public const int Valid=1,HasBearing=2,ViaEntry=4,ViaExit=8;
        public static float Normalize(float degrees)
        {if(!math.isfinite(degrees))return 0;float value=degrees%360f;return value<0?value+360f:value;}
        public static float Delta(float from,float to)
        {float d=Normalize(Normalize(to)-Normalize(from));return d>180f?d-360f:d;}
        public static float Step(float from,float to,float maximumStep)
        {float limit=math.isfinite(maximumStep)?math.max(0,maximumStep):0;return Normalize(Normalize(from)+math.clamp(Delta(from,to),-limit,limit));}
        public static float Heading(float2 forward,float fallback)
        {
            if(!math.all(math.isfinite(forward))||math.lengthsq(forward)<1e-10f)return math.isfinite(fallback)?Normalize(fallback):0;
            return Normalize(math.degrees(math.atan2(forward.x,forward.y)));
        }
        public static float2 CardinalOffset(int cardinal,float cameraHeading,float radius)
        {float radians=math.radians(cardinal*90f-cameraHeading);return new float2(math.sin(radians),math.cos(radians))*radius;}
        public static ObjectiveCompass32 Bearing(float2 player,float2 target,float2 forward,float fallback,int destination,int flags=0)
        {
            var value=new ObjectiveCompass32{CameraHeading=Heading(forward,fallback),Destination=destination,Waypoint=target};
            if(destination<=0||!math.all(math.isfinite(player))||!math.all(math.isfinite(target)))return value;
            float2 delta=target-player;float squared=math.lengthsq(delta);
            if(!math.isfinite(squared))return value;
            value.Metres=math.sqrt(squared);value.Flags=Valid|flags;
            if(squared<.25f)return value;
            value.TargetHeading=Heading(delta,0);value.RelativeHeading=Delta(value.CameraHeading,value.TargetHeading);value.Flags|=HasBearing;
            return value;
        }
        public static bool Contains(float4 room,float2 point)=>math.all(point>=room.xy)&&math.all(point<=room.zw);
        public static float2 Doorway(float4 main,float4 side,float z)
        {
            float x=side.z<=main.x?main.x+1f:main.z-1f;
            float half=math.min(.65f,(side.w-side.y)*.25f);
            return new float2(x,math.clamp(z,side.y+half,side.w-half));
        }
    }
    [BurstCompile]
    public struct ObjectiveCompassJob:IJob
    {
        [ReadOnly] public NativeArray<float4> Rooms;
        public int RoomCount,Destination;
        public float2 Player,Target,Forward;
        public float PreviousHeading,DisplayedHeading;
        [WriteOnly] public NativeArray<ObjectiveCompass32> Result;
        [WriteOnly] public NativeArray<float2> CardinalOffsets;
        public void Execute()
        {
            float2 waypoint=Target;int flags=0,count=math.min(32,math.min(RoomCount,Rooms.Length));
            if(count>1&&Destination>0)
            {
                int own=-1,goal=-1;float ownArea=float.PositiveInfinity,goalArea=float.PositiveInfinity;
                for(int i=1;i<count;i++)
                {
                    float4 room=Rooms[i];float area=(room.z-room.x)*(room.w-room.y);
                    if(!math.all(math.isfinite(room))||math.any(room.zw<=room.xy)||area<=0)continue;
                    if(ObjectiveCompassMath.Contains(room,Player)&&area<ownArea){own=i;ownArea=area;}
                    if(ObjectiveCompassMath.Contains(room,Target)&&area<goalArea){goal=i;goalArea=area;}
                }
                if(own>=0&&own!=goal)
                {waypoint=ObjectiveCompassMath.Doorway(Rooms[0],Rooms[own],Player.y);flags=ObjectiveCompassMath.ViaExit;}
                else if(goal>=0&&own!=goal)
                {
                    float2 door=ObjectiveCompassMath.Doorway(Rooms[0],Rooms[goal],Target.y);
                    if(math.distancesq(Player,door)>1f){waypoint=door;flags=ObjectiveCompassMath.ViaEntry;}
                }
            }
            if(Result.Length>0)Result[0]=ObjectiveCompassMath.Bearing(Player,waypoint,Forward,PreviousHeading,Destination,flags);
            for(int i=0;i<math.min(4,CardinalOffsets.Length);i++)CardinalOffsets[i]=ObjectiveCompassMath.CardinalOffset(i,DisplayedHeading,35f);
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: actual horizontal WorldXZ/+Znorth/+Xeast; atan2 headings normalized[0,360), signed relative bearing; shared-room doorway waypoints only.
// Array Formats: sequential32B result[1], immutablefloat4 rectangles[32] maximum, cardinalfloat2[4], scalar input copies; Burst job owns no buffers/Unity objects.
// Dependencies/Pointers: caller supplies true target and current room metadata, completes all writers/readers before disposal. Doorway guidance is not obstacle/LOS/A*proof. Positive180tie is deterministic; no hot strings/Physics/heap.
