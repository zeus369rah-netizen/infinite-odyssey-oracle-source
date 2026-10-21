namespace OdysseyOracle
{
    public sealed partial class CampaignQuestState
    {
        internal bool BeginRecoveredSoloStage(string newCampaign,SoloStageRestartTicket ticket)
        {
            if(!ticket.Valid||newCampaign==ticket.SourceCampaign||!Begin(newCampaign,new ulong[]{0}))return false;
            StageNumber=ticket.Stage;CompletedStages=ticket.Stage-1;firstStageCoreLoadout=ticket.FirstStageCoreLoadout;
            peers[0].visits=ticket.Visits;peers[0].towers=ticket.Towers;
            peers[0].ready=peers[0].participating=true;
            Phase=CampaignPhase.Preparing;activeRun=null;Epoch++;Revision++;
            return ValidSnapshot(Snapshot(),1);
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: new campaign nonce/current stage, sole authenticated server0 membership.
// Array Formats: existing fixed campaign peer array; accepted entitlement copied without invented NPC visits.
// Dependencies/Pointers: existing Preparing/arena barrier remains; completed stage count stays stage-1; no completion reward or midbattle state import.
