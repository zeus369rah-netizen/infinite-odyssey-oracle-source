using UnityEngine;
using UnityEngine.UI;

namespace OdysseyOracle
{
    public sealed partial class OdysseyHud
    {
        static readonly string[] ShowcaseQualityLabels={"QUALITY: OFF","QUALITY: LOW","QUALITY: ULTRA"};
        MenuShowcaseLease menuShowcase;
        MenuShowcaseCatalog menuShowcaseCatalog;
        RawImage menuShowcaseImage;
        Texture2D menuShowcaseFallback;
        Text menuShowcaseStatus,menuShowcaseMotionLabel,menuShowcaseQualityLabel;
        bool menuShowcaseAttempted,menuShowcaseMotion=true,menuShowcaseReduced;
        int menuShowcaseQuality=2;
        public bool MenuShowcaseReady=>menuShowcase!=null;
        public bool IsMenuShowcaseActive=>menuShowcase!=null&&menuShowcase.IsVisible;
        public int MenuShowcasePoseCount=>menuShowcase!=null?menuShowcase.PoseCount:0;
        public RenderTexture MenuShowcaseTexture=>menuShowcase!=null?menuShowcase.Target:null;
        void BuildMenuShowcaseBackdrop()
        {
            var backdrop=Stretch(menuRoot,"ScriptedCoopShowcaseBackdrop");backdrop.SetAsFirstSibling();
            menuShowcaseImage=backdrop.gameObject.AddComponent<RawImage>();menuShowcaseImage.raycastTarget=false;
            menuShowcaseFallback=Resources.Load<Texture2D>("Interface/CoopMapPreview");menuShowcaseImage.texture=menuShowcaseFallback;menuShowcaseImage.color=menuShowcaseFallback!=null?Color.white:Color.clear;
            menuShowcaseCatalog=Resources.Load<MenuShowcaseCatalog>(MenuShowcaseCatalog.ResourceKey);
            menuShowcaseReduced=OracleBookAccessibility.PrefersReducedMotion;
        }
        void BuildShowcaseStartControls(RectTransform content)
        {
            CacheShowcaseFitLayout();
            MenuLabel(content,"ShowcaseDisclosure","TWO SCRIPTED DEMO ORACLES · NOT A LIVE MATCH",12,Muted,23);
            var row=Rect(content,"ShowcaseUserControls",Vector2.zero,new Vector2(.5f,.5f),Vector2.zero,new Vector2(580,34));Height(row,34);
            var grid=row.gameObject.AddComponent<GridLayoutGroup>();grid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;grid.constraintCount=3;grid.cellSize=new Vector2(186,34);grid.spacing=new Vector2(8,0);grid.childAlignment=TextAnchor.MiddleCenter;
            var motion=MenuButton(row,"ShowcaseMotion",ShowcaseMotionText(),()=>{if(!OracleBookAccessibility.PrefersReducedMotion)menuShowcaseMotion=!menuShowcaseMotion;menuShowcaseMotionLabel.text=ShowcaseMotionText();},34);
            menuShowcaseMotionLabel=motion.GetComponentInChildren<Text>();menuShowcaseMotionLabel.fontSize=13;
            var quality=MenuButton(row,"ShowcaseQuality",ShowcaseQualityLabels[menuShowcaseQuality],CycleShowcaseQuality,34);menuShowcaseQualityLabel=quality.GetComponentInChildren<Text>();menuShowcaseQualityLabel.fontSize=13;
            var music=MenuButton(row,"ShowcaseMusic","MUSIC / VOLUME",()=>{session.Audio?.StartAudioFromGesture();OpenSettings(JourneyPanel.Start);SetPanel(JourneyPanel.Audio);},34);music.GetComponentInChildren<Text>().fontSize=13;
            menuShowcaseStatus=MenuLabel(content,"ShowcaseVerifiedState","SCENIC PREVIEW · ANIMATION UNAVAILABLE",11,Muted,20);
        }
        string ShowcaseMotionText()=>OracleBookAccessibility.PrefersReducedMotion?"REDUCED MOTION: ON":menuShowcaseMotion?"ANIMATION: ON":"ANIMATION: PAUSED";
        void UseShowcaseFallback()
        {menuShowcaseImage.texture=menuShowcaseFallback;menuShowcaseImage.color=menuShowcaseFallback!=null?Color.white:Color.clear;}
        void CycleShowcaseQuality()
        {
            menuShowcaseQuality=(menuShowcaseQuality+1)%3;menuShowcaseQualityLabel.text=ShowcaseQualityLabels[menuShowcaseQuality];
            if(menuShowcase!=null)
            {
                try{menuShowcase.SetQuality(menuShowcaseQuality);}
                catch(System.Exception failure){menuShowcase.Dispose();menuShowcase=null;Debug.LogWarning("Scripted showcase resize failed: "+failure.Message);menuShowcaseStatus.text="STATIC FALLBACK · SHOWCASE UNAVAILABLE";}
                if(menuShowcase!=null&&menuShowcase.Target!=null){menuShowcaseImage.texture=menuShowcase.Target;menuShowcaseImage.color=Color.white;}else UseShowcaseFallback();
                if(menuShowcase!=null)menuShowcaseStatus.text=menuShowcaseQuality==0?"STATIC VIEW · SHOWCASE OFF":"SCRIPTED PRESENTATION · NO MATCH OR REWARDS";
            }
        }
        void TickMenuShowcase()
        {
            if(menuShowcaseImage==null)return;
            bool reduced=OracleBookAccessibility.PrefersReducedMotion;
            if(menuShowcaseReduced!=reduced){menuShowcaseReduced=reduced;menuShowcaseMotionLabel.text=ShowcaseMotionText();}
            bool start=currentPanel==JourneyPanel.Start&&session!=null&&!session.HasStarted&&!IntroActive&&!LoadingActive;
            if(menuShowcaseImage.gameObject.activeSelf!=start)menuShowcaseImage.gameObject.SetActive(start);
            bool fitReady=RefreshShowcaseFitLayout();
            bool visible=start&&Application.isFocused&&menuShowcaseQuality!=0&&fitReady;
            if(start&&!fitReady)menuShowcaseStatus.text="WIDER VIEW NEEDED · STATIC PREVIEW";
            else if(start&&menuShowcase!=null)menuShowcaseStatus.text=menuShowcaseQuality==0?"STATIC VIEW · SHOWCASE OFF":"SCRIPTED PRESENTATION · NO MATCH OR REWARDS";
            if(visible&&!menuShowcaseAttempted)
            {
                menuShowcaseAttempted=true;
                try
                {
                    if(menuShowcaseCatalog!=null)
                    {
                        menuShowcase=new MenuShowcaseLease(menuShowcaseCatalog,session.TowerModels,menuShowcaseQuality);
                        menuShowcase.SetViewport(showcaseFitAspect,showcaseFitSafe);
                        menuShowcaseImage.texture=menuShowcase.Target;menuShowcaseImage.color=Color.white;menuShowcaseStatus.text="SCRIPTED PRESENTATION · NO MATCH OR REWARDS";
                    }
                }
                catch(System.Exception failure){menuShowcase?.Dispose();menuShowcase=null;UseShowcaseFallback();Debug.LogWarning("Scripted menu showcase unavailable: "+failure.GetType().Name+" · "+failure.Message);menuShowcaseStatus.text="STATIC FALLBACK · SHOWCASE UNAVAILABLE";}
            }
            if(menuShowcase!=null){menuShowcase.SetVisible(visible);menuShowcase.Tick(Time.unscaledDeltaTime,menuShowcaseMotion&&!reduced);}
        }
        void DisposeMenuShowcase(){menuShowcase?.Dispose();menuShowcase=null;}
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing Canvas/start-card reference coordinates; one aspect-preserving RT backdrop. Current Start and !HasStarted exclusively authorize visibility.
// Array Formats: Cold reusable lease, three static quality labels; active tick passes scalar unscaled delta/motion only. No hot instantiate, scene search, text formatting, audio source or save writes.
// Dependencies/Pointers: Existing Audio director retains explicit user-gesture music and saved Master/Music controls. Fallback is labeled and never accepted as animated completion. Root alone bakes/validates actual catalog and browser behavior.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: actual full catalog-local map bounds to private RT; real menu-left safe viewport, no scenic crop.
// Array Formats: existing bounded poses plus48B camera frame[1], two cold original avatar leases; no hot objects/strings.
// Dependencies/Pointers: actual MenuCoop009/rendering-layer7 and saved gameplay settings preserved; root native/fresh bake/browser acceptance pending.
