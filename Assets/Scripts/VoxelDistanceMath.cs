using System;

namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // Scalar XZ/metre math only: no Unity Objects, allocations, jobs or native ownership.
    // A future native-array job can use these bounds rules without moving render calls off-thread.
    /// <summary>One block is one Unity metre. Shared by runtime selection and standalone checks.</summary>
    public static class VoxelDistanceMath
    {
        public const int MinimumBlocks = 32, MaximumBlocks = 128, DefaultBlocks = 96;
        public const float BlockMetres = 1f, ChunkMetres = 10f, HysteresisMetres = 2f;
        public const float DefaultFarDetail = .35f;

        public static int ClampBlocks(int value) => Math.Max(MinimumBlocks, Math.Min(MaximumBlocks, value));
        public static float ClampDetail(float value) => float.IsNaN(value) || float.IsInfinity(value)
            ? DefaultFarDetail : Math.Max(0f, Math.Min(1f, value));
        public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        public static float DistanceSquaredToBoundsXZ(float x, float z, float minX, float minZ, float maxX, float maxZ)
        {
            if (!Finite(x) || !Finite(z) || !Finite(minX) || !Finite(minZ) || !Finite(maxX) || !Finite(maxZ) || minX > maxX || minZ > maxZ)
                return float.PositiveInfinity;
            double dx = x < minX ? (double)minX - x : x > maxX ? (double)x - maxX : 0d;
            double dz = z < minZ ? (double)minZ - z : z > maxZ ? (double)z - maxZ : 0d;
            return (float)(dx * dx + dz * dz);
        }

        public static bool ShouldHide(float boundsDistanceSquared, int blocks, bool wasHidden)
            => ShouldHide(boundsDistanceSquared, ClampBlocks(blocks) * BlockMetres, wasHidden);

        public static bool ShouldHide(float boundsDistanceSquared, float reachMetres, bool wasHidden)
        {
            // Reappear two metres inside the cutoff; no centre-point approximation.
            if (!Finite(reachMetres) || reachMetres <= 0f) return true;
            float cutoff = Math.Max(0f, reachMetres - (wasHidden ? HysteresisMetres : 0f));
            return !Finite(boundsDistanceSquared) || boundsDistanceSquared > cutoff * cutoff;
        }

        public static float EffectiveReach(int blocks, float cameraCeilingMetres)
        {
            float requested = ClampBlocks(blocks) * BlockMetres;
            return Finite(cameraCeilingMetres) && cameraCeilingMetres > 0f ? Math.Min(requested, cameraCeilingMetres) : requested;
        }

        public static float FogStart(int blocks) => ClampBlocks(blocks) * BlockMetres * .68f;
        public static float FogEnd(int blocks) => ClampBlocks(blocks) * BlockMetres * .96f;
        public static float DetailStart(int blocks) => ClampBlocks(blocks) * BlockMetres * .40f;
        public static float DetailEnd(int blocks) => ClampBlocks(blocks) * BlockMetres * .90f;

        public static float DetailWeight(float distance, int blocks)
        {
            if (!Finite(distance)) return 0f;
            float t = Math.Max(0f, Math.Min(1f, (distance - DetailStart(blocks)) / (DetailEnd(blocks) - DetailStart(blocks))));
            return t * t * (3f - 2f * t);
        }
    }
}
