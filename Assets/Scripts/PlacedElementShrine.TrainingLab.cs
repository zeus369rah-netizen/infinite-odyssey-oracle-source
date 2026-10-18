using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class PlacedElementShrine
    {
        TrainingLabDirector trainingLabOwner;
        internal void BindTrainingLab(TrainingLabDirector lab){trainingLabOwner=lab;}
        internal void ClearTrainingLabBinding(){trainingLabOwner=null;}
        bool TrainingLabAuthorized=>!remoteOnly&&Element==1&&trainingLabOwner!=null&&trainingLabOwner.CanAct&&trainingLabOwner.OwnsTower(this);
        bool TryGetTrainingLabTarget(Vector3 origin,out OracleEnemy target)
        {
            target=trainingLabOwner!=null?trainingLabOwner.Dummy:null;
            return TrainingLabAuthorized&&trainingLabOwner.TowerAttacking&&target!=null&&trainingLabOwner.OwnsDummy(target)&&target.Targetable&&
                (target.AimPoint-origin).sqrMagnitude<=TowerForgeDefinitions.Range*TowerForgeDefinitions.Range&&session.Combat.HasLineOfSight(origin,target.AimPoint);
        }
        void ObserveTrainingLabTowerShot(OracleEnemy target,float before)
        {if(TrainingLabAuthorized)trainingLabOwner.ObserveTowerShot(this,target,before,target.HP);}
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Water tower on its supported World pad, one adjacent target, actual LOS/range.
// Array Formats: Cached lab reference; existing saved renderer, cooldown scalar and bounded feedback pool.
// Dependencies/Pointers: Same Update/MatchTowerForgeRules/TakeHit cadence and same SetHealth damage owner; no defense protocol, rewards, tokens or parallel HP ledger.
