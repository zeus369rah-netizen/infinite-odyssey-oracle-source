using System;
using Unity.Mathematics;
namespace OdysseyOracle
{
    public static class ShadePoseRecipe015
    {
        public const string ResourcePath="Animation015/Shade/Pilot015";
        public const int KeyCount=1500;
        public static RigidPoseKey015 Sample(int index)
        {
            if(index<0||index>=KeyCount)throw new ArgumentOutOfRangeException(nameof(index));
            int clip=index/150,binding=index/25%6,key=index%25,phase=clip%5;bool flight=clip>=5;
            float t=key/24f,wave=math.sin(t*math.PI*2f),stride=phase==1?wave:0f;
            float lean=flight?phase<2?wave*.25f:0f:phase==0?wave*.6f:phase==1?-2f:phase==2?math.lerp(0,4,t):phase==3?math.lerp(-6,1,t):0f;
            float3 position=default,angles=default;
            if(binding<2)
            {
                float start=binding==0?-70f:-105f,end=binding==0?30f:35f;
                angles.x=phase==2?start*t:phase==3?math.lerp(start,end,t):phase==4?math.lerp(end,0,t):phase==1?(binding==0?-stride*(flight?4:9):stride*6):wave*(binding==0?.8f:1.6f);
                if(phase<2)angles.z=wave*(binding==0?-.7f:1.1f);
            }
            else if(binding<4)
            {
                float s=binding==2?stride:-stride;position=new float3(binding==2?-.23f:.23f,.29f+(flight?0:math.max(0,s)*.06f),flight?0:-s*.065f);
                angles.x=flight?-48f:s*15f-lean;
            }
            else if(binding==4)angles=new float3(lean,0,-stride*(flight?.4f:1.3f));
            else angles=new float3(-lean*.3f,-stride*(flight?.6f:1.5f),0);
            return new RigidPoseKey015{Position=position,Rotation=quaternion.EulerZXY(math.radians(angles)),Binding=(uint)binding};
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Distinct original Shade six-child local articulation; no world/body/root/focus/crown/fin curves.
// Array Formats: Reuses32byte key,1500fixed samples across10ground/flight clips; flight tuck-48 and ranged arms-70/-105→30/35.
// Dependencies/Pointers: Pure Burst-compatible math; actual host phase/hover/bolts/HP and grounded sole policy remain external. No allocation in valid sampling.
