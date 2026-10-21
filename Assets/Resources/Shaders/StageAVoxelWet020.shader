Shader "Oracle/StageA Voxel Wet020"
{
    Properties
    {
        _Wetness020("Weather wetness", Range(0,1)) = 0
        _WindClock020("Held wind clock", Float) = 0
        _WindStrength020("Bounded wind strength", Range(0,1)) = 0
        _WindDirection020("World wind direction", Vector) = (1,0,0,0)
        _Reflections020("Reflected shine", Range(0,1)) = 1
        _EmissionScale020("Core emission scale", Range(0,2)) = 1
        [HideInInspector] _ReflectionPreferenceWeight020("Saved reflection preference weight", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Back
        ZWrite On
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        CBUFFER_START(UnityPerMaterial)
        float _Wetness020, _WindClock020, _WindStrength020, _Reflections020;
        float4 _WindDirection020;
        float _EmissionScale020, _ReflectionPreferenceWeight020;
        CBUFFER_END
        // Existing shared preference owner publishes this global; materials never shadow it.
        float _OracleReflectionStrength020;
        float3 _LightDirection, _LightPosition;
        struct A020
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 color : COLOR;
            float2 exposurePool : TEXCOORD0;
            float2 lightmap : TEXCOORD1;
            float2 pivotAXY : TEXCOORD2;
            float2 pivotAZAngle : TEXCOORD3;
            float2 pivotBXY : TEXCOORD4;
            float2 pivotBZAngle : TEXCOORD5;
            float2 phase : TEXCOORD6;
            float2 frequency : TEXCOORD7;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct V020
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            half3 normalWS : TEXCOORD1;
            float2 exposurePool : TEXCOORD2;
            float4 shadowCoord : TEXCOORD3;
            half fog : TEXCOORD4;
            DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 5);
            half3 vertexLighting : TEXCOORD6;
            half4 color : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };
        float3 Rotate020(float3 value, float3 axis, float angle)
        {
            float s, c; sincos(angle, s, c);
            return value*c + cross(axis,value)*s + axis*dot(axis,value)*(1-c);
        }
        void Deform020(A020 a, out float3 position, out float3 normal)
        {
            float3 wind = mul((float3x3)GetWorldToObjectMatrix(), float3(_WindDirection020.x,0,_WindDirection020.z));
            wind.y = 0;
            float lengthSquared=dot(wind,wind);
            wind=lengthSquared>1e-8 ? wind*rsqrt(max(lengthSquared,1e-8)) : float3(1,0,0);
            float3 axis = float3(wind.z,0,-wind.x);
            // Authored Hz are integer multiples of 1/4096, so held-clock wrap is continuous.
            float2 angle = sin(a.phase + a.frequency * (_WindClock020 * 6.28318530718));
            angle *= float2(a.pivotAZAngle.y,a.pivotBZAngle.y) * saturate(_WindStrength020);
            float3 pivotA = float3(a.pivotAXY,a.pivotAZAngle.x);
            float3 pivotB = float3(a.pivotBXY,a.pivotBZAngle.x);
            position = Rotate020(a.positionOS.xyz-pivotB,axis,angle.y)+pivotB;
            normal = Rotate020(a.normalOS,axis,angle.y);
            position = Rotate020(position-pivotA,axis,angle.x)+pivotA;
            normal = Rotate020(normal,axis,angle.x);
        }
        V020 Vertex020(A020 a)
        {
            V020 o = (V020)0;
            UNITY_SETUP_INSTANCE_ID(a);
            UNITY_TRANSFER_INSTANCE_ID(a,o);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            float3 p,n; Deform020(a,p,n);
            o.positionWS = TransformObjectToWorld(p);
            o.positionCS = TransformWorldToHClip(o.positionWS);
            o.normalWS = TransformObjectToWorldNormal(n);
            o.color = a.color;
            o.exposurePool = a.exposurePool;
            o.fog = ComputeFogFactor(o.positionCS.z);
            #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                o.shadowCoord = ComputeScreenPos(o.positionCS);
            #else
                o.shadowCoord = TransformWorldToShadowCoord(o.positionWS);
            #endif
            OUTPUT_LIGHTMAP_UV(a.lightmap,unity_LightmapST,o.lightmapUV);
            OUTPUT_SH(o.normalWS,o.vertexSH);
            o.vertexLighting = VertexLighting(o.positionWS,o.normalWS);
            return o;
        }
        // x=dry smoothness, y=wet smoothness, z=metallic, w=wet darkening.
        half4 Profile020(uint id)
        {
            if(id==1u) return half4(.17,.35,0,.78);
            if(id==2u) return half4(.28,.56,0,.82);
            if(id==3u) return half4(.36,.52,.9,.92);
            if(id==4u) return half4(.40,.56,.9,.92);
            if(id==5u) return half4(.20,.36,0,.86);
            if(id==6u) return half4(.10,.18,0,.86);
            if(id==7u) return half4(.80,.85,0,.90);
            if(id==8u) return half4(.70,.78,0,.96);
            if(id==9u) return half4(.15,.15,0,1);
            if(id==10u) return half4(.55,.63,0,1);
            return half4(.18,.55,0,.78);
        }
        float Hash020(float2 p)
        {
            float3 q=frac(float3(p.xyx)*.1031);
            q+=dot(q,q.yzx+33.33);
            return frac((q.x+q.y)*q.z);
        }
        float Patch020(float2 p)
        {
            float2 c=floor(p),f=frac(p); f=f*f*(3-2*f);
            return lerp(lerp(Hash020(c),Hash020(c+float2(1,0)),f.x),
                        lerp(Hash020(c+float2(0,1)),Hash020(c+1),f.x),f.y);
        }
        half Shine020()
        {
            return saturate(_Reflections020)*lerp(1.0h,saturate(_OracleReflectionStrength020),saturate(_ReflectionPreferenceWeight020));
        }
        half3 Direct020(BRDFData b, Light light, half3 normal, half3 view)
        {
            half3 response=b.diffuse + b.specular*DirectBRDFSpecular(b,normal,light.direction,view)*Shine020();
            return response*light.color*(light.distanceAttenuation*light.shadowAttenuation*saturate(dot(normal,light.direction)));
        }
        half4 Fragment020(V020 i) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(i);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
            half3 normal=NormalizeNormalPerPixel(i.normalWS);
            half3 view=GetWorldSpaceNormalizeViewDir(i.positionWS);
            uint renderingLayers=GetMeshRenderingLayer();
            uint id=(uint)round(i.color.a*16.0h);
            half4 profile=Profile020(id);
            float patch=Patch020(i.positionWS.xz*2.7+i.positionWS.y*.31);
            half coverage=saturate(_Wetness020)*saturate(i.exposurePool.x)*lerp(.42h,1.0h,patch);
            coverage*=lerp(.55h,1.0h,saturate(normal.y));
            if(id==7u) coverage=max(coverage,saturate(_Wetness020)*saturate(i.exposurePool.y));
            half3 albedo=i.color.rgb*lerp(1.0h,profile.w,coverage);
            half smoothness=lerp(profile.x,profile.y,coverage);
            half alpha=1;
            BRDFData b;
            InitializeBRDFData(albedo,profile.z,half3(0,0,0),smoothness,alpha,b);
            half3 diffuseGI=SAMPLE_GI(i.lightmapUV,i.vertexSH,normal);
            half4 shadowMask=SAMPLE_SHADOWMASK(i.lightmapUV);
            Light main=GetMainLight(i.shadowCoord,i.positionWS,shadowMask);
            MixRealtimeAndBakedGI(main,normal,diffuseGI);
            half shine=Shine020();
            half3 reflected=0;
            [branch] if(shine>0)
                reflected=GlossyEnvironmentReflection(reflect(-view,normal),i.positionWS,b.perceptualRoughness,1,GetNormalizedScreenSpaceUV(i.positionCS));
            half fresnel=Pow4(1-saturate(dot(normal,view)));
            half3 result=EnvironmentBRDF(b,diffuseGI,reflected*shine,fresnel);
            #ifdef _LIGHT_LAYERS
            if(IsMatchingLightLayer(main.layerMask,renderingLayers))
            #endif
                result+=Direct020(b,main,normal,view);
            #if defined(_ADDITIONAL_LIGHTS)
                InputData inputData=(InputData)0;
                inputData.positionWS=i.positionWS;
                inputData.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);
                #if USE_CLUSTER_LIGHT_LOOP
                    UNITY_LOOP for(uint lightIndex=0;lightIndex<min(URP_FP_DIRECTIONAL_LIGHTS_COUNT,MAX_VISIBLE_LIGHTS);++lightIndex)
                    {
                        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
                        Light light=GetAdditionalLight(lightIndex,i.positionWS,shadowMask);
                        #ifdef _LIGHT_LAYERS
                        if(IsMatchingLightLayer(light.layerMask,renderingLayers))
                        #endif
                            result+=Direct020(b,light,normal,view);
                    }
                #endif
                uint count=GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(count)
                    Light light=GetAdditionalLight(lightIndex,i.positionWS,shadowMask);
                    #ifdef _LIGHT_LAYERS
                    if(IsMatchingLightLayer(light.layerMask,renderingLayers))
                    #endif
                        result+=Direct020(b,light,normal,view);
                LIGHT_LOOP_END
            #endif
            #if defined(_ADDITIONAL_LIGHTS_VERTEX)
                result+=i.vertexLighting*b.diffuse;
            #endif
            half emission=id==9u ? 2.5h : (id==10u ? 1.8h : 0.0h);
            result+=i.color.rgb*emission*_EmissionScale020;
            return half4(MixFog(result,i.fog),1);
        }
        struct D020 { float4 positionCS:SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };
        D020 DepthVertex020(A020 a)
        {
            UNITY_SETUP_INSTANCE_ID(a);
            D020 o=(D020)0; UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            float3 p,n; Deform020(a,p,n);
            o.positionCS=TransformObjectToHClip(p);
            return o;
        }
        D020 ShadowVertex020(A020 a)
        {
            UNITY_SETUP_INSTANCE_ID(a);
            D020 o=(D020)0; UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            float3 p,n; Deform020(a,p,n);
            float3 world=TransformObjectToWorld(p),normal=TransformObjectToWorldNormal(n);
            #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 direction=normalize(_LightPosition-world);
            #else
                float3 direction=_LightDirection;
            #endif
            o.positionCS=ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(world,normal,direction)));
            return o;
        }
        half4 DepthFragment020(D020 i):SV_Target { return 0; }
        struct N020 { float4 positionCS:SV_POSITION; half3 normalWS:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };
        N020 NormalVertex020(A020 a)
        {
            UNITY_SETUP_INSTANCE_ID(a);
            N020 o=(N020)0; UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            UNITY_TRANSFER_INSTANCE_ID(a,o);
            float3 p,n; Deform020(a,p,n);
            o.positionCS=TransformObjectToHClip(p);
            o.normalWS=TransformObjectToWorldNormal(n);
            return o;
        }
        void NormalFragment020(N020 i,out half4 normal:SV_Target0
        #ifdef _WRITE_RENDERING_LAYERS
            ,out uint layers:SV_Target1
        #endif
        )
        {
            UNITY_SETUP_INSTANCE_ID(i);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
            float3 n=NormalizeNormalPerPixel(i.normalWS);
            #if defined(_GBUFFER_NORMALS_OCT)
                float2 oct=PackNormalOctQuadEncode(n);
                normal=half4(PackFloat2To888(saturate(oct*.5+.5)),0);
            #else
                normal=half4(n,0);
            #endif
            #ifdef _WRITE_RENDERING_LAYERS
                layers=EncodeMeshRenderingLayer();
            #endif
        }
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vertex020
            #pragma fragment Fragment020
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVertex020
            #pragma fragment DepthFragment020
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex NormalVertex020
            #pragma fragment NormalFragment020
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthVertex020
            #pragma fragment DepthFragment020
            #pragma multi_compile_instancing
            ENDHLSL
        }
    }
    FallBack Off
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Cooked canonical local +Y-up metres; world directional lighting and probe reflection.
// Array Formats: One combined opaque mesh per LOD; COLOR profiles and eight float2 UV streams.
// Dependencies/Pointers: Installed URP 17.6 BRDF/shadow/probe API; held 4096-second clock, no CPU per-leaf updates.
