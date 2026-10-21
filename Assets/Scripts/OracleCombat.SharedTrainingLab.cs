using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class OracleCombat
    {
        internal bool SharedLabRemote=>remoteSimulation;
        internal void ResetSharedLabInput()
        {
            ResetHeldFire015();selection.Clear();hasRemoteSelection=false;remoteSelectionSequence=0;autoSelectionWait.Clear();
            ApplyRemoteAutoFireState(false);RefreshSelectionStatus();
        }
        internal void ApplySharedLabInput(SharedLabFrame state)
        {
            ApplyRemoteSelectionState(state.Prepared,(int)state.Mask,state.Dominant,state.SelectionSequence);
            ApplyRemoteAutoFireState(state.Auto!=0);
            float r0=Mathf.Clamp(state.R0,0,5),r1=Mathf.Clamp(state.R1,0,5),r2=Mathf.Clamp(state.R2,0,5),r3=Mathf.Clamp(state.R3,0,5);
            CooldownRemaining[0]=ElementSwitchCooldowns[0]=r0;CooldownRemaining[1]=ElementSwitchCooldowns[1]=r1;
            CooldownRemaining[2]=ElementSwitchCooldowns[2]=r2;CooldownRemaining[3]=ElementSwitchCooldowns[3]=r3;
            CooldownDuration[0]=r0>4?5:4;CooldownDuration[1]=r1>4?5:4;CooldownDuration[2]=r2>4?5:4;CooldownDuration[3]=r3>4?5:4;
        }
        internal OracleEnemy BeginTrainingLabReplica(TrainingLabDirector lab,Vector3 feet,int prototype)
        {
            if(lab==null||!lab.SharedReplicaApplying||!remoteSimulation||!initialized||destroying||prototype<0||prototype>3||
                !FinitePosition(feet)||trainingLabTarget!=null||trainingLabOwner!=null&&!object.ReferenceEquals(trainingLabOwner,lab))return null;
            if(trainingLabOwner==null)
            {
                trainingLabOwner=lab;trainingLabSuppressedShrine=Shrine;
                if(Shrine!=null){trainingLabShrineWasEnabled=Shrine.enabled;Shrine.enabled=false;}
            }
            var style=prototype==0?MonsterVoxelCosmetics.Style.GuardianGolem:prototype==1?MonsterVoxelCosmetics.Style.Stalker:
                prototype==2?MonsterVoxelCosmetics.Style.Shade:MonsterVoxelCosmetics.Style.Spider;
            var replica=SpawnGuardian(feet,prototype==2,false,visualStyle:style);
            if(replica==null)return null;
            replica.ConfigureArchetype(prototype==2?OracleEnemy.MonsterArchetype.Shade:prototype==3?OracleEnemy.MonsterArchetype.Spider:OracleEnemy.MonsterArchetype.Stalker);
            replica.ConfigureDefenseWave(1,1,1);replica.BindTrainingLab(lab);trainingLabTarget=replica;encounterActive=true;return replica;
        }
        internal void SharedLabTowerFeedback(Vector3 target)
        {if(remoteSimulation&&FinitePosition(target)&&trainingLabOwner!=null&&trainingLabOwner.Tower!=null)trainingLabOwner.Tower.PresentSharedLabShot(target);}
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing world metres/Y-up and exact four prototype style mapping; no authority granted by presentation.
// Array Formats: Existing cached input arrays and fixed48 gameplay/cosmetic pools reused, one owned remote target.
// Dependencies/Pointers: Replica creation is callable only during validated lab snapshot reconciliation; remoteSimulation rejects damage, actual host retains launch/TakeHit/reward exclusion.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing accepted world metres/Y-up, input bits and owner simulation seconds.
// Array Formats: Four ingredient deadlines, fixed48 projectile pool, at most three component shots; existing host frame ABIs.
// Dependencies/Pointers: Work-only rapid-casting candidate. Existing host hit/damage/reward owners retained; root owns native/browser acceptance.
