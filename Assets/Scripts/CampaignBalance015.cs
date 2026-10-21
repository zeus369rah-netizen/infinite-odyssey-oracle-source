using System;

namespace OdysseyOracle
{
    // Pure scalar policy. The host supplies accepted wave/level, never client values.
    public static class CampaignBalance015
    {
        public const int WaveCount = 40, WavesPerMilestone = 5, MaximumLevel = 50;
        public const int BaseCount = 2, BaseXp = 8, MaximumAlive = 48;
        public const double HealthGrowth = 1.15d, CountGrowth = 1.10d, XpGrowth = 1.25d;
        public const float BaseEnemyHealth = 125f, BaseTowerDamage = 25f;
        public const double TowerInterval = 1.5d;
        public static bool ValidWave(int wave) => wave >= 1 && wave <= WaveCount;
        public static bool ValidLevel(int level) => level >= 1 && level <= MaximumLevel;
        public static int Admissions(int wave)
        { RequireWave(wave); return (int)Math.Ceiling(BaseCount * Math.Pow(CountGrowth, wave - 1)); }
        public static int XpBudget(int wave)
        { RequireWave(wave); return Math.Max(Admissions(wave), (int)Math.Ceiling(BaseXp * Math.Pow(XpGrowth, wave - 1))); }
        public static int BaseXpForAdmission(int wave, int ordinal)
        {
            int count = Admissions(wave), budget = XpBudget(wave);
            if (ordinal < 0 || ordinal >= count) throw new ArgumentOutOfRangeException(nameof(ordinal));
            return budget / count + (ordinal < budget % count ? 1 : 0);
        }
        public static int XpThreshold(int level)
        {
            if (!ValidLevel(level)) throw new ArgumentOutOfRangeException(nameof(level));
            return (int)Math.Ceiling(BaseXp * Math.Pow(XpGrowth, level - 1));
        }
        public static float EnemyHealth(int wave)
        { RequireWave(wave); return (float)(BaseEnemyHealth * Math.Pow(HealthGrowth, wave - 1)); }
        public static float RunDamageMultiplier(int level)
        {
            if (!ValidLevel(level)) throw new ArgumentOutOfRangeException(nameof(level));
            return (float)Math.Pow(HealthGrowth, level - 1);
        }
        public static float TierDamageMultiplier(int tier)
        {
            if (tier < 1 || tier > 3) throw new ArgumentOutOfRangeException(nameof(tier));
            return (float)Math.Pow(HealthGrowth, tier - 1);
        }
        public const float BaseTowerHealth = 80f, TowerWaveRateStep = .04f;
        public static int TowerCapacity(int wave)
        { RequireWave(wave); return wave <= 2 ? 2 : wave <= 4 ? 3 : 4; }
        public static bool TowerTargetGroupAllows(int type, bool flying)
            => type >= 0 && type < 4 && ((type == 1 || type == 3) == flying);
        public static float TowerRateMultiplier(int wave)
        { RequireWave(wave); return 1f + TowerWaveRateStep * (wave - 1); }
        public static float TowerShotInterval(float baseInterval, int wave)
        {
            if (!FiniteTowerValue015(baseInterval) || baseInterval < .05f || baseInterval > 10f) throw new ArgumentOutOfRangeException(nameof(baseInterval));
            return baseInterval / TowerRateMultiplier(wave);
        }
        public static float TowerMaximumHealth(int tier, int wave)
            => BaseTowerHealth * TierDamageMultiplier(tier) * TowerRateMultiplier(wave);
        public static bool TryAdvanceTowerHealth(int previousTier, int previousWave, float previousMaximum, float previousHealth,
            int tier, int wave, out float maximum, out float health)
        {
            maximum = previousMaximum; health = previousHealth;
            if (previousTier < 1 || previousTier > 3 || tier < previousTier || tier > 3 || !ValidWave(previousWave) || !ValidWave(wave) || wave < previousWave ||
                !FiniteTowerValue015(previousMaximum) || !FiniteTowerValue015(previousHealth) || previousMaximum <= 0f || previousHealth < 0f || previousHealth > previousMaximum ||
                previousMaximum != TowerMaximumHealth(previousTier, previousWave)) return false;
            if (tier == previousTier && wave == previousWave) return true;
            maximum = TowerMaximumHealth(tier, wave);
            // Preserve the existing damaged fraction, never refill or resurrect on a replay.
            health = previousHealth <= 0f ? 0f : (float)Math.Min(maximum, (double)previousHealth * maximum / previousMaximum);
            return true;
        }
        static bool FiniteTowerValue015(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        // All existing archetypes initialize at100HP. Bound is derived from wave40
        // and the existing accepted two-player1.1 factor, not an arbitrary10x clamp.
        public static float MaximumHealthMultiplier => EnemyHealth(WaveCount) / 100f * 1.1f;
        public static bool ValidHealthMultiplier(float value) => !float.IsNaN(value) && !float.IsInfinity(value) &&
            value >= .1f && value <= MaximumHealthMultiplier;
        static void RequireWave(int wave)
        { if (!ValidWave(wave)) throw new ArgumentOutOfRangeException(nameof(wave)); }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Accepted scalar global waves1..40 and run levels1..50; no world/Object access.
// Array Formats: Allocation-free bounded scalar formulas; no native pointer, cache or per-frame allocation.
// Dependencies/Pointers: Existing host selects context; pickup helper conserves quotient/remainder XP and applies supplements; caller owns state.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Accepted global waves1..40, tiers1..3 and existing Y-up/XZ tower roots; exact bound lesson/lab adapters remain separate.
// Array Formats: Existing fixed4 towers and unchanged host snapshots plus cached scalar policy tier/wave/maximum; no ABI, save, cost, damage, enemy-aggression or hot allocation change.
// Dependencies/Pointers: CampaignBalance015 owns target groups/capacity/additive4percent rate and durability math. Host alone accepts context; monotonic policy rebases preserve damaged fraction, repeated contexts are inert, and dead towers stay dead.
