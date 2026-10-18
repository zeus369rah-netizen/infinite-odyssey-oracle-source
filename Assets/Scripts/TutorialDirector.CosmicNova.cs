using UnityEngine;
using UnityEngine.InputSystem;

namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // Explicit offline lesson after the real guardian. Two acknowledged teaching
    // pages precede five owned reward-free actors. Only Combat's actual positive
    // trial-impact event completes practice; selection, timers and kills do not.
    // The combat owner retains all casting/HP/reward rules. No profile writes.
    public sealed partial class TutorialDirector
    {
        public const string NovaPrepareLessonLine="The practice targets appear after both explanations. Select Fire, Water, Earth, and Air before launching. Three selected elements cannot cast. Add the fourth, aim into the group, and left click for Cosmic Nova. You do not need to launch earlier pairs.";
        public const string NovaSplashLessonLine="Cosmic Nova uses all four elements and gives them five seconds of recovery after a successful launch. Normal singles and pairs recover for four seconds. Every new element tap resets auto fire's half second wait. Aim into the group. This practice finishes only when your Nova really hits a target.";
        AudioClip novaPrepareClip,novaSplashClip;
        OracleCombat novaTrialCombat;
        int novaTeachingStep,novaInitialCastCount,novaInitialResolutionCount,novaObservedCastCount,novaBindingFrame=-1,novaHintState=int.MinValue;
        float novaLaunchHintRemaining,novaSuccessHoldRemaining;
        bool novaTrialOwned,novaImpactAccepted,novaSetupFailed;
        string novaInteract,novaElementKeys;

        void InitializeNovaLesson()
        {
            novaPrepareClip=Resources.Load<AudioClip>("Tutorial/lesson_nova_prepare_rapid150_v1");
            novaSplashClip=Resources.Load<AudioClip>("Tutorial/lesson_nova_splash_rapid150_v1");
        }
        void BeginNovaTeaching()
        {
            if(sharedPractice||session.CoopMode){BeginEcho();return;}
            ResetNovaLesson();GuideHighlightElement=-1;CurrentLesson=Lesson.NovaTeaching;
            session.CombatUnlocked=false;session.World.LockPortal();
            session.Objective="Learn Cosmic Nova, then land its splash on a practice target.";
            PlayNovaTeachingStep(0);
        }
        void PlayNovaTeachingStep(int step)
        {
            novaTeachingStep=Mathf.Clamp(step,0,1);lessonTime=0; novaHintState=int.MinValue;
            Heading=novaTeachingStep==0?"Cosmic Nova · 1/2 · Select all four":"Cosmic Nova · 2/2 · Land the splash";
            AudioClip clip=novaTeachingStep==0?novaPrepareClip:novaSplashClip;
            string caption=novaTeachingStep==0?NovaPrepareLessonLine:NovaSplashLessonLine;
            narrationDuration=clip!=null?clip.length+.7f:CaptionDuration(caption);
            PlayNarration(clip,caption);RefreshNovaBindings();UpdateNovaTeachingHint(false);
        }
        void RefreshNovaBindings()
        {
            if(novaInteract!=null&&novaBindingFrame==AbilityBindings.LastChangeFrame)return;
            novaBindingFrame=AbilityBindings.LastChangeFrame;novaHintState=int.MinValue;
            novaInteract="["+GameActionBindings.GetLabel("Interact")+"]";
            novaElementKeys="["+AbilityBindings.GetLabel(0)+"] Fire, ["+AbilityBindings.GetLabel(1)+"] Water, ["+
                AbilityBindings.GetLabel(2)+"] Earth, ["+AbilityBindings.GetLabel(3)+"] Air";
        }
        void TickNovaLesson(float dt)
        {
            RefreshNovaBindings();lessonTime+=dt;
            var keyboard=Keyboard.current;
            bool acknowledge=keyboard!=null&&!GameActionBindings.IsRebinding&&keyboard[GameActionBindings.GetKey("Interact")].wasPressedThisFrame;
            if(CurrentLesson==Lesson.NovaTeaching)
            {
                bool ready=lessonTime>=narrationDuration;UpdateNovaTeachingHint(ready);
                if(!ready||!acknowledge)return;
                if(novaTeachingStep==0)PlayNovaTeachingStep(1);else BeginNovaPractice();
                return;
            }
            var combat=novaTrialCombat;
            if(novaImpactAccepted)
            {
                // Keep the accepted1.2s splash alive before EndTrial resets its pool.
                // This uses advancing lesson time, so Pause/focus loss freezes the hold.
                Heading="Cosmic Nova · Practice complete";
                Instruction="Nova impact confirmed. Watch the splash; the next lesson follows.";
                novaSuccessHoldRemaining=Mathf.Max(0f,novaSuccessHoldRemaining-dt);
                if(novaSuccessHoldRemaining<=0f){ReleaseNovaTrial();BeginTowerPracticeLesson();}
                return;
            }
            if(!novaTrialOwned||combat==null||!combat.CosmicNovaTrialActive||combat.CosmicNovaTrialAliveCount<=0)
            {
                if(novaHintState!=-2)
                {
                    novaHintState=-2;
                    Instruction="Practice interrupted or targets cleared. Press "+novaInteract+" to reset the five targets. No XP or Heartstones.";
                }
                if(acknowledge)BeginNovaPractice();
                return;
            }
            int mask=combat.ConfirmedMask,selected=SpellIngredientRules.Count(mask),alive=combat.CosmicNovaTrialAliveCount;
            int recovery=0;
            for(int i=0;i<4;i++)recovery=Mathf.Max(recovery,Mathf.CeilToInt(combat.CooldownRemaining[i]));
            if(combat.OwnCosmicNovaCastCount>novaObservedCastCount){novaObservedCastCount=combat.OwnCosmicNovaCastCount;novaLaunchHintRemaining=.9f;}
            else novaLaunchHintRemaining=Mathf.Max(0,novaLaunchHintRemaining-dt);
            bool launched=novaLaunchHintRemaining>0;
            int state=(mask&15)|(alive<<4)|(combat.CosmicNovaPrepared?1<<7:0)|(launched?1<<8:0)|
                (combat.AutoFireEnabled?1<<9:0)|(Mathf.Clamp(recovery,0,5)<<10);
            if(state==novaHintState)return;novaHintState=state;
            Heading="Cosmic Nova · FREE PRACTICE · "+alive+" / 5 targets remain";
            Instruction=launched?"Nova launched. Completion waits for a real impact on a practice target."
                :selected==3?"Three elements remain highlighted but cannot cast. Add the fourth for Cosmic Nova."
                :mask==15&&combat.CosmicNovaPrepared?"All four ingredients are ready. Aim into the group and left-click Cosmic Nova. Auto-fire waits half a second after your latest element tap."
                :mask==15?"All four selected. Wait for ingredient recovery: "+recovery+"s, then aim and cast."
                :"Select "+novaElementKeys+" before launching. Every tap restarts auto's half-second wait; aim at the middle target.";
        }
        void UpdateNovaTeachingHint(bool ready)
        {
            int state=novaTeachingStep*2+(ready?1:0)+(novaSetupFailed?8:0);if(state==novaHintState)return;novaHintState=state;
            Instruction=novaSetupFailed?"Practice could not start. Press "+novaInteract+" to retry. No reward or point was granted."
                :!ready?"Read or listen first. No practice targets or attacks are enabled yet."
                :novaTeachingStep==0?"Press "+novaInteract+" for the second explanation. The highlighted elements are a preview."
                :"Press "+novaInteract+" to create five free practice targets. Select all four before launching Cosmic Nova; three selected elements cannot cast.";
        }
        void BeginNovaPractice()
        {
            ReleaseNovaTrial();novaImpactAccepted=false;novaSetupFailed=false;
            var combat=session!=null?session.Combat:null;
            if(combat==null||session.CoopMode||combat.CosmicNovaTrialActive)
            {NovaSetupFailed();return;}
            novaTrialCombat=combat;combat.CosmicNovaResolved+=ObserveNovaTrialImpact;
            if(!combat.BeginCosmicNovaTrial())
            {ReleaseNovaTrial();NovaSetupFailed();return;}
            novaTrialOwned=true;novaInitialCastCount=novaObservedCastCount=combat.OwnCosmicNovaCastCount;novaInitialResolutionCount=combat.CosmicNovaResolvedCount;novaLaunchHintRemaining=0;
            CurrentLesson=Lesson.NovaPractice;novaHintState=int.MinValue;lessonTime=0;
            session.CombatUnlocked=true;session.World.LockPortal();
            session.Objective="FREE PRACTICE · Land Cosmic Nova on a practice target. No XP or Heartstones.";
            // BeginCosmicNovaTrial never resumes or locks the pointer. This path was
            // acknowledged in already-running gameplay, so no hidden Resume call.
        }
        void NovaSetupFailed()
        {
            CurrentLesson=Lesson.NovaTeaching;novaTeachingStep=1;novaSetupFailed=true;
            lessonTime=narrationDuration;novaHintState=int.MinValue;session.CombatUnlocked=false;UpdateNovaTeachingHint(true);
        }
        void ObserveNovaTrialImpact(Vector3 point,int damagedTargets)
        {
            var combat=novaTrialCombat;
            if(novaImpactAccepted||!active||CurrentLesson!=Lesson.NovaPractice||!novaTrialOwned||combat==null||session==null||session.CoopMode||
                !combat.CosmicNovaTrialActive||damagedTargets<=0||combat.OwnCosmicNovaCastCount<=novaInitialCastCount||combat.CosmicNovaResolvedCount<=novaInitialResolutionCount)return;
            novaImpactAccepted=true;novaSuccessHoldRemaining=1.25f;
            // The accepted event starts presentation time only; no key/timer/kill
            // can start it, and a duplicate event cannot restart the hold.
        }
        void ReleaseNovaTrial()
        {
            var combat=novaTrialCombat;
            if(!object.ReferenceEquals(combat,null))combat.CosmicNovaResolved-=ObserveNovaTrialImpact;
            if(novaTrialOwned&&combat!=null)combat.EndCosmicNovaTrial();
            novaTrialCombat=null;novaTrialOwned=false;novaImpactAccepted=false;novaSuccessHoldRemaining=0f;
        }
        void ResetNovaLesson()
        {
            ReleaseNovaTrial();novaSetupFailed=false;novaTeachingStep=novaInitialCastCount=novaInitialResolutionCount=novaObservedCastCount=0;novaLaunchHintRemaining=0;
            novaBindingFrame=-1;novaHintState=int.MinValue;
        }
        void SuspendNovaLesson()
        {
            if(CurrentLesson!=Lesson.NovaPractice)return;
            ReleaseNovaTrial();novaHintState=int.MinValue;
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: existing offline trial actors and real impact events in Unity metres.
// Array Formats: fixed four-bit selection and four recovery slots; bounded cached hint state.
// Dependencies/Pointers: actual positive owned-trial collision remains the only completion gate; no lesson, HP, recovery, profile or authority bypass.

// # ARCHITECTURE SNAPSHOT
// Bounded offline tutorial adapter; existing actor/HP/tower/native owners and reward boundaries remain.
