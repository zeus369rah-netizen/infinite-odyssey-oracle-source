namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // Pure pickup-range policy, world units = metres. Run/combined passive mask is
    // supplied by its existing authority owner; no native containers or Unity objects.
    // No managed allocation, saved preference, gameplay timer or wallet mutation.
    public static class PickupReachRules
    {
        public const float BaseRangeMetres = 4f, MaximumRangeMetres = 8f;
        private const int Tailwind = 1 << 10, Skystride = 1 << 11;
        public static float RangeForMask(int mask12) => BaseRangeMetres +
            ((mask12 & Tailwind) != 0 ? 2f : 0f) + ((mask12 & Skystride) != 0 ? 2f : 0f);
        public static bool WithinReach(float distanceSquared, int mask12)
        {
            if (float.IsNaN(distanceSquared) || float.IsInfinity(distanceSquared) || distanceSquared < 0f) return false;
            float range = RangeForMask(mask12);
            return distanceSquared <= range * range;
        }
        public static string RangeLabel(int mask12)
        {
            float range = RangeForMask(mask12);
            return range >= 8f ? "PICKUP REACH 8m" : range >= 6f ? "PICKUP REACH 6m" : "PICKUP REACH 4m";
        }
        public static string HeartstoneHint(int mask12)
        {
            float range = RangeForMask(mask12);
            return range >= 8f ? "Heartstone · Flies to you within 8m · +1 for town training" :
                range >= 6f ? "Heartstone · Flies to you within 6m · +1 for town training" :
                "Heartstone · Flies to you within 4m · +1 for town training";
        }
    }
}
