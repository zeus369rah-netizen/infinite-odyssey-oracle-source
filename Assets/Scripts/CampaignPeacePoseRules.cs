using System;

namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // Pure bounded peace-pose policy. Accepted campaign phase/context comes
    // only from the host Director; this is not a client mode or health packet.
    // The unchanged ledger owns all scalars and normal speed/capsule admission
    // remains in PeerCombatAuthority. No wallet, stage clock or network state.
    public static class CampaignPeacePoseRules
    {
        public static bool CanValidate(bool matchingRun, bool connectedHostArena,
            bool running, bool acceptedStageClear, bool prepared)
            => matchingRun && connectedHostArena && (running || acceptedStageClear && prepared);

        public static bool NormalizeDownedAtAcceptedPoses(PeerCombatLedger ledger,
            ulong[] connectedReadyIds, int count, double simulationNow, double poseNow)
        {
            if (ledger == null || !PeerCombatLedger.ValidRun(ledger.RunId) || connectedReadyIds == null ||
                count < 1 || count > PeerCombatLedger.Capacity || count > connectedReadyIds.Length ||
                !PeerCombatLedger.Finite(simulationNow) || simulationNow < 0 ||
                !PeerCombatLedger.Finite(poseNow) || poseNow < 0) return false;
            // Prevalidate the entire bounded set before any scalar mutation.
            // Same-thread Revive has no callbacks. A successful first revive
            // establishes the identical clock gate used by all later records.
            for (int i = 0; i < count; i++)
            {
                ulong id = connectedReadyIds[i];
                for (int j = 0; j < i; j++) if (connectedReadyIds[j] == id) return false;
                if (!ledger.TryGet(id, out var peer) || !peer.Ready || poseNow < peer.PoseTime ||
                    !PeerCombatLedger.ValidPosition(peer.X, peer.Y, peer.Z) ||
                    peer.Downed && (peer.Revision == int.MaxValue || peer.DownReport < 1)) return false;
            }
            for (int i = 0; i < count; i++)
            {
                ledger.TryGet(connectedReadyIds[i], out var peer);
                if (peer.Downed && !ledger.Revive(peer.Id, peer.DownReport, peer.X, peer.Y, peer.Z,
                    simulationNow, poseNow)) return false;
            }
            return true;
        }
    }
}
