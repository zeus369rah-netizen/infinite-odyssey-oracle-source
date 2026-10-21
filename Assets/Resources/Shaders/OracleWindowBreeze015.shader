Shader "Oracle/Presentation/WindowBreeze015"
{
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent"}
        Pass
        {
            Tags {"LightMode"="SRPDefaultUnlit"}
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off ZWrite Off ZTest LEqual
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "../../Oracle/Presentation/Shaders/WorldWind015.hlsl"
            struct Attributes {float3 p:POSITION;float2 corner:TEXCOORD0;float3 seed:TEXCOORD1;};
            struct Varyings {float4 p:SV_POSITION;float2 uv:TEXCOORD0;float alpha:TEXCOORD1;};
            Varyings Vert(Attributes v)
            {
                Varyings o;float2 direction;float speed,time,gain;WindField015(direction,speed,time,gain);
                float3 localDirection=mul((float3x3)GetWorldToObjectMatrix(),float3(direction.x,0,direction.y));
                float signedFlow=-localDirection.x,life=frac(time*.25+v.seed.x),inside=signedFlow>=0?life:1-life;
                float3 center=v.p+float3(-2*inside,.04*sin(life*6.2831853+v.seed.y),clamp(localDirection.z,-1,1)*.18*inside);
                float3 world=TransformObjectToWorld(center),flow=float3(direction.x,0,direction.y);
                float3 eye=SafeNormalize(_WorldSpaceCameraPos-world),side=SafeNormalize(cross(eye,flow));
                world+=flow*(v.corner.y*.18*v.seed.z)+side*(v.corner.x*.014);
                o.p=TransformWorldToHClip(world);o.uv=v.corner;
                float pulse=sin(3.14159265359*life);
                o.alpha=.060*gain*saturate(speed)*smoothstep(.08,.40,abs(signedFlow))*pulse*pulse;return o;
            }
            half4 Frag(Varyings v):SV_Target
            {
                float edge=saturate(1-abs(v.uv.x));edge*=edge;
                float tip=saturate(1-abs(v.uv.y));return half4(.68,.79,.87,v.alpha*edge*tip);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: actual clear-grille window seeds in supported refuge local space; signed world breeze converts into local inward/outward2m trails, world-facing thin streaks.
// Array Formats: one immutable144vertex/216index mesh and one shared float4 field; fixed36 quads, no spawn, upload, texture, fog, compute or per-window objects.
// Dependencies/Pointers: original Core vertex math and existing batch/material lifetime; low alpha/depth test/highY near-window bounds avoid room veils. Root/wall/door/grille collision and lights unchanged. WebGL/native shader/culling/visual acceptance pending.
