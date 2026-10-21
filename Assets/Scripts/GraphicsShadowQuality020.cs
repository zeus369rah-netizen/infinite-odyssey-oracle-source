// PSEUDOCODE: validate the stored tier, otherwise migrate the legacy enabled flag by nearest preset cost;
// return one immutable value budget; never call Unity, persistence or rendering from this scalar policy.
using System;

namespace OdysseyOracle
{
    public readonly struct GraphicsShadowBudget020
    {
        public readonly float Distance;
        public readonly int Resolution,Cascades;
        public readonly bool Soft;
        public GraphicsShadowBudget020(float distance,int resolution,int cascades,bool soft)
        {Distance=distance;Resolution=resolution;Cascades=cascades;Soft=soft;}
    }

    public static class GraphicsShadowQuality020
    {
        public const int Count=4;
        public static int Clamp(int value)=>Math.Max(0,Math.Min(Count-1,value));
        public static int DefaultForPreset(int preset)=>preset<=0||preset==4?3:preset==1?2:preset==2?1:0;
        public static bool AllowsCloudShadows(bool clouds,bool weather,int tier)=>clouds&&weather&&tier>0;
        public static int ResolveStored(int stored,bool hasStored,int preset,bool hasLegacy,bool legacyEnabled)
        {
            if(hasStored&&stored>=0&&stored<Count)return stored;
            if(hasLegacy)return !legacyEnabled?0:preset<=1?2:1;
            return DefaultForPreset(GraphicsPresetCatalog.Clamp(preset));
        }
        public static GraphicsShadowBudget020 Get(int value)
        {
            switch(Clamp(value))
            {
                case 0:return new GraphicsShadowBudget020(0,512,1,false);
                case 1:return new GraphicsShadowBudget020(12,512,1,false);
                case 2:return new GraphicsShadowBudget020(25,1024,2,true);
                default:return new GraphicsShadowBudget020(40,2048,4,true);
            }
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Shadow reach in existing Unity world metres; scalar quality policy only.
// Array Formats: Four immutable value budgets; no mutable collection or frame allocation.
// Dependencies/Pointers: Existing preset catalogue supplies valid preset range; the main-thread owner persists/applies values.
