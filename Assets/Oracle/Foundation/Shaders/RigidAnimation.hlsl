#ifndef ORACLE_RIGID_ANIMATION_INCLUDED
#define ORACLE_RIGID_ANIMATION_INCLUDED

// These field names/order are the exact C# structured-buffer ABI. Each structure is 64 bytes.
struct OracleRigidMatrix64 { float4 C0; float4 C1; float4 C2; float4 C3; };
struct OracleAnimationPacket64 { float4 PivotHealth; float4 TipSupport; float4 AxisProgress; float4 ClockRecoil; };

float3 OracleRigidPoint(OracleRigidMatrix64 root, float3 local)
{
    return root.C0.xyz * local.x + root.C1.xyz * local.y + root.C2.xyz * local.z + root.C3.xyz;
}
float3 OracleRigidVector(OracleRigidMatrix64 root, float3 local)
{
    return root.C0.xyz * local.x + root.C1.xyz * local.y + root.C2.xyz * local.z;
}
float3 OracleRigidNormal(OracleRigidMatrix64 root, float3 localNormal)
{
    float3 normal = cross(root.C1.xyz, root.C2.xyz) * localNormal.x
        + cross(root.C2.xyz, root.C0.xyz) * localNormal.y
        + cross(root.C0.xyz, root.C1.xyz) * localNormal.z;
    // Rescale before squaring so a valid tiny cofactor keeps its direction, identically to the CPU reference.
    float maxAbs = max(abs(normal.x), max(abs(normal.y), abs(normal.z)));
    if (!(maxAbs > 0.0) || !isfinite(maxAbs) || !all(isfinite(normal))) return float3(0,1,0);
    normal /= maxAbs;
    return normal * rsqrt(dot(normal, normal));
}
float OracleSmooth01(float value)
{
    float t = saturate(value);
    return t * t * (3.0 - 2.0 * t);
}
float OracleRecoilEnvelope(float elapsed, float duration)
{
    if (elapsed < 0.0 || duration <= 0.0 || elapsed >= duration) return 0.0;
    float t = saturate(elapsed / duration);
    float inverse = 1.0 - t;
    return inverse * inverse * (1.0 + 2.0 * t);
}
float3 OracleTowerScale(OracleAnimationPacket64 packet, float4 recoilConfig)
{
    float amount = packet.ClockRecoil.z * OracleRecoilEnvelope(packet.ClockRecoil.y, packet.ClockRecoil.w);
    float horizontal = 1.0 + amount * recoilConfig.x;
    return float3(horizontal, 1.0 - amount, horizontal);
}
float4 OracleAxisAngle(float3 normalizedAxis, float radians)
{
    float sine, cosine;
    sincos(radians * 0.5, sine, cosine);
    return float4(normalizedAxis * sine, cosine);
}
float3 OracleRotate(float4 quaternion, float3 value)
{
    return value + 2.0 * cross(quaternion.xyz, cross(quaternion.xyz, value) + quaternion.w * value);
}
float4 OracleStaffRotation(OracleAnimationPacket64 packet, float4 formulaConfig)
{
    float p = saturate(packet.AxisProgress.w);
    float4 slam = OracleAxisAngle(normalize(packet.AxisProgress.xyz), formulaConfig.z * OracleSmooth01(p));
    if (p >= formulaConfig.w) return slam;
    float idleAngle = formulaConfig.y * sin(packet.ClockRecoil.x * formulaConfig.x);
    float4 idle = OracleAxisAngle(float3(0,1,0), idleAngle);
    if (dot(idle, slam) < 0.0) slam = -slam;
    float weight = OracleSmooth01(p / formulaConfig.w);
    return normalize(idle + (slam - idle) * weight);
}
float3 OracleRigidPosition(float3 local, OracleRigidMatrix64 root, OracleAnimationPacket64 packet,
    float4 formulaConfig, float4 recoilConfig, uint partKind)
{
    if (partKind == 0u) return OracleRigidPoint(root, local);
    if (partKind == 2u)
    {
        float3 anchor = packet.TipSupport.xyz;
        return OracleRigidPoint(root, anchor + (local - anchor) * OracleTowerScale(packet, recoilConfig));
    }
    float4 rotation = OracleStaffRotation(packet, formulaConfig);
    float3 position = OracleRigidPoint(root, packet.PivotHealth.xyz)
        + OracleRotate(rotation, OracleRigidVector(root, local - packet.PivotHealth.xyz));
    float p = saturate(packet.AxisProgress.w);
    if (p > 0.0)
    {
        // Direct contact assignment, not a sine/Y offset or a lerp rounded through the root height.
        float contactY = packet.TipSupport.w + OracleRotate(rotation, OracleRigidVector(root, local - packet.TipSupport.xyz)).y;
        position.y = p >= 1.0 ? contactY : position.y + (contactY - position.y) * OracleSmooth01(p);
    }
    return position;
}
float3 OracleRigidVertexNormal(float3 localNormal, OracleRigidMatrix64 root, OracleAnimationPacket64 packet,
    float4 formulaConfig, float4 recoilConfig, uint partKind)
{
    if (partKind == 2u) return OracleRigidNormal(root, localNormal / OracleTowerScale(packet, recoilConfig));
    float3 normal = OracleRigidNormal(root, localNormal);
    return partKind == 1u ? OracleRotate(OracleStaffRotation(packet, formulaConfig), normal) : normal;
}
uint OracleCrackHash(int3 cell)
{
    uint hash = asuint(cell.x) * 0x8da6b343u ^ asuint(cell.y) * 0xd8163841u ^ asuint(cell.z) * 0xcb1ab31fu;
    hash ^= hash >> 16;
    hash *= 0x7feb352du;
    hash ^= hash >> 15;
    hash *= 0x846ca68bu;
    return hash ^ (hash >> 16);
}
// Signed24-bit exact-float endpoints, identical to C# CrackCellMinimum/Maximum.
// Clamp BEFORE floor/int cast; finite inputs whose product overflows saturate to an endpoint cell, never wrap.
// NaN vertices/scale violate the CPU bounds-aware upload contract.
int3 OracleCrackCell(float3 restLocal, float noiseScale)
{
    float3 bounded = clamp(restLocal * noiseScale, -8388608.0, 8388607.0);
    return (int3)floor(bounded);
}
float OracleCrackMask(float3 restLocal, float normalizedHealth, float4 recoilConfig)
{
    float damage = 1.0 - saturate(normalizedHealth);
    if (damage <= 0.0) return 0.0;
    int3 cell = OracleCrackCell(restLocal, recoilConfig.y);
    float noise = (OracleCrackHash(cell) & 0x00ffffffu) * (1.0 / 16777215.0);
    return saturate((damage - noise) * recoilConfig.z) * damage;
}
#endif
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Rest-local rigid parts -> explicit column-vector render-origin-relative world; Y-up support, metres/radians.
// Array Formats: Matrix64 + Packet64, four float4 each; signed24-bit clamped crack cells; part0 body/1 staff/2 tower; no skin/bone data.
// Dependencies/Pointers: Pure HLSL; caller uses finite mesh-bounds-aware +/-4096 upload proof, positive affine basis, support/generation/GPU read fences.
