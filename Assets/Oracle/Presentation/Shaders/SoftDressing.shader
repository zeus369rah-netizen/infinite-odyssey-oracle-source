Shader "Oracle/Presentation/SoftDressingCandidate"
{
    Properties
    {
        _UseRoomReactions("Fixed candle state enabled",Float)=0
        _GrassTint("Grass tint",Color)=(.45,.7,.3,1)
        _Wind("Wind XZ, strength, speed",Vector)=(1,.35,.075,1.4)
        [HDR] _FlameBase("Candle hot base",Color)=(2.8,1.6,.35,1)
        [HDR] _FlameTip("Candle orange tip",Color)=(1.5,.22,.015,1)
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
        Pass
        {
            Tags {"LightMode"="UniversalForward"}
            Cull Back ZWrite On ZTest LEqual
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "WorldWind015.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _GrassTint,_FlameBase,_FlameTip;
                float4 _Wind;float _UseRoomReactions;float4 _RoomReactionFlameGroups[64];
            CBUFFER_END
            float4 _OracleLaneWeather; // Existing directionXZ/strength/motion global.
            struct Attributes {float3 positionOS:POSITION;float3 normalOS:NORMAL;half4 color:COLOR;float4 data:TEXCOORD0;float4 pivot:TEXCOORD1;};
            struct Varyings {float4 positionCS:SV_POSITION;float3 normalWS:TEXCOORD0;half4 color:COLOR;float4 data:TEXCOORD1;half fog:TEXCOORD2;half fuel:TEXCOORD3;};
            Varyings Vert(Attributes v)
            {
                Varyings o;o.fuel=1;
                float3 p=v.positionOS,normal=v.normalOS;
                float weight=saturate(v.data.x),phase=v.data.y;
                if(v.data.z<.5)
                {
                    float2 worldDirection;float speed,time,gain;WindField015(worldDirection,speed,time,gain);
                    float2 direction=mul((float3x3)GetWorldToObjectMatrix(),float3(worldDirection.x,0,worldDirection.y)).xz;
                    float2 offset,slope;
                    WindSample015(weight,phase,time,direction,
                        gain*saturate(speed),clamp(_Wind.z,0,.12),1,offset,slope);
                    p.xz+=offset;
                    normal=WindNormal015(normal,slope/max(v.data.w,.05));
                }
                else
                {
                    float t=WindFlickerTime015();float3 local=p-v.pivot.xyz;
                    local.y*=1+.08*sin(t*10+phase)+.03*sin(t*17+phase*1.7);
                    local.xz*=.94+.06*sin(t*8+phase);
                    local.xz+=float2(sin(t*6+phase),cos(t*7+phase))*.02*weight*weight;
                    float2 worldDirection;float speed,time,gain;WindField015(worldDirection,speed,time,gain);
                    float2 direction=mul((float3x3)GetWorldToObjectMatrix(),float3(worldDirection.x,0,worldDirection.y)).xz;
                    float2 offset,slope;WindSample015(weight,phase,time,direction,gain*saturate(speed),.025,1,offset,slope);local.xz+=offset;
                    int index=clamp((int)v.pivot.w,0,255);o.fuel=_UseRoomReactions>.5?saturate(_RoomReactionFlameGroups[index>>2][index&3]):1;
                    p=v.pivot.xyz+local*o.fuel;
                }
                o.positionCS=TransformObjectToHClip(p);o.normalWS=TransformObjectToWorldNormal(normal);
                o.color=v.color;o.data=v.data;o.fog=ComputeFogFactor(o.positionCS.z);return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                if(i.data.z>.5)clip(i.fuel-.001);
                half3 color;
                if(i.data.z<.5)
                {
                    half3 n=normalize(i.normalWS);Light sun=GetMainLight();
                    half3 light=max(SampleSH(n),half3(.08,.08,.08))+sun.color*saturate(dot(n,sun.direction));
                    color=i.color.rgb*_GrassTint.rgb*lerp(.55,1.15,saturate(i.data.x))*light;
                }
                else
                {
                    float t=WindFlickerTime015();half flicker=.91+.06*sin(t*11+i.data.y)+.03*sin(t*19+i.data.y*1.9);
                    color=lerp(_FlameBase.rgb,_FlameTip.rgb,saturate(i.data.x))*flicker;
                }
                return half4(MixFog(color,i.fog),1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: geometry-root local metres/Y-up; UV1 is wick/root pivot; grass base remains fixed at accepted support.
// Array Formats: baked POSITION/NORMAL/COLOR, UV0(height01,phase,mode,height), UV1(pivot); fixed clamp bounds.
// Dependencies/Pointers: URP vertex math only, shader3.0/no compute/StructuredBuffer/CPU Update; opaque depth avoids layered coplanar flames.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing refuge geometry/world axes and support pivots unchanged; elemental numeric state applies only to the supported refuge.
// Array Formats: Existing owner arrays remain canonical; fixed512-node/8word reaction state supplies bounded scalar presentation gains.
// Dependencies/Pointers: Host accepted single Fire/Earth/Wind shots only; existing Object adapters retain materials/lights/lifetime, no rewards/saves or shrine/portal gameplay mutation.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing supported Y-up refuge local/unit-yaw and arena-world/chunk anchor contract; no root/floor/actor/pivot change.
// Array Formats: Existing immutable vertex/normal/UV streams and32B grass seeds; shared bounded shader wind,16original3x trees, unchanged counts/resources.
// Dependencies/Pointers: Existing World owns cold materials/meshes; LaneVignetteRenderer publishes weather policy. No new Update/CPU spatial loop/compute dependency; root native/GPU/browser acceptance pending.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: existing anchored presentation receives the same bounded passive world-XZ field; geometry/physics/accepted room fuel stay canonical.
// Array Formats: existing fixed mesh/vertex/material owner and one shared float4; no hot collections, per-window objects or mutable gameplay alias.
// Dependencies/Pointers: work-only shared-breeze adaptation; original shaders and fallback lifetimes remain. Root alone imports/tests actual native/WebGL visuals, culling, allocation and lifecycle.
