using System;
using UnityEngine;

namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // Snapshot-only value data and fixed scalar ledgers. Positions/ranges are
    // world metres, phase progress is normalized, serials are host/run-owned.
    // No physics, HP, Unity object lookup, transport send or native ownership.
    [Serializable]
    public struct HostileBoltState
    {
        // Short serialized fields keep the existing64KiB transport cap. Semantic
        // properties are not serialized by Unity's field-based serializer.
        [SerializeField] int s, g, e, q, l;
        [SerializeField] bool a, h;
        [SerializeField] Vector3 o, p, d, n;
        [SerializeField] float v, r, t;
        public int slot { get => s; set => s = value; }
        public int generation { get => g; set => g = value; }
        public int sourceEnemyId { get => e; set => e = value; }
        public int attackSerial { get => q; set => q = value; }
        public int lane { get => l; set => l = value; }
        public bool active { get => a; set => a = value; }
        public bool impact { get => h; set => h = value; }
        public Vector3 origin { get => o; set => o = value; }
        public Vector3 position { get => p; set => p = value; }
        public Vector3 direction { get => d; set => d = value; }
        public Vector3 normal { get => n; set => n = value; }
        public float speed { get => v; set => v = value; }
        public float remaining { get => r; set => r = value; }
        public float age { get => t; set => t = value; }
    }

    internal static class RemoteDangerPolicy
    {
        internal const int Version = 3003, Capacity = 48, MaximumActorId = 511;
        internal const int MaximumAttackSerial = 1000000, MaximumSnapshotCharacters = 64 * 1024;
        internal const float ShadeSpeed = 246.675f / 32f, ShadeRange = 588f / 32f;
        internal const float TerminalRetention = .5f, MaximumExtrapolation = .15f;
        internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        internal static bool Point(Vector3 p) => Finite(p.x) && Finite(p.y) && Finite(p.z) &&
            Math.Abs(p.x) <= 512f && Math.Abs(p.z) <= 512f && p.y >= -25f && p.y <= 100f;
        internal static bool Direction(Vector3 d)
        {
            if (!Finite(d.x) || !Finite(d.y) || !Finite(d.z)) return false;
            float square = d.x * d.x + d.y * d.y + d.z * d.z;
            return square >= .998f && square <= 1.002f;
        }
        internal static bool Phase(int phase, float progress, int attackSequence) =>
            phase >= 0 && phase <= 5 && Finite(progress) && progress >= 0f && progress <= 1f &&
            attackSequence >= 0 && attackSequence <= MaximumAttackSerial;
        internal static bool Style(int style, float windup, float strike, float recovery) =>
            style >= 0 && style <= 2 && Finite(windup) && windup >= .2f && windup <= 3f &&
            Finite(strike) && strike >= .01f && strike <= 1f && Finite(recovery) && recovery >= .2f && recovery <= 8f;
        internal static bool Run(string run)
        {
            if (run == null || run.Length != 32) return false;
            for (int i = 0; i < run.Length; i++)
            {
                char c = run[i];
                if (!(c >= '0' && c <= '9' || c >= 'a' && c <= 'f')) return false;
            }
            return true;
        }
        internal static bool Bolt(HostileBoltState value)
        {
            if (value.slot < 0 || value.slot >= Capacity || value.generation < 1 ||
                value.sourceEnemyId < 1 || value.sourceEnemyId > MaximumActorId ||
                value.attackSerial < 1 || value.attackSerial > MaximumAttackSerial || value.lane < 0 || value.lane > 7 ||
                !Point(value.origin) || !Point(value.position) || !Direction(value.direction) ||
                !Finite(value.speed) || Math.Abs(value.speed - ShadeSpeed) > .001f ||
                !Finite(value.remaining) || value.remaining < 0f || value.remaining > ShadeRange + .001f ||
                !Finite(value.age) || value.age < 0f || value.age > TerminalRetention + .001f ||
                value.active && (value.impact || value.age != 0f)) return false;
            float x = value.position.x - value.origin.x, y = value.position.y - value.origin.y, z = value.position.z - value.origin.z;
            float limit = ShadeRange + .25f;
            if (x * x + y * y + z * z > limit * limit) return false;
            return value.impact ? Direction(value.normal) :
                Finite(value.normal.x) && Finite(value.normal.y) && Finite(value.normal.z);
        }
    }

    internal struct RemoteAttackPresentationLedger
    {
        int snapshot, attack;
        internal bool Apply(int snapshotSequence, int attackSequence, int nextPhase, float nextProgress)
        {
            if (snapshotSequence <= snapshot || attackSequence < attack ||
                !RemoteDangerPolicy.Phase(nextPhase, nextProgress, attackSequence)) return false;
            // Interrupts can reset an authoritative recovery timer. Snapshot and
            // attempt serials fence replay; normalized progress need not increase.
            snapshot = snapshotSequence; attack = attackSequence;
            return true;
        }
        internal void Clear() { snapshot = attack = 0; }
    }

    internal sealed class RemoteDangerRunGate
    {
        // Bounded, fail-closed per-connection history. A reconnect clears it.
        // This avoids accepting an earlier run after a reset or replayed prepare.
        readonly string[] runs = new string[64];
        int count, snapshot;
        string current;
        internal bool Prepare(string run)
        {
            if (!RemoteDangerPolicy.Run(run)) return false;
            if (run == current) return true;
            for (int i = 0; i < count; i++) if (runs[i] == run) return false;
            if (count == runs.Length) return false;
            runs[count++] = current = run; snapshot = 0; return true;
        }
        internal bool Accept(string run, int sequence)
        {
            if (sequence < 1 || !RemoteDangerPolicy.Run(run)) return false;
            if (current == null && !Prepare(run)) return false;
            if (run != current || sequence <= snapshot) return false;
            snapshot = sequence; return true;
        }
        internal void EndRun() { current = null; snapshot = 0; }
        internal void ResetConnection()
        { for (int i = 0; i < count; i++) runs[i] = null; count = snapshot = 0; current = null; }
    }

    internal sealed class RemoteBoltLedger
    {
        readonly int[] latest = new int[RemoteDangerPolicy.Capacity];
        readonly bool[] ended = new bool[RemoteDangerPolicy.Capacity];
        internal bool Apply(HostileBoltState value, out bool impact)
        {
            impact = false;
            if (!RemoteDangerPolicy.Bolt(value)) return false;
            int slot = value.slot;
            if (value.generation < latest[slot] || value.generation == latest[slot] && ended[slot]) return false;
            if (value.generation > latest[slot]) { latest[slot] = value.generation; ended[slot] = false; }
            if (!value.active) { ended[slot] = true; impact = value.impact; }
            return true;
        }
        internal void Clear()
        { for (int i = 0; i < latest.Length; i++) { latest[i] = 0; ended[i] = false; } }
    }
}
