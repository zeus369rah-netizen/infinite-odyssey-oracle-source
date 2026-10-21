using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class OdysseyHud
    {
        readonly MenuAncestryState015 menuAncestry015=new MenuAncestryState015();
        DungeonWorld menuWorld015;string menuRoomCode015;bool menuContext015,menuStarted015,menuConnected015,menuArena015,menuBack015;
        UnityEngine.UI.Button compactGuidePlay015,compactGuideNext015,compactGuidePrevious020;
        UnityEngine.UI.Text compactGuidePlayLabel015;
        bool compactGuideDismissed015;string compactGuideStep015;TutorialDirector.Lesson compactGuideLesson015;
        uint guideUiRevision015=uint.MaxValue;
        UnityEngine.UI.Button restartTutorialButton015,partyVitalsMenuButton015;
        UnityEngine.UI.Text partyVitalsMenuLabel015,partyDisclosureLabel015;
        bool partyVitalsExpanded015;
        JourneyPanel guideReturn015=JourneyPanel.None;bool guideCursorReturn015;
        bool GuideTranscriptShown015=>instructionTranscriptExpanded&&currentPanel==JourneyPanel.TutorialInstruction;
        void OpenGuideFromPause015()
        {
            if(CampaignHubHudMode020)return;
            if(session.Tutorial!=null&&session.Tutorial.AwaitingSkillsMenu)session.Tutorial.TryOpenInstructionCard();
            else if(session.Tutorial!=null&&session.Tutorial.HasGuidePresentation015)OpenGuideMenu015(false);
            else OpenMechanicsGuide009(JourneyPanel.Pause);
        }
        void RefreshGuidePauseButtons015()
        {
            if(restartTutorialButton015!=null)restartTutorialButton015.gameObject.SetActive(session.CanRestartTutorial015);
            if(partyVitalsMenuButton015!=null)
            {
                partyVitalsMenuButton015.gameObject.SetActive(session.Rooms!=null&&session.Rooms.Connected);
                string text=partyVitalsExpanded015?"COLLAPSE PARTY VITALS":"EXPAND PARTY VITALS";
                if(partyVitalsMenuLabel015.text!=text)partyVitalsMenuLabel015.text=text;
            }
        }
        void BuildElementCards015(RectTransform content)
        {
            for(int r=0;r<2;r++)
            {
                var row=Rect(content,"OriginalElementCards"+r,Vector2.zero,new Vector2(.5f,.5f),Vector2.zero,new Vector2(0,116));Height(row,116);
                for(int c=0;c<2;c++)
                {
                    int i=r*2+c;Color tint=TutorialElementColor(i);
                    var card=Frame(row,"ElementCard"+i,new Vector2(c==0?.25f:.75f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(370,108),Panel,tint);
                    var key=Frame(card,"ActualElementKeycap",new Vector2(0,1),new Vector2(0,1),new Vector2(12,-12),new Vector2(48,34),Panel,MutedGold);
                    mechanicsElementKeys[i]=Label(key,"ActualBoundKey","",18,Ivory,TextAnchor.MiddleCenter,Vector2.zero,new Vector2(44,30),new Vector2(.5f,.5f));
                    var icon=Rect(card,"OriginalElementSigil",new Vector2(0,.5f),new Vector2(.5f,.5f),new Vector2(98,0),new Vector2(50,50));
                    foreach(var glyph in DrawSigil(icon,i))glyph.color=tint;
                    Label(card,"ElementName",i==0?"FIRE":i==1?"WATER":i==2?"EARTH":"AIR",18,tint,TextAnchor.MiddleLeft,new Vector2(145,-18),new Vector2(210,27),new Vector2(0,1));
                    Label(card,"ShortEffect",i==0?"Burn":i==1?"Slow":i==2?"Stagger":"Push",17,Ivory,TextAnchor.MiddleLeft,new Vector2(145,-53),new Vector2(210,26),new Vector2(0,1));
                    Label(card,"KeyCaption","KEY",9,Muted,TextAnchor.MiddleCenter,new Vector2(12,-51),new Vector2(48,18),new Vector2(0,1));
                }
            }
        }
        void TogglePartyVitals015()
        {
            partyVitalsExpanded015=!partyVitalsExpanded015;nextPartyHealthRefresh=0;
            if(partyDisclosureLabel015!=null)partyDisclosureLabel015.text=partyVitalsExpanded015?"▾ PARTY VITALS · COLLAPSE":"▸ PARTY VITALS · EXPAND";
            RefreshGuidePauseButtons015();
        }
        void BuildCompactGuide015()
        {
            BuildCompactGuideState020();
            var row=Rect(tutorialPrompt,"OptionalGuideControls",new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,-72),new Vector2(620,28));
            var layout=row.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();layout.spacing=6;layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandWidth=true;
            compactGuidePlay015=MenuButton(row,"CompactGuidePlayback","PLAY",()=>session.Tutorial?.ToggleGuideNarration015(),28);
            compactGuidePlayLabel015=compactGuidePlay015.GetComponentInChildren<UnityEngine.UI.Text>();compactGuidePlayLabel015.fontSize=12;
            MenuButton(row,"CompactGuideTranscript","TRANSCRIPT",()=>OpenGuideMenu015(true),28).GetComponentInChildren<UnityEngine.UI.Text>().fontSize=12;
            compactGuidePrevious020=MenuButton(row,"CompactPreviousGuide020","PREVIOUS",()=>session.Tutorial?.ReviewPreviousGuide015(),28);compactGuidePrevious020.GetComponentInChildren<UnityEngine.UI.Text>().fontSize=12;
            compactGuideNext015=MenuButton(row,"CompactNextGuide","NEXT",()=>session.Tutorial?.NextGuide015(),28);compactGuideNext015.GetComponentInChildren<UnityEngine.UI.Text>().fontSize=12;
            MenuButton(row,"DismissCompactGuide","HIDE",()=>{compactGuideDismissed015=true;tutorialPrompt.gameObject.SetActive(false);},28).GetComponentInChildren<UnityEngine.UI.Text>().fontSize=12;
        }
        void RefreshCompactGuide015(TutorialDirector tutorial)
        {
            if(compactGuidePlay015==null||tutorial==null)return;
            RefreshGuideState020(tutorial);
            if(compactGuideStep015!=tutorial.Heading||compactGuideLesson015!=tutorial.CurrentLesson)
            {compactGuideStep015=tutorial.Heading;compactGuideLesson015=tutorial.CurrentLesson;compactGuideDismissed015=false;}
            compactGuidePlay015.gameObject.SetActive(tutorial.HasGuidePresentation015);
            compactGuidePrevious020.gameObject.SetActive(tutorial.HasGuidePresentation015);
            compactGuideNext015.gameObject.SetActive(tutorial.HasGuidePresentation015);
            compactGuidePrevious020.interactable=tutorial.GuidePreviousAvailable015;
            compactGuideNext015.interactable=tutorial.GuideNextAvailable015;
            if(guideUiRevision015==tutorial.GuidePresentationRevision015)return;
            guideUiRevision015=tutorial.GuidePresentationRevision015;
            compactGuidePlay015.interactable=tutorial.GuideAudioAvailable015;
            compactGuidePlayLabel015.text=tutorial.GuidePlaybackLabel020;
        }
        bool OpenGuideMenu015(bool transcript)
        {
            var tutorial=session.Tutorial;
            if(tutorial==null||!tutorial.HasGuidePresentation015||!ShowTutorialInstruction())return false;
            instructionTranscriptExpanded=transcript;instructionSnapshotValid=false;RefreshTutorialInstructionPresentation();return true;
        }
        void TrackMenuContext015()
        {
            var world=session.World;bool connected=session.Rooms!=null&&session.Rooms.Connected,arena=world!=null&&world.IsCoopArena;string code=connected?session.Rooms.Code:null;
            if(!menuContext015||menuWorld015!=world||menuStarted015!=session.HasStarted||menuConnected015!=connected||menuArena015!=arena||menuRoomCode015!=code)
            {
                menuAncestry015.Clear();menuWorld015=world;menuStarted015=session.HasStarted;menuConnected015=connected;menuArena015=arena;menuRoomCode015=code;menuContext015=true;
                guideReturn015=JourneyPanel.None;guideCursorReturn015=false;instructionTranscriptExpanded=false;partyVitalsExpanded015=false;
                if(partyDisclosureLabel015!=null)partyDisclosureLabel015.text="▸ PARTY VITALS · EXPAND";
            }
        }
        public bool CanShowMouse015=>built&&session!=null&&currentPanel==JourneyPanel.None&&!IntroActive&&!LoadingActive&&!DownedActive&&!ShopVisible;
        public bool TryEscapeMenu015()
        {
            if(!built||session==null)return false;
            if(CampaignBookVisible020){EscapeCampaignBook020();return true;}
            if(IntroActive||LoadingActive||DownedActive||ShopVisible)return false;
            if(AbilityBindings.IsRebinding||GameActionBindings.IsRebinding){AbilityBindings.CancelRebind();GameActionBindings.CancelRebind();return true;}
            if(currentPanel==JourneyPanel.None)return false;
            BackMenu015();return true;
        }
        void BackMenu015()
        {
            if(currentPanel==JourneyPanel.Start||currentPanel==JourneyPanel.Outcome)return;
            if(currentPanel==JourneyPanel.Choice&&session.Rooms!=null&&session.Rooms.Busy)session.Rooms.LeaveRoom();
            if(currentPanel==JourneyPanel.Exit020){CancelExitPanel020();return;}
            if(currentPanel==JourneyPanel.Pause){ResumeJourney();return;}
            if(currentPanel==JourneyPanel.Tree){CloseTree();return;}
            if(currentPanel==JourneyPanel.TrainingLab){session.TrainingLab?.Close(true);return;}
            if(currentPanel==JourneyPanel.FieldForge){session.FieldSmith?.Close(true);return;}
            if(currentPanel==JourneyPanel.MechanicsGuide)session.RefugeActivities?.StopMechanicsGuide();
            if(currentPanel==JourneyPanel.TutorialInstruction){session.Tutorial?.PauseGuideNarration015();session.Tutorial?.ReturnToLiveGuide015();instructionTranscriptExpanded=false;}
            JourneyPanel fallback=session.HasStarted?JourneyPanel.Pause:JourneyPanel.Start;
            var target=(JourneyPanel)menuAncestry015.Back((int)fallback);
            bool returnMouse=currentPanel==JourneyPanel.TutorialInstruction&&guideCursorReturn015;
            if(currentPanel==JourneyPanel.TutorialInstruction)target=guideReturn015;
            if(target==JourneyPanel.None&&session.HasStarted)
            {
                if(returnMouse){SetPanel(JourneyPanel.None);session.ReturnToGuideControls015();}
                else ResumeJourney();
            }
            else {menuBack015=true;try{SetPanel(target);}finally{menuBack015=false;}}
        }
        void HostCoopFromGuide015()
        {
            if(CampaignHubHudMode020||IntroActive||LoadingActive)return;
            SetRoomMode020(false);
            session.Tutorial?.PauseGuideNarration015();SetPanel(JourneyPanel.Choice);session.Rooms?.CreateRoom();
        }
        void JoinFriendFromGuide015()
        {
            if(CampaignHubHudMode020||IntroActive||LoadingActive)return;
            SetRoomMode020(false);
            session.Tutorial?.PauseGuideNarration015();SetPanel(JourneyPanel.Choice);
            if(roomCodeInput!=null){roomCodeInput.Select();roomCodeInput.ActivateInputField();}
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing Canvas and actual current menu/world/room context; no pose/camera/pointer or networking authority writes.
// Array Formats: One fixed16ancestor store, cold compact controls and cached guide revision/text references. No hot hierarchy searches or string formatting.
// Dependencies/Pointers: One GameSession Escape owner calls this once; native/browser Resume remains canonical. Host/Join reuse real2player Room UI APIs; all custom Close owners remain. No theme/mixer/playlist restart calls.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing fitted guide/menu Canvas and actual guide view/audio state.
// Array Formats: Existing bounded UI/state stores; cold immutable65status labels, no frame formatting.
// Dependencies/Pointers: Original restart/portal/settings/narration/voice/input/achievement owners remain. Root owns source apply and full compile/native/browser/listening acceptance.
