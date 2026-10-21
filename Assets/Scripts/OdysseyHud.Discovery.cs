namespace OdysseyOracle
{
    public sealed partial class OdysseyHud
    {
        public bool TryOpenPhysicalDiscoveryBook(string title, string body)
        {
            if (!built || session == null || !session.HasStarted || session.Paused || currentPanel != JourneyPanel.None ||
                session.IsDead || session.IsDowned || session.Victory || ShopVisible || IntroActive || LoadingActive) return false;
            ShowLoreBook(title, body);
            return currentPanel == JourneyPanel.Lore;
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing book modal and trusted explicit local interaction.
// Array Formats: No new buffers, input loops or reward state.
// Dependencies/Pointers: Lore caller records discovery only after this existing modal actually opens; achievement menu calls never count as physical reads.
