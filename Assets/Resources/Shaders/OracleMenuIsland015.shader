// # ARCHITECTURE SNAPSHOT
// Coordinate Space: immutable catalog-local vertex positions to private preview stage/clip space.
// Array Formats: bounded POSITION/NORMAL/COLOR/TEXCOORD streams; private scalar mode/day/clock, no textures or dynamic buffers.
// Dependencies/Pointers: original first-party menu-only shader; no global time, weather, room state or gameplay material mutation.
Shader "OdysseyOracle/MenuIsland015"
{
    Properties
    {
        _Mode("Rock0 Cloud1 Rain2",Float)=0
        _Daylight("Private daylight",Range(0,1))=1
        _LocalClock("Private presentation clock",Float)=0
        _SrcBlend("Source blend",Float)=1
        _DstBlend("Destination blend",Float)=0
        _ZWrite("Depth write",Float)=1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "OriginalMenuIsland015"
            Tags { "LightMode"="UniversalForwardOnly" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull Back
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex SceneVertex
            #pragma fragment SceneFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float _Mode,_Daylight,_LocalClock,_SrcBlend,_DstBlend,_ZWrite;
            CBUFFER_END
            struct Input { float4 positionOS:POSITION; float3 normalOS:NORMAL; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct Varying { float4 positionCS:SV_POSITION; half3 normal:TEXCOORD0; half4 color:COLOR; };
            Varying SceneVertex(Input input)
            {
                Varying output;
                float3 p=input.positionOS.xyz;
                if(_Mode>1.5)
                {
                    float start=input.uv.x;
                    p.y+=frac((start-max(0,_LocalClock)*9.0)/40.0)*40.0-start;
                }
                else if(_Mode>.5)p.x+=sin(max(0,_LocalClock)*.0523598776)*3.0;
                output.positionCS=TransformObjectToHClip(p);
                output.normal=input.normalOS;
                output.color=input.color;
                return output;
            }
            half4 SceneFragment(Varying input):SV_Target
            {
                half day=saturate(_Daylight);
                half3 normal=normalize(input.normal);
                half face=.42h+.58h*saturate(dot(normal,normalize(half3(-.4h,.8h,-.3h))));
                half3 tint=lerp(half3(.32h,.43h,.64h),half3(1.05h,1.03h,.96h),day);
                half3 color=input.color.rgb*tint*face;
                if(_Mode>1.5)return half4(input.color.rgb*lerp(.58h,.9h,day),.20h);
                if(_Mode>.5)color=lerp(color,color+lerp(half3(.055h,.075h,.12h),half3(.14h,.17h,.17h),day),.65h);
                return half4(color,1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: local Y-up geometry; explicit scalar clock animates bounded clouds/rain without global time or vertex-buffer writes.
// Array Formats: one fixed vertex stream per combined mesh, private UnityPerMaterial constants; opaque rock/cloud and scoped low-alpha rain.
// Dependencies/Pointers: cold C# owner supplies day/night and clock, grows conservative motion bounds, suppresses rain on Low. No screen-space image, texture/license download, GPU compute requirement, shadow/sky/global-state claim. Root Unity/Web shader compilation and visual tests pending.
