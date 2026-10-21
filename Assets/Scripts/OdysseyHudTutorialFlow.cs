using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class OdysseyHud
    {
        UnityEngine.UI.Text instructionCardHeading,instructionCardCaption,instructionCardNote,instructionVoiceLabel,instructionTranscriptLabel;
        UnityEngine.UI.Button instructionPlayButton,instructionNextButton,instructionPrevious015,instructionVisitButton,instructionTranscriptButton;
        int displayedInstructionCard=-2;uint displayedGuideRevision015=uint.MaxValue;
        bool instructionSnapshotValid,instructionTranscriptExpanded,displayedTranscript015;
        public bool IsTutorialInstructionVisible=>built&&currentPanel==JourneyPanel.TutorialInstruction&&
            panels.ContainsKey(JourneyPanel.TutorialInstruction)&&panels[JourneyPanel.TutorialInstruction].gameObject.activeInHierarchy;
        static Color TutorialElementColor(int element)
        {
            switch(element){case 0:return new Color(1f,.28f,.22f);case 1:return new Color(.25f,.62f,1f);case 2:return new Color(.65f,.42f,.23f);default:return Color.white;}
        }
        void BuildTutorialInstructionPanel()
        {
            var content=PanelContent(JourneyPanel.TutorialInstruction,"OptionalTutorialGuide",new Vector2(780,660),false);
            content.GetComponent<UnityEngine.UI.VerticalLayoutGroup>().spacing=6;
            instructionCardHeading=MenuLabel(content,"InstructionHeading","",25,Ivory,36);
            instructionCardNote=MenuLabel(content,"InstructionFacts","",16,Ivory,64);
            instructionCardCaption=MenuLabel(content,"OptionalExactTranscript","",17,Muted,126);
            instructionTranscriptButton=MenuButton(content,"ToggleInstructionTranscript","SHOW TRANSCRIPT",ToggleInstructionTranscript,32);
            instructionTranscriptLabel=instructionTranscriptButton.GetComponentInChildren<UnityEngine.UI.Text>();
            instructionPlayButton=MenuButton(content,"GuideNarrationPlayPause","PLAY NARRATION",()=>{session.Tutorial?.ToggleGuideNarration015();RefreshTutorialInstructionPresentation();},38);
            instructionVoiceLabel=instructionPlayButton.GetComponentInChildren<UnityEngine.UI.Text>();
            var navigation=Rect(content,"GuideHistory",Vector2.zero,new Vector2(.5f,.5f),Vector2.zero,new Vector2(0,38));Height(navigation,38);
            var row=navigation.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();row.spacing=10;row.childControlWidth=row.childControlHeight=row.childForceExpandWidth=row.childForceExpandHeight=true;
            instructionPrevious015=MenuButton(navigation,"PreviousOpenedGuide","PREVIOUS GUIDE",()=>session.Tutorial?.ReviewPreviousGuide015(),38);
            instructionNextButton=MenuButton(navigation,"NextGuide","NEXT GUIDE",()=>session.Tutorial?.NextGuide015(),38);
            instructionGuideState020=MenuLabel(navigation,"GuideCounterState020","",12,Muted,38);
            instructionVisitButton=MenuButton(content,"VisitRaykonInstruction","VISIT RAYKON · WALK THERE",()=>session.Tutorial?.VisitRaykonFromInstruction(),32);
            MenuButton(content,"InstructionSettings","NARRATION SETTINGS",()=>{OpenSettings(JourneyPanel.TutorialInstruction);SetPanel(JourneyPanel.Audio);},34);
            MenuButton(content,"CloseOptionalGuide","CLOSE / BACK · ESC",BackMenu015,40);
            instructionCardCaption.gameObject.SetActive(false);
        }
        void ToggleInstructionTranscript()
        {
            if(currentPanel!=JourneyPanel.TutorialInstruction||session.Tutorial==null)return;
            instructionTranscriptExpanded=!instructionTranscriptExpanded;instructionSnapshotValid=false;RefreshTutorialInstructionPresentation();
        }
        public bool ShowTutorialInstruction()
        {
            if(!built||session==null||!session.HasStarted||session.IsDead||session.IsDowned||session.Victory||ShopVisible||
                session.Tutorial==null||!session.Tutorial.HasGuidePresentation015)return false;
            if(currentPanel!=JourneyPanel.TutorialInstruction){guideCursorReturn015=session.CursorFree015;guideReturn015=currentPanel;instructionTranscriptExpanded=false;}
            AbilityBindings.CancelRebind();session.Pause();instructionSnapshotValid=false;
            SetPanel(JourneyPanel.TutorialInstruction);RefreshTutorialInstructionPresentation();return true;
        }
        void FitTopInstructionCard(bool caption)
        {
            var card=panels[JourneyPanel.TutorialInstruction];card.sizeDelta=new Vector2(780,caption?660:528);
            var bounds=(canvas.transform as RectTransform).rect.size;
            float fit=Mathf.Min(1,Mathf.Min((bounds.x-48)/780,(bounds.y-64)/card.sizeDelta.y));card.localScale=Vector3.one*Mathf.Max(.1f,fit);
        }
        void RefreshTutorialInstructionPresentation()
        {
            if(currentPanel!=JourneyPanel.TutorialInstruction)return;
            var tutorial=session.Tutorial;
            if(tutorial==null||!tutorial.HasGuidePresentation015){instructionTranscriptExpanded=false;SetPanel(JourneyPanel.None);return;}
            FitTopInstructionCard(instructionTranscriptExpanded);RefreshGuideState020(tutorial);
            uint revision=tutorial.GuidePresentationRevision015;
            if(instructionSnapshotValid&&displayedGuideRevision015==revision&&displayedTranscript015==instructionTranscriptExpanded)return;
            instructionSnapshotValid=true;displayedGuideRevision015=revision;displayedTranscript015=instructionTranscriptExpanded;
            displayedInstructionCard=tutorial.CurrentGuideIndex;
            instructionCardHeading.text=tutorial.GuideTitle015;instructionCardCaption.text=tutorial.GuideCaption015;instructionCardNote.text=tutorial.GuideNote015;
            instructionCardCaption.gameObject.SetActive(instructionTranscriptExpanded);
            instructionTranscriptLabel.text=instructionTranscriptExpanded?"HIDE TRANSCRIPT":"SHOW TRANSCRIPT";
            instructionPlayButton.interactable=tutorial.GuideAudioAvailable015;
            instructionVoiceLabel.text=tutorial.GuideAudioAvailable015?tutorial.GuidePlaybackLabel020:"NO AUDIO";
            instructionNextButton.interactable=tutorial.GuideNextAvailable015;instructionPrevious015.interactable=tutorial.GuidePreviousAvailable015;
            instructionVisitButton.gameObject.SetActive(tutorial.HasPendingInstruction&&!tutorial.GuideReview015&&tutorial.CurrentGuideIndex==2);
            instructionVisitButton.interactable=!tutorial.GuidePlaying015;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing fitted uGUI Canvas, explicit optional guide menu only; compact hint owns automatic presentation.
// Array Formats: Cold controls, cached pure playback revision and one collapsed transcript disclosure; no Update collections or format strings.
// Dependencies/Pointers: Exact original resource/caption pairing and real tutorial acknowledgement remain canonical. No automatic menu open/lesson completion, focus replay, new audio/context/pointer/reward authority.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing fitted guide/menu Canvas and actual guide view/audio state.
// Array Formats: Existing bounded UI/state stores; cold immutable65status labels, no frame formatting.
// Dependencies/Pointers: Original restart/portal/settings/narration/voice/input/achievement owners remain. Root owns source apply and full compile/native/browser/listening acceptance.
