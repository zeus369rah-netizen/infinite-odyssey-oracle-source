using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class TrainingLabDirector
    {
        readonly Collider[] managementOverlap=new Collider[16];
        readonly RaycastHit[] managementSight=new RaycastHit[16];
        int selectedTowerType=1,activePrototype=-1;
        bool labSessionHeld;
        public int SelectedTowerType=>selectedTowerType;
        public int ActiveTowerType=>tower!=null?tower.Element:-1;
        public int ActivePrototype=>dummy!=null?activePrototype:-1;
        public bool HasTower=>!object.ReferenceEquals(tower,null);
        public bool HasMonster=>!object.ReferenceEquals(dummy,null);
        public bool SessionActive=>labSessionHeld;
        public bool SelectTowerType(int element)
        {
            if(!CanOpen||!TowerForgeDefinitions.ValidType(element))return false;
            selectedTowerType=element;Status="Tower choice selected. Existing actors stay unchanged until REPLACE / ADD TOWER.";return true;
        }
        bool HoldLabSession()
        {
            if(!combat.TryAcquireTrainingLabSession(this))
            {Status="A living encounter or another practice owner blocks this lab. Existing actors were left intact.";return false;}
            if(!labSessionHeld)novaLaunches=0;
            labSessionHeld=true;return true;
        }
        void StopLabAttacksAndShots()
        {
            TowerAttacking=MonsterAttackingTower=false;
            if(dummy!=null)dummy.CancelTrainingLabAttack();
            if(tower!=null)tower.ClearTrainingLabTargetReferences();
            if(combat!=null)combat.ClearTrainingLabProjectiles(this);
        }
        public bool RemoveTower()
        {
            if(SharedRequest(SharedLabControl.RemoveTower,0,out bool sharedResult))return sharedResult;
            if(!OwnerContext||ending)return false;
            StopLabAttacksAndShots();ReleaseLabTower();
            Status="TOWER REMOVED | Monster retained. Both attacks OFF. Choose a tower, then REPLACE / ADD TOWER.";return true;
        }
        public bool RemoveMonster()
        {
            if(SharedRequest(SharedLabControl.RemoveMonster,0,out bool sharedResult))return sharedResult;
            if(!OwnerContext||ending)return false;
            StopLabAttacksAndShots();ReleaseLabMonster();
            Status="MONSTER REMOVED | Tower retained. Both attacks OFF. Choose a monster, then REPLACE / ADD MONSTER.";return true;
        }
        public bool ReplaceTower()
        {
            if(SharedRequest(SharedLabControl.ReplaceTower,(uint)selectedTowerType,out bool sharedResult))return sharedResult;
            if(!CanOpen||ending)return false;
            StopLabAttacksAndShots();
            if(builder.PlacedCount!=0||!TowerSiteClear())
            {Status="TOWER UNCHANGED | Step away from the pad and clear its footprint. Both attacks OFF.";return false;}
            if(!HoldLabSession())return false;
            ReleaseLabTower();
            if(!builder.TryCreateTrainingLabTower(this,out tower))
            {tower=null;Status="TOWER NOT ADDED | Saved model or registry admission failed. Monster retained; both attacks OFF.";return false;}
            Status="TOWER READY | Selected real tier-one model added. Monster retained; both attacks OFF. No rewards.";return true;
        }
        public bool ReplaceMonster()
        {
            if(SharedRequest(SharedLabControl.ReplaceMonster,(uint)selectedPrototype,out bool sharedResult))return sharedResult;
            if(!CanOpen||ending)return false;
            StopLabAttacksAndShots();
            if(!MonsterSiteClear())
            {Status="MONSTER UNCHANGED | Step away and clear its footprint/firing line. Both attacks OFF.";return false;}
            if(!HoldLabSession())return false;
            ReleaseLabMonster();
            dummy=combat.BeginTrainingLab(this,world.TrainingLabDummyPosition,selectedPrototype);
            if(dummy==null)
            {Status="MONSTER NOT ADDED | Existing encounter or model admission failed. Tower retained; both attacks OFF.";return false;}
            activePrototype=selectedPrototype;
            Status="MONSTER READY | Selected stationary prototype added. Tower retained; both attacks OFF. No rewards.";return true;
        }
        bool MonsterSiteClear()
        {
            Vector3 foot=world.TrainingLabDummyPosition,pad=world.TrainingLabTowerPosition;
            if(SharedAuthority&&!Shared.PeersClear(foot,3.24f))return false;
            if(session.Player!=null&&FlatSquared(session.Player.transform.position,foot)<3.24f)return false;
            return FootprintClear(foot+Vector3.up*1.55f,new Vector3(1.30f,1.5f,1.30f),dummy!=null?dummy.transform:null)&&
                FiringLineClear(pad+Vector3.up*1.25f,foot+Vector3.up*1.2f);
        }
        bool TowerSiteClear()
        {
            Vector3 pad=world.TrainingLabTowerPosition;
            if(SharedAuthority&&!Shared.PeersClear(pad,1.44f))return false;
            if(session.Player!=null&&FlatSquared(session.Player.transform.position,pad)<1.44f)return false;
            return Physics.Raycast(pad+Vector3.up*.1f,Vector3.down,.15f,~0,QueryTriggerInteraction.Ignore)&&
                FootprintClear(pad+Vector3.up*.85f,new Vector3(.70f,.75f,.70f),tower!=null?tower.transform:null);
        }
        bool FootprintClear(Vector3 centre,Vector3 half,Transform ignored)
        {
            // Bounded cold main-thread PhysX adapter, not a worker/spatial simulation loop.
            int count=Physics.OverlapBoxNonAlloc(centre,half,managementOverlap,Quaternion.identity,~0,QueryTriggerInteraction.Ignore);
            bool clear=count<managementOverlap.Length;
            for(int i=0;i<count;i++)
            {
                Collider hit=managementOverlap[i];managementOverlap[i]=null;
                if(hit==null)continue;Transform root=hit.transform;
                if(ignored!=null&&(root==ignored||root.IsChildOf(ignored)))continue;
                clear=false;
            }
            return clear;
        }
        bool FiringLineClear(Vector3 from,Vector3 to)
        {
            Vector3 ray=to-from;float length=ray.magnitude;if(length<.001f)return false;
            int count=Physics.RaycastNonAlloc(from,ray/length,managementSight,length,~0,QueryTriggerInteraction.Ignore);
            if(count==managementSight.Length)return false;
            Transform oldDummy=dummy!=null?dummy.transform:null,oldTower=tower!=null?tower.transform:null;
            for(int i=0;i<count;i++)
            {
                Transform hit=managementSight[i].transform;
                if(hit==null||oldDummy!=null&&(hit==oldDummy||hit.IsChildOf(oldDummy))||
                    oldTower!=null&&(hit==oldTower||hit.IsChildOf(oldTower)))continue;
                return false;
            }
            return true;
        }
        void ReleaseLabTower()
        {
            var old=tower;tower=null;towerDamage=default;towerShots=default;
            if(old!=null){old.ClearTrainingLabBinding();old.gameObject.SetActive(false);Destroy(old.gameObject);}
        }
        void ReleaseLabMonster()
        {
            if(combat!=null)combat.ReleaseTrainingLabTarget(this);
            dummy=null;activePrototype=-1;dummyDamage=default;towerShots=default;
        }
        void RemoveAllOwnedActors()
        {
            if(ending)return;ending=true;
            StopLabAttacksAndShots();ReleaseLabTower();ReleaseLabMonster();ending=false;
            Status="BOTH ACTORS REMOVED | Choices retained. Both attacks OFF. No rewards.";
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing exact World Y-up/metre pad and dummy feet; placement remains a cold main-thread PhysX adapter.
// Array Formats: Four pending monster/four tower IDs, one actor per side, fixed16 overlap+ray buffers; no hot allocation or additional HP ledger.
// Dependencies/Pointers: Combat keeps reward exclusion with zero/one actors and retires shots; existing Enemy/Shrine own real HP, attack cadence and model rendering. Root alone integrates/accepts native geometry and lifetimes.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing refuge world metres/Y-up; host-owned shared lab, model local+Z.
// Array Formats: Two native peer records and bounded fixed frames; inherited48 projectile pool and two real actors.
// Dependencies/Pointers: Dedicated shared owner validates requests/fences; guests remote-only; no rewards, migration or narrator; root integrates complete slice.
