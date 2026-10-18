using System;
using System.Runtime.InteropServices;

namespace OdysseyOracle.VisualDressing
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct DressingPlacement
    {
        public float X, Y, Z, YawRadians, RotationY, RotationW, UniformScale;
        public uint Valid;
    }

    // Pure scalar cold placement math. World supplies an accepted support point;
    // this class neither searches for support nor changes occupancy/colliders.
    public static class DressingPlacementKernel
    {
        public const float Pi = 3.14159265358979323846f;
        public static bool TryPlace(uint acceptedSupport, float supportX, float supportY, float supportZ,
            float desiredFrontX, float desiredFrontZ, float authoredFrontX, float authoredFrontZ,
            float uniformScale, float pivotToBaseX, float pivotToBaseY, float pivotToBaseZ,
            out DressingPlacement placement)
        {
            placement = default;
            if (acceptedSupport != 1u || !Finite(supportX) || !Finite(supportY) || !Finite(supportZ) ||
                !Finite(desiredFrontX) || !Finite(desiredFrontZ) || !Finite(authoredFrontX) || !Finite(authoredFrontZ) ||
                !Finite(uniformScale) || uniformScale <= 0f || uniformScale > 8f ||
                !Finite(pivotToBaseX) || !Finite(pivotToBaseY) || !Finite(pivotToBaseZ)) return false;
            double desiredLength = (double)desiredFrontX * desiredFrontX + (double)desiredFrontZ * desiredFrontZ;
            double authoredLength = (double)authoredFrontX * authoredFrontX + (double)authoredFrontZ * authoredFrontZ;
            if (desiredLength < 1e-12 || authoredLength < 1e-12) return false;
            double yaw = Math.Atan2(desiredFrontX, desiredFrontZ) - Math.Atan2(authoredFrontX, authoredFrontZ);
            while (yaw > Math.PI) yaw -= 2d * Math.PI;
            while (yaw < -Math.PI) yaw += 2d * Math.PI;
            double sine = Math.Sin(yaw), cosine = Math.Cos(yaw);
            double x = supportX - uniformScale * (cosine * pivotToBaseX + sine * pivotToBaseZ);
            double y = supportY - uniformScale * pivotToBaseY;
            double z = supportZ - uniformScale * (-sine * pivotToBaseX + cosine * pivotToBaseZ);
            if (Math.Abs(x) > 1000000d || Math.Abs(y) > 1000000d || Math.Abs(z) > 1000000d) return false;
            placement = new DressingPlacement
            {
                X = (float)x, Y = (float)y, Z = (float)z, YawRadians = (float)yaw,
                RotationY = (float)Math.Sin(yaw * .5d), RotationW = (float)Math.Cos(yaw * .5d),
                UniformScale = uniformScale, Valid = 1u
            };
            return true;
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: accepted World support metres, Y-up, planar XZ, yaw radians; explicit authored front (+Z or -Z).
// Array Formats:32B scalar DressingPlacement; no container/array ownership; uniform scale applied once.
// Dependencies/Pointers: System math only; no UnityObject/Physics/native pointer/support query or runtime registration.
