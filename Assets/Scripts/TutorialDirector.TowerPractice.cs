using UnityEngine;
using UnityEngine.InputSystem;

namespace OdysseyOracle
{
    public sealed partial class TutorialDirector
    {
        public const string TowerPracticeLessonLine="This is offline tower practice in the left alcove. A Water tower will fire at one fixed practice target. Watch its shots defeat the target. Tier one is included. Practice spends no build tokens, Forge charge or Heartcrystals, and grants no XP or rewards. Your own casting stays locked during this demonstration.";
        AudioClip towerPracticeClip;
        OracleCombat towerPracticeCombat;
        ShrinePlacementBuilder towerPracticeBuilder;
        PlacedElementShrine towerPracticeTower;
        OracleEnemy towerPracticeTarget;
        TutorialTowerPracticePhase towerPracticePhase;
        float towerPracticeCompleteHold;
        public TutorialTowerPracticePhase TowerPracticePhase=>towerPracticePhase;
        public int TowerPracticeAcceptedHits{get;private set;}
        public int TowerPracticeAcceptedDefeats{get;private set;}
        public bool TowerPracticeCompleted=>towerPracticePhase==TutorialTowerPracticePhase.Complete;
        internal bool TowerPracticeOwnerContext=>session!=null&&session.World!=null&&session.World.Mode020==WorldMode020.TutorialRefuge&&
            TutorialTowerPracticeRules.CanOwn(active&&CurrentLesson==Lesson.TowerPractice,Finished,session.HasStarted,
                session.CoopMode,session.Rooms!=null&&session.Rooms.Connected,session.World.IsCoopArena,
                session.Defense!=null&&session.Defense.Running,session.IsDead,session.IsDowned,session.Victory);
        internal bool TowerPracticeCanAct=>TutorialTowerPracticeRules.CanAct(towerPracticePhase,TowerPracticeOwnerContext,
            session!=null&&session.IsPlaying&&CanAdvanceTime());
        internal bool OwnsTowerPracticeActors(PlacedElementShrine tower,OracleEnemy target)=>
            towerPracticePhase==TutorialTowerPracticePhase.Running&&TowerPracticeOwnerContext&&
            object.ReferenceEquals(tower,towerPracticeTower)&&object.ReferenceEquals(target,towerPracticeTarget)&&
            towerPracticeCombat!=null&&object.ReferenceEquals(towerPracticeCombat,session.Combat);

        void InitializeTowerPracticeLesson()=>towerPracticeClip=Resources.Load<AudioClip>("Tutorial/lesson_tower_practice");
        void BeginTowerPracticeLesson()
        {
            ResetTowerPracticeLesson();TowerPracticeAcceptedDefeats=0;
            if(session==null||session.CoopMode||session.Rooms!=null&&session.Rooms.Connected){BeginEcho();return;}
            CurrentLesson=Lesson.TowerPractice;towerPracticePhase=TutorialTowerPracticePhase.Instruction;lessonTime=0f;
            session.CombatUnlocked=false;session.World.LockPortal();Heading="Offline tower practice · Water tower";
            session.Objective="Walk to the left tower alcove. Observe one real Water tower defeat its fixed practice target.";
            narrationDuration=towerPracticeClip!=null?towerPracticeClip.length+.7f:CaptionDuration(TowerPracticeLessonLine);
            PlayNarration(towerPracticeClip,TowerPracticeLessonLine);
            Instruction="Offline only. Read or listen, then walk to the left tower alcove and press Interact to begin. No rewards or purchases.";
        }

        public bool TryStartTowerPractice()
        {
            if(!TowerPracticeOwnerContext||towerPracticePhase!=TutorialTowerPracticePhase.Instruction||
                session.Combat==null||session.Builder==null||session.Player==null||!session.IsPlaying||
                lessonTime<narrationDuration||
                (session.Player.transform.position-session.World.TutorialTowerPracticePosition).sqrMagnitude>36f)return false;
            ReleaseTowerPracticeActors();TowerPracticeAcceptedHits=0;towerPracticePhase=TutorialTowerPracticePhase.Preparing;
            towerPracticeCombat=session.Combat;towerPracticeBuilder=session.Builder;
            towerPracticeTarget=towerPracticeCombat.BeginTutorialTowerPractice(session.World.TutorialTowerPracticeTargetPosition);
            if(towerPracticeTarget==null||!towerPracticeBuilder.TryCreateTutorialPracticeTower(towerPracticeTarget,out towerPracticeTower))
            {
                ReleaseTowerPracticeActors();towerPracticePhase=TutorialTowerPracticePhase.Instruction;
                Instruction="Practice could not start. Keep the alcove clear and press Interact to retry. No token or reward changed.";
                return false;
            }
            towerPracticePhase=TutorialTowerPracticePhase.Running;
            Instruction="Watch the Water tower's real shots defeat the fixed target. Your casting is locked. No XP or purchases.";
            return true;
        }

