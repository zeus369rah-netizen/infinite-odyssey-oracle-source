Shader "OdysseyOracle/MenuHubMotesPreview"
{
    Properties { _PreviewClock("Private preview clock",Float)=0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Name "PreviewHubMotes"
            Tags { "LightMode"="UniversalForwardOnly" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float _PreviewClock;
            CBUFFER_END
            // All four vertices share one emitter origin. UV0.xy is signed corner,
            // UV0.zw is phase/size; UV1 is radius/rise/orbit phase/lower offset.
            struct Attributes { float3 positionOS:POSITION;float4 uv:TEXCOORD0;float4 motion:TEXCOORD1;float4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION;half4 color:COLOR;float2 uv:TEXCOORD0; };
            Varyings Vertex(Attributes input)
            {
                float t=max(0,_PreviewClock),age=frac(t*.25+input.uv.z);
                float fade=smoothstep(0,.12,age)*(1-smoothstep(.62,.96,age));
                float angle=input.motion.z+t*(6.28318530718/32.0);
                float3 center=input.positionOS+float3(cos(angle)*input.motion.x,input.motion.w+age*input.motion.y,sin(angle)*input.motion.x);
                float3 centerWS=TransformObjectToWorld(center);
                float3 right=UNITY_MATRIX_I_V._m00_m10_m20,up=UNITY_MATRIX_I_V._m01_m11_m21;
                float3 positionWS=centerWS+(right*input.uv.x+up*input.uv.y)*(input.uv.w*fade);
                Varyings output;output.positionCS=TransformWorldToHClip(positionWS);output.uv=input.uv.xy;output.color=half4(input.color.rgb,fade);return output;
            }
            half4 Fragment(Varyings input):SV_Target
            {
                half edge=1-smoothstep(.18,.5,max(abs(input.uv.x),abs(input.uv.y)));
                return half4(input.color.rgb*.72h,input.color.a*edge*.72h);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: fixed arena-local emitter origin transformed into isolated stage; camera-right/up billboard offsets only after world transform.
// Array Formats:120 quads/480vertices/720indices; POSITION,UV0 phase+size,UV1 radius/rise/orbit/lower offset,COLOR; one private32s clock.
// Dependencies/Pointers: saved borrowed mesh/material and cached MPB only;4s ages and32s orbit close exactly, size+alpha reachzero byage.96 before wrap. No global Time, lights, particles, collision or gameplay pointers.
