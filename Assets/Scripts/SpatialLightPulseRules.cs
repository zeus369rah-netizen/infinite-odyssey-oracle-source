using System;

namespace OdysseyOracle
{
    internal enum SpatialLightPulseKind : byte { Steady, Warm, Violet }

    /// <summary>Scalar-only presentation rules. No Unity object, random state or heap ownership.</summary>
    internal static class SpatialLightPulseRules
    {
        // Both angular frequencies complete an integer number of cycles over this period.
        internal const double Period = 20.0 * Math.PI;

        internal static float WarmPhase(int group)
        {
            unchecked
            {
                uint bits = (uint)group * 747796405u + 2891336453u;
                bits = ((bits >> ((int)(bits >> 28) + 4)) ^ bits) * 277803737u;
                bits = (bits >> 22) ^ bits;
                return (float)((bits & 65535u) * (2.0 * Math.PI / 65536.0));
            }
        }

        internal static float Evaluate(SpatialLightPulseKind kind, double seconds, float warmPhase)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds)) seconds = 0.0;
            if (float.IsNaN(warmPhase) || float.IsInfinity(warmPhase)) warmPhase = 0f;
            if (kind == SpatialLightPulseKind.Warm)
                return (float)(.90 + .075 * Math.Sin(.9 * seconds + warmPhase)
                    + .025 * Math.Sin(1.7 * seconds + .61 * warmPhase));
            if (kind == SpatialLightPulseKind.Violet)
                return (float)(.92 + .08 * Math.Sin(1.7 * seconds));
            return 1f;
        }
    }

    /// <summary>One shared sample per rendered frame; pauses never accumulate a resume jump.</summary>
    internal struct SpatialLightPulseClock
    {
        private double seconds;
        private int lastFrame;
        private bool hasFrame;

        internal double Sample(int frame, float deltaSeconds, bool suspended)
        {
            if (hasFrame && frame == lastFrame) return seconds;
            hasFrame = true;
            lastFrame = frame;
            if (suspended || float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds) || deltaSeconds <= 0f)
                return seconds;
            seconds += Math.Min(.1, deltaSeconds);
            if (seconds >= SpatialLightPulseRules.Period) seconds -= SpatialLightPulseRules.Period;
            return seconds;
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Presentation seconds/radians only; authored spatial range remains in URP.
// Array Formats: Blittable scalar clock/enum; no managed containers or allocation in numeric methods.
// Dependencies/Pointers: Main-thread adapters borrow these pure rules; no Unity Object API or native pointer.
