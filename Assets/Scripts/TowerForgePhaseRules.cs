namespace OdysseyOracle
{
    public static class TowerForgePhaseRules
    {
        // The initial refuge is preparation with free tier-one blueprints.
        // Only returns after stage one/two expose an upgrade transaction.
        public static bool AllowsUpgrade(CampaignPhase phase,int stageNumber)=>
            phase==CampaignPhase.Refuge&&stageNumber>=2&&stageNumber<=CampaignQuestState.StageCount;
    }
}

// # ARCHITECTURE SNAPSHOT
// Pure scalar campaign phase/stage policy; no coordinates, Unity objects, arrays, wallet or native ownership.
// Host authority and local UI share this rule; actual accepted Smith visit/pose/LOS remain separate gates.

// # ARCHITECTURE SNAPSHOT
// Existing metre/Y-up/XZ actor space, host Forge ledger and sole combat owners remain.
// Exact composed Refuge/interstage/balance adapter hunks; no new native/save authority.
