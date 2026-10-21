namespace OdysseyOracle
{
    public sealed partial class MatchTowerForgeAccount
    {
        bool soloRecoverySeeded;
        int soloRecoverySuppressThroughDropId;
        internal bool TrySeedSoloRestart(SoloStageRestartTicket ticket)
        {
            if(!ticket.Valid||soloRecoverySeeded||balance!=0||packedTiers!=0||revision!=0||lastRequest.RequestId!=0||LastOutcome!=MatchForgeOutcome.None)return false;
            for(int i=0;i<creditedDrops.Length;i++)if(creditedDrops[i]!=0)return false;
            MatchForgeSnapshot accepted=ticket.Progress.Forge;
            balance=accepted.Balance;packedTiers=accepted.PackedTiers;revision=accepted.Revision;
            // Separate cutoff, never creditedDrops: no claimed reward history is invented.
            // No credit or revision increment occurs. Defense also disables these pickups.
            soloRecoverySuppressThroughDropId=CampaignStagePacing.LastRewardId(ticket.Stage);
            soloRecoverySeeded=true;return true;
        }
    }
    public sealed partial class MatchTowerForgeClientState
    {
        internal bool TryCaptureAcceptedSoloRestart(out MatchForgeSnapshot snapshot)
        {snapshot=accepted;return HasState&&snapshot.Valid;}
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: existing ephemeral match account only; no town wallet or saved profile.
// Array Formats: accepted scalar bank/ranks/revision plus separate scalar replay cutoff; credited-drop history remains untouched; cold exactly-once pristine-account seed.
// Dependencies/Pointers: internal recovery ticket only, never network fields or a public Grant/Restore API; no credit/revision reward increment and no reused request identity.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing world and accepted015 campaign/source identity, original arena positions retained.
// Array Formats: Eight milestones/40waves/905logical rewards within4096native slots, fixed128B XP channel and existing bounded pools.
// Dependencies/Pointers: Root composes actual current pool/audio owners with reviewed campaign/XP hunks; source/native/browser/performance/public acceptance remain separate.
