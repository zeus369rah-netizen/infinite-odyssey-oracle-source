using System;
using System.Runtime.InteropServices;

namespace OdysseyOracle
{
    // Small nonspatial control policy, one main-thread owner; not a jobs/transport owner.
    public sealed class RoomIdlePolicy
    {
        public const int Capacity = 8;
        public const double IdleSeconds = 600d, WarningSeconds = 60d;

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        struct Slot
        {
            public ulong Id;
            public double LastActivity, NextReport;
            public uint Generation, LastSequence, HasSequence, Occupied;
        }

        // Exactly one cold allocation. All active methods reuse these eight records.
        readonly Slot[] slots = new Slot[Capacity];
        double clock, warningSentAt = -1d, resumeUntil;
        public ulong Epoch { get; private set; }
        public uint RosterRevision { get; private set; }
        public uint ActivityRevision { get; private set; }
        public int Count { get; private set; }
        public bool IsClosing { get; private set; }

        public enum Decision { Blocked, Active, NeedWarning, Warning, ReadyToClose, Closing }

        static bool FiniteTime(double value)
        { return !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0d && value <= 1e12d; }
        bool AcceptTime(double now)
        { if (!FiniteTime(now) || now < clock) return false; clock = now; return true; }
        void InvalidateWarning()
        { warningSentAt = -1d; unchecked { ActivityRevision++; } }

        public bool Reset(ulong epoch, double now)
        {
            if (epoch == 0 || !FiniteTime(now)) return false;
            Array.Clear(slots, 0, slots.Length);
            Epoch = epoch; clock = now; warningSentAt = -1d; resumeUntil = 0d;
            RosterRevision = ActivityRevision = 0; Count = 0; IsClosing = false;
            return true;
        }

        public void Disable()
        { Epoch = 0; Count = 0; IsClosing = false; warningSentAt = -1d; }

        public bool Bind(ulong epoch, int slot, ulong id, double now, out uint generation)
        {
            generation = 0;
            if (Epoch == 0 || epoch != Epoch || IsClosing || (uint)slot >= Capacity ||
                (id == 0) != (slot == 0) || !AcceptTime(now)) return false;
            Slot value = slots[slot];
            if (value.Occupied != 0 && value.Id == id) { generation = value.Generation; return true; }
            for (int i = 0; i < Capacity; i++)
                if (i != slot && slots[i].Occupied != 0 && slots[i].Id == id) return false;
            unchecked { generation = value.Generation + 1; }
            if (generation == 0) generation = 1;
            if (value.Occupied == 0) Count++;
            slots[slot] = new Slot { Id = id, LastActivity = now, Generation = generation, Occupied = 1 };
            unchecked { RosterRevision++; }
            InvalidateWarning();
            return true;
        }

        bool Matches(ulong epoch, int slot, ulong id, uint generation)
        {
            return Epoch != 0 && epoch == Epoch && !IsClosing && (uint)slot < Capacity &&
                slots[slot].Occupied != 0 && slots[slot].Id == id && slots[slot].Generation == generation;
        }

        public bool Remove(ulong epoch, int slot, ulong id, uint generation, double now)
        {
            if (!Matches(epoch, slot, id, generation) || !AcceptTime(now)) return false;
            Slot value = slots[slot]; value.Occupied = 0; slots[slot] = value; Count--;
            unchecked { RosterRevision++; }
            InvalidateWarning();
            return true;
        }

        // Caller must have accepted a genuine player action, never a heartbeat/automatic cast.
        public bool RecordAcceptedAction(ulong epoch, int slot, ulong id, uint generation, double now)
        {
            if (!Matches(epoch, slot, id, generation) || !AcceptTime(now)) return false;
            Slot value = slots[slot]; value.LastActivity = now; slots[slot] = value;
            InvalidateWarning();
            return true;
        }

        // The adapter must first authenticate NGO sender, exact wire size/kind/flags and current connection.
        public bool RecordClientReport(ulong epoch, int slot, ulong id, uint generation, uint sequence, double now)
        {
            if (!Matches(epoch, slot, id, generation) || !AcceptTime(now)) return false;
            Slot value = slots[slot];
            if (now < value.NextReport || value.HasSequence != 0 && unchecked((int)(sequence - value.LastSequence)) <= 0)
                return false;
            value.LastSequence = sequence; value.HasSequence = 1; value.NextReport = now + 1d;
            value.LastActivity = now; slots[slot] = value; InvalidateWarning();
            return true;
        }

        public bool DeferAfterForeground(ulong epoch, double now)
        {
            if (epoch != Epoch || Epoch == 0 || IsClosing || !AcceptTime(now)) return false;
            resumeUntil = Math.Max(resumeUntil, now + 2d);
            return true;
        }

        public Decision Evaluate(double now, bool rosterComplete, out double remaining)
        {
            remaining = 0d;
            if (Epoch == 0 || !AcceptTime(now)) return Decision.Blocked;
            if (IsClosing) return Decision.Closing;
            if (!rosterComplete || Count == 0 || slots[0].Occupied == 0) return Decision.Blocked;
            double latest = 0d;
            for (int i = 0; i < Capacity; i++)
                if (slots[i].Occupied != 0) latest = Math.Max(latest, slots[i].LastActivity);
            double idleDeadline = latest + IdleSeconds;
            if (now < idleDeadline - WarningSeconds)
            { remaining = idleDeadline - now; return Decision.Active; }
            if (warningSentAt < 0d) { remaining = WarningSeconds; return Decision.NeedWarning; }
            double deadline = Math.Max(Math.Max(idleDeadline, warningSentAt + WarningSeconds), resumeUntil);
            remaining = Math.Max(0d, deadline - now);
            return now >= deadline ? Decision.ReadyToClose : Decision.Warning;
        }

        // Call only after local warning is visible and host state publication was queued successfully.
        public bool ConfirmWarningPublished(ulong epoch, uint rosterRevision, uint activityRevision,
            double now, bool rosterComplete)
        {
            double unused;
            if (epoch != Epoch || rosterRevision != RosterRevision || activityRevision != ActivityRevision ||
                Evaluate(now, rosterComplete, out unused) != Decision.NeedWarning) return false;
            warningSentAt = now;
            return true;
        }

        public bool TryCommitClose(ulong epoch, uint rosterRevision, uint activityRevision,
            double now, bool rosterComplete)
        {
            double unused;
            if (epoch != Epoch || rosterRevision != RosterRevision || activityRevision != ActivityRevision ||
                Evaluate(now, rosterComplete, out unused) != Decision.ReadyToClose) return false;
            IsClosing = true;
            return true;
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: No spatial coordinates; host-local monotonic unscaled seconds, finite domain0..1e12.
// Array Formats: One cold managed Slot[8],40-byte blittable numeric records; no hot allocations or engine-object loops.
// Dependencies/Pointers: System only; single control-thread owner authenticates roster/actions and owns all transport/SDK cleanup. No gameplay binding is installed.
