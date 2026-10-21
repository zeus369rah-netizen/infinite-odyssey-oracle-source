using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace OdysseyOracle
{
    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 24)]
    public struct OracleViewMotion020
    {
        public float3 Position;
        public float3 Angles;
    }

    // Pure presentation math. No UnityEngine.Object, gameplay state or resource owner.
    public static class OracleViewmodelGrip020
    {
        public const float GripRadians = 1.0471975512f;
        const float Epsilon = 0.000001f;

        public static bool TryRotation(float3 shaft, float3 cameraRight,
            float3 sourceAxis, float3 sourceRadial, float angle, out quaternion rotation)
        {
            rotation = quaternion.identity;
            if (!math.all(math.isfinite(shaft)) || !math.all(math.isfinite(cameraRight)) ||
                !math.all(math.isfinite(sourceAxis)) || !math.all(math.isfinite(sourceRadial)) ||
                !math.isfinite(angle) || math.lengthsq(shaft) < Epsilon || math.lengthsq(sourceAxis) < Epsilon)
                return false;
            float3 up = math.normalize(shaft);
            float3 right = cameraRight - up * math.dot(cameraRight, up);
            float3 sourceUp = math.normalize(sourceAxis);
            float3 sourceRight = sourceRadial - sourceUp * math.dot(sourceRadial, sourceUp);
            if (math.lengthsq(right) < Epsilon || math.lengthsq(sourceRight) < Epsilon)
                return false;
            right = math.normalize(right);
            sourceRight = math.normalize(sourceRight);
            float3 forward = math.normalize(math.cross(right, up));
            float3 radial = right * math.cos(angle) - forward * math.sin(angle);
            float3 outward = math.normalize(math.cross(radial, up));
            var source = new float3x3(sourceRight, sourceUp, math.cross(sourceRight, sourceUp));
            var target = new float3x3(radial, up, outward);
            rotation = math.normalize(math.mul(new quaternion(target), math.inverse(new quaternion(source))));
            return math.all(math.isfinite(rotation.value));
        }

        public static OracleViewMotion020 Walk(float phase, float time, float movement, float strength, bool grounded)
        {
            if (!grounded || !math.isfinite(phase) || !math.isfinite(time) ||
                !math.isfinite(movement) || !math.isfinite(strength))
                return default;
            movement = math.saturate(movement);
            strength = math.saturate(strength);
            float side = math.sin(phase * 0.5f), step = math.sin(phase);
            float breath = math.sin(time * 1.6f) * 0.001f * (1f - movement);
            return new OracleViewMotion020
            {
                Position = new float3(side * 0.008f * movement,
                    (1f - math.cos(phase)) * 0.003f * movement + breath,
                    step * 0.002f * movement) * strength,
                Angles = new float3(-math.cos(phase) * 0.35f,
                    side * 0.35f, -side * 0.55f) * (movement * strength)
            };
        }

        // X/Y retain the caller's existing yaw/pitch target convention.
        public static float3 Turn(float yawDelta, float pitchDelta, float delta)
        {
            if (!math.isfinite(yawDelta) || !math.isfinite(pitchDelta) ||
                !math.isfinite(delta) || delta <= 0f)
                return default;
            return new float3(math.clamp(-yawDelta / delta * 0.003f, -1.7f, 1.7f),
                math.clamp(pitchDelta / delta * 0.0027f, -1.5f, 1.5f), 0f);
        }

        public static float3 Attack(float pitchDegrees, float yawDegrees)
        {
            if (!math.isfinite(pitchDegrees) || !math.isfinite(yawDegrees))
                return default;
            float pitch = math.clamp(pitchDegrees, -4.5f, 4.5f);
            float yaw = math.clamp(yawDegrees, -1.5f, 1.5f);
            return new float3(-yaw * 0.0013f, -pitch * 0.0007f, pitch * 0.002f);
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Proper authored-hand to staff-local basis; radians for grip, local metres and degrees for existing cosmetic Transform adapter; camera/aim independent.
// Array Formats: One24B motion value and bounded scalar/vector math only; no reference fields, arrays, allocations, native resources or mutable static storage.
// Dependencies/Pointers: Unity.Mathematics only; existing viewmodel owns cold geometry and Transform writes; accepted attack spring, movement phase, clearance and native profiles retain their owners.
