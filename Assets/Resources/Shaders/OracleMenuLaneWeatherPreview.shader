Shader "OdysseyOracle/MenuLaneWeatherPreview"
{
    Properties
    {
        _Mode("Canopy0 Lava1 Steam2 Portal3", Float) = 0
        _BaseColor("Original base tint", Color) = (1,1,1,1)
        _PreviewClock("Private preview clock", Float) = 0
        _SrcBlend("Source blend", Float) = 1
        _DstBlend("Destination blend", Float) = 0
        _ZWrite("Depth write", Float) = 1
        _Cull("Cull mode", Float) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "MenuLaneWeather"
            Tags { "LightMode"="UniversalForwardOnly" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex PreviewVertex
            #pragma fragment PreviewFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float _Mode, _PreviewClock, _SrcBlend, _DstBlend, _ZWrite, _Cull;
            CBUFFER_END

            struct PreviewInput
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };
            struct PreviewVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 arenaPosition : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float4 color : COLOR;
            };
            // Same project-authored cell recipe as the original lane material,
            // sampled in the baked arena space instead of the displaced stage.
            float PreviewNoise(float2 cell)
            {
                float2 p=frac(cell*float2(.1031,.11369));
                p+=dot(p,p.yx+19.19);
                return frac(p.x*p.y);
            }
            PreviewVaryings PreviewVertex(PreviewInput input)
            {
                PreviewVaryings output;
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                output.arenaPosition=input.positionOS.xyz;
                output.normalWS=TransformObjectToWorldNormal(input.normalOS);
                output.uv=input.uv;
                output.color=input.color;
                return output;
            }
            half4 PreviewFragment(PreviewVaryings input) : SV_Target
            {
                float clock=max(0,_PreviewClock);
                if(_Mode>2.5)
                {
                    // Original portal UV is a signed radial plane, not a0..1 card.
                    float2 p=input.uv;float radius=max(length(p),.0001);
                    float angle=atan2(p.y,p.x);
                    float tooth=pow(saturate(.5+.5*sin(angle*23+sin(angle*5-clock*.37)*.75)),6);
                    float skin=.925+tooth*.068+.014*sin(angle*7-clock*.52);
                    float aa=max(fwidth(radius),.0015);
                    float liquid=1-smoothstep(skin-aa,skin+.022+aa,radius);
                    float outerFade=1-smoothstep(1.075,1.22,radius);
                    float core=1-smoothstep(.25,.51,radius);
                    float curl=angle+1.55/(radius+.28)-clock*.22;
                    float flow=sin(radius*48+sin(curl*3)*1.25-clock*1.35);
                    float ridge=pow(saturate(flow*.5+.5),8)*smoothstep(.31,.65,radius);
                    float shine=pow(saturate(.5+.5*sin(curl*5+radius*15)),12);
                    float narrow=abs(radius-skin);
                    float rim=1-smoothstep(.003+aa,.027+aa,narrow);
                    float ring=(1-smoothstep(.002+aa,.013+aa,abs(radius-(.69+.014*sin(curl*4)))))*.24;
                    float trailing=(1-smoothstep(.015,.17,narrow))*.11;
                    float3 dark=(float3(.0025,.003,.008)+ridge*float3(.018,.014,.031)+shine*ridge*float3(.065,.032,.095))*(1-core*.91);
                    float3 glow=_BaseColor.rgb*(rim*(.65+tooth*.40)+ring+trailing);
                    float alpha=saturate(liquid+rim*.75+ring*.5+trailing*.85)*outerFade;
                    return half4(dark+glow,saturate(alpha));
                }
                if(_Mode>1.5)
                {
                    float noise=PreviewNoise(floor(float2(input.uv.x*5,input.uv.y*6-clock*.35)));
                    float edge=saturate(1-abs(input.uv.x-.5)*2);
                    float fade=saturate(1-input.uv.y)*saturate(input.uv.y*5);
                    return half4(.54,.61,.64,edge*edge*fade*(.10+.12*noise));
                }
                if(_Mode>.5)
                {
                    float2 position=input.arenaPosition.xz;
                    float cell=PreviewNoise(floor(position*1.85+float2(clock*.07,clock*.10)));
                    float cracks=1-smoothstep(.08,.35,abs(frac(position.x*.38+position.y*.27+clock*.035+cell*.18)-.5));
                    float pulse=.92+.08*sin(clock*.62+cell*6);
                    return half4(lerp(half3(1.15,.10,.008),half3(3.1,.94,.055),cracks)*pulse,1);
                }
                float grain=.94+.12*PreviewNoise(floor(input.arenaPosition.xz*3)+floor(input.arenaPosition.y*3));
                half3 albedo=input.color.rgb*_BaseColor.rgb*grain;
                half3 normal=normalize(input.normalWS);
                Light moon=GetMainLight();
                half wrap=.38+.62*saturate(dot(normal,moon.direction));
                half3 illumination=max(SampleSH(normal),half3(.16,.20,.19))+moon.color*wrap;
                return half4(albedo*illumination+albedo*input.color.a*.70,1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: COLOR/UV and arena-local baked metres/Y-up; only clip transform includes isolated stage position. Static canopies, no anchor/wind displacement.
// Array Formats: Existing vertex POSITION/NORMAL/COLOR/TEXCOORD0 streams; mode0/1/2/3 and one explicit scalar preview clock per cached MPB. Portal UV is signed radial-plane data.
// Dependencies/Pointers: Original project lane tint/lava/steam and portal radial-core/rim math adapted for one scoped saved preview shader. No portal vertex expansion/distortion, global time/weather/sky writes, gameplay pointers, source material mutation or native visual acceptance.
