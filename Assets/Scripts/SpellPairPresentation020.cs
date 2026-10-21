using Unity.Mathematics;
namespace OdysseyOracle
{
    // Immutable numeric flyweight recipes. No Object, allocation, gameplay or clock owner.
    internal readonly struct SpellPairRecipe020
    {
        public readonly int Mask;
        public readonly float3 Core,Wing,Offset,ImpactStretch;
        public readonly float4 Tint,Accent;
        public readonly float Duration,Lift,Spread;
        public SpellPairRecipe020(int mask,float3 core,float3 wing,float3 offset,float4 tint,float4 accent,float duration,float lift,float spread,float3 stretch)
        {Mask=mask;Core=core;Wing=wing;Offset=offset;Tint=tint;Accent=accent;Duration=duration;Lift=lift;Spread=spread;ImpactStretch=stretch;}
    }
    internal static class SpellPairPresentation020
    {
        public static bool TryGet(int combo,out SpellPairRecipe020 value)
        {
            switch(combo)
            {
                case 1:value=new SpellPairRecipe020(9,new float3(1.4f,1.4f,4.8f),new float3(.65f,.6f,2.8f),new float3(1.6f,0,-1.3f),new float4(1,.48f,.08f,1),new float4(1,.82f,.25f,1),.42f,.18f,2.2f,new float3(1.8f,.8f,1.8f));return true;
                case 2:value=new SpellPairRecipe020(6,new float3(2.8f,1.8f,2.6f),new float3(1.2f,.8f,1.1f),new float3(1.8f,-.5f,-.9f),new float4(.30f,.20f,.08f,1),new float4(.37f,.56f,.18f,1),.50f,.10f,1.25f,new float3(1.8f,.45f,1.8f));return true;
                case 3:value=new SpellPairRecipe020(3,new float3(2.4f,2.4f,3),new float3(1.7f,2.2f,1.7f),new float3(1.8f,.9f,-1.5f),new float4(.70f,.86f,.92f,1),new float4(.93f,.97f,1,1),.65f,2.2f,.7f,new float3(1.4f,2.8f,1.4f));return true;
                case 4:value=new SpellPairRecipe020(12,new float3(1.2f,1.2f,5),new float3(.55f,.55f,3.6f),new float3(1.8f,.2f,-.8f),new float4(.63f,.61f,.50f,1),new float4(.83f,.90f,.75f,1),.36f,.35f,2.5f,new float3(.6f,.6f,2.8f));return true;
                case 5:value=new SpellPairRecipe020(5,new float3(2.2f,1.8f,3),new float3(1,.8f,1.7f),new float3(1.7f,-.4f,-1),new float4(.90f,.16f,.03f,1),new float4(1,.65f,.08f,1),.60f,.30f,1.2f,new float3(1.6f,.7f,1.3f));return true;
                case 6:value=new SpellPairRecipe020(10,new float3(1.5f,.9f,5),new float3(2.4f,.45f,2.1f),new float3(1.9f,.6f,-1.4f),new float4(.35f,.84f,1,1),new float4(.90f,.98f,1,1),.48f,.80f,2,new float3(2,.35f,1.2f));return true;
                default:value=default;return false;
            }
        }
        public static bool Allows(int combo,int element)=>element>=0&&element<4&&TryGet(combo,out var value)&&(value.Mask&(1<<element))!=0;
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Pure numeric projectile-local dimensions and world-impact expansion coefficients; no physical radius or position changes.
// Array Formats: Six immutable blittable value recipes returned by bounded switch; no heap, native pointer, Object API or mutable store.
// Dependencies/Pointers: Existing accepted combo IDs/masks remain canonical. Adapters consume only cosmetic values; damage/status/recovery/authority/wire layouts are unchanged.
