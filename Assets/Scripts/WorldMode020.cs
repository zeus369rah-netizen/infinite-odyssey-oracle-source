namespace OdysseyOracle
{
    public enum WorldMode020 : byte { None=0,TutorialRefuge=1,DefenseArena=2,CampaignHub=3 }
    public static class WorldModeRules020
    {
        public static bool LegacyCombat(WorldMode020 mode)=>mode==WorldMode020.TutorialRefuge||mode==WorldMode020.DefenseArena;
        public static bool ControlReady(WorldMode020 mode,bool hubReady)=>LegacyCombat(mode)||mode==WorldMode020.CampaignHub&&hubReady;
        public static bool CanEnterHub(bool started,bool connected,bool busy,bool terminal,bool loading,bool intro,bool supported)=>
            !started&&!connected&&!busy&&!terminal&&!loading&&!intro&&supported;
        public static bool InitialAdmission(in LandingAdmission020 value,uint actualGeneration)=>PortalLandingLayout020.Active(value)&&
            actualGeneration!=0&&value.Generation==actualGeneration&&value.Authority==0&&value.UnlockedEndpoints==0;
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Explicit semantic world mode and validated initial landing admission; no spatial/world API calls.
// Array Formats: One byte enum and scalar value predicates over builder-owned admission records; no runtime allocations.
// Dependencies/Pointers: Exact actual prototype generation is required. Initial entry grants no bridge unlock or travel authority; session/world owners publish the accepted mode.
