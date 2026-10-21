// PSEUDOCODE: use the existing held clock and supported anchors for soft2D rain; at each periodic impact animate four small closed3D droplets on a bounded ballistic arc, ease alpha to zero by.22s and retain no residue.
Shader "OdysseyOracle/PortalLandingRain020"
{
    Properties
    {
        _PresentationClock020("Held presentation clock",Float)=0
        _Mode020("Rain0 splash1",Float)=0
        _Instanced020("Native instance path",Float)=1
        _Count020("Combined prefix count",Float)=64
        _Wind020("Borrowed bounded wind",Vector)=(0,0,0,0)
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+12"}
        Pass
        {
            Name "PooledRedRainAndSmallSplash020" Tags {"LightMode"="UniversalForward"}
            Blend SrcAlpha OneMinusSrcAlpha ZWrite Off ZTest LEqual Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float _PresentationClock020,_Mode020,_Instanced020,_Count020;
            float4 _Wind020;
            CBUFFER_END
            struct A {float4 positionOS:POSITION;float2 uv:TEXCOORD0;float4 anchor:TEXCOORD1;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float phase:TEXCOORD1;float valid:TEXCOORD2;half fog:TEXCOORD3;float fade:TEXCOORD4;half shade:TEXCOORD5;};
            float Seed(float2 p){return frac(sin(dot(p,float2(5.17,9.37)))*1387.27);}
            V Vert(A a)
            {
                UNITY_SETUP_INSTANCE_ID(a);V o;
                float3 root=_Instanced020>.5?TransformObjectToWorld(float3(0,0,0)):a.anchor.xyz;
                float valid=_Instanced020>.5?step(-128,root.y):step(.5,a.anchor.w)*step(a.anchor.w,_Count020+.5);
                bool clockValid=_PresentationClock020>=0&&_PresentationClock020<4096;
                valid*=clockValid?1:0;
                float seed=Seed(root.xz),phase=frac((clockValid?_PresentationClock020:0)+seed);
                float3 world;o.shade=1;
                if(_Mode020<.5)
                {
                    float height=5*(1-phase);
                    float3 right=float3(UNITY_MATRIX_I_V._m00,0,UNITY_MATRIX_I_V._m20);
                    float rightSquare=dot(right,right);
                    right=rightSquare>1e-8?right*rsqrt(max(rightSquare,1e-8)):float3(1,0,0);
                    world=root+float3(_Wind020.x,0,_Wind020.y)*(height/5);
                    world+=right*((a.uv.x-.5)*lerp(.020,.028,seed))+float3(0,height+a.uv.y*lerp(.22,.42,seed),0);
                }
                else
                {
                    // Four closed tetrahedra move in world3D, never a flattened floor ring/decal.
                    float life=saturate(phase/.22);
                    valid*=step(phase,.22);
                    float angle=seed*6.28318530718,s,c;sincos(angle,s,c);
                    float2 direction=float2(a.uv.x*c-a.uv.y*s,a.uv.x*s+a.uv.y*c);
                    float3 radial=float3(direction.x,0,direction.y),tangent=float3(-direction.y,0,direction.x);
                    float size=lerp(.022,.012,life),spread=lerp(.018,.108,life);
                    float height=.020+.44*life*(1-life);
                    world=root+radial*spread+float3(0,height,0)+(tangent*a.positionOS.x+float3(0,a.positionOS.y,0)+radial*a.positionOS.z)*size;
                    o.shade=saturate(.72+a.positionOS.y*.20-a.positionOS.z*.10);
                }
                float3 relative=world-GetCameraPositionWS();float front=-TransformWorldToView(world).z;
                o.fade=saturate((12-length(relative))*.5)*smoothstep(.3,1,front);
                valid*=step(.001,o.fade);
                o.positionCS=TransformWorldToHClip(world);o.uv=a.uv;o.phase=phase;o.valid=valid;o.fog=ComputeFogFactor(o.positionCS.z);
                if(valid<.5)o.positionCS=float4(2,2,2,1);
                return o;
            }
            half4 Frag(V i):SV_Target
            {
                clip(i.valid-.5);float alpha;
                if(_Mode020<.5)
                {
                    float edge=1-smoothstep(.15,1,abs(i.uv.x*2-1));
                    float tail=1-smoothstep(.02,1,i.uv.y);
                    alpha=edge*tail*.28*smoothstep(0,.025,i.phase)*(1-smoothstep(.98,1,i.phase));
                }
                else
                {
                    float life=saturate(i.phase/.22);
                    alpha=.46*smoothstep(0,.08,life)*(1-smoothstep(.40,1,life));
                }
                return half4(MixFog(half3(.56,.025,.055)*i.shade,i.fog),saturate(alpha*i.fade));
            }
            ENDHLSL
        }
    }
    Fallback Off
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Builder-supported floor+.025m anchors; soft2Dstreaks rise<=5.42m with<=.28m drift. Four closed world3D tetrahedral droplets/splash remain within.14m radius/<.18m above floor and fade by.22s; no floor ring/pool/residue. Positive camera depth/12m fade retained.
// Array Formats:64/16maximum native matrix prefixes or two fixed Vertex36 buffers (256/256vertices),same shader/material/two cached blocks; 4rain vertices or16splash vertices per anchor. No full-world weather,particle collision,history/decal/pool or depth texture.
// Dependencies/Pointers: Private held clock supplies one-second rain/splash cycles; reused world wind is clamped by owner. Native writer completes before draw. Unsupported hardware uses combined mesh; unsupported shader omits weather and reports failure, never gameplay authority.
