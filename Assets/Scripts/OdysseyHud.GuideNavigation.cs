using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class OdysseyHud
    {
        RectTransform practicePrompt;
        UnityEngine.UI.Button practicePromptButton;
        UnityEngine.UI.Text practicePromptKey,practicePromptAction;
        int practicePromptBindingFrame=int.MinValue,practicePromptKind;
        bool practicePromptShown,guideCaptionShown;

        void BuildPracticeInteractionPrompt()
        {
            practicePrompt=Frame(hudRoot,"PracticeInteractCue",new Vector2(.5f,.5f),new Vector2(.5f,.5f),
                Vector2.zero,new Vector2(326,92),new Color(.025f,.035f,.033f,.96f),Gold);
            practicePromptKey=Label(practicePrompt,"ConfiguredInteractKey","",30,Ivory,TextAnchor.MiddleCenter,
                new Vector2(12,0),new Vector2(72,64),new Vector2(0,.5f));
            Border(practicePromptKey.rectTransform,Gold,2);
            practicePromptAction=Label(practicePrompt,"RealPracticeAction","",16,Ivory,TextAnchor.MiddleCenter,
                new Vector2(-12,0),new Vector2(220,72),new Vector2(1,.5f));
            practicePromptButton=practicePrompt.gameObject.AddComponent<UnityEngine.UI.Button>();
            practicePromptButton.targetGraphic=practicePrompt.GetComponent<UnityEngine.UI.Image>();
            practicePromptButton.targetGraphic.raycastTarget=true;
            practicePromptButton.onClick.AddListener(ActivatePracticeInteraction);
            practicePrompt.gameObject.SetActive(false);
        }

        void RefreshSingleTopGuide(bool visible,TutorialDirector tutorial)
        {
            RefreshCompactGuide015(tutorial); // Existing explicitly opened Guide counter/playback state remains current.
            if(tutorialPrompt!=null&&tutorialPrompt.gameObject.activeSelf)tutorialPrompt.gameObject.SetActive(false);
            if(captionPanel!=null&&captionPanel.gameObject.activeSelf)captionPanel.gameObject.SetActive(false);
            guideCaptionShown=false; // Detailed guide presentation belongs to the Pause side card and full Guide panel.
        }

        void RefreshPracticeInteractionPrompt()
        {
            if(practicePrompt==null)return;
            bool context=session!=null&&session.IsPlaying&&currentPanel==JourneyPanel.None&&!ShopVisible&&
                session.World!=null&&session.World.IsTutorialRefuge&&!session.World.IsCoopArena&&session.Player!=null&&
                session.Player.ViewCamera!=null&&!GameActionBindings.IsRebinding;
            int kind=0;Vector3 target=default;bool canActivate=false;
            if(context)
            {
                var tutorial=session.Tutorial;var lab=session.TrainingLab;
                if(tutorial!=null&&tutorial.TowerPracticeInteractionReady)
                {
                    target=session.World.TutorialTowerPracticePosition+Vector3.up*.9f;
                    if((session.Player.transform.position-session.World.TutorialTowerPracticePosition).sqrMagnitude<=36f)
                    {kind=1;canActivate=true;}
                }
                else if(lab!=null&&lab.CanOpen)
                {
                    target=session.World.TrainingLabTowerPosition+Vector3.up*.7f;
                    if((session.Player.ViewCamera.transform.position-target).sqrMagnitude<=30.25f)
                    {kind=2;canActivate=lab.LookingAtControl;}
                }
            }
            Vector2 local=default;
            bool show=kind!=0&&TryPracticePromptPosition(target,out local);
            if(practicePromptShown!=show){practicePromptShown=show;practicePrompt.gameObject.SetActive(show);}
            if(!show){practicePromptKind=0;return;}
            bool changedKind=practicePromptKind!=kind;
            practicePromptKind=kind;
            practicePrompt.anchoredPosition=local;
            practicePromptButton.interactable=canActivate;
            if(practicePromptBindingFrame!=GameActionBindings.LastChangeFrame)
            {practicePromptBindingFrame=GameActionBindings.LastChangeFrame;practicePromptKey.text=GameActionBindings.GetLabel("Interact");}
            string action=kind==1?"START TOWER PRACTICE\nPRESS KEY / TAP":canActivate?"OPEN TRAINING LAB\nPRESS KEY / TAP":"LOOK AT THE TOWER PAD\nTO OPEN TRAINING LAB";
            if(changedKind||practicePromptAction.text!=action)practicePromptAction.text=action;
        }

        bool TryPracticePromptPosition(Vector3 target,out Vector2 local)
        {
            local=default;
            var screen=session.Player.ViewCamera.WorldToScreenPoint(target);
            if(!(screen.z>0f)||float.IsNaN(screen.x)||float.IsInfinity(screen.x)||float.IsNaN(screen.y)||float.IsInfinity(screen.y))return false;
            if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(hudRoot,new Vector2(screen.x,screen.y),null,out local))return false;
            // A bounded projection beside the real target, with the top-right quick portal area clear.
            var bounds=hudRoot.rect;float minX=bounds.xMin+179,maxX=bounds.xMax-179;
            float minY=bounds.yMin+154,maxY=bounds.yMax-(guideCaptionShown?440:320);
            if(maxX<minX||maxY<minY)return false;
            local.x=Mathf.Clamp(local.x,minX,maxX);local.y=Mathf.Clamp(local.y+72,minY,maxY);
            return true;
        }

        void ActivatePracticeInteraction()
        {
            if(session==null||!session.IsPlaying||currentPanel!=JourneyPanel.None||GameActionBindings.IsRebinding)return;
            if(practicePromptKind==1)session.Tutorial?.TryStartTowerPractice();
            else if(practicePromptKind==2)session.TrainingLab?.Open();
            RefreshPracticeInteractionPrompt();
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: actual World metre/Y-up practice anchor -> real ViewCamera screen -> existing overlay Canvas local reference pixels.
// Array Formats: one cold pooled UI cue, cached string references/key revision and scalar guide snapshot; no frame-built collections or duplicate input polling.
// Dependencies/Pointers: legacy HUD owns main-thread UI; real TryStartTowerPractice/TrainingLab.Open revalidate all acceptance. Existing keyboard and touch EventSystem remain; no lesson/HP/ready/save or co-op lab authority changes. Native/browser/GC acceptance pending.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing guide/menu Canvas and accepted real tutorial/world/session owners.
// Array Formats: Cold fixed presentation stores and cached labels; no new gameplay/native memory ownership.
// Dependencies/Pointers: Work-only original UI packet; real pointer/audio/practice/reward/network authorities retained. Native/browser acceptance pending.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing accepted world metres/Y-up, input bits and owner simulation seconds.
// Array Formats: Four ingredient deadlines, fixed48 projectile pool, at most three component shots; existing host frame ABIs.
// Dependencies/Pointers: Work-only rapid-casting candidate. Existing host hit/damage/reward owners retained; root owns native/browser acceptance.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing fitted guide/menu Canvas and actual guide view/audio state.
// Array Formats: Existing bounded UI/state stores; cold immutable65status labels, no frame formatting.
// Dependencies/Pointers: Original restart/portal/settings/narration/voice/input/achievement owners remain. Root owns source apply and full compile/native/browser/listening acceptance.
