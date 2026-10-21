Shader "Oracle/Presentation/RaykonRigidCandidate"
{
    Properties
    {
        _BaseColor("Borrowed palette multiplier",Color)=(1,1,1,1)
        _Brew0("Brew warm red",Color)=(1,.22,.10,1)
        _Brew1("Brew living green",Color)=(.12,.85,.32,1)
        _Brew2("Brew azure",Color)=(.16,.52,1,1)
        _Brew3("Brew violet",Color)=(.76,.20,1,1)
        _BrewCycleSeconds("Gentle palette cycle seconds",Range(12,32))=16
        _BrewEmission("Brew emission (no Light)",Range(0,2))=.8
        _StaffMode("Rigid staff0/1",Float)=0
        _TalkBlend("Talk normalized progress",Range(0,1))=0
        _Speaking("Approximate greeting jaw amount",Range(0,1))=0
        _HeadAngles009("Rigid head pitch yaw radians",Vector)=(0,0,0,0)
        _RoomTorchFuel("Host torch fuel",Range(0,1))=1
        _StaffPivot("Local staff pivot",Vector)=(.5,1.28,.43,0)
        _TipAndSupport("Local support vertex/world floorY",Vector)=(0,0,0,0)
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
        Pass
        {
            Tags {"LightMode"="UniversalForwardOnly"}
            Cull Back ZWrite On ZTest LEqual
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            // Planned import path:Assets/Oracle/Presentation/Shaders/RaykonRigid.shader.
            #include "../../Foundation/Shaders/RigidAnimation.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor,_Brew0,_Brew1,_Brew2,_Brew3;
                float _BrewCycleSeconds,_BrewEmission;
                float _StaffMode,_TalkBlend,_Speaking,_RoomTorchFuel;
                float4 _StaffPivot,_TipAndSupport,_HeadAngles009;
            CBUFFER_END
            struct Attributes{float3 p:POSITION;float3 n:NORMAL;half4 color:COLOR;float2 uv:TEXCOORD0;float4 fx:TEXCOORD1;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct Varyings
            {
                float4 p:SV_POSITION;float3 n:TEXCOORD0;half4 color:COLOR;
                float glow:TEXCOORD1;float3 effectLocal:TEXCOORD2;
                float3 world:TEXCOORD3;float4 shadow:TEXCOORD4;
                #if defined(_ADDITIONAL_LIGHTS_VERTEX)
                    half3 vertexLight:TEXCOORD5;
                #endif
            };
            float3 OracleHeadRotate009(float3 p,float2 angles)
            {
                float sx,cx,sy,cy;sincos(angles.x,sx,cx);sincos(angles.y,sy,cy);
                float3 pitched=float3(p.x,cx*p.y-sx*p.z,sx*p.y+cx*p.z);
                return float3(cy*pitched.x+sy*pitched.z,pitched.y,-sy*pitched.x+cy*pitched.z);
            }
            Varyings Vert(Attributes v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                float3 local=v.p;float3 localNormal=v.n;
                if(_StaffMode>.5)
                {
                    // Clear cauldron orbit only while idle. Fully zero before the
                    // existing slam/support kernel takes its final contact pose.
                    float idleWeight=1-OracleSmooth01(saturate(_TalkBlend/.35));
                    float phase=_Time.y*1.35;
                    local+=float3(cos(phase)*.14,.015*sin(phase*2),.23+sin(phase)*.14)*idleWeight;
                }
                if(v.uv.y>2.5&&v.uv.y<3.5)
                {
                    // Approximate rigid speaking cadence, not phoneme or amplitude lip sync.
                    float cadence=.3+.7*abs(sin(_Time.y*9.5)*sin(_Time.y*6.1+.4));
                    local.y-=.04*saturate(_Speaking)*cadence;
                }
                else if(v.uv.y>1.5&&v.uv.y<2.5)
                {
                    float progress=frac(_Time.y/3.2+v.fx.w);
                    float envelope=sin(3.14159265359*progress);envelope*=envelope;
                    local=v.fx.xyz+(v.p-v.fx.xyz)*envelope+float3(0,progress*.22,0);
                }
                else if(v.uv.y>.5&&v.uv.y<1.5)local.y+=.008*sin(_Time.y*.7+v.p.x*8+v.p.z*6);
                // Original whole head/hood boxes are above1.60; low beard is explicit effect3.
                // Torso tops at1.56, so no box is split and feet/staff/cauldron stay fixed.
                if(_StaffMode<.5&&(v.p.y>=1.60||v.uv.y>2.5)){
                    float2 angles=clamp(_HeadAngles009.xy,float2(-.31415927,-.9599311),float2(.31415927,.9599311));
                    float3 pivot=float3(0,1.60,0);local=pivot+OracleHeadRotate009(local-pivot,angles);localNormal=OracleHeadRotate009(localNormal,angles);
                }
                Varyings o;float4x4 m=GetObjectToWorldMatrix();OracleRigidMatrix64 root;
                root.C0=float4(m._m00_m10_m20,0);root.C1=float4(m._m01_m11_m21,0);
                root.C2=float4(m._m02_m12_m22,0);root.C3=float4(m._m03_m13_m23,1);
                OracleAnimationPacket64 packet;
                packet.PivotHealth=float4(_StaffPivot.xyz,1);packet.TipSupport=_TipAndSupport;
                packet.AxisProgress=float4(normalize(root.C0.xyz),saturate(_TalkBlend));
                packet.ClockRecoil=float4(_Time.y,0,0,1);
                float4 formula=float4(1.1,.07,.8,.35);float4 recoil=float4(0,0,1,0);
                uint kind=_StaffMode>.5?1u:0u;
                float3 world=OracleRigidPosition(local,root,packet,formula,recoil,kind);
                o.p=TransformWorldToHClip(world);o.n=OracleRigidVertexNormal(localNormal,root,packet,formula,recoil,kind);
                o.color=v.color*_BaseColor;o.glow=v.uv.x;o.effectLocal=float3(v.uv.y,v.p.x,v.p.z);
                o.world=world;
                VertexPositionInputs shadowVertex=(VertexPositionInputs)0;
                shadowVertex.positionWS=world;shadowVertex.positionCS=o.p;
                o.shadow=GetShadowCoord(shadowVertex);
                #if defined(_ADDITIONAL_LIGHTS_VERTEX)
                    o.vertexLight=VertexLighting(world,NormalizeNormalPerVertex(o.n));
                #endif
                return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                half3 albedo=i.color.rgb;
                half3 emission=i.color.rgb*i.glow*1.7;
                if(i.effectLocal.x>.5&&i.effectLocal.x<2.5)
                {
                    float cycle=frac(_Time.y/clamp(_BrewCycleSeconds,12,32)+i.effectLocal.y*.23+i.effectLocal.z*.17)*4;
                    int segment=(int)floor(cycle);float blend=smoothstep(0,1,frac(cycle));
                    half3 a=segment==0?_Brew0.rgb:segment==1?_Brew1.rgb:segment==2?_Brew2.rgb:_Brew3.rgb;
                    half3 b=segment==0?_Brew1.rgb:segment==1?_Brew2.rgb:segment==2?_Brew3.rgb:_Brew0.rgb;
                    half3 brew=lerp(a,b,blend);
                    albedo=brew*.35;emission=brew*clamp(_BrewEmission,0,2);
                }
                if(i.effectLocal.x>3.5&&i.effectLocal.x<4.5){clip(_RoomTorchFuel-.001);albedo*=saturate(_RoomTorchFuel);emission*=saturate(_RoomTorchFuel);}
                SurfaceData surface=(SurfaceData)0;
                surface.albedo=albedo;surface.emission=emission;surface.alpha=1;
                surface.metallic=0;surface.smoothness=.055;surface.occlusion=1;
                surface.normalTS=half3(0,0,1);
                InputData lighting=(InputData)0;
                lighting.positionWS=i.world;lighting.positionCS=i.p;
                lighting.normalWS=NormalizeNormalPerPixel(i.n);
                lighting.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.world);
                lighting.shadowCoord=i.shadow;
                lighting.bakedGI=SampleSH(lighting.normalWS);
                #if defined(_ADDITIONAL_LIGHTS_VERTEX)
                    lighting.vertexLighting=i.vertexLight;
                #endif
                lighting.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.p);
                lighting.shadowMask=half4(1,1,1,1);
                half4 color=UniversalFragmentPBR(lighting,surface);
                return half4(color.rgb,1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space:logical local+Z/+Y,borrowed object matrix->world;foundation staff kernel locks support vertex worldY atTalkBlend1.
// Array Formats:actual OracleRigidMatrix64 and OracleAnimationPacket64 ABI reused as shader-local values;cauldron UV0(glow,effect)/UV1(centerXYZ,phase);no uploaded buffer/redeclared schema.
// Dependencies/Pointers:root-fixed Foundation/RigidAnimation.hlsl include;UV0 effect tags/UV1 bubble centre+phase;6 bounded cauldron bubbles,16s smooth4palette cycle;no compute/skins/extraLights/new NPC authority.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing Foundation-animated local vertices/normals -> world position and URP shadow coordinates.
// Array Formats: Existing rigid64 ABI/UV0/UV1 unchanged; bounded interpolants through TEXCOORD5, no new buffers.
// Dependencies/Pointers: Installed URP17.6 public UniversalFragmentPBR receives existing point-light distance/shadow
// attenuation in Forward/cluster variants; target3.5 retained. Existing brew albedo/emission cycle remains local
// presentation, not dynamic GI. Idle-only 14 cm cauldron orbit fades completely by TalkBlend .35;
// original support kernel/normal rotation remains authoritative. Six bubbles retain original bounds/lift/colors at 3.2 s.
// Effect3 is only the existing rigid jaw/beard: <=4 cm local downward motion while the real greeting source is active;
// approximate cadence, no phoneme claim. Ordinary body colors stay outside the unchanged four-brew fragment path.
// No new Light, shadowcaster pass, skin, compute or runtime allocation owner.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing refuge geometry/world axes and support pivots unchanged; elemental numeric state applies only to the supported refuge.
// Array Formats: Existing owner arrays remain canonical; fixed512-node/8word reaction state supplies bounded scalar presentation gains.
// Dependencies/Pointers: Host accepted single Fire/Earth/Wind shots only; existing Object adapters retain materials/lights/lifetime, no rewards/saves or shrine/portal gameplay mutation.
