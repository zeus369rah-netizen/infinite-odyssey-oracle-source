using System;
namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // Main-thread cold cache:12 profiles from the exact shared library templates.
    // No frame loop, scene objects, mesh readback, material clone or authority state.
    public static class TowerModelClearanceProfiles
    {
        static readonly TowerModelNavigationProfile[,] profiles=new TowerModelNavigationProfile[4,3];
        public static void Prewarm(TowerModelLibrary library)
        {
            if(library==null)throw new ArgumentNullException(nameof(library));
            for(int type=0;type<4;type++)for(int tier=1;tier<=3;tier++)
                if(profiles[type,tier-1]==null)profiles[type,tier-1]=new TowerModelNavigationProfile(library.Get(type,tier).Recipe);
        }
        public static void Prewarm(SavedTowerModelLibrary library)
        {
            if(library==null)throw new ArgumentNullException(nameof(library));
            for(int type=0;type<4;type++)for(int tier=1;tier<=3;tier++)
                if(profiles[type,tier-1]==null)profiles[type,tier-1]=new TowerModelNavigationProfile(library.Get(type,tier).Recipe);
        }
        public static float RadiusBetween(int type,int acceptedTier,float minimumLocalY,float maximumLocalY)
        {
            if(type<0||type>3||acceptedTier<1||acceptedTier>3)return 1f;
            var profile=profiles[type,acceptedTier-1];
            return profile==null?1f:(float)profile.RadiusBetween(minimumLocalY,maximumLocalY);
        }
    }
}
