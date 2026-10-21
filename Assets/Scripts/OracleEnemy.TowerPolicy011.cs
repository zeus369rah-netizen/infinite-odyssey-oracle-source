namespace OdysseyOracle
{
    // Pure default-deny policy. Existing host/lab owners still validate phase, range and authority.
    internal static class EnemyTowerAggressionPolicy011
    {
        internal static bool Allows(bool boundTraining, bool exactEligibleOwner, bool intentionalToggle) =>
            boundTraining && exactEligibleOwner && intentionalToggle;
        internal static bool PresentationAllowed(bool towerPermission, int style, int targetTowerId) =>
            style >= 0 && style <= 2 && targetTowerId >= 0 && targetTowerId <= 65535 &&
            (style == 2 ? towerPermission && targetTowerId > 0 : targetTowerId == 0);
    }

    public sealed partial class OracleEnemy
    {
        internal bool TowerAggressionAllowed011 => EnemyTowerAggressionPolicy011.Allows(
            TrainingLabBound,
            trainingLabOwner != null && (trainingLabOwner.OwnsDummy(this) ||
                trainingLabOwner.SharedClient && object.ReferenceEquals(trainingLabOwner.Dummy, this)),
            trainingLabOwner != null && trainingLabOwner.MonsterAttackingTower);

        void ApplyTowerAggressionPolicy011()
        {
            if (TowerAggressionAllowed011) return;
            bool hadTower = blockingTower != null;
            bool towerCommit = pendingTower != null || pendingTowerAttack || pendingTowerVolley || committedTowerId != 0;
            bool remoteTower = remoteAttackStyle == 2;
            blockingTower = pendingTower = null;
            pendingTowerAttack = pendingTowerVolley = false;
            committedTowerId = 0;
            // Preserve a current core fan; only a tower fan is invalidated.
            if (!coreFan)
            {
                attackFan = false; AttackSlotReady = false; TowerApproachPoint = default;
            }
            if (towerCommit)
            {
                windup = strikeRemaining = 0f;
                pendingShrine = pendingPeerAttack = false; pendingPeerId = 0;
                committedAttackPoint = default; aimedDirection = laneForward;
            }
            if (remoteTower)
            {
                remoteAttackPhase = -1; remoteAttackProgress = remoteSnapshotProgress = remotePhaseElapsed = 0f;
                remoteAttackStyle = 0; remoteTargetPoint = default; remoteWindupCueSequence = -1;
                // Preserve the accepted snapshot/attack serial ledger; rejected tower state cannot reset replay fences.
            }
            if (hadTower || towerCommit) aggroSearchRemaining = 0f;
            if ((towerCommit || remoteTower) && warning != null) warning.gameObject.SetActive(false);
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing world-lane/shrine targets and presentation state; no transform position writes.
// Array Formats: Existing scalar commitments only; pure boolean/int default-deny policy, no arrays/allocations/timers.
// Dependencies/Pointers: Exact current training binding+intentional toggle only; normal host/replica tower state canceled without resetting HP/cooldown/rewards/generation or replay ledger. Native/peer acceptance pending.
