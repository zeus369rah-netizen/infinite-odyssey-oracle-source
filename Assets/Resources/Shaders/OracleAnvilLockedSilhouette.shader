Shader "Oracle/AnvilLockedSilhouette"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "LockedTowerSilhouette"
            Tags { "LightMode"="UniversalForward" }
            Cull Back ZWrite On ZTest LEqual
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float3 positionOS:POSITION; };
            struct Varyings { float4 positionCS:SV_POSITION; };
            Varyings Vert(Attributes input){Varyings output;output.positionCS=TransformObjectToHClip(input.positionOS);return output;}
            half4 Frag(Varyings input):SV_Target{return half4(0.008,0.012,0.014,1.0);}
            ENDHLSL
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: unchanged borrowed saved model vertices under the private tile transform -> URP clipspace; one opaque near-black silhouette material shared by all future/waiting tiers.
// Array Formats: position-only standard mesh attributes and fixed fragment color, no texture/buffer/animation/lighting/globalkeyword or per-pixel allocation.
// Dependencies/Pointers: render-only lock styling; source state never changes accepted Forge tier or borrowed saved material. Root native ShaderUtil/WebGL/WebGPU/render acceptance pending.
