namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // Main-thread presentation input only; combat/host owns confirmation, launch,
    // recovery and chain time. These scalar masks cannot spend, cast or hide a wand.
    // No Unity objects, clocks, allocations, native jobs or transport ownership.
    public struct ConfirmedWandAppearanceState
    {
        int confirmedMask, acceptedMask, observedNovaCount;
        bool finisherReady, hasNovaBaseline;
        public int ConfirmedMask => confirmedMask;
        public int AcceptedMask => acceptedMask;
        public bool FinisherReady => finisherReady;

        public static bool ValidConfirmedMask(int mask)
        {
            if (mask < 1 || mask > 15) return false;
            int rest = mask & (mask - 1);
            return mask == 15 || rest == 0 || (rest & (rest - 1)) == 0;
        }

        public static bool HasLiveChain(float seconds) => seconds > 0f &&
            !float.IsNaN(seconds) && !float.IsInfinity(seconds);

        public bool SetMasks(int confirmed, int accepted, bool authorityReady)
        {
            int nextConfirmed = ValidConfirmedMask(confirmed) ? confirmed : 0;
            // A completed chain resets on its accepted launch. A forged/full mask
            // cannot create a Nova pulse; only the accepted own-cast counter can.
            int nextAccepted = accepted >= 0 && accepted < 15 ? accepted : 0;
            bool nextReady = authorityReady && nextConfirmed != 0 && nextAccepted != 0 &&
                (nextConfirmed | nextAccepted) == 15 && (nextConfirmed & ~nextAccepted) != 0;
            bool changed = nextConfirmed != confirmedMask || nextAccepted != acceptedMask || nextReady != finisherReady;
            confirmedMask = nextConfirmed; acceptedMask = nextAccepted; finisherReady = nextReady;
            return changed;
        }

        public bool ObserveNovaCount(int acceptedOwnCount)
        {
            if (acceptedOwnCount < 0) { hasNovaBaseline = false; return false; }
            bool pulse = hasNovaBaseline && acceptedOwnCount > observedNovaCount;
            observedNovaCount = acceptedOwnCount; hasNovaBaseline = true;
            return pulse;
        }

        public void Reset(int acceptedOwnCount = 0)
        {
            confirmedMask = acceptedMask = 0; finisherReady = false;
            observedNovaCount = acceptedOwnCount < 0 ? 0 : acceptedOwnCount;
            hasNovaBaseline = acceptedOwnCount >= 0;
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: cosmetic value-state, four ingredient bits;1/2/4 confirmed masks accepted and3 rejected, matching existing combat policy.
// Array Formats: unchanged scalar masks and accepted-own Nova observation; no heap/clock/Unity references.
// Dependencies/Pointers: presentation validity only; cannot commit recovery, launch a projectile, change host state or grant Nova.
