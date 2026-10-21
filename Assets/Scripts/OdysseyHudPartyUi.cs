namespace OdysseyOracle
{
    public sealed partial class OdysseyHud
    {
        // # ARCHITECTURE SNAPSHOT
        // Presentation only. The compiled room authority supplies capacity; accepted
        // Room state supplies members/ready counts. This does not enable capacity8,
        // approve joins, set readiness or manufacture teammate health/slot records.
        private static readonly string ConfiguredPartyCapacity="1 TO "+RoomConnection.MaximumPlayers+" PLAYERS · 3+ UNAVAILABLE";
        private static readonly string ConfiguredCoopMenu="CREATE / JOIN PARTY\n"+ConfiguredPartyCapacity;
        private static readonly string ConfiguredRoomDescription=ConfiguredPartyCapacity+"\nMeet in the refuge, then ready together at the portal.";
        private int displayedSharedPadCount=-1,displayedSharedPadBinding=-1;
        private string displayedSharedPadHint="";

        private string ActualSharedPadHint()
        {
            int count=session!=null&&session.Builder!=null?session.Builder.BuildPads.Length:0;
            int binding=AbilityBindings.LastChangeFrame;
            if(count!=displayedSharedPadCount||binding!=displayedSharedPadBinding)
            {
                displayedSharedPadCount=count;displayedSharedPadBinding=binding;
                displayedSharedPadHint=count>0?count+" SHARED PADS TOTAL | "+GameActionBindings.GetLabel("Interact")+" / Right-click":"BUILD PADS UNAVAILABLE";
            }
            return displayedSharedPadHint;
        }
    }
}
