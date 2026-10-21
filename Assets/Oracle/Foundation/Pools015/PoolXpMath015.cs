using System;
using System.Runtime.InteropServices;

namespace OdysseyOracle.Foundation.Pools015
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct PoolSlot16
    {
        public uint Generation;
        public int StableId;
        public uint State; //0 free,1 alive,2 death tail. Generation never wraps.
        public float TailAge;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct PoolHandle8 { public int Slot; public uint Generation; }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct XpSource64
    {
        public float X, Y, Z;
        public int BaseXp;
        public uint SourceId;
        public int RunEpoch, RewardGeneration, OriginWave;
        public ulong Owner0, Owner1;
        public uint EligibleMask, ClaimedMask, Flags, Reserved;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct XpClaimQuote48
    {
        public uint SourceId, RecipientBit, ExpectedClaimedMask;
        public int RunEpoch, RewardGeneration, AcceptedLevel, BaseXp, BonusXp, TotalXp, OriginWave;
        public ulong AuthenticatedOwner;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct XpCluster64
    {
        public float X, Y, Z;
        public uint FirstSourceId;
        public int Head, Tail, Count;
        public uint EligibleMask, PendingMask;
        public int RunEpoch, RewardGeneration, MinimumOriginWave;
        public long BaseXpSum;
        public uint Active, Reserved;
    }

    public static class PoolXpMath015
    {
        public const int ActorCapacity = 48, SourceCapacity = 4096, RecipientCapacity = 2;
        public const int MaximumWave = 40, MaximumLevel = 50, MaximumGapBonus = 3;
        public const float WorldBound = 8192f, MaximumActorRadius = 2f, MaximumStep = .6f;
        public const uint Issued = 1u;

        public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public static bool Position(float x, float y, float z) => Finite(x) && Finite(y) && Finite(z) &&
            Math.Abs(x) <= WorldBound && Math.Abs(y) <= WorldBound && Math.Abs(z) <= WorldBound;

        public static bool TryActivate(ref PoolSlot16 slot, int stableId)
        {
            if (slot.State != 0u || stableId < 1 || slot.Generation == uint.MaxValue) return false;
            slot.Generation++; slot.StableId = stableId; slot.State = 1u; slot.TailAge = 0f;
            return true;
        }

        public static bool BeginDeath(ref PoolSlot16 slot, uint expectedGeneration)
        {
            if (slot.State != 1u || expectedGeneration == 0u || slot.Generation != expectedGeneration) return false;
            slot.State = 2u; slot.TailAge = 0f; return true;
        }

        public static bool AdvanceDeath(ref PoolSlot16 slot, float dt)
        {
            if (slot.State != 2u || !Finite(dt) || dt < 0f || dt > .1f) return false;
            slot.TailAge += dt;
            if (slot.TailAge < .75f) return false;
            slot.State = 0u; slot.StableId = 0; slot.TailAge = 0f; return true;
        }

        public static bool SourceValid(in XpSource64 source, int maximumBaseXp)
        {
            return maximumBaseXp > 0 && maximumBaseXp <= int.MaxValue / 4 &&
                source.SourceId >= 1u && source.SourceId <= SourceCapacity &&
                source.Flags == Issued && source.Reserved == 0u && source.RunEpoch > 0 && source.RewardGeneration > 0 &&
                source.OriginWave >= 1 && source.OriginWave <= MaximumWave &&
                source.BaseXp >= 1 && source.BaseXp <= maximumBaseXp &&
                source.EligibleMask > 0u && source.EligibleMask <= 3u &&
                (source.ClaimedMask & ~source.EligibleMask) == 0u &&
                ((source.EligibleMask & 3u) != 3u || source.Owner0 != source.Owner1) &&
                Position(source.X, source.Y, source.Z);
        }

        public static bool TryQuote(in XpSource64 source, int recipientSlot, ulong authenticatedOwner,
            int acceptedLevel, int runEpoch, int rewardGeneration, int maximumBaseXp, out XpClaimQuote48 quote)
        {
            quote = default;
            if (!SourceValid(source, maximumBaseXp) || recipientSlot < 0 || recipientSlot >= RecipientCapacity ||
                acceptedLevel < 1 || acceptedLevel > MaximumLevel || runEpoch != source.RunEpoch ||
                rewardGeneration != source.RewardGeneration) return false;
            uint bit = 1u << recipientSlot;
            if ((source.EligibleMask & bit) == 0u || (source.ClaimedMask & bit) != 0u ||
                authenticatedOwner != (recipientSlot == 0 ? source.Owner0 : source.Owner1)) return false;
            int gap = Math.Min(MaximumGapBonus, Math.Max(0, source.OriginWave - acceptedLevel));
            quote = new XpClaimQuote48 { SourceId = source.SourceId, RecipientBit = bit,
                ExpectedClaimedMask = source.ClaimedMask, RunEpoch = source.RunEpoch,
                RewardGeneration = source.RewardGeneration, AcceptedLevel = acceptedLevel,
                BaseXp = source.BaseXp, BonusXp = source.BaseXp * gap, TotalXp = source.BaseXp * (gap + 1),
                OriginWave = source.OriginWave, AuthenticatedOwner = authenticatedOwner };
            return true;
        }

        // Owner calls only inside its accepted, no-await transaction after reach/LOS/recovery checks.
        // This method never writes player progression, forge state, saves, health or networking.
        public static bool TryMarkClaimed(ref XpSource64 source, in XpClaimQuote48 quote, int maximumBaseXp)
        {
            int slot = quote.RecipientBit == 1u ? 0 : quote.RecipientBit == 2u ? 1 : -1;
            if (source.ClaimedMask != quote.ExpectedClaimedMask ||
                !TryQuote(source, slot, quote.AuthenticatedOwner, quote.AcceptedLevel,
                    quote.RunEpoch, quote.RewardGeneration, maximumBaseXp, out XpClaimQuote48 expected) ||
                expected.SourceId != quote.SourceId || expected.BaseXp != quote.BaseXp || expected.BonusXp != quote.BonusXp ||
                expected.TotalXp != quote.TotalXp || expected.OriginWave != quote.OriginWave) return false;
            source.ClaimedMask |= quote.RecipientBit; return true;
        }

        public static XpCluster64 ClusterFor(in XpSource64 source, int slot)
        {
            return new XpCluster64 { X = source.X, Y = source.Y, Z = source.Z, FirstSourceId = source.SourceId,
                Head = slot, Tail = slot, Count = 1, EligibleMask = source.EligibleMask,
                PendingMask = source.EligibleMask & ~source.ClaimedMask, RunEpoch = source.RunEpoch,
                RewardGeneration = source.RewardGeneration, MinimumOriginWave = source.OriginWave,
                BaseXpSum = source.BaseXp, Active = 1u };
        }

        // Original source positions remain the only claim geometry; anchor never creates a new ticket.
        public static bool TryMerge(in XpCluster64 a, in XpCluster64 b, bool matchingNumericWorldClear,
            out XpCluster64 merged)
        {
            merged = default;
            if (!matchingNumericWorldClear || a.Active != 1u || b.Active != 1u || a.Reserved != 0u || b.Reserved != 0u ||
                a.FirstSourceId == b.FirstSourceId || a.FirstSourceId < 1u || b.FirstSourceId < 1u ||
                a.FirstSourceId > SourceCapacity || b.FirstSourceId > SourceCapacity ||
                a.RunEpoch != b.RunEpoch || a.RewardGeneration != b.RewardGeneration ||
                a.EligibleMask != b.EligibleMask || a.PendingMask != b.PendingMask || a.PendingMask == 0u ||
                a.Count < 1 || b.Count < 1 || a.Count > SourceCapacity - b.Count ||
                a.BaseXpSum < 1 || b.BaseXpSum < 1 || a.BaseXpSum > long.MaxValue - b.BaseXpSum ||
                !Position(a.X, a.Y, a.Z) || !Position(b.X, b.Y, b.Z)) return false;
            double dx = (double)a.X - b.X, dy = (double)a.Y - b.Y, dz = (double)a.Z - b.Z;
            if (dx * dx + dy * dy + dz * dz > 4d) return false; // Inclusive2m anchor separation.
            merged = a.FirstSourceId < b.FirstSourceId ? a : b;
            merged.Count = a.Count + b.Count; merged.BaseXpSum = a.BaseXpSum + b.BaseXpSum;
            merged.MinimumOriginWave = Math.Min(a.MinimumOriginWave, b.MinimumOriginWave);
            return true;
        }

        // Returns false at contact/overlap; closed sweep t=[0,1], double intermediates.
        public static bool SweptDiscsClear(float ax, float az, float adx, float adz,
            float bx, float bz, float bdx, float bdz, float combinedRadius)
        {
            if (!Position(ax, 0f, az) || !Position(bx, 0f, bz) || !Finite(adx) || !Finite(adz) ||
                !Finite(bdx) || !Finite(bdz) || !Finite(combinedRadius) || combinedRadius <= 0f || combinedRadius > 4.1f ||
                (double)adx * adx + (double)adz * adz > (double)MaximumStep * MaximumStep ||
                (double)bdx * bdx + (double)bdz * bdz > (double)MaximumStep * MaximumStep) return false;
            double px = (double)ax - bx, pz = (double)az - bz;
            double vx = (double)adx - bdx, vz = (double)adz - bdz;
            double denominator = vx * vx + vz * vz;
            double t = denominator > 0d ? Math.Max(0d, Math.Min(1d, -(px * vx + pz * vz) / denominator)) : 0d;
            double x = px + vx * t, z = pz + vz * t;
            return x * x + z * z > (double)combinedRadius * combinedRadius;
        }

        // Conservative scalar fraction. A stationary-self unsafe result signals preexisting overlap;
        // caller must defer/hold instead of claiming this solver repairs penetrations.
        public static float LimitAgainstTrajectory(float ax, float az, float dx, float dz,
            float bx, float bz, float bdx, float bdz, float combinedRadius)
        {
            if (!SweptDiscsClear(ax, az, 0f, 0f, bx, bz, bdx, bdz, combinedRadius)) return 0f;
            if (SweptDiscsClear(ax, az, dx, dz, bx, bz, bdx, bdz, combinedRadius)) return 1f;
            float safe = 0f, blocked = 1f;
            for (int i = 0; i < 16; i++)
            {
                float fraction = (safe + blocked) * .5f;
                if (SweptDiscsClear(ax, az, dx * fraction, dz * fraction, bx, bz, bdx, bdz, combinedRadius)) safe = fraction;
                else blocked = fraction;
            }
            return safe;
        }

        // Signed21-bit axis coordinates, positive bias and3-way interleave; no floating hash key.
        public static bool TryMortonCell(float x, float y, float z, float cellSize, out ulong key)
        {
            key = 0UL;
            if (!Position(x, y, z) || !Finite(cellSize) || cellSize < .125f || cellSize > 16f) return false;
            int ix = (int)Math.Floor((double)x / cellSize), iy = (int)Math.Floor((double)y / cellSize), iz = (int)Math.Floor((double)z / cellSize);
            return TryMortonCoordinates(ix, iy, iz, out key);
        }

        public static bool TryMortonCoordinates(int x, int y, int z, out ulong key)
        {
            key = 0UL; const int bias = 1 << 20;
            if (x < -bias || x >= bias || y < -bias || y >= bias || z < -bias || z >= bias) return false;
            uint a = (uint)(x + bias), b = (uint)(y + bias), c = (uint)(z + bias);
            for (int bit = 0; bit < 21; bit++)
                key |= ((ulong)((a >> bit) & 1u) << (3 * bit)) |
                    ((ulong)((b >> bit) & 1u) << (3 * bit + 1)) | ((ulong)((c >> bit) & 1u) << (3 * bit + 2));
            return true;
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Finite world metres/Y-up; actor XZ closed sweeps; signed21-bit Morton63 cells.
// Array Formats: Blittable PoolSlot16/PoolHandle8/XpSource64/XpClaimQuote48/XpCluster64;48 actors,4096 sources,two recipients.
// Dependencies/Pointers: Scalar candidate only; host owner retains XP/forge/save/HP authority; no Unity object, allocation, native borrowing or automatic binding.
