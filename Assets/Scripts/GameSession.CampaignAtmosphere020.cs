using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class GameSession
    {
        PortalLandingAtmosphere020 campaignAtmosphere020;
        public PortalLandingAtmosphere020 CampaignAtmosphere020=>campaignAtmosphere020;
        partial void TickCampaignHubAtmosphere020()
        {
            if(!CampaignHubActive020||World==null){RetireCampaignHubAtmosphere020();return;}
            // One cold admission allocation; subsequent frames borrow this same owner and all buffers.
            if(campaignAtmosphere020==null)
            {
                campaignAtmosphere020=gameObject.AddComponent<PortalLandingAtmosphere020>();
                campaignAtmosphere020.Initialize(this,World);
            }
            campaignAtmosphere020.TickPresentation(Time.unscaledDeltaTime,!IsPlaying||Paused||!Application.isFocused,
                Graphics!=null?Graphics.ParticleQuality:0,Graphics!=null?Graphics.EffectiveParticleFactor:.125f,Player!=null?Player.ViewCamera:null);
            TickCampaignTree020();World.TickCampaignAssets020(this,campaignAtmosphere020);TickCampaignFoliage020();
        }
        partial void RetireCampaignHubAtmosphere020(){try{World?.HideCampaignAssetPresentation020();}finally{try{RetireCampaignFoliage020();}finally{try{RetireCampaignTree020();}finally{if(campaignAtmosphere020!=null)campaignAtmosphere020.Retire();}}}}
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing accepted GameSession->World->first-person camera chain; no inferred !arena mode or alternate entry route.
// Array Formats: One cached component reference; cold first admission prepares the existing-object adapter, then reuses bounded native VFX data.
// Dependencies/Pointers: Routing owner declares/calls the two private partial hooks after menu input and on invalidation/reset/teardown. Session positive generation owns admission; pause/focus/motion holds never reset the presentation clock. Root owns actual integration/import/native capture.
