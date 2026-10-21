using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class CoopCampaignDirector
    {
        string interstageReturnBlocked="";
        void WarmInterstagePortalJob()
        {
            NativeArray<CampaignPortalPoint16> points=default;NativeArray<int> result=default;JobHandle fence=default;
            try
            {
                points=new NativeArray<CampaignPortalPoint16>(8,Allocator.TempJob);result=new NativeArray<int>(1,Allocator.TempJob);
                for(int i=0;i<8;i++)points[i]=new CampaignPortalPoint16{Lane=-1};
                fence=new CampaignPortalReachJob{Points=points,Result=result,Mask=1,Reach=CampaignPortalMath.LocalReach}.Schedule();fence.Complete();
            }
            finally
            {
                try{fence.Complete();}
                finally{if(result.IsCreated)result.Dispose();if(points.IsCreated)points.Dispose();}
            }
        }
        public int ClearedLevelExitMask=>Phase==CampaignPhase.StageClear&&owner!=null&&owner.World!=null&&owner.World.IsCoopArena&&
            owner.World.AdmissionLaneMask==CampaignStagePacing.ActiveLaneMask(StageNumber)?owner.World.AdmissionLaneMask:0;
        public string InterstageBlockedReason
        {
            get
            {
                if(Phase==CampaignPhase.StageClear)return interstageReturnBlocked.Length>0?interstageReturnBlocked:"Resume, walk to any active monster portal, then use Interact. Your connected party returns together.";
                if(Phase!=CampaignPhase.Refuge)return "Monster portals return to refuge only after a cleared level.";
                var peer=LocalPeer();return CampaignPortalMath.RefugeBlockedReason(peer!=null,peer?.visits??0,peer?.towers??0,FirstStageCoreLoadout,StageNumber,peer?.ready??false,ReadyPlayerCount,MemberCount);
            }
        }
        // Cold diagnostic/UI action query only; do not call every frame. It grants no transition or authority.
        public bool TryGetReachableStageClearPortal(Vector3 feet,out int lane,out Vector3 point,out string blockedReason)
        {
            bool accepted=TryStageClearPortal(feet,CampaignPortalMath.HostReach,out lane,out point,out _,out blockedReason);
            interstageReturnBlocked=accepted?"":blockedReason;cachedStatus=BuildStatus();return accepted;
        }
        bool LocalStageClearPortal()
        {
            string reason="Waiting for the actual player pose.";
            bool accepted=owner.Player!=null&&TryStageClearPortal(owner.Player.transform.position,CampaignPortalMath.LocalReach,out _,out _,out _,out reason);
            interstageReturnBlocked=accepted?"":reason;cachedStatus=BuildStatus();return accepted;
        }

        bool TryStageClearPortal(Vector3 feet,float reach,out int lane,out Vector3 point,out Transform allowed,out string blockedReason)
        {
            lane=-1;point=default;allowed=null;
            blockedReason="Monster portals return to refuge only after a cleared level.";
            if(Phase!=CampaignPhase.StageClear||owner==null||owner.World==null||!owner.World.IsCoopArena||rooms==null||!rooms.Connected||
                owner.Defense==null||owner.Defense.Running||!Finite(feet))return false;
            int expected=CampaignStagePacing.ActiveLaneMask(StageNumber);
            blockedReason="Waiting for the accepted cleared-level monster portals.";
            if(expected==0||owner.World.AdmissionLaneMask!=expected)return false;
            NativeArray<CampaignPortalPoint16> points=default;NativeArray<int> result=default;JobHandle fence=default;
            try
            {
                points=new NativeArray<CampaignPortalPoint16>(8,Allocator.TempJob);result=new NativeArray<int>(1,Allocator.TempJob);
                // Cold main-thread Object adapter only; the scheduled numeric pass receives no Objects.
                for(int i=0;i<8;i++)
                {
                    var value=new CampaignPortalPoint16{Lane=-1};
                    if((expected&(1<<i))!=0&&owner.World.TryGetActiveInvasionPortalTarget(i,out var anchor,out _))
                        value=new CampaignPortalPoint16{X=anchor.x,Y=anchor.y,Z=anchor.z,Lane=i};
                    points[i]=value;
                }
                fence=new CampaignPortalReachJob{Points=points,Result=result,Mask=expected,X=feet.x,Y=feet.y,Z=feet.z,Reach=reach}.Schedule();fence.Complete();lane=result[0];
            }
            finally
            {
                try{fence.Complete();}
                finally{if(result.IsCreated)result.Dispose();if(points.IsCreated)points.Dispose();}
            }
            blockedReason="Walk closer to an active cleared-level monster portal.";
            if(lane<0||!owner.World.TryGetActiveInvasionPortalTarget(lane,out point,out allowed))return false;
            blockedReason="The active monster portal is obstructed. Approach its open centre.";
            if(!Visible(feet+Vector3.up*1.65f,point+Vector3.up*1.5f,allowed))return false;
            blockedReason="";return true;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing stage-cleared monster gate world anchors and actual admitted peer feet; refuge role/ready snapshots are read-only.
// Array Formats: Cold8xPoint16+1int TempJob query; scheduled numeric selector fenced before read/dispose. Existing32-hit LOS scratch remains fail-closed on saturation.
// Dependencies/Pointers: No per-frame query/allocation or new input/UI/visual owner; host independently validates phase/member/epoch/pose/LOS before existing atomic party-return. Same SDK session/progression/refuge readiness flow retained.
