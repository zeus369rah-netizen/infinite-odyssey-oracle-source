using Unity.Mathematics;
using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class OdysseyHud
    {
        RectTransform showcaseFitCanvas,showcaseFitCard;
        readonly Vector3[] showcaseFitCorners=new Vector3[4];
        Vector2 showcaseFitCanvasSize,showcaseFitCardSize,showcaseFitCardPosition;Vector3 showcaseFitCardScale;
        float showcaseFitAspect=16f/9f;float4 showcaseFitSafe=new float4(.025f,.08f,.55f,.92f);bool showcaseFitReady,showcaseFitValid;
        void CacheShowcaseFitLayout()
        {showcaseFitCanvas=canvas.transform as RectTransform;showcaseFitCard=panels[JourneyPanel.Start];showcaseFitReady=false;}
        bool RefreshShowcaseFitLayout()
        {
            if(showcaseFitCanvas==null||showcaseFitCard==null)return false;
            Vector2 size=showcaseFitCanvas.rect.size,cardSize=showcaseFitCard.rect.size,position=showcaseFitCard.anchoredPosition;Vector3 scale=showcaseFitCard.localScale;
            if(!showcaseFitReady||size!=showcaseFitCanvasSize||cardSize!=showcaseFitCardSize||position!=showcaseFitCardPosition||scale!=showcaseFitCardScale)
            {
                showcaseFitReady=true;showcaseFitCanvasSize=size;showcaseFitCardSize=cardSize;showcaseFitCardPosition=position;showcaseFitCardScale=scale;
                showcaseFitValid=false;
                if(size.x>0&&size.y>0)
                {
                    showcaseFitCard.GetWorldCorners(showcaseFitCorners);
                    float left=showcaseFitCanvas.InverseTransformPoint(showcaseFitCorners[0]).x-showcaseFitCanvas.rect.xMin;
                    showcaseFitAspect=size.x/size.y;
                    showcaseFitSafe=new float4(24f/size.x,40f/size.y,(left-24f)/size.x,1f-40f/size.y);
                    showcaseFitValid=MenuShowcaseCameraMath.ValidViewport(showcaseFitAspect,showcaseFitSafe);
                }
            }
            if(showcaseFitValid&&menuShowcase!=null)
            {
                try
                {
                    menuShowcase.SetViewport(showcaseFitAspect,showcaseFitSafe,Mathf.Max(1,Screen.width),Mathf.Max(1,Screen.height));
                    if(menuShowcase.Target!=null&&menuShowcaseImage.texture!=menuShowcase.Target){menuShowcaseImage.texture=menuShowcase.Target;menuShowcaseImage.color=Color.white;}
                }
                catch(System.Exception failure)
                {menuShowcase.Dispose();menuShowcase=null;UseShowcaseFallback();showcaseFitValid=false;Debug.LogWarning("Scripted showcase viewport unavailable: "+failure.Message);}
            }
            return showcaseFitValid;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: actual Canvas/start-card rectangle layout -> normalized free-left safe viewport,24reference-pixel horizontal/40vertical gaps; private showcase only.
// Array Formats: cold Vector3[4] layout scratch plus cached scalar size/position/scale/aspect/safe rectangle; dimension changes alone refresh corner queries/RT shape.
// Dependencies/Pointers: menu owners retain controls/layout; no player saved settings, World, network or progress changes. Unusable narrow free area explicitly suppresses animated publication rather than claiming readable full-map fit.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: physical viewport pixel dimensions plus existing normalized Canvas-safe rectangle.
// Array Formats: existing cached corners/layout; unchanged per-frame scalar call permits screen-pixel resize even when reference Canvas dimensions are constant.
// Dependencies/Pointers: menu-only RT budget input; no tutorial/HUD labels, saved quality or player settings writes.
