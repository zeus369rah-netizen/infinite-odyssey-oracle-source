Shader "Oracle/Presentation/SoftDressingCandidate"
{
    Properties
    {
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
            CBUFFER_START(UnityPerMaterial)
                half4 _GrassTint,_FlameBase,_FlameTip;
                float4 _Wind;
            CBUFFER_END
            struct Attributes {float3 positionOS:POSITION;float3 normalOS:NORMAL;half4 color:COLOR;float4 data:TEXCOORD0;float4 pivot:TEXCOORD1;};
            struct Varyings {float4 positionCS:SV_POSITION;float3 normalWS:TEXCOORD0;half4 color:COLOR;float4 data:TEXCOORD1;half fog:TEXCOORD2;};
            Varyings Vert(Attributes v)
            {
                Varyings o;
                float3 p=v.positionOS,normal=v.normalOS;
                float weight=saturate(v.data.x),phase=v.data.y;
                if(v.data.z<.5)
                {
                    float2 wind=_Wind.xy*rsqrt(max(dot(_Wind.xy,_Wind.xy),1e-6));
                    float amplitude=clamp(_Wind.z,0,.12)*(sin(_Time.y*clamp(_Wind.w,0,4)+phase)+.35*sin(_Time.y*2.1+phase*1.7));
                    p.xz+=wind*amplitude*weight*weight;
                    normal.xz-=wind*amplitude*2*weight/max(v.data.w,.05);
                }
                else
                {
                    float3 local=p-v.pivot.xyz;
                    local.y*=1+.08*sin(_Time.y*10+phase)+.03*sin(_Time.y*17+phase*1.7);
                    local.xz*=.94+.06*sin(_Time.y*8+phase);
                    local.xz+=float2(sin(_Time.y*6+phase),cos(_Time.y*7+phase))*.02*weight*weight;
                    p=v.pivot.xyz+local;
                }
                o.positionCS=TransformObjectToHClip(p);o.normalWS=TransformObjectToWorldNormal(normal);
                o.color=v.color;o.data=v.data;o.fog=ComputeFogFactor(o.positionCS.z);return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                half3 color;
                if(i.data.z<.5)
                {
                    half3 n=normalize(i.normalWS);Light sun=GetMainLight();
                    half3 light=max(SampleSH(n),half3(.08,.08,.08))+sun.color*saturate(dot(n,sun.direction));
                    color=i.color.rgb*_GrassTint.rgb*lerp(.55,1.15,saturate(i.data.x))*light;
                }
                else
                {
                    half flicker=.91+.06*sin(_Time.y*11+i.data.y)+.03*sin(_Time.y*19+i.data.y*1.9);
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
