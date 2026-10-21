Shader "OdysseyOracle/PlayerStaffSpiral"
{
    Properties
    {
        _PulseAge("Private cosmetic pulse age",Float)=0
        _PulseDuration("Private cosmetic duration",Float)=.46
        _ElementMask("Actual selection or accepted mask",Float)=0
        _Strength("Bounded visual strength",Float)=0
        _EngulfAge("Private one-second staff age",Float)=1
        _EngulfPhase("Held unit color phase",Float)=0
        _EngulfMask("Staff-only actual mask",Float)=0
        _EngulfStrength("Staff-only continuous envelope",Float)=0
        _FireColor("Existing Fire",Color)=(1,.36,.12,1)
        _WaterColor("Existing Water",Color)=(.18,.68,1,1)
        _EarthColor("Existing Earth",Color)=(.62,.78,.29,1)
        _AirColor("Existing Air",Color)=(.66,.91,1,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+20" }
        Pass
        {
            Name "OriginalPlayerStaffSpiral"
            Tags { "LightMode"="UniversalForwardOnly" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _FireColor,_WaterColor,_EarthColor,_AirColor;
            float _PulseAge,_PulseDuration,_ElementMask,_Strength;
            float _EngulfAge,_EngulfPhase,_EngulfMask,_EngulfStrength;
            CBUFFER_END
            struct Attributes {float3 positionOS:POSITION;float4 uv:TEXCOORD0;};
            struct Varyings {float4 positionCS:SV_POSITION;half4 color:COLOR;float side:TEXCOORD0;float2 staff:TEXCOORD1;};
            float3 Accent(int element)
            {return element==0?_FireColor.rgb:element==1?_WaterColor.rgb:element==2?_EarthColor.rgb:_AirColor.rgb;}
            int FirstElement(int mask)
            {return (mask&1)!=0?0:(mask&2)!=0?1:(mask&4)!=0?2:3;}
            float3 WholeStaffColor(float along,float phase,int mask)
            {
                if(mask==15)return saturate(abs(frac(along+phase+float3(0,.666666667,.333333333))*6-3)-1);
                int first=FirstElement(mask),remaining=mask&~(1<<first);
                float3 a=Accent(first);
                if(remaining==0)return a;
                float blend=.5-.5*cos(6.28318530718*(phase+along*.35));
                return lerp(a,Accent(FirstElement(remaining)),blend);
            }
            Varyings Vertex(Attributes input)
            {
                float u=input.uv.x,side=input.uv.y;
                if(input.uv.w>.5)
                {
                    Varyings surface;surface.positionCS=TransformObjectToHClip(input.positionOS);surface.side=0;surface.staff=float2(u,1);
                    float visible=_EngulfAge>=0&&_EngulfAge<1?saturate(_EngulfStrength):0;
                    surface.color=half4(0,0,0,visible*.62);
                    return surface;
                }
                int element=(int)round(input.uv.z),mask=(int)round(_ElementMask);
                float selected=(mask>>element)&1;
                float age=saturate(_PulseAge/max(.001,_PulseDuration)),head=saturate(age/.78);
                float packet=smoothstep(0,.045,age)*(1-smoothstep(.78,1,age))*(1-smoothstep(head,head+.035,u))*smoothstep(head-.26,head-.20,u);
                float angle=u*(6.28318530718*3)-age*(6.28318530718*1.4)+element*1.57079632679;
                float radius=.052*(1-smoothstep(.80,1,u));
                float3 radial=float3(cos(angle),0,sin(angle));
                float width=.0032*selected*saturate(_Strength)*packet;
                // Original staff base and lens center are exact; radial offset reacheszero at u1.
                float3 positionOS=float3(0,lerp(-.332,.450,u),0)+radial*(radius+side*width);
                float3 color=Accent(element);
                Varyings output;output.positionCS=TransformObjectToHClip(positionOS);output.side=side;output.staff=0;
                output.color=half4(color*1.9,selected*saturate(_Strength)*packet*.78);return output;
            }
            half4 Fragment(Varyings input):SV_Target
            {
                // Evaluate per pixel so long original shaft faces contain the whole spectrum.
                if(input.staff.y>.5)return half4(WholeStaffColor(input.staff.x,frac(_EngulfPhase),(int)round(_EngulfMask))*1.9,input.color.a);
                half edge=1-smoothstep(.45,1,abs(input.side));return half4(input.color.rgb,input.color.a*edge);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: original wand-local base(0,-.332,0) to exact lens(0,.450,0); radius.052m collapses at top; same wand/camera clearance transform, no global Time.
// Array Formats: fixed4strands x17steps x2sides=136vertices/128triangles; UV carries along/side/element, four constant colors and scalar accepted/selected mask, age/duration/strength.
// Dependencies/Pointers: GPU sin/cos only; private finite cosmetic state, zero end alpha+width, bounded additive lightless effect; no active CPU geometry, bones, collider or cast/recovery/host authority.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: unchanged original helix plus exact staff-only copied surfaces; UV.w distinguishes surface, UV.x is full authored staff height. Original hand/cuff never enters this mesh.
// Array Formats: existing single material/forward pass and MPB, four new bounded scalar uniforms. Per-pixel color keeps one original element/two cosine-blended elements/a complete four-mask hue cycle even on long unsplit shaft faces.
// Dependencies/Pointers: private held phase/one-second age only, no global Time, new light, render target, body material mutation or authority. Surface alpha becomes exactly zero at age1; original geometry/palette then remains visible unchanged.
