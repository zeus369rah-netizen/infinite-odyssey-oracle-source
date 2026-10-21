Shader "OdysseyOracle/PortalLandingGravity020"
{
    Properties
    {
        _PresentationClock020("Existing held presentation clock",Float)=0
        _Quality020("Bounded analytic detail",Range(0,2))=1
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-10"}
        Pass
        {
            Name "OriginalGravitySheet020"
            Tags {"LightMode"="SRPDefaultUnlit"}
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _PresentationClock020,_Quality020;
            CBUFFER_END
            struct A {float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
            struct V {float4 positionCS:SV_POSITION;float2 plane:TEXCOORD0;float3 world:TEXCOORD1;half fog:TEXCOORD2;};
            V Vert(A value)
            {
                V result;
                result.world=TransformObjectToWorld(value.positionOS.xyz);
                result.positionCS=TransformWorldToHClip(result.world);
                result.plane=value.uv*2-1;
                result.fog=ComputeFogFactor(result.positionCS.z);
                return result;
            }
            float Seed020(float2 cell)
            {
                float2 value=frac(cell*float2(.157,.113)+float2(.231,.719));
                float mixed=dot(value,value.yx+float2(9.31,5.73));
                return frac((value.x+mixed*.131)*(value.y+mixed+.27)*6.37);
            }
            float Stars020(float2 planeCoordinate,float scale,float salt,float phase,float aa)
            {
                float2 value=planeCoordinate*scale,cell=floor(value),local=frac(value);
                float seed=Seed020(cell+salt),second=Seed020(cell.yx+float2(salt+3.7,salt+8.1));
                float2 center=.24+.52*float2(seed,second);
                float distance=length(local-center);
                float star=1-smoothstep(.012,.045+clamp(aa*scale,.001,.08),distance);
                float pulse=.78+.22*sin(phase*512+second*6.28318530718);
                return step(.975,seed)*star*pulse;
            }
            float4 Field020(float2 planeCoordinate,float clock,float quality,float aa)
            {
                if(!all(planeCoordinate>=-1.001)||!all(planeCoordinate<=1.001))return 0;
                if(!(clock>=0)||!(clock<=4096))clock=0;
                if(!(quality>=0)||!(quality<=2))quality=0;
                if(!(aa>=0)||!(aa<=1))aa=.001;
                aa=clamp(aa,.001,.03);
                float phase=clock*(6.28318530718/4096.0);
                float radius=length(planeCoordinate),angle=radius>.0001?atan2(planeCoordinate.y,planeCoordinate.x):0;
                float aperture=1-smoothstep(.965,1.0,radius);
                float tooth=saturate(.5+.5*sin(angle*19+sin(angle*3-phase*64)*.75+phase*128));
                float square=tooth*tooth;
                tooth=square*square*square;
                float coreRadius=.245+.016*sin(phase*256)+.075*tooth;
                float liquid=1-smoothstep(coreRadius-.008,coreRadius+.026+aa,radius);
                float rim=1-smoothstep(.006,.028+aa,abs(radius-coreRadius));
                float ringRadius=.48+.016*sin(angle*3+phase*128);
                float ring=1-smoothstep(.008,.053+aa,abs(radius-ringRadius));
                float trail=(1-smoothstep(.025,.15,abs(radius-ringRadius)))*.20;
                float current=.58+.42*sin(angle*7-radius*22-phase*256);
                float warm=saturate(.5+.5*sin(angle*2-phase*64));
                warm*=warm;warm*=warm;
                float3 bandColor=lerp(float3(.65,.035,.42),float3(.90,.34,.12),warm);
                float shear=.17/(radius+.16)+phase*32;
                float sine=sin(shear),cosine=cos(shear);
                float2 warped=float2(planeCoordinate.x*cosine-planeCoordinate.y*sine,planeCoordinate.x*sine+planeCoordinate.y*cosine)*(1+.11/(radius+.18));
                float stars=Stars020(warped,19,11.7,phase,aa);
                float3 color=float3(.0015,.002,.006)+stars*float3(.30,.38,.56);
                if(quality>.5)
                {
                    float fine=saturate(.5+.5*sin(radius*71-angle*5-phase*256));
                    float fine2=fine*fine;fine=fine2*fine2*fine2;
                    color+=Stars020(warped,31,27.3,phase,aa)*float3(.22,.17,.29);
                    color+=liquid*smoothstep(.10,.25,radius)*fine*float3(.012,.018,.030);
                }
                color=lerp(color,float3(.0007,.0008,.0018),liquid*.97);
                color+=bandColor*(ring*(.28+.56*current)+trail*.28);
                color+=rim*(.026+.038*tooth)*float3(.62,.72,.92);
                return float4(clamp(color,0,1.8),aperture*.985);
            }
            half4 Frag(V value):SV_Target
            {
                if(!all(value.world>=-131072)||!all(value.world<=131072)||
                    !all(_WorldSpaceCameraPos>=-131072)||!all(_WorldSpaceCameraPos<=131072)||
                    !(value.fog>=0)||!(value.fog<=65504))return 0;
                float radius=length(value.plane);
                float4 field=Field020(value.plane,_PresentationClock020,_Quality020,max(fwidth(radius),.001));
                float3 delta=_WorldSpaceCameraPos-value.world;
                float gap=abs(delta.z),alignment=gap/max(length(delta),.0001);
                field.a*=smoothstep(.12,.50,gap)*smoothstep(.05,.20,alignment);
                return half4(MixFog(field.rgb,value.fog),saturate(field.a));
            }
            ENDHLSL
        }
    }
    Fallback Off
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Local normalized ellipse on a stationary XY sheet; analytic star-coordinate shear only, no scene refraction or vertex displacement.
// Array Formats: Two existing scalar uniforms;1or2fixed sparse star layers, bounded analytic core/rim/accretion/viscous spikes; zero loops/textures/buffers/RTs/lights.
// Dependencies/Pointers: Existing atmosphere held modulo4096 clock with integer harmonics, quality Low skips second star/fine highlights. Transparent depth-tested sheet remains behind unchanged magenta geometry; alpha fades aperture/near/edge-on views. Source/math checks do not constitute Unity shader/GPU/visual/performance acceptance.
