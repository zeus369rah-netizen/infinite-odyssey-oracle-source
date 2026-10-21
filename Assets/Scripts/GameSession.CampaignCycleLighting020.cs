using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class GameSession
    {
        Light campaignDirectionalLight020 = null;

        internal bool TryGetCampaignDirectionalLight020(out Light light)
        {
            light = campaignDirectionalLight020;
            if (!ReferenceEquals(Instance, this) || !isActiveAndEnabled || light == null || !light.isActiveAndEnabled || light.type != LightType.Directional)
            {
                light = null;
                return false;
            }
            return true;
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: One session-owned main-thread directional Light reference; no transform data is copied into entity or Burst arrays.
// Array Formats: Single retained Unity object pointer and guarded read-only out parameter; no per-frame collections or allocations.
// Dependencies/Pointers: GameSession.ConfigureAtmosphere creates the existing sole directional key once in Awake and stores it in this partial. PortalLandingAtmosphere020 may borrow only the active key from the exact GameSession.Instance; this partial creates no Light and owns no animation, shadows, or RenderSettings.
