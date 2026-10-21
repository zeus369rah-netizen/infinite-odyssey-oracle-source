using System;
using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace OdysseyOracle
{
    [StructLayout(LayoutKind.Sequential,Pack=4)]
    public struct TowerElementRecipe64
    {
        public float4 ColorRadius; // RGB original element, orbit amplitude.
        public float4 Shape; // height, cycles/sec, opacity, neutral model brightness.
        public uint4 Detail; // streams/bands/lobes, drops/bits/rocks/bubbles, embers, smoke.
        public uint Type,Tier,Reserved0,Reserved1;
    }
    [StructLayout(LayoutKind.Sequential,Pack=4)]
    public struct TowerElementVertex32 {public float3 Position;public float2 UV;public float3 Tag;}
    [StructLayout(LayoutKind.Sequential,Pack=4)]
    public struct TowerElementClock32
    {
        public float Clock,Age,Travel,Duration;
        public uint Serial,Type,Tier,Alive;
    }
    [StructLayout(LayoutKind.Sequential,Pack=4)]
    public struct TowerElementPose64
    {
        public float4 Time; // clock,age,travel,pulse.
        public float4 Tint;
        public float4 Minimum,Maximum;
    }
    public static class TowerElementRecipes
    {
        public const int Types=4,Tiers=3,Variants=12,MaximumVertices=512,MaximumIndices=1024;
        public const float MaximumAmbientRadius=.95f,MinimumAmbientY=-.1f,MaximumAmbientY=2.3f;
        public static bool Valid(int type,int tier)=>type>=0&&type<Types&&tier>=1&&tier<=Tiers;
        public static TowerElementRecipe64 Get(int type,int tier)
        {
            if(!Valid(type,tier))throw new ArgumentOutOfRangeException();
            float3 color=type==0?new float3(1,.36f,.12f):type==1?new float3(.18f,.68f,1):type==2?new float3(.62f,.78f,.29f):new float3(.66f,.91f,1);
            uint a=type==0?(tier==1?2u:tier==2?3u:5u):type==1?(uint)(tier+1):type==2?0u:(uint)tier;
            uint b=type==0?(uint)(tier*2):type==1?(uint)(tier+1):type==2?(uint)(tier*2):(tier==1?2u:tier==2?5u:8u);
            return new TowerElementRecipe64{ColorRadius=new float4(color,tier==1?.40f:tier==2?.58f:.72f),Shape=new float4(1.8f,.16f+tier*.025f,type==0?.60f:type==1?.55f:type==2?1:.20f,tier==1?.82f:tier==2?.96f:1.06f),Detail=new uint4(a,b,type==0?(tier==1?3u:tier==2?5u:8u):0u,type==0?(tier==1?1u:tier==2?2u:4u):0u),Type=(uint)type,Tier=(uint)tier};
        }
        public static float FlightSeconds(float distance)=>math.clamp(distance/28f,.08f,.36f);
        public static bool Finite(float value)=>math.isfinite(value);
        public static bool Point(float3 point)=>math.all(math.isfinite(point))&&math.all(math.abs(point)<2000);
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Head-local +Z/Y-up effects; fixed .95m radial/- .1..2.3m vertical max envelope across all12 variants, unrelated to damage area.
// Array Formats: Recipe64,Vertex32,Clock32,Pose64 blittable records;12 descriptors, fixed512/1024 cold geometry caps and one96-byte state/pose pair per instance.
// Dependencies/Pointers: Existing three host-accepted tiers/types and saved templates remain canonical. Recipes change no damage/range/cadence/unlock/economy; actual base4 area radius remains0.
