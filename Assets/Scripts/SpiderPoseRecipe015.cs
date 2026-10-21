using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace OdysseyOracle
{
    // Work-only original numeric prototype. No graph, scene or gameplay owner.
    public static class SpiderPoseRecipe015
    {
        public const int ClipCount=5,BindingCount=10,KeysPerBinding=49;
        public const int KeyCount=ClipCount*BindingCount*KeysPerBinding;
        public const string ResourcePath="Animation015/Spider/Pilot015";
        public static string BindingPath(int binding)
        {
            switch(binding)
            {
                case 0:return "Spider leg 0";case 1:return "Spider leg 1";
                case 2:return "Spider leg 2";case 3:return "Spider leg 3";
                case 4:return "Spider leg 4";case 5:return "Spider leg 5";
                case 6:return "Spider leg 6";case 7:return "Spider leg 7";
                case 8:return "Left arm pivot";case 9:return "Right arm pivot";
                default:return null;
            }
        }
        public static float3 Hip(int leg)=>new float3((leg&1)==0?-.29f:.29f,.75f,((leg>>1)-1.5f)*.2112f);
        public static bool SwingGroupA(int leg)=>((leg>>1)%2)==(leg&1);
        public static RigidPoseKey015 Sample(int index)
        {
            if(index<0||index>=KeyCount)throw new ArgumentOutOfRangeException(nameof(index));
            int clip=index/(BindingCount*KeysPerBinding),binding=index/KeysPerBinding%BindingCount;
            TrySample(clip,binding,(index%KeysPerBinding)/(float)(KeysPerBinding-1),out var value);
            return value;
        }
        public static bool TrySample(int clip,int binding,float t,out RigidPoseKey015 value)
        {
            value=default;
            if(clip<0||clip>=ClipCount||binding<0||binding>=BindingCount||!math.isfinite(t)||t<0f||t>1f)return false;
            value.Binding=(uint)binding;
            float eased=t*t*t*(t*(t*6f-15f)+10f);
            if(binding<8)
            {
                // Exact original hip/yaw-only articulation: no new joint or mesh.
                float phase=(t==1f?0f:t)*math.PI*2f+(SwingGroupA(binding)?0f:math.PI);
                float wave=math.sin(phase),lift=0f,yaw=0f;
                if(clip==0)yaw=wave*.2f;
                else if(clip==1){float positive=math.max(0f,wave);lift=.055f*positive*positive;yaw=wave*4f;}
                else
                {
                    float side=(binding&1)==0?-1f:1f;
                    float rowSign=(binding>>1)<2?-1f:1f;
                    float brace=clip==2?eased:clip==3?math.lerp(1f,-.5f,eased):math.lerp(-.5f,0f,eased);
                    yaw=side*rowSign*1.5f*brace; // All eight soles remain at authoredY.
                }
                value.Position=Hip(binding)+new float3(0f,lift,0f);
                value.Rotation=quaternion.RotateY(math.radians(yaw));
            }
            else
            {
                // Two pre-existing tiny mandible/arm pivots; position is NOT a clip binding.
                float end=binding==8?30f:35f;
                float angle=clip==2?-105f*t:clip==3?math.lerp(-105f,end,t):clip==4?end*(1f-eased):0f;
                value.Rotation=quaternion.RotateX(math.radians(angle));
            }
            return true;
        }
    }
    [BurstCompile]
    public struct BuildSpiderPoseKeys015:IJobParallelFor
    {
        [WriteOnly] public NativeArray<RigidPoseKey015> Output;
        public void Execute(int index){Output[index]=SpiderPoseRecipe015.Sample(index);}
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Original unscaled body-local metres, +Y up/+Z forward; body1.20 scale stays external. Eight exact hip pivots plus existing two arm/mandible rotations only.
// Array Formats: Existing32B RigidPoseKey015,5clips x10bindings x49keys=2450keys/78400B caller-owned cold TempJob output; no runtime allocation or graph ownership.
// Dependencies/Pointers: Work-only prototype against actual Foundation/game metadata; root must bake/verify a separate default-OFF Spider profile and share global max2 graph slots. No movement, collision, damage, attack clock, pool generation or mesh changes.
