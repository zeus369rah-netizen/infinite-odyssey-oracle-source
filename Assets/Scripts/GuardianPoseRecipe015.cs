using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace OdysseyOracle
{
    [StructLayout(LayoutKind.Sequential, Pack=4)]
    public struct GuardianLegBounds015
    {
        public float3 Center, Extents;
        public bool Valid => math.all(math.isfinite(Center)) && math.all(math.isfinite(Extents)) &&
            math.all(Extents > 0f) && math.all(Extents < 2f) && math.all(math.abs(Center) < 2f) &&
            math.abs(Center.y - Extents.y + .29f) <= .0001f;
    }

    // Original rigid recipe; the cold baker supplies ACTUAL saved V2 leg bounds.
    public static class GuardianPoseRecipe015
    {
        public const int ClipCount=5, BindingCount=6, KeysPerBinding=101;
        public const int KeyCount=ClipCount*BindingCount*KeysPerBinding;
        public const string ResourcePath="Animation015/Guardian/Pilot015";
        public const float IdleHertz=.18f, UnwrappedCycleScale=.9f;
        public static string BindingPath(int binding)
        {
            switch(binding)
            {
                case 0:return "Left arm pivot";
                case 1:return "Right arm pivot";
                case 2:return "Left leg";
                case 3:return "Right leg";
                case 4:return "Procedural torso pivot";
                case 5:return "Procedural torso pivot/Procedural head pivot";
                default:return null;
            }
        }
        public static float3 RestPosition(int binding)
        {
            switch(binding)
            {
                case 0:return new float3(-.53f,1.36f,0f);
                case 1:return new float3(.53f,1.36f,0f);
                case 2:return new float3(-.23f,.29f,0f);
                case 3:return new float3(.23f,.29f,0f);
                case 4:return new float3(0f,.65f,0f);
                case 5:return new float3(0f,.83f,0f);
                default:return default;
            }
        }
        public static float Ease(float x) { x=math.clamp(x,0f,1f);return x*x*x*(10f+x*(-15f+6f*x)); }
        public static bool TryRecoveryProgress(float progress,float strikeSeconds,float recoverySeconds,out float value)
        {
            value=0f;
            if(!math.isfinite(progress)||!math.isfinite(strikeSeconds)||!math.isfinite(recoverySeconds)||
                progress<0f||progress>1f||strikeSeconds<=0f||recoverySeconds<=strikeSeconds)return false;
            float r=strikeSeconds/recoverySeconds;
            if(!math.isfinite(r)||1f-r<=.000001f)return false;
            value=math.clamp((progress-r)/(1f-r),0f,1f);return true;
        }
        public static float UpperWeight(float action,float progress,bool combat)
        {
            if(!combat)return 1f;
            return action<.25f?Ease(progress/.1f):action>.75f?1f-Ease(progress):1f;
        }
        public static bool TrySample(int clip,int binding,float t,GuardianLegBounds015 left,
            GuardianLegBounds015 right,out RigidPoseKey015 value)
        {
            value=default;
            if(clip<0||clip>=ClipCount||binding<0||binding>=BindingCount||!math.isfinite(t)||t<0f||t>1f||
                !left.Valid||!right.Valid)return false;
            value.Position=RestPosition(binding);value.Binding=(uint)binding;
            float pitch=0f,wave=math.sin((t==1f?0f:t)*2f*math.PI);
            if(binding==2||binding==3)
            {
                if(clip==1)
                {
                    float phase=math.frac((t==1f?0f:t)+(binding==3?.5f:0f));
                    const float swing=.35f,stance=.65f,scale=1.2f;
                    const float a=stance/(2f*scale),b=2f*a*swing/stance;
                    float z,h=0f;
                    if(phase<swing)
                    {
                        float u=phase/swing;
                        z=-a-b*u+(2f*a+b)*Ease(u);
                        h=64f*u*u*u*(1f-u)*(1f-u)*(1f-u);
                    }
                    else z=a-2f*a*(phase-swing)/stance;
                    pitch=-12f*h;
                    GuardianLegBounds015 bounds=binding==2?left:right;
                    float angle=math.radians(pitch),cs=math.cos(angle),sn=math.sin(angle);
                    float rotatedMinimum=bounds.Center.y*cs-bounds.Center.z*sn-
                        math.abs(cs)*bounds.Extents.y-math.abs(sn)*bounds.Extents.z;
                    float restMinimum=bounds.Center.y-bounds.Extents.y;
                    value.Position+=new float3(0f,.035f*h-(rotatedMinimum-restMinimum),z);
                }
            }
            else
            {
                float torso=0f,arm=0f;
                if(clip==0){torso=.6f*wave;arm=(binding==0?-1.5f:1.5f)*wave;}
                else if(clip==1){torso=2.5f+.65f*math.cos(t*4f*math.PI);arm=(binding==0?-7f:7f)*wave;}
                else if(clip==2)
                {
                    if(t<=.65f){float e=Ease(t/.65f);arm=-92f*e;torso=-3f*e;}
                    else{float e=Ease((t-.65f)/.35f);arm=math.lerp(-92f,24f,e);torso=math.lerp(-3f,6f,e);}
                }
                else{arm=24f;torso=6f;}
                // Recovery holds the impact shape; the masked native layer fades
                // toward live locomotion with the accepted recovery remap.
                pitch=binding<2?arm:binding==4?torso:-.35f*torso+(clip==0?.15f*wave:0f);
            }
            value.Rotation=quaternion.RotateX(math.radians(pitch));return true;
        }
    }

    [BurstCompile]
    public struct BuildGuardianPoseKeys015:IJobParallelFor
    {
        [ReadOnly] public NativeArray<GuardianLegBounds015> LegBounds;
        [WriteOnly] public NativeArray<RigidPoseKey015> Output;
        public void Execute(int index)
        {
            int clip=index/(GuardianPoseRecipe015.BindingCount*GuardianPoseRecipe015.KeysPerBinding);
            int binding=index/GuardianPoseRecipe015.KeysPerBinding%GuardianPoseRecipe015.BindingCount;
            float t=(index%GuardianPoseRecipe015.KeysPerBinding)/(float)(GuardianPoseRecipe015.KeysPerBinding-1);
            GuardianPoseRecipe015.TrySample(clip,binding,t,LegBounds[0],LegBounds[1],out var value);
            Output[index]=value;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Original six-pivot body-local +Z/Y-up metres; existing 1.20 body scale remains external.
// Array Formats: Existing 32B keys/frame unchanged; two caller-owned 24B actual saved-V2 leg bounds; fixed3030 keys, Burst-isolated cold build.
// Dependencies/Pointers: No UnityEngine.Object or gameplay authority; caller validates bounds/lengths, completes job before publishing/disposal. Recovery uses accepted owner durations; no new clock/damage/event.
