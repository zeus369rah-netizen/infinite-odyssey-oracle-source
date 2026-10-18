using Unity.Mathematics;

namespace OdysseyOracle.Foundation.Presentation
{
    // Pure numeric CPU reference for RigidAnimation.hlsl. No clocks, allocation, UnityEngine.Object, jobs or binding here.
    public static class RigidAnimationMath
    {
        public static float3 TransformPoint(in RigidInstanceMatrix64 root, float3 local)
        {
            return root.C0.xyz * local.x + root.C1.xyz * local.y + root.C2.xyz * local.z + root.C3.xyz;
        }

        public static float3 TransformVector(in RigidInstanceMatrix64 root, float3 local)
        {
            return root.C0.xyz * local.x + root.C1.xyz * local.y + root.C2.xyz * local.z;
        }

        public static float3 TransformNormal(in RigidInstanceMatrix64 root, float3 localNormal)
        {
            float3 normal = math.cross(root.C1.xyz, root.C2.xyz) * localNormal.x
                + math.cross(root.C2.xyz, root.C0.xyz) * localNormal.y
                + math.cross(root.C0.xyz, root.C1.xyz) * localNormal.z;
            // Cofactor magnitude depends on root scale. Rescale BEFORE squaring; an absolute threshold loses valid directions.
            float maxAbs = math.cmax(math.abs(normal));
            if (!(maxAbs > 0f) || !math.isfinite(maxAbs) || !math.all(math.isfinite(normal))) return new float3(0f, 1f, 0f);
            normal /= maxAbs;
            return normal * math.rsqrt(math.dot(normal, normal));
        }

        // Compatibility/structural check ONLY: without actual mesh bounds this cannot certify deformed upload positions.
        // Every new upload boundary must use IsValidForUpload (or the explicit-bounds IsValid overload) below.
        public static bool IsValid(in RigidInstanceMatrix64 root, in AnimationGpuPacket64 packet,
            in RigidAnimationConfig32 config, RigidPartKind part)
        {
            if (!Finite(root.C0) || !Finite(root.C1) || !Finite(root.C2) || !Finite(root.C3)
                || !Finite(packet.PivotHealth) || !Finite(packet.TipSupport) || !Finite(packet.AxisProgress)
                || !Finite(packet.ClockRecoil) || !Finite(config.FormulaConfig) || !Finite(config.RecoilConfig)) return false;
            if (root.C0.w != 0f || root.C1.w != 0f || root.C2.w != 0f || root.C3.w != 1f) return false;
            float determinant = math.dot(root.C0.xyz, math.cross(root.C1.xyz, root.C2.xyz));
            if (!(determinant > 0.00000001f) || !math.isfinite(determinant)) return false;
            if ((uint)part > (uint)RigidPartKind.Tower || packet.PivotHealth.w < 0f || packet.PivotHealth.w > 1f
                || packet.AxisProgress.w < 0f || packet.AxisProgress.w > 1f) return false;
            if (config.FormulaConfig.w <= 0f || config.FormulaConfig.w > 1f
                || config.RecoilConfig.x < 0f || config.RecoilConfig.y < 0f
                || config.RecoilConfig.z <= 0f || config.RecoilConfig.w < 0f) return false;
            if (packet.ClockRecoil.z < 0f || packet.ClockRecoil.z >= 1f || packet.ClockRecoil.w < 0f) return false;
            float axisLengthSquared = math.lengthsq(packet.AxisProgress.xyz);
            if (part == RigidPartKind.Staff && (!(axisLengthSquared > 0.00000001f) || !math.isfinite(axisLengthSquared))) return false;
            if (!math.isfinite(packet.ClockRecoil.x * config.FormulaConfig.x)) return false;
            if (packet.ClockRecoil.y >= 0f && packet.ClockRecoil.z > 0f && packet.ClockRecoil.w <= 0f) return false;
            return true;
        }

