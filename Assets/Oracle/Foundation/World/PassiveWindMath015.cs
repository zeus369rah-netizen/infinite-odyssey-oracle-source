using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace OdysseyOracle.Foundation.World
{
    // # ARCHITECTURE SNAPSHOT
    // Coordinate Space: numeric world-XZ velocity, seconds and anchored cosmetic offsets; window mesh stays refuge local.
    // Array Formats: one float4 descriptor, fixed3 window inputs and at most144 Vertex32/216 indices; no engine objects.
    // Dependencies/Pointers: scalar field rules and a cold isolated job only. No gameplay/weather authority, random stream or persistent native ownership.
    public static class PassiveWindMath015
    {
        public const float Period=256f,MaximumDelta=.05f;
        public static double Advance(double seconds,float delta,bool held)
        {
            if(!math.isfinite(seconds))seconds=0;
            if(held||!math.isfinite(delta))return seconds;
            seconds+=math.clamp(delta,0,MaximumDelta);return seconds>=Period?seconds-Period:seconds;
        }
        public static float4 Sample(float seconds,float2 direction,float strength,bool enabled)
        {
            if(!math.isfinite(seconds)||!math.all(math.isfinite(direction))||!math.isfinite(strength))return default;
            float squared=math.lengthsq(direction);direction=squared>1e-6f?direction*math.rsqrt(squared):math.normalizesafe(new float2(-.82f,.57f));
            float theta=seconds*(2*math.PI/Period),turn=.07f*math.sin(theta),s=math.sin(turn),c=math.cos(turn);
            direction=new float2(direction.x*c-direction.y*s,direction.x*s+direction.y*c);
            float speed=.78f+.12f*math.sin(3*theta+.4f),pulse=math.max(0,math.sin(5*theta-.6f));pulse*=pulse;pulse*=pulse;pulse*=pulse;
            float gain=enabled?math.saturate(strength)*(.48f+.52f*pulse):0;
            return new float4(direction*speed,seconds,gain);
        }
        public static float2 Offset(float2 velocity,float seconds,float gain,float phase,float maximum)
        {
            float squared=math.lengthsq(velocity);if(squared<=1e-8f||!math.all(math.isfinite(velocity))||!math.isfinite(seconds)||!math.isfinite(gain)||!math.isfinite(phase)||!math.isfinite(maximum))return default;
            float speed=math.sqrt(squared),theta=seconds*(2*math.PI/Period);
            float wave=.72f+.20f*math.sin(23*theta+phase)+.08f*math.sin(11*theta+phase*1.7f);
            return velocity/speed*(wave*math.saturate(gain)*math.saturate(speed)*math.clamp(maximum,0,.72f));
        }
    }
    [StructLayout(LayoutKind.Sequential,Pack=4)]
    public struct PassiveWindVertex32
    {public float3 Position;public float2 Corner;public float3 Seed;}
    [BurstCompile]
    public struct PassiveWindWindowMesh015:IJob
    {
        [ReadOnly]public NativeArray<float3> Anchors;
        public NativeArray<float3> WorldAnchors;
        public NativeArray<PassiveWindVertex32> Vertices;
        public NativeArray<ushort> Indices;
        public float4x4 LocalToWorld;
        public void Execute()
        {
            if(Anchors.Length!=3||WorldAnchors.Length!=3||Vertices.Length!=144||Indices.Length!=216)return;
            for(int window=0;window<3;window++)
            {
                WorldAnchors[window]=math.transform(LocalToWorld,Anchors[window]);
                for(int particle=0;particle<12;particle++)
                {
                    int at=(window*12+particle)*4,first=(window*12+particle)*6;
                    float3 anchor=Anchors[window]+new float3(.36f,((particle/3)%4-1.5f)*.05f,(particle%3-1)*.6f);
                    float3 seed=new float3((particle+.37f)/12,(window*12+particle)*2.39996323f,.9f+((window*7+particle)%5)*.03f);
                    for(int corner=0;corner<4;corner++)Vertices[at+corner]=new PassiveWindVertex32{Position=anchor,Corner=new float2(corner==0||corner==3?-1:1,corner<2?-1:1),Seed=seed};
                    Indices[first]=(ushort)at;Indices[first+1]=(ushort)(at+1);Indices[first+2]=(ushort)(at+2);Indices[first+3]=(ushort)at;Indices[first+4]=(ushort)(at+2);Indices[first+5]=(ushort)(at+3);
                }
            }
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: world-XZ cosmetic harmonics, root-weight-free amplitude and source-backed refuge-local trail anchors; no force/visibility authority.
// Array Formats: fixed float4 field; cold3anchors/144 Vertex32/216ushort outputs in contiguous temporary native arrays, completed before Mesh access.
// Dependencies/Pointers: caller owns/disposes buffers. Scalar CPU/vertex fallback is explicit; only the cold window loop is Burst-scheduled. No Object API, physics, compute, settings or random state inside numeric code.
