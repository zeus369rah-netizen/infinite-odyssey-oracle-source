namespace OdysseyOracle
{
    public sealed partial class OdysseyHud
    {
        readonly CampaignWaveLabel015 defenseWaveCopy015=new CampaignWaveLabel015();
        void RefreshDefenseWaveCopy015()
        {
            var defense=session.Defense;if(defense==null)return;
            bool campaign=CampaignUiActive;
            if(defenseWaveCopy015.Update(campaign,defense.Running,campaign?campaignUi.StageNumber:0,
                defense.WaveNumber,defense.SpawnedThisWave,defense.WaveQuota,defense.AliveCount))defenseWave.text=defenseWaveCopy015.Text;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing defense panel; accepted Defense/Campaign scalar projection only.
// Array Formats: One cold bounded label cache, no queries, events, native allocations or per-frame string formatting.
// Dependencies/Pointers: Existing HUD/session own lifetime; actual global wave/quota values remain host-published, root owns integration and native layout acceptance.
