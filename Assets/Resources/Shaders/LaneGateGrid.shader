Shader "OdysseyOracle/LaneGateGrid"
{
    Properties
    {
        _BaseColor ("Transparent red shell", Color) = (0.8,0.015,0.008,0.15)
        [HDR] _GridColor ("Red matrix lines", Color) = (1.4,0.045,0.025,0.72)
        _GridSpacing ("Grid spacing in metres", Float) = 0.7
        _LineWidth ("Line width in metres", Float) = 0.025
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+25" }
        Pass
        {
            Name "InactiveLaneGrid"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _GridColor;
                float _GridSpacing;
                float _LineWidth;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionOS : TEXCOORD0; float3 normalOS : TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                output.positionOS=input.positionOS.xyz;
                output.normalOS=input.normalOS;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float3 n=abs(input.normalOS);
                float2 plane=n.y>.5?input.positionOS.xz:(n.x>.5?input.positionOS.yz:input.positionOS.xy);
                float spacing=max(_GridSpacing,.05);
                float2 d=abs(frac(plane/spacing+.5)-.5)*spacing;
                float edge=min(d.x,d.y);
                float antialias=max(fwidth(edge),.002);
                float grid=1-smoothstep(_LineWidth,_LineWidth+antialias,edge);
                float scan=.92+.08*sin(input.positionOS.y*4-_Time.y*1.8);
                half4 color=lerp(_BaseColor,_GridColor,grid);
                color.rgb*=scan;
                return color;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
