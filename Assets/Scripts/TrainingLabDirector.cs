using UnityEngine;

namespace OdysseyOracle
{
    [DisallowMultipleComponent]
    public sealed partial class TrainingLabDirector:MonoBehaviour
    {
        GameSession session;DungeonWorld world;OracleCombat combat;ShrinePlacementBuilder builder;
        OracleEnemy dummy;PlacedElementShrine tower;
        TrainingLabDamageMeter dummyDamage,towerDamage,towerShots;
        int selectedPrototype,novaLaunches;bool ending;
        public string Status{get;private set;}="OFFLINE TRAINING LAB | No rewards";
        public int SelectedPrototype=>selectedPrototype;
        public bool HasActors=>!object.ReferenceEquals(dummy,null)||!object.ReferenceEquals(tower,null);
        public bool PrototypeLocked=>false; // Choices are pending until the explicit Replace/Add gesture.
        public bool TowerAttacking{get;private set;}
        public bool MonsterAttackingTower{get;private set;}
        public OracleEnemy Dummy=>dummy;
        public PlacedElementShrine Tower=>tower;
        public bool OwnerContext=>enabled&&session!=null&&world!=null&&world==session.World&&combat!=null&&combat==session.Combat&&
            (TrainingLabRules.CanOwn(session.HasStarted,session.Tutorial!=null&&session.Tutorial.Finished,session.CombatUnlocked,
                world.IsTutorialRefuge,session.CoopMode,session.Rooms!=null&&session.Rooms.Connected,
                session.Defense!=null&&session.Defense.Running,session.IsDead,session.IsDowned,session.Victory)||SharedAuthority)&&
            !combat.CosmicNovaTrialActive&&!combat.TutorialTowerPracticeActive;
        public bool CanAct=>OwnerContext&&(SharedAuthority?session.SimulationActive:session.IsPlaying);
        public bool CanOpen=>(OwnerContext||SharedClient)&&builder!=null&&builder.ModelsReady;
        public bool LookingAtControl
        {
            get
            {
                if(session==null||world==null||world!=session.World||!world.IsTutorialRefuge||!session.IsPlaying||
                    session.Tutorial==null||!session.Tutorial.Finished||session.Player==null||session.Player.ViewCamera==null)return false;
                var eye=session.Player.ViewCamera.transform;Vector3 delta=world.TrainingLabTowerPosition+Vector3.up*.7f-eye.position;
                return delta.sqrMagnitude<=30.25f&&delta.sqrMagnitude>.001f&&Vector3.Dot(eye.forward,delta.normalized)>.82f;
            }
        }
        public void Initialize(GameSession owner)
        {
            if(session!=null)return;
            session=owner;world=owner.World;combat=owner.Combat;builder=owner.Builder;
        }
        public bool Open()
        {
            if(!LookingAtControl)return false;
            if(!CanOpen){Status=session.CoopMode||session.Rooms!=null&&session.Rooms.Connected?"TRAINING LAB UNAVAILABLE IN CO-OP | Offline only":"Training lab unavailable. Saved tower art or current context is not ready.";return false;}
            Status=SharedContext?"SHARED LAB | Baseline damage · Choose then REPLACE / ADD · No rewards":"OFFLINE ONLY | Choose each side, then REPLACE / ADD. Both attacks start OFF; no rewards.";
            return session.Hud!=null&&session.Hud.ShowTrainingLab();
        }
        public void Close(bool resume=true)
        {
            if(session==null||session.Hud==null)return;
            session.Hud.HideTrainingLab();if(resume&&(OwnerContext||SharedClient))session.Resume();
        }
        public bool SelectPrototype(int prototype)
        {
            if(!CanOpen||!TrainingLabRules.CanSelect(prototype,HasActors))return false;
            selectedPrototype=prototype;Status="Monster choice selected. Existing actors stay unchanged until REPLACE / ADD MONSTER.";return true;
        }
        public bool SpawnDummy()
        {
            if(SharedRequest(SharedLabControl.SpawnPair,(uint)(selectedPrototype|(selectedTowerType<<2)),out bool sharedResult))return sharedResult;
            // Compatibility entry: the explicit new controls can add either side first.
            if(!CanOpen||HasActors||builder.PlacedCount!=0)return false;
            if(!ReplaceMonster())return false;
            if(!ReplaceTower()){RemoveMonster();Status="PAIR NOT ADDED | Clear the tower pad, then retry. Both attacks OFF.";return false;}
            dummyDamage=default;towerDamage=default;towerShots=default;novaLaunches=0;
            return true;
        }
        public void DespawnDummy(){if(SharedRequest(SharedLabControl.RemoveAll,0,out _))return;RemoveAllOwnedActors();}
        public bool SetTowerAttacking(bool value)
        {
            if(SharedRequest(SharedLabControl.TowerAttack,value?1u:0u,out bool sharedResult))return sharedResult;
            if(!OwnerContext||value&&(dummy==null||!dummy.Alive||tower==null||tower.IsDestroyed))return false;TowerAttacking=value;return true;
        }
        public bool SetMonsterAttackingTower(bool value)
        {
            if(SharedRequest(SharedLabControl.MonsterAttack,value?1u:0u,out bool sharedResult))return sharedResult;
            if(!OwnerContext||value&&(dummy==null||!dummy.Alive||tower==null||tower.IsDestroyed))return false;MonsterAttackingTower=value;
            if(!value&&dummy!=null)dummy.CancelTrainingLabAttack();return true;
        }
        internal bool OwnsDummy(OracleEnemy value)=>OwnerContext&&!object.ReferenceEquals(value,null)&&object.ReferenceEquals(value,dummy);
        internal bool OwnsTower(PlacedElementShrine value)=>OwnerContext&&!object.ReferenceEquals(value,null)&&object.ReferenceEquals(value,tower);
        internal void ObserveDummyDamage(OracleEnemy value,float before,float after)
        {if(OwnsDummy(value))dummyDamage.Observe(before,after);}
        internal void ObserveTowerDamage(PlacedElementShrine value,float before,float after)
        {if(OwnsTower(value))towerDamage.Observe(before,after);}
        internal void ObserveTowerShot(PlacedElementShrine source,OracleEnemy target,float before,float after)
        {if(OwnsTower(source)&&OwnsDummy(target)){towerShots.Observe(before,after);if(SharedAuthority)Shared.ObserveTowerShot(target.AimPoint);}}
        internal void ObserveNovaLaunch(){if(OwnerContext&&novaLaunches<int.MaxValue)novaLaunches++;}
        public TrainingLabSnapshot ReadSnapshot()=>SharedClient?sharedSnapshot:new TrainingLabSnapshot{
            HasActors=HasActors,Prototype=selectedPrototype,DummyAlive=dummy!=null&&dummy.Alive,TowerAlive=tower!=null&&!tower.IsDestroyed,
            TowerAttacking=TowerAttacking,MonsterAttackingTower=MonsterAttackingTower,
            DummyHealth=dummy!=null?dummy.HP:0f,DummyMaximum=dummy!=null?dummy.MaxHP:0f,
            TowerHealth=tower!=null?tower.Health:0f,TowerMaximum=tower!=null?tower.MaxHealth:0f,
            DummyHits=dummyDamage.Hits,TowerHits=towerDamage.Hits,TowerShots=towerShots.Hits,NovaLaunches=novaLaunches,
            LastDummyDamage=dummyDamage.Last,TotalDummyDamage=dummyDamage.Total,LastTowerDamage=towerDamage.Last,
            TotalTowerDamage=towerDamage.Total,TowerDamageToDummy=towerShots.Total};
        public void EndSession(){Close(false);RemoveAllOwnedActors();if(combat!=null)combat.ReleaseTrainingLab(this);labSessionHeld=false;}
        void Update()
        {
            if(!OwnerContext&&!SharedClient&&(HasActors||labSessionHeld))EndSession();
            if(SharedContext){if(!SharedAuthority&&!SharedClient)Status="SHARED LAB | Waiting for the host state...";}
            else if(session!=null&&(session.CoopMode||session.Rooms!=null&&session.Rooms.Connected))Status="SHARED LAB | Waiting for the refuge and host...";
            else if(HasActors&&tower!=null&&tower.IsDestroyed)Status="TOWER DESTROYED | Actual HP is zero. Use REPLACE / ADD TOWER; monster is retained.";
            else if(HasActors&&dummy!=null&&!dummy.Alive)Status="MONSTER DEFEATED | Actual HP is zero. Use REPLACE / ADD MONSTER; tower is retained.";
        }
        void OnDisable(){EndSession();}
        void OnDestroy(){EndSession();}
        static float FlatSquared(Vector3 a,Vector3 b){float x=a.x-b.x,z=a.z-b.z;return x*x+z*z;}
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing Y-up refuge world metres, exact World pad/foot anchors; no local input poller.
// Array Formats: Four prototype choices and exactly one enemy plus one tier-one core tower; value snapshots/meters, no per-frame collections/formatting.
// Dependencies/Pointers: Existing Combat/Enemy/PlacedElementShrine retain all HP/cadence/damage authority; this offline owner releases only its two actors and suppresses no real tutorial gate.
// # ARCHITECTURE SNAPSHOT
// Existing Y-up/metre lab anchors and fixed actor pools/real HP owners remain.
// Pending four-type selectors apply only on explicit Replace/Add; zero/one actors retain the same Combat reward-exclusion owner.
// Remove/Replace reset both attacks and retire shots before actor mutation; full EndSession alone releases the lab session.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing refuge world metres/Y-up; host-owned shared lab, model local+Z.
// Array Formats: Two native peer records and bounded fixed frames; inherited48 projectile pool and two real actors.
// Dependencies/Pointers: Dedicated shared owner validates requests/fences; guests remote-only; no rewards, migration or narrator; root integrates complete slice.
