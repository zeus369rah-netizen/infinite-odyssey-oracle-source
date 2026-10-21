using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class OracleCombat
    {
        TrainingLabDirector trainingLabOwner;OracleEnemy trainingLabTarget;
        OracleShrine trainingLabSuppressedShrine;bool trainingLabShrineWasEnabled;
        internal bool TrainingLabActive=>trainingLabOwner!=null&&trainingLabOwner.OwnerContext;
        internal bool IsTrainingLabTarget(OracleEnemy value)=>!object.ReferenceEquals(value,null)&&object.ReferenceEquals(value,trainingLabTarget);
        internal OracleEnemy BeginTrainingLab(TrainingLabDirector lab,Vector3 feet,int prototype)
        {
            if(lab==null||!lab.OwnerContext||!FinitePosition(feet)||prototype<0||prototype>=4||
                !object.ReferenceEquals(trainingLabTarget,null)||!TryAcquireTrainingLabSession(lab))return null;
            // Never displace a living encounter; retired corpses do not permanently lock the lab after real lessons.
            for(int i=0;i<enemies.Count;i++)if(enemies[i]!=null&&enemies[i].Alive)return null;
            RetireAllEnemies015();
            enemies.Clear();targetGrid.Clear();crowdSpacing.Clear();towerAttackSlots.Clear();
            var style=prototype==0?MonsterVoxelCosmetics.Style.GuardianGolem:prototype==1?MonsterVoxelCosmetics.Style.Stalker:
                prototype==2?MonsterVoxelCosmetics.Style.Shade:MonsterVoxelCosmetics.Style.Spider;
            var target=SpawnGuardian(feet,prototype==2,false,visualStyle:style);
            if(target==null)return null;
            target.transform.position=feet;
            Vector3 forward=world.TrainingLabTowerPosition-feet;forward.y=0f;
            if(forward.sqrMagnitude>.0001f)target.transform.rotation=Quaternion.LookRotation(forward);
            target.ConfigureArchetype(prototype==2?OracleEnemy.MonsterArchetype.Shade:prototype==3?OracleEnemy.MonsterArchetype.Spider:OracleEnemy.MonsterArchetype.Stalker);
            target.ConfigureDefenseWave(1f,1f,1f);
            trainingLabTarget=target;target.BindTrainingLab(lab);
            encounterActive=true;return target;
        }
        internal bool TryAcquireTrainingLabSession(TrainingLabDirector lab)
        {
            if(lab==null||!lab.OwnerContext||!initialized||destroying||remoteSimulation||towerPracticeOwned||CosmicNovaTrialActive)return false;
            if(trainingLabOwner!=null)return object.ReferenceEquals(trainingLabOwner,lab);
            for(int i=0;i<enemies.Count;i++)if(enemies[i]!=null&&enemies[i].Alive&&!IsRefugePracticeRepeat020(enemies[i]))return false;
            YieldRefugePracticeToLab020();
            trainingLabOwner=lab;trainingLabSuppressedShrine=Shrine;
            if(trainingLabSuppressedShrine!=null){trainingLabShrineWasEnabled=trainingLabSuppressedShrine.enabled;trainingLabSuppressedShrine.enabled=false;}
            ClearTrainingLabProjectiles(lab);return true;
        }
        internal void ClearTrainingLabProjectiles(TrainingLabDirector lab)
        {
            if(!object.ReferenceEquals(trainingLabOwner,lab))return;
            for(int i=0;i<projectiles.Count;i++)if(projectiles[i]!=null){projectiles[i].Retire();projectiles[i].ClearDangerState();}
            if(!destroying)ClearCosmeticShots();
            if(lab.SharedContext)ApplyRemoteAutoFireState(false);else SetAutoFire(false);
        }
        internal void ReleaseTrainingLabTarget(TrainingLabDirector lab)
        {
            if(!object.ReferenceEquals(trainingLabOwner,lab))return;
            ClearTrainingLabProjectiles(lab);
            var target=trainingLabTarget;trainingLabTarget=null;encounterActive=false;
            if(!object.ReferenceEquals(target,null))enemies.Remove(target);
            if(target!=null){target.ClearTrainingLabBinding();RetireEnemy015(target);}
            if(!destroying){targetGrid?.Clear();crowdSpacing?.Clear();towerAttackSlots.Clear();}
            // Retain this exact lab owner and shrine suppression with zero/one actors.
        }
        internal void ReleaseTrainingLab(TrainingLabDirector lab)
        {
            if(!object.ReferenceEquals(trainingLabOwner,lab))return;
            ReleaseTrainingLabTarget(lab);trainingLabOwner=null;
            if(!destroying&&trainingLabSuppressedShrine!=null)trainingLabSuppressedShrine.enabled=trainingLabShrineWasEnabled;
            trainingLabSuppressedShrine=null;
        }
        void AbortTrainingLab(){if(trainingLabOwner!=null)trainingLabOwner.EndSession();}
        void CountAcceptedNovaLaunch()
        {
            if(TrainingLabActive)trainingLabOwner.ObserveNovaLaunch();else OwnCosmicNovaCastCount++;
        }
        void OnDisable(){SuspendEnemyFeedback022();AbortTrainingLab();}
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Exact refuge feet; model local +Z forward/Y-up; existing 1.20 presentation scale once.
// Array Formats: Existing bounded enemy list, fixed48 gameplay projectile pool and native grids retain their owners; one borrowed lab enemy.
// Dependencies/Pointers: Reward exclusion and accepted Nova routing share the real Combat owner. Cold creation only; synchronous owned cleanup before reset/encounter/disable/disposal.
// # ARCHITECTURE SNAPSHOT
// Existing Y-up/metre lab anchors and fixed actor pools/real HP owners remain.
// Pending four-type selectors apply only on explicit Replace/Add; zero/one actors retain the same Combat reward-exclusion owner.
// Remove/Replace reset both attacks and retire shots before actor mutation; full EndSession alone releases the lab session.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing refuge world metres/Y-up; host-owned shared lab, model local+Z.
// Array Formats: Two native peer records and bounded fixed frames; inherited48 projectile pool and two real actors.
// Dependencies/Pointers: Dedicated shared owner validates requests/fences; guests remote-only; no rewards, migration or narrator; root integrates complete slice.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing world metres and original actor-local+Z/Y-up; pooled lifetime only.
// Array Formats: Exactly48 persistent actor slots/four retained original templates/17 reusable part slots per actor.
// Dependencies/Pointers: Existing HP/projectile/attack owners remain; root integrates guarded hunks; native/Burst/memory acceptance pending.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing authoritative legacy Combat/World/revision and actor pool lifetime, world metres/Y-up.
// Array Formats: Existing48 actors with bounded feedback values/Morton jobs and one synchronous accepted-damage observation; no extra damage, mesh or material owner.
// Dependencies/Pointers: Positive actual HP loss owns immediate cached MPB red flash and observer dispatch. Root composes this exact work projection and verifies native collision/flash/audio/network/GC; replicas never move authoritatively.
