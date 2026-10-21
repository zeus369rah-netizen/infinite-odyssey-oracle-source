Shader "Oracle/Presentation/LivingCampfire"
{
    Properties
    {
        _Height("Existing flame height",Range(.15,1.2))=.78
        _LocalClock("Private held presentation clock",Float)=.31
        _Detail("Existing particle quality",Range(0,2))=1
        _Fuel("Host room flame fuel",Range(0,1))=1
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent"}
        Pass
        {
            Tags {"LightMode"="SRPDefaultUnlit"}
            Blend One OneMinusSrcAlpha
            Cull Off ZWrite Off ZTest LEqual
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "../../Oracle/Presentation/Shaders/WorldWind015.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Height,_LocalClock,_Detail,_Fuel;
            CBUFFER_END
            struct Attributes{float3 p:POSITION;float2 uv:TEXCOORD0;float4 tag:TEXCOORD1;};
            struct Varyings{float4 p:SV_POSITION;float2 uv:TEXCOORD0;float4 effect:TEXCOORD1;float enabled:TEXCOORD2;};
            Varyings Vert(Attributes v)
            {
                Varyings o;o.uv=v.uv;o.enabled=1;float3 local=0;float envelope=1,ny=v.p.y;
                float2 wind;float speed,time,gain;WindField015(wind,speed,time,gain);
                wind=mul((float3x3)GetWorldToObjectMatrix(),float3(wind.x,0,wind.y)).xz;
                float2 drift,slope;
                if(v.tag.x<.5)
                {
                    float y=saturate(ny),radius=(.20+.80*sin(3.14159265359*y))*(1-.78*y);
                    float width=v.tag.y<.5?.34:.21,height=v.tag.y<.5?1.05:.68+.07*v.tag.y;
                    float2 baseXZ=v.tag.y<.5?float2(0,0):float2(cos(v.tag.z),sin(v.tag.z))*.11;
                    float2 bend=float2(sin(_LocalClock*3.2+v.tag.z+y*5)*.13,cos(_LocalClock*2.7+v.tag.z+y*4)*.09)*y*y;
                    local=float3(baseXZ.x+v.p.x*radius*width+bend.x,ny*height*(1+.055*sin(_LocalClock*5+v.tag.z)),baseXZ.y+v.p.z*radius*width+bend.y)*_Height;
                    WindSample015(y,v.tag.z,time,wind,gain*saturate(speed),_Height*.065,1,drift,slope);local.xz+=drift;
                    if(_Detail<.5&&v.tag.y>2.5)o.enabled=0;
                }
                else if(v.tag.x<1.5)
                {
                    float life=frac(_LocalClock*.38+v.tag.y/8.0);
                    float pulse=sin(3.14159265359*life);envelope=pulse*pulse;
                    float angle=v.tag.z+life*3;
                    float3 center=float3(sin(angle)*(.10+.20*life),.25+1.8*life,cos(angle)*(.10+.20*life));
                    local=(center+v.p*(.008+.013*(1-life))*envelope)*_Height;
                    WindSample015(life,v.tag.z,time,wind,gain*saturate(speed),_Height*.12,1,drift,slope);local.xz+=drift;
                    if(_Detail<.5&&v.tag.y>3.5)o.enabled=0;
                }
                else
                {
                    float life=frac(_LocalClock*.24+v.tag.y/4.0);
                    float pulse=sin(3.14159265359*life);envelope=pulse*pulse;
                    float size=(.12+.22*life)*pulse*(1-smoothstep(.65,1,life))*_Height*_Fuel;
                    float3 center=float3(sin(v.tag.z+life*2.2)*(.05+.28*life),.95+2*life,cos(v.tag.z+life*1.8)*(.05+.28*life))*_Height;
                    WindSample015(life,v.tag.z,time,wind,gain*saturate(speed),_Height*.12,1,drift,slope);center.xz+=drift;
                    float3 world=TransformObjectToWorld(center*_Fuel)+UNITY_MATRIX_I_V._m00_m10_m20*(v.p.x*size)+UNITY_MATRIX_I_V._m01_m11_m21*(v.p.y*size);
                    o.p=TransformWorldToHClip(world);o.effect=float4(v.tag.x,life,envelope,v.tag.z);
                    if(_Detail<.5&&v.tag.y>1.5)o.enabled=0;return o;
                }
                o.p=TransformObjectToHClip(local*_Fuel);o.effect=float4(v.tag.x,ny,envelope,v.tag.z);return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                clip(i.enabled-.5);clip(_Fuel-.001);half3 color;float alpha;
                if(i.effect.x<.5)
                {
                    float hot=pow(saturate(1-i.effect.y),2);
                    color=lerp(half3(1,.19,.018),half3(1,.76,.20),hot)*1.6;
                    alpha=(.60-.28*saturate(i.effect.y))*(.82+.18*sin(_LocalClock*6+i.uv.x*9+i.effect.y*8+i.effect.w));
                }
                else if(i.effect.x<1.5){color=half3(1,.45,.055)*1.8;alpha=.78*i.effect.z;}
                else
                {
                    float2 q=i.uv*2-1;float feather=saturate(1-dot(q,q));feather*=feather;
                    color=half3(.20,.22,.25);alpha=.10*i.effect.z*feather;
                }
                alpha*=_Fuel;return half4(color*alpha,alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Geometry-root local+Z/Y-up normalized 3D lobes/octrahedra -> world; four smoke billboards use world camera axes. Private MPB clock only, no global time/sky mutation.
// Array Formats: Fixed424 vertices/2316 indices with UV1(type,id,seed,0); quality0 admits3lobes/4embers/2smoke, higher quality5/8/4. Shader-only sway, rise and bounded birth/death shrink/fade.
// Dependencies/Pointers: Original URP Core math, one owned premultiplied transparent material; no textures/providers/light/physics/skin/compute or gameplay authority. Native shader/appearance/performance acceptance remains root-owned.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing refuge geometry/world axes and support pivots unchanged; elemental numeric state applies only to the supported refuge.
// Array Formats: Existing owner arrays remain canonical; fixed512-node/8word reaction state supplies bounded scalar presentation gains.
// Dependencies/Pointers: Host accepted single Fire/Earth/Wind shots only; existing Object adapters retain materials/lights/lifetime, no rewards/saves or shrine/portal gameplay mutation.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: existing anchored presentation receives the same bounded passive world-XZ field; geometry/physics/accepted room fuel stay canonical.
// Array Formats: existing fixed mesh/vertex/material owner and one shared float4; no hot collections, per-window objects or mutable gameplay alias.
// Dependencies/Pointers: work-only shared-breeze adaptation; original shaders and fallback lifetimes remain. Root alone imports/tests actual native/WebGL visuals, culling, allocation and lifecycle.
