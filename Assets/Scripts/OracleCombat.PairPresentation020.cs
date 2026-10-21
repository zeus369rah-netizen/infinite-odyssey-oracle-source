using UnityEngine;
namespace OdysseyOracle
{
    public sealed partial class OracleCombat
    {
        static Vector3 PairVector020(Unity.Mathematics.float3 value)=>new Vector3(value.x,value.y,value.z);
        static Color PairColor020(Unity.Mathematics.float4 value)=>new Color(value.x,value.y,value.z,value.w);
        void PaintPairPart020(Renderer renderer,Color color)
        {
            impactBlock.SetColor("_BaseColor",color);impactBlock.SetColor("_Color",color);impactBlock.SetColor("_EmissionColor",color*1.3f);
            renderer.SetPropertyBlock(impactBlock);
        }
        internal void ApplyPairFlight020(int element,int combo,bool nova,float radius,Transform core,Transform first,Transform second,Renderer coreRenderer,Renderer firstRenderer,Renderer secondRenderer)
        {
            // Reset borrowed slots before a single/hostile/Nova launch can reuse a former pair.
            coreRenderer.SetPropertyBlock(null);firstRenderer.SetPropertyBlock(null);secondRenderer.SetPropertyBlock(null);
            first.localRotation=second.localRotation=Quaternion.identity;
            if(nova||!SpellPairPresentation020.Allows(combo,element)||!SpellPairPresentation020.TryGet(combo,out var recipe))return;
            core.localScale=PairVector020(recipe.Core)*radius;
            first.localScale=second.localScale=PairVector020(recipe.Wing)*radius;
            Vector3 offset=PairVector020(recipe.Offset)*radius;first.localPosition=offset;second.localPosition=new Vector3(-offset.x,offset.y,offset.z);
            PaintPairPart020(coreRenderer,PairColor020(recipe.Tint));PaintPairPart020(firstRenderer,PairColor020(recipe.Accent));PaintPairPart020(secondRenderer,PairColor020(recipe.Accent));
        }
        void PreparePairImpact020(Impact effect,int combo,ref Color color)
        {
            effect.Duration=.3f;effect.PairLift020=.3f;effect.PairSpread020=1;effect.PairStretch020=Vector3.one;
            if(!SpellPairPresentation020.TryGet(combo,out var recipe))return;
            color=PairColor020(recipe.Tint);effect.Duration=recipe.Duration;effect.PairLift020=recipe.Lift;effect.PairSpread020=recipe.Spread;effect.PairStretch020=PairVector020(recipe.ImpactStretch);
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing three child transforms in projectile-local space and accepted world-impact roots; collider and damage envelopes unchanged.
// Array Formats: Same48 real/12 cosmetic bolts and20 three-part impacts, one existing reusable property block; no new Object, draw-part, light, timer or native allocation.
// Dependencies/Pointers: Main-thread presentation adapters borrow owned renderers and reset reused slots. Pure six-recipe data changes appearance only; root owns actual render/performance acceptance.
