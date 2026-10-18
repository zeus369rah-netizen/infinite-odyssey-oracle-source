namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // Pure scalar rules; saved 12-bit passive IDs remain stable. No Unity objects,
    // native ownership, allocation, timing, authority or currency mutation here.
    // Only accepted shots snapshot their caster's trusted mask at the Unity boundary.
    public static class PassiveStatusRules
    {
        public const float WaterSecondsPerNode = .20f;
        public const float EarthSecondsPerUtilityNode = .10f;

        public static float WaterSlowSeconds(int mask, bool bindingMud = false)
        {
            int branch = (mask >> 3) & 7;
            int count = (branch & 1) + ((branch >> 1) & 1) + ((branch >> 2) & 1);
            return (bindingMud ? 2.2f : 1.4f) + WaterSecondsPerNode * count;
        }

        public static float EarthStaggerSeconds(int mask, int combo = 0)
        {
            int count = ((mask >> 7) & 1) + ((mask >> 8) & 1);
            return (combo == 2 ? .55f : combo == 5 ? .20f : .35f) + EarthSecondsPerUtilityNode * count;
        }
    }
}
