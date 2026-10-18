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
            if(lab==null||!lab.OwnerContext||!initialized||destroying||remoteSimulation||trainingLabOwner!=null||
                towerPracticeOwned||CosmicNovaTrialActive||!FinitePosition(feet)||prototype<0||prototype>=4)return null;
            // Never displace a living encounter; retired corpses do not permanently lock the lab after real lessons.
            for(int i=0;i<enemies.Count;i++)if(enemies[i]!=null&&enemies[i].Alive)return null;
            for(int i=0;i<enemies.Count;i++)if(enemies[i]!=null){enemies[i].gameObject.SetActive(false);Destroy(enemies[i].gameObject);}
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
            trainingLabOwner=lab;trainingLabTarget=target;target.BindTrainingLab(lab);
            trainingLabSuppressedShrine=Shrine;
            if(trainingLabSuppressedShrine!=null){trainingLabShrineWasEnabled=trainingLabSuppressedShrine.enabled;trainingLabSuppressedShrine.enabled=false;}
            encounterActive=true;return target;
        }
        internal void ReleaseTrainingLab(TrainingLabDirector lab)
        {
            if(!object.ReferenceEquals(trainingLabOwner,lab))return;
            var target=trainingLabTarget;trainingLabTarget=null;trainingLabOwner=null;encounterActive=false;
            if(!destroying&&trainingLabSuppressedShrine!=null)trainingLabSuppressedShrine.enabled=trainingLabShrineWasEnabled;
            trainingLabSuppressedShrine=null;
            if(!object.ReferenceEquals(target,null))enemies.Remove(target);
            if(target!=null){target.ClearTrainingLabBinding();target.gameObject.SetActive(false);Destroy(target.gameObject);}
            // A lab projectile must never outlive reward exclusion and hit a later encounter.
            for(int i=0;i<projectiles.Count;i++)if(projectiles[i]!=null){projectiles[i].Retire();projectiles[i].ClearDangerState();}
            if(!destroying){targetGrid?.Clear();crowdSpacing?.Clear();towerAttackSlots.Clear();ClearCosmeticShots();}
            SetAutoFire(false);
        }
        void AbortTrainingLab(){if(trainingLabOwner!=null)trainingLabOwner.EndSession();}
        void CountAcceptedNovaLaunch()
        {
            if(TrainingLabActive)trainingLabOwner.ObserveNovaLaunch();else OwnCosmicNovaCastCount++;
        }
        void OnDisable(){AbortTrainingLab();}
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Exact refuge feet; model local +Z forward/Y-up; existing 1.20 presentation scale once.
// Array Formats: Existing bounded enemy list, fixed48 gameplay projectile pool and native grids retain their owners; one borrowed lab enemy.
// Dependencies/Pointers: Reward exclusion and accepted Nova routing share the real Combat owner. Cold creation only; synchronous owned cleanup before reset/encounter/disable/disposal.
