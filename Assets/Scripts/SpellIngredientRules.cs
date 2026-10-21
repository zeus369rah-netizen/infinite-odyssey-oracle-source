namespace OdysseyOracle
{
    // Numeric recipe contract; no Unity objects, allocations, clocks or native ownership.
    public static class SpellIngredientRules
    {
        public const float NormalRecoverySeconds = 4f, SuperRecoverySeconds = 5f, GlobalCadenceSeconds = .5f;
        public static int Count(int mask)
        {
            if (!ConfirmedSelection.ValidMask(mask)) return 0;
            int count = 0;
            for (int bits = mask; bits != 0; bits &= bits - 1) count++;
            return count;
        }
        // A logical triple is three existing single projectiles, never a new fusion.
        public static bool ValidProjectileMask(int mask) => ConfirmedSelection.ValidMask(mask) && Count(mask) != 3;
        public static bool IsSuper(int mask) => mask == 15;
        public static float RecoverySeconds(int mask) => IsSuper(mask) ? SuperRecoverySeconds : NormalRecoverySeconds;
        public static int MaskFor(int element, int combo = 0)
        {
            if (element < 0 || element > 3) return 0;
            int pair = combo == 1 ? 9 : combo == 2 ? 6 : combo == 3 ? 3 : combo == 4 ? 12 : combo == 5 ? 5 : combo == 6 ? 10 : 0;
            return (1 << element) | pair;
        }
        public static int ResolveMask(int element, int combo, int explicitMask)
        {
            if (element < 0 || element > 3) return 0;
            int candidate = explicitMask == 0 ? MaskFor(element, combo) : explicitMask;
            return ValidProjectileMask(candidate) && (candidate & (1 << element)) != 0 ? candidate : 0;
        }
    }
    public struct AutoSelectionWait
    {
        public double ReadyAt { get; private set; }
        public bool NotifyInput(double now)
        {
            if (!Finite(now) || now < 0d || !Finite(now + SpellIngredientRules.GlobalCadenceSeconds)) return false;
            ReadyAt = now + SpellIngredientRules.GlobalCadenceSeconds; return true;
        }
        public bool Ready(double now) => Finite(now) && now >= 0d && now >= ReadyAt;
        public void Clear() { ReadyAt = 0d; }
        static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}

// # ARCHITECTURE SNAPSHOT
// Unity metres, Y-up/XZ; four ingredient bits and caller-owned four-double recovery timestamps.
// Existing host/solo owners retain simulation clocks, pooled48 projectiles and private native memory lifetimes.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing accepted world metres/Y-up, input bits and owner simulation seconds.
// Array Formats: Four ingredient deadlines, fixed48 projectile pool, at most three component shots; existing host frame ABIs.
// Dependencies/Pointers: Work-only rapid-casting candidate. Existing host hit/damage/reward owners retained; root owns native/browser acceptance.
