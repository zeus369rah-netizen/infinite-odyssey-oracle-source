using System;

namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // Pure scalar cache, metres, main-thread owner. A failed or saturated new
    // query invalidates old support; no Unity/native resources or allocations.
    internal struct GroundedFloorSample
    {
        internal const float MaximumHeightDelta = .75f;
        internal const float MaximumPlanarReuse = .75f;
        internal bool Known { get; private set; }
        internal float Height { get; private set; }
        float nearest, sampleX, sampleZ;

        internal void Begin(float x, float z)
        {
            Known = false;
            nearest = float.PositiveInfinity;
            sampleX = x; sampleZ = z;
        }

        internal bool Consider(float distance, float height, float normalY, float rootY)
        {
            if (!Finite(distance) || !Finite(height) || !Finite(normalY) || !Finite(rootY) ||
                distance < 0f || distance >= nearest || normalY < .75f ||
                Math.Abs(height - rootY) > MaximumHeightDelta) return false;
            nearest = distance; Height = height; Known = true;
            return true;
        }

        internal bool IsUsable(float x, float y, float z)
        {
            if (!Known || !Finite(x) || !Finite(y) || !Finite(z) ||
                Math.Abs(y - Height) > MaximumHeightDelta) return false;
            float dx = x - sampleX, dz = z - sampleZ;
            return dx * dx + dz * dz <= MaximumPlanarReuse * MaximumPlanarReuse;
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
