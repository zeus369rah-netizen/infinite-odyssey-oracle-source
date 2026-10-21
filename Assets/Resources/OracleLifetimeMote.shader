Shader "Oracle/Presentation/LifetimeMote"
{
    Properties { [HDR] _Tint("Living mote tint", Color)=(.16,1,.02,1) _MoteFade("Lifetime fade",Range(0,1))=1 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha One
            ZWrite Off ZTest LEqual Cull Back
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _MoteFade;
            CBUFFER_END
            struct Attributes { float3 positionOS:POSITION; };
            struct Varyings { float4 positionCS:SV_POSITION; };
            Varyings Vert(Attributes input) { Varyings output; output.positionCS=TransformObjectToHClip(input.positionOS); return output; }
            half4 Frag(Varyings input):SV_Target { return half4(max(_Tint.rgb,0),saturate(_Tint.a)*saturate(_MoteFade)); }
            ENDHLSL
        }
    }
    FallBack Off
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: existing shrine-cube object vertices and unchanged transform; no shader movement or bounds expansion.
// Array Formats: one shared material tint and cached per-renderer float fade; alpha reaches0 from caller envelope, no texture or compute allocation.
// Dependencies/Pointers: Resources inclusion, URP Core17.6 shader3.0; no shared rune/health-bar material mutation, HP/collision or light writes.
