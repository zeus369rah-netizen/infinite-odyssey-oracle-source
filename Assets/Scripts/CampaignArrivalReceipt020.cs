namespace OdysseyOracle
{
    public struct CampaignArrivalReceipt020
    {
        public uint Generation {get;private set;}
        public uint Revision {get;private set;}
        public bool Accept(uint generation,uint revision)
        {
            if(generation==0||revision==0||revision<=Revision)return false;
            Generation=generation;Revision=revision;return true;
        }
        public void Reset(){Generation=Revision=0;}
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: One actual accepted campaign World identity supplied by the owning session; nonzero generation/world revision.
// Array Formats: One8byte scalar receipt; strictly advancing nonwrapping revision, no collections/allocation or event objects.
// Dependencies/Pointers: Session alone resets on a different actual World reference. Receipt is consumed before visual/audio preparation; duplicates and failed presentation never replay or award progress.
