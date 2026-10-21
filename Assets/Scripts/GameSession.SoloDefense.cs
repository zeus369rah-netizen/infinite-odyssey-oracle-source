namespace OdysseyOracle
{
    public sealed partial class GameSession
    {
        public void StartSoloDefense()
        {
            if (Rooms == null || Rooms.Busy || Rooms.Connected || Loading != null && Loading.IsLoading) return;
            // The same accepted host/campaign pipeline runs with exactly one MPS
            // member. No parallel encounter, invented readiness or reward owner.
            pendingCoopStart = true;
            Rooms.CreateSoloRoom();
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing refuge arrival and shared defense arena; no direct pose writes.
// Array Formats: Existing pending-start flag and one-member session configuration.
// Dependencies/Pointers: RoomConnection owns service admission; existing campaign readiness/arena barrier owns actual start, HP, stages and rewards.
