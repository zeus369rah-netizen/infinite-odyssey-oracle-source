Shader "OdysseyOracle/PortalLandingSky020"
{
    Properties
    {
        _PresentationClock020("Held presentation clock",Float)=0
        _Quality020("Bounded detail quality",Range(0,2))=1
        _CycleDaylight020("Daylight weight",Range(0,1))=0
        _CycleNight020("Night state",Range(0,1))=1
        _CycleStorm020("Night storm weight",Range(0,1))=0
        _CycleTwilight020("Twilight weight",Range(0,1))=0
        _CycleAurora020("Night aurora weight",Range(0,1))=0
        _CycleSunDirection020("Session key sky direction",Vector)=(0,.67,-.74,0)
        _CloudVisibility020("Seeded cloud visibility",Range(0,1))=1
        _CloudSeed020("Campaign cloud seed",Range(0,1))=.5
        _LensFlareAxis020("Subtle lens axis",Vector)=(1,0,0,0)
        _LensFlareVisibility020("Occluded flare visibility",Range(0,1))=0
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox"}
        Pass
        {
            Name "IndigoAuroraAndDistantClouds020"
            Cull Off ZWrite Off ZTest LEqual
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float _PresentationClock020,_Quality020;
            float _CycleDaylight020,_CycleNight020,_CycleStorm020,_CycleTwilight020,_CycleAurora020;
            float4 _CycleSunDirection020;
            float _CloudVisibility020,_CloudSeed020,_LensFlareVisibility020;
            float4 _LensFlareAxis020;
            CBUFFER_END
            struct A {float4 positionOS:POSITION;};
            struct V {float4 positionCS:SV_POSITION;float3 direction:TEXCOORD0;};
            V Vert(A a)
            {
                V o;o.positionCS=TransformObjectToHClip(a.positionOS.xyz);
                #if UNITY_REVERSED_Z
                o.positionCS.z=0;
                #else
                o.positionCS.z=o.positionCS.w;
                #endif
                o.direction=a.positionOS.xyz;return o;
            }
            float LandingHash020(float2 cell)
            {
                float2 a=frac(cell*float2(.173,.137)+float2(.319,.271));
                float v=dot(a,a.yx+float2(11.71,7.13));
                return frac((a.x+v*.173)*(a.y+v+.37)*7.91);
            }
            float LandingNoise020(float2 coordinate)
            {
                float2 cell=floor(coordinate),f=frac(coordinate);
                float2 s=f*f*f*(f*(f*6-15)+10);
                float a=LandingHash020(cell),b=LandingHash020(cell+float2(1,0));
                float c=LandingHash020(cell+float2(0,1)),e=LandingHash020(cell+1);
                return lerp(lerp(a,b,s.x),lerp(c,e,s.x),s.y);
            }
            float3 Aurora020(float3 d,float2 h,float2 drift,float lift,float seed,float opacity,out float intensity)
            {
                float warp=LandingNoise020(h*1.35+drift*.35+float2(seed*2.3,seed*.71));
                float broad=LandingNoise020(h*2.65+float2(d.y*1.6,-d.y*.7)+drift*.26+float2(seed*4.1,seed*1.3));
                float edgeNoise=(warp-.5)*.65+(broad-.5)*.35;
                float middleAltitude=.32+lift+(warp-.5)*.13+edgeNoise*.028+h.x*.035-h.y*.025;
                float altitude=d.y-middleAltitude;
                float width=.125+.045*broad;
                float q=altitude/width,halo=(altitude+.055)/(width*1.6);
                float envelope=exp2(-q*q*1.7),soft=exp2(-halo*halo*1.5);
                float edge=(altitude+edgeNoise*.020+.025)/(width*.42);
                edge=exp2(-edge*edge*2.0);
                float2 axis=float2(cos(seed*2.73),sin(seed*2.73));
                float arc=smoothstep(-.78,.32,dot(h,axis));
                float rate=_Quality020<.5?24.0:_Quality020<1.5?40.0:56.0;
                float strandCoordinate=dot(h,float2(axis.y,-axis.x))*rate+warp*3.0+drift.x*.35;
                float tooth=abs(frac(strandCoordinate)-.5)*2.0;
                float strandAA=max(fwidth(strandCoordinate),.002);
                float strand=1-smoothstep(.20-strandAA,.64+strandAA,tooth);
                float reach=frac(floor(strandCoordinate)*.61803398875+seed*.73);
                strand*=smoothstep(middleAltitude-.20+reach*.10,middleAltitude-.06+reach*.06,d.y);
                float vertical=smoothstep(.01,.08,d.y)*(1-smoothstep(.79,.96,d.y));
                intensity=(envelope*(.82+.18*strand)+soft*.07+edge*(.15+.05*strand))*(.70+.30*broad)*(.55+.45*arc)*vertical;
                float3 tint=lerp(float3(.018,.65,.55),float3(.54,.045,.86),saturate(seed*.52+warp*.20));
                intensity*=opacity;
                return tint*intensity;
            }
            float CloudCell020(float2 cell,float period)
            {
                if(period>0)cell.x-=floor(cell.x/period)*period;
                return LandingHash020(cell+float2(_CloudSeed020*13.71,_CloudSeed020*31.17));
            }
            float3 CloudField020(float2 coordinate,float period,out float4 seeds)
            {
                float2 cell=floor(coordinate),f=frac(coordinate);
                float2 s=f*f*f*(f*(f*6-15)+10),ds=30*f*f*(f*(f-2)+1);
                float a=CloudCell020(cell,period),b=CloudCell020(cell+float2(1,0),period);
                float c=CloudCell020(cell+float2(0,1),period),e=CloudCell020(cell+1,period);
                seeds=float4(a,b,c,e);
                float low=lerp(a,b,s.x),high=lerp(c,e,s.x);
                return float3(lerp(low,high,s.y),lerp(b-a,e-c,s.y)*ds.x,(high-low)*ds.y);
            }
            float4 CloudPuff020(float2 delta,float seed,float3 east,float3 outward,float3 incoming)
            {
                float2 jitter=float2(seed-.5,frac(seed*7.13)-.5)*.40;
                float radius=.52+.18*frac(seed*5.17);
                float2 q=(delta-jitter)/radius;
                float2 block=floor(q*6.0+.5)/6.0;
                float shape=dot(block,block);
                float edgeAA=max(max(fwidth(q.x),fwidth(q.y))*.75,.012);
                float coverage=1-smoothstep(.72-edgeAA,1.0+edgeAA,shape);
                if(coverage<=0)return 0;
                float front=sqrt(saturate(1-dot(q,q)));
                float3 normal=east*q.x+float3(0,q.y,0)-outward*front;
                float light=saturate(.18+.58*saturate(dot(normal,incoming))+.18*front);
                float3 shade=lerp(float3(.014,.023,.069),float3(.13,.17,.32),light);
                shade+=float3(.15,.009,.045)*saturate(.30-q.y)*front*.35;
                float weight=coverage*(.75+.25*front);
                return float4(shade*weight,weight);
            }
            float4 CloudGroup020(float2 f,float4 seeds,float3 east,float3 outward,float3 incoming)
            {
                return CloudPuff020(f,seeds.x,east,outward,incoming)+
                    CloudPuff020(f-float2(1,0),seeds.y,east,outward,incoming)+
                    CloudPuff020(f-float2(0,1),seeds.z,east,outward,incoming)+
                    CloudPuff020(f-1,seeds.w,east,outward,incoming);
            }
            float4 Bank020(float3 d,float2 h,float2 drift,float scale,float height,float thickness,float strength,float3 incoming)
            {
                if(_CloudVisibility020<=0)return 0;
                if(d.y>height+.10+thickness*1.2)return 0;
                // Exactly14/20/28 angular cells in the three finite banks; wrap hashes at the azimuth seam.
                float period=floor((10.5-scale)*4.0+.5);
                float safeY=abs(h.x)+abs(h.y)>.00001?h.y:1.0;
                float u=atan2(h.x,safeY)*(1.0/6.28318530718)+.5;
                float2 coordinate=float2(u*period,(d.y-height)*(period/6.28318530718)*1.35)+drift;
                float4 seeds;
                float3 field=CloudField020(coordinate,period,seeds);
                float3 east=float3(h.y,0,-h.x),outward=float3(h.x,0,h.y);
                float4 puffs=CloudGroup020(frac(coordinate),seeds,east,outward,incoming);
                if(_Quality020>1.5)
                {
                    float2 smallCoordinate=coordinate*2.0+float2(6.7,3.1);
                    float4 smallSeeds;
                    float3 small=CloudField020(smallCoordinate,period*2.0,smallSeeds);
                    field=float3(field.x*.77+small.x*.23,field.yz*.77+small.yz*(.23*2.0));
                    puffs=puffs*.72+CloudGroup020(frac(smallCoordinate),smallSeeds,east,outward,incoming)*.58;
                }
                float top=height+(field.x-.5)*.20;
                float altitude=(d.y-top)/thickness;
                float alpha=(1-smoothstep(-.40,1.2,altitude))*strength*saturate(_CloudVisibility020);
                float density=saturate(puffs.a*.82+field.x*.12);
                alpha*=.18+.82*density;
                float crown=(altitude+.35)*.85;
                crown=exp2(-crown*crown*2.0);
                float3 normal=normalize(float3(-field.y*.48,.80,-field.z*.48));
                float light=saturate(.12+.30*saturate(dot(normal,incoming))+.14*crown);
                float3 shade=lerp(float3(.012,.025,.072),float3(.080,.140,.280),light);
                shade=lerp(shade,puffs.rgb/max(puffs.a,.0001),saturate(puffs.a));
                float horizonRim=exp2(-abs(top-.035)*9.0)*crown;
                shade+=float3(.24,.015,.085)*horizonRim*density;
                float haze=saturate((height+.085)*3.0)*.20;
                shade=lerp(shade,float3(.085,.045,.115),haze);
                return float4(shade,saturate(alpha));
            }
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Seamless normalized sky directions; ground-visible angular curtains and three finite stepped/bevelled cloud banks.
// Array Formats: Same two fields per aurora lobe and four/eight seeded puffs per bank;24/40/56 analytic strand rates add no hash/noise/trig call. Finite14/20/28wrapped angular cells, maximum56with octave; no world tiles/arrays/raymarch/samplers/RTs.
// Dependencies/Pointers: Existing held clock/quality/main-light direction. Derivative-bounded strand and puff edges; extra floor/ALU operations require root GPU measurement, not a performance claim.
            float BoxMask020(float2 p,float2 center,float2 halfSize,float aa)
            {float2 q=abs(p-center)-halfSize;return 1-smoothstep(-aa,aa,max(q.x,q.y));}
            float Island020(float3 d,float2 h,float2 heading,float width,float elevation,float variant)
            {
                float front=smoothstep(.88,.94,dot(h,heading));
                float side=h.x*heading.y-h.y*heading.x;
                float2 p=float2(side,d.y-elevation);
                float aa=max(fwidth(side),.0006);
                float radius=saturate(abs(side)/width);
                float bottom=-.085*pow(1-radius,.65);
                float cliff=(1-smoothstep(width-aa,width+aa,abs(side)))*smoothstep(bottom-aa,bottom+aa,p.y)*(1-smoothstep(.012-aa,.012+aa,p.y));
                float towerHeight=.098+variant*.026;
                float shape=BoxMask020(p,float2(0,towerHeight*.5),float2(width*.16,towerHeight*.5),aa);
                shape=max(shape,BoxMask020(p,float2(-width*.47,.030),float2(width*.12,.041),aa));
                shape=max(shape,BoxMask020(p,float2(width*.43,.041),float2(width*.10,.052),aa));
                float roofY=p.y-towerHeight;
                float roof=(1-smoothstep(width*.24-aa,width*.24+aa,abs(side)+max(0,roofY)*.48))*smoothstep(-aa,aa,roofY)*(1-smoothstep(.030-aa,.030+aa,roofY));
                return saturate(max(cliff,max(shape,roof)))*front;
            }
            float4 IslandShade020(float3 d,float2 h,float2 heading,float width,float elevation,float variant,float nearLayer)
            {
                float side=h.x*heading.y-h.y*heading.x;
                float aa=max(fwidth(side),.0006);
                float mask=Island020(d,h,heading,width,elevation,variant);
                if(mask<=.00001)return 0;
                float2 p=float2(side,d.y-elevation);
                float grain=.5;
                if(_Quality020>.5)grain=LandingNoise020(float2(side/max(width,.0001)*1.7,p.y*13.0)+float2(variant*4.7,variant*.73));
                float depth=saturate(-p.y/.085);
                float facing=saturate(.68+side/max(width,.0001)*.14+grain*.17-depth*.18);
                float3 shade=lerp(float3(.078,.066,.121),float3(.027,.038,.078),nearLayer)*facing;
                float terrace=exp2(-abs(p.y-.007)*210.0);
                shade+=lerp(float3(.040,.025,.052),float3(.030,.044,.069),nearLayer)*terrace;
                float recess=BoxMask020(p,float2(-width*.035,.039),float2(width*.060,.0072),aa);
                recess=max(recess,BoxMask020(p,float2(width*.04,.071),float2(width*.055,.0072),aa));
                shade=lerp(shade,shade*.32,recess);
                float windows=BoxMask020(p,float2(-width*.035,.039),float2(width*.035,.005),aa);
                windows=max(windows,BoxMask020(p,float2(width*.04,.071),float2(width*.03,.005),aa));
                shade=lerp(shade,float3(.82,.30,.055),windows*.86);
                return float4(shade,mask*lerp(.80,.96,nearLayer));
            }
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing four immutable Island020 silhouettes; color/haze/terrace/window shading stays strictly inside their original mask.
// Array Formats: Four explicit shade calls with unchanged headings/widths/elevations/variants; Low omits grain. Same two windows per mask, each with one extra recess BoxMask; no extra noise fields.
// Dependencies/Pointers: Existing BoxMask020 and exact preserved Island020 geometry functions. No additional island/world/bridge/Light/instance or loading representation is introduced.

            // Optional angular-only falls for the four existing sky masks; no world-space anchors.
            float _OracleReflectionStrength020;
            float4 SkyWaterfall020(float3 d,float2 h,float2 heading,float islandWidth,float elevation,
                float sideFraction,float dropSpan,float seed,float pureWater)
            {
                float side=h.x*heading.y-h.y*heading.x;
                float rootSide=islandWidth*sideFraction;
                float source=elevation-.085*pow(1.0-saturate(abs(sideFraction)),.65);
                float down=(source-d.y)/dropSpan;
                float width=islandWidth*.065;
                float aa=max(fwidth(side),.0005);
                // Coarse angular rejection precedes noise; this adds no transparent draw or full-world sheet.
                if(dot(h,heading)<.90 || down<-.025 || down>1.0 || abs(side-rootSide)>width*6.0+.003) return 0;
                float phase=_PresentationClock020*(6.28318530718/4096.0);
                float bend=sin(phase*512.0+seed*6.28318530718)*.0022*down*down;
                float offset=side-rootSide-bend;
                float core=1.0-smoothstep(width*.40,width+aa,abs(offset));
                float tail=1.0-smoothstep(.24,1.0,down);
                float root=smoothstep(-.015,.018,down);
                float flow=.68+.32*sin(6.28318530718*(down*6.0-_PresentationClock020*.75+seed));
                float streak=.72;
                if(_Quality020>.5)
                {
                    float2 drift=float2(cos(phase*1024.0),sin(phase*1024.0));
                    float rateLimit=.42/max(aa/max(width,.0001),.001);
                    streak=.48+.52*LandingNoise020(float2(offset/max(width,.0001)*min(2.9,rateLimit)+seed*13.0,down*1.8)+drift*.31);
                    if(_Quality020>1.5) streak=streak*.78+LandingNoise020(float2(offset/max(width,.0001)*min(7.7,rateLimit)+seed*7.0,down*2.3)+drift.yx*.23)*.22;
                }
                float mistDistance=offset/max(width*3.6,.0001);
                float mist=exp2(-mistDistance*mistDistance*2.4)*smoothstep(.30,.65,down)*(1.0-smoothstep(.65,1.0,down))*.12;
                float alpha=saturate((core*flow*streak*tail*.49+mist)*root);
                float3 color=lerp(float3(.36,.055,.075),float3(.13,.55,.63),pureWater);
                color+=lerp(float3(.37,.13,.10),float3(.40,.58,.62),pureWater)*
                    (saturate(_OracleReflectionStrength020)*streak*flow*.13);
                return float4(color,alpha);
            }
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing normalized sky directions only; source height exactly follows the current Island020 tapered underside at its fixed side fraction.
// Array Formats: Four explicit angular calls, Low two calls/no noise; no world coordinates, objects, tiles, textures or extra passes. Cloud layers still composite afterward.
// Dependencies/Pointers: Optional source-pinned addendum to current sky, independently frozen from the earlier two-shader refinement. Existing held clock/quality/noise; global reflection scalar outside UnityPerMaterial. Default sky calls use red storm runoff; pureWater1 preserves cyan independently.

            half4 Frag(V i):SV_Target
            {
                float3 d=normalize(i.direction);
                float2 h=d.xz*rsqrt(max(dot(d.xz,d.xz),.000001));
                float p=_PresentationClock020*(6.28318530718/4096.0);
                float cloudPhase=_CloudSeed020*6.28318530718;
                float2 drift=float2(cos(p*97+cloudPhase),sin(p*97+cloudPhase))*.30+
                    float2(sin(p*137+cloudPhase*1.71),cos(p*137+cloudPhase*1.71))*.14;
                float3 nightColor=lerp(float3(.027,.019,.067),float3(.004,.009,.028),smoothstep(.015,.96,d.y));
                float3 dayColor=lerp(float3(.09,.15,.26),float3(.18,.29,.43),smoothstep(.015,.96,d.y));
                float3 color=lerp(nightColor,dayColor,saturate(_CycleDaylight020));
                float horizon=exp2(-abs(d.y-.035)*13);
                float stormBreak=.58+.42*saturate(sin(h.x*2.3+h.y*1.7+p*3));
                float visibleStorm=saturate(_CycleStorm020)*(1-saturate(_CycleTwilight020));
                color+=float3(.24,.012,.075)*horizon*stormBreak*visibleStorm;
                color+=float3(.018,.008,.004)*horizon*saturate(_CycleTwilight020);
                float3 moonDirection=normalize(_CycleSunDirection020.xyz);
                float sunDistance=length(d-moonDirection),sunAA=max(fwidth(sunDistance),.0005);
                float sunDisc=1-smoothstep(.020-sunAA,.020+sunAA,sunDistance);
                float sunHalo=exp2(-sunDistance*sunDistance*170.0)*.006+exp2(-sunDistance*sunDistance*32.0)*.0015;
                color+=float3(.98,.87,.69)*(sunDisc*.10+sunHalo)*saturate(_CycleDaylight020);
                float moonDistance=length(d-moonDirection),moonAA=max(fwidth(moonDistance),.0005);
                float disc=1-smoothstep(.026-moonAA,.026+moonAA,moonDistance);
                float nightWeight=saturate(_CycleNight020)*(1-saturate(_CycleTwilight020));
                float moonWeight=nightWeight;
                color+=float3(.24,.027,.040)*(disc*.70+exp2(-moonDistance*moonDistance*180)*.08)*smoothstep(0,.06,d.y)*moonWeight;
                // One wrap-safe7.002-second breath; peak is58% of the original .45 source peak.
                float auroraOpacity=(.06525+.19575*(.5+.5*sin(p*585)))*saturate(_CycleAurora020);
                float curtain;
                color+=Aurora020(d,h,drift,0,.13,auroraOpacity,curtain);
                if(_Quality020>.5)
                {
                    float second;
                    color+=Aurora020(d,h,-drift+float2(7.3,4.1),.145,1.21,auroraOpacity,second)*.60;
                    curtain=max(curtain,second*.60);
                }
                if(_Quality020>.5)
                {
                    float3 q=d*191,cell=floor(q);
                    float seed=frac(sin(dot(cell,float3(3.1,7.7,11.3)))*1871.73);
                    float radius=length(frac(q)-.5),aa=max(fwidth(radius),.012);
                    float star=(1-smoothstep(.025,.07+aa,radius))*step(.998,seed)*smoothstep(.23,.67,d.y)*nightWeight;
                    color+=float3(.26,.35,.54)*star*(1-saturate(curtain)*.7);
                }
                float3 flareOffset=d-moonDirection;
                float3 flareAxisRaw=_LensFlareAxis020.xyz-moonDirection*dot(_LensFlareAxis020.xyz,moonDirection);
                float flareAxisLength2=dot(flareAxisRaw,flareAxisRaw);
                float3 flareFallback=abs(moonDirection.y)<.9?float3(0,1,0):float3(1,0,0);
                flareFallback-=moonDirection*dot(flareFallback,moonDirection);
                float3 flareAxis=flareAxisLength2>1e-6?flareAxisRaw*rsqrt(flareAxisLength2):normalize(flareFallback);
                float flareAlong=dot(flareOffset,flareAxis);
                float flareAcross=sqrt(max(dot(flareOffset,flareOffset)-flareAlong*flareAlong,0));
                float flareAA=max(fwidth(flareAcross),.00035);
                float flareStreak=(1-smoothstep(.0018-flareAA,.0042+flareAA,flareAcross))*exp2(-flareAlong*flareAlong*330);
                float flareGhostA=exp2(-((flareAlong-.047)*(flareAlong-.047)*1150+flareAcross*flareAcross*25000));
                float flareGhostB=exp2(-((flareAlong+.078)*(flareAlong+.078)*690+flareAcross*flareAcross*16500));
                float flareShape=flareStreak*.008+flareGhostA*.0032+flareGhostB*.0021;
                float sunCloudOpacity=0;
                [branch] if(_LensFlareVisibility020>.001 && flareShape>.00001 && _CloudVisibility020>.5)
                {
                    float sunHorizontal=rsqrt(max(dot(moonDirection.xz,moonDirection.xz),.000001));
                    float2 sunH=moonDirection.xz*sunHorizontal;
                    float4 sunFar=Bank020(moonDirection,sunH,drift*.45,3.4,.095,.055,.86,moonDirection);
                    float4 sunSea=Bank020(moonDirection,sunH,drift*.23+float2(13.4,2.8),7.1,-.085,.085,.96,moonDirection);
                    float4 sunMiddle=0;
                    if(_Quality020>.5) sunMiddle=Bank020(moonDirection,sunH,-drift*.31+float2(4.7,9.1),5.6,.010,.067,.62,moonDirection);
                    sunCloudOpacity=1-(1-sunFar.a)*(1-sunMiddle.a)*(1-sunSea.a);
                }
                float flareEnergy=saturate(_LensFlareVisibility020)*saturate(_CycleDaylight020)*(1-saturate(sunCloudOpacity));
                color+=float3(1.0,.72,.44)*flareShape*flareEnergy;
                float4 farCloud=Bank020(d,h,drift*.45,3.4,.095,.055,.86,moonDirection);
                color=lerp(color,farCloud.rgb,farCloud.a);
                // Same four original masses; shading never changes their geometry mask or angular placement.
                float4 farA=IslandShade020(d,h,normalize(float2(.56,.83)),.067,.125,.15,0);
                float4 farB=IslandShade020(d,h,normalize(float2(-.80,-.60)),.047,.113,1.1,0);
                color=lerp(color,farA.rgb,farA.a); color=lerp(color,farB.rgb,farB.a);
                if(_Quality020>.5)
                {
                    float4 fallA=SkyWaterfall020(d,h,normalize(float2(.56,.83)),.067,.125,.72,.13,.17,0);
                    float4 fallB=SkyWaterfall020(d,h,normalize(float2(-.80,-.60)),.047,.113,-.64,.12,.53,0);
                    color=lerp(color,fallA.rgb,fallA.a); color=lerp(color,fallB.rgb,fallB.a);
                }
                float4 nearA=IslandShade020(d,h,normalize(float2(.94,-.34)),.084,.105,.7,1);
                float4 nearB=IslandShade020(d,h,normalize(float2(-.91,.41)),.058,.117,.35,1);
                color=lerp(color,nearA.rgb,nearA.a); color=lerp(color,nearB.rgb,nearB.a);
                float4 fallC=SkyWaterfall020(d,h,normalize(float2(.94,-.34)),.084,.105,-.70,.16,.79,0);
                float4 fallD=SkyWaterfall020(d,h,normalize(float2(-.91,.41)),.058,.117,.76,.14,.31,0);
                color=lerp(color,fallC.rgb,fallC.a); color=lerp(color,fallD.rgb,fallD.a);
                if(_Quality020>.5)
                {
                    float4 middle=Bank020(d,h,-drift*.31+float2(4.7,9.1),5.6,.010,.067,.62,moonDirection);
                    color=lerp(color,middle.rgb,middle.a);
                }
                float4 sea=Bank020(d,h,drift*.23+float2(13.4,2.8),7.1,-.085,.085,.96,moonDirection);
                color=lerp(color,sea.rgb,sea.a);
                return half4(max(color,0),1);
            }
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Normalized world sky directions with four preserved masks, two broad aurora lobes and three analytic cloud banks; Low uses one lobe/two banks.
// Array Formats: Existing clock/quality pair; one seeded cloud hash stream, bounded cloud-on-sun samples only within the flare mask, and three scalar/vector flare controls. One sky pass; no world instances/materials/textures/RTs/lights.
// Dependencies/Pointers: Existing key sky direction, held4096s clock, and atmosphere-provided deterministic seed/visibility. Flare uses one tangent streak/two ghosts, daylight and existing cloud-bank opacity; all physical geometry/cascade occlusion remains external. Root owns actual import/native comparison and GPU measurement.

            ENDHLSL
        }
    }
    Fallback Off
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Seamless normalized world sky directions, Y-up; curtains are gated above horizon, four exact existing nonphysical island/tower masks remain unchanged.
// Array Formats: Existing clock/quality pair, five cycle weights, seeded cloud controls, and bounded flare controls; fixed analytic strands/stepped puffs/two recessed windows per island. No new textures, RTs, passes, compute, raymarch or world objects.
// Dependencies/Pointers: Existing scoped held4096s/reduced-motion clock and incoming main light. Aurora keeps its exact 585 harmonic (4096/585=7.0017 seconds), prior softened pulse and 42.2/29.9-second drift, gated by the same night fade. Seeded clouds and projected flare are shader-only; root owns actual import, native comparison, and GPU/browser measurements.
