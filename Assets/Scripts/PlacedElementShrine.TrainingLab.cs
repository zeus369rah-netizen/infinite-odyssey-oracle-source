using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class PlacedElementShrine
    {
        TrainingLabDirector trainingLabOwner;
        internal void BindTrainingLab(TrainingLabDirector lab){trainingLabOwner=lab;}
        internal void ClearTrainingLabBinding(){ClearTrainingLabTargetReferences();trainingLabOwner=null;}
        internal void ClearTrainingLabTargetReferences()
        {
            for(int i=0;i<secondaryTargets.Length;i++)secondaryTargets[i]=null;
        }
        bool SharedTrainingLabReplica=>remoteOnly&&trainingLabOwner!=null&&trainingLabOwner.SharedClient;
        internal void PresentSharedLabShot(Vector3 point){if(SharedTrainingLabReplica&&Finite(point))ShowShotFeedback(point,point,point,0);}
        bool TrainingLabAuthorized=>!remoteOnly&&TowerForgeDefinitions.ValidType(Element)&&trainingLabOwner!=null&&trainingLabOwner.CanAct&&trainingLabOwner.OwnsTower(this);
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
// Coordinate Space: Selected tier-one core tower on its supported World pad, one adjacent target, actual LOS/range.
// Array Formats: Cached lab reference; existing saved renderer, cooldown scalar and bounded feedback pool.
// Dependencies/Pointers: Same Update/MatchTowerForgeRules/TakeHit cadence and same SetHealth damage owner; no defense protocol, rewards, tokens or parallel HP ledger.
// # ARCHITECTURE SNAPSHOT
// Existing Y-up/metre lab anchors and fixed actor pools/real HP owners remain.
// Pending four-type selectors apply only on explicit Replace/Add; zero/one actors retain the same Combat reward-exclusion owner.
// Remove/Replace reset both attacks and retire shots before actor mutation; full EndSession alone releases the lab session.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing refuge world metres/Y-up; host-owned shared lab, model local+Z.
// Array Formats: Two native peer records and bounded fixed frames; inherited48 projectile pool and two real actors.
// Dependencies/Pointers: Dedicated shared owner validates requests/fences; guests remote-only; no rewards, migration or narrator; root integrates complete slice.
