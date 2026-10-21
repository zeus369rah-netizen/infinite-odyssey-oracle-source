namespace OdysseyOracle
{
    public sealed partial class OraclePlayer
    {
        private PlayerSprintState020 sprintState020 = PlayerSprintState020.Full;
        public float Stamina020 => sprintState020.Value;
        public float StaminaFraction020 => sprintState020.Value / PlayerSprintRules020.Maximum;
        public bool StaminaExhausted020 => sprintState020.Locked;
        public bool SprintKeyHeld020 => sprintState020.Held;
        public bool Sprinting020 => sprintState020.Active;
        public uint SprintExhaustions020 { get; private set; }
        public uint SprintCueFailures020 { get; private set; }

        private void ResetSprint020()
        { sprintState020 = PlayerSprintState020.Full; SprintExhaustions020 = SprintCueFailures020 = 0; }
        private void SuspendSprint020() { sprintState020.Active = false; }
        private bool UpdateSprint020(bool held, bool moving, bool inputAllowed, float delta, GameSession session)
        {
            if (!inputAllowed) { SuspendSprint020(); return false; }
            bool active = PlayerSprintRules020.Step(ref sprintState020, delta, held, moving, out bool exhausted);
            if (exhausted)
            {
                if (SprintExhaustions020 < uint.MaxValue) SprintExhaustions020++;
                // An audio failure cannot skip the existing collision/movement path or retry every held frame.
                try { session?.Audio?.PlayPlayerExhausted020(); }
                catch (System.Exception) { if (SprintCueFailures020 < uint.MaxValue) SprintCueFailures020++; }
            }
            return active;
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing OraclePlayer controlled world movement tick and existing simulation time clamp.
// Array Formats: One owned scalar stamina state and two saturating diagnostic counters; no frame allocation, save, wire, controller or physics owner change.
// Dependencies/Pointers: Existing Initialize alone resets; control loss/downed/teleport suspend without refill or lock removal. Audio is best-effort presentation after one exhaustion transition.
