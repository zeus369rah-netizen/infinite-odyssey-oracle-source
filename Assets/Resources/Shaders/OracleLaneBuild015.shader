Shader "OdysseyOracle/OriginalLaneBuild015"
{
    Properties { _Tint("Tint",Color)=(.2,1,.3,.8) _Mode("Marker mode",Float)=0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _Tint;float _Mode;
            CBUFFER_END
            struct In { float3 position:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Out { float4 position:SV_POSITION;float2 uv:TEXCOORD0; };
            Out Vert(In v){UNITY_SETUP_INSTANCE_ID(v);Out o;o.position=TransformObjectToHClip(v.position);o.uv=v.uv;return o;}
            half4 Frag(Out i):SV_Target
            {
                float2 p=i.uv-.5;float rim=max(abs(p.x),abs(p.y));
                clip(.49-rim);
                float edge=step(.40,rim);
                float fill=_Mode<.5?.18:(_Mode>1.5?.25:.22);
                return half4(_Tint.rgb,_Tint.a*lerp(fill,1,edge));
            }
            ENDHLSL
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing instance world matrix to URP clip; one filled and outlined cosmetic floor block, no physical support change.
// Array Formats: Saved4vertex/6index quad, standard instanced64B matrices and cached uniform tint/mode; target3.5, WebGL2-compatible no compute/structured buffer.
// Dependencies/Pointers: Borrowed URP Core and Builder-owned material/mesh, standard engine draw lifetime; shader/native/browser visual acceptance pending root.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing accepted metre/Y-up/XZ support roots and HUD Canvas; cosmetic floor boxes do not establish build permission.
// Array Formats: Existing fixed8192 preview records, one ray job, four cached original tower choices, unchanged Frame64 pending/replay transaction.
// Dependencies/Pointers: Local picker owns no pause/cursor/economy; accepted host result alone closes successful transaction. Normal input/native/Web rendering remain separate acceptance gates.
