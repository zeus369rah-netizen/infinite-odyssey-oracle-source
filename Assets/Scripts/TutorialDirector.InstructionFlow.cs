using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class TutorialDirector
    {
        TutorialInstructionState instructionState;
        readonly AudioClip[] instructionClips=new AudioClip[TutorialInstructionState.CardCount];
        public bool RaykonIntroduced {get;private set;}
        public bool AwaitingSkillsMenu {get;private set;}
        public bool HasPendingInstruction=>active&&!Finished&&CurrentLesson==Lesson.Guide&&instructionState.HasCurrent;
        public bool InstructionNarrationPlayed=>HasPendingInstruction&&instructionState.HasSpoken(guideIndex);
        public string InstructionCardNote=>HasPendingInstruction?TutorialInstructionCards.Note(guideIndex):"";

        void InitializeInstructionFlow()
        {
            InitializeTrainingLabNarration();
            for(int i=0;i<instructionClips.Length;i++)instructionClips[i]=UnityEngine.Resources.Load<AudioClip>(TutorialInstructionCards.Resource(i));
        }
        void ResetInstructionFlow(){ResetGuidePlayback015();instructionState.Reset();RaykonIntroduced=false;AwaitingSkillsMenu=false;}
        void ShowInstructionCard(int card)
        {
            if(!active||Finished||!instructionState.Begin(card))return;
            if(card==2)RaykonIntroduced=true;
            CurrentLesson=Lesson.Guide;guideIndex=card;GuideHighlightElement=-1;guideTime=0;
            Heading=TutorialInstructionCards.Title(card);Instruction=TutorialInstructionCards.Note(card);
            Subtitle=narrationLine=TutorialInstructionCards.Caption(card);
            PrepareGuideStep015(card,instructionClips[card],TutorialInstructionCards.Caption(card));
        }
        public bool TryOpenInstructionCard()
        {
            if(!active||Finished||session==null||session.Hud==null)return false;
            if(AwaitingSkillsMenu){session.Hud.ShowSkillTree(false);return true;}
            if(!HasGuidePresentation015)return false;
            return session.Hud.ShowTutorialInstruction();
        }
        public bool TryPlayInstructionNarration()
        {
            return ToggleGuideNarration015();
        }
        public void ContinueInstructionCard()
        {
            if(!HasPendingInstruction||IsSpeaking||!Application.isFocused||applicationPaused||GameActionBindings.IsRebinding||
                session==null||session.Hud==null||GuideReview015||!instructionState.TryAcknowledge(out int card))return;
            StopVoice();session.Hud.ClosePanels();
            switch(card)
            {
                case 0:CurrentLesson=Lesson.Move;SetLessonText();ResumeInstructionPractice();break;
                case 2:ShowInstructionCard(3);ResumeInstructionPractice();break;
                case 3:ShowInstructionCard(4);ResumeInstructionPractice();break;
                case 4:BeginCombat();ResumeInstructionPractice();break;
                case 1:
                    AwaitingSkillsMenu=true;session.Hud.ShowSkillTree(false);break;
                case 5:
                    if(sharedPractice||session.CoopMode){ShowInstructionCard(6);ResumeInstructionPractice();}
                    else {BeginNovaPractice();ResumeInstructionPractice();}
                    break;
                case 6:
                    if(sharedPractice||session.CoopMode){ShowInstructionCard(7);ResumeInstructionPractice();}
                    else {BeginTowerPracticeLesson();ResumeInstructionPractice();}
                    break;
                case 7:case 8:case 9:case 10:ShowInstructionCard(card+1);ResumeInstructionPractice();break;
                case 11:BeginEcho();ResumeInstructionPractice();break;
            }
        }
        public void ContinueAfterSkillsMenu()
        {
            if(!active||Finished||!AwaitingSkillsMenu)return;
            AwaitingSkillsMenu=false;session.Hud.ClosePanels();ShowInstructionCard(5);ResumeInstructionPractice();
        }
        public void VisitRaykonFromInstruction()
        {
            if(!HasPendingInstruction||guideIndex!=2||IsSpeaking)return;
            // No remote talk/quest completion: the player must really walk to and interact with Raykon.
            session.Hud.ClosePanels();ResumeInstructionPractice();
            Instruction="Visit Raykon in his alcove if you wish. ESC > GUIDE & NARRATION returns to this guide.";
        }
        void ResumeInstructionPractice()
        {
            previousPosition=session.Player.transform.position;
            // Guide navigation keeps its mouse accessible; only explicit Resume rearms aim.
            session.ReturnToGuideControls015();
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: existing world metres and session pointer-capture ownership; card display stays in existing Canvas.
// Array Formats: fixed twelve AudioClip references loaded cold; bounded value masks own narration/acknowledgement only.
// Dependencies/Pointers: existing Combat accepted casts/guardian, Nova impact event and Tower defeat predicates retain authority. HUD owns modal; no XP, currency or room mutation.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing guide/lab modal and focus policy; no actor, world or saved progression mutation.
// Array Formats: Existing twelve guide clips plus two cold-loaded lab help references, one selected-caption index.
// Dependencies/Pointers: Sole TutorialDirector guide AudioSource; exact journal caption/resource pairs; root owns import/playback.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing guide/menu Canvas and accepted real tutorial/world/session owners.
// Array Formats: Cold fixed presentation stores and cached labels; no new gameplay/native memory ownership.
// Dependencies/Pointers: Work-only original UI packet; real pointer/audio/practice/reward/network authorities retained. Native/browser acceptance pending.
