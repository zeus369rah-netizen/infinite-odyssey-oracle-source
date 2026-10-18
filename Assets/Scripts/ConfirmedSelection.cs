namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // Pure value-state: four ingredient bits, one optional pending index and a
    // confirmed dominant element. No time, Unity/native/transport/profile owner.
    // The cast owner commits caller-owned4 timestamps only after actual launch.
    public struct ConfirmedSelection
    {
        // One isolated policy switch; host and solo compile the same value.
        public static readonly bool SingleTapCastsImmediately = true;
        int pending, mask, dominant;
        bool acceptedSinceTap;
        public int PreparedElement => pending - 1;
        public int ConfirmedMask => mask;
        public int ConfirmedElement => mask == 0 ? -1 : dominant - 1;
        public int PreviewElement => pending != 0 ? pending - 1 : mask != 0 ? dominant - 1 : 0;
        public bool SelectionPending => pending != 0;
        public bool HasConfirmedSelection => pending == 0 && ValidCastMask(mask);
        public int Combo => HasConfirmedSelection ? ComboForMask(mask) : 0;

        public bool Tap(int element)
        {
            if (element < 0 || element > 3) return false;
            if (acceptedSinceTap) Clear();
            if (pending != 0)
            {
                mask = (1 << (pending - 1)) | (1 << element);
                dominant = element + 1; pending = 0;
            }
            else if (mask != 0) { mask |= 1 << element; dominant = element + 1; }
            else if (SingleTapCastsImmediately) { mask = 1 << element; dominant = element + 1; }
            else { pending = element + 1; dominant = 0; }
            return true;
        }
        // Preserve the recipe for repeat/auto-fire; the next tap starts a new recipe.
        // Called only after a pooled projectile was accepted, never on selection/failure.
        public void MarkCastAccepted() { if (HasConfirmedSelection) acceptedSinceTap = true; }
        public bool Restore(int preparedElement, int confirmedMask, int confirmedElement)
        {
            if (!ValidState(preparedElement, confirmedMask, confirmedElement)) return false;
            pending = preparedElement + 1; mask = confirmedMask; dominant = confirmedMask == 0 ? 0 : confirmedElement + 1; acceptedSinceTap = false;
            return true;
        }
        public void Clear() { pending = mask = dominant = 0; acceptedSinceTap = false; }
        public static bool ValidMask(int value) => value >= 1 && value <= 15;
        public static bool ValidCastMask(int value)
        {
            if (!ValidMask(value)) return false;
            int rest = value & (value - 1);
            return value == 15 || rest == 0 || (rest & (rest - 1)) == 0;
        }
        public static bool ValidState(int prepared, int confirmedMask, int confirmedElement)
        {
            if (prepared < -1 || prepared > 3) return false;
            if (prepared >= 0 || confirmedMask == 0) return confirmedMask == 0 && confirmedElement == -1;
            return ValidMask(confirmedMask) && confirmedElement >= 0 && confirmedElement < 4 && (confirmedMask & (1 << confirmedElement)) != 0;
        }
        public static int ComboForMask(int value) => value == 9 ? 1 : value == 6 ? 2 : value == 3 ? 3 :
            value == 12 ? 4 : value == 5 ? 5 : value == 10 ? 6 : 0;
        public bool Ready(double now, double primaryReadyAt, double[] readyAt)
        {
            if (!HasConfirmedSelection || !Finite(now) || now < 0d || !Finite(primaryReadyAt) ||
                primaryReadyAt < 0d || now < primaryReadyAt || readyAt == null || readyAt.Length != 4) return false;
            for (int i = 0; i < 4; i++)
                if (!Finite(readyAt[i]) || readyAt[i] < 0d || (mask & (1 << i)) != 0 && now < readyAt[i]) return false;
            return true;
        }
        public static bool CommitRecovery(int usedMask, double now, double seconds, double[] readyAt)
        {
            if (!ValidCastMask(usedMask) || !Finite(now) || now < 0d || !Finite(seconds) || seconds <= 0d ||
                readyAt == null || readyAt.Length != 4 || !Finite(now + seconds)) return false;
            for (int i = 0; i < 4; i++) if ((usedMask & (1 << i)) != 0) readyAt[i] = now + seconds;
            return true;
        }
        static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        public static bool Newer(uint candidate, uint previous) => unchecked((int)(candidate - previous)) > 0;
    }

    public struct ConfirmedRequestSequence
    {
        public uint Last { get; private set; }
        public bool Accept(uint value) { if (!ConfirmedSelection.Newer(value, Last)) return false; Last = value; return true; }
    }

    public struct ManualCastClickGate
    {
        bool armed;
        public bool Step(bool context, bool pressed, bool pressedThisFrame)
        {
            if (!context) { armed = false; return false; }
            if (!pressed) { armed = true; return false; }
            if (!armed || !pressedThisFrame) return false;
            armed = false; return true;
        }
        public void Clear() { armed = false; }
    }
}

// # ARCHITECTURE SNAPSHOT
// Unity metres, Y-up/XZ; four ingredient bits and caller-owned four-double recovery timestamps.
// Existing host/solo owners retain simulation clocks, pooled48 projectiles and private native memory lifetimes.
