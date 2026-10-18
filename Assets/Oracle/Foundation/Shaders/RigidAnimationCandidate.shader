Shader "Oracle/Foundation/RigidAnimationCandidate"
{
    Properties
    {
        _BaseMap("Saved albedo", 2D) = "white" {}
        _BaseColor("Base color", Color) = (1,1,1,1)
        _CrackTint("Crack tint", Color) = (0,0,0,1)
        _RigidPartKind("Part: body0 staff1 tower2", Float) = 0
        _FormulaConfig("Stir frequency/amplitude, slam angle, blend end", Vector) = (0,0,0,1)
        _RecoilConfig("XZ stretch, crack scale/contrast/emission", Vector) = (0,0,1,0)
        _RigidInstanceCount("Validated structured record count", Float) = 0
        _RigidBaseIndex("Structured draw base index", Float) = 0
        [PerRendererData] _RootC0("CPU root column0", Vector) = (1,0,0,0)
        [PerRendererData] _RootC1("CPU root column1", Vector) = (0,1,0,0)
        [PerRendererData] _RootC2("CPU root column2", Vector) = (0,0,1,0)
        [PerRendererData] _RootC3("CPU root column3", Vector) = (0,0,0,1)
        [PerRendererData] _AnimPivotHealth("CPU pivot and health", Vector) = (0,0,0,1)
        [PerRendererData] _AnimTipSupport("CPU tip/base and support Y", Vector) = (0,0,0,0)
        [PerRendererData] _AnimAxisProgress("CPU world axis and progress", Vector) = (1,0,0,0)
        [PerRendererData] _AnimClockRecoil("CPU stir sample, elapsed, amplitude, duration", Vector) = (0,-1,0,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "RigidStructured"
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            ZWrite On
            HLSLPROGRAM
            #pragma target 4.5
            #pragma shader_feature_local _ RIGID_CPU_UNIFORMS
            #define ORACLE_STRUCTURED 1

            #pragma vertex RigidVert
            #pragma fragment RigidFrag
            #pragma multi_compile_instancing
            #pragma instancing_options maxcount:16
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "RigidAnimation.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _CrackTint;
                float4 _FormulaConfig;
                float4 _RecoilConfig;
                float _RigidPartKind;
                float _RigidInstanceCount;
                float _RigidBaseIndex;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(RigidCpu)
                UNITY_DEFINE_INSTANCED_PROP(float4, _RootC0)
                UNITY_DEFINE_INSTANCED_PROP(float4, _RootC1)
                UNITY_DEFINE_INSTANCED_PROP(float4, _RootC2)
                UNITY_DEFINE_INSTANCED_PROP(float4, _RootC3)
                UNITY_DEFINE_INSTANCED_PROP(float4, _AnimPivotHealth)
                UNITY_DEFINE_INSTANCED_PROP(float4, _AnimTipSupport)
                UNITY_DEFINE_INSTANCED_PROP(float4, _AnimAxisProgress)
                UNITY_DEFINE_INSTANCED_PROP(float4, _AnimClockRecoil)
            UNITY_INSTANCING_BUFFER_END(RigidCpu)

            #if ORACLE_STRUCTURED && !defined(RIGID_CPU_UNIFORMS)
                StructuredBuffer<OracleRigidMatrix64> _RigidMatrices;
                StructuredBuffer<OracleAnimationPacket64> _RigidPackets;
            #endif

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 restLocal : TEXCOORD1;
                float health : TEXCOORD2;
            };
            bool LoadRigid(out OracleRigidMatrix64 root, out OracleAnimationPacket64 packet)
            {
                #if ORACLE_STRUCTURED && !defined(RIGID_CPU_UNIFORMS)
                    uint instanceIndex = 0u;
                    #if UNITY_ANY_INSTANCING_ENABLED
                        instanceIndex = unity_InstanceID;
                    #endif
                    root = (OracleRigidMatrix64)0;
                    packet = (OracleAnimationPacket64)0;
                    // Comparisons reject NaN/infinity too; float properties may encode only exact nonnegative integers.
                    if (!(_RigidInstanceCount >= 0.0 && _RigidInstanceCount <= 16777216.0
                        && _RigidBaseIndex >= 0.0 && _RigidBaseIndex <= 16777216.0)
                        || floor(_RigidInstanceCount) != _RigidInstanceCount || floor(_RigidBaseIndex) != _RigidBaseIndex) return false;
                    uint matrixCount, matrixStride, packetCount, packetStride;
                    _RigidMatrices.GetDimensions(matrixCount, matrixStride);
                    _RigidPackets.GetDimensions(packetCount, packetStride);
                    if (matrixStride != 64u || packetStride != 64u) return false;
                    uint count = min((uint)_RigidInstanceCount, min(matrixCount, packetCount));
                    uint baseIndex = (uint)_RigidBaseIndex;
                    // Check subtraction BEFORE addition: neither a forged count nor wrapping base can cause an out-of-bounds read.
                    if (baseIndex >= count || instanceIndex >= count - baseIndex) return false;
                    uint index = baseIndex + instanceIndex;
                    root = _RigidMatrices[index];
                    packet = _RigidPackets[index];
                #else
                    root.C0 = UNITY_ACCESS_INSTANCED_PROP(RigidCpu, _RootC0);
                    root.C1 = UNITY_ACCESS_INSTANCED_PROP(RigidCpu, _RootC1);
                    root.C2 = UNITY_ACCESS_INSTANCED_PROP(RigidCpu, _RootC2);
                    root.C3 = UNITY_ACCESS_INSTANCED_PROP(RigidCpu, _RootC3);
                    packet.PivotHealth = UNITY_ACCESS_INSTANCED_PROP(RigidCpu, _AnimPivotHealth);
                    packet.TipSupport = UNITY_ACCESS_INSTANCED_PROP(RigidCpu, _AnimTipSupport);
                    packet.AxisProgress = UNITY_ACCESS_INSTANCED_PROP(RigidCpu, _AnimAxisProgress);
                    packet.ClockRecoil = UNITY_ACCESS_INSTANCED_PROP(RigidCpu, _AnimClockRecoil);
                #endif
                return true;
            }
            Varyings RigidVert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output = (Varyings)0;
                OracleRigidMatrix64 root;
                OracleAnimationPacket64 packet;
                if (!LoadRigid(root, packet))
                {
                    // A disabled/out-of-range record becomes a degenerate primitive outside the frustum.
                    output.positionCS = float4(2,2,2,1);
                    return output;
                }
                float3 world = OracleRigidPosition(input.positionOS, root, packet,
                    _FormulaConfig, _RecoilConfig, (uint)clamp(_RigidPartKind, 0.0, 2.0));
                output.positionCS = TransformWorldToHClip(world);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.restLocal = input.positionOS;
                output.health = packet.PivotHealth.w;
                return output;
            }
            half4 RigidFrag(Varyings input) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                float crack = OracleCrackMask(input.restLocal, input.health, _RecoilConfig);
                float3 crackColor = max(0.0, _CrackTint.rgb);
                float3 color = lerp(albedo.rgb, crackColor, crack)
                    + crackColor * crack * max(0.0, _RecoilConfig.w);
                return half4(color, albedo.a);
            }
            ENDHLSL
        }
    }
    // WebGL2 / constrained renderer fallback: bounded instanced uniforms, no structured buffers or compute kernel.
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "RigidUniformFallback"
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #define ORACLE_STRUCTURED 0

            #pragma vertex RigidVert
            #pragma fragment RigidFrag
            #pragma multi_compile_instancing
            #pragma instancing_options maxcount:16
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "RigidAnimation.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _CrackTint;
                float4 _FormulaConfig;
                float4 _RecoilConfig;
                float _RigidPartKind;
                float _RigidInstanceCount;
                float _RigidBaseIndex;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(RigidCpu)
                UNITY_DEFINE_INSTANCED_PROP(float4, _RootC0)
                UNITY_DEFINE_INSTANCED_PROP(float4, _RootC1)
                UNITY_DEFINE_INSTANCED_PROP(float4, _RootC2)
                UNITY_DEFINE_INSTANCED_PROP(float4, _RootC3)
                UNITY_DEFINE_INSTANCED_PROP(float4, _AnimPivotHealth)
                UNITY_DEFINE_INSTANCED_PROP(float4, _AnimTipSupport)
                UNITY_DEFINE_INSTANCED_PROP(float4, _AnimAxisProgress)
                UNITY_DEFINE_INSTANCED_PROP(float4, _AnimClockRecoil)
            UNITY_INSTANCING_BUFFER_END(RigidCpu)

            #if ORACLE_STRUCTURED && !defined(RIGID_CPU_UNIFORMS)
                StructuredBuffer<OracleRigidMatrix64> _RigidMatrices;
                StructuredBuffer<OracleAnimationPacket64> _RigidPackets;
            #endif

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 restLocal : TEXCOORD1;
                float health : TEXCOORD2;
            };
            bool LoadRigid(out OracleRigidMatrix64 root, out OracleAnimationPacket64 packet)
            {
                #if ORACLE_STRUCTURED && !defined(RIGID_CPU_UNIFORMS)
                    uint instanceIndex = 0u;
                    #if UNITY_ANY_INSTANCING_ENABLED
                        instanceIndex = unity_InstanceID;
                    #endif
                    root = (OracleRigidMatrix64)0;
                    packet = (OracleAnimationPacket64)0;
                    // Comparisons reject NaN/infinity too; float properties may encode only exact nonnegative integers.
                    if (!(_RigidInstanceCount >= 0.0 && _RigidInstanceCount <= 16777216.0
                        && _RigidBaseIndex >= 0.0 && _RigidBaseIndex <= 16777216.0)
                        || floor(_RigidInstanceCount) != _RigidInstanceCount || floor(_RigidBaseIndex) != _RigidBaseIndex) return false;
                    uint matrixCount, matrixStride, packetCount, packetStride;
                    _RigidMatrices.GetDimensions(matrixCount, matrixStride);
                    _RigidPackets.GetDimensions(packetCount, packetStride);
                    if (matrixStride != 64u || packetStride != 64u) return false;
                    uint count = min((uint)_RigidInstanceCount, min(matrixCount, packetCount));
                    uint baseIndex = (uint)_RigidBaseIndex;
                    // Check subtraction BEFORE addition: neither a forged count nor wrapping base can cause an out-of-bounds read.
                    if (baseIndex >= count || instanceIndex >= count - baseIndex) return false;
                    uint index = baseIndex + instanceIndex;
                    root = _RigidMatrices[index];
                    packet = _RigidPackets[index];
                #else
                    root.C0 = UNITY_ACCESS_INSTANCED_PROP(RigidCpu, _RootC0);
                    root.C1 = UNITY_ACCESS_INSTANCED_PROP(RigidCpu, _RootC1);
                    root.C2 = UNITY_ACCESS_INSTANCED_PROP(RigidCpu, _RootC2);
                    root.C3 = UNITY_ACCESS_INSTANCED_PROP(RigidCpu, _RootC3);
                    packet.PivotHealth = UNITY_ACCESS_INSTANCED_PROP(RigidCpu, _AnimPivotHealth);
                    packet.TipSupport = UNITY_ACCESS_INSTANCED_PROP(RigidCpu, _AnimTipSupport);
                    packet.AxisProgress = UNITY_ACCESS_INSTANCED_PROP(RigidCpu, _AnimAxisProgress);
                    packet.ClockRecoil = UNITY_ACCESS_INSTANCED_PROP(RigidCpu, _AnimClockRecoil);
                #endif
                return true;
            }
            Varyings RigidVert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output = (Varyings)0;
                OracleRigidMatrix64 root;
                OracleAnimationPacket64 packet;
                if (!LoadRigid(root, packet))
                {
                    // A disabled/out-of-range record becomes a degenerate primitive outside the frustum.
                    output.positionCS = float4(2,2,2,1);
                    return output;
                }
                float3 world = OracleRigidPosition(input.positionOS, root, packet,
                    _FormulaConfig, _RecoilConfig, (uint)clamp(_RigidPartKind, 0.0, 2.0));
                output.positionCS = TransformWorldToHClip(world);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.restLocal = input.positionOS;
                output.health = packet.PivotHealth.w;
                return output;
            }
            half4 RigidFrag(Varyings input) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                float crack = OracleCrackMask(input.restLocal, input.health, _RecoilConfig);
                float3 crackColor = max(0.0, _CrackTint.rgb);
                float3 color = lerp(albedo.rgb, crackColor, crack)
                    + crackColor * crack * max(0.0, _RecoilConfig.w);
                return half4(color, albedo.a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Authored rigid local mesh -> explicit column-vector render-origin-relative world; camera VP only, no automatic object matrix/time.
// Array Formats: _RigidMatrices Matrix64 + _RigidPackets Packet64; bounded draw count/base; CPU fallback eight instanced float4 values, maxcount16.
// Dependencies/Pointers: URP Core + local pure HLSL; root owns buffers/materials, valid support stamps, upload/read fences and native/WebGPU/WebGL verification.
