namespace OdysseyOracle
{
    public enum LandingTourStop020:byte {None,Home,Shrine,Watchtower,Terrace,Smith,ExitOverlook}
    public static class PortalLandingTour020
    {
        public const int PointCount=37;
        public static bool TryPoint(int index,out LandingPoint020 feet,out LandingTourStop020 stop)
        {
            feet=default;stop=LandingTourStop020.None;
            if(index==0){feet=new LandingPoint020(1.8f,6.05f,44f);stop=LandingTourStop020.Home;return true;}
            if(index==1){feet=new LandingPoint020(0,6.05f,40f);return true;}
            if(index>=2&&index<26){int tread=25-index;feet=new LandingPoint020(0,(tread+1)*.25f+.05f,20+(tread+.5f)*.75f);return true;}
            switch(index)
            {
                case 26:return PortalLandingLayout020.TryAnchor(9,out feet,out _);
                case 27:return PortalLandingLayout020.TryAnchor(1,out feet,out _);
                case 28:stop=LandingTourStop020.Shrine;return PortalLandingLayout020.TryAnchor(2,out feet,out _);
                case 29:return PortalLandingLayout020.TryAnchor(1,out feet,out _);
                case 30:stop=LandingTourStop020.Watchtower;return PortalLandingLayout020.TryAnchor(5,out feet,out _);
                case 31:return PortalLandingLayout020.TryAnchor(1,out feet,out _);
                case 32:stop=LandingTourStop020.Terrace;return PortalLandingLayout020.TryAnchor(6,out feet,out _);
                case 33:stop=LandingTourStop020.Smith;return PortalLandingLayout020.TryAnchor(4,out feet,out _);
                case 34:feet=new LandingPoint020(-18,.05f,-18);return true;
                case 35:return PortalLandingLayout020.TryAnchor(1,out feet,out _);
                case 36:feet=new LandingPoint020(56*.9510565f,.05f,-56*.309017f);stop=LandingTourStop020.ExitOverlook;return true;
                default:return false;
            }
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Provisional metre/Y-up supported landing tour;24 exact descending treads, explicit shrine bypass and internal bridgehead overlook.
// Array Formats:37 immutable value waypoints and named stop enum; no array, actor, dialogue, movement, authority, quest, unlock or trigger owner.
// Dependencies/Pointers: Nibwick owner copies into its bounded native route and validates current prototype generation/support. Interpolate grounded steps through existing numeric queries; distant waypoint segments are not teleport permissions. Geometry revisions invalidate/revalidate the route.
