Shader "Oracle/UI/CampaignBook020"
{
    Properties
    {
        [PerRendererData] _MainTex ("Original page image", 2D) = "white" {}
        _Color ("UI tint", Color) = (1,1,1,1)
        _Progress ("Drawing progress", Range(0,1)) = 0
        _ColorProgress ("Original color progress", Range(0,1)) = 0
        _PageAlpha ("Page alpha", Range(0,1)) = 0
        _PaperColor ("Muted papyrus", Color) = (0.86,0.82,0.70,1)
        _InkColor ("Warm etched ink", Color) = (0.16,0.14,0.095,1)

        [HideInInspector] _StencilComp ("Stencil comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil reference", Float) = 0
        [HideInInspector] _StencilOp ("Stencil operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil write mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil read mask", Float) = 255
        [HideInInspector] _ColorMask ("Color mask", Float) = 15
        [HideInInspector] _ClipRect ("Canvas clip rectangle", Vector) = (-32767,-32767,32767,32767)
        [HideInInspector] _UIMaskSoftnessX ("Canvas clip softness X", Float) = 0
        [HideInInspector] _UIMaskSoftnessY ("Canvas clip softness Y", Float) = 0
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use UI alpha clip", Float) = 0
    }
    SubShader
    {
        Tags
        {
            "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent"
            "PreviewType"="Plane" "CanUseSpriteAtlas"="True"
        }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "CampaignPage"
            Tags { "LightMode"="SRPDefaultUnlit" }
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST, _MainTex_TexelSize, _ClipRect;
            half4 _Color, _TextureSampleAdd, _PaperColor, _InkColor;
            float _Progress, _ColorProgress, _PageAlpha;
            float _UIMaskSoftnessX, _UIMaskSoftnessY;

            struct Attributes
            {
                float4 position : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 position : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 page : TEXCOORD1;
                float4 mask : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.position=UnityObjectToClipPos(input.position);
                output.color=input.color*_Color;
                output.uv=TRANSFORM_TEX(input.uv,_MainTex);
                output.page=input.uv;
                output.mask=0;
                #ifdef UNITY_UI_CLIP_RECT
                    float4 bounds=clamp(_ClipRect,-1000000.0,1000000.0);
                    float2 projection=abs(mul((float2x2)UNITY_MATRIX_P,_ScreenParams.xy));
                    float2 halfPixel=abs(output.position.w)/max(projection,float2(0.00001,0.00001));
                    float2 softness=max(float2(_UIMaskSoftnessX,_UIMaskSoftnessY),0);
                    output.mask=float4(input.position.xy*2-bounds.xy-bounds.zw,
                        0.25/(0.25*softness+max(halfPixel,float2(0.00001,0.00001))));
                #endif
                return output;
            }

            float Grain(float2 sampleAt)
            {
                float2 value=frac(sampleAt*float2(0.1137,0.1379));
                value+=dot(value,value.yx+19.19);
                return frac((value.x+value.y)*value.x);
            }

            float PaperField(float2 sampleAt)
            {
                float2 cell=floor(sampleAt), part=frac(sampleAt);
                part=part*part*(3-2*part);
                float low=lerp(Grain(cell),Grain(cell+float2(1,0)),part.x);
                float high=lerp(Grain(cell+float2(0,1)),Grain(cell+float2(1,1)),part.x);
                return lerp(low,high,part.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 image=tex2D(_MainTex,input.uv)+_TextureSampleAdd;
                half3 rgb=image.rgb;
                // Uniform completed-page path skips paper/ink ALU during reading.
                UNITY_BRANCH if(_Progress<1 || _ColorProgress<1)
                {
                    float2 page=saturate(input.page);
                    float field=PaperField(page*float2(18,12));
                    float grain=Grain(floor(page*float2(520,340)));
                    half3 paper=_PaperColor.rgb*(0.98+0.04*(0.7*field+0.3*grain));

                    float luminance=saturate(dot(image.rgb,float3(0.2126,0.7152,0.0722)));
                    float2 stepUv=_MainTex_TexelSize.xy*1.35;
                    float nextX=dot(tex2D(_MainTex,input.uv+float2(stepUv.x,0)).rgb,float3(0.2126,0.7152,0.0722));
                    float nextY=dot(tex2D(_MainTex,input.uv+float2(0,stepUv.y)).rgb,float3(0.2126,0.7152,0.0722));
                    float edge=saturate((abs(nextX-luminance)+abs(nextY-luminance))*7);
                    float shade=floor((1-luminance)*6)/6;
                    float density=saturate(0.30*shade+0.70*edge+(grain-0.5)*0.025);
                    half3 ink=lerp(paper,_InkColor.rgb,density);

                    float color=saturate(_ColorProgress);
                    color=color*color*(3-2*color);
                    half3 illustration=lerp(ink,image.rgb,color);
                    float order=dot(float2(page.x,1-page.y),float2(0.38,0.62))+0.035*(field-0.5);
                    float front=lerp(-0.14,1.14,saturate(_Progress));
                    float drawing=smoothstep(order-0.085,order+0.085,front);
                    rgb=lerp(paper,illustration,drawing);
                }
                rgb*=input.color.rgb;
                half alpha=saturate(image.a*input.color.a*saturate(_PageAlpha));

                #ifdef UNITY_UI_CLIP_RECT
                    float4 bounds=clamp(_ClipRect,-1000000.0,1000000.0);
                    float2 coverage=saturate((bounds.zw-bounds.xy-abs(input.mask.xy))*input.mask.zw);
                    alpha*=coverage.x*coverage.y;
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                    clip(alpha-0.001);
                #endif
                return half4(rgb,alpha);
            }
            ENDCG
        }
    }
    Fallback Off
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing Canvas-overlay RawImage vertices and original fitted image UVs; stable normalized-page paper/ink field, no deformation or texture edits.
// Array Formats: One texture sample for completed pages, three during the bounded paper/edge/ink reveal; three independent progress/alpha scalars, two cold style colors and standard uGUI uniforms; no clock, buffers, derivatives or offscreen target.
// Dependencies/Pointers: UnityCG/UnityUI and installed uGUI masking contract. Guide owns one cold material/effective stencil copy and held scalar updates; root owns actual import, masking, endpoint, WebGL and frame acceptance.
