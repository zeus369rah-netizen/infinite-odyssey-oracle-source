Shader "OdysseyOracle/OracleNightSky"
{
    Properties
    {
        _HorizonColor("Midnight blue horizon",Color)=(0.012,0.025,0.065,1)
        _ZenithColor("Deep blue zenith",Color)=(0.0025,0.006,0.021,1)
        _LowerColor("Continuous lower sky",Color)=(0.006,0.012,0.034,1)
        _MoonColor("Restrained moon and halo",Color)=(0.40,0.49,0.63,1)
        _MoonDirection("World moon direction",Vector)=(1,0.30,0.32,0)
        _MoonBrightness("Moon brightness",Range(0,1))=0.38
        _StarBrightness("Sparse star brightness",Range(0,1))=0.36
        _CloudAmount("Subtle distant wisps",Range(0,1))=0.20
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox"}
        Pass
        {
            Name "ContinuousOriginalMidnight"
            Cull Off
            ZWrite Off
            ZTest LEqual
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _HorizonColor;
                half4 _ZenithColor;
                half4 _LowerColor;
                half4 _MoonColor;
                float4 _MoonDirection;
                float _MoonBrightness;
                float _StarBrightness;
                float _CloudAmount;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS:POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS:SV_POSITION;
                float3 direction:TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                Varyings output=(Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                #if UNITY_REVERSED_Z
                    output.positionCS.z=0.0;
                #else
                    output.positionCS.z=output.positionCS.w;
                #endif
                output.direction=input.positionOS.xyz;
                return output;
            }

            float3 Hash3(float3 p)
            {
                // Original deterministic arithmetic pattern; no texture or random stream.
                p=frac(p*float3(.1031,.1030,.0973));
                p+=dot(p,p.yxz+33.33);
                return frac((p.xxy+p.yxx)*p.zyx);
            }

            half4 Frag(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 d=normalize(input.direction);
                float upper=smoothstep(0.0,.90,saturate(d.y));
                float3 color=lerp(_HorizonColor.rgb,_ZenithColor.rgb,upper);
                color=lerp(_LowerColor.rgb,color,smoothstep(-.45,.06,d.y));

                // World-direction functions have no panoramic UV seam or finite edge.
                // Two static, low-frequency waves suggest sparse, quiet cloud wisps.
                float cloudWave=sin(d.x*12.0+d.z*8.0)+sin(d.z*15.0-d.y*19.0)*.50;
                float cloud=smoothstep(.84,1.42,cloudWave)*smoothstep(.01,.13,d.y)*(1.0-smoothstep(.45,.78,d.y));
                color=lerp(color,float3(.027,.039,.073),cloud*_CloudAmount);

                // Sparse stars on a 3D directional lattice avoid cubemap seams.
                // Antialiasing grows their tiny cores only to pixel footprint size.
                float3 grid=d*170.0,cell=floor(grid);
                float3 seed=Hash3(cell),starPosition=Hash3(cell+19.17)*.64+.18;
                float starDistance=length(frac(grid)-starPosition);
                float aa=max(fwidth(starDistance),.018);
                float star=(1.0-smoothstep(.018,.075+aa,starDistance))*step(.9958,seed.z);
                star*=smoothstep(.015,.20,d.y)*(1.0-cloud*.65);
                color+=star*_StarBrightness*lerp(float3(.62,.74,1.0),float3(1.0,.92,.78),seed.x);

                float3 moonDirection=normalize(_MoonDirection.xyz);
                float moonDistance=length(d-moonDirection);
                float moonAA=max(fwidth(moonDistance),.0004);
                float disc=1.0-smoothstep(.020-moonAA,.020+moonAA,moonDistance);
                float halo=exp2(-moonDistance*moonDistance*170.0)*.028;
                // Soft tonal shading keeps the small disc quiet without an asset.
                float moonFace=.66+.34*saturate(dot(d,float3(-.30,.40,.86))+.45);
                color+=_MoonColor.rgb*_MoonBrightness*(disc*moonFace+halo);
                return half4(max(color,0.0),1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
