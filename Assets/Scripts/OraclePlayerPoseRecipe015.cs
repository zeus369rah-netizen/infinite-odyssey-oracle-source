using System;
using System.Runtime.InteropServices;
using Unity.Mathematics;
namespace OdysseyOracle
{
    [StructLayout(LayoutKind.Sequential,Pack=4,Size=32)]
    public struct OraclePlayerPoseFrame015
    {public float Speed01,Cycle01,Cast01,Motion01;public uint Flags,Sequence,Generation,Reserved;}
    public static class OraclePlayerPoseRecipe015
    {
        public const int Samples=25,Clips=4,BodyBindings=5,HandBindings=1,BodyKeyCount=500,HandKeyCount=100;
        public const float CastSeconds=.42f;
        public const string BodyPath="Animation015/OracleBody/Pilot015",HandsPath="Animation015/OracleHands/Pilot015";
        static readonly string[] BodyPaths={"Rigid left arm","Rigid right arm","Rigid right arm/Wrapped Oracle wand","Rigid left leg","Rigid right leg"};
        public static string Path(bool hands,int index)=>hands?(index==0?"Native hand animation015":null):(index>=0&&index<BodyBindings?BodyPaths[index]:null);
        public static RigidPoseKey015 Sample(bool hands,int index)
        {
            int count=hands?HandKeyCount:BodyKeyCount,bindings=hands?HandBindings:BodyBindings;
            if(index<0||index>=count)throw new ArgumentOutOfRangeException(nameof(index));
            int clip=index/(bindings*Samples),binding=index/Samples%bindings,key=index%Samples;float t=key/24f;
            float wave=math.sin(t*math.PI*2f),envelope=math.sin(t*math.PI);envelope*=envelope;
            float3 position=default,angles=default;
            if(hands)
            {
                if(clip==1)position.y=wave*.001f;
                else if(clip==2){position=new float3(wave*.01f,math.sin(t*math.PI*4f)*.007f,0);angles.z=wave*.45f;}
                else if(clip==3){position=new float3(-.008f,.004f,-.025f)*envelope;angles=new float3(6,-2,3)*envelope;}
            }
            else if(binding<2)
            {
                angles.z=binding==0?-3f:3f;
                angles.x=clip==1?wave*.6f:clip==2?wave*(binding==0?18f:-18f):clip==3?envelope*(binding==0?-24f:-72f):0f;
            }
            else if(binding==2)angles=new float3(6f+(clip==2?wave*3f:clip==3?envelope*12f:0),0,-13f);
            else
            {
                // Flat rigid soles make the supported pose set convex under native state blending.
                float side=binding==3?-1f:1f,stride=clip==2?wave*(binding==3?1f:-1f):0f;
                float restHip=.71f+math.max(0f,-SoleAtHip(0f)),lift=math.max(0f,stride)*.045f;
                position=new float3(side*.17f,restHip+lift,stride*.065f);
            }
            return new RigidPoseKey015{Position=position,Rotation=quaternion.EulerZXY(math.radians(angles)),Binding=(uint)binding};
        }
        public static float SoleAtHip(float degrees)
        {
            float angle=math.radians(degrees),c=math.cos(angle),s=math.sin(angle);
            float leg=.71f+c*(-.28f)-(math.abs(c)*.28f+math.abs(s)*.12f);
            float boot=.71f+c*(-.59f)-s*.06f-(math.abs(c)*.12f+math.abs(s)*.185f);
            return math.min(leg,boot);
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Original five body-child rigid pivots or one camera-local hand child; no world/camera/collider curve.
// Array Formats: Explicit32B input/existing32B key, fixed500body+100hand keys; only50Walkleg keys may change. Identity leg rotation/nonnegative .045m lift/.065m stride preserve original rest hip and full-mesh support under convex interpolation.
// Dependencies/Pointers: Pure Burst-compatible key math; clips sample accepted cosmetic age only. Root owns native binding/floor/peer acceptance.
