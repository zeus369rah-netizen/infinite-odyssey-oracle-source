using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class OdysseyHud
    {
        UnityEngine.UI.Text instructionCardHeading,instructionCardCaption,instructionCardNote,instructionVoiceLabel;
        UnityEngine.UI.Button instructionPlayButton,instructionNextButton,instructionVisitButton;
        int displayedInstructionCard=-2;
        bool displayedInstructionSpeaking,displayedInstructionPlayed,displayedInstructionAudio,instructionSnapshotValid;
        public bool IsTutorialInstructionVisible=>built&&currentPanel==JourneyPanel.TutorialInstruction&&
            panels.ContainsKey(JourneyPanel.TutorialInstruction)&&panels[JourneyPanel.TutorialInstruction].gameObject.activeInHierarchy;

        static Color TutorialElementColor(int element)
        {
            switch(element)
            {
                case 0:return new Color(1f,.28f,.22f);
                case 1:return new Color(.25f,.62f,1f);
                case 2:return new Color(.65f,.42f,.23f);
                default:return Color.white;
            }
        }
        void BuildTutorialInstructionPanel()
        {
            var content=PanelContent(JourneyPanel.TutorialInstruction,"TutorialInstructionPanel",new Vector2(1050,880));
            instructionCardHeading=MenuLabel(content,"InstructionHeading","",30,Ivory,66);
            instructionCardCaption=MenuLabel(content,"InstructionCaption","",22,Ivory,186);
            instructionCardNote=MenuLabel(content,"InstructionFacts","",17,Muted,108);
            var legend=Rect(content,"ElementColorLegend",Vector2.zero,new Vector2(.5f,.5f),Vector2.zero,new Vector2(0,40));
            Height(legend,40);
            var row=legend.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            row.spacing=10;row.childControlWidth=row.childControlHeight=row.childForceExpandWidth=row.childForceExpandHeight=true;
            string[] names={"FIRE / RED","WATER / BLUE","EARTH / BROWN","AIR / WHITE"};
            for(int i=0;i<4;i++)MenuLabel(legend,"ElementColor"+i,names[i],16,TutorialElementColor(i),40);
            instructionPlayButton=MenuButton(content,"PlayInstructionOnce","PLAY NARRATION ONCE",()=>{session.Tutorial?.TryPlayInstructionNarration();RefreshTutorialInstructionPresentation();},52);
            instructionVoiceLabel=instructionPlayButton.GetComponentInChildren<UnityEngine.UI.Text>();
            instructionNextButton=MenuButton(content,"PlayNextInstruction","PLAY NEXT",()=>session.Tutorial?.ContinueInstructionCard(),58);
            instructionVisitButton=MenuButton(content,"VisitRaykonInstruction","VISIT RAYKON · WALK THERE",()=>session.Tutorial?.VisitRaykonFromInstruction(),48);
            MenuButton(content,"InstructionPauseOptions","PAUSE OPTIONS / SKIP",()=>SetPanel(JourneyPanel.Pause),45);
        }
        public bool ShowTutorialInstruction()
        {
            if(!built||session==null||!session.HasStarted||session.IsDead||session.IsDowned||session.Victory||ShopVisible||
                session.Tutorial==null||!session.Tutorial.HasPendingInstruction)return false;
            AbilityBindings.CancelRebind();session.Pause();instructionSnapshotValid=false;
            SetPanel(JourneyPanel.TutorialInstruction);RefreshTutorialInstructionPresentation();return true;
        }
        void RefreshTutorialInstructionPresentation()
        {
            if(currentPanel!=JourneyPanel.TutorialInstruction)return;
            var tutorial=session.Tutorial;
            if(tutorial==null||!tutorial.HasPendingInstruction){SetPanel(JourneyPanel.None);return;}
            // BeginCampaign ends with Resume. Restore this already requested modal once,
            // after Intro/Loading have yielded; Pause owns pointer release and does not re-lock.
            if(!session.Paused)session.Pause();
            int card=tutorial.CurrentGuideIndex;
            bool speaking=tutorial.IsSpeaking,played=tutorial.InstructionNarrationPlayed,audio=tutorial.VoiceAvailable;
            if(instructionSnapshotValid&&card==displayedInstructionCard&&speaking==displayedInstructionSpeaking&&
                played==displayedInstructionPlayed&&audio==displayedInstructionAudio)return;
            instructionSnapshotValid=true;displayedInstructionCard=card;displayedInstructionSpeaking=speaking;
            displayedInstructionPlayed=played;displayedInstructionAudio=audio;
            instructionCardHeading.text=tutorial.Heading;instructionCardCaption.text=tutorial.Subtitle;instructionCardNote.text=tutorial.InstructionCardNote;
            instructionPlayButton.interactable=audio&&!played;
            instructionVoiceLabel.text=!audio?"AUDIO UNAVAILABLE · READ CAPTION":speaking?"NARRATION PLAYING":played?"NARRATION PLAYED ONCE":"PLAY NARRATION ONCE";
            instructionNextButton.interactable=!speaking;
            instructionVisitButton.gameObject.SetActive(card==2);instructionVisitButton.interactable=!speaking;
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: existing Canvas PanelContent fit/layout; existing session owns pause/pointer capture.
// Array Formats: four cold legend entries, cached scalar card/voice snapshot; no repeated callback/array construction or mesh rebuilds.
// Dependencies/Pointers: borrows TutorialDirector presentation API only. Element colors never encode TrainingLab ON/OFF state or alter progression colors/data.
