Shader "OdysseyOracle/VoxelDistanceLit"
{
    // # ARCHITECTURE SNAPSHOT
    // GPU opaque PBR texture-detail style only; no mesh LOD or native-buffer ownership.
    // Material-clone budget and local-camera enable scope are owned by the main-thread controller.
    Properties
    {
        _BaseMap("Original pixel atlas", 2D) = "white" {}
        _BaseColor("Tint", Color) = (1,1,1,1)
        _Metallic("Metallic", Range(0,1)) = 0
        _Smoothness("Smoothness", Range(0,1)) = .055
        [HDR] _EmissionColor("Emission", Color) = (0,0,0,1)
        _OracleDetailStart("Far detail begins (metres)", Float) = 38.4
        _OracleDetailEnd("Far detail reaches strength (metres)", Float) = 86.4
        _OracleDetailStrength("Far texture pixelation and mip bias", Range(0,1)) = .35
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Cull Back
        ZWrite On
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        float _OracleLocalDistanceDetailEnabled;
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _BaseMap_TexelSize;
            half4 _BaseColor;
            half4 _EmissionColor;
            half _Metallic, _Smoothness;
            float _OracleDetailStart, _OracleDetailEnd, _OracleDetailStrength;
        CBUFFER_END
        struct OracleAttributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
        struct OracleVaryings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            half3 normalWS : TEXCOORD1;
            float2 uv : TEXCOORD2;
            half fog : TEXCOORD3;
        };
        OracleVaryings OracleVertex(OracleAttributes input)
        {
            OracleVaryings output;
            VertexPositionInputs vertex = GetVertexPositionInputs(input.positionOS.xyz);
            output.positionCS = vertex.positionCS;
            output.positionWS = vertex.positionWS;
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
            output.fog = ComputeFogFactor(vertex.positionCS.z);
            return output;
        }
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex OracleVertex
            #pragma fragment OracleFragment
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            half4 OracleFragment(OracleVaryings input) : SV_TARGET
            {
                float viewDistance = distance(_WorldSpaceCameraPos.xz, input.positionWS.xz);
                float blend = smoothstep(_OracleDetailStart, max(_OracleDetailStart + .01, _OracleDetailEnd), viewDistance);
                blend *= saturate(_OracleDetailStrength) * saturate(_OracleLocalDistanceDetailEnabled);
                half4 texel = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                UNITY_BRANCH if (blend > .001)
                {
                    // One extra far-only sample. Point-filtered 32px world atlases
                    // become 16px texture blocks, blended without a threshold pop.
                    // Bias is bounded to [0,1]; meshes and lighting stay full quality.
                    float2 coarseSize = max(_BaseMap_TexelSize.zw * .5, 1.0);
                    float2 coarseUV = (floor(input.uv * coarseSize) + .5) / coarseSize;
                    half4 coarse = SAMPLE_TEXTURE2D_BIAS(_BaseMap, sampler_BaseMap, coarseUV, blend);
                    texel = lerp(texel, coarse, blend);
                }
                SurfaceData surface = (SurfaceData)0;
                surface.albedo = texel.rgb * _BaseColor.rgb;
                surface.alpha = 1;
                surface.metallic = saturate(_Metallic);
                surface.smoothness = saturate(_Smoothness);
                surface.normalTS = half3(0,0,1);
                surface.emission = _EmissionColor.rgb;
                surface.occlusion = 1;
                InputData lighting = (InputData)0;
                lighting.positionWS = input.positionWS;
                lighting.positionCS = input.positionCS;
                lighting.normalWS = NormalizeNormalPerPixel(input.normalWS);
                lighting.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                lighting.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                lighting.bakedGI = SampleSH(lighting.normalWS);
                lighting.vertexLighting = VertexLighting(input.positionWS, lighting.normalWS);
                lighting.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                lighting.shadowMask = half4(1,1,1,1);
                half4 color = UniversalFragmentPBR(lighting, surface);
                // Radial XZ fog matches the CPU bounds cutoff even at the view's corners.
                // Ordinary depth fog would expose a distance-culling pop off-axis.
                if (_OracleLocalDistanceDetailEnabled > .5)
                {
                    float visibility = saturate(viewDistance * unity_FogParams.z + unity_FogParams.w);
                    color.rgb = lerp(unity_FogColor.rgb, color.rgb, visibility);
                }
                else color.rgb = MixFog(color.rgb, input.fog);
                return color;
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex OracleShadowVertex
            #pragma fragment OracleDepthFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            float3 _LightDirection, _LightPosition;
            float4 OracleShadowVertex(OracleAttributes input) : SV_POSITION
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 direction = normalize(_LightPosition - positionWS);
                #else
                    float3 direction = _LightDirection;
                #endif
                return ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, direction)));
            }
            half4 OracleDepthFragment() : SV_TARGET { return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ColorMask R
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex OracleVertex
            #pragma fragment OracleDepthFragment
            half4 OracleDepthFragment() : SV_TARGET { return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex OracleVertex
            #pragma fragment OracleNormalsFragment
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            half4 OracleNormalsFragment(OracleVaryings input) : SV_TARGET
            {
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 oct = PackNormalOctQuadEncode(normalize(input.normalWS));
                    return half4(PackFloat2To888(saturate(oct * .5 + .5)), 0);
                #else
                    return half4(NormalizeNormalPerPixel(input.normalWS), 0);
                #endif
            }
            ENDHLSL
        }
    }
    Fallback Off
}
