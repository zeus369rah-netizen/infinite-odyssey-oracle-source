Shader "OdysseyOracle/LaneWeather"
{
    Properties
    {
        _Mode("Canopy0 Lava1 Steam2", Float) = 0
        _BaseColor("Base", Color) = (1,1,1,1)
        _SrcBlend("Source blend", Float) = 1
        _DstBlend("Destination blend", Float) = 0
        _ZWrite("Depth write", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        CBUFFER_START(UnityPerMaterial)
        float4 _BaseColor;
        float _Mode, _SrcBlend, _DstBlend, _ZWrite;
        CBUFFER_END
        // DirectionXZ, strength0..1, motionEnabled0..1. Applied on scene/settings changes only.
        float4 _OracleLaneWeather;
        float3 _LightDirection, _LightPosition;
        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
            float4 anchor : TEXCOORD1; // rootWorldX,rootWorldY,rootWorldZ,treeHeight
            float4 color : COLOR;
        };
        float Hash(float2 p)
        { p=frac(p*float2(.1031,.11369)); p+=dot(p,p.yx+19.19); return frac(p.x*p.y); }
        float3 WeatherPosition(float3 world, float4 anchor)
        {
            if (_Mode > .5 || anchor.w <= 0) return world;
            float t=_Time.y*saturate(_OracleLaneWeather.w);
            float weight=anchor.w<2 ? saturate((world.y-anchor.y)/anchor.w)
                : saturate((world.y-anchor.y-anchor.w*.40)/(anchor.w*.60));
            float phase=Hash(anchor.xz)*6.2831853;
            float gust=.72+.28*sin(t*.17+phase);
            float wave=sin(t*.64+phase)*.76+sin(t*.31+phase*1.7)*.24;
            float maxOffset=anchor.w<2 ? .08 : .72;
            float2 direction=_OracleLaneWeather.xy*rsqrt(max(dot(_OracleLaneWeather.xy,_OracleLaneWeather.xy),.0001));
            world.xz+=direction*wave*gust*saturate(_OracleLaneWeather.z)*saturate(_OracleLaneWeather.w)*maxOffset*weight*weight;
            return world;
        }
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            float3 normalWS : TEXCOORD1;
            float2 uv : TEXCOORD2;
            float4 color : COLOR;
            float4 shadowCoord : TEXCOORD3;
        };
        Varyings Vert(Attributes input)
        {
            Varyings output;
            output.positionWS=WeatherPosition(TransformObjectToWorld(input.positionOS.xyz),input.anchor);
            output.positionCS=TransformWorldToHClip(output.positionWS);
            output.normalWS=TransformObjectToWorldNormal(input.normalOS);
            output.uv=input.uv; output.color=input.color;
            VertexPositionInputs shadowInput=(VertexPositionInputs)0;
            shadowInput.positionWS=output.positionWS;shadowInput.positionCS=output.positionCS;
            output.shadowCoord=GetShadowCoord(shadowInput);
            return output;
        }
        half4 Frag(Varyings input) : SV_Target
        {
            float t=_Time.y*saturate(_OracleLaneWeather.w);
            if (_Mode>1.5)
            {
                float2 uv=input.uv;
                float noise=Hash(floor(float2(uv.x*5,uv.y*6-t*.35)));
                float edge=saturate(1-abs(uv.x-.5)*2);
                float fade=saturate(1-uv.y)*saturate(uv.y*5);
                float alpha=edge*edge*fade*(.10+.12*noise);
                return half4(.54,.61,.64,alpha);
            }
            if (_Mode>.5)
            {
                float2 p=input.positionWS.xz;
                float cell=Hash(floor(p*1.85+float2(t*.07,t*.10)));
                float cracks=1-smoothstep(.08,.35,abs(frac(p.x*.38+p.y*.27+t*.035+cell*.18)-.5));
                float pulse=.92+.08*sin(t*.62+cell*6);
                half3 lava=lerp(half3(1.15,.10,.008),half3(3.1,.94,.055),cracks)*pulse;
                return half4(lava,1);
            }
            float grain=.94+.12*Hash(floor(input.positionWS.xz*3.0)+floor(input.positionWS.y*3.0));
            half3 albedo=input.color.rgb*grain*_BaseColor.rgb;
            half3 normal=normalize(input.normalWS);
            #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
            Light moon=GetMainLight(input.shadowCoord);
            #else
            // Cascade selection must remain per fragment on these broad moving crowns.
            Light moon=GetMainLight(TransformWorldToShadowCoord(input.positionWS));
            #endif
            half wrap=.38+.62*saturate(dot(normal,moon.direction));
            half3 illumination=max(SampleSH(normal),half3(.16,.20,.19))+moon.color*wrap*moon.shadowAttenuation;
            return half4(albedo*illumination+albedo*input.color.a*.70,1);
        }
        float4 ShadowVert(Attributes input) : SV_POSITION
        {
            float3 world=WeatherPosition(TransformObjectToWorld(input.positionOS.xyz),input.anchor);
            float3 normal=TransformObjectToWorldNormal(input.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
            float3 direction=normalize(_LightPosition-world);
            #else
            float3 direction=_LightDirection;
            #endif
            return ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(world,normal,direction)));
        }
        half4 ShadowFrag() : SV_Target { clip(.5-_Mode); return 0; }
        // The same displaced Vert supplies camera depth and normal prepasses. Transparent
        // steam never contributes; opaque lava and canopies remain depth-consistent.
        half4 DepthFrag(Varyings input) : SV_Target { clip(1.5-_Mode); return 0; }
        half4 DepthNormalsFrag(Varyings input) : SV_Target
        {
            clip(1.5-_Mode);
            float3 normal=normalize(input.normalWS);
            #if defined(_GBUFFER_NORMALS_OCT)
            float2 octNormal=PackNormalOctQuadEncode(normal);
            return half4(PackFloat2To888(saturate(octNormal*.5+.5)),0);
            #else
            return half4(normal,0);
            #endif
        }
        ENDHLSL
        Pass
        {
            Name "LaneForward"
            Tags { "LightMode"="UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull Back
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull Back
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R Cull Back
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment DepthFrag
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            ZWrite On Cull Back
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment DepthNormalsFrag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            ENDHLSL
        }
    }
    FallBack Off
}
