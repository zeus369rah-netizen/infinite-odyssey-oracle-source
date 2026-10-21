Shader "Oracle/Presentation/WindowShaft020"
{
    Properties
    {
        _BaseColor ("Window light", Color) = (0.25,0.55,0.9,0.045)
        _WindowCenters020 ("Actual window centres", Vector) = (-3,8,18,0)
        _WindowAperture020 ("Clear half-width, bottom, top", Vector) = (1.74,1.76,7.64,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+25" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off ZTest LEqual Cull Back
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "../../Oracle/Presentation/Shaders/WorldWind015.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _WindowCenters020;
                float4 _WindowAperture020;
            CBUFFER_END
            float _OracleSoftWindowShadows020;
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 local : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                output.local=input.positionOS.xyz;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float center=_WindowCenters020.x;
                if(abs(input.local.z-_WindowCenters020.y)<abs(input.local.z-center))center=_WindowCenters020.y;
                if(abs(input.local.z-_WindowCenters020.z)<abs(input.local.z-center))center=_WindowCenters020.z;
                float along=saturate((7.1-input.local.y)/7.065);
                float across=(input.local.z-center)/lerp(1.35,1.7,along);
                float edge=1-smoothstep(.82,1.02,abs(across));
                float ends=smoothstep(0,.08,along)*(1-smoothstep(.90,1,along));
                float mask=1,gain=1;
                UNITY_BRANCH if(_OracleSoftWindowShadows020>.5)
                {
                    float theta=_OraclePassiveWind015.z*(6.28318530718/256.0);
                    float drift=.025*sin(17*theta+center*.2);
                    float u=across*_WindowAperture020.x+drift;
                    float v=lerp(_WindowAperture020.z,_WindowAperture020.y,along);
                    float bar=-1.5+clamp(round((u+1.5)/.6),0,5)*.6;
                    float rail=2.35+clamp(round((v-2.35)/1.15),0,4)*1.15;
                    float feather=clamp(.018+along*.025+max(fwidth(u),fwidth(v))*.7,.012,.08);
                    mask=smoothstep(.05,.05+feather,abs(u-bar))*smoothstep(.05,.05+feather,abs(v-rail));
                    mask*=1-smoothstep(_WindowAperture020.x-feather,_WindowAperture020.x,abs(u));
                    gain=.97+.03*sin(7*theta+along*2);
                }
                return half4(_BaseColor.rgb*gain,_BaseColor.a*edge*ends*mask);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing three RoomV2-local widening beam planes and actual grille/aperture coordinates; stylized atmospheric mask, physical bar shadows remain main-light geometry.
// Array Formats: Existing beam mesh/material batch plus two cold float4 window constants and one global mode scalar; fragment-only analytic math, no textures/compute/render features/CPU ticks.
// Dependencies/Pointers: Existing World owns mesh/material and held passive-wind clock; GraphicsPreferences owns mode. CullBack matches original double-winding indices; WebGL2/native shader/render/performance acceptance remains root-owned.
