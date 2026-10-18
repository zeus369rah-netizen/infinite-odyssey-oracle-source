using UnityEngine;

namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // Canonical road IDs E,NE,N,NW,W,SW,S,SE are independent of stable admission
    // slots. Host Director owns actors/hostile-bolt drain; World owns physical gates.
    // Local occupancy is an additional fail-closed safety check, never authority.
    // No Update, allocations in repeated mask/picking queries, or actor relocation.
    public sealed partial class DungeonWorld
    {
        readonly ArenaLaneState arenaLanes=new ArenaLaneState();
        readonly Collider[] laneDrainHits=new Collider[128];
        public int AdmissionLaneMask=>IsCoopArena?arenaLanes.AdmissionMask:0;
        public int ActiveLaneMask=>IsCoopArena?arenaLanes.GeometryMask:0;
        public int DrainingLaneMask=>IsCoopArena?arenaLanes.DrainingMask:0;
        public int ActiveLaneCount=>ArenaLaneState.Count(ActiveLaneMask);
        public int AdmissionLaneCount=>ArenaLaneState.Count(AdmissionLaneMask);

        public static Vector3 LaneDirection(int canonicalLane)=>ElementalTowerPlatforms.Direction(canonicalLane);
        public int GetArenaLane(Vector3 feet)=>ElementalTowerPlatforms.NearestLane(feet-ShrinePosition);
        public Vector3 GetInvasionPortal(int canonicalLane)
        {return ShrinePosition+LaneDirection(canonicalLane)*ArenaPortalRadius;}
        public bool TryGetActiveInvasionPortal(int canonicalLane,out Vector3 position)
        {
            position=default;
            if(canonicalLane<0||canonicalLane>=8||(AdmissionLaneMask&(1<<canonicalLane))==0)return false;
            position=GetInvasionPortal(canonicalLane);return true;
        }
        public Vector3 GetCoopPlayerSpawn(int admissionSlot)=>ElementalTowerPlatforms.PlayerSpawn(admissionSlot);
        public float GetCoopPlayerSpawnYaw(int admissionSlot)
        {
            Vector3 p=GetCoopPlayerSpawn(admissionSlot);
            return Mathf.Atan2(-p.x,-p.z)*Mathf.Rad2Deg;
        }
        // Keep authored safe hub positions; only face the next assigned road.
        // Rotating the host position itself could intersect the fixed SE forge.
        public Vector3 GetCampaignPlayerSpawn(int admissionSlot,int stage)
        {
            if(!CampaignStagePacing.ValidStage(stage))throw new System.ArgumentOutOfRangeException(nameof(stage));
            return GetCoopPlayerSpawn(admissionSlot);
        }
        public float GetCampaignPlayerSpawnYaw(int admissionSlot,int stage)
        {
            int lane=CampaignStagePacing.LaneForAdmissionSlot(admissionSlot,stage);
            if(lane<0)throw new System.ArgumentOutOfRangeException();
            Vector3 spawn=GetCampaignPlayerSpawn(admissionSlot,stage),portal=GetInvasionPortal(lane);
            return CampaignStagePacing.FacingYawDegrees(spawn.x,spawn.z,portal.x,portal.z);
        }
        public void ConfigureActiveLanes(int playerCount)
        {ConfigureActiveLaneMask(ElementalTowerPlatforms.MaskForPlayerCount(playerCount));}
        public void ConfigureActiveLaneMask(int canonicalMask)
        {
            ArenaLaneState.CheckMask(canonicalMask);
            if(!IsCoopArena)return;
            int oldAdmission=arenaLanes.AdmissionMask,oldGeometry=arenaLanes.GeometryMask;
            arenaLanes.Request(canonicalMask);
            RefreshLaneGeometry(oldAdmission,oldGeometry);
        }
        public bool CompleteLaneDrain(int canonicalLane)
        {
            ElementalTowerPlatforms.CheckLane(canonicalLane);
            if(!IsCoopArena||(AdmissionLaneMask&(1<<canonicalLane))!=0)return false;
            int oldGeometry=arenaLanes.GeometryMask;
            if(!arenaLanes.CompleteDrain(canonicalLane,CanCloseLaneGeometry(canonicalLane)))return false;
            RefreshLaneGeometry(arenaLanes.AdmissionMask,oldGeometry);return true;
        }
        public bool ApplyRemoteLaneState(int admissionMask,int geometryMask)
        {
            ArenaLaneState.CheckMask(admissionMask);ArenaLaneState.CheckMask(geometryMask);
            if(!IsCoopArena||(admissionMask&~geometryMask)!=0)return false;
            int occupied=0,oldAdmission=arenaLanes.AdmissionMask,oldGeometry=arenaLanes.GeometryMask;
            int closing=oldGeometry&~geometryMask;
            if(closing!=0)Physics.SyncTransforms();
            for(int lane=0;lane<8;lane++)if((closing&(1<<lane))!=0&&!LaneClosureIsClear(lane))occupied|=1<<lane;
            bool applied=arenaLanes.ApplyRemote(admissionMask,geometryMask,occupied);
            RefreshLaneGeometry(oldAdmission,oldGeometry);return applied;
        }
        public bool IsBuildPadActive(Vector3 position)
        {return IsCoopArena&&AdmissionLaneMask!=0&&ElementalTowerPlatforms.IsCanonicalPad(position);}

        public bool CanCloseLaneGeometry(int canonicalLane)
        {
            ElementalTowerPlatforms.CheckLane(canonicalLane);
            if(!IsCoopArena)return false;
            Physics.SyncTransforms();return LaneClosureIsClear(canonicalLane);
        }
        bool LaneClosureIsClear(int canonicalLane)
        {
            ElementalTowerPlatforms.CheckLane(canonicalLane);
            if(!IsCoopArena||arenaRoot==null||laneBarriers[canonicalLane]==null)return false;
            Transform barrier=laneBarriers[canonicalLane].transform;
            // A10cm safety apron includes bodies touching a soon-to-close face.
            // The existing normal arena-root identity/metre contract is preserved.
            Vector3 half=new Vector3(ElementalTowerPlatforms.BarrierWidth*.5f+.1f,
                ElementalTowerPlatforms.BarrierHeight*.5f+.1f,
                (ElementalTowerPlatforms.BarrierEnd-ElementalTowerPlatforms.BarrierStart)*.5f+.1f);
            int count=Physics.OverlapBoxNonAlloc(barrier.position,half,laneDrainHits,barrier.rotation,
                ~0,QueryTriggerInteraction.Collide);
            if(count==laneDrainHits.Length)return false;
            for(int i=0;i<count;i++)
            {
                Collider c=laneDrainHits[i];
                if(c==null)continue;
                // Explicit actor checks also work if a future consumer parents an
                // actor below arenaRoot; external proxies conservatively block too.
                if(c.GetComponentInParent<OraclePlayer>()!=null||c.GetComponentInParent<OracleEnemy>()!=null||
                    c.GetComponentInParent<OracleProjectile>()!=null||!c.transform.IsChildOf(arenaRoot))return false;
            }
            return true;
        }
        void ResetArenaLaneGeometry()
        {
            // Fresh match entry normally has no actors. Keep an occupied corridor
            // open even on a reused arena; never insert a solid barrier through it.
            int occupied=0;Physics.SyncTransforms();
            for(int lane=1;lane<8;lane++)if(!LaneClosureIsClear(lane))occupied|=1<<lane;
            arenaLanes.Reset(1|occupied);arenaLanes.Request(1);
            RefreshLaneGeometry(-1,-1);
        }
        void RefreshLaneGeometry(int oldAdmission,int oldGeometry)
        {
            int admission=arenaLanes.AdmissionMask,geometry=arenaLanes.GeometryMask;
            if(oldAdmission!=admission)
            {
                // Legacy array is compressed ACTIVE admissions, ordered canonical.
                // Its index is never a lane ID; use GetInvasionPortal(lane) instead.
                InvasionPortals=new Vector3[ArenaLaneState.Count(admission)];int index=0;
                for(int lane=0;lane<8;lane++)if((admission&(1<<lane))!=0)InvasionPortals[index++]=GetInvasionPortal(lane);
            }
            for(int lane=0;lane<8;lane++)
            {
                int bit=1<<lane;bool active=(admission&bit)!=0;
                if(oldGeometry!=geometry&&laneBarriers[lane]!=null)laneBarriers[lane].SetBlocked((geometry&bit)==0);
                if(oldAdmission!=admission&&invasionGateRoots[lane]!=null)invasionGateRoots[lane].SetActive(active);
                if(oldAdmission!=admission&&invasionGateLights[lane]!=null)invasionGateLights[lane].enabled=active;
            }
        }
    }
}
