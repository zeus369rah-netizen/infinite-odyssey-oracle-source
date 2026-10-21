using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class SavedTowerModelPresentation
    {
        TowerElementVisualInstance elementalPresentation;
        bool elementalFailed;
        int elementalTintId;
        public bool ElementPresentationReady=>elementalPresentation!=null&&elementalPresentation.Ready;
        private void ApplyElementPresentationTier()
        {
            if(!initialized||destroyed)return;
            if(elementalPresentation==null&&!elementalFailed&&library.Elements!=null&&library.Elements.Ready)
            {
                try{elementalPresentation=new TowerElementVisualInstance(groups[1],library.Elements,type,state.Tier);}
                catch(System.Exception error){elementalFailed=true;Debug.LogWarning("Original tower elemental instance unavailable: "+error.Message);}
            }
            if(ElementPresentationReady)elementalPresentation.SetTier(state.Tier);
            if(library.Elements==null||!library.Elements.Ready)elementalFailed=true;
            float brightness=TowerElementRecipes.Get(type,state.Tier).Shape.w;
            if(elementalTintId==0)elementalTintId=Shader.PropertyToID("_BaseColor");
            var authored=library.GetMaterial(type);Color baseColor=authored.GetColor(elementalTintId);
            glowBlock.SetColor(elementalTintId,new Color(baseColor.r*brightness,baseColor.g*brightness,baseColor.b*brightness,baseColor.a));
            // Restore authored emission before binding all groups; the existing Tick alone pulses groups2/3/4.
            glowBlock.SetColor(EmissionId,authored.GetColor(EmissionId));
            // Six existing scene references only; recipe/spatial/particle loops live in Burst jobs/shader.
            for(int i=0;i<renderers.Length;i++)renderers[i].SetPropertyBlock(glowBlock);
        }
        private void TickElementPresentation(float delta,bool animate)
        {
            if(elementalPresentation==null&&!elementalFailed)ApplyElementPresentationTier();
            elementalPresentation?.Tick(delta,animate);
        }
        private void StopElementPresentation(){elementalPresentation?.Dispose();elementalPresentation=null;}
        private void OnDisable(){StopElementPresentation();}
        private void OnDestroy(){StopElementPresentation();}
        private void OnEnable(){elementalFailed=false;}
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing saved six-group Head pivot/orientation remains canonical; additive ambient/accepted flight owner is a child of that head. Foundation and logical collision/range do not move.
// Array Formats: One nullable owned fixed-native instance, cached tier-tint property id and existing6-renderer MPB adapter; no native Object field initializer or hot arrays.
// Dependencies/Pointers: Only existing accepted tier/shot consumers invoke binding; borrowed72 meshes/four materials remain unchanged. Instance stops/disposes on destroyed/disable/reload lifetime. Missing shader/instance uses existing fallback and is not visual feature acceptance.
