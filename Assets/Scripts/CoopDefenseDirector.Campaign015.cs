namespace OdysseyOracle
{
    public sealed partial class CoopDefenseDirector
    {
        int xpRewardGeneration015;
        public int CampaignAcceptedAdmissions => CampaignActive ? campaignAdmissions.Count : 0;
        public int CampaignCompletedWaves => CampaignActive ? campaignAdmissions.CompletedWaves : 0;

        // Register the immutable source's origin wave here. Claim-time code must
        // use that source wave, not resample a later wave for a larger supplement.
        public bool TryGetXpWaveContext(out int runEpoch, out int waveIndex, out int rewardGeneration)
        {
            runEpoch = waveIndex = rewardGeneration = 0;
            if (!IsAuthority || !CampaignActive || !Running || !clockStarted ||
                xpRewardGeneration015 <= 0 || campaign.Epoch <= 0 ||
                campaignAdmissions.Stage != campaign.StageNumber || !CampaignBalance015.ValidWave(campaignAdmissions.GlobalWave)) return false;
            runEpoch = campaign.Epoch; waveIndex = campaignAdmissions.GlobalWave; rewardGeneration = xpRewardGeneration015;
            return true;
        }

        void ResetCampaignWaveContext015()
        {
            campaignAdmissions.Reset(CampaignActive ? campaign.StageNumber : 1);
            // Exhaustion is explicit/fail-closed; never wrap a stale reward generation.
            xpRewardGeneration015 = xpRewardGeneration015 == int.MaxValue ? -1 :
                xpRewardGeneration015 < 0 ? -1 : xpRewardGeneration015 + 1;
        }

        void SyncCampaignWaveMetrics015()
        {
            int previousTowerWave015=WaveNumber;
            waveIndex = campaignAdmissions.WaveIndex;
            WaveNumber = campaignAdmissions.GlobalWave;
            SpawnedThisWave = campaignAdmissions.WaveAccepted;
            WaveQuota = campaignAdmissions.WaveQuota;
            if(WaveNumber!=previousTowerWave015)RefreshTowerHealth();
        }

        public float GetAcceptedTowerDamage015(int buildingId, int expectedBaseType)
        {
            if (!IsAuthority || !Running || !MatchTowerForgeRules.ValidType(expectedBaseType) ||
                !TryGetBuilding(buildingId, out BuildingState building) || building.element != expectedBaseType ||
                !MatchTowerForgeRules.ValidTier(building.tier) || !peers.TryGetValue(building.owner, out Peer peer) ||
                !CampaignBalance015.ValidLevel(peer.Level)) return 0f;
            return MatchTowerForgeRules.Damage(building.tier) * CampaignBalance015.RunDamageMultiplier(peer.Level);
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Host-owned accepted campaign epoch/global-wave/reward generation and building-owner level.
// Array Formats: Existing fixed host peer/building records plus one generation scalar; no new wire field or reward ledger.
// Dependencies/Pointers: Existing authority/readiness gates; no client level/damage trust. Root composes protocol and helper-owned source/claim integration.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Accepted global waves1..40, tiers1..3 and existing Y-up/XZ tower roots; exact bound lesson/lab adapters remain separate.
// Array Formats: Existing fixed4 towers and unchanged host snapshots plus cached scalar policy tier/wave/maximum; no ABI, save, cost, damage, enemy-aggression or hot allocation change.
// Dependencies/Pointers: CampaignBalance015 owns target groups/capacity/additive4percent rate and durability math. Host alone accepts context; monotonic policy rebases preserve damaged fraction, repeated contexts are inert, and dead towers stay dead.
