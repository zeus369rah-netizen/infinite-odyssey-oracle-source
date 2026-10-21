Shader "OdysseyOracle/WorkshopBuild015"
{
    Properties { _WorkshopBuild("Progress Active Reduced Type",Vector)=(1,0,0,0) _WorkshopHeight("Saved model height",Float)=2 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _WorkshopBuild;
            float _WorkshopHeight;
            CBUFFER_END
            struct Attributes {float4 positionOS:POSITION;float4 color:COLOR;float4 meta:TEXCOORD1;};
            struct Varyings {float4 positionCS:SV_POSITION;half4 color:COLOR;};
            float3 ElementTint011(float type){return type<.5?float3(1,.36,.12):type<1.5?float3(.18,.68,1):type<2.5?float3(.62,.78,.29):float3(.66,.91,1);}
            float2 Turn(float2 p,float a){float s=sin(a),c=cos(a);return float2(c*p.x-s*p.y,s*p.x+c*p.y);}
            Varyings vert(Attributes v)
            {
                Varyings o;float p=saturate(_WorkshopBuild.x),active=_WorkshopBuild.y,reduced=_WorkshopBuild.z;float kind=v.meta.x,seed=v.meta.y;float3 q=v.positionOS.xyz;float fade=active;float enter=sin(p*3.14159265);
                float3 color=v.color.rgb;
                if((kind>=.5&&kind<1.5)||kind>4.5)color=lerp(color,ElementTint011(_WorkshopBuild.w),.7);
                if(kind<.5)
                {
                    float age=frac(p*1.8+seed*.61803399);float a=seed*2.39996323;
                    q+=float3(cos(a)*(.30+age*.44),age*.58,sin(a)*(.30+age*.44));fade*=saturate((1-age)*1.3)*enter*.62*(1-reduced);
                }
                else if(kind<1.5)
                {
                    float h=frac(seed/24.0+p*1.4),a=h*12.56637+seed*.32;
                    q+=float3(cos(a)*(.64-h*.12),h*_WorkshopHeight,sin(a)*(.64-h*.12));fade*=enter*.85*(1-reduced);
                }
                else if(kind<2.5)
                {
                    q.xy=Turn(q.xy,sin(p*31.4159)*.55);q+=float3(-.88+(1-enter)*.6,.45+sin(p*18.8496)*.07,-.10);fade*=smoothstep(0,.12,p)*(1-smoothstep(.82,1,p))*(1-reduced);
                }
                else if(kind<3.5)
                {
                    q.xy=Turn(q.xy,-.25);q+=float3(.79-(1-enter)*.5,.63,sin(p*37.6991)*.14);fade*=smoothstep(0,.12,p)*(1-smoothstep(.82,1,p))*(1-reduced);
                }
                else
                {
                    if(kind>4.5){q.x=q.x*p-(1-p)*.43;}
                    float3 origin=TransformObjectToWorld(float3(0,_WorkshopHeight+.23,0));
                    float3 right=normalize(UNITY_MATRIX_I_V._m00_m10_m20),up=normalize(UNITY_MATRIX_I_V._m01_m11_m21),back=normalize(UNITY_MATRIX_I_V._m02_m12_m22);
                    // Derive actual parent scale so atlas rows and world bars use the same model-relative footprint.
                    float scale=length(TransformObjectToWorldDir(float3(1,0,0),false));
                    o.positionCS=TransformWorldToHClip(origin+(right*q.x+up*q.y+back*q.z)*scale);o.color=half4(color,fade);return o;
                }
                o.positionCS=TransformObjectToHClip(q);o.color=half4(color,fade);return o;
            }
            half4 frag(Varyings i):SV_Target {return i.color;}
            ENDHLSL
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Original saved-floor-local shader motion and model-relative camera-facing bar; world/private-atlas matrices stay external.
// Array Formats: One84-box shared mesh uses TEXCOORD1 kind/seed and vertex colors; MPB progress/active/reduced/type plus saved height, no compute/particles/skin buffers.
// Dependencies/Pointers: Presentation only, no lights/collision/damage. WebGPU/WebGL shader compilation, transparent overlap and native visual acceptance remain root gates; no engine or provider code copied.
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing accepted elemental shop/UI/listener or cosmetic atlas space; no authority or saved-key changes.
// Array Formats: Four bounded clip/image identities and accepted-element bits; existing12models/fourcuevoices and workshop mesh.
// Dependencies/Pointers: Root follow-up imports four retained generated cues; baseline/replay/request failure stays silent, no hot loads/new audio sources or media/provider work here.
