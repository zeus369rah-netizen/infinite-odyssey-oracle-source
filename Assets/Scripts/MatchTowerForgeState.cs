using System;

namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // Pure session state. Director owns one account per authenticated connected peer/run.
    // Only a validated host pickup event credits this bank. No PlayerPrefs/loadout import.
    // Main-thread transactions are synchronous; scene/UI/network work follows the commit.
    public static class MatchTowerForgeRules
    {
        public const int Version = 1, BaseTypes = TowerForgeDefinitions.BaseTypes, MaximumTier = TowerForgeDefinitions.MaximumTier;
        public const int MaximumDropId = 4096, MaximumBalance = 4096;
        public const int MaximumRevision = MaximumDropId + BaseTypes * 2;
        public static bool ValidType(int type) => TowerForgeDefinitions.ValidType(type);
        public static bool ValidTier(int tier) => TowerForgeDefinitions.ValidTier(tier);
        public static int Cost(int tier) => ValidTier(tier) ? TowerForgeDefinitions.GetRank(tier).NextCost : 0;
        public static float Damage(int tier) => TowerForgeDefinitions.GetRank(tier).Damage;
        public static float Interval(int tier) => TowerForgeDefinitions.GetRank(tier).Interval;
        public static bool ValidPackedTiers(int packed)
        {
            if (packed < 0 || packed > 255) return false;
            for (int type = 0; type < BaseTypes; type++) if (((packed >> (type * 2)) & 3) > 2) return false;
            return true;
        }
        public static int Tier(int packed, int type)
        { if (!ValidType(type) || !ValidPackedTiers(packed)) throw new ArgumentOutOfRangeException(); return 1 + ((packed >> (type * 2)) & 3); }
    }

    public enum MatchForgeOutcome
    { None, Applied, InvalidRequest, StaleState, InsufficientCharge, MaximumTier, ContextDenied, SequenceRejected, RateLimited }

    public readonly struct MatchForgeRequest
    {
        // No owner, cost, damage, balance, tier grant, saved profile or drop amount from a client.
        public readonly uint RequestId;
        public readonly int BaseType, ExpectedTier, ExpectedRevision;
        public MatchForgeRequest(uint requestId, int baseType, int expectedTier, int expectedRevision)
        { RequestId = requestId; BaseType = baseType; ExpectedTier = expectedTier; ExpectedRevision = expectedRevision; }
    }

    public readonly struct MatchForgeSnapshot
    {
        public readonly int Version, Balance, PackedTiers, Revision;
        public MatchForgeSnapshot(int version, int balance, int packedTiers, int revision)
        { Version = version; Balance = balance; PackedTiers = packedTiers; Revision = revision; }
        public bool Valid => Version == MatchTowerForgeRules.Version && Balance >= 0 && Balance <= MatchTowerForgeRules.MaximumBalance
            && MatchTowerForgeRules.ValidPackedTiers(PackedTiers) && Revision >= 0 && Revision <= MatchTowerForgeRules.MaximumRevision;
    }

    public readonly struct MatchForgeReceipt
    {
        public readonly uint RequestId;
        public readonly MatchForgeOutcome Outcome;
        public readonly MatchForgeSnapshot State;
        public readonly bool Replayed;
        public MatchForgeReceipt(uint requestId, MatchForgeOutcome outcome, MatchForgeSnapshot state, bool replayed)
        { RequestId = requestId; Outcome = outcome; State = state; Replayed = replayed; }
    }

    public sealed class MatchTowerForgeAccount
    {
        // This run-long tombstone outlives the existing live128-drop presentation cache.
        // Its fixed512 bytes prevent retired/re-registered IDs crediting the bank again.
        private readonly ulong[] creditedDrops = new ulong[MatchTowerForgeRules.MaximumDropId / 64];
        private int balance, packedTiers, revision;
        private MatchForgeRequest lastRequest;
        public MatchForgeOutcome LastOutcome { get; private set; }
        public uint LastRequestId => lastRequest.RequestId;
        public int Balance => balance;
        public int Revision => revision;
        public int PackedTiers => packedTiers;
        public MatchForgeSnapshot Snapshot => new MatchForgeSnapshot(MatchTowerForgeRules.Version, balance, packedTiers, revision);
        public int GetTier(int type) => MatchTowerForgeRules.Tier(packedTiers, type);

        // Call AFTER Director validates the host-issued drop, sender, range and LOS.
        // Fixed +1 Forge charge; existing XP amount/skill points remain untouched.
        // There is intentionally no public Grant(amount), Restore or save-load method.
        public bool TryCreditAcceptedDrop(int dropId)
        {
            if (dropId < 1 || dropId > MatchTowerForgeRules.MaximumDropId) return false;
            int bit = dropId - 1, word = bit >> 6;
            ulong mask = 1UL << (bit & 63);
            if ((creditedDrops[word] & mask) != 0 || balance >= MatchTowerForgeRules.MaximumBalance
                || revision >= MatchTowerForgeRules.MaximumRevision) return false;
            creditedDrops[word] |= mask; balance++; revision++;
            return true;
        }

        // hostContextAccepted is a Director decision, never a field from the request.
        // Validate all scene/authority gates first; this pure commit cannot yield/call out.
        public MatchForgeReceipt TryUpgrade(MatchForgeRequest request, bool hostContextAccepted)
        {
            if (request.RequestId == 0)
                return new MatchForgeReceipt(0, MatchForgeOutcome.SequenceRejected, Snapshot, false);
            if (request.RequestId == lastRequest.RequestId)
            {
                bool same = request.BaseType == lastRequest.BaseType && request.ExpectedTier == lastRequest.ExpectedTier
                    && request.ExpectedRevision == lastRequest.ExpectedRevision;
                return new MatchForgeReceipt(request.RequestId, same ? LastOutcome : MatchForgeOutcome.SequenceRejected, Snapshot, true);
            }
            if (request.RequestId < lastRequest.RequestId)
                return new MatchForgeReceipt(request.RequestId, MatchForgeOutcome.SequenceRejected, Snapshot, true);

            MatchForgeOutcome outcome;
            if (!hostContextAccepted) outcome = MatchForgeOutcome.ContextDenied;
            else if (!MatchTowerForgeRules.ValidType(request.BaseType) || !MatchTowerForgeRules.ValidTier(request.ExpectedTier)
                || request.ExpectedRevision < 0 || request.ExpectedRevision > MatchTowerForgeRules.MaximumRevision)
                outcome = MatchForgeOutcome.InvalidRequest;
            else if (request.ExpectedRevision != revision || request.ExpectedTier != GetTier(request.BaseType)) outcome = MatchForgeOutcome.StaleState;
            else if (request.ExpectedTier >= MatchTowerForgeRules.MaximumTier) outcome = MatchForgeOutcome.MaximumTier;
            else if (balance < MatchTowerForgeRules.Cost(request.ExpectedTier)) outcome = MatchForgeOutcome.InsufficientCharge;
            else
            {
                int shift = request.BaseType * 2;
                balance -= MatchTowerForgeRules.Cost(request.ExpectedTier);
                packedTiers = (packedTiers & ~(3 << shift)) | (request.ExpectedTier << shift);
                revision++;
                outcome = MatchForgeOutcome.Applied;
            }
            lastRequest = request; LastOutcome = outcome;
            return new MatchForgeReceipt(request.RequestId, outcome, Snapshot, false);
        }
    }

    // UI cache only. It never debits money or changes a rank on button press.
    // Director must authenticate host/run before forwarding snapshots/receipts here.
    public sealed class MatchTowerForgeClientState
    {
        private MatchForgeSnapshot accepted;
        private uint nextRequest, pendingRequest;
        public bool HasState { get; private set; }
        public bool Pending => pendingRequest != 0;
        public MatchForgeOutcome LastOutcome { get; private set; }
        public int Balance => HasState ? accepted.Balance : 0;
        public int Revision => HasState ? accepted.Revision : 0;
        public int GetTier(int type) => HasState ? MatchTowerForgeRules.Tier(accepted.PackedTiers, type)
            : MatchTowerForgeRules.ValidType(type) ? 1 : throw new ArgumentOutOfRangeException(nameof(type));

        public void ResetForRun()
        { accepted = default; nextRequest = pendingRequest = 0; HasState = false; LastOutcome = MatchForgeOutcome.None; }
        public bool ApplyHostState(MatchForgeSnapshot state)
        {
            if (!state.Valid || HasState && (state.Revision < accepted.Revision || state.Revision == accepted.Revision
                && (state.Balance != accepted.Balance || state.PackedTiers != accepted.PackedTiers))) return false;
            accepted = state; HasState = true; return true;
        }
        public bool TryBeginRequest(int type, bool localForgeContext, out MatchForgeRequest request)
        {
            request = default;
            if (!HasState || Pending || !localForgeContext || !MatchTowerForgeRules.ValidType(type) || GetTier(type) >= 3
                || Balance < MatchTowerForgeRules.Cost(GetTier(type)) || nextRequest == UInt32.MaxValue) return false;
            pendingRequest = ++nextRequest;
            request = new MatchForgeRequest(pendingRequest, type, GetTier(type), accepted.Revision);
            LastOutcome = MatchForgeOutcome.None;
            return true;
        }
        public bool ApplyHostReceipt(MatchForgeReceipt receipt)
        {
            if (receipt.RequestId == 0 || (uint)receipt.Outcome > (uint)MatchForgeOutcome.RateLimited || !receipt.State.Valid) return false;
            bool acceptedState = ApplyHostState(receipt.State);
            // A newer pickup snapshot can precede an older valid receipt. Acknowledge
            // that request without rolling the newer balance/rank view backwards.
            if (!acceptedState && (!HasState || receipt.State.Revision >= accepted.Revision)) return false;
            if (receipt.RequestId > nextRequest) nextRequest = receipt.RequestId;
            if (receipt.RequestId != pendingRequest) return true;
            pendingRequest = 0; LastOutcome = receipt.Outcome; return true;
        }
        // Closing a panel is not cancellation of a request already processed by the host.
        // A timeout only releases the button; an eventual authoritative snapshot still wins.
        public void ReleasePendingAfterTimeout() { pendingRequest = 0; }
    }
}
