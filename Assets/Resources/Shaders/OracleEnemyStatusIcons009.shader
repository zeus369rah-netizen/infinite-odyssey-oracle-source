Shader "OdysseyOracle/EnemyStatusIcons009"
{
    Properties { _Tint("Original glyph tint",Color)=(1,1,1,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+40" }
        Pass
        {
            Name "OriginalLiveStatus"
            Tags { "LightMode"="UniversalForwardOnly" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _Tint;
            CBUFFER_END
            struct Attributes {float3 positionOS:POSITION;float4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct Varyings {float4 positionCS:SV_POSITION;half4 color:COLOR;};
            Varyings Vertex(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;output.positionCS=TransformObjectToHClip(input.positionOS);
                output.color=half4(input.color.rgb*_Tint.rgb,input.color.a*_Tint.a);return output;
            }
            half4 Fragment(Varyings input):SV_Target {return input.color;}
            ENDHLSL
        }
    }
    FallBack Off
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Original glyph-local plane transformed by completed per-camera CPU job matrices; standard world depth test.
// Array Formats: Shared original mesh vertex POSITION/COLOR and instanced matrices, one immutable tint per9kind batches; no textures/compute buffers.
// Dependencies/Pointers: URP Core instancing macros, target3.5; no lights/material cloning/runtime geometry mutation; actual WebGPU/WebGL shader/instancing acceptance root-owned.