        // One conservative numeric domain, not game balance or a world-size limit: origin-shift before leaving this +/-4096m window.
        // The same limit bounds authored local coordinates, root coefficients/translation, support and resulting peak render coordinates.
        public const float UploadCoordinateLimit = 4096f;
        public const int CrackCellMinimum = -8388608;
        public const int CrackCellMaximum = 8388607;

        public static bool IsValid(in RigidInstanceMatrix64 root, in AnimationGpuPacket64 packet,
            in RigidAnimationConfig32 config, RigidPartKind part, float3 meshMinimum, float3 meshMaximum)
        {
            return IsValidForUpload(root, packet, config, part, meshMinimum, meshMaximum);
        }

        // Caller supplies the exact finite local AABB covering ALL uploaded mesh vertices, not an arbitrary proxy bound.
        // Double interval arithmetic makes the proof before any potentially overflowing float deformation is evaluated.
        // Saved local normals must separately be finite unit vectors; this method validates positions/anchors/deformation.
        public static bool IsValidForUpload(in RigidInstanceMatrix64 root, in AnimationGpuPacket64 packet,
            in RigidAnimationConfig32 config, RigidPartKind part, float3 meshMinimum, float3 meshMaximum)
        {
            if (!IsValid(root, packet, config, part) || !CoordinateInDomain(meshMinimum) || !CoordinateInDomain(meshMaximum)
                || math.any(meshMinimum > meshMaximum) || !CoordinateInDomain(root.C0.xyz) || !CoordinateInDomain(root.C1.xyz)
                || !CoordinateInDomain(root.C2.xyz) || !CoordinateInDomain(root.C3.xyz)
                || !CoordinateInDomain(packet.PivotHealth.xyz) || !CoordinateInDomain(packet.TipSupport.xyz)
                || math.abs(packet.TipSupport.w) > UploadCoordinateLimit) return false;

            double3 minimum = (double3)meshMinimum;
            double3 maximum = (double3)meshMaximum;
            double3 absolute = math.max(math.abs(minimum), math.abs(maximum));
            if (part == RigidPartKind.AnchoredBody) return BoundInDomain(AffineAbsoluteBound(root, absolute));
            if (part == RigidPartKind.Tower)
            {
                double3 anchor = (double3)packet.TipSupport.xyz;
                double3 delta = math.max(math.abs(minimum - anchor), math.abs(maximum - anchor));
                // The finite pulse envelope is always [0,1]; use its maximum for the entire accepted event, even if currently idle.
                double horizontalPeak = 1d + (double)packet.ClockRecoil.z * (double)config.RecoilConfig.x;
                double3 peakLocal = math.abs(anchor) + delta * new double3(horizontalPeak, 1d, horizontalPeak);
                return BoundInDomain(peakLocal) && BoundInDomain(AffineAbsoluteBound(root, peakLocal));
            }
            double3 pivot = (double3)packet.PivotHealth.xyz;
            double3 tip = (double3)packet.TipSupport.xyz;
            double3 fromPivot = math.max(math.abs(minimum - pivot), math.abs(maximum - pivot));
            double3 fromTip = math.max(math.abs(minimum - tip), math.abs(maximum - tip));
            // A unit rotation preserves length; twice the L1 bound leaves ample float-rounding headroom for quaternion arithmetic.
            double pivotSpan = 2d * math.csum(LinearAbsoluteBound(root, fromPivot));
            double tipSpan = 2d * math.csum(LinearAbsoluteBound(root, fromTip));
            double3 unconstrained = AffineAbsoluteBound(root, math.abs(pivot)) + new double3(pivotSpan);
            double contactY = math.abs((double)packet.TipSupport.w) + tipSpan;
            // Contact is a common Y translation; its interpolation is between these two finite endpoint bounds.
            unconstrained.y = math.max(unconstrained.y, contactY);
            return BoundInDomain(unconstrained);
        }

        private static bool CoordinateInDomain(float3 value)
        {
            return math.all(math.isfinite(value)) && math.cmax(math.abs(value)) <= UploadCoordinateLimit;
        }

