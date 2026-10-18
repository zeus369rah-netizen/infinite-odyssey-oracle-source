using System;
using Unity.Netcode;
using UnityEngine;

namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // Cold stage/reset hooks over007's SAME Peer objects and005 Forge accounts.
    // Run XP/mask/bank/ranks/tombstones survive; arena actors/health/casts do not.
    // Refuge progress has a separate versioned payload, no invented arena HP.
    public sealed partial class CoopDefenseDirector
    {
        CoopCampaignDirector campaign;
        bool preserveCampaignPeers;
        int appliedCampaignProgress;
        bool CampaignActive=>campaign!=null&&campaign.Active;
        bool CampaignRefuge=>CampaignActive&&campaign.InRefuge;
        public bool CampaignTowerUpgradePhase=>CampaignActive&&
            TowerForgePhaseRules.AllowsUpgrade(campaign.Phase,campaign.StageNumber);
        public bool CampaignPeacePoses=>CampaignActive&&campaign.Phase==CampaignPhase.StageClear;

        public void BindCampaign(CoopCampaignDirector source)
        {
            if(source==null)throw new ArgumentNullException(nameof(source));
            if(campaign!=null&&campaign!=source)throw new InvalidOperationException("One campaign owner per Director.");
            campaign=source;
        }
        public void StartCampaignStage()
        {
            if(!CampaignActive||campaign.Phase!=CampaignPhase.Preparing)return;
            preserveCampaignPeers=true;
            try{StartDefense();}finally{preserveCampaignPeers=false;}
        }
        public void EnterCampaignRefuge(string context,bool newCampaign)
        {
            if(!CampaignRefuge||context!=campaign.Context)return;
            Running=hasRun=false;
            if(newCampaign)appliedCampaignProgress=0;
            preserveCampaignPeers=!newCampaign;
            try{ResetLocalState();}finally{preserveCampaignPeers=false;}
            runId=context;
            owner.Combat.ClearEnemies();owner.Combat.SetRemoteSimulation(true);
            owner.Builder.ClearPlacedShrines();owner.Progression.ResetNetworkXp();
            if(IsAuthority)
            {
                ReconcileCampaignMembers();
                foreach(var entry in peers)SendCampaignPersonalTo(entry.Key);
            }
        }
        public void HaltCampaignStageView()
        {
            if(!CampaignActive||(campaign.Phase!=CampaignPhase.StageClear&&campaign.Phase!=CampaignPhase.Complete&&campaign.Phase!=CampaignPhase.Failed))return;
            Running=hasRun=false;
            owner.Combat.ClearEnemies();owner.Combat.ClearRemoteDangerState();owner.Combat.SetRemoteSimulation(true);
            replicas.Clear();
            if(CampaignPeacePoses)
            {
                // Accepted StageClear retains ONLY the host's already validated
                // pose ledger, allowing the real walk to the return portal.
                // Damage/attack/target authority remains Running-gated.
                if(IsAuthority&&(peerHealth==null||!peerHealth.BeginCampaignPeacePoses()))
                {
                    owner.FailArenaPreparation("The return-portal pose context could not be retained. Return to the main menu and reconnect.");
                    return;
                }
            }
            else peerHealth?.ClearRun();
            owner.Combat.InvalidatePeerTargetCache();
            owner.Progression.ResetNetworkXp();owner.Builder.CancelPlacement();
        }
        void ResetCampaignPeerTransients()
        {
            foreach(var entry in peers)
            {
                var peer=entry.Value;
                // Never replace Peer or peer.Forge: its4096 accepted credit IDs,
                // request highwater, bank and bought tiers span all three stages.
                peer.Tokens=3;peer.Element=0;peer.AutoFireEnabled=false;
                peer.Claimed.Clear();peer.Selection.Clear();peer.Nova.Reset();peer.AutoSelectionWait.Clear();
                peer.TapRequests=default;peer.CastRequests=default;
                peer.AutoFireSequence=0;peer.NextAutoFireChange=0;peer.NextCast=1;
                peer.NextPersonalSync=peer.NextForgeRequest=0;
                Array.Clear(peer.ReturnReadyAt,0,peer.ReturnReadyAt.Length);
                peer.ArenaReady=entry.Key==rooms.LocalClientId&&owner.World!=null&&owner.World.IsCoopArena;
            }
        }
        public void ReconcileCampaignMembers()
        {
            if(!IsAuthority||!CampaignRefuge)return;
            missingPeers.Clear();foreach(var entry in peers)if(!ConnectedId(entry.Key))missingPeers.Add(entry.Key);
            foreach(ulong id in missingPeers)peers.Remove(id);
            foreach(ulong id in rooms.ConnectedClientIds)
                if(rooms.IsCampaignMemberRegistered(id)&&!peers.ContainsKey(id))peers[id]=new Peer();
        }
        CampaignProgressSnapshot CaptureCampaignProgress(ulong id,Peer peer)=>new CampaignProgressSnapshot
        {
            id=id,revision=++peer.CampaignProgressRevision,level=peer.Level,xp=peer.Xp,points=peer.Points,mask=peer.Mask,
            forgeVersion=MatchTowerForgeRules.Version,forgeBank=peer.Forge.Balance,forgeTiers=peer.Forge.PackedTiers,
            forgeRevision=peer.Forge.Revision,forgeLastRequest=peer.Forge.LastRequestId,forgeOutcome=(int)peer.Forge.LastOutcome
        };
        public void SendCampaignPersonalTo(ulong id,MatchForgeReceipt? requestReceipt=null)
        {
            if(!IsAuthority||!CampaignRefuge||!rooms.IsCampaignMemberRegistered(id)||!peers.TryGetValue(id,out var peer))return;
            var state=CaptureCampaignProgress(id,peer);
            // Transient denials (notably RateLimited) do not change the account's
            // last committed request. Acknowledge the actual dispatched request
            // so its pending UI can finish without writing or debiting a wallet.
            if(requestReceipt.HasValue&&requestReceipt.Value.RequestId!=0&&requestReceipt.Value.Outcome!=MatchForgeOutcome.None)
            {state.forgeLastRequest=requestReceipt.Value.RequestId;state.forgeOutcome=(int)requestReceipt.Value.Outcome;}
            if(id==rooms.LocalClientId)ApplyCampaignProgress(state);
            else rooms.SendReliableTo(id,JsonUtility.ToJson(StampCampaign(new Message
                {kind="oracle.campaign.progress.v1",run=runId,campaignProgress=state})));
        }
        Message StampCampaign(Message message)
        {
            if(CampaignActive)
            {message.campaign=campaign.CampaignId;message.campaignStage=campaign.StageNumber;message.campaignEpoch=campaign.Epoch;message.campaignVersion=CoopCampaignDirector.ProtocolVersion;}
            return message;
        }
        bool MatchesCampaign(Message message)=>!CampaignActive||message.campaignVersion==CoopCampaignDirector.ProtocolVersion&&
            message.campaign==campaign.CampaignId&&message.campaignStage==campaign.StageNumber&&message.campaignEpoch==campaign.Epoch;
        void ApplyCampaignProgress(CampaignProgressSnapshot state)
        {
            if(!CampaignRefuge||state==null||state.id!=rooms.LocalClientId||!CampaignProgressSnapshot.Valid(state)||state.revision<=appliedCampaignProgress)return;
            appliedCampaignProgress=state.revision;
            owner.Progression.ApplyAuthoritativeState(state.level,state.xp,state.points,state.mask);
            ApplyForgePersonal(new PlayerState{forgeVersion=state.forgeVersion,forgeBank=state.forgeBank,forgeTiers=state.forgeTiers,
                forgeRevision=state.forgeRevision,forgeLastRequest=state.forgeLastRequest,forgeOutcome=state.forgeOutcome});
        }
        bool HandleCampaignProgress(ulong sender,Message message)
        {
            if(message.kind!="oracle.campaign.progress.v1")return false;
            if(!IsAuthority&&sender==NetworkManager.ServerClientId&&CampaignRefuge&&MatchesCampaign(message)&&
                message.run==runId)ApplyCampaignProgress(message.campaignProgress);
            return true;
        }
        public ITowerForgeModeAdapter CreateCampaignForgeAdapter(Func<bool> localUiContext)
        {
            if(localUiContext==null)throw new ArgumentNullException(nameof(localUiContext));
            return new CoopMatchTowerForgeAdapter(localForge,()=>CampaignTowerUpgradePhase&&owner!=null&&!owner.IsDowned&&
                owner.Shop!=null&&owner.Shop.Blacksmith!=null&&localUiContext(),SendForgeRequest);
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Existing metre/Y-up/XZ actor space, host Forge ledger and sole combat owners remain.
// Exact composed Refuge/interstage/balance adapter hunks; no new native/save authority.
