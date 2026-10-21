Shader "OdysseyOracle/LaneGateGrid"
{
    Properties
    {
        _BaseColor ("Transparent ruby shell", Color) = (0.72,0.018,0.045,0.45)
        [HDR] _GridColor ("Ruby matrix lines", Color) = (1.8,0.055,0.12,0.78)
        [HDR] _ProximityColor ("Approach warning color", Color) = (3.2,0.12,0.20,0.90)
        _BarrierEffect ("Explicit closed-lane effect", Float) = 0
        _GridSpacing ("Grid spacing in metres", Float) = 0.7
        _LineWidth ("Line width in metres", Float) = 0.025
        _ProximityFar ("Approach start in metres", Float) = 6
        _ProximityNear ("Approach full in metres", Float) = 1.5
        _PulseSpeed ("Warning pulse speed", Range(0.1,8)) = 3.5
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+25" }
        Pass
        {
            Name "InactiveLaneGrid"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _GridColor;
                half4 _ProximityColor;
                float _BarrierEffect;
                float _GridSpacing;
                float _LineWidth;
                float _ProximityFar;
                float _ProximityNear;
                float _PulseSpeed;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionOS : TEXCOORD0; float3 normalOS : TEXCOORD1; float3 positionWS : TEXCOORD2; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                output.positionWS=TransformObjectToWorld(input.positionOS.xyz);
                output.positionOS=input.positionOS.xyz;
                output.normalOS=input.normalOS;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float3 n=abs(input.normalOS);
                float2 plane=n.y>.5?input.positionOS.xz:(n.x>.5?input.positionOS.yz:input.positionOS.xy);
                float spacing=max(_GridSpacing,.05);
                float2 d=abs(frac(plane/spacing+.5)-.5)*spacing;
                float edge=min(d.x,d.y);
                float antialias=max(fwidth(edge),.002);
                float grid=1-smoothstep(_LineWidth,_LineWidth+antialias,edge);
                float scan=.92+.08*sin(input.positionOS.y*4-_Time.y*1.8);
                half4 color=lerp(_BaseColor,_GridColor,grid);
                color.rgb*=scan;
                // Window shafts share this shader. Only their explicit World barrier owner opts in.
                if(_BarrierEffect<.5)return color;

                float distanceToView=length(input.positionWS-GetCameraPositionWS());
                float farDistance=max(_ProximityFar,.5);
                float nearDistance=clamp(_ProximityNear,0,farDistance-.25);
                float value=saturate((farDistance-distanceToView)/(farDistance-nearDistance));
                float proximity=value*value*(3-2*value);
                if(proximity<=0)return color;
                float phase=_Time.y*clamp(_PulseSpeed,.1,8);
                float pulse=.9+.1*sin(phase*2);
                float ring=saturate(.5+.5*sin(distanceToView*4.8-phase*2.4));ring*=ring;ring*=ring;
                float cascade=saturate(.5+.5*sin(input.positionOS.y*3.5-phase*1.8));cascade*=cascade;cascade*=cascade;
                float ripple=.65*ring+.35*cascade;
                half3 warning=clamp(_ProximityColor.rgb,half3(0,0,0),half3(8,8,8));
                color.rgb=lerp(color.rgb,warning*(.55+.45*grid),.55*proximity);
                color.rgb*=lerp(1,pulse,.6*proximity);
                color.rgb+=warning*(proximity*(.16+.35*ripple));
                color.rgb=clamp(color.rgb,half3(0,0,0),half3(8,8,8));
                color.a=min(.92,clamp(max(color.a,.45),.45,.9)+proximity*(.12+.10*ripple));
                return color;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: original object-local grid and world-metre camera proximity;6m start/1.5m full warning with smoothstep endpoint slopes.
// Array Formats: existing POSITION/NORMAL vertex buffers plus one float3 world-position interpolator; shared UnityPerMaterial constants, no textures/compute/dynamic CPU buffers.
// Dependencies/Pointers: World explicitly opts only closed barriers into bounded ruby/glow/pulse/ripple; default0 preserves blue window shafts. Existing transparent pass, geometry, solid collider/lane masks untouched; Unlit fallback has ruby45percent alpha but no proximity. Native shader/pixel/browser/performance acceptance pending.
