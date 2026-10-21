// PSEUDOCODE: retain floor/shelter/Morton/count rules; share a numeric four-tetrahedron splash prototype between cold meshes and disjoint seed-job slices; the shader alone animates/fades the existing supported anchors.
using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace OdysseyOracle
{
    public static class PortalLandingWeatherRules020
    {
        public const int RainCapacity=64,SplashCapacity=16;
        public const float Span=32f,CellSize=8f,ColumnRadius=.42f,ColumnHeight=6.5f;
        public const double Period=4096d;
        public static double Advance(double clock,float delta,bool held)
        {
            if(double.IsNaN(clock)||double.IsInfinity(clock)||clock<0d||clock>=Period)clock=0d;
            if(held||float.IsNaN(delta)||float.IsInfinity(delta)||delta<=0f)return clock;
            clock+=Math.Min(.15d,delta);return clock>=Period?clock-Period:clock;
        }
        public static int RainCount(int quality,float factor)
        {
            int cap=quality<=0?16:quality==1?32:RainCapacity;
            if(float.IsNaN(factor)||float.IsInfinity(factor)||factor<=0)return 0;
            return Math.Min(cap,(int)Math.Ceiling(RainCapacity*Math.Min(1d,factor)));
        }
        public static uint Mix(uint value)
        {value^=value<<13;value^=value>>17;value^=value<<5;return value;}
        public static void Stratum(int index,out int x,out int z)
        {
            // Reverse six index bits, then decode an 8 by 8 Morton stratum grid.
            uint a=(uint)index,b=0;for(int bit=0;bit<6;bit++){b=(b<<1)|(a&1u);a>>=1;}
            x=(int)((b&1u)|((b>>1)&2u)|((b>>2)&4u));
            z=(int)(((b>>1)&1u)|((b>>2)&2u)|((b>>3)&4u));
        }
        public static bool TryAnchor(int index,int cellX,int cellZ,out float3 anchor)
        {
            anchor=new float3(0,-2048,0);
            if((uint)index>=RainCapacity||cellX<-17||cellX>16||cellZ<-17||cellZ>16)return false;
            Stratum(index,out int x,out int z);
            uint seed=Mix((uint)(index+1)^((uint)cellX*73856093u)^((uint)cellZ*19349663u));
            float jitterX=.2f+.6f*((seed&65535u)/65535f),jitterZ=.2f+.6f*((seed>>16)/65535f);
            float px=cellX*CellSize-Span*.5f+(x+jitterX)*(Span/8f);
            float pz=cellZ*CellSize-Span*.5f+(z+jitterZ)*(Span/8f);
            if(!PortalLandingQueries020.TryFloor(px,pz,16f,out float y))return false;
            // Splash/wind footprint remains on the same canonical support face.
            for(int side=0;side<4;side++)
            {
                float dx=side==0?ColumnRadius:side==1?-ColumnRadius:0;
                float dz=side==2?ColumnRadius:side==3?-ColumnRadius:0;
                if(!PortalLandingQueries020.TryFloor(px+dx,pz+dz,16f,out float other)||Math.Abs(y-other)>.025f)return false;
            }
            var feet=new LandingPoint020(px,y+.025f,pz);
            if(!PortalLandingQueries020.ClearSegment(feet,new LandingPoint020(px,y+ColumnHeight,pz),ColumnRadius,0,0))return false;
            anchor=new float3(px,y+.025f,pz);return true;
        }
    }

    public struct LandingWeatherVertex020
    {
        public float3 Position;
        public float2 Uv;
        public float4 Anchor;
    }

    // One cold prototype shared by native instances and the combined hardware fallback.
    // Each splash is four closed tetrahedral droplets, never a floor ring or residue mesh.
    public static class PortalLandingWeatherGeometry020
    {
        public const int RainVertices=4,RainIndices=6,SplashVertices=16,SplashIndices=48;
        public static int VertexCount(bool splash)=>splash?SplashVertices:RainVertices;
        public static int IndexCount(bool splash)=>splash?SplashIndices:RainIndices;
        public static LandingWeatherVertex020 Vertex(bool splash,int index,float4 anchor)
        {
            if(!splash)
            {
                float2 uv=new float2(index==0||index==3?0:1,index<2?0:1);
                return new LandingWeatherVertex020{Position=new float3(uv-.5f,0),Uv=uv,Anchor=anchor};
            }
            int lobe=index>>2,point=index&3;
            float2 direction=lobe==0?new float2(1,0):lobe==1?new float2(0,1):lobe==2?new float2(-1,0):new float2(0,-1);
            float3 position=point==0?new float3(-.75f,-.35f,-.55f):point==1?new float3(.75f,-.35f,-.55f):point==2?new float3(0,-.35f,.8f):new float3(0,.9f,0);
            return new LandingWeatherVertex020{Position=position,Uv=direction,Anchor=anchor};
        }
        public static ushort Index(bool splash,int index,int firstVertex)
        {
            if(!splash){int corner=index==0||index==3?0:index==1?1:index==2||index==4?2:3;return (ushort)(firstVertex+corner);}
            int lobe=index/12,point=index%12;
            int localCorner=point==0||point==3||point==11?0:point==1||point==5||point==6?1:point==2||point==8||point==9?2:3;
            return (ushort)(firstVertex+lobe*4+localCorner);
        }
    }

    [BurstCompile]
    public struct PortalLandingWeatherSeedJob020:IJobParallelFor
    {
        public int CellX,CellZ;
        [WriteOnly] public NativeArray<float4x4> Matrices;
        [WriteOnly] public NativeArray<byte> Valid;
        [WriteOnly,NativeDisableParallelForRestriction] public NativeArray<LandingWeatherVertex020> RainVertices;
        [WriteOnly,NativeDisableParallelForRestriction] public NativeArray<LandingWeatherVertex020> SplashVertices;
        public void Execute(int index)
        {
            bool supported=PortalLandingWeatherRules020.TryAnchor(index,CellX,CellZ,out float3 p);
            Valid[index]=supported?(byte)1:(byte)0;
            Matrices[index]=float4x4.Translate(p);
            var anchor=new float4(p,supported?index+1:-1);
            for(int corner=0;corner<PortalLandingWeatherGeometry020.RainVertices;corner++)
                RainVertices[index*PortalLandingWeatherGeometry020.RainVertices+corner]=PortalLandingWeatherGeometry020.Vertex(false,corner,anchor);
            if(index<PortalLandingWeatherRules020.SplashCapacity)
                for(int vertex=0;vertex<PortalLandingWeatherGeometry020.SplashVertices;vertex++)
                    SplashVertices[index*PortalLandingWeatherGeometry020.SplashVertices+vertex]=PortalLandingWeatherGeometry020.Vertex(true,vertex,anchor);
        }
    }

    [BurstCompile]
    public struct PortalLandingWeatherCountJob020:IJob
    {
        public int RainCount,SplashCount;
        [ReadOnly] public NativeArray<byte> Valid;
        [WriteOnly] public NativeArray<int> Counts;
        public void Execute()
        {
            int rain=0,splash=0;
            for(int i=0;i<PortalLandingWeatherRules020.RainCapacity;i++)
            {if(i<RainCount)rain+=Valid[i];if(i<SplashCount)splash+=Valid[i];}
            Counts[0]=rain;Counts[1]=splash;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Stationary admitted hub world metres/Y-up;8m cells and deterministic Morton-stratified32m local seed region, shader-clipped to12m in front of camera; actual builder floor/closed obstacles.
// Array Formats:64float4x4,64byte validity,256 rain+256 splash Vertex36,2int counts; parallel writers occupy disjoint index*4 / index*16 slices. Four closed tetrahedra/16triangles per splash; all buffers borrowed by jobs.
// Dependencies/Pointers: Main-thread owner validates lengths/cells, completes the seed->count writer chain before read/upload/disposal, and rejects generation changes. No Object/physics/random/allocation or travel authority in jobs.