        void TickTowerPracticeLesson(float dt)
        {
            lessonTime+=dt;
            if(towerPracticePhase==TutorialTowerPracticePhase.Instruction)
            {
                var keyboard=Keyboard.current;
                if(keyboard!=null&&!GameActionBindings.IsRebinding&&keyboard[GameActionBindings.GetKey("Interact")].wasPressedThisFrame)TryStartTowerPractice();
                return;
            }
            if(towerPracticePhase==TutorialTowerPracticePhase.Complete)
            {
                towerPracticeCompleteHold=Mathf.Max(0f,towerPracticeCompleteHold-dt);
                if(towerPracticeCompleteHold<=0f){ResetTowerPracticeLesson();BeginEcho();}
                return;
            }
            if(towerPracticePhase==TutorialTowerPracticePhase.Running&&
                (towerPracticeTower==null||towerPracticeTarget==null||!towerPracticeTarget.Alive||
                 !towerPracticeBuilder.OwnsTutorialPracticeTower(towerPracticeTower)))
            {
                ReleaseTowerPracticeActors();towerPracticePhase=TutorialTowerPracticePhase.Instruction;
                Instruction="Practice was interrupted. Press Interact in the left alcove to reset the one tower and target. No rewards.";
            }
        }

        internal void ObserveTowerPracticeDamage(PlacedElementShrine tower,OracleEnemy target,float before)
        {
            if(!OwnsTowerPracticeActors(tower,target)||!TowerPracticeCanAct||target==null||
                float.IsNaN(before)||float.IsInfinity(before)||float.IsNaN(target.HP)||float.IsInfinity(target.HP)||target.HP>=before)return;
            TowerPracticeAcceptedHits++;
            if(!TutorialTowerPracticeRules.AcceptedTowerDefeat(true,true,before,target.HP))return;
            TowerPracticeAcceptedDefeats=1;towerPracticePhase=TutorialTowerPracticePhase.Complete;towerPracticeCompleteHold=1.25f;
            Heading="Tower practice complete";Instruction="The real Water tower defeated its target. Practice granted no XP or currency.";
        }

        void UpdateTowerPracticeLifetime()
        {
            if(towerPracticePhase==TutorialTowerPracticePhase.Inactive||TowerPracticeOwnerContext)return;
            // Offline practice cannot continue after joining a shared session.
            // Temporary interruptions keep an explicit retry state instead.
            if(active&&!Finished&&CurrentLesson==Lesson.TowerPractice&&session!=null&&
                (session.CoopMode||session.Rooms!=null&&session.Rooms.Connected))
            {BeginEcho();return;}
            SuspendTowerPracticeLesson();
        }
        void ReleaseTowerPracticeActors()
        {
            var builder=towerPracticeBuilder;var combat=towerPracticeCombat;
            towerPracticeBuilder=null;towerPracticeCombat=null;
            if(builder!=null)builder.ReleaseTutorialPracticeTower(towerPracticeTower);
            if(combat!=null)combat.EndTutorialTowerPractice();
            towerPracticeTower=null;towerPracticeTarget=null;
        }
        void ResetTowerPracticeLesson()
        {
            towerPracticePhase=TutorialTowerPracticePhase.Inactive;
            ReleaseTowerPracticeActors();TowerPracticeAcceptedHits=0;towerPracticeCompleteHold=0f;
        }
        void SuspendTowerPracticeLesson()
        {
            bool canRetry=active&&!Finished&&CurrentLesson==Lesson.TowerPractice;
            ResetTowerPracticeLesson();
            if(canRetry){towerPracticePhase=TutorialTowerPracticePhase.Instruction;Instruction="Practice paused and cleared. Press Interact in the left alcove to restart. No rewards or purchases.";}
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Existing metre/Y-up/XZ World supplies exact tower/target floor anchors; this lesson owns only two borrowed actor references.
// Offline-only bounded lifetime; actual HP/shot owners decide outcomes. No wallet/Forge/build-token/reward or native owner is created.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: existing offline tutorial lesson/actor lifetime only; no spatial math changes.
// Array Formats: existing scalar phase/context and borrowed tower/target references.
// Dependencies/Pointers: temporary loss uses the existing cleanup/retry owner; shared-session ineligibility follows existing BeginEcho, never grants completion.
