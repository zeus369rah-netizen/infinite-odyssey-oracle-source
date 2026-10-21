using System;

namespace OdysseyOracle
{
    public struct LanternPendulumState { public float X, Z, VX, VZ; }
    public static class LanternSwingMath
    {
        public const int Capacity = 8;
        public const float MaximumAngle = .20943951f, MaximumSpeed = 2.4f;
        public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public static bool Kick(ref LanternPendulumState state, float directionX, float directionZ, float strength)
        {
            if (!Finite(directionX) || !Finite(directionZ) || !Finite(strength) || strength <= 0f) return false;
            float squared = directionX * directionX + directionZ * directionZ;
            if (!Finite(squared) || squared < 1e-8f) return false;
            float gain = 1.6f * Math.Min(1.8f, Math.Max(.15f, strength)) / (float)Math.Sqrt(squared);
            state.VX -= directionZ * gain; state.VZ += directionX * gain;
            ClampVelocity(ref state); return true;
        }
        public static bool Step(ref LanternPendulumState state, float delta)
        {
            if (!Finite(state.X) || !Finite(state.Z) || !Finite(state.VX) || !Finite(state.VZ)) { state = default; return false; }
            if (!Finite(delta) || delta <= 0f) return state.X != 0f || state.Z != 0f || state.VX != 0f || state.VZ != 0f;
            float dt = Math.Min(.25f, delta), alpha = 2.184f, beta = 4.719719f;
            float decay = (float)Math.Exp(-alpha * dt), sine = (float)Math.Sin(beta * dt), cosine = (float)Math.Cos(beta * dt);
            Axis(ref state.X, ref state.VX, alpha, beta, decay, sine, cosine);
            Axis(ref state.Z, ref state.VZ, alpha, beta, decay, sine, cosine);
            float angle2 = state.X * state.X + state.Z * state.Z;
            if (angle2 > MaximumAngle * MaximumAngle)
            {
                float inverse = 1f / (float)Math.Sqrt(angle2), nx = state.X * inverse, nz = state.Z * inverse;
                state.X = nx * MaximumAngle; state.Z = nz * MaximumAngle;
                float outward = state.VX * nx + state.VZ * nz;
                if (outward > 0f) { state.VX -= nx * outward; state.VZ -= nz * outward; }
            }
            ClampVelocity(ref state);
            if (state.X * state.X + state.Z * state.Z < 1e-7f && state.VX * state.VX + state.VZ * state.VZ < 1e-6f)
            { state = default; return false; }
            return true;
        }
        static void Axis(ref float angle, ref float velocity, float a, float b, float decay, float sine, float cosine)
        {
            float first = angle, second = (velocity + a * angle) / b;
            angle = decay * (first * cosine + second * sine);
            velocity = decay * ((-a * first + b * second) * cosine + (-a * second - b * first) * sine);
        }
        static void ClampVelocity(ref LanternPendulumState state)
        {
            float speed2 = state.VX * state.VX + state.VZ * state.VZ;
            if (speed2 > MaximumSpeed * MaximumSpeed)
            { float scale = MaximumSpeed / (float)Math.Sqrt(speed2); state.VX *= scale; state.VZ *= scale; }
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: two local hinge angles/radial angular velocities; immutable hanging pivot is external.
// Array Formats: four-float blittable state; pure scalar analytic damping/kick/clamps, no Unity objects/heap ownership.
// Dependencies/Pointers: Burst job caller supplies accepted delta and existing accepted-shot direction; no gameplay permission, HP/reward or networking authority.
