using System;

namespace OdysseyOracle
{
    public struct PlayerSprintState020
    {
        public float Value, RegenerationWait;
        public bool Held, Locked, Active;
        public static PlayerSprintState020 Full => new PlayerSprintState020 { Value = PlayerSprintRules020.Maximum };
    }

    // Pure finite-state resource transitions; the existing controller supplies simulation time and input.
    public static class PlayerSprintRules020
    {
        public const float Maximum = 100f, DrainPerSecond = 20f, RegenerationPerSecond = 25f;
        public const float RegenerationDelay = 1f, ReentryMinimum = 20f, MaximumStep = .05f;

        public static bool Step(ref PlayerSprintState020 state, float delta, bool held, bool moving, out bool exhausted)
        {
            exhausted = false;
            state.Active = false;
            if (float.IsNaN(delta) || float.IsInfinity(delta) || delta <= 0f) return false;
            delta = Math.Min(delta, MaximumStep);
            bool pressed = held && !state.Held;
            state.Held = held;
            if (state.Locked && pressed && state.Value >= ReentryMinimum) state.Locked = false;

            if (held && moving && !state.Locked && state.Value > 0f)
            {
                state.Value = Math.Max(0f, state.Value - DrainPerSecond * delta);
                state.RegenerationWait = RegenerationDelay;
                if (state.Value <= .0001f)
                {
                    state.Value = 0f;
                    state.Locked = true;
                    exhausted = true;
                    return false;
                }
                state.Active = true;
                return true;
            }

            float waiting = Math.Min(delta, state.RegenerationWait);
            state.RegenerationWait = Math.Max(0f, state.RegenerationWait - waiting);
            state.Value = Math.Min(Maximum, state.Value + RegenerationPerSecond * (delta - waiting));
            return false;
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing controlled movement simulation seconds and normalized scalar stamina0..100; no Unity Object access.
// Array Formats: One flat value state with two floats and three flags; no heap, native pointer, per-frame container or persisted/wire record.
// Dependencies/Pointers: OraclePlayer owns lifetime and input. Pure finite-state transitions consume20/sec, wait1sec, regenerate25/sec, and require a fresh press at20 after exhaustion.
