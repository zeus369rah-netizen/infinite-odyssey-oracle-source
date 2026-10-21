Shader "OdysseyOracle/OriginalTreeLife020"
{
    Properties
    {
        _BaseColor("Original palette gain",Color)=(1,1,1,1)
        _WindClock("Region-owned held unit wind phase",Float)=0
        _WindStrength("Bounded leaf amplitude metres",Range(0,.6))=.35
        _AmbientReadability("Tree-local ambient readability candidate",Range(0,.15))=.15
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest"}
        Cull Off
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        CBUFFER_START(UnityPerMaterial)
        float4 _BaseColor;
        float _WindClock,_WindStrength,_AmbientReadability;
        CBUFFER_END
        struct Attributes {float3 positionOS:POSITION;float3 normalOS:NORMAL;float4 color:COLOR;float2 uv:TEXCOORD0;float4 anchor:TEXCOORD1;};
        float3 WindPositionWS(Attributes input)
        {
            float3 anchorWS=TransformObjectToWorld(input.anchor.xyz);
            float phase=_WindClock*6.28318530718+dot(anchorWS.xz,float2(.173,.127));
            float3 deltaOS=float3(sin(phase)*.65+sin(phase*2+.4)*.35,sin(phase)*.18,cos(phase*3)*.72);
            deltaOS*=clamp(_WindStrength,0,.6)*saturate(input.anchor.w);
            // Position is ORIGINAL position plus a transformed DIRECTION delta; translation is never applied twice.
            return TransformObjectToWorld(input.positionOS)+mul((float3x3)unity_ObjectToWorld,deltaOS);
        }
        float LeafAlpha(float2 uv)
        {
            if(uv.x<0)return 1;
            float2 cell=min(floor(saturate(uv)*8),7),p=(cell+.5)*.25-1;
            float outline=step(abs(p.x)+abs(p.y)*.6,1.05);
            float stem=step(abs(p.x),.14)*step(p.y,-.1);
            float holes=step(.5,fmod(cell.x+cell.y*3,7))+step(abs(p.x),.30);
            return saturate(max(stem,outline*step(.5,holes)));
        }
        struct DepthVaryings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
        DepthVaryings DepthVertex(Attributes input)
        {DepthVaryings output;output.positionCS=TransformWorldToHClip(WindPositionWS(input));output.uv=input.uv;return output;}
        half4 DepthFragment(DepthVaryings input):SV_Target {clip(LeafAlpha(input.uv)-.5);return 0;}
        ENDHLSL
        Pass
        {
            Name "OriginalTreeForward"
            Tags {"LightMode"="UniversalForwardOnly"}
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ForwardVertex
            #pragma fragment ForwardFragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            struct Varyings {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;half3 normalWS:TEXCOORD1;float2 uv:TEXCOORD2;half4 color:COLOR;half fog:TEXCOORD3;float4 shadow:TEXCOORD4;};
            Varyings ForwardVertex(Attributes input)
            {
                Varyings output;output.positionWS=WindPositionWS(input);output.positionCS=TransformWorldToHClip(output.positionWS);
                output.normalWS=TransformObjectToWorldNormal(input.normalOS);output.uv=input.uv;output.color=input.color*_BaseColor;output.fog=ComputeFogFactor(output.positionCS.z);
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                output.shadow=ComputeScreenPos(output.positionCS);
                #else
                output.shadow=TransformWorldToShadowCoord(output.positionWS);
                #endif
                return output;
            }
            half4 ForwardFragment(Varyings input,FRONT_FACE_TYPE facing:FRONT_FACE_SEMANTIC):SV_Target
            {
                clip(LeafAlpha(input.uv)-.5);
                half3 normal=normalize(input.normalWS)*IS_FRONT_VFACE(facing,1,-1);
                Light main=GetMainLight(input.shadow);
                half3 ambient=max(SampleSH(normal),0);
                ambient+=saturate(1-ambient)*clamp(_AmbientReadability,0,.15);
                half3 light=ambient+main.color*(saturate(dot(normal,main.direction))*main.distanceAttenuation*main.shadowAttenuation);
                return half4(MixFog(input.color.rgb*light,input.fog),1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "OriginalTreeShadow"
            Tags {"LightMode"="ShadowCaster"}
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVertex
            #pragma fragment DepthFragment
            // Only the existing main directional shadow owner is supported; no new punctual-light path.
            float3 _LightDirection;
            DepthVaryings ShadowVertex(Attributes input)
            {
                DepthVaryings output;float3 positionWS=WindPositionWS(input),normalWS=TransformObjectToWorldNormal(input.normalOS);
                normalWS=dot(normalWS,_LightDirection)<0?-normalWS:normalWS;
                output.positionCS=TransformWorldToHClip(ApplyShadowBias(positionWS,normalWS,_LightDirection));
                #if UNITY_REVERSED_Z
                output.positionCS.z=min(output.positionCS.z,output.positionCS.w*UNITY_NEAR_CLIP_VALUE);
                #else
                output.positionCS.z=max(output.positionCS.z,output.positionCS.w*UNITY_NEAR_CLIP_VALUE);
                #endif
                output.uv=input.uv;return output;
            }
            ENDHLSL
        }
        Pass
        {
            Name "OriginalTreeDepth"
            Tags {"LightMode"="DepthOnly"}
            ZWrite On ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthVertex
            #pragma fragment DepthFragment
            ENDHLSL
        }
    }
    FallBack Off
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: original object position plus transformed direction delta; fixed zero-mask trunk/roots/branches, anchored leaf cards and rigid canopy blocks. Unit-world matrices and max0.6m amplitude are lease preconditions.
// Array Formats: one64B vertex ABI, original vertex palette, one shared held clock/strength; procedural8x8 alpha mask needs no texture/image edits. Identical WindPositionWS/LeafAlpha feed Forward, ShadowCaster and DepthOnly.
// Dependencies/Pointers: existing main directional light/shadows/fog only; no new light/RT/camera/reflection or CPU leaf update. Lease expands all bounds0.8m; root imports/checks shader/LOD/shadow/bounds and actual Web rendering before acceptance.
