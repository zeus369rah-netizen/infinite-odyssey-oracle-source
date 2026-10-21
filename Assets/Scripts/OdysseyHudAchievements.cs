namespace OdysseyOracle
{
    public sealed partial class OdysseyHud
    {
        void ShowAchievementBook()
        {
            ShowAchievementBadges020();
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing Lore book modal and responsive Canvas cards; no world or combat mutation.
// Array Formats: 21 fixed presentation rows backed by cached 25-cell badge wrappers; no changes to saved achievement bits.
// Dependencies/Pointers: DiscoveryProgress and separate RefugeFlames adapters retain progress authority; HUD owns presentation only.
