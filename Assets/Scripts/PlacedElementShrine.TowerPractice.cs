using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class PlacedElementShrine
    {
        TutorialDirector tutorialPracticeLesson;
        OracleEnemy tutorialPracticeTarget;
        internal void BindTutorialPractice(TutorialDirector lesson,OracleEnemy target)
        {tutorialPracticeLesson=lesson;tutorialPracticeTarget=target;}
        internal void ClearTutorialPracticeBinding(){tutorialPracticeLesson=null;tutorialPracticeTarget=null;}
        bool TutorialPracticeAuthorized=>!remoteOnly&&Element==1&&session!=null&&session.Combat!=null&&
            tutorialPracticeLesson!=null&&tutorialPracticeTarget!=null&&tutorialPracticeLesson.TowerPracticeCanAct&&
            tutorialPracticeLesson.OwnsTowerPracticeActors(this,tutorialPracticeTarget)&&
            session.Combat.IsTutorialTowerPracticeTarget(tutorialPracticeTarget);
        bool TryGetTutorialPracticeTarget(Vector3 origin,out OracleEnemy target)
        {
            target=tutorialPracticeTarget;
            return TutorialPracticeAuthorized&&target!=null&&target.Targetable&&
                (target.AimPoint-origin).sqrMagnitude<=TowerForgeDefinitions.Range*TowerForgeDefinitions.Range&&
                session.Combat.HasLineOfSight(origin,target.AimPoint);
        }
        void ObserveTutorialPracticeDamage(OracleEnemy target,float before)
        {
            if(TutorialPracticeAuthorized)tutorialPracticeLesson.ObserveTowerPracticeDamage(this,target,before);
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Cached one-target tutorial adapter; same existing Update cadence, saved renderer and OracleEnemy.TakeHit remain authoritative.
// Offline/context/actor identity and exact LOS gates; no new spatial search loop, network event, inventory or native owner.
