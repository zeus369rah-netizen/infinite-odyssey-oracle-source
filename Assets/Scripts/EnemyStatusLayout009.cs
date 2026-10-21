using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace OdysseyOracle
{
    public static class EnemyLiveStatusRules009
    {
        public const uint Burning=1u,Slowed=2u,Staggered=4u,EarthExposed=8u,Weakened=16u,KnownMask=31u;
        public static bool Valid(uint mask)=>(mask&~KnownMask)==0;
        public static uint Derive(float burn,float slow,float stagger,float exposed,float weaken)
        {return(burn>0?Burning:0u)|(slow>0?Slowed:0u)|(stagger>0?Staggered:0u)|(stagger>0&&exposed>0?EarthExposed:0u)|(weaken>0?Weakened:0u);}
    }

    [StructLayout(LayoutKind.Sequential,Pack=4)]
    internal struct EnemyStatusHandle009 {internal int Index;internal uint Generation;}
    [StructLayout(LayoutKind.Sequential,Pack=4)]
    internal struct EnemyStatusInput009
    {
        internal float3 Anchor;
        internal uint LiveStatus;
        internal int Theme;
        internal uint Generation,Active,Reserved;
    }
    [StructLayout(LayoutKind.Sequential,Pack=4)]
    internal struct EnemyStatusBounds009 {internal float3 Min,Max;}
    internal static class EnemyStatusMath009
    {
        internal const int Capacity=48,Kinds=9;
        internal static float Width(int kind)=>kind>=7?2.8f:1f;
        internal static bool Selected(int kind,int theme,uint mask)=>kind<4?theme==kind:(mask&(1u<<(kind-4)))!=0;
        internal static float Pixels(float distance)=>math.lerp(26f,14f,math.saturate((distance-3f)/27f));
        internal static bool Layout(float3 anchor,float3 eye,float3 forward,float tanHalfFov,float pixelHeight,float near,float far,
            out float size)
        {
            size=0;
            if(!math.all(math.isfinite(anchor))||!math.all(math.isfinite(eye))||!math.all(math.isfinite(forward))||
                !math.isfinite(tanHalfFov)||tanHalfFov<=0||tanHalfFov>10||!math.isfinite(pixelHeight)||pixelHeight<1||
                !math.isfinite(near)||near<0||!math.isfinite(far)||far<=near||math.abs(math.lengthsq(forward)-1f)>.001f)return false;
            float3 offset=anchor-eye;float depth=math.dot(offset,forward);float squared=math.lengthsq(offset);
            if(!math.isfinite(depth)||!math.isfinite(squared)||depth<=near+.05f||depth>=far||squared>36f*36f)return false;
            size=math.min(.85f,Pixels(math.sqrt(squared))*2f*depth*tanHalfFov/pixelHeight);
            return math.isfinite(size)&&size>0;
        }
    }
    [BurstCompile]
    internal struct EnemyStatusLayoutJob009:IJob
    {
        [ReadOnly] internal NativeArray<EnemyStatusInput009> Inputs;
        internal float3 Eye,Right,Up,Forward;
        internal float TanHalfFov,PixelHeight,Near,Far;
        [WriteOnly] internal NativeArray<float4x4> Matrices;
        internal NativeArray<int> Counts;
        internal NativeArray<EnemyStatusBounds009> Bounds;
        public void Execute()
        {
            for(int k=0;k<9;k++){Counts[k]=0;Bounds[k]=new EnemyStatusBounds009{Min=new float3(float.PositiveInfinity),Max=new float3(float.NegativeInfinity)};}
            if(!math.all(math.isfinite(Right))||!math.all(math.isfinite(Up))||math.abs(math.lengthsq(Right)-1f)>.001f||
                math.abs(math.lengthsq(Up)-1f)>.001f||math.abs(math.dot(Right,Up))>.001f||
                math.abs(math.dot(Right,Forward))>.001f||math.abs(math.dot(Up,Forward))>.001f)return;
            for(int i=0;i<48;i++)
            {
                EnemyStatusInput009 input=Inputs[i];
                if(input.Active!=1||input.Generation==0||input.Reserved!=0||!EnemyLiveStatusRules009.Valid(input.LiveStatus)||input.Theme< -1||input.Theme>3||
                    !EnemyStatusMath009.Layout(input.Anchor,Eye,Forward,TanHalfFov,PixelHeight,Near,Far,out float size))continue;
                float width=0;int glyphs=0;
                for(int k=0;k<9;k++)if(EnemyStatusMath009.Selected(k,input.Theme,input.LiveStatus)){width+=EnemyStatusMath009.Width(k);glyphs++;}
                if(glyphs==0)continue;
                width+=(glyphs-1)*.18f;float cursor=-width*.5f;
                for(int k=0;k<9;k++)
                {
                    if(!EnemyStatusMath009.Selected(k,input.Theme,input.LiveStatus))continue;
                    float w=EnemyStatusMath009.Width(k);float3 center=input.Anchor+Up*(.13f+size*.5f)+Right*((cursor+w*.5f)*size);cursor+=w+.18f;
                    int at=Counts[k];Matrices[k*48+at]=new float4x4(new float4(Right*size,0),new float4(Up*size,0),new float4(-Forward*size,0),new float4(center,1));Counts[k]=at+1;
                    float3 ext=math.abs(Right)*(size*w*.5f)+math.abs(Up)*(size*.5f)+math.abs(Forward)*.002f;
                    EnemyStatusBounds009 b=Bounds[k];b.Min=math.min(b.Min,center-ext);b.Max=math.max(b.Max,center+ext);Bounds[k]=b;
                }
            }
        }
    }

}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Pure numeric world/camera-local billboard layout; camera/object snapshots copied by main-thread adapters.
// Array Formats:48x32Binputs,9x48x64Bfloat4x4matrices,9counts/24Bbounds,knownlivebits31; no engine objects or managed state in job.
// Dependencies/Pointers: Burst/Collections/Jobs/Mathematics; same renderer owns all native arrays/fence, no borrowed module memory or damage authority.
