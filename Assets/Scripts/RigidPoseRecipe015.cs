using System;
using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace OdysseyOracle
{
    [StructLayout(LayoutKind.Sequential,Pack=4)]
    public struct RigidPoseFrame015
    {public float Speed01,Cycle01,Action01,Phase01;public uint Generation,Sequence,Flags,Revision;}
    [StructLayout(LayoutKind.Sequential,Pack=4)]
    public struct RigidPoseKey015
    {public float3 Position;public quaternion Rotation;public uint Binding;}
    public static class RigidPoseRecipe015
    {
        public const int KeyCount=750;
        public static RigidPoseKey015 Sample(int index)
        {
            if(index<0||index>=KeyCount)throw new ArgumentOutOfRangeException(nameof(index));
            int clip=index/150,binding=index/25%6,key=index%25;float t=key/24f;
            float stride=clip==1?math.sin(t*2f*math.PI):0f;
            float lean=clip==0?math.sin(t*2f*math.PI)*.35f:clip==1?-3f:clip==2?math.lerp(0f,7f,t):clip==3?math.lerp(-11f,2f,t):0f;
            float arm=clip==2?-105f*t:clip==3?math.lerp(-105f,35f,t):clip==4?math.lerp(35f,0f,t):0f;
            float3 position=default,angles=default;
            if(binding<2)angles.x=clip==1?(binding==0?-stride:stride)*16f:arm;
            else if(binding<4){float s=binding==2?stride:-stride;position=new float3(binding==2?-.23f:.23f,.29f+math.max(0f,s)*.07f,-s*.075f);angles.x=s*18f-lean;}
            else if(binding==4)angles=new float3(lean,0,-stride*1.2f);
            else angles=new float3(-lean*.35f,-stride*2f,0);
            return new RigidPoseKey015{Position=position,Rotation=quaternion.EulerZXY(math.radians(angles)),Binding=(uint)binding};
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Pure original body-local position/quaternion samples, no UnityEngine.Object calls.
// Array Formats: Explicit32byte input/key; fixed750key index domain, no allocation in valid sampling.
// Dependencies/Pointers: Burst editor job fills caller-owned TempJob storage; recipe has no scene/native lifetime, gameplay state or animation asset ownership.
