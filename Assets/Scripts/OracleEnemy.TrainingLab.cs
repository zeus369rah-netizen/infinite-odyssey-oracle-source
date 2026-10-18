using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class OracleEnemy
    {
        TrainingLabDirector trainingLabOwner;
        bool TrainingLabBound=>!object.ReferenceEquals(trainingLabOwner,null);
        internal void BindTrainingLab(TrainingLabDirector lab){trainingLabOwner=lab;SetPlayerAggro(false,false);}
        internal void ClearTrainingLabBinding(){CancelTrainingLabAttack();trainingLabOwner=null;}
        internal void CancelTrainingLabAttack()
        {
            if(!TrainingLabBound)return;
            windup=strikeRemaining=0f;pendingTower=null;blockingTower=null;
            pendingTowerAttack=pendingTowerVolley=pendingShrine=pendingPeerAttack=false;
            if(warning!=null)warning.gameObject.SetActive(false);SetPlayerAggro(false,false);
        }
        bool PrepareTrainingLabAttack(float dt)
        {
            if(!TrainingLabBound)return true;
            SetPlayerAggro(false,false);
            var tower=trainingLabOwner!=null?trainingLabOwner.Tower:null;
            if(trainingLabOwner==null||!trainingLabOwner.CanAct||!trainingLabOwner.MonsterAttackingTower||tower==null||tower.IsDestroyed)
            {
                CancelTrainingLabAttack();slowRemaining=Mathf.Max(0f,slowRemaining-dt);staggerRemaining=Mathf.Max(0f,staggerRemaining-dt);
                earthVulnerabilityRemaining=Mathf.Max(0f,earthVulnerabilityRemaining-dt);attackCooldown=Mathf.Max(0f,attackCooldown-dt);
                return false;
            }
            // Feed the existing windup/strike/recovery state machine its sole legal victim.
            blockingTower=tower;return true;
        }
        void ObserveTrainingLabHit(float before)
        {if(trainingLabOwner!=null)trainingLabOwner.ObserveDummyDamage(this,before,hp);}
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Fixed spawn foot point; existing local+Z rotation may aim at the adjacent tower, never chase.
// Array Formats: One cached lab/tower reference and existing scalar attack/status clocks; no arrays, target queries or new timer owner.
// Dependencies/Pointers: Existing TakeHit and existing tower windup/strike/recovery damage branches remain authoritative; OFF cancels pending strike immediately, spacing/push movement is suppressed while bound.
