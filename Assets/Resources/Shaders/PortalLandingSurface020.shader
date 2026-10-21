Shader "OdysseyOracle/PortalLandingSurface020"
{
    Properties
    {
        _PresentationClock020("Held presentation clock",Float)=0
        _Quality020("Bounded wet detail quality",Range(0,2))=1
        _CloudShadowState020("Seeded projected ground shade",Vector)=(0,0.5,0,0)
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        CBUFFER_START(UnityPerMaterial)
        float _PresentationClock020,_Quality020;
        float4 _CloudShadowState020;
        float4 _TorchAnchors020[8];
        CBUFFER_END
        // Global is owned by GraphicsPreferences.Reflections020, not shadowed by a material property.
        float _OracleReflectionStrength020;
        float3 _LightDirection,_LightPosition;
        struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;float4 color:COLOR;float2 detail:TEXCOORD0;};
        struct V {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;half4 color:COLOR;float2 detail:TEXCOORD2;half fog:TEXCOORD3;float4 screenShadow:TEXCOORD4;float2 motion:TEXCOORD5;};
        float3 TorchVisualAnchor020(int index)
        {
            float3 visualPosition=_TorchAnchors020[index].xyz;
            float floorY=index<2||index>=6?0.0:index<4?3.25:6.0;
            visualPosition.y=floorY+(visualPosition.y-floorY)*1.5;
            return visualPosition;
        }
        float3 DeformTorchVertex020(A a,out float role,out float fade,out float seed)
        {
            float3 world=TransformObjectToWorld(a.positionOS.xyz);role=0;fade=1;seed=0;
            float tag=a.color.a;
            if(tag<.24){role=1;seed=saturate((tag-.05)*7.1428571);}
            else if(tag<.49){role=2;seed=saturate((tag-.30)*7.1428571);}
            else if(tag<.75){role=3;seed=saturate((tag-.56)*7.1428571);}
            else return world;

            float clock=max(0,_PresentationClock020);
            float3 pivot=TransformObjectToWorld(a.normalOS);
            float wrapPhase=clock*(6.28318530718/4096.0);
            if(role<1.5)
            {
                float height=saturate((a.positionOS.y-a.detail.y)/1.62);
                float phaseX=wrapPhase*3325.0+seed*6.28318530718;
                float phaseZ=wrapPhase*2427.0+seed*3.4;
                float phaseY=wrapPhase*4557.0+height*2.2;
                world.x+=sin(phaseX+height*2.6)*(.035*height);
                world.z+=cos(phaseZ+height*1.8)*(.018*height);
                world.y+=sin(phaseY)*(.012*height);
                return world;
            }

            float period=role<2.5?4.0:8.0;
            float age=frac(clock/period+seed*.37);
            if(role<2.5)
            {
                fade=smoothstep(0,.08,age)*(1-smoothstep(.72,.94,age));
                float scale=lerp(.92,.08,age);
                world=pivot+(world-pivot)*scale;
                world.y+=lerp(.55,.82,seed)*age;
                world.x+=sin(wrapPhase*1630.0+seed*6.2831853+age*4)*(.11*age);
                world.z+=cos(wrapPhase*1368.0+seed*9.1+age*3)*(.075*age);
            }
            else
            {
                fade=smoothstep(0,.14,age)*(1-smoothstep(.70,.96,age));
                float scale=lerp(.48,.88,age);
                world=pivot+(world-pivot)*scale;
                world.y+=lerp(.82,1.20,seed)*age;
                world.x+=sin(wrapPhase*750.0+seed*6.2831853+age*2)*(.16*age);
                world.z+=cos(wrapPhase*606.0+seed*8.2+age*2)*(.12*age);
            }
            if(fade<=.015)world=pivot;
            return world;
        }
        V Vert(A a)
        {
            float role,fade,seed;V o;o.world=DeformTorchVertex020(a,role,fade,seed);o.positionCS=TransformWorldToHClip(o.world);
            o.normal=role>0?float3(0,1,0):TransformObjectToWorldNormal(a.normalOS);o.color=a.color;o.detail=a.detail;o.motion=float2(role,fade);
            o.fog=ComputeFogFactor(o.positionCS.z);o.screenShadow=ComputeScreenPos(o.positionCS);return o;
        }
        float StoneHash020(float2 cell)
        {
            float2 a=frac(cell*float2(.173,.137)+float2(.319,.271));
            float v=dot(a,a.yx+float2(11.71,7.13));
            return frac((a.x+v*.173)*(a.y+v+.37)*7.91);
        }
        float StoneNoise020(float2 coordinate)
        {
            float2 cell=floor(coordinate),f=frac(coordinate),s=f*f*f*(f*(f*6-15)+10);
            return lerp(lerp(StoneHash020(cell),StoneHash020(cell+float2(1,0)),s.x),
                lerp(StoneHash020(cell+float2(0,1)),StoneHash020(cell+1),s.x),s.y);
        }
        void FloorCrackCoverage020(float2 seamKey,float2 orientationSalt,float along,float verticalSeam,out float coverage,out float widthSeed)
        {
            coverage=0;
            float weightedWidth=0,widthWeight=0,fallbackSeed=0;
            [unroll] for(int startOffset=0;startOffset<3;startOffset++)
            {
                if(verticalSeam>.5&&startOffset>0)continue;
                float seed=StoneHash020(seamKey+float2(0,-startOffset)+orientationSalt);
                if(startOffset==0)fallbackSeed=seed;
                float runLength=verticalSeam>.5?1.0:1+floor(saturate((seed-.90)*10.0)*2.999);
                float admitted=step(.90,seed)*step((float)startOffset+.5,runLength);
                float runPosition=along+startOffset;
                float cap=smoothstep(0,.10,runPosition)*(1-smoothstep(runLength-.10,runLength,runPosition));
                float candidate=admitted*cap;
                coverage=max(coverage,candidate);
                weightedWidth+=seed*candidate;widthWeight+=candidate;
            }
            widthSeed=widthWeight>.0001?weightedWidth/widthWeight:fallbackSeed;
        }
        float FloorCrackRed020(float2 worldPosition,float aa,float patch)
        {
            // Comparisons reject NaN/Inf before floor/frac/hash; the generous finite domain contains the complete hub.
            if(!all(worldPosition>=-131072.0)||!all(worldPosition<=131072.0)||!(aa>=0)||!(aa<=16)||!(patch>=0)||!(patch<=1))return 0;
            float2 coordinate=worldPosition/float2(1.4,.8);
            float row=floor(coordinate.y),alongH=frac(coordinate.x),alongV=frac(coordinate.y);
            float verticalCoordinate=coordinate.x+row*.5;
            float verticalFraction=frac(verticalCoordinate);
            float distanceH=min(alongV,1-alongV)*.8,distanceV=min(verticalFraction,1-verticalFraction)*1.4;
            float coverageH,widthH,coverageV,widthV;
            FloorCrackCoverage020(float2(floor(coordinate.y+.5),floor(coordinate.x)),float2(17.9,41.3),alongH,0,coverageH,widthH);
            FloorCrackCoverage020(float2(floor(verticalCoordinate+.5),row),float2(41.3,17.9),alongV,1,coverageV,widthV);
            float edgeOffset=(patch-.5)*.012;
            float horizontal=coverageH*(1-smoothstep(.012,.052+.038*widthH+aa,distanceH+edgeOffset));
            float vertical=coverageV*(1-smoothstep(.012,.052+.038*widthV+aa,distanceV+edgeOffset));
            float2 junctionIndex=floor(worldPosition/float2(.7,.8)+.5);
            float2 junctionDelta=worldPosition-junctionIndex*float2(.7,.8);
            float2 junctionKey=float2(floor((junctionIndex.x+junctionIndex.y)*.5),junctionIndex.y);
            float junctionHash=StoneHash020(junctionKey+float2(59.7,13.9));
            float junction=(1-smoothstep(.012,.095,length(junctionDelta)))*step(.985,junctionHash);
            junction*=1-smoothstep(.012,.052+.038*junctionHash+aa,min(distanceH,distanceV)+edgeOffset);
            return saturate(max(max(horizontal,vertical),junction)*(.52+.48*patch));
        }
        float CloudShadowDensity020(float3 worldPosition,float3 keyDirection)
        {
            float strength=saturate(_CloudShadowState020.x);
            if(strength<=0||keyDirection.y<=.10)return 0;
            float seed=saturate(_CloudShadowState020.y);
            float2 wind=_CloudShadowState020.zw;
            float windLength2=dot(wind,wind);
            wind=windLength2>1e-6?wind*rsqrt(windLength2):float2(cos(seed*6.2831853),sin(seed*6.2831853));
            float phase=_PresentationClock020*(6.28318530718/4096.0);
            float driftDistance=sin(phase+seed*6.2831853)*3.0+sin(phase*2.0+seed*2.173)*.8;
            float cloudHeight=18.0;
            float castDistance=min(64.0,cloudHeight/max(keyDirection.y,.28));
            float2 cloudPoint=worldPosition.xz+keyDirection.xz*castDistance+wind*driftDistance;
            float noise=StoneNoise020(cloudPoint*.024+float2(seed*17.31,seed*31.17));
            return smoothstep(.45,.70,noise)*strength;
        }
        float3 StoneNormal020(float3 position,float3 normal,float height)
        {
            float3 dx=ddx(position),dy=ddy(position),a=cross(dy,normal),b=cross(normal,dx);
            float determinant=dot(dx,a);
            float3 gradient=(a*ddx(height)+b*ddy(height))*(sign(determinant)/max(abs(determinant),.000001));
            return normalize(normal-clamp(gradient,-.19,.19));
        }
        float2 TaggedFace020(float3 position,float3 normal)
        {
            // Horizontal top faces contain no building-yaw channel; root accepted this worldXZfallback.
            if(abs(normal.y)>.70)return position.xz;
            float3 tangent=float3(normal.z,0,-normal.x);
            tangent*=rsqrt(max(dot(tangent,tangent),.0001));
            float3 bitangent=cross(normal,tangent);
            return float2(dot(position,tangent),dot(position,bitangent));
        }
        float3 TaggedStone020(float3 position,float3 normal,float3 baseColor,float slate,out float height)
        {
            float2 face=TaggedFace020(position,normal);
            float2 size=lerp(float2(.55,.28),float2(.46,.24),slate);
            float row=floor(face.y/size.y);
            float2 tile=float2(face.x/size.x+row*.5,face.y/size.y),cell=floor(tile),uv=frac(tile);
            float value=StoneHash020(cell+float2(17.7+slate*11.3,5.9));
            float2 edge=min(uv,1-uv)*size;
            float seam=min(edge.x,edge.y),aa=max(fwidth(seam),.0005);
            float footprint=max(fwidth(face.x),fwidth(face.y));
            float courseFade=1-smoothstep(.018,.065,footprint);
            float fineFade=0,pixel=.5,patch=0;
            if(_Quality020>.5)
            {
                fineFade=1-smoothstep(.008,.028,footprint);
                pixel=StoneHash020(floor(face/.035)+float2(4.7,19.3+slate*7.1));
                patch=StoneHash020(floor(face/float2(1.65,.84))+float2(23.1,7.7));
            }
            float width=lerp(.0065,.0045,slate)+step(.75,pixel)*.0025*fineFade;
            float groove=(1-smoothstep(width,width+.004+aa,seam))*courseFade;
            float chips=(1-smoothstep(width+.002,width+.035,seam))*step(.78,pixel)*fineFade;
            float lip=smoothstep(width+.004,width+.014,seam)*(1-smoothstep(width+.014,width+.033,seam))*courseFade;
            float tone=lerp(1,lerp(.82+.32*value,.88+.28*value,slate),courseFade);
            tone*=1+(pixel-.5)*.13*fineFade;
            float3 color=baseColor*tone*lerp(float3(1,1,1),float3(.94,.98,1.08),slate);
            color*=1-groove*.58-chips*.08;
            color+=baseColor*lip*(.045+.025*value);
            float moss=step(.83,patch)*smoothstep(.60,.95,pixel)*(1-smoothstep(.014,.060,seam))*fineFade;
            moss*=lerp(.16,.07,slate);
            color=lerp(color,baseColor*float3(.35,.55,.26),moss);
            height=((1-groove)*lerp(.0035,.0025,slate)+lip*.0008-chips*.0007)*courseFade;
            return saturate(color);
        }
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Orthonormal world-metre wall faces; horizontal roofs use accepted worldXZfine projection while real geometry courses retain builder yaw.
// Array Formats: Negative Detail.x tags reuse existing scalar metadata; Stone0.55x0.28m/Slate0.46x0.24m courses,1new cell hash on Low/3new hashes above Low. No texture/buffer/object allocation.
// Dependencies/Pointers: Existing StoneHash020/StoneNormal020/quality; millimetre virtual height changes lighting only, never geometry/depth/alpha. Sparse moss capped0.16Stone/0.07Slate blend; detailed normals are omitted on Low.

        half4 Frag(V i):SV_Target
        {
            float3 geometricNormal=normalize(i.normal),view=GetWorldSpaceNormalizeViewDir(i.world);
            float floorMask=step(.5,i.detail.y)*smoothstep(.75,.96,geometricNormal.y)*step(.95,i.color.a);
            clip(i.motion.y-.015);
            float row=floor(i.world.z/.8);
            float2 tile=float2(i.world.x/1.4+row*.5,i.world.z/.8),cell=floor(tile),cellUv=frac(tile);
            float cellValue=StoneHash020(cell+float2(3.1,7.7));
            float2 edge=min(cellUv,1-cellUv);
            float seamDistance=min(edge.x*1.4,edge.y*.8);
            float aa=max(fwidth(seamDistance),.001);
            float patch=StoneNoise020(i.world.xz*.73+float2(4.3,1.7));
            float chips=_Quality020>.5?StoneNoise020(i.world.xz*10.7+float2(2.4,9.3)):cellValue;
            float width=.014+.017*chips;
            float groove=(1-smoothstep(width,width+.019+aa,seamDistance))*floorMask;
            float chippedEdge=(1-smoothstep(.025,.105,seamDistance))*smoothstep(.53,.86,chips)*floorMask;
            float redPool=0;
            if(floorMask>0&&floorMask<=1)redPool=FloorCrackRed020(i.world.xz,aa,patch)*floorMask;
            float3 n=geometricNormal;
            if(_Quality020>.5&&floorMask>.5)
                n=StoneNormal020(i.world,n,(1-groove*.90-chippedEdge*.48)*.024+patch*.004);
            float2 face=abs(geometricNormal.y)>.5?i.world.xz:abs(geometricNormal.x)>.5?i.world.zy:i.world.xy;
            float roughValue=StoneHash020(floor(face*2.7));
            float grainFade=1-saturate(max(fwidth(face.x),fwidth(face.y))*3);
            float grain=lerp(1,.89+.22*roughValue,grainFade);
            float floorValue=.73+cellValue*.40;
            float3 albedo=i.color.rgb*grain*lerp(1,floorValue*.73,floorMask);
            albedo*=1-groove*.54-chippedEdge*.17;
            float lip=smoothstep(.028,.065,seamDistance)*(1-smoothstep(.065,.12,seamDistance))*floorMask;
            albedo+=i.color.rgb*lip*(.035+.07*chips);
            albedo=lerp(albedo,float3(.083,.0045,.013),redPool*.85);
            // Explicit intrinsic tags; floor shading wins before any new tagged detail.
            float stoneTag=1-step(.125,abs(i.detail.x+1.0));
            float slateTag=1-step(.125,abs(i.detail.x+2.0));
            if(floorMask==0&&max(stoneTag,slateTag)>.5)
            {
                float height;
                albedo=TaggedStone020(i.world,geometricNormal,i.color.rgb,slateTag,height);
                if(_Quality020>.5)n=StoneNormal020(i.world,geometricNormal,height);
            }
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing opaque fragment world position/normal; exact floor mask takes priority over negative intrinsic tags.
// Array Formats: Detail.x-1Stone/-2Slate decoded within nonoverlapping0.125tolerances; Detail.y, positive emission and all buffers unchanged.
// Dependencies/Pointers: Builder reserves Detail.x -1/-2 only for stone/slate. Ordinary, floor, and positive-emission vertices keep their prior albedo path; alpha-tagged stair particles use the explicit motion branch below.

            #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
            Light key=GetMainLight(i.screenShadow);
            #else
            Light key=GetMainLight(TransformWorldToShadowCoord(i.world));
            #endif
            float p=_PresentationClock020*6.28318530718;
            float flicker=.90+.065*sin(p*1.75+i.world.x*.7)+.035*sin(p*2.25+i.world.z*.9);
            float3 illumination=max(SampleSH(n),float3(.085,.085,.13));
            float cloudDensity=CloudShadowDensity020(i.world,key.direction)*floorMask;
            float cloudTransmission=1-.30*cloudDensity;
            illumination+=key.color*saturate(dot(n,key.direction))*key.shadowAttenuation*cloudTransmission;
            float stairWarmth=0;
            if(floorMask>.5&&abs(i.world.x)<=10.5&&i.world.z>=18&&i.world.z<=50)
            {
                [unroll] for(int t=0;t<6;t++)
                {
                    float3 delta=TorchVisualAnchor020(t)-i.world;
                    float distance2=dot(delta,delta),falloff=saturate(1-distance2/196.0);
                    float pulse=.94+.06*sin(p*(5530.0/4096.0)+t*2.173+_TorchAnchors020[t].x*.13);
                    stairWarmth=max(stairWarmth,falloff*falloff*saturate(_TorchAnchors020[t].w)*pulse);
                }
            }
            illumination+=float3(1,.34,.10)*(.34*stairWarmth);
            float3 color=albedo*illumination;
            float role=i.motion.x,tag=i.color.a;
            float seed=role<1.5?saturate((tag-.05)*7.1428571):role<2.5?saturate((tag-.30)*7.1428571):saturate((tag-.56)*7.1428571);
            if(role>0&&role<2.5)
            {
                float firePulse=role<1.5?.84+.16*sin(p*(12698.0/4096.0)+seed*6.2831853+i.world.y*1.4):.80+.20*sin(p*(19251.0/4096.0)+seed*8.1);
                color=i.color.rgb*max(0,i.detail.x)*firePulse*.68;
            }
            else color+=i.color.rgb*max(0,i.detail.x)*flicker*.68;
            float reflection=role>0?0:saturate(_OracleReflectionStrength020);
            if(reflection>.001)
            {
                float wet=(.15+patch*.30+redPool*.62)*reflection;
                float3 mainHalf=normalize(key.direction+view+float3(0,.0001,0));
                float glossPower=lerp(10,_Quality020>.5?lerp(48,96,redPool):28,floorMask);
                float glossWeight=lerp(.007,.31+redPool*.66,floorMask);
                color+=key.color*key.shadowAttenuation*pow(saturate(dot(n,mainHalf)),glossPower)*glossWeight*reflection;
                if(floorMask>.5)
                {
                    float fresnel=.08+.92*pow(1-saturate(dot(n,view)),4);
                    float3 reflected=reflect(-view,n);
                    float tint=StoneNoise020(reflected.xz*2.4+float2(6.2,1.3));
                    float3 palette=lerp(float3(.020,.033,.094),float3(.017,.123,.105),tint);
                    palette=lerp(palette,float3(.25,.015,.038),redPool*.64);
                    color+=palette*wet*fresnel*(_Quality020>.5?1:.30);
                    if(_Quality020>.5)
                    {
                        float3 tangent=normalize(float3(view.x,0,view.z)+float3(.00001,0,.00002));
                        float3 bitangent=normalize(cross(n,tangent));
                        // Same eight real anchors; finite rough directional glints, not point lights or occluded reflections.
                        [unroll] for(int t=0;t<8;t++)
                        {
                            float3 delta=TorchVisualAnchor020(t)-i.world;
                            float distance2=max(dot(delta,delta),.25),range=saturate(1-distance2/400);
                            if(range>0)
                            {
                                float3 direction=delta*rsqrt(distance2),halfVector=normalize(direction+view+float3(0,.0001,0));
                                float along=dot(halfVector,tangent),across=dot(halfVector,bitangent);
                                float lobe=exp2(-along*along*24-across*across*96)*pow(saturate(dot(n,halfVector)),6);
                                float attenuation=_TorchAnchors020[t].w*range*range/(1+distance2*.035);
                                float broken=.48+.52*smoothstep(.15,.86,patch);
                                float glint=lobe*5.2*broken+pow(saturate(dot(n,direction)),2)*.025;
                                color+=float3(1.15,.31,.045)*attenuation*wet*glint*flicker;
                            }
                        }
                    }
                }
            }
            color*=i.motion.y;
            return half4(MixFog(max(color,0),i.fog),1);
        }
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing world-metre opaque surface; chipped seams and stone normals remain fragment-only. Only alpha-tagged stair particles deform in the held clock and share that deformation with depth/caster passes.
// Array Formats: Same material clock/quality/global reflection scalar/eight anchors plus one bounded cloud-state float4; one seeded noise sample per eligible floor pixel and no new buffer/texture.
// Dependencies/Pointers: Existing URP main-light direction and cascaded shadow attenuation remain intact; a <=13.8% quality-scaled seeded moving shade multiplies only floor direct light. Walls, fixed structure fragments, depth/caster passes and probe/global owners are unchanged; this is approximate projection, not a new cascade or exact sky/cloud match. Native image/performance comparison remains root-owned.

        struct ShadowV020 {float4 positionCS:SV_POSITION;float fade:TEXCOORD0;float role:TEXCOORD1;};
        ShadowV020 ShadowVert(A a)
        {
            float role,fade,seed;float3 p=DeformTorchVertex020(a,role,fade,seed);
            float3 n=role>0?float3(0,1,0):TransformObjectToWorldNormal(a.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
            float3 lightDirection=normalize(_LightPosition-p);
            #else
            float3 lightDirection=_LightDirection;
            #endif
            ShadowV020 o;o.positionCS=ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(p,n,lightDirection)));o.fade=fade;o.role=role;return o;
        }
        half4 ShadowFrag(ShadowV020 i):SV_Target{clip(i.fade-.015);if(i.role>1.5)clip(-1);return 0;}
        half4 DepthFrag(V i):SV_Target{clip(i.motion.y-.015);return 0;}
        half4 NormalsFrag(V i):SV_Target
        {
            clip(i.motion.y-.015);
            float3 n=normalize(i.normal);
            #if defined(_GBUFFER_NORMALS_OCT)
            return half4(PackFloat2To888(saturate(PackNormalOctQuadEncode(n)*.5+.5)),0);
            #else
            return half4(n,0);
            #endif
        }
        ENDHLSL
        Pass
        {
            Name "HubForward020" Tags {"LightMode"="UniversalForward"}
            ZWrite On Cull Back
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster" Tags {"LightMode"="ShadowCaster"}
            ZWrite On ZTest LEqual ColorMask 0 Cull Back
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly" Tags {"LightMode"="DepthOnly"}
            ZWrite On ColorMask R Cull Back
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment DepthFrag
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals" Tags {"LightMode"="DepthNormalsOnly"}
            ZWrite On Cull Back
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment NormalsFrag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            ENDHLSL
        }
    }
    Fallback Off
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Builder world-metre mesh POSITION/NORMAL/COLOR(float4)/TEXCOORD0(float2); static Detail.x is emission and Detail.y is floor mask. Tagged stair vertices encode their world pivot in NORMAL, role/phase in COLOR.a, and flame source height in Detail.y.
// Array Formats: Same vertex stream and borrowed material; one extra TEXCOORD5 varying carries particle role/fade. The eight anchor vector and held 4096-second clock are reused; no texture, buffer, shader variant, or render pass is added.
// Dependencies/Pointers: The builder tags only six stair fixtures and supplies swept render bounds. Forward, depth, depth-normal and shadow vertices share one bounded deformation; ember/smoke fade clips consistently, while only the existing flame voxel and fixed world geometry cast shadows. Six existing anchors add a max-combined, unshadowed diffuse approximation on floor fragments; the eight reflection glints retain their existing global preference gate. Cloud attenuation and cloud/floor domains are unchanged. Root owns shader compilation, native images and performance acceptance.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Stable world-metre independent horizontal/vertical seams and shared0.7x0.8m T-junction lattice.
// Array Formats: Existing scalar material/varying ABI only; H3/V1/junction1 seeded hash calls, bounded three-start run loop; no texture/buffer/object/state.
// Dependencies/Pointers: Final red shading is a union of separately distance/width-masked seams, so a vertical cap cannot suppress horizontal continuation. Finite-input guard returns zero red, preserving opaque alpha/geometry/depth/physics, cloud/Advanced shader code and held clock. Baked paver variation and native visual/performance acceptance remain separate.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing world-metre opaque hub mesh; alpha-tagged stair flame/ember/smoke vertices pivot and deform in the shared held-clock domain.
// Array Formats: Existing vertex ABI/material, eight-anchor float4 array and scalar presentation clock; no particle buffer, texture, render pass, or per-frame state.
// Dependencies/Pointers: Six stair fixtures reuse their admitted anchor positions for a bounded warm floor diffuse approximation. Static bridge fixtures, floor/collider geometry, cloud shading, directional key and global reflection ownership remain unchanged; native shader and image acceptance is parent-owned.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing eight hub fixtures; only upper presentation Y grows1.5 about each original support plane. Grounded bases/caps and allXZ/yaw remain exact.
// Array Formats: Same six23-command stair fixtures and two7-command bridge fixtures,152total; no extra commands, runtime buffers, lights or frame allocations.
// Dependencies/Pointers: Fixture providers/anchors/colliders remain authoritative. Cold geometry raises post/bowl/cage/fire and tagged source heights; the mesh writer retains correct center-as-Normal pivots and swept bounds. Shared shader projects the same visual light centers without raising brightness; clock/capture/cleanup/farm/footstep ownership remains unchanged. Native visual/HLSL/Burst acceptance is root-owned.
