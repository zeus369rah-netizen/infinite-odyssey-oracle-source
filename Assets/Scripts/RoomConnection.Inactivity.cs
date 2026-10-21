using System;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class RoomConnection
    {
        const string IdleChannel = "odyssey.room.idle.v1";
        const int IdleFrameBytes = 28;
        readonly RoomIdlePolicy idlePolicy = new RoomIdlePolicy();
        readonly ulong[] idleIds = new ulong[MaximumPlayers];
        readonly uint[] idleGenerations = new uint[MaximumPlayers];
        int idleMask;
        ulong idleEpoch;
        uint idleLocalGeneration, idleReportSequence, idleStateSequence, idleAppliedSequence;
        double nextIdleReport, nextIdleStatus, idleViewDeadline, idleCloseDispatchAt;
        bool idleInputDirty, idleStayDirty, idleStopAdmissions;
        Task<string> ownedLeaveTask;
        bool closeHostedSessionNextDisconnect;
        ISession pendingIdleHostDeletion;
        internal int InactivityLifecycleGeneration => operation;
        internal bool InactivityOwnerAlive => !destroyed;
        public int IdleWarningPhase { get; private set; }
        public int IdleWarningSeconds => IdleWarningPhase == 1 ? Math.Max(0, (int)Math.Ceiling(idleViewDeadline - Time.realtimeSinceStartupAsDouble)) : 0;

        void InitializeRoomInactivity()
        {
            ResetRoomInactivity(); idleStopAdmissions = false;
            if (!IsHost) return;
            byte[] nonce = Guid.NewGuid().ToByteArray(); // Connection-only allocation, never an active tick.
            idleEpoch = BitConverter.ToUInt64(nonce, 0); if (idleEpoch == 0) idleEpoch = 1;
            idlePolicy.Reset(idleEpoch, Time.realtimeSinceStartupAsDouble);
        }
        void ResetRoomInactivity()
        {
            idlePolicy.Disable(); idleEpoch = 0; idleMask = 0; idleLocalGeneration = 0;
            idleReportSequence = idleStateSequence = idleAppliedSequence = 0;
            nextIdleReport = nextIdleStatus = idleViewDeadline = 0d;
            idleCloseDispatchAt = double.PositiveInfinity;
            idleInputDirty = idleStayDirty = false; IdleWarningPhase = 0;
            Array.Clear(idleIds, 0, idleIds.Length); Array.Clear(idleGenerations, 0, idleGenerations.Length);
        }

        // Nonspatial membership control only. No input is inferred from recurrent pose traffic.
        bool ReconcileIdleRoster(double now)
        {
            if (!IsHost || network == null) return false;
            int mask = 0; bool complete = admissions.PendingCount == 0;
            var ids = network.ConnectedClientsIds;
            if (ids.Count < 1 || ids.Count > MaximumPlayers) return false;
            for (int i = 0; i < ids.Count; i++)
            {
                ulong id = ids[i];
                if (!admissions.TryGetSlot(id, out int slot) || (uint)slot >= MaximumPlayers)
                { complete = false; continue; }
                if (!idlePolicy.Bind(idleEpoch, slot, id, now, out uint generation)) { complete = false; continue; }
                idleIds[slot] = id; idleGenerations[slot] = generation; mask |= 1 << slot;
            }
            for (int slot = 0; slot < MaximumPlayers; slot++)
                if ((idleMask & (1 << slot)) != 0 && (mask & (1 << slot)) == 0)
                    idlePolicy.Remove(idleEpoch, slot, idleIds[slot], idleGenerations[slot], now);
            idleMask = mask;
            return complete && idlePolicy.Count == ids.Count;
        }

        public void RecordMeaningfulLocalActivity(bool stayConnected = false)
        {
            if (!Connected || leaving || network == null) return;
            if (IsHost) RecordAcceptedPeerActivity(LocalClientId);
            else { idleInputDirty = true; idleStayDirty |= stayConnected; }
        }
        // Invoke only after a real action passed its existing owner's validation/deduplication.
        public void RecordAcceptedPeerActivity(ulong sender)
        {
            if (!Connected || !IsHost || leaving || !admissions.TryGetSlot(sender, out int slot) ||
                (uint)slot >= MaximumPlayers || (idleMask & (1 << slot)) == 0 || idleIds[slot] != sender) return;
            idlePolicy.RecordAcceptedAction(idleEpoch, slot, sender, idleGenerations[slot], Time.realtimeSinceStartupAsDouble);
        }
        void SendIdleFrame(ulong recipient, uint generation, uint sequence, uint kind, uint remaining, uint flags)
        {
            using (var writer = new FastBufferWriter(IdleFrameBytes, Allocator.Temp))
            {
                writer.WriteValueSafe(idleEpoch); writer.WriteValueSafe(generation); writer.WriteValueSafe(sequence);
                writer.WriteValueSafe(kind); writer.WriteValueSafe(remaining); writer.WriteValueSafe(flags);
                network.CustomMessagingManager.SendNamedMessage(IdleChannel, recipient, writer, NetworkDelivery.ReliableSequenced);
            }
        }
        void PublishIdleState(int phase, double remaining, double now)
        {
            uint milliseconds = (uint)Math.Max(0d, Math.Min(600000d, Math.Ceiling(remaining * 1000d)));
            uint serial = ++idleStateSequence;
            for (int slot = 0; slot < MaximumPlayers; slot++)
                if ((idleMask & (1 << slot)) != 0 && idleIds[slot] != LocalClientId && IsConnectedPeer(idleIds[slot]))
                    SendIdleFrame(idleIds[slot], idleGenerations[slot], serial, 2, milliseconds, (uint)phase);
            IdleWarningPhase = phase; idleViewDeadline = now + milliseconds / 1000d;
            GameSession.Instance?.DisplayRoomIdleWarning(phase == 1, phase == 1 ? (int)Math.Ceiling(remaining) : 0);
            nextIdleStatus = now + 1d;
        }

        void ReceiveIdleFrame(ulong sender, FastBufferReader reader)
        {
            if (!Connected || leaving || network == null || reader.Length != IdleFrameBytes || !reader.TryBeginRead(IdleFrameBytes)) return;
            reader.ReadValueSafe(out ulong epoch); reader.ReadValueSafe(out uint generation); reader.ReadValueSafe(out uint serial);
            reader.ReadValueSafe(out uint kind); reader.ReadValueSafe(out uint remaining); reader.ReadValueSafe(out uint flags);
            if (epoch == 0 || generation == 0) return;
            double now = Time.realtimeSinceStartupAsDouble;
            if (IsHost)
            {
                if (kind != 1 || remaining != 0 || (flags != 1 && flags != 2) || sender == LocalClientId ||
                    !IsConnectedPeer(sender) || !admissions.TryGetSlot(sender, out int slot) || (uint)slot >= MaximumPlayers) return;
                idlePolicy.RecordClientReport(epoch, slot, sender, generation, serial, now);
            }
            else
            {
                if (sender != NetworkManager.ServerClientId || kind != 2 || flags > 2 || remaining > 600000 ||
                    (idleEpoch != 0 && epoch != idleEpoch) || !Newer(serial, idleAppliedSequence)) return;
                idleEpoch = epoch; idleLocalGeneration = generation; idleAppliedSequence = serial;
                IdleWarningPhase = (int)flags; idleViewDeadline = now + remaining / 1000d;
                GameSession.Instance?.DisplayRoomIdleWarning(flags == 1, flags == 1 ? (int)Math.Ceiling(remaining / 1000d) : 0);
                if (flags == 2) GameSession.Instance?.BeginIdleApplicationEnd(false, idleEpoch);
            }
        }

        // LateUpdate follows local input Update and NGO's normal receive processing.
        void LateUpdate()
        {
            if (!Connected || leaving || destroyed || network == null) return;
            double now = Time.realtimeSinceStartupAsDouble;
            if (!IsHost)
            {
                if (idleInputDirty && idleEpoch != 0 && idleLocalGeneration != 0 && now >= nextIdleReport)
                {
                    SendIdleFrame(NetworkManager.ServerClientId, idleLocalGeneration, ++idleReportSequence, 1, 0, idleStayDirty ? 2u : 1u);
                    idleInputDirty = idleStayDirty = false; nextIdleReport = now + 1d;
                }
                return;
            }
            if (idlePolicy.IsClosing)
            {
                if (now >= idleCloseDispatchAt)
                { idleCloseDispatchAt = double.PositiveInfinity; GameSession.Instance?.BeginIdleApplicationEnd(true, idleEpoch); }
                return;
            }
            bool complete = ReconcileIdleRoster(now);
            var decision = idlePolicy.Evaluate(now, complete, out double remaining);
            if (decision == RoomIdlePolicy.Decision.NeedWarning)
            {
                uint roster = idlePolicy.RosterRevision, activity = idlePolicy.ActivityRevision;
                PublishIdleState(1, 60d, now);
                idlePolicy.ConfirmWarningPublished(idleEpoch, roster, activity, now, complete);
            }
            else if (decision == RoomIdlePolicy.Decision.ReadyToClose)
            {
                if (!idlePolicy.TryCommitClose(idleEpoch, idlePolicy.RosterRevision, idlePolicy.ActivityRevision, now, complete)) return;
                idleStopAdmissions = true;
                idleCloseDispatchAt = now + 2d; // Best-effort reliable flush grace; never a delivery guarantee.
                PublishIdleState(2, 0d, now);
            }
            else if (decision == RoomIdlePolicy.Decision.Active || decision == RoomIdlePolicy.Decision.Warning)
            {
                int phase = decision == RoomIdlePolicy.Decision.Warning ? 1 : 0;
                if (now >= nextIdleStatus || phase != IdleWarningPhase) PublishIdleState(phase, phase == 1 ? remaining : 0d, now);
            }
            else if (decision == RoomIdlePolicy.Decision.Blocked && IdleWarningPhase != 0) PublishIdleState(0, 0d, now);
        }
        void OnApplicationFocus(bool focused)
        { if (focused && IsHost) idlePolicy.DeferAfterForeground(idleEpoch, Time.realtimeSinceStartupAsDouble); }

        public Task<string> LeaveRoomAsync() { return BeginOwnedLeave(false, false, "You left the game."); }
        internal Task<string> CloseIdleRoomAsync(ulong expectedEpoch)
        {
            if (!IsHost || leaving || expectedEpoch != idleEpoch || !idlePolicy.IsClosing)
                return Task.FromResult("The room changed before idle closure; no new room was closed.");
            return BeginOwnedLeave(true, true, "The room ended after all players were inactive.");
        }
        internal Task<string> LeaveIdleGuestAsync(ulong expectedEpoch)
        {
            if (IsHost || expectedEpoch == 0 || expectedEpoch != idleEpoch || IdleWarningPhase != 2)
                return Task.FromResult("The room changed before idle closure; no new room was closed.");
            return BeginOwnedLeave(true, false, "The room ended after all players were inactive.");
        }
        Task<string> BeginOwnedLeave(bool pause, bool deleteHostedSession, string completion)
        {
            if (ownedLeaveTask != null && !ownedLeaveTask.IsCompleted) return ownedLeaveTask;
            if (destroyed) return Task.FromResult("Application cleanup has already started.");
            var result = new TaskCompletionSource<string>(); // One cold lifecycle owner, assigned before callbacks/awaits.
            ownedLeaveTask = result.Task;
            int ticket = ++operation; leaving = Busy = true;
            closeHostedSessionNextDisconnect = deleteHostedSession;
            Status = "Closing the game connection...";
            if (pause) { Connected = false; playerCount = 0; GameSession.Instance?.Pause(); }
            CompleteOwnedLeaveAsync(ticket, completion, result);
            return result.Task;
        }
        async void CompleteOwnedLeaveAsync(int ticket, string completion, TaskCompletionSource<string> result)
        {
            string failure = null;
            try
            {
                try { await CancelAndDrainConnectionAttemptAsync(); }
                catch (Exception error) { failure = Explain(error); }
                try
                {
                    string cleanup = await DisconnectAsync();
                    if (cleanup != null) failure = failure == null ? cleanup : failure + "; " + cleanup;
                }
                catch (Exception error) { failure = failure == null ? Explain(error) : failure + "; " + Explain(error); }
                if (IsCurrent(ticket)) Status = failure == null ? completion : "Disconnected locally; room cleanup failed: " + failure;
            }
            finally
            {
                leaving = false; if (!destroyed) Busy = pendingConnections > 0;
                result.TrySetResult(failure);
            }
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Nonspatial NGO connection IDs and host-local unscaled monotonic seconds.
// Array Formats: Policy8 slots plus8 cached IDs/generations; new28-byte binary idle channel, existing36-byte poses unchanged.
// Dependencies/Pointers: Partial existing RoomConnection owns all handlers/Task cleanup; queued work candidate only. SDK delete/leave selection requires guarded owner hunks. No health, rewards, saves or manager owner added.