        private static bool BoundInDomain(double3 nonnegativeBound)
        {
            return math.all(math.isfinite(nonnegativeBound)) && math.cmax(nonnegativeBound) <= UploadCoordinateLimit;
        }

        private static double3 LinearAbsoluteBound(in RigidInstanceMatrix64 root, double3 localAbsolute)
        {
            return math.abs((double3)root.C0.xyz) * localAbsolute.x + math.abs((double3)root.C1.xyz) * localAbsolute.y
                + math.abs((double3)root.C2.xyz) * localAbsolute.z;
        }

        private static double3 AffineAbsoluteBound(in RigidInstanceMatrix64 root, double3 localAbsolute)
        {
            return LinearAbsoluteBound(root, localAbsolute) + math.abs((double3)root.C3.xyz);
        }

        // Main-thread/caller boundary only: float shader properties must represent exact integers, within both buffers and caller capacity.
        // Return false for an empty/invalid draw; GPU independently clamps dimensions and guards subtraction before index addition.
        public static bool TryValidateDrawRange(float requestedCount, float requestedBase, uint matrixCount,
            uint packetCount, uint hardCapacity, uint drawInstances, out uint count, out uint baseOffset)
        {
            count = 0u;
            baseOffset = 0u;
            const float maximumExactInteger = 16777216f;
            if (!math.isfinite(requestedCount) || !math.isfinite(requestedBase)
                || requestedCount < 1f || requestedBase < 0f
                || requestedCount > maximumExactInteger || requestedBase > maximumExactInteger
                || math.floor(requestedCount) != requestedCount || math.floor(requestedBase) != requestedBase) return false;
            uint boundedCount = (uint)requestedCount;
            uint boundedBase = (uint)requestedBase;
            uint actualCapacity = math.min(hardCapacity, math.min(matrixCount, packetCount));
            if (boundedCount > actualCapacity || boundedBase >= boundedCount || drawInstances == 0u
                || drawInstances > boundedCount - boundedBase) return false;
            count = boundedCount;
            baseOffset = boundedBase;
            return true;
        }

        public static float Smooth01(float value)
        {
            float t = math.saturate(value);
            return t * t * (3f - 2f * t);
        }

        public static float RecoilEnvelope(float eventElapsed, float duration)
        {
            if (eventElapsed < 0f || duration <= 0f || eventElapsed >= duration) return 0f;
            float t = math.saturate(eventElapsed / duration);
            float inverse = 1f - t;
            return inverse * inverse * (1f + 2f * t);
        }

        public static float3 TowerScale(in AnimationGpuPacket64 packet, in RigidAnimationConfig32 config)
        {
            float amount = packet.ClockRecoil.z * RecoilEnvelope(packet.ClockRecoil.y, packet.ClockRecoil.w);
            float horizontal = 1f + amount * config.RecoilConfig.x;
            return new float3(horizontal, 1f - amount, horizontal);
        }

        // Quaternion is xyzw. Normalized linear blending preserves a single rigid rotation, never blends vertices/bones.
        public static float4 StaffRotation(in AnimationGpuPacket64 packet, in RigidAnimationConfig32 config)
        {
            float p = math.saturate(packet.AxisProgress.w);
            float3 axis = math.normalize(packet.AxisProgress.xyz);
            float4 slam = AxisAngle(axis, config.FormulaConfig.z * Smooth01(p));
            if (p >= config.FormulaConfig.w) return slam;
            float idleAngle = config.FormulaConfig.y * math.sin(packet.ClockRecoil.x * config.FormulaConfig.x);
            float4 idle = AxisAngle(new float3(0f, 1f, 0f), idleAngle);
            if (math.dot(idle, slam) < 0f) slam = -slam;
            float weight = Smooth01(p / config.FormulaConfig.w);
            return math.normalize(idle + (slam - idle) * weight);
        }

