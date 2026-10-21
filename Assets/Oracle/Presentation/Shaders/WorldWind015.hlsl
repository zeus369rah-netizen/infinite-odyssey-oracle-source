#ifndef WORLD_WIND_015_INCLUDED
#define WORLD_WIND_015_INCLUDED

float4 _OraclePassiveWind015; // world XZ velocity, held unscaled seconds, common gust gain.
void WindField015(out float2 direction,out float speed,out float time,out float gain)
{
    speed=length(_OraclePassiveWind015.xy);
    direction=speed>1e-5?_OraclePassiveWind015.xy/speed:float2(-1,0);
    time=_OraclePassiveWind015.z;gain=saturate(_OraclePassiveWind015.w);
}
// Forty integral cycles over the256s field period retain a near-one intrinsic candle clock rate and a continuous wrap.
float WindFlickerTime015(){return _OraclePassiveWind015.z*(6.28318530718*40.0/256.0);}
// Scalar vertex math. Shared state/clock/lifetime are supplied by the one existing World owner.
void WindSample015(float weight,float phase,float time,float2 direction,
    float strength,float maximumOffset,float motion,out float2 offset,out float2 slope)
{
    float lengthSquared=dot(direction,direction);
    direction=lengthSquared>1e-6?direction*rsqrt(lengthSquared):float2(1,0);
    float q=saturate(weight),enabled=saturate(motion);
    float t=time*enabled;
    float theta=t*(6.28318530718/256.0);
    float wave=.72+.20*sin(23*theta+phase)+.08*sin(11*theta+phase*1.7);
    float amount=wave*saturate(strength)*enabled*clamp(maximumOffset,0,.72);
    offset=direction*(amount*q*q);
    slope=direction*(2*amount*q); // dOffset/dWeight; caller divides by height span.
}

float3 WindNormal015(float3 normal,float2 slopePerMetre)
{
    // Inverse-transpose of I+(dx/dy,0,dz/dy) outer-product up.
    float3 result=float3(normal.x,normal.y-dot(normal.xz,slopePerMetre),normal.z);
    return result*rsqrt(max(dot(result,result),1e-12));
}

#endif

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Caller-supplied XZ direction and anchored height weight; zero vertical/root displacement. Refuge uses local unit/yaw space, arena existing world-aligned chunk contract.
// Array Formats: Register-only float/float2/float3 parameters; one shared global float4 supplied outside this include. No buffer/compute/texture/native ABI.
// Dependencies/Pointers: Existing shader3.0 vertex paths and main-thread weather settings owner supply time/state; maximum offset.72, grass callers cap.12. Root verifies actual WebGPU/WebGL2 compile/normal/culling visuals.


// # ARCHITECTURE SNAPSHOT
// Coordinate Space: existing anchored presentation receives the same bounded passive world-XZ field; geometry/physics/accepted room fuel stay canonical.
// Array Formats: existing fixed mesh/vertex/material owner and one shared float4; no hot collections, per-window objects or mutable gameplay alias.
// Dependencies/Pointers: work-only shared-breeze adaptation; original shaders and fallback lifetimes remain. Root alone imports/tests actual native/WebGL visuals, culling, allocation and lifecycle.
