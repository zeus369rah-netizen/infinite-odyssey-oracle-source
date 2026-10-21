using System.Runtime.InteropServices;
using UnityEngine;
using OdysseyOracle.Foundation.Pools015;

namespace OdysseyOracle
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct XpFrame015 : Unity.Netcode.INetworkSerializeByMemcpy
    {
        public uint Magic; public int Version, Kind; public uint ValueRevision;
        public ulong RunLow, RunHigh;
        public int RunEpoch, RewardGeneration;
        public uint Sequence, SourceId;
        public int BaseXp, OriginWave;
        public Vector3 Position; public uint EligibleMask;
        public ulong Owner0, Owner1;
        public uint ClaimedMask; public int Level;
        public int Xp, Points, Mask, ForgeVersion, ForgeBalance, ForgeTiers, ForgeRevision, ProgressRevision;
    }
    public static class XpFrameRules015
    {
        public const uint Magic = 0x58503135;
        public const int Version = 2, Bytes = 128, Source = 1, Claim = 2, Ack = 3, Entitlement = 4, EntitlementAck = 5;
        public static bool Shape(in XpFrame015 f)
        {
            if (f.Magic != Magic || f.Version != Version ||
                (f.RunLow == 0 && f.RunHigh == 0) || f.RunEpoch <= 0 || f.RewardGeneration <= 0 ||
                f.Sequence == 0 || XpCatchupOrbs015.ParentId((int)f.SourceId)==0) return false;
            if (f.Kind == Claim || f.Kind == Ack || f.Kind==EntitlementAck) return (f.Kind==EntitlementAck?f.SourceId<=XpCatchupOrbs015.ParentCapacity:true)&&
                (f.Kind == Claim ? f.ValueRevision == 0 : f.ValueRevision >= 1 && f.ValueRevision <= (f.Kind==EntitlementAck?2:3)) &&
                f.BaseXp == 0 && f.OriginWave == 0 && f.Position.x == 0f && f.Position.y == 0f && f.Position.z == 0f &&
                f.EligibleMask == 0 && f.Owner0 == 0 && f.Owner1 == 0 && f.ClaimedMask == 0 &&
                f.Level == 0 && f.Xp == 0 && f.Points == 0 && f.Mask == 0 && f.ForgeVersion == 0 &&
                f.ForgeBalance == 0 && f.ForgeTiers == 0 && f.ForgeRevision == 0 && f.ProgressRevision == 0;
            if(f.Kind==Entitlement)return XpCatchupFrameRules015.ShapeEntitlement(f);
            if (f.Kind != Source || !XpCatchupOrbs015.SourceIdentity((int)f.SourceId,f.OriginWave,f.BaseXp) || f.ValueRevision < 1 || f.ValueRevision > 3 || !PoolXpMath015.SourceValid(ToSource(f), CampaignStagePacing.MaximumBaseXp) ||
                !CampaignBalance015.ValidLevel(f.Level) || f.ProgressRevision < 1 || f.Xp < 0 || f.Points < 0 || f.Mask < 0 || f.Mask > 4095 ||
                f.ForgeVersion != MatchTowerForgeRules.Version || f.ForgeBalance < 0 || f.ForgeBalance > MatchTowerForgeRules.MaximumBalance ||
                !MatchTowerForgeRules.ValidPackedTiers(f.ForgeTiers) || f.ForgeRevision < 0 || f.ForgeRevision > MatchTowerForgeRules.MaximumRevision) return false;
            int spent = 0;
            for (int element = 0; element < 4; element++)
            {
                int branch = (f.Mask >> (element * 3)) & 7;
                if (branch != 0 && branch != 1 && branch != 3 && branch != 7) return false;
                for (int bit = 0; bit < 3; bit++) if ((branch & (1 << bit)) != 0) spent++;
            }
            return (long)f.Points + spent <= f.Level - 1 &&
                (f.Level == CampaignBalance015.MaximumLevel ? f.Xp == 0 : f.Xp < CampaignBalance015.XpThreshold(f.Level));
        }
        public static XpSource64 ToSource(in XpFrame015 f) => new XpSource64
        {
            SourceId = f.SourceId, BaseXp = f.BaseXp, OriginWave = f.OriginWave,
            X = f.Position.x, Y = f.Position.y, Z = f.Position.z,
            RunEpoch = f.RunEpoch, RewardGeneration = f.RewardGeneration,
            Owner0 = f.Owner0, Owner1 = f.Owner1, EligibleMask = f.EligibleMask, ClaimedMask = f.ClaimedMask,
            Flags = PoolXpMath015.Issued
        };
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Authenticated existing NGO run128 identity and original arena source position; scalar accepted progression only.
// Array Formats: Explicit fixed128B blittable frame; source logical64B record, two owner slots, strict zero unused claim fields.
// Dependencies/Pointers: Room owns writer/handler lifetime; host owns immutable source/value and claim bits; no per-frame JSON/arrays or client-granted damage/XP.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Accepted parent/wave/owner/run identities and original source positions; literal claim-time catch-up children.
// Array Formats: Existing4096source64 ledger,905entitlement32 sidecars/dirty words,4096readonly projection, exact128BVersion2 frames.
// Dependencies/Pointers: Root composes structural clearance and these guarded owner hooks; base-onlyForge, no doublebonus, native/peer/carry/visual/performance acceptance pending.
