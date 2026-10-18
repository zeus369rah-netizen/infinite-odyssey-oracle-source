Shader "Oracle/Presentation/CrystalMotesCandidate"
{
    Properties {[HDR] _Tint("Purple motes",Color)=(1.5,.24,2.8,1)}
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent"}
        Pass
        {
            Tags {"LightMode"="SRPDefaultUnlit"}
            Blend SrcAlpha One Cull Off ZWrite Off ZTest LEqual
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
            CBUFFER_END
            struct Attributes {float3 root:POSITION;float4 data:TEXCOORD0;float4 settings:TEXCOORD1;};
            struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;half fade:TEXCOORD1;};
            Varyings Vert(Attributes v)
            {
                Varyings o;float age=frac(_Time.y*v.settings.w+v.data.z);
                float angle=v.data.w+_Time.y*.7;
                float3 p=v.root+float3(cos(angle)*v.settings.x,age*v.settings.y,sin(angle)*v.settings.x);
                float3 view=TransformWorldToView(TransformObjectToWorld(p));
                view.xy+=v.data.xy*v.settings.z;
                o.positionCS=mul(UNITY_MATRIX_P,float4(view,1));o.uv=v.data.xy;
                o.fade=sin(age*PI);return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                half radial=saturate(1-dot(i.uv,i.uv));
                return half4(_Tint.rgb,radial*radial*i.fade*_Tint.a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: root-local orbit/rise ->world ->view-plane billboard; Y-up; visual clock only.
// Array Formats:<=8 emitters x24 immutable quad records; UV0(cornerXY,phase01,angle), UV1(radius,height,size,speed).
// Dependencies/Pointers: URP shader3.0 vertex/fragment math only; no per-mote GameObject/CPU update/compute/light or collision writes.
