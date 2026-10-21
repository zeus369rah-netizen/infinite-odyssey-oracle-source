Shader "OdysseyOracle/CampaignGardenFoliage020"
{
    Properties
    {
        _WindClock("World-owned held wind phase",Float)=0
        _WindStrength("Bounded ambient wind",Range(0,1))=1
        _MotionEnabled("Reduced-motion gate",Range(0,1))=1
        _Brush0("Accepted player capsule 0: XZ, radius, strength",Vector)=(0,0,0,0)
        _Brush1("Accepted player capsule 1: XZ, radius, strength",Vector)=(0,0,0,0)
        _BrushY0("Player capsule 0: feet Y, height",Vector)=(0,0,0,0)
        _BrushY1("Player capsule 1: feet Y, height",Vector)=(0,0,0,0)
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
        Cull Off
        Pass
        {
            Name "GardenOpaqueForward"
            Tags {"LightMode"="UniversalForwardOnly"}
            ZWrite On
            ZTest LEqual
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex GardenVertex
            #pragma fragment GardenFragment
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _WindClock;
                float _WindStrength;
                float _MotionEnabled;
                float4 _Brush0;
                float4 _Brush1;
                float4 _BrushY0;
                float4 _BrushY1;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 anchor : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float4 color : COLOR;
                half fogFactor : TEXCOORD1;
            };

            float2 BrushImpulse(float2 worldXZ,float vertexY,float4 brush,float4 brushY)
            {
                float2 away=worldXZ-brush.xy;
                float capsuleRadius=clamp(brush.z,0.0,0.5);
                float capsuleHeight=max(brushY.y,2.0*capsuleRadius);
                float segmentY=clamp(vertexY,brushY.x+capsuleRadius,
                    brushY.x+capsuleHeight-capsuleRadius);
                float distanceToPlayer=length(float3(away.x,vertexY-segmentY,away.y));
                float reach=capsuleRadius+0.30;
                float influence=saturate(1.0-distanceToPlayer/max(reach,1e-4))*saturate(brush.w);
                return (away/max(length(away),1e-4))*(influence*0.035);
            }

            float2 ClampMotion(float2 motion)
            {
                float lengthSquared=dot(motion,motion);
                return lengthSquared>0.01 ? motion*(0.10*rsqrt(lengthSquared)) : motion;
            }

            Varyings GardenVertex(Attributes input)
            {
                Varyings output;
                float3 worldPosition=TransformObjectToWorld(input.positionOS);
                float3 rootWorld=TransformObjectToWorld(input.anchor.xyz);
                float phase=_WindClock+dot(rootWorld.xz,float2(0.173,0.127));
                float2 wind=float2(sin(phase),cos(phase*0.73))*saturate(_WindStrength)*0.025;
                float2 motion=ClampMotion(wind+BrushImpulse(worldPosition.xz,worldPosition.y,_Brush0,_BrushY0)+
                    BrushImpulse(worldPosition.xz,worldPosition.y,_Brush1,_BrushY1));
                worldPosition.xz+=motion*saturate(input.anchor.w)*saturate(_MotionEnabled);
                output.positionCS=TransformWorldToHClip(worldPosition);
                output.normalWS=TransformObjectToWorldNormal(input.normalOS);
                output.color=input.color;
                output.fogFactor=ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 GardenFragment(Varyings input,FRONT_FACE_TYPE facing:FRONT_FACE_SEMANTIC) : SV_Target
            {
                half3 normal=normalize(input.normalWS)*IS_FRONT_VFACE(facing,1,-1);
                Light mainLight=GetMainLight();
                half lambert=saturate(dot(normal,mainLight.direction));
                half3 illumination=SampleSH(normal)+mainLight.color*(lambert*mainLight.distanceAttenuation);
                half3 color=MixFog(input.color.rgb*illumination,input.fogFactor);
                return half4(color,1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Renderer transform is identity; vertices and clump-root anchors are world metres/Y-up from the admitted world snapshot.
// Array Formats: One64B vertex stream supplies Position/Normal/Color/UV/Anchor; uniforms are held wind phase, wind strength, reduced-motion gate, two XZ/radius/strength capsules and their feetY/height pairs.
// Dependencies/Pointers: Existing URP forward main light and SH only; opaque one-material path, no textures, compute, shadows or GPU resource allocation. Motion is per-vertex, roots use bend mask0, and total XZ offset is clamped to0.10m.
