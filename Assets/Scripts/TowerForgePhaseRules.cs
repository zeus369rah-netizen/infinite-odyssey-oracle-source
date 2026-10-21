namespace OdysseyOracle
{
    public static class TowerForgePhaseRules
    {
        // The initial refuge is preparation with free tier-one blueprints.
        // Returns after cleared levels1..7 expose an upgrade transaction.
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

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Stable existing arena road IDs and host-owned eight-level campaign identity.
// Array Formats: Existing two peers/eight lane arrays/four shared pads/48 actors/4096 reward tombstones; no ABI growth.
// Dependencies/Pointers: Level-derived lane prefix, actual host ready/admission/recovery fences; root owns shared protocol and native acceptance.
