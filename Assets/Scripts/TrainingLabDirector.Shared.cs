using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class TrainingLabDirector
    {
        internal bool SharedDispatching,SharedReplicaApplying;
        TrainingLabSnapshot sharedSnapshot;
        uint appliedDummyGeneration,appliedTowerGeneration;
        internal CoopTrainingLabDirector Shared=>session!=null?session.SharedTrainingLab:null;
        internal bool SharedContext=>Shared!=null&&Shared.ContextActive;
        internal bool SharedAuthority=>Shared!=null&&Shared.IsAuthorityContext;
        internal bool SharedClient=>Shared!=null&&Shared.ClientReady;
        internal bool PrepareSharedCastSession()=>SharedAuthority&&HoldLabSession();
        internal bool SharedRequest(SharedLabControl control,uint argument,out bool result)
        {
            result=false;if(!SharedContext||SharedDispatching)return false;
            result=Shared.RequestControl(control,argument);return true;
        }
        internal bool ExecuteSharedControl(SharedLabControl command,uint argument)
        {
            if(!SharedAuthority||argument>15)return false;
            SharedDispatching=true;
            try
            {
                switch(command)
                {
                    case SharedLabControl.ReplaceMonster:selectedPrototype=(int)argument;return ReplaceMonster();
                    case SharedLabControl.RemoveMonster:return RemoveMonster();
                    case SharedLabControl.ReplaceTower:selectedTowerType=(int)argument;return ReplaceTower();
                    case SharedLabControl.RemoveTower:return RemoveTower();
                    case SharedLabControl.RemoveAll:RemoveAllOwnedActors();return true;
                    case SharedLabControl.SpawnPair:selectedPrototype=(int)(argument&3);selectedTowerType=(int)(argument>>2);return SpawnDummy();
                    case SharedLabControl.TowerAttack:return argument<=1&&SetTowerAttacking(argument!=0);
                    case SharedLabControl.MonsterAttack:return argument<=1&&SetMonsterAttackingTower(argument!=0);
                    default:return false;
                }
            }
            finally{SharedDispatching=false;}
        }
        internal void ApplySharedReplica(SharedLabFrame frame)
        {
            if(!SharedClient||!SharedLabCastMath.StateShape(frame))return;
            SharedReplicaApplying=true;
            try
            {
                if((frame.Flags&1)==0){if(dummy!=null)ReleaseLabMonster();appliedDummyGeneration=frame.DummyGeneration;}
                else if(dummy==null||appliedDummyGeneration!=frame.DummyGeneration||activePrototype!=frame.Prototype)
                {
                    ReleaseLabMonster();dummy=combat.BeginTrainingLabReplica(this,world.TrainingLabDummyPosition,frame.Prototype);
                    activePrototype=frame.Prototype;appliedDummyGeneration=frame.DummyGeneration;labSessionHeld=dummy!=null||tower!=null;
                }
                if((frame.Flags&2)==0){if(tower!=null)ReleaseLabTower();appliedTowerGeneration=frame.TowerGeneration;}
                else if(tower==null||appliedTowerGeneration!=frame.TowerGeneration||tower.Element!=frame.TowerType)
                {
                    ReleaseLabTower();int pending=selectedTowerType;selectedTowerType=frame.TowerType;
                    try{builder.TryCreateTrainingLabTower(this,out tower);}finally{selectedTowerType=pending;}
                    if(tower!=null)tower.SetRemote(true);appliedTowerGeneration=frame.TowerGeneration;labSessionHeld=dummy!=null||tower!=null;
                }
                TowerAttacking=(frame.Flags&4)!=0;MonsterAttackingTower=(frame.Flags&8)!=0;
                if(dummy!=null)
                {
                    dummy.ApplyRemoteState(frame.A,frame.DummyHP,frame.DummyHP>0,frame.DummyMax);
                    dummy.ApplyRemoteLiveStatus009(frame.LiveStatus);
                    dummy.transform.rotation=Quaternion.Euler(frame.D);
                    dummy.ApplyRemoteAttackPresentation(frame.Phase,frame.Progress,false,frame.AttackSequence,(int)frame.Sequence,
                        frame.B,frame.AttackStyle,frame.Windup,frame.Strike,frame.Recovery,4,frame.C);
                }
                if(tower!=null)tower.ApplyRemoteHealth(frame.TowerHP);
                sharedSnapshot=new TrainingLabSnapshot{HasActors=(frame.Flags&3)!=0,Prototype=selectedPrototype,
                    DummyAlive=frame.DummyHP>0,TowerAlive=frame.TowerHP>0,TowerAttacking=TowerAttacking,MonsterAttackingTower=MonsterAttackingTower,
                    DummyHealth=frame.DummyHP,DummyMaximum=frame.DummyMax,TowerHealth=frame.TowerHP,TowerMaximum=frame.TowerMax,
                    DummyHits=frame.DummyHits,TowerHits=frame.TowerHits,TowerShots=frame.TowerShots,NovaLaunches=frame.NovaLaunches,
                    LastDummyDamage=frame.LastDummy,TotalDummyDamage=frame.TotalDummy,LastTowerDamage=frame.LastTower,TotalTowerDamage=frame.TotalTower,TowerDamageToDummy=frame.TowerDamage};
                Status=frame.Result==0?"SHARED LAB | Host health and hits · Baseline damage · No rewards":"SHARED REQUEST DECLINED | Refresh/clear the pad, then retry. Host state retained.";
            }
            finally{SharedReplicaApplying=false;}
        }
        internal void ClearSharedSnapshot(){sharedSnapshot=default;appliedDummyGeneration=appliedTowerGeneration=0;}
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing supported world anchors/Y-up, exact prototype local+Z orientation; replicas consume accepted host values.
// Array Formats: One scalar common snapshot and2 generation keys; at most2 explicit cold replica objects, no extra simulation pool.
// Dependencies/Pointers: Only dedicated host dispatch can run real mutation APIs; guest binding never makes OwnerContext true; Combat/Enemy/Tower remain sole real HP and cadence owners.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing actor world healthbar anchor and authenticated host status snapshot.
// Array Formats: Five live bits31 distinct from caster12bit passive mask; fixed status presentation slots,256B lab word224 dedicated.
// Dependencies/Pointers: Existing HP/damage/timer owners unchanged; root composes protocol/version bumps and verifies native/two-peer visuals.
