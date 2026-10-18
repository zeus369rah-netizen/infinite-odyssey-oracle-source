using System;

namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // UI ports, not additional wallets. Each mode reads exactly one accepted authority.
    // Local town save and ephemeral co-op account never import/convert one another.
    // Root explicitly selects the mode; disconnecting cannot switch to local-save ranks.
    public enum TowerForgeMode { SoloPersistent, CoopMatch }
    public enum TowerForgeSubmission { Rejected, Committed, AwaitingHost }
    public interface ITowerForgeModeAdapter
    {
        TowerForgeMode Mode { get; }
        bool Pending { get; }
        int Balance { get; }
        int GetTier(int baseType);
        TowerForgeSubmission Submit(int baseType);
    }

    public interface ISoloTowerForgeTransaction
    {
        // Existing single Heartstone save/envelope is the only persistent balance.
        // Implementation must atomically commit wallet+tier and roll back failed saves.
        int Balance { get; }
        int Revision { get; }
        int GetTier(int baseType);
        bool InSoloTownWithGameplayConsumer { get; }
        bool TryCommitUpgrade(int baseType, int expectedTier, int expectedRevision);
    }

    public sealed class SoloPersistentTowerForgeAdapter : ITowerForgeModeAdapter
    {
        private readonly ISoloTowerForgeTransaction transaction;
        public SoloPersistentTowerForgeAdapter(ISoloTowerForgeTransaction transaction)
        { this.transaction = transaction ?? throw new ArgumentNullException(nameof(transaction)); }
        public TowerForgeMode Mode => TowerForgeMode.SoloPersistent;
        public bool Pending => false;
        public int Balance => transaction.Balance;
        public int GetTier(int type) => transaction.GetTier(type);
        public TowerForgeSubmission Submit(int type)
        {
            if (!TowerForgeDefinitions.ValidType(type) || !transaction.InSoloTownWithGameplayConsumer) return TowerForgeSubmission.Rejected;
            int tier = transaction.GetTier(type);
            if (!TowerForgeDefinitions.ValidTier(tier) || tier >= 3 || transaction.Balance < TowerForgeDefinitions.GetRank(tier).NextCost)
                return TowerForgeSubmission.Rejected;
            return transaction.TryCommitUpgrade(type, tier, transaction.Revision) ? TowerForgeSubmission.Committed : TowerForgeSubmission.Rejected;
        }
    }

    public sealed class CoopMatchTowerForgeAdapter : ITowerForgeModeAdapter
    {
        private readonly MatchTowerForgeClientState state;
        private readonly Func<bool> localForgeContext;
        private readonly Action<MatchForgeRequest> sendToHost;
        public CoopMatchTowerForgeAdapter(MatchTowerForgeClientState state, Func<bool> localForgeContext, Action<MatchForgeRequest> sendToHost)
        {
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.localForgeContext = localForgeContext ?? throw new ArgumentNullException(nameof(localForgeContext));
            this.sendToHost = sendToHost ?? throw new ArgumentNullException(nameof(sendToHost));
        }
        public TowerForgeMode Mode => TowerForgeMode.CoopMatch;
        public bool Pending => state.Pending;
        public int Balance => state.Balance;
        public int GetTier(int type) => state.GetTier(type);
        public TowerForgeSubmission Submit(int type)
        {
            if (!state.TryBeginRequest(type, localForgeContext(), out MatchForgeRequest request)) return TowerForgeSubmission.Rejected;
            sendToHost(request);
            return TowerForgeSubmission.AwaitingHost;
        }
    }
}
