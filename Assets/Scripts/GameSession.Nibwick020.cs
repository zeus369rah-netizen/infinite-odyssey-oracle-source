namespace OdysseyOracle
{
    public sealed partial class GameSession
    {
        public NibwickRuntime020 Nibwick020 {get;private set;}
        partial void TickNibwick020()
        {
            if(!CampaignHubActive020){RetireNibwick020();return;}
            if(Nibwick020==null){Nibwick020=gameObject.AddComponent<NibwickRuntime020>();Nibwick020.Initialize020(this);}
            Nibwick020.Tick020();
        }
        partial void RetireNibwick020(){if(Nibwick020!=null)Nibwick020.Retire020();}
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Accepted explicit Campaign hub session/world generation, never tutorial/defense boolean aliases.
// Array Formats: One retained cold NPC owner reference; numeric/native resources live in its bounded lease.
// Dependencies/Pointers: Routing owner's optional partial hooks invoke tick only after positive admission and retire before world exit/reset/teardown. No independent Update, duplicate session authority, save, gameplay reward or voice source.
