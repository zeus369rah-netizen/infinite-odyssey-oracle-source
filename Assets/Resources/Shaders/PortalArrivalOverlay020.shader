Shader "OdysseyOracle/PortalArrivalOverlay020"
{
    Properties {_ArrivalPhase020("Accepted bounded arrival phase",Range(0,1))=0 _ArrivalAspect020("Screen aspect",Float)=1.7778 _ArrivalMotion020("Permitted motion",Range(0,1))=1}
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Overlay"}
        Pass
        {
            Name "OriginalPurplePeripheralRipple020" Tags {"LightMode"="UniversalForward"}
            Blend SrcAlpha OneMinusSrcAlpha ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float _ArrivalPhase020,_ArrivalAspect020,_ArrivalMotion020;
            CBUFFER_END
            struct A {float3 positionOS:POSITION;float2 uv:TEXCOORD0;};
            struct V {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
            V Vert(A a){V o;o.positionCS=float4(a.positionOS.xy,0,1);o.uv=a.uv;return o;}
            half4 Frag(V i):SV_Target
            {
                float t=saturate(_ArrivalPhase020),moving=t*saturate(_ArrivalMotion020),phase=moving*6.28318530718;
                float2 p=(i.uv-.5)*float2(clamp(_ArrivalAspect020,.5,3),1);
                float r=length(p);clip(r-.19);
                float angle=atan2(p.y,p.x);
                float boundary=.30+.018*sin(angle*5+phase*1.25)+.012*sin(angle*9-phase);
                float rim=smoothstep(boundary-.035,boundary+.21,r);
                float ripple=exp2(-pow((r-(.26+moving*.58))*14,2));
                float wobble=.5+.5*sin(r*26-angle*2-phase*2);
                float envelope=smoothstep(0,.08,t)*(1-smoothstep(.14,1,t));
                float alpha=rim*(.20+.13*wobble+.18*ripple)*envelope;
                float3 color=lerp(float3(.18,.025,.41),float3(.40,.055,.67),wobble*.55+ripple*.35);
                return half4(color,saturate(alpha));
            }
            ENDHLSL
        }
    }
    Fallback Off
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Aspect-correct normalized screen UV; fully clear central radius.19 and soft original continuous purple peripheral veil/ripple.
// Array Formats: One bounded phase/aspect material pair and reused quad; no camera texture, render texture, copied tile patterns, scene distortion or global time.
// Dependencies/Pointers: Accepted-arrival adapter alone drives1.1active-second fade and disposes resources. No input interception or gameplay mutation; native visual/readability/listening acceptance remains root-owned.