        public static float3 EvaluatePosition(float3 local, in RigidInstanceMatrix64 root,
            in AnimationGpuPacket64 packet, in RigidAnimationConfig32 config, RigidPartKind part)
        {
            if (part == RigidPartKind.AnchoredBody) return TransformPoint(root, local);
            if (part == RigidPartKind.Tower)
            {
                float3 anchor = packet.TipSupport.xyz;
                return TransformPoint(root, anchor + (local - anchor) * TowerScale(packet, config));
            }
            float4 rotation = StaffRotation(packet, config);
            float3 position = TransformPoint(root, packet.PivotHealth.xyz)
                + Rotate(rotation, TransformVector(root, local - packet.PivotHealth.xyz));
            float p = math.saturate(packet.AxisProgress.w);
            if (p > 0f)
            {
                // Common rigid translation. At contact the exact declared tip subtracts to zero before rotation.
                float contactY = packet.TipSupport.w + Rotate(rotation, TransformVector(root, local - packet.TipSupport.xyz)).y;
                position.y = p >= 1f ? contactY : position.y + (contactY - position.y) * Smooth01(p);
            }
            return position;
        }

        public static float3 EvaluateNormal(float3 localNormal, in RigidInstanceMatrix64 root,
            in AnimationGpuPacket64 packet, in RigidAnimationConfig32 config, RigidPartKind part)
        {
            if (part == RigidPartKind.Tower) return TransformNormal(root, localNormal / TowerScale(packet, config));
            float3 normal = TransformNormal(root, localNormal);
            return part == RigidPartKind.Staff ? Rotate(StaffRotation(packet, config), normal) : normal;
        }

        // Stable rest-local crack mask, appearance only; never displaces geometry or mutates occupancy/colliders.
        public static float CrackMask(float3 restLocal, float normalizedHealth, in RigidAnimationConfig32 config)
        {
            float damage = 1f - math.saturate(normalizedHealth);
            if (damage <= 0f) return 0f;
            int3 cell = CrackCell(restLocal, config.RecoilConfig.y);
            uint hash = Hash(cell);
            float noise = (hash & 0x00ffffffu) * (1f / 16777215f);
            return math.saturate((damage - noise) * config.RecoilConfig.z) * damage;
        }

        // Identical CPU/HLSL clamp BEFORE floor/cast: every integer in this signed24-bit domain is exactly representable as float.
        // Finite local/scale inputs may multiply to +/-infinity; these saturate to the nearest endpoint cell, never wrap.
        // NaN inputs are outside the upload contract (mesh vertices and scale must already be finite).
        public static int3 CrackCell(float3 restLocal, float noiseScale)
        {
            float3 bounded = math.clamp(restLocal * noiseScale, new float3(CrackCellMinimum), new float3(CrackCellMaximum));
            return (int3)math.floor(bounded);
        }

        private static uint Hash(int3 cell)
        {
            unchecked
            {
                uint hash = (uint)cell.x * 0x8da6b343u ^ (uint)cell.y * 0xd8163841u ^ (uint)cell.z * 0xcb1ab31fu;
                hash ^= hash >> 16;
                hash *= 0x7feb352du;
                hash ^= hash >> 15;
                hash *= 0x846ca68bu;
                return hash ^ (hash >> 16);
            }
        }

        private static float4 AxisAngle(float3 normalizedAxis, float radians)
        {
            math.sincos(radians * 0.5f, out float sine, out float cosine);
            return new float4(normalizedAxis * sine, cosine);
        }

        public static float3 Rotate(float4 quaternion, float3 value)
        {
            return value + 2f * math.cross(quaternion.xyz, math.cross(quaternion.xyz, value) + quaternion.w * value);
        }

        private static bool Finite(float4 value) { return math.all(math.isfinite(value)); }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Rest-local rigid parts -> column-vector render-origin-relative world; world Y contact/axis, metres/radians.
// Array Formats: Separate Matrix64/Packet64, Config32; finite mesh AABB for upload; signed24-bit clamped crack cells; no skins/bones.
// Dependencies/Pointers: Pure Unity.Mathematics CPU reference; bounds-aware +/-4096 domain proof before upload, caller support/generation/read fences; no ownership changes.
