using System;

namespace OdysseyOracle
{
    // Independent cosmetic envelope; the caller already owns selection and accepted casts.
    public struct StaffEngulfState020
    {
        public const float Duration = 1f;
        public int SelectionMask { get; private set; }
        public int Mask { get; private set; }
        public float Age { get; private set; }
        public float Phase { get; private set; }
        float level, target;
        public bool Active => Mask != 0 && Age < Duration;
        public float Strength
        {
            get
            {
                if (!Active) return 0f;
                float t = Math.Max(0f, Math.Min(1f, (Duration - Age) / .25f));
                return level * t * t * (3f - 2f * t);
            }
        }
        public bool Select(int mask)
        {
            if ((mask != 0 && !StaffSpiralState.ValidMask(mask)) || mask == SelectionMask) return false;
            SelectionMask = mask;
            if (mask == 0) return false;
            Begin(mask, .7f);
            return true;
        }
        public bool Launch(int mask)
        {
            if (!StaffSpiralState.ValidMask(mask)) return false;
            Begin(mask, 1f);
            return true;
        }
        void Begin(int mask, float nextTarget)
        {
            // Preserve the visible value and phase even when an accepted rapid cast retriggers.
            level = Strength;
            Mask = mask; Age = 0f; target = nextTarget;
        }
        public void Step(float delta)
        {
            if (!Active || float.IsNaN(delta) || float.IsInfinity(delta) || delta <= 0f) return;
            delta = Math.Min(.05f, delta);
            Age = Math.Min(Duration, Age + delta);
            Phase += delta * .9f;
            if (Phase >= 1f) Phase -= 1f;
            level += (target - level) * (1f - (float)Math.Exp(-22f * delta));
            if (!Active) level = 0f;
        }
        public void Clear() { SelectionMask = Mask = 0; Age = Phase = level = target = 0f; }
        public static float Sling(float pitchDegrees)
        {
            if (float.IsNaN(pitchDegrees) || float.IsInfinity(pitchDegrees)) return 0f;
            float t = Math.Max(0f, Math.Min(1f, pitchDegrees / 4.5f));
            return .028f * t * t * (3f - 2f * t);
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: private unpaused cosmetic seconds and phase in[0,1); one/two/four-bit original element masks only.
// Array Formats: two ints and four floats, no reference fields, heap, native container or Unity object; one-second envelope with continuous retrigger value/phase; finite accepted-spring forward sling bounded0..0.028m.
// Dependencies/Pointers: existing StaffSpiralState validates masks; caller owns all confirmed selection, accepted launch, pause/reset and cached shader uploads. No gameplay/audio/network/save authority.
