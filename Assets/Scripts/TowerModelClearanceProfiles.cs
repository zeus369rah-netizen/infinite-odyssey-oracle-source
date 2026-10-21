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
        public static bool TryEnvelope015(int type,int acceptedTier,out float radius,out float lower,out float upper)
        {
            radius=lower=upper=0;
            if(type<0||type>3||acceptedTier<1||acceptedTier>3)return false;
            var profile=profiles[type,acceptedTier-1];if(profile==null||profile.Count==0)return false;
            double r=TowerElementRecipes.MaximumAmbientRadius,min=TowerElementRecipes.MinimumAmbientY,max=TowerElementRecipes.MaximumAmbientY;
            for(int i=0;i<profile.Count;i++)
            {var band=profile.GetBand(i);r=Math.Max(r,band.Radius);min=Math.Min(min,band.MinimumY);max=Math.Max(max,band.MaximumY);}
            radius=(float)r+.001f;lower=(float)min-.001f;upper=(float)max+.001f;
            return !float.IsNaN(radius)&&!float.IsInfinity(radius)&&radius>0&&lower<upper;
        }
        public static float RadiusBetween(int type,int acceptedTier,float minimumLocalY,float maximumLocalY)
        {
            if(type<0||type>3||acceptedTier<1||acceptedTier>3)return 1f;
            var profile=profiles[type,acceptedTier-1];
            return profile==null?1f:(float)profile.RadiusBetween(minimumLocalY,maximumLocalY);
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Current world solid AABB supersets and original XP anchors; floor and complete rotating/bobbing octahedron envelope included.
// Array Formats: Fixed4104 native solids/Morton grid and4095 source-pair certificates; independent world/source generations.
// Dependencies/Pointers: World and source owners complete readers before mutation/dispose. Existing claims/recipient masks/range/LOS and XP arithmetic remain canonical; native real merges/performance pending root.
