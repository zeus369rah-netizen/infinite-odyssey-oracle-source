Shader "Oracle/Presentation/TowerElements009"
{
    Properties
    {
        _Clock("Explicit held clock",Float)=0 _ShotAge("Accepted shot age",Float)=10 _Travel("Cosmetic flight seconds",Float)=.2
        _TypeTier("Type tier accepted quality",Vector)=(0,1,0,1) _ColorRadius("Original element and orbit",Vector)=(1,.36,.12,.4)
        _Detail("Fixed feature counts",Vector)=(2,2,3,1) _Shape("Height rate opacity tint",Vector)=(1.8,.185,.6,.82)
        _ShotOrigin("Accepted local muzzle",Vector)=(0,0,0,0) _ShotHitRadius("Accepted local hit and real area",Vector)=(0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float _Clock,_ShotAge,_Travel;
            float4 _TypeTier,_ColorRadius,_Detail,_Shape,_ShotOrigin,_ShotHitRadius;
            CBUFFER_END
            struct Attributes { float3 p:POSITION;float2 uv:TEXCOORD0;float3 tag:TEXCOORD1; };
            struct Varyings { float4 p:SV_POSITION;half4 color:COLOR;float2 uv:TEXCOORD0;float soft:TEXCOORD1;float enabled:TEXCOORD2; };
            float3 RotateY(float3 p,float angle){float s=sin(angle),c=cos(angle);return float3(c*p.x+s*p.z,p.y,-s*p.x+c*p.z);}
            Varyings Vert(Attributes v)
            {
                Varyings o;o.uv=v.uv;o.soft=0;o.enabled=1;
                float kind=v.tag.x,id=v.tag.y,seed=v.tag.z,type=_TypeTier.x,tier=_TypeTier.y,r=_ColorRadius.w;
                float time=_Clock*(.8+tier*.10);
                float3 p=v.p,color=_ColorRadius.rgb;float alpha=_Shape.z;
                if(kind<.5)
                {
                    float u=v.uv.y;float x=(id-(_Detail.x-1)*.5)*.15+sin(time*4+u*5+seed)*.018;
                    p=float3(x+(v.uv.x*2-1)*.026,1.35-u*1.4,.69+sin(u*3.14159265)*.06);
                    color*=.8+.3*sin(u*18+time*6+seed);alpha=.55;
                }
                else if(kind<1.5)
                {
                    float u=v.uv.y,a=u*9.42477796+time*2.2+id*2.1;
                    float radius=r*(.12+u*.70)+(v.uv.x*2-1)*.035;
                    p=float3(cos(a)*radius,.05+u*1.7,sin(a)*radius);alpha=.20*(.65+.35*sin(u*9+time*3+id));
                }
                else if(kind<2.5)
                {
                    float a=time*(.7+id*.025)+id*2.399963;float3 center=float3(cos(a)*r,.75+.08*sin(time+seed),sin(a)*r);
                    p=center+RotateY(v.p*(.10+.02*tier),time*.8+id);color=lerp(float3(.35,.29,.18),_ColorRadius.rgb*.65,frac(id*.37));alpha=1;
                }
                else if(kind<3.5)
                {
                    float hot=saturate(v.p.y*.5+.5);float3 center=float3(cos(seed)*r*.4,.95+id*.02,sin(seed)*r*.3);
                    p=center+float3(v.p.x*(.10+.018*tier)*(1-.65*hot)+sin(time*3+seed+hot*4)*.035*hot,v.p.y*(.25+.05*tier),v.p.z*(.10+.018*tier)*(1-.65*hot));
                    color=lerp(float3(1,.72,.16),float3(1,.16,.015),hot)*1.45;alpha=.60;
                }
                else if(kind<4.5)
                {
                    float life=frac(time*.42+id/max(1,_Detail.z)),envelope=sin(3.14159265*life);envelope*=envelope;float a=seed+life*4;
                    p=float3(sin(a)*(.08+.30*life),.8+1.25*life,cos(a)*(.08+.30*life))+v.p*(.018*envelope);
                    color=float3(1,.42,.035)*1.5;alpha=.75*envelope;
                }
                else if(kind<5.5)
                {
                    float life=frac(time*.24+id/max(1,_Detail.w)),envelope=sin(3.14159265*life);envelope*=envelope;
                    float3 center=float3(sin(seed+life*2)*.15,.95+1.1*life,cos(seed+life*2)*.15);
                    float4x4 objectMatrix=GetObjectToWorldMatrix();
                    float objectScale=min(length(objectMatrix._m00_m10_m20),min(length(objectMatrix._m01_m11_m21),length(objectMatrix._m02_m12_m22)));
                    float size=(.10+.14*life)*envelope*objectScale;
                    float3 world=TransformObjectToWorld(center)+UNITY_MATRIX_I_V._m00_m10_m20*(v.p.x*size)+UNITY_MATRIX_I_V._m01_m11_m21*((v.uv.y-.5)*size);
                    o.p=TransformWorldToHClip(world);o.color=half4(.18,.19,.21,.10*envelope);o.soft=1;if(_TypeTier.w<.5&&id>=1)o.enabled=0;return o;
                }
                else if(kind<6.5)
                {
                    float life=frac(time*.35+id/max(1,_Detail.y)),envelope=sin(3.14159265*life);envelope*=envelope;
                    p=float3(cos(seed)*r*.3,.3+life*.45,sin(seed)*r*.3)+v.p*(.035*envelope);color=float3(1,.62,.11)*1.3;alpha=.65*envelope;
                }
                else if(kind<7.5)
                {
                    float life=frac(time*.75+id/max(1,_Detail.y)),envelope=sin(3.14159265*life);
                    p=float3((id-(_Detail.y-1)*.5)*.15,1.2-life*1.25,.72)+v.p*float3(.025,.05,.025)*envelope;alpha=.65*envelope;
                }
                else if(kind<8.5)
                {
                    float a=time*(.9+id*.03)+id*2.399963;
                    p=float3(cos(a)*r*.75,.35+(.5+.5*sin(time*.8+seed))*1.1,sin(a)*r*.75)+RotateY(v.p*(.065+tier*.01),time+id);alpha=.75;
                }
                else if(kind<9.5)
                {
                    float a=time*.6+id*2.399963;float3 center=float3(cos(a)*(r+.05),1+.1*sin(time+seed),sin(a)*(r+.05));
                    p=center+float3(v.p.x*.07,(v.uv.y-.5)*.07,0);alpha=.14;o.soft=1;
                }
                else
                {
                    float3 delta=_ShotHitRadius.xyz-_ShotOrigin.xyz;float distanceValue=max(length(delta),.0001);float3 forward=delta/distanceValue;
                    if(dot(forward,forward)<.5)forward=float3(0,0,1);
                    float3 side=cross(forward,float3(0,1,0));if(dot(side,side)<.001)side=float3(1,0,0);side=normalize(side);float3 up=normalize(cross(side,forward));
                    float u=saturate(_ShotAge/max(_Travel,.001));float3 center=lerp(_ShotOrigin.xyz,_ShotHitRadius.xyz,u);
                    if(type==1)center+=up*(sin(u*3.14159265)*.12);
                    if(type==3)center+=side*(sin(u*6.2831853)*.08);
                    if(kind<10.5)
                    {
                        o.enabled=_TypeTier.z>.5&&_ShotAge<=_Travel?1:0;float size=.08+.02*tier;
                        p=center+(side*v.p.x+up*v.p.y+forward*v.p.z*1.3)*size;
                        if(type==2)color=lerp(float3(.38,.31,.20),_ColorRadius.rgb,.3);
                        alpha=type==3?.08:type==1?.75:1;
                    }
                    else if(kind<11.5)
                    {
                        o.enabled=_TypeTier.z>.5&&_ShotAge<=_Travel?1:0;float a=time*12+id;
                        float3 trail=center-forward*((id+1)*.055)+side*cos(a)*.05+up*sin(a)*.05;
                        p=trail+side*(v.p.x*.09)+up*((v.uv.y-.5)*.09);alpha=(1-id/7)*.40;o.soft=1;
                    }
                    else
                    {
                        float life=saturate((_ShotAge-_Travel)/.38),fade=1-life,a=id*1.04719755+(type==3?life*3:0);
                        o.enabled=_TypeTier.z>.5&&_ShotAge>=_Travel&&life<1?1:0;
                        float reach=_ShotHitRadius.w>0?_ShotHitRadius.w*life:(.05+.20*life);float3 radial=side*cos(a)+up*sin(a);
                        if(kind<12.5){p=_ShotHitRadius.xyz+radial*reach+up*(life*.12)+RotateY(v.p*.07*fade,time+id);alpha=.65*fade;}
                        else{p=_ShotHitRadius.xyz+radial*reach+side*(v.p.x*.10*fade)+up*((v.uv.y-.5)*.10*fade);alpha=.45*fade;o.soft=1;}
                    }
                }
                if(_TypeTier.w<.5&&kind<9.5){float limit=kind<.5?2:kind<1.5?1:kind<2.5?2:kind<3.5?2:kind<4.5?3:kind<5.5?1:kind<6.5?2:2;if(id>=limit)o.enabled=0;}
                o.p=TransformObjectToHClip(p);o.color=half4(color,alpha);return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                clip(i.enabled-.5);float alpha=i.color.a;
                if(i.soft>.5){float2 q=i.uv*2-1;alpha*=saturate(1-dot(q,q));}
                return half4(i.color.rgb*alpha,alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing accepted Head-local +Z/Y-up; explicit private clock/accepted local endpoints only. Smoke uses bounded camera axes; no global Time/sky, Physics or retarget.
// Array Formats: Vertex32 POSITION/UV0/UV1(kind,id,seed); fixed recipe/detail/clock/shot MPBs. Continuous ribbon flow/helix/orbit plus bounded age shrink/fade, no particle Objects/compute/skins.
// Dependencies/Pointers: Original shared shader/material and12 reusable mesh pairs. Small contact flourishes are cosmetic; any wider radius must be actual accepted combat data. Native shader/bounds/visual/two-peer/performance remain root acceptance gates.
