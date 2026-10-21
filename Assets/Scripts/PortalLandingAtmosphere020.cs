using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace OdysseyOracle
{
    [DisallowMultipleComponent]
    public sealed partial class PortalLandingAtmosphere020:MonoBehaviour
    {
        static readonly int ClockId=Shader.PropertyToID("_PresentationClock020"),QualityId=Shader.PropertyToID("_Quality020"),
            TorchId=Shader.PropertyToID("_TorchAnchors020"),ModeId=Shader.PropertyToID("_Mode020"),InstancedId=Shader.PropertyToID("_Instanced020"),
            CountId=Shader.PropertyToID("_Count020"),WindId=Shader.PropertyToID("_Wind020");
        static PortalLandingAtmosphere020 globalLease;
        static readonly Color SkyColor=new Color(.27f,.19f,.29f),EquatorColor=new Color(.21f,.13f,.22f),GroundColor=new Color(.10f,.06f,.085f),FogColor=new Color(.062f,.031f,.074f);
        const float FogDensity=.0027f;
        const int RainCapacity=PortalLandingWeatherRules020.RainCapacity,SplashCapacity=PortalLandingWeatherRules020.SplashCapacity;
        GameSession session;DungeonWorld world;
        Material sky,weather,surface;
        Mesh rainQuad,splashQuad,rainCombined,splashCombined;
        MaterialPropertyBlock rainBlock,splashBlock;
        NativeArray<float4x4> matrices;
        NativeArray<byte> valid;
        NativeArray<int> counts;
        NativeArray<LandingWeatherVertex020> rainVertices,splashVertices;
        readonly Vector4[] torches=new Vector4[8];
        JobHandle fence;
        Camera drawCamera,captureCamera;
        Vector4 heldWind;
        Bounds weatherBounds;

        float seedDelay;
        int cellX,cellZ,lastFrame=-1,quality=1,rainCount,splashCount;
        uint generation;
        bool initialized,attempted,prepared,bound,hasSeeds,disposed,weatherSupported,skyFallback,forceCombined,renderBound;
#if UNITY_EDITOR
        int captureQuality=-1,captureOriginalQuality;
#endif
        Material previousSky;
        AmbientMode previousAmbientMode;
        Color previousSkyColor,previousEquatorColor,previousGroundColor,previousFogColor;
        float previousAmbientIntensity,previousFogDensity;
        bool previousFog;
        FogMode previousFogMode;
        SphericalHarmonicsL2 previousProbe,installedProbe;

        public bool Bound020=>bound&&OwnsAdmission();
        public uint Generation020=>Bound020?generation:0;
        public float PresentationClock020=>presentationShaderClock020;
        public bool WeatherSupported020=>weatherSupported;
        public bool UsesFallbackSky020=>skyFallback;
        public bool UsesCombinedWeather020=>forceCombined||!SystemInfo.supportsInstancing;
        public int RequestedRain020=>rainCount;
        public int RequestedSplashes020=>splashCount;
        public int ValidRain020=>counts.IsCreated&&hasSeeds?counts[0]:0;
        public int ValidSplashes020=>counts.IsCreated&&hasSeeds?counts[1]:0;
        public int LastSubmittedBatches020 {get;private set;}
        public string PreparationStatus020 {get;private set;}="Unprepared";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetLease(){globalLease=null;}
        public void Initialize(GameSession owner,DungeonWorld source)
        {
            if(disposed||owner==null||source==null)return;
            if(initialized){if(session!=owner||world!=source)throw new InvalidOperationException("Atmosphere owner changed without retirement.");return;}
            session=owner;world=source;initialized=true;
            world.WorldModeChanged020+=WorldChanged;
            RenderPipelineManager.beginCameraRendering+=BeforeCamera;renderBound=true;
        }
        bool OwnsAdmission()=>!disposed&&initialized&&session!=null&&world!=null&&session.isActiveAndEnabled&&world.isActiveAndEnabled&&
            ReferenceEquals(GameSession.Instance,session)&&ReferenceEquals(session.World,world)&&session.CampaignHubActive020&&world.CampaignHubActive020&&
            generation!=0&&worldRevision020!=0&&world.CampaignHubGeneration020==generation&&world.WorldRevision020==worldRevision020&&
            world.PortalLandingPrototypeReady020&&world.PortalLandingPrototypeGeneration020==generation&&
            session.TryGetCampaignDirectionalLight020(out Light admittedKey)&&ReferenceEquals(admittedKey,cycleKeyLight020)&&
            world.TryGetPortalLandingPrototype020(generation,out var snapshot)&&snapshot.Generation==generation&&ReferenceEquals(snapshot.Root,admittedRoot020)&&
            snapshot.Root!=null&&snapshot.Root.gameObject.activeInHierarchy;
        bool TryBind()
        {
            if(!initialized||session==null||world==null||!session.isActiveAndEnabled||!world.isActiveAndEnabled||
                !ReferenceEquals(GameSession.Instance,session)||!ReferenceEquals(session.World,world)||
                !session.CampaignHubActive020||!world.CampaignHubActive020||world.CampaignHubGeneration020==0)return false;
            uint current=world.CampaignHubGeneration020,currentRevision=world.WorldRevision020;
            if(!session.TryGetCampaignDirectionalLight020(out Light cycleKeyLight))return false;
            if(currentRevision==0||!world.TryGetPortalLandingPrototype020(current,out var snapshot)||snapshot.Generation!=current||
                snapshot.Root==null||!snapshot.Root.gameObject.activeInHierarchy)return false;
            if(bound&&generation==current&&worldRevision020==currentRevision&&ReferenceEquals(admittedRoot020,snapshot.Root)&&OwnsAdmission())return true;
            Retire();
            if(globalLease!=null&&!ReferenceEquals(globalLease,this))return false;
            Material borrowed=world.PortalLandingSurfaceMaterial020;
            if(borrowed==null||borrowed.shader==null||!borrowed.shader.isSupported||!borrowed.HasProperty(ClockId)||!borrowed.HasProperty(QualityId))
            {PreparationStatus020="Hub surface contract unavailable";return false;}
            if(world.PortalLandingTorchCount020!=8)return false;
            for(int i=0;i<8;i++)
            {
                if(!world.TryGetPortalLandingTorch020(i,out Vector3 p)||!Finite(p))return false;
                torches[i]=new Vector4(p.x,p.y,p.z,1f);
            }
            if(!Prepare())return false;
            CaptureCycleLighting020(cycleKeyLight);
            generation=current;worldRevision020=currentRevision;admittedRoot020=snapshot.Root;presentationElapsedSeconds020=0d;presentationShaderClock020=0f;presentationHeld020=true;surface=borrowed;
            CaptureCloudSurfaceState020(surface);
            surface.SetVectorArray(TorchId,torches);
            previousSky=RenderSettings.skybox;previousAmbientMode=RenderSettings.ambientMode;
            previousSkyColor=RenderSettings.ambientSkyColor;previousEquatorColor=RenderSettings.ambientEquatorColor;previousGroundColor=RenderSettings.ambientGroundColor;
            previousAmbientIntensity=RenderSettings.ambientIntensity;previousProbe=RenderSettings.ambientProbe;
            previousFog=RenderSettings.fog;previousFogMode=RenderSettings.fogMode;previousFogColor=RenderSettings.fogColor;previousFogDensity=RenderSettings.fogDensity;
            installedProbe=default;installedProbe.AddAmbientLight(new Color(.16f,.115f,.18f));
            RenderSettings.skybox=sky;RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=SkyColor;RenderSettings.ambientEquatorColor=EquatorColor;RenderSettings.ambientGroundColor=GroundColor;
            RenderSettings.ambientIntensity=1f;RenderSettings.ambientProbe=installedProbe;
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogColor=FogColor;RenderSettings.fogDensity=FogDensity;
            RecordInstalledAmbient020();
            globalLease=this;bound=true;hasSeeds=false;seedDelay=0;lastFrame=-1;RefreshCampaignCycleView020();return true;
        }
        bool Prepare()
        {
            if(attempted)return prepared;attempted=true;
            try
            {
                Shader skyShader=Resources.Load<Shader>("Shaders/PortalLandingSky020");
                skyFallback=skyShader==null||!skyShader.isSupported;
                if(skyFallback)skyShader=Resources.Load<Shader>("Shaders/OracleNightSky");
                if(skyShader==null||!skyShader.isSupported){PreparationStatus020="No supported procedural sky";return false;}
                sky=new Material(skyShader){name="Campaign room private procedural sky020",hideFlags=HideFlags.DontSave};
                Shader weatherShader=Resources.Load<Shader>("Shaders/PortalLandingRain020");
                weatherSupported=weatherShader!=null&&weatherShader.isSupported;
                if(weatherSupported)
                {
                    if(UnsafeUtility.SizeOf<float4x4>()!=64||UnsafeUtility.SizeOf<Matrix4x4>()!=64||UnsafeUtility.SizeOf<LandingWeatherVertex020>()!=36)
                        throw new InvalidOperationException("Weather matrix/vertex ABI changed.");
                    weather=new Material(weatherShader){name="Campaign room pooled rain and splash020",enableInstancing=true,hideFlags=HideFlags.DontSave};
                    rainBlock=new MaterialPropertyBlock();splashBlock=new MaterialPropertyBlock();
                    matrices=new NativeArray<float4x4>(RainCapacity,Allocator.Persistent,NativeArrayOptions.UninitializedMemory);
                    valid=new NativeArray<byte>(RainCapacity,Allocator.Persistent,NativeArrayOptions.ClearMemory);
                    counts=new NativeArray<int>(2,Allocator.Persistent,NativeArrayOptions.ClearMemory);
                    rainVertices=new NativeArray<LandingWeatherVertex020>(RainCapacity*4,Allocator.Persistent,NativeArrayOptions.UninitializedMemory);
                    splashVertices=new NativeArray<LandingWeatherVertex020>(SplashCapacity*PortalLandingWeatherGeometry020.SplashVertices,Allocator.Persistent,NativeArrayOptions.UninitializedMemory);
                    rainQuad=CreateMesh(1,"One reused soft rain quad020");splashQuad=CreateMesh(1,"One reused four-droplet splash020",true);
                    rainCombined=CreateMesh(RainCapacity,"Combined WebGL rain pool020");splashCombined=CreateMesh(SplashCapacity,"Combined WebGL four-droplet pool020",true);
                }
                PreparePortalEffect020();
                prepared=true;PreparationStatus020=weatherSupported?(skyFallback?"Static procedural sky fallback; weather prepared":"Procedural sky and bounded weather prepared"):
                    "Sky prepared; weather shader unsupported";return true;
            }
            catch{ReleasePrivateResources();PreparationStatus020="Preparation failed; resources retired";throw;}
        }
        static Mesh CreateMesh(int capacity,string label,bool splash=false)
        {
            Mesh mesh=null;
            try
            {
            mesh=new Mesh{name=label,hideFlags=HideFlags.DontSave,indexFormat=IndexFormat.UInt16};
            int vertexCount=PortalLandingWeatherGeometry020.VertexCount(splash),indexCount=PortalLandingWeatherGeometry020.IndexCount(splash);
            mesh.SetVertexBufferParams(capacity*vertexCount,new VertexAttributeDescriptor(VertexAttribute.Position,VertexAttributeFormat.Float32,3),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0,VertexAttributeFormat.Float32,2),new VertexAttributeDescriptor(VertexAttribute.TexCoord1,VertexAttributeFormat.Float32,4));
            var indices=new ushort[capacity*indexCount];
            for(int i=0;i<capacity;i++)for(int j=0;j<indexCount;j++)indices[i*indexCount+j]=PortalLandingWeatherGeometry020.Index(splash,j,i*vertexCount);
            mesh.SetIndexBufferParams(indices.Length,IndexFormat.UInt16);mesh.SetIndexBufferData(indices,0,0,indices.Length);mesh.subMeshCount=1;
            mesh.SetSubMesh(0,new SubMeshDescriptor(0,indices.Length),MeshUpdateFlags.DontRecalculateBounds);
            if(capacity==1)
            {
                var vertices=new LandingWeatherVertex020[vertexCount];
                for(int i=0;i<vertexCount;i++)vertices[i]=PortalLandingWeatherGeometry020.Vertex(splash,i,default);
                mesh.SetVertexBufferData(vertices,0,0,vertexCount);
                mesh.bounds=splash?new Bounds(new Vector3(0,.09f,0),new Vector3(.32f,.20f,.32f)):new Bounds(new Vector3(0,2.72f,0),new Vector3(.7f,5.48f,.7f));mesh.UploadMeshData(true);
            }
            else mesh.MarkDynamic();
            return mesh;
            }
            catch{DestroyOwned(mesh);throw;}
        }
        public void TickPresentation(float delta,bool held,int requestedQuality,float particleFactor,Camera camera)
        {
            LastSubmittedBatches020=0;
            if(disposed||!isActiveAndEnabled||!TryBind()){if(bound)Retire();return;}
            if(lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
            if(!OwnsAdmission()){Retire();return;}
            drawCamera=camera;
            Vector4 policy=LaneVignetteRenderer.PassiveWindDescriptor015;
            bool motion=policy.w>.5f&&!OracleBookAccessibility.PrefersReducedMotion;
            bool cycleHold=held||!Application.isFocused||captureCamera!=null||camera==null||!camera.isActiveAndEnabled;
            bool animationHold=cycleHold||!motion;
            presentationHeld020=cycleHold;
            presentationElapsedSeconds020=CampaignCycleRules020.AdvanceElapsed(presentationElapsedSeconds020,delta,cycleHold);
            if(!animationHold)presentationShaderClock020=CampaignCycleRules020.ProjectShaderClock(presentationElapsedSeconds020);
            RefreshCampaignCycleView020();
            if(!animationHold)
            {
                Vector4 wind=world.PassiveWindField015;Vector2 direction=Finite(new Vector3(wind.x,0,wind.y))?new Vector2(wind.x,wind.y):Vector2.zero;
                direction=Vector2.ClampMagnitude(direction*.12f,.28f);heldWind=new Vector4(direction.x,direction.y,0,0);
                if(!float.IsNaN(delta)&&!float.IsInfinity(delta)&&delta>0)seedDelay=Mathf.Max(0,seedDelay-Mathf.Min(delta,.15f));
            }
            int nextQuality=Mathf.Clamp(requestedQuality,0,2);
#if UNITY_EDITOR
            if(captureCamera!=null&&captureQuality>=0)nextQuality=captureQuality;
#endif
            ReadCloudPreferences020();SetLensFlarePause020(held);
            int nextRain=motion&&weatherEffectsEnabled020&&cycleViewReady020&&CampaignCycleRules020.AllowsBloodstorm(cycleView020)?PortalLandingWeatherRules020.RainCount(nextQuality,particleFactor):0;
            int nextSplash=Mathf.Min(SplashCapacity,(nextRain+3)/4);
            bool recount=nextRain!=rainCount||nextSplash!=splashCount;
            quality=nextQuality;rainCount=nextRain;splashCount=nextSplash;
            SetPresentationUniforms();
            if(!weatherSupported||camera==null||!Finite(camera.transform.position))return;
            Vector3 eye=camera.transform.position;
            int nextX=Mathf.FloorToInt(eye.x/8+.5f),nextZ=Mathf.FloorToInt(eye.z/8+.5f);
            if(!hasSeeds||(!animationHold&&seedDelay<=0&&(nextX!=cellX||nextZ!=cellZ)))RebuildSeeds(nextX,nextZ);
            else if(recount&&hasSeeds)Recount();
        }
        void SetPresentationUniforms()
        {
            float shaderClock=presentationShaderClock020;
            if(!skyFallback){sky.SetFloat(ClockId,shaderClock);sky.SetFloat(QualityId,quality);}
            ApplyCampaignCycleUniforms020();
            if(surface!=null){surface.SetFloat(ClockId,shaderClock);surface.SetFloat(QualityId,quality);}
            ApplyCloudPresentation020();
            SetPortalEffectUniforms020();
            if(!weatherSupported)return;
            weather.SetFloat(ClockId,shaderClock);weather.SetVector(WindId,heldWind);
            ConfigureBlock(rainBlock,0,rainCount);ConfigureBlock(splashBlock,1,splashCount);
        }
        void ConfigureBlock(MaterialPropertyBlock block,int mode,int count)
        {block.SetFloat(ModeId,mode);block.SetFloat(InstancedId,UsesCombinedWeather020?0:1);block.SetFloat(CountId,count);}
        void RebuildSeeds(int x,int z)
        {
            fence.Complete();hasSeeds=false;
            // Numeric job rejects coordinates outside its Morton-query domain.
            cellX=x;cellZ=z;
            fence=new PortalLandingWeatherSeedJob020{CellX=x,CellZ=z,Matrices=matrices,Valid=valid,RainVertices=rainVertices,SplashVertices=splashVertices}.Schedule(RainCapacity,16);
            fence=new PortalLandingWeatherCountJob020{RainCount=rainCount,SplashCount=splashCount,Valid=valid,Counts=counts}.Schedule(fence);fence.Complete();
            weatherBounds=new Bounds(new Vector3(x*8,3.5f,z*8),new Vector3(34,20,34));
            rainCombined.SetVertexBufferData(rainVertices,0,0,RainCapacity*4,0,MeshUpdateFlags.DontRecalculateBounds);
            splashCombined.SetVertexBufferData(splashVertices,0,0,SplashCapacity*PortalLandingWeatherGeometry020.SplashVertices,0,MeshUpdateFlags.DontRecalculateBounds);
            rainCombined.bounds=weatherBounds;splashCombined.bounds=weatherBounds;
            hasSeeds=true;seedDelay=.25f;
        }
        void Recount()
        {fence=new PortalLandingWeatherCountJob020{RainCount=rainCount,SplashCount=splashCount,Valid=valid,Counts=counts}.Schedule();fence.Complete();}
        void BeforeCamera(ScriptableRenderContext context,Camera camera)
        {
            if(!bound||!OwnsAdmission()){if(bound)Retire();return;}
            if(camera==null||(captureCamera!=null?camera!=captureCamera:camera!=drawCamera))return;
            UpdateSunLensFlare020(camera);
            DrawPortalEffect020(camera);
            if(!hasSeeds||!weatherSupported)return;
            fence.Complete();LastSubmittedBatches020=0;
            if(rainCount>0&&counts[0]>0)Draw(camera,rainQuad,rainCombined,rainBlock,rainCount);
            if(splashCount>0&&counts[1]>0)Draw(camera,splashQuad,splashCombined,splashBlock,splashCount);
        }
        void Draw(Camera camera,Mesh unit,Mesh combined,MaterialPropertyBlock block,int count)
        {
            var parameters=new RenderParams(weather){camera=camera,matProps=block,worldBounds=weatherBounds,shadowCastingMode=ShadowCastingMode.Off,
                receiveShadows=false,lightProbeUsage=LightProbeUsage.Off,reflectionProbeUsage=ReflectionProbeUsage.Off};
            if(UsesCombinedWeather020)Graphics.RenderMesh(parameters,combined,0,Matrix4x4.identity);
            else Graphics.RenderMeshInstanced(parameters,unit,0,matrices.Reinterpret<Matrix4x4>(),count);
            LastSubmittedBatches020++;
        }
        void WorldChanged(DungeonWorld source)
        {if(!ReferenceEquals(source,world)||!OwnsAdmission())Retire();}
        public void Retire()
        {
            fence.Complete();LastSubmittedBatches020=0;drawCamera=captureCamera=null;forceCombined=false;RetireLensClouds020();RestoreCycleLighting020();RetireCampaignCycle020();
            if(bound&&ReferenceEquals(globalLease,this))
            {
                // A replacement owner's values must not be overwritten during our exit.
                if(RenderSettings.skybox==sky)RenderSettings.skybox=previousSky;
                if(RenderSettings.ambientMode==AmbientMode.Trilight)RenderSettings.ambientMode=previousAmbientMode;
                if(RenderSettings.ambientSkyColor==installedSkyColor020)RenderSettings.ambientSkyColor=previousSkyColor;
                if(RenderSettings.ambientEquatorColor==installedEquatorColor020)RenderSettings.ambientEquatorColor=previousEquatorColor;
                if(RenderSettings.ambientGroundColor==installedGroundColor020)RenderSettings.ambientGroundColor=previousGroundColor;
                if(RenderSettings.ambientIntensity==installedAmbientIntensity020)RenderSettings.ambientIntensity=previousAmbientIntensity;
                if(SameProbe(RenderSettings.ambientProbe,installedProbe))RenderSettings.ambientProbe=previousProbe;
                if(RenderSettings.fog==installedFog020)RenderSettings.fog=previousFog;
                if(RenderSettings.fogMode==installedFogMode020)RenderSettings.fogMode=previousFogMode;
                if(RenderSettings.fogColor==installedFogColor020)RenderSettings.fogColor=previousFogColor;
                if(RenderSettings.fogDensity==installedFogDensity020)RenderSettings.fogDensity=previousFogDensity;
                globalLease=null;
            }
            bound=false;generation=0;surface=null;hasSeeds=false;previousSky=null;lastFrame=-1;
        }
        static bool SameProbe(SphericalHarmonicsL2 a,SphericalHarmonicsL2 b)
        {for(int channel=0;channel<3;channel++)for(int coefficient=0;coefficient<9;coefficient++)if(a[channel,coefficient]!=b[channel,coefficient])return false;return true;}
        static bool Finite(Vector3 v)=>!float.IsNaN(v.x)&&!float.IsInfinity(v.x)&&!float.IsNaN(v.y)&&!float.IsInfinity(v.y)&&!float.IsNaN(v.z)&&!float.IsInfinity(v.z);
        void OnDisable(){Retire();}
        void OnDestroy()
        {
            Retire();disposed=true;
            if(initialized&&world!=null)world.WorldModeChanged020-=WorldChanged;
            if(renderBound)RenderPipelineManager.beginCameraRendering-=BeforeCamera;
            initialized=renderBound=false;session=null;world=null;ReleasePrivateResources();
        }
        void ReleasePrivateResources()
        {
            fence.Complete();prepared=false;weatherSupported=false;
            if(matrices.IsCreated)matrices.Dispose();if(valid.IsCreated)valid.Dispose();if(counts.IsCreated)counts.Dispose();
            if(rainVertices.IsCreated)rainVertices.Dispose();if(splashVertices.IsCreated)splashVertices.Dispose();
            DestroyOwned(rainQuad);DestroyOwned(splashQuad);DestroyOwned(rainCombined);DestroyOwned(splashCombined);DestroyOwned(sky);DestroyOwned(weather);
            rainQuad=splashQuad=rainCombined=splashCombined=null;sky=weather=null;rainBlock=splashBlock=null;
            ReleasePortalEffect020();
        }
        static void DestroyOwned(UnityEngine.Object value)
        {if(value==null)return;if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
#if UNITY_EDITOR
        // Cold diagnostic admission is still the actual session/world lease. This only holds presentation, never gameplay.
        public bool BeginCapture020(Camera camera,bool combined)
        {
            if(camera==null||captureCamera!=null||!Bound020||!weatherSupported)return false;
            captureOriginalQuality=quality;captureQuality=-1;
            captureCamera=camera;forceCombined=combined;SetPresentationUniforms();return true;
        }
        public void SetCaptureCombined020(bool combined)
        {if(captureCamera==null||!Bound020)return;forceCombined=combined;SetPresentationUniforms();}
        public void SetCaptureQuality020(int value)
        {
            if(captureCamera==null||!Bound020)return;ReadCloudPreferences020();captureQuality=Mathf.Clamp(value,0,2);quality=captureQuality;
            rainCount=weatherEffectsEnabled020&&cycleViewReady020&&CampaignCycleRules020.AllowsBloodstorm(cycleView020)?PortalLandingWeatherRules020.RainCount(quality,session.Graphics!=null?session.Graphics.EffectiveParticleFactor:.125f):0;
            if(LaneVignetteRenderer.PassiveWindDescriptor015.w<=.5f||OracleBookAccessibility.PrefersReducedMotion)rainCount=0;
            splashCount=Mathf.Min(SplashCapacity,(rainCount+3)/4);if(hasSeeds)Recount();SetPresentationUniforms();
        }
        public void EndCapture020()
        {
            bool hadCapture=captureCamera!=null;captureCamera=null;forceCombined=false;captureQuality=-1;
            if(!bound)return;
            if(!OwnsAdmission()){Retire();return;}
            if(hadCapture)quality=captureOriginalQuality;
            ReadCloudPreferences020();
            if(hadCapture)
            {
                Vector4 policy=LaneVignetteRenderer.PassiveWindDescriptor015;
                float particleFactor=session.Graphics!=null?session.Graphics.EffectiveParticleFactor:.125f;
                bool canRain=policy.w>.5f&&!OracleBookAccessibility.PrefersReducedMotion&&weatherEffectsEnabled020&&cycleViewReady020&&CampaignCycleRules020.AllowsBloodstorm(cycleView020);
                rainCount=canRain?PortalLandingWeatherRules020.RainCount(quality,particleFactor):0;
                splashCount=Mathf.Min(SplashCapacity,(rainCount+3)/4);
                if(hasSeeds)Recount();
            }
            SetPresentationUniforms();
        }
#endif
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Exact active session/World/root/generation/revision lease, stationary world metres/Y-up; one conditional sky/ambient/fog lease and existing session key light.
// Array Formats:64native64B matrices,64byte validity,256+64Vertex36,2int counts;4fixed meshes,1private weather+1private sky material,2cached MPBs,8cold Vector4 anchors. Cycle and lighting state are retained scalar/Unity structs; no per-node objects.
// Dependencies/Pointers: Builder alone destroys the borrowed surface; atmosphere borrows GameSession's retained directional Light and writes only its intensity/color/rotation. Session tick drives one elapsed cycle clock and caches its last motion-admitted shader projection; reduced motion alone holds only that projection, while pause/focus/capture holds both; every changed Light/RenderSettings field restores only if still equal to its last installed value. Cloud controls read GraphicsPreferences without writing it; sky flare uses only the admitted camera and one cold ray buffer, while projected shade remains floor-mask-only. Capture teardown revalidates the lease, restores quality, rereads current weather and cloud preferences, and recomputes bounded counts before publishing uniforms.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing exact admitted floor anchors, held shader clock and World/root/generation/revision ownership remain unchanged.
// Array Formats: Rain64x4 and splash16x16 Vertex36; shared numeric prototypes; maximum128+256triangles/two batches. Persistent splash storage increases6912bytes, with no frame allocation or per-frame mesh upload added.
// Dependencies/Pointers: Only preparation/CreateMesh/seed-upload geometry hunks change. Clock/weather/capture/cleanup/lighting methods remain byte-identical; root imports/compiles Burst/shader and judges native pixels.
