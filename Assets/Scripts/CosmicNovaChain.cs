namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // Pure value-type accepted-shot state: four ingredient bits, simulation
    // seconds and no Unity objects/native containers. Caller owns the clock and
    // commits only after an accepted pooled launch. Selection never mutates this
    // state. This is data-oriented source, not a claimed Burst-compiled job.
    public struct CosmicNovaChain
    {
        public const double WindowSeconds = 4d;
        int ingredients;
        double startedAt;

        static bool ValidTime(double now) => !double.IsNaN(now) && !double.IsInfinity(now) && now >= 0d;
        public int MaskAt(double now) => ValidTime(now) && now >= startedAt && now - startedAt <= WindowSeconds ? ingredients : 0;
        public float RemainingAt(double now)
        {
            if (MaskAt(now) == 0) return 0f;
            double remaining = WindowSeconds - (now - startedAt);
            return remaining > 0d ? (float)remaining : 0f;
        }

        // Confirmed input remains one/two bits. Two accepted complementary
        // pairs (or single+pair+single) can fill the four-bit accumulator.
        public bool WouldCompleteMask(int acceptedMask, double now) => ConfirmedSelection.ValidMask(acceptedMask) &&
            ValidTime(now) && (MaskAt(now) | acceptedMask) == 15;

        public bool CommitAcceptedMask(int acceptedMask, double now)
        {
            if (!ConfirmedSelection.ValidMask(acceptedMask) || !ValidTime(now)) return false;
            int current = MaskAt(now);
            if (current == 0) startedAt = now;
            ingredients = current | acceptedMask;
            if (ingredients != 15) return false;
            Reset(); return true;
        }

        public bool WouldComplete(int element, double now) => element >= 0 && element < 4 && WouldCompleteMask(1 << element, now);
        public bool CommitAccepted(int element, double now) => element >= 0 && element < 4 && CommitAcceptedMask(1 << element, now);

        public void Reset() { ingredients = 0; startedAt = 0d; }
    }
}
