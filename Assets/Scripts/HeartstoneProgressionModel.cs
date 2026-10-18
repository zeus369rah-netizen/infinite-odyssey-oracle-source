using System;

namespace OdysseyOracle
{
    /// <summary>
    /// Prepared meta-progression for a future release. No Unity or persistence dependency.
    /// A trusted caller owns saving and the authoritative town-shop location policy.
    /// </summary>
    [Serializable]
    public sealed class HeartstoneProgressionModel
    {
        public const int ElementCount = 4;
        public const int PassiveNodesPerElement = 3;
        public const int MaximumWandLevel = 5;
        public const int SnapshotVersion = 1;
        public const float DamagePerWandUpgrade = .15f;
        public const float MaximumWandDamageMultiplier = 1.6f;
        private const int AllPassiveBits = (1 << 12) - 1;

        private int crystalWallet;
        private readonly int[] wandLevels = { 1, 1, 1, 1 };
        private int passiveMask;

        public int CrystalWallet { get { return crystalWallet; } }
        public int PassiveMask { get { return passiveMask; } }

        public int GetWandLevel(int element)
        {
            RequireElement(element);
            return wandLevels[element];
        }

        /// <summary>Zero means that the wand has already reached its level cap.</summary>
        public int GetWandUpgradeCost(int element)
        {
            RequireElement(element);
            int level = wandLevels[element];
            return level < MaximumWandLevel ? level * 2 : 0;
        }

        /// <summary>Provisional balance: +15% per purchased level, capped at 1.60x.</summary>
        public float GetWandDamageMultiplier(int element)
        {
            RequireElement(element);
            return Math.Min(MaximumWandDamageMultiplier,
                1f + (wandLevels[element] - 1) * DamagePerWandUpgrade);
        }

        public bool CanBuyWand(int element, bool inTownShop)
        {
            return inTownShop && ValidElement(element) && wandLevels[element] < MaximumWandLevel &&
                crystalWallet >= wandLevels[element] * 2;
        }

        public bool TryBuyWand(int element, bool inTownShop)
        {
            if (!CanBuyWand(element, inTownShop)) return false;
            crystalWallet -= wandLevels[element] * 2;
            wandLevels[element]++;
            return true;
        }

        public static int GetPassiveUnlockCost(int node)
        {
            if (node < 0 || node >= PassiveNodesPerElement) throw new ArgumentOutOfRangeException("node");
            return node + 1;
        }

        public bool IsPassiveUnlocked(int element, int node)
        {
            return ValidNode(element, node) && (passiveMask & NodeBit(element, node)) != 0;
        }

        public bool CanUnlockPassive(int element, int node, bool inTownShop)
        {
            return inTownShop && ValidNode(element, node) && !IsPassiveUnlocked(element, node) &&
                (node == 0 || IsPassiveUnlocked(element, node - 1)) && crystalWallet >= node + 1;
        }

        public bool TryUnlockPassive(int element, int node, bool inTownShop)
        {
            if (!CanUnlockPassive(element, node, inTownShop)) return false;
            crystalWallet -= node + 1;
            passiveMask |= NodeBit(element, node);
            return true;
        }

        /// <summary>
        /// Call only for a future accepted monster-drop claim. This method neither
        /// creates drops nor proves their provenance; the network authority does.
        /// Rejects nonpositive awards and overflow without changing the wallet.
        /// </summary>
        public bool GrantCrystal(int amount)
        {
            if (amount <= 0 || amount > int.MaxValue - crystalWallet) return false;
            crystalWallet += amount;
            return true;
        }

        public HeartstoneProgressionSnapshot Snapshot()
        {
            return new HeartstoneProgressionSnapshot
            {
                Version = SnapshotVersion,
                Crystals = crystalWallet,
                WandLevels = (int[])wandLevels.Clone(),
                UnlockedPassiveMask = passiveMask
            };
        }

        /// <summary>Strict, atomic restore from a trusted save, with no shared arrays.</summary>
        public bool Restore(HeartstoneProgressionSnapshot snapshot)
        {
            if (snapshot == null || snapshot.Version != SnapshotVersion || snapshot.Crystals < 0 ||
                snapshot.WandLevels == null || snapshot.WandLevels.Length != ElementCount ||
                !ValidPassiveMask(snapshot.UnlockedPassiveMask)) return false;
            for (int element = 0; element < ElementCount; element++)
                if (snapshot.WandLevels[element] < 1 || snapshot.WandLevels[element] > MaximumWandLevel) return false;

            // The model is used on one authority thread. Validate every field
            // before committing any part of a restored purchase/save state.
            crystalWallet = snapshot.Crystals;
            Array.Copy(snapshot.WandLevels, wandLevels, ElementCount);
            passiveMask = snapshot.UnlockedPassiveMask;
            return true;
        }

        private static bool ValidPassiveMask(int mask)
        {
            if (mask < 0 || (mask & ~AllPassiveBits) != 0) return false;
            for (int element = 0; element < ElementCount; element++)
            {
                int branch = (mask >> (element * PassiveNodesPerElement)) & 7;
                // Exactly the sequential prefixes: no nodes, first, first two, all.
                if (branch != 0 && branch != 1 && branch != 3 && branch != 7) return false;
            }
            return true;
        }

        private static int NodeBit(int element, int node) { return 1 << (element * PassiveNodesPerElement + node); }
        private static bool ValidElement(int element) { return element >= 0 && element < ElementCount; }
        private static bool ValidNode(int element, int node)
        { return ValidElement(element) && node >= 0 && node < PassiveNodesPerElement; }
        private static void RequireElement(int element)
        { if (!ValidElement(element)) throw new ArgumentOutOfRangeException("element"); }
    }

    /// <summary>Serialize this detached value object; do not trust client-authored saves.</summary>
    [Serializable]
    public sealed class HeartstoneProgressionSnapshot
    {
        public int Version = HeartstoneProgressionModel.SnapshotVersion;
        public int Crystals;
        public int[] WandLevels = { 1, 1, 1, 1 };
        public int UnlockedPassiveMask;
    }
}
