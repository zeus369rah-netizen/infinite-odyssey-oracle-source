using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class OdysseyHud
    {
        const float PauseGuideWidth020=420,PauseGuideHeight020=570,PauseGuideGap020=24;
        RectTransform pauseGuideCard020;
        UnityEngine.UI.Text pauseGuideHeading020,pauseGuideInstruction020,pauseGuideState020;
        UnityEngine.UI.LayoutElement pauseGuideInstructionHeight020;
        UnityEngine.UI.ScrollRect pauseGuideScroll020;
        UnityEngine.UI.Button pauseGuideSkip020;
        Vector2 pauseGuideBounds020,pauseGuidePauseSize020;
        Vector3 pauseGuidePauseScale020;
        bool pauseGuideLayoutReady020,pauseGuideSnapshot020;

        void BuildPauseGuide020()
        {
            pauseGuideCard020=Frame(menuRoot,"PausedTutorialGuide020",new Vector2(.5f,.5f),new Vector2(.5f,.5f),
                Vector2.zero,new Vector2(PauseGuideWidth020,PauseGuideHeight020),new Color(.025f,.04f,.033f,.98f),MutedGold);
            var content=Stretch(pauseGuideCard020,"CurrentGuideContent020",24,20,24,20);
            var layout=content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();layout.spacing=10;
            layout.childAlignment=TextAnchor.UpperCenter;layout.childControlWidth=layout.childControlHeight=true;
            layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;
            MenuLabel(content,"PausedGuideTitle020","TUTORIAL GUIDE",26,Gold,36);
            pauseGuideHeading020=MenuLabel(content,"ExactCurrentGuideHeading020","",20,Ivory,56);
            var body=ScrollingContent(content,"PausedGuideInstructionScroll020",230,out pauseGuideScroll020);
            pauseGuideInstruction020=MenuLabel(body,"ExactCurrentGuideInstruction020","",18,Ivory,230);
            pauseGuideInstructionHeight020=pauseGuideInstruction020.GetComponent<UnityEngine.UI.LayoutElement>();
            pauseGuideState020=MenuLabel(content,"ExactCurrentGuideState020","",17,Muted,42);
            pauseGuideSkip020=MenuButton(content,"PausedGuideSkip020","SKIP & ENTER PORTAL",SkipTutorialFromMenu,48);
            MenuButton(content,"PausedGuideNarration020","GUIDE / NARRATION",OpenGuideFromPause015,44);
            pauseGuideCard020.gameObject.SetActive(false);
        }

        bool PauseGuideAdmitted020()
        {
            return session!=null&&session.HasStarted&&session.Paused&&currentPanel==JourneyPanel.Pause
                &&!IntroActive&&!LoadingActive&&!DownedActive&&!ShopVisible&&!CampaignHubHudMode020
                &&session.World!=null&&session.World.Mode020==WorldMode020.TutorialRefuge
                &&session.Tutorial!=null&&!session.Tutorial.Finished&&!string.IsNullOrEmpty(session.Tutorial.Heading);
        }

        void HidePauseGuide020()
        {
            if(pauseGuideCard020!=null&&pauseGuideCard020.gameObject.activeSelf)pauseGuideCard020.gameObject.SetActive(false);
            if(panels.TryGetValue(JourneyPanel.Pause,out var pause)&&pause.anchoredPosition!=Vector2.zero)pause.anchoredPosition=Vector2.zero;
            pauseGuideLayoutReady020=false;pauseGuideSnapshot020=false;
        }

        void RefreshPauseGuide020()
        {
            if(pauseGuideCard020==null)return;
            if(!PauseGuideAdmitted020()||canvas==null||!panels.TryGetValue(JourneyPanel.Pause,out var pause))
            {HidePauseGuide020();return;}
            var bounds=(canvas.transform as RectTransform).rect.size;
            var size=pause.sizeDelta;var scale=pause.localScale;
            bool layoutChanged=!pauseGuideLayoutReady020||pauseGuideBounds020!=bounds||pauseGuidePauseSize020!=size||pauseGuidePauseScale020!=scale;
            if(layoutChanged)
            {
                pauseGuideLayoutReady020=true;pauseGuideBounds020=bounds;pauseGuidePauseSize020=size;pauseGuidePauseScale020=scale;
                float guideScale=Mathf.Min(1,(bounds.y-64)/PauseGuideHeight020);
                float pauseWidth=size.x*scale.x,guideWidth=PauseGuideWidth020*guideScale;
                bool fits=guideScale>=.8f&&pauseWidth+PauseGuideGap020+guideWidth<=bounds.x-64;
                if(pauseGuideCard020.gameObject.activeSelf!=fits)pauseGuideCard020.gameObject.SetActive(fits);
                pause.anchoredPosition=fits?new Vector2(-(guideWidth+PauseGuideGap020)*.5f,0):Vector2.zero;
                if(fits)
                {
                    pauseGuideCard020.localScale=Vector3.one*guideScale;
                    pauseGuideCard020.anchoredPosition=new Vector2((pauseWidth+PauseGuideGap020)*.5f,0);
                }
            }
            if(!pauseGuideCard020.gameObject.activeSelf)return; // Existing GAME GUIDE button is the narrow-screen fallback.
            var tutorial=session.Tutorial;
            bool changed=!pauseGuideSnapshot020;
            if(pauseGuideHeading020.text!=tutorial.Heading){pauseGuideHeading020.text=tutorial.Heading;changed=true;}
            if(pauseGuideInstruction020.text!=tutorial.Instruction){pauseGuideInstruction020.text=tutorial.Instruction;changed=true;}
            if(pauseGuideState020.text!=tutorial.GuideCounterStatus020)pauseGuideState020.text=tutorial.GuideCounterStatus020;
            pauseGuideSkip020.interactable=CanOfferTutorialSkip;
            if(changed)
            {
                // Only an accepted visibility/text edge requests layout; unchanged paused frames do no text measurement.
                Canvas.ForceUpdateCanvases();
                pauseGuideInstructionHeight020.preferredHeight=Mathf.Max(230,pauseGuideInstruction020.preferredHeight);
                pauseGuideScroll020.verticalNormalizedPosition=1;
            }
            pauseGuideSnapshot020=true;
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing menu Canvas reference pixels and exact fitted Pause card; no world positions or pointer owner.
// Array Formats: One cold retained card/scroll and fixed text/button references; scalar bounds, size, scale and visibility snapshots only.
// Dependencies/Pointers: Actual TutorialRefuge/paused/unfinished guide admits the card. Exact TutorialDirector text and existing Skip/Guide callbacks remain authoritative; narrow bounds retain the existing GAME GUIDE button. No gameplay, voice, progression or menu ancestry transition is introduced; root accepts actual native/browser layout/input.
