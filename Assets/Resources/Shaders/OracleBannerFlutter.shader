Shader "OdysseyOracle/SupportedBannerFlutter"
{
    Properties
    {
        _BaseMap("Original pixels",2D)="white"{}
        _BaseColor("Tint",Color)=(1,1,1,1)
        _Metallic("Metallic",Range(0,1))=0
        _Smoothness("Smoothness",Range(0,1))=.055
        [HDR] _EmissionColor("Emission",Color)=(0,0,0,1)
    }
    SubShader
    {
        Tags{"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
        Cull Back
        ZWrite On
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
        TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor,_EmissionColor;
            half _Metallic,_Smoothness;
        CBUFFER_END
        struct Attributes{float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;};
        struct Varyings{float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;half3 normalWS:TEXCOORD1;float2 uv:TEXCOORD2;half fog:TEXCOORD3;};
        void Flutter(inout float3 p,inout float3 n)
        {
            float down=-p.y,w=saturate((down-.08)/2.41);
            float phase=_Time.y*1.7+down*2.4+p.x*2;
            float wave=sin(phase),slope=cos(phase),weight=w*w;
            float dy=(down>.08&&down<2.49)?-1.0/2.41:0;
            float dx=.035*slope*2*weight;
            float ddy=.035*(-2.4*slope*weight+wave*2*w*dy);
            p.z+=.035*wave*weight;
            n=normalize(float3(n.x-dx*n.z,n.y-ddy*n.z,n.z));
        }
        Varyings Vert(Attributes input)
        {
            Varyings output;float3 p=input.positionOS.xyz,n=input.normalOS;Flutter(p,n);
            VertexPositionInputs position=GetVertexPositionInputs(p);
            output.positionCS=position.positionCS;output.positionWS=position.positionWS;
            output.normalWS=TransformObjectToWorldNormal(n);output.uv=TRANSFORM_TEX(input.uv,_BaseMap);
            output.fog=ComputeFogFactor(position.positionCS.z);return output;
        }
        half4 Depth(Varyings input):SV_TARGET{return 0;}
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags{"LightMode"="UniversalForwardOnly"}
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            half4 Frag(Varyings input):SV_TARGET
            {
                SurfaceData surface=(SurfaceData)0;surface.albedo=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv).rgb*_BaseColor.rgb;
                surface.alpha=1;surface.metallic=saturate(_Metallic);surface.smoothness=saturate(_Smoothness);
                surface.normalTS=half3(0,0,1);surface.emission=_EmissionColor.rgb;surface.occlusion=1;
                InputData lighting=(InputData)0;lighting.positionWS=input.positionWS;lighting.positionCS=input.positionCS;
                lighting.normalWS=NormalizeNormalPerPixel(input.normalWS);lighting.viewDirectionWS=GetWorldSpaceNormalizeViewDir(input.positionWS);
                lighting.shadowCoord=TransformWorldToShadowCoord(input.positionWS);lighting.bakedGI=SampleSH(lighting.normalWS);
                lighting.vertexLighting=VertexLighting(input.positionWS,lighting.normalWS);lighting.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(input.positionCS);lighting.shadowMask=half4(1,1,1,1);
                half4 color=UniversalFragmentPBR(lighting,surface);color.rgb=MixFog(color.rgb,input.fog);return color;
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags{"LightMode"="ShadowCaster"}
            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowVert
            #pragma fragment Depth
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            float3 _LightDirection,_LightPosition;
            Varyings ShadowVert(Attributes input)
            {
                Varyings output=(Varyings)0;float3 p=input.positionOS.xyz,n=input.normalOS;Flutter(p,n);
                float3 ws=TransformObjectToWorld(p),normalWS=TransformObjectToWorldNormal(n);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 direction=normalize(_LightPosition-ws);
                #else
                    float3 direction=_LightDirection;
                #endif
                output.positionCS=ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(ws,normalWS,direction)));return output;
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags{"LightMode"="DepthOnly"}
            ColorMask R
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Depth
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags{"LightMode"="DepthNormalsOnly"}
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Normals
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            half4 Normals(Varyings input):SV_TARGET
            {
                half3 normal=NormalizeNormalPerPixel(input.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 oct=PackNormalOctQuadEncode(normal);return half4(PackFloat2To888(saturate(oct*.5+.5)),0);
                #else
                    return half4(normal,0);
                #endif
            }
            ENDHLSL
        }
    }
    Fallback Off
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: local cloth Y0 at rigid rod; shared XY deformation for original cloth, trim and relief.
// Array Formats: immutable vertex positions/normals/UVs plus Unity time; no uploaded frame buffers or CPU animation.
// Dependencies/Pointers: top80mm pinned, normal-corrected Z flutter <=35mm; forward/depth/normals/shadow passes use the same function. Native shader/backend acceptance remains required.
