using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class OracleCombat
    {
        OracleEnemy towerPracticeOwnedTarget;
        OracleShrine towerPracticeSuppressedShrine;
        bool towerPracticeShrineWasEnabled;
        bool towerPracticeOwned;
        internal bool TutorialTowerPracticeActive
        {
            get
            {
                var context=GameSession.Instance;
                return !destroying&&towerPracticeOwned&&towerPracticeOwnedTarget!=null&&context!=null&&
                    object.ReferenceEquals(context.Combat,this)&&context.Tutorial!=null&&context.Tutorial.TowerPracticeCanAct;
            }
        }
        internal bool IsTutorialTowerPracticeTarget(OracleEnemy enemy)=>towerPracticeOwned&&
            !object.ReferenceEquals(enemy,null)&&object.ReferenceEquals(enemy,towerPracticeOwnedTarget);
        internal OracleEnemy BeginTutorialTowerPractice(Vector3 footPoint)
        {
            var context=GameSession.Instance;
            if(!initialized||destroying||remoteSimulation||towerPracticeOwned||trainingLabOwner!=null||CosmicNovaTrialActive||context==null||
                context.Tutorial==null||!context.Tutorial.TowerPracticeOwnerContext||context.World==null||
                context.Tutorial.TowerPracticePhase!=TutorialTowerPracticePhase.Preparing||
                !object.ReferenceEquals(context.Combat,this)||!FinitePosition(footPoint))return null;
            ClearEnemies();
            var target=SpawnGuardian(footPoint,false,false);
            if(target==null)return null;
            // World supplies the exact supported foot anchor; remove the legacy spawn helper's lift once.
            target.transform.position=footPoint;
            towerPracticeOwnedTarget=target;towerPracticeOwned=true;encounterActive=false;
            towerPracticeSuppressedShrine=Shrine;
            if(towerPracticeSuppressedShrine!=null)
            {towerPracticeShrineWasEnabled=towerPracticeSuppressedShrine.enabled;towerPracticeSuppressedShrine.enabled=false;}
            return target;
        }
        internal void EndTutorialTowerPractice()
        {
            var target=towerPracticeOwnedTarget;towerPracticeOwned=false;towerPracticeOwnedTarget=null;
            if(!destroying&&towerPracticeSuppressedShrine!=null)towerPracticeSuppressedShrine.enabled=towerPracticeShrineWasEnabled;
            towerPracticeSuppressedShrine=null;
            if(!object.ReferenceEquals(target,null))enemies.Remove(target);
            if(target!=null)RetireEnemy015(target);
            if(!destroying){targetGrid?.Clear();crowdSpacing?.Clear();towerAttackSlots.Clear();}
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Existing OracleCombat owns its one fixed real enemy and existing native grid/crowd stores; no parallel HP/damage owner.
// Exact World foot anchor, same TakeHit authority, zero rewards through existing defeat exclusion; teardown never allocates a replacement root.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing world metres and original actor-local+Z/Y-up; pooled lifetime only.
// Array Formats: Exactly48 persistent actor slots/four retained original templates/17 reusable part slots per actor.
// Dependencies/Pointers: Existing HP/projectile/attack owners remain; root integrates guarded hunks; native/Burst/memory acceptance pending.
