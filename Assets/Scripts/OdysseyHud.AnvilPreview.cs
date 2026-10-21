using OdysseyOracle.Foundation.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OdysseyOracle
{
    public sealed partial class OdysseyHud
    {
        readonly RawImage[] anvilPreviewImages=new RawImage[12];
        readonly Text[] anvilPreviewPlaceholders=new Text[12],anvilPreviewLocks=new Text[12];
        readonly int[] anvilPreviewShownStates={-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1};
        TowerAnvilPreviewAtlas anvilPreview;
        ScrollRect anvilPreviewScroll;
        bool anvilPreviewAttempted,anvilPreviewReady,anvilPreviewPixelsReady;
        int anvilPreviewFire,anvilPreviewWater,anvilPreviewEarth,anvilPreviewAir;
        static readonly string[,] AnvilPreviewStateText={
            {"TIER 1 · WAITING","TIER 1 · LOCKED","TIER 1 · INCLUDED","TIER 1 · CURRENT"},
            {"TIER 2 · WAITING","TIER 2 · LOCKED","TIER 2 · AVAILABLE","TIER 2 · CURRENT"},
            {"TIER 3 · WAITING","TIER 3 · LOCKED","TIER 3 · AVAILABLE","TIER 3 · CURRENT"}};

        void BuildAnvilPreviewTile(RectTransform card,int type,int tier)
        {
            int tile=type*3+tier-1;float x=102+(tier-1)*168;
            var rect=Rect(card,"ActualTower3DPreview"+tier,new Vector2(0,1),new Vector2(.5f,1),new Vector2(x,-108),new Vector2(116,116));
            var image=rect.gameObject.AddComponent<RawImage>();image.raycastTarget=false;image.color=Color.clear;float4uv(tile,out Rect uv);image.uvRect=uv;anvilPreviewImages[tile]=image;
            Border(rect,MutedGold,1);
            anvilPreviewPlaceholders[tile]=Label(rect,"PreviewModelStatus","3D MODEL WAITING",12,Muted,TextAnchor.MiddleCenter,Vector2.zero,new Vector2(112,112),new Vector2(.5f,.5f));
            anvilPreviewLocks[tile]=Label(card,"AcceptedPreviewLock"+tier,AnvilPreviewStateText[tier-1,0],12,Muted,TextAnchor.MiddleCenter,
                new Vector2(18+(tier-1)*168,-230),new Vector2(156,18),new Vector2(0,1));
        }
        static void float4uv(int tile,out Rect uv)
        {var value=TowerAnvilPreviewMath.TileUv(tile);uv=new Rect(value.x,value.y,value.z,value.w);}
        void RefreshAnvilPreviewAccepted(bool ready,ITowerForgeModeAdapter adapter)
        {
            int fire=ready&&adapter!=null?adapter.GetTier(0):0,water=ready&&adapter!=null?adapter.GetTier(1):0,earth=ready&&adapter!=null?adapter.GetTier(2):0,air=ready&&adapter!=null?adapter.GetTier(3):0;
            bool snapshot=ready&&TowerForgeDefinitions.ValidTier(fire)&&TowerForgeDefinitions.ValidTier(water)&&TowerForgeDefinitions.ValidTier(earth)&&TowerForgeDefinitions.ValidTier(air);
            anvilPreviewReady=snapshot;anvilPreviewFire=fire;anvilPreviewWater=water;anvilPreviewEarth=earth;anvilPreviewAir=air;
            anvilPreview?.SetAcceptedTiers(snapshot,fire,water,earth,air);
            for(int type=0;type<4;type++)
            {
                int accepted=type==0?fire:type==1?water:type==2?earth:air;
                for(int tier=1;tier<=3;tier++)
                {
                    int tile=type*3+tier-1,state=!snapshot?0:tier>accepted?1:tier==accepted?3:2;
                    if(anvilPreviewShownStates[tile]==state)continue;anvilPreviewShownStates[tile]=state;
                    anvilPreviewLocks[tile].text=AnvilPreviewStateText[tier-1,state];anvilPreviewLocks[tile].color=state==3?Gold:state==2?Ivory:Muted;
                }
            }
        }
        void TickAnvilPreview()
        {
            bool show=IsFieldTowerForgeVisible&&session!=null&&Application.isFocused&&!IntroActive&&!LoadingActive&&!DownedActive&&!ShopVisible;
            if(show&&!anvilPreviewAttempted&&session.TowerModels!=null)
            {
                anvilPreviewAttempted=true;
                try
                {
                    anvilPreview=new TowerAnvilPreviewAtlas(session.TowerModels);anvilPreview.SetAcceptedTiers(anvilPreviewReady,anvilPreviewFire,anvilPreviewWater,anvilPreviewEarth,anvilPreviewAir);
                    for(int tile=0;tile<12;tile++){anvilPreviewImages[tile].texture=anvilPreview.Target;anvilPreviewImages[tile].color=Color.clear;}
                }
                catch(System.Exception failure)
                {
                    FailAnvilPreview(failure);
                }
            }
            if(anvilPreview!=null)
            {
                try
                {
                    anvilPreview.SetVisible(show);anvilPreview.Tick(Time.unscaledDeltaTime,!OracleBookAccessibility.PrefersReducedMotion);
                    if(show&&anvilPreview.ConsumeWorkshopCue015(out uint workshopRevision015))session.Audio?.TryPlayWorkshopAssembly015(Vector3.zero,workshopRevision015,true);
                    if(!anvilPreviewPixelsReady&&anvilPreview.RenderCount>0)
                    {
                        anvilPreviewPixelsReady=true;
                        for(int tile=0;tile<12;tile++){anvilPreviewImages[tile].color=Color.white;anvilPreviewPlaceholders[tile].gameObject.SetActive(false);}
                    }
                }
                catch(System.Exception failure){FailAnvilPreview(failure);}
            }
            if(show&&anvilPreviewScroll!=null&&!GameActionBindings.IsRebinding)
            {
                var keyboard=Keyboard.current;
                if(keyboard!=null&&keyboard.pageDownKey.wasPressedThisFrame)anvilPreviewScroll.verticalNormalizedPosition=Mathf.Clamp01(anvilPreviewScroll.verticalNormalizedPosition-.65f);
                else if(keyboard!=null&&keyboard.pageUpKey.wasPressedThisFrame)anvilPreviewScroll.verticalNormalizedPosition=Mathf.Clamp01(anvilPreviewScroll.verticalNormalizedPosition+.65f);
            }
        }
        void FailAnvilPreview(System.Exception failure)
        {
            anvilPreview?.Dispose();anvilPreview=null;anvilPreviewPixelsReady=false;
            for(int tile=0;tile<12;tile++){anvilPreviewImages[tile].texture=null;anvilPreviewImages[tile].color=Color.clear;anvilPreviewPlaceholders[tile].text="3D MODEL UNAVAILABLE";anvilPreviewPlaceholders[tile].gameObject.SetActive(true);}
            Debug.LogWarning("Tower Anvil preview unavailable: "+failure.GetType().Name+" · "+failure.Message);
        }
        void HideAnvilPreview(){anvilPreview?.SetVisible(false);}
        void DisposeAnvilPreview()
        {
            for(int tile=0;tile<12;tile++)if(anvilPreviewImages[tile]!=null)anvilPreviewImages[tile].texture=null;
            anvilPreview?.Dispose();anvilPreview=null;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: actual FieldForge2x2masked scroll cards with3square116px model views/card; one3x4RT cropped by immutable UVs. Balance/receipt/Return stay outside scroll; outer1240x1024fit unchanged.
// Array Formats:12cachedRawImages/placeholders/state labels, static tier-state strings and scalar accepted tier copies; one lazy cold lease and pixels remain hidden until a matched real camera render. No hot instantiation/load/search/texture readback or strings on successful tick.
// Dependencies/Pointers: ForgeAdapter exclusively supplies accepted tiers; pending/purchases never unlock previews. Only visible/focused menu ticks20Hz, reducedmotion static, immediate hide/owned disposal; existing upgrade buttons/ledger/charge/save authority untouched.
