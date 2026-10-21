using System;
namespace OdysseyOracle
{
    public struct MoteBreathingState { public float Brightness, RangeFactor; }
    public static class MoteEnvelopeMath
    {
        public const float FadeInEnd = .12f, FadeOutStart = .62f, InvisibleTail = .96f;
        public static float Envelope(float age)
        {
            if (float.IsNaN(age) || float.IsInfinity(age) || age <= 0f || age >= InvisibleTail) return 0f;
            return Smooth(0f, FadeInEnd, age) * (1f - Smooth(FadeOutStart, InvisibleTail, age));
        }
        public static MoteBreathingState Breath(float time)
        {
            float unit = float.IsNaN(time) || float.IsInfinity(time) ? .5f : .5f + .5f * (float)Math.Sin((double)time * 1.05d);
            unit = Math.Min(1f, Math.Max(0f, unit));
            return new MoteBreathingState { Brightness = .88f + .12f * unit, RangeFactor = .96f + .04f * unit };
        }
        static float Smooth(float begin, float end, float value)
        {
            float t = Math.Min(1f, Math.Max(0f, (value - begin) / (end - begin)));
            return t * t * (3f - 2f * t);
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: normalized existing mote age and unchanged visual seconds; no world/spatial/gameplay operations.
// Array Formats: scalar envelope plus two-float breathing value; no allocations/native or Unity object ownership.
// Dependencies/Pointers: bounded smoothstep taper is used for shrine size+alpha; GPU shader matches constants. Root validates shader/native/visual parity.
