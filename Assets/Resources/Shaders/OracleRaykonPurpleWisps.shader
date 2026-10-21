Shader "Oracle/Presentation/RaykonPurpleWisps"
{
    Properties
    {
        _Tint("Purple wisp tint",Color)=(.52,.12,.85,.10)
        _Emitter("Raykon local brew emitter",Vector)=(.58,.53,.78,0)
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent"}
        Pass
        {
            Tags {"LightMode"="SRPDefaultUnlit"}
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off ZWrite Off ZTest LEqual
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                float4 _Emitter;
            CBUFFER_END
            struct Attributes{float3 corner:POSITION;float2 uv:TEXCOORD0;float2 phase:TEXCOORD1;};
            struct Varyings{float4 position:SV_POSITION;float2 uv:TEXCOORD0;float envelope:TEXCOORD1;};
            Varyings Vert(Attributes v)
            {
                float life=frac(_Time.y/5.5+v.phase.x);
                float angle=v.phase.y+life*2.1;
                float drift=.025+.045*life;
                float3 local=_Emitter.xyz+float3(cos(angle)*drift,.82*life,sin(angle)*drift);
                float3 center=TransformObjectToWorld(local);
                float3 right=UNITY_MATRIX_I_V._m00_m10_m20;
                float3 up=UNITY_MATRIX_I_V._m01_m11_m21;
                float3 world=center+right*(v.corner.x*(.035+.07*life))+up*(v.corner.y*(.09+.13*life));
                Varyings o;o.position=TransformWorldToHClip(world);o.uv=v.uv;
                float birthDeath=sin(3.14159265359*life);o.envelope=birthDeath*birthDeath;
                return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float2 centered=i.uv*2-1;
                float feather=saturate(1-dot(centered,centered));feather*=feather;
                return half4(_Tint.rgb,min(saturate(_Tint.a),.12)*i.envelope*feather);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Raykon-local emitter -> world center; camera right/up only orient six world billboards; all motion stays inside the declared local AABB.
// Array Formats: Immutable 24 vertex corners/UV0 and UV1(lifetime phase,drift angle), 36 indices; numeric shader motion reads existing clock, never writes global state.
// Dependencies/Pointers: Original URP Core math only; dedicated owned material alpha capped .12, no textures, lights, depth writes, skins, compute or simulation authority.
