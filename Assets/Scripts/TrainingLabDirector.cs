using UnityEngine;

namespace OdysseyOracle
{
    [DisallowMultipleComponent]
    public sealed class TrainingLabDirector:MonoBehaviour
    {
        GameSession session;DungeonWorld world;OracleCombat combat;ShrinePlacementBuilder builder;
        OracleEnemy dummy;PlacedElementShrine tower;
        TrainingLabDamageMeter dummyDamage,towerDamage,towerShots;
        int selectedPrototype,novaLaunches;bool ending;
        public string Status{get;private set;}="OFFLINE TRAINING LAB | No rewards";
        public int SelectedPrototype=>selectedPrototype;
        public bool HasActors=>!object.ReferenceEquals(dummy,null)||!object.ReferenceEquals(tower,null);
        public bool PrototypeLocked=>HasActors;
        public bool TowerAttacking{get;private set;}
        public bool MonsterAttackingTower{get;private set;}
        public OracleEnemy Dummy=>dummy;
        public PlacedElementShrine Tower=>tower;
        public bool OwnerContext=>enabled&&session!=null&&world!=null&&world==session.World&&combat!=null&&combat==session.Combat&&
            TrainingLabRules.CanOwn(session.HasStarted,session.Tutorial!=null&&session.Tutorial.Finished,session.CombatUnlocked,
                world.IsTutorialRefuge,session.CoopMode,session.Rooms!=null&&session.Rooms.Connected,
                session.Defense!=null&&session.Defense.Running,session.IsDead,session.IsDowned,session.Victory)&&
            !combat.CosmicNovaTrialActive&&!combat.TutorialTowerPracticeActive;
        public bool CanAct=>OwnerContext&&session.IsPlaying;
        public bool CanOpen=>OwnerContext&&builder!=null&&builder.ModelsReady;
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
            if(!CanOpen){Status=session.CoopMode||session.Rooms!=null&&session.Rooms.Connected?"TRAINING LAB UNAVAILABLE IN CO-OP | Offline only":"Training lab unavailable. Saved Water tower art or current context is not ready.";return false;}
            Status="OFFLINE ONLY | Spawn a dummy explicitly. Prototype locks until DESPAWN.";
            return session.Hud!=null&&session.Hud.ShowTrainingLab();
        }
        public void Close(bool resume=true)
        {
            if(session==null||session.Hud==null)return;
            session.Hud.HideTrainingLab();if(resume&&OwnerContext)session.Resume();
        }
        public bool SelectPrototype(int prototype)
        {
            if(!OwnerContext||!TrainingLabRules.CanSelect(prototype,HasActors))return false;
            selectedPrototype=prototype;Status="Prototype selected. Press SPAWN DUMMY to create the two practice actors.";return true;
        }
        public bool SpawnDummy()
        {
            if(!CanOpen||HasActors||builder.PlacedCount!=0)return false;
            // Cold admission only. Do not push the player or a live actor out of the way.
            if(session.Player!=null&&(FlatSquared(session.Player.transform.position,world.TrainingLabDummyPosition)<3.24f||
                FlatSquared(session.Player.transform.position,world.TrainingLabTowerPosition)<1.44f))
            {Status="Step away from the marked pad and dummy footprint, then retry SPAWN DUMMY.";return false;}
            Vector3 foot=world.TrainingLabDummyPosition,pad=world.TrainingLabTowerPosition;
            if(Physics.CheckBox(foot+Vector3.up*1.55f,new Vector3(1.30f,1.5f,1.30f),Quaternion.identity,~0,QueryTriggerInteraction.Ignore)||
                !combat.HasLineOfSight(pad+Vector3.up*1.25f,foot+Vector3.up*1.2f)||
                !Physics.Raycast(pad+Vector3.up*.1f,Vector3.down,.15f,~0,QueryTriggerInteraction.Ignore))
            {Status="The actual pad/target footprint or firing line is blocked. No actors spawned.";return false;}
            dummy=combat.BeginTrainingLab(this,world.TrainingLabDummyPosition,selectedPrototype);
            if(dummy==null){Status="Dummy admission failed. The existing encounter was left intact.";return false;}
            if(!builder.TryCreateTrainingLabTower(this,out tower))
            {combat.ReleaseTrainingLab(this);dummy=null;Status="Saved Water tower is unavailable. No token, currency or actor retained.";return false;}
            dummyDamage=default;towerDamage=default;towerShots=default;novaLaunches=0;
            TowerAttacking=MonsterAttackingTower=false;
            Status="DUMMY ACTIVE | Both attacks OFF. Stationary, no chase, no rewards. DESPAWN unlocks prototype choice.";
            return true;
        }
        public void DespawnDummy()
        {
            if(ending)return;ending=true;
            TowerAttacking=MonsterAttackingTower=false;
            var oldTower=tower;tower=null;
            if(oldTower!=null){oldTower.ClearTrainingLabBinding();oldTower.gameObject.SetActive(false);Destroy(oldTower.gameObject);}
            if(combat!=null)combat.ReleaseTrainingLab(this);
            dummy=null;ending=false;Status="DESPAWNED | Prototype choice unlocked. Nothing was purchased or rewarded.";
        }
        public bool SetTowerAttacking(bool value)
        {
            if(!OwnerContext||!HasActors)return false;TowerAttacking=value;return true;
        }
        public bool SetMonsterAttackingTower(bool value)
        {
            if(!OwnerContext||!HasActors)return false;MonsterAttackingTower=value;
            if(!value&&dummy!=null)dummy.CancelTrainingLabAttack();return true;
        }
        internal bool OwnsDummy(OracleEnemy value)=>OwnerContext&&!object.ReferenceEquals(value,null)&&object.ReferenceEquals(value,dummy);
        internal bool OwnsTower(PlacedElementShrine value)=>OwnerContext&&!object.ReferenceEquals(value,null)&&object.ReferenceEquals(value,tower);
        internal void ObserveDummyDamage(OracleEnemy value,float before,float after)
        {if(OwnsDummy(value))dummyDamage.Observe(before,after);}
        internal void ObserveTowerDamage(PlacedElementShrine value,float before,float after)
        {if(OwnsTower(value))towerDamage.Observe(before,after);}
        internal void ObserveTowerShot(PlacedElementShrine source,OracleEnemy target,float before,float after)
        {if(OwnsTower(source)&&OwnsDummy(target))towerShots.Observe(before,after);}
        internal void ObserveNovaLaunch(){if(OwnerContext&&novaLaunches<int.MaxValue)novaLaunches++;}
        public TrainingLabSnapshot ReadSnapshot()=>new TrainingLabSnapshot{
            HasActors=HasActors,Prototype=selectedPrototype,DummyAlive=dummy!=null&&dummy.Alive,TowerAlive=tower!=null&&!tower.IsDestroyed,
            TowerAttacking=TowerAttacking,MonsterAttackingTower=MonsterAttackingTower,
            DummyHealth=dummy!=null?dummy.HP:0f,DummyMaximum=dummy!=null?dummy.MaxHP:0f,
            TowerHealth=tower!=null?tower.Health:0f,TowerMaximum=tower!=null?tower.MaxHealth:0f,
            DummyHits=dummyDamage.Hits,TowerHits=towerDamage.Hits,TowerShots=towerShots.Hits,NovaLaunches=novaLaunches,
            LastDummyDamage=dummyDamage.Last,TotalDummyDamage=dummyDamage.Total,LastTowerDamage=towerDamage.Last,
            TotalTowerDamage=towerDamage.Total,TowerDamageToDummy=towerShots.Total};
        public void EndSession(){Close(false);DespawnDummy();}
        void Update()
        {
            if(!OwnerContext&&HasActors)EndSession();
            if(session!=null&&(session.CoopMode||session.Rooms!=null&&session.Rooms.Connected))Status="TRAINING LAB UNAVAILABLE IN CO-OP | Offline only";
            else if(HasActors&&tower!=null&&tower.IsDestroyed)Status="TOWER DESTROYED | Actual HP is zero. DESPAWN to reset both actors and unlock prototype choice.";
            else if(HasActors&&dummy!=null&&!dummy.Alive)Status="DUMMY DEFEATED | Actual HP is zero. DESPAWN to reset both actors and unlock prototype choice.";
        }
        void OnDisable(){EndSession();}
        void OnDestroy(){EndSession();}
        static float FlatSquared(Vector3 a,Vector3 b){float x=a.x-b.x,z=a.z-b.z;return x*x+z*z;}
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing Y-up refuge world metres, exact World pad/foot anchors; no local input poller.
// Array Formats: Four prototype choices and exactly one enemy plus one Water tower; value snapshots/meters, no per-frame collections/formatting.
// Dependencies/Pointers: Existing Combat/Enemy/PlacedElementShrine retain all HP/cadence/damage authority; this offline owner releases only its two actors and suppresses no real tutorial gate.
