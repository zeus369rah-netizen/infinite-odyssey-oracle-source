Shader "OdysseyOracle/PortalAnomaly"
{
    Properties
    {
        [HDR] _RimColor("Original violet accretion",Color)=(1.5,0.08,4.1,1)
        _AnomalyTime("Bounded local animation clock",Float)=0
        _HighQuality("High quality",Float)=1
        _UseOpaqueTexture("Camera already supplies opaque color",Float)=0
        _Radius("Local aperture radius",Float)=1.4
        _MaxDistortionPixels("Comfort limit in screen pixels",Range(0,14))=14
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-20"}
        Pass
        {
            Name "OriginalFerrofluidLens"
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
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _RimColor;
                float _AnomalyTime;
                float _HighQuality;
                float _UseOpaqueTexture;
                float _Radius;
                float _MaxDistortionPixels;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS:POSITION;
                float2 uv:TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS:SV_POSITION;
                float2 plane:TEXCOORD0;
                float3 positionWS:TEXCOORD1;
                half fog:TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float Spike(float angle,float time)
            {
                float count=lerp(13.0,23.0,_HighQuality);
                float tooth=saturate(.5+.5*sin(angle*count+sin(angle*5.0-time*.37)*.75));
                float tooth2=tooth*tooth;return tooth2*tooth2*tooth2;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output=(Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input,output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float2 p=input.uv;float r=length(p);
                float angle=atan2(p.y,p.x),time=_AnomalyTime;
                float outer=1.0-smoothstep(1.03,1.22,r);
                float liquid=smoothstep(.40,.84,r)*outer;
                float teeth=Spike(angle,time);
                float3 position=input.positionOS.xyz;
                float dilation=1.0+liquid*(teeth*.027+sin(angle*7.0-time*.52)*.008);
                position.xy*=dilation;
                position.z+=_Radius*liquid*(.012*sin(r*36.0-angle*3.0-time*1.15)+teeth*.050);
                output.positionWS=TransformObjectToWorld(position);
                output.positionCS=TransformWorldToHClip(output.positionWS);
                output.plane=p;
                output.fog=ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 p=input.plane;float r=max(length(p),.0001);
                float angle=atan2(p.y,p.x),time=_AnomalyTime;
                float tooth=Spike(angle,time);
                float skinRadius=.925+tooth*.068+.014*sin(angle*7.0-time*.52);
                float aa=max(fwidth(r),.0015);
                float liquid=1.0-smoothstep(skinRadius-aa,skinRadius+.022+aa,r);
                float outerFade=1.0-smoothstep(1.075,1.22,r);
                float core=1.0-smoothstep(.25,.51,r);

                // Moving concentric ridges and sheared currents suggest an original
                // glossy black liquid. No texture downloads or volume/raymarch pass.
                float curl=angle+1.55/(r+.28)-time*.22;
                float flow=sin(r*48.0+sin(curl*3.0)*1.25-time*1.35);
                float ridge=pow(saturate(flow*.5+.5),8.0)*smoothstep(.31,.65,r);
                float shine=pow(saturate(.5+.5*sin(curl*5.0+r*15.0)),12.0);
                float narrow=abs(r-skinRadius);
                float rim=1.0-smoothstep(.003+aa,.027+aa,narrow);
                float innerRing=(1.0-smoothstep(.002+aa,.013+aa,abs(r-(.69+.014*sin(curl*4.0)))))*.24;
                float trailing=(1.0-smoothstep(.015,.17,narrow))*.11;
                float3 liquidColor=float3(.0025,.003,.008)+ridge*float3(.018,.014,.031);
                liquidColor+=shine*ridge*float3(.065,.032,.095)*lerp(.3,1.0,_HighQuality);
                liquidColor*=1.0-core*.91;
                float3 glow=_RimColor.rgb*(rim*(.65+tooth*.40)+innerRing+trailing);
                float3 color=MixFog(liquidColor+glow,input.fog);
                float alpha=saturate(liquid+rim*.75+innerRing*.5+trailing*.85)*outerFade;

                float3 centerWS=TransformObjectToWorld(float3(0,0,0));
                float3 normalWS=normalize(TransformObjectToWorldNormal(float3(0,0,1)));
                float3 viewDelta=_WorldSpaceCameraPos-centerWS;
                float planeGap=abs(dot(viewDelta,normalWS));
                // Fade rather than fill the view when the eye intersects the sheet.
                float nearComfort=smoothstep(.15,.55,planeGap);
                float bendComfort=smoothstep(.60,1.65,planeGap);
                float alignment=abs(dot(viewDelta/max(length(viewDelta),.0001),normalWS));
                bendComfort*=smoothstep(.12,.45,alignment);

                // The branch is off for low quality and cameras without a configured
                // opaque texture. The texel-size guard rejects an unbound default.
                if(_UseOpaqueTexture>.5 && _HighQuality>.5 && _CameraOpaqueTexture_TexelSize.z>4.0)
                {
                    float lens=smoothstep(.72,.96,r)*(1.0-smoothstep(1.01,1.22,r));
                    float2 radial=p/r,tangent=float2(-radial.y,radial.x);
                    float2 bend=radial*(-.80)+tangent*(.25+.12*sin(angle*3.0-time*.45));
                    float3 bendWS=TransformObjectToWorldDir(float3(bend,0),true);
                    float2 bendVS=mul((float3x3)UNITY_MATRIX_V,bendWS).xy;
                    bendVS/=max(1.0,length(bendVS));
                    float2 pixel=rcp(max(_ScaledScreenParams.xy,float2(1.0,1.0)));
                    float2 screenUV=GetNormalizedScreenSpaceUV(input.positionCS);
                    float2 offset=bendVS*min(_MaxDistortionPixels,14.0)*pixel*lens*bendComfort;
                    float2 sampleUV=clamp(screenUV+offset,pixel*1.5,1.0-pixel*1.5);
                    float3 scene=SampleSceneColor(sampleUV);
                    float outside=(1.0-liquid)*lens;
                    color=lerp(color,scene+glow*.7,outside);
                    alpha=max(alpha,outside*.94)*outerFade;
                }
                alpha*=nearComfort;
                return half4(color,saturate(alpha));
            }
            ENDHLSL
        }
    }
    Fallback Off
}
