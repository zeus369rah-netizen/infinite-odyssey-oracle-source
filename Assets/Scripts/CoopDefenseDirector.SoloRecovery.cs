namespace OdysseyOracle
{
    public sealed partial class CoopDefenseDirector
    {
        AcceptedSoloRestartProgress soloAcceptedProgress;
        string soloAcceptedCampaign;
        int soloAcceptedStage,soloSuppressedThroughStage;
        ulong soloAcceptedClient;
        SoloStageRestartGate soloSeedGate;
        int soloSeedGeneration;
        void CacheAcceptedSoloRestartProgress(int level,int xp,int points,int mask)
        {
            if(!CampaignActive||rooms==null||!rooms.Connected||!localForge.TryCaptureAcceptedSoloRestart(out var forge))return;
            var accepted=new AcceptedSoloRestartProgress(level,xp,points,mask,forge);
            if(!accepted.Valid)return;
            soloAcceptedProgress=accepted;soloAcceptedCampaign=campaign.CampaignId;soloAcceptedStage=campaign.StageNumber;soloAcceptedClient=rooms.LocalClientId;
        }
        internal bool TryCaptureSoloRestartProgress(string acceptedCampaign,int stage,ulong client,out AcceptedSoloRestartProgress progress)
        {progress=soloAcceptedProgress;return soloAcceptedCampaign==acceptedCampaign&&soloAcceptedStage==stage&&soloAcceptedClient==client&&progress.Valid;}
        internal void FreezeForSoloRestart()
        {
            Running=hasRun=false;
            owner.Combat.ClearEnemies();owner.Combat.ClearRemoteDangerState();owner.Combat.SetRemoteSimulation(true);
            owner.Builder.CancelPlacement();owner.Builder.ClearPlacedShrines();owner.Progression.ResetNetworkXp();
        }
        internal bool QueueSoloRestartSeed(SoloStageRestartGate gate,int generation)
        {
            if(gate==null||!gate.Ticket.Valid||gate.Generation!=generation||gate.Phase!=SoloRestartPhase.Preparing||
                rooms==null||!rooms.Connected||!rooms.IsHost||rooms.PlayerCount!=1||!rooms.TryGetAdmissionSlot(rooms.LocalClientId,out int slot)||slot!=0)return false;
            preserveCampaignPeers=false;ResetLocalState();
            soloSeedGate=gate;soloSeedGeneration=generation;return true;
        }
        bool CommitQueuedSoloRestartSeed()
        {
            if(soloSeedGate==null)return true;
            if(!IsAuthority||rooms.PlayerCount!=1||!rooms.TryGetAdmissionSlot(rooms.LocalClientId,out int slot)||
                !soloSeedGate.ConsumeSeed(soloSeedGeneration,rooms.SoloRestartOperation,rooms.PlayerCount,slot,rooms.IsHost,out var ticket)||
                !CampaignActive||campaign.StageNumber!=ticket.Stage||!peers.TryGetValue(rooms.LocalClientId,out var peer)||!peer.Forge.TrySeedSoloRestart(ticket))return false;
            peer.Level=ticket.Progress.Level;peer.Xp=ticket.Progress.Xp;peer.Points=ticket.Progress.Points;peer.Mask=ticket.Progress.Mask;
            soloSuppressedThroughStage=ticket.Stage;soloSeedGate=null;return true;
        }
        bool SoloRestartSuppressesCurrentRewards=>CampaignActive&&campaign.StageNumber<=soloSuppressedThroughStage;
        internal void ClearSoloRestartRewardScope(){soloSuppressedThroughStage=0;soloSeedGate=null;}
        bool TryAcceptedPartyDifficulty(out float multiplier)
        {
            multiplier=1;
            if(rooms==null||!rooms.Connected||!rooms.IsHost||rooms.PlayerCount<1||rooms.PlayerCount>2||rooms.ConnectedClientIds.Count!=rooms.PlayerCount)return false;
            var ids=rooms.ConnectedClientIds;for(int i=0;i<ids.Count;i++)if(!rooms.IsCampaignMemberRegistered(ids[i]))return false;
            return AcceptedPartyDifficulty.TryMultiplier(rooms.PlayerCount,out multiplier);
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: authenticated local campaign/progression scalars, fresh sole-host Peer and new run nonce.
// Array Formats: allocation-free accepted scalar cache; cold restart gate pointer; existing fixed Forge account/tombstone arrays.
// Dependencies/Pointers: cache hooks run only after existing accepted personal/progress validation; old actors/placements cleared, no economy grant; current/prior replay pickup rewards disabled, next stage normal.
