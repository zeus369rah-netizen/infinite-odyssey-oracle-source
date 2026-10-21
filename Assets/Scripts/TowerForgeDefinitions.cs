using System;

namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // Campaign-neutral pure definitions, shared by local-save and host-session adapters.
    // No wallet, entitlement, account, renderer, generated asset or mode-selection policy.
    public enum TowerForgeSymbol { Flame, Drop, Mountain, FourRaySwirl }
    public readonly struct TowerForgeDefinition
    {
        public readonly int BaseType;
        public readonly string Name, Role;
        public readonly TowerForgeSymbol Symbol;
        public readonly float Red, Green, Blue;
        public TowerForgeDefinition(int baseType, string name, string role, TowerForgeSymbol symbol, float red, float green, float blue)
        { BaseType = baseType; Name = name; Role = role; Symbol = symbol; Red = red; Green = green; Blue = blue; }
    }
    public readonly struct TowerForgeRankDefinition
    {
        public readonly int Tier, NextCost;
        public readonly float Damage, Interval;
        public TowerForgeRankDefinition(int tier, int nextCost, float damage, float interval)
        { Tier = tier; NextCost = nextCost; Damage = damage; Interval = interval; }
    }
    public static class TowerForgeDefinitions
    {
        public const int BaseTypes = 4, MaximumTier = 3, MaximumVisualBoxes = 32;
        public const float Range = 9f, MaximumHealth = 80f;
        private static readonly TowerForgeDefinition[] Types =
        {
            new TowerForgeDefinition(0, "Fire Tower", "Burns ground enemies; cannot target flying enemies.", TowerForgeSymbol.Flame, 1f, .36f, .12f),
            new TowerForgeDefinition(1, "Water Tower", "Slows flying enemies; cannot target ground enemies.", TowerForgeSymbol.Drop, .18f, .68f, 1f),
            new TowerForgeDefinition(2, "Earth Tower", "Staggers ground enemies; cannot target flying enemies.", TowerForgeSymbol.Mountain, .62f, .78f, .29f),
            new TowerForgeDefinition(3, "Air Spire", "Pushes flying enemies; cannot target ground enemies.", TowerForgeSymbol.FourRaySwirl, .66f, .91f, 1f)
        };
        private static readonly TowerForgeRankDefinition[] Ranks =
        {
            new TowerForgeRankDefinition(1, 8, Foundation.CombatConfig.Default.TowerDamage, Foundation.CombatConfig.Default.TowerCooldown),
            new TowerForgeRankDefinition(2, 18, Foundation.CombatConfig.Default.TowerDamage * CampaignBalance015.TierDamageMultiplier(2), Foundation.CombatConfig.Default.TowerCooldown * (14f / 15f)),
            new TowerForgeRankDefinition(3, 0, Foundation.CombatConfig.Default.TowerDamage * CampaignBalance015.TierDamageMultiplier(3), Foundation.CombatConfig.Default.TowerCooldown * (13f / 15f))
        };
        private static readonly string[] Names =
        { "Ember Brazier", "Flame Crown", "Sunfire Crucible", "Tide Basin", "Cascade", "Deepwater Font",
          "Stone Sentinel", "Stonebow", "Runic Stonebow", "Wind Vane", "Gale Spire", "Sky Crown" };
        private static readonly string[] Effects = { RankEffect(Ranks[0]), RankEffect(Ranks[1]), RankEffect(Ranks[2]) };
        static string RankEffect(TowerForgeRankDefinition rank) =>
            rank.Damage.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " damage / " +
            rank.Interval.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " seconds";
        public static bool ValidType(int type) => type >= 0 && type < BaseTypes;
        public static bool ValidTier(int tier) => tier >= 1 && tier <= MaximumTier;
        public static TowerForgeDefinition Get(int type)
        { if (!ValidType(type)) throw new ArgumentOutOfRangeException(nameof(type)); return Types[type]; }
        public static TowerForgeRankDefinition GetRank(int tier)
        { if (!ValidTier(tier)) throw new ArgumentOutOfRangeException(nameof(tier)); return Ranks[tier - 1]; }
        public static string RankName(int type, int tier)
        { Get(type); GetRank(tier); return Names[type * 3 + tier - 1]; }
        public static string EffectText(int tier) { GetRank(tier); return Effects[tier - 1]; }
    }
}

// # ARCHITECTURE SNAPSHOT
// Existing metre/Y-up/XZ actor space, host Forge ledger and sole combat owners remain.
// Exact composed Refuge/interstage/balance adapter hunks; no new native/save authority.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing world and accepted015 campaign/source identity, original arena positions retained.
// Array Formats: Eight milestones/40waves/905logical rewards within4096native slots, fixed128B XP channel and existing bounded pools.
// Dependencies/Pointers: Root composes actual current pool/audio owners with reviewed campaign/XP hunks; source/native/browser/performance/public acceptance remain separate.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Accepted global waves1..40, tiers1..3 and existing Y-up/XZ tower roots; exact bound lesson/lab adapters remain separate.
// Array Formats: Existing fixed4 towers and unchanged host snapshots plus cached scalar policy tier/wave/maximum; no ABI, save, cost, damage, enemy-aggression or hot allocation change.
// Dependencies/Pointers: CampaignBalance015 owns target groups/capacity/additive4percent rate and durability math. Host alone accepts context; monotonic policy rebases preserve damaged fraction, repeated contexts are inert, and dead towers stay dead.
