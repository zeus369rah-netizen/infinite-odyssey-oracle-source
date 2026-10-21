Shader "OdysseyOracle/RefugeReactionFlower"
{
    Properties { _Growth("Host bloom growth",Range(0,1))=0 _PreviewClock("Private clock",Float)=0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "OriginalEarthBloom"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float _Growth,_PreviewClock;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION;half4 color:COLOR;float2 data:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION;half4 color:COLOR;float kind:TEXCOORD0;float growth:TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings output;float3 p=input.positionOS.xyz;float kind=input.data.x;
                float bloom=smoothstep(.34,1,_Growth);
                if(kind>.5){p.y=.364+(p.y-.364)*_Growth;if(kind>1.5&&kind<2.5)p.xz*=lerp(.12,1,bloom);}
                if(kind>2.5){p.y+=sin(_PreviewClock*.8+input.data.y)*.025*bloom;}
                output.positionCS=TransformObjectToHClip(p);output.kind=kind;output.growth=_Growth;output.color=input.color;
                if(kind>1.5&&kind<2.5)output.color.rgb*=1.15;
                if(kind>2.5)output.color.a*=bloom*(.7+.3*sin(_PreviewClock*.9+input.data.y));
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                if(input.kind>.5)clip(input.growth-.002);
                half shade=input.kind<.5?.68:1;return half4(input.color.rgb*shade,input.color.a);
            }
            ENDHLSL
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Supported pot-local metres; only above-soil rigid flower vertices scale, aura stays within declared bounds. Clock is explicit local presentation input, no global Time/sky.
// Array Formats: Fixed vertex POSITION/COLOR/UV(kind,id) and two cached scalar uniforms; no textures, loops, native allocation or particle simulation.
// Dependencies/Pointers: Original first-party cold flower owner supplies owned mesh/material and host-derived growth; transparent low-alpha aura emits no real light. Root must validate native shader import/appearance.
