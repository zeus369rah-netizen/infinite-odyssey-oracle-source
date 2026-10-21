using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OdysseyOracle
{
    public sealed partial class MenuShowcaseLease:IDisposable
    {
        public const int StageLayer=30;
        // GameObject culling layer30 is independent of URP defined rendering/light layers.
        const uint RenderMask=1u<<7;
        readonly GameObject root;
        readonly List<Transform> bindings=new List<Transform>(256);
        readonly List<MenuShowcaseSeed48> seedScratch=new List<MenuShowcaseSeed48>(256);
        readonly List<Material> ownedMaterials=new List<Material>(8);
        readonly List<MonsterVisualLease> monsterLeases=new List<MonsterVisualLease>(12);
        readonly bool[] shown=new bool[256];
        readonly Renderer[] glowRenderers=new Renderer[256];
        readonly MaterialPropertyBlock[] glowBlocks=new MaterialPropertyBlock[256];
        static readonly int Emission=Shader.PropertyToID("_EmissionColor");
        static readonly int PreviewClock=Shader.PropertyToID("_PreviewClock");
        readonly Renderer[] weatherRenderers=new Renderer[MenuShowcaseCatalog.MaximumMeshes];
        readonly MaterialPropertyBlock[] weatherBlocks=new MaterialPropertyBlock[MenuShowcaseCatalog.MaximumMeshes];
        int weatherCount;
        readonly Light[] hubLights=new Light[MenuShowcaseCatalog.MaximumHubLights];
        readonly float[] hubIntensities=new float[MenuShowcaseCatalog.MaximumHubLights];
        int hubLightCount;
        NativeArray<MenuShowcaseSeed48> seeds;
        NativeArray<MenuShowcasePose48> poses;
        NativeArray<MenuShowcaseSegment32> segments;
        NativeArray<int2> ranges;
        NativeArray<float3> pads;
        NativeArray<float3> muzzles,headPivots;
        OracleAvatarModel oracle,oraclePartner;
        NativeArray<MenuShowcaseFrame48> cameraFrames;
        float tourClock,viewportAspect=16f/9f;float4 safeViewport=new float4(.025f,.08f,.55f,.92f);
        SavedTowerModelLibrary towers;
        Mesh boltMesh;
        Camera camera;
        JobHandle fence;
        bool scheduled,visible,disposed,dirty=true,lastMotion=true,staticRendered;
        float clock,timer;
        float acceptedClock,acceptedTourClock,acceptedWeatherClock;bool acceptedPresentation;
        int quality=2;
        public RenderTexture Target{get;private set;}
        public int Quality=>quality;
        public Bounds PresentationBounds{get;}
        public Camera PresentationCamera=>camera;
        public int DemoAvatarCount=>(oracle!=null?1:0)+(oraclePartner!=null?1:0);
        public float TourYawDegrees=>cameraFrames.IsCreated?cameraFrames[0].YawDegrees:0;
        public int PoseCount=>bindings.Count;
        public bool IsVisible=>visible;
        public MenuShowcaseLease(MenuShowcaseCatalog catalog,TowerModelAssetCatalog towerAssets,int initialQuality=2,uint startupSeed015=0)
        {
            if(catalog==null||!catalog.TryValidate(out _))throw new InvalidOperationException("Validated baked co-op catalog required.");
            if((RenderingLayerMask.GetDefinedRenderingLayersCombinedMaskValue()&RenderMask)==0)throw new InvalidOperationException("Preview rendering/light layer7 must remain defined; never reuse the camera culling layer index.");
            // Fit model bases at the origin before moving the inactive stage;
            // worldY-20000 would otherwise quantize the cold bounds subtraction.
            root=new GameObject("Scripted start-menu showcase only");root.SetActive(false);
            try
            {
                if(!SavedTowerModelLibrary.TryCreate(towerAssets,out towers,out string reason))throw new InvalidOperationException(reason);
                for(int i=0;i<catalog.Draws.Length;i++)AddSnapshotDraw(catalog.Draws[i],"Arena snapshot "+i);
                if(catalog.HubDraws!=null)for(int i=0;i<catalog.HubDraws.Length;i++)AddSnapshotDraw(catalog.HubDraws[i],"Visual-only hub snapshot "+i);
                if(catalog.HubLights!=null)for(int i=0;i<catalog.HubLights.Length;i++)
                {
                    var source=catalog.HubLights[i];var light=Node("Preview-only local hub illumination "+i,root.transform).gameObject.AddComponent<Light>();
                    light.transform.localPosition=source.Position;light.type=LightType.Point;light.color=source.Color;light.intensity=source.Intensity;
                    light.range=source.Range;light.shadows=LightShadows.None;light.bounceIntensity=0;light.cullingMask=1<<StageLayer;light.renderingLayerMask=unchecked((int)RenderMask);
                    light.gameObject.AddComponent<UniversalAdditionalLightData>().renderingLayers=RenderMask;
                    hubLights[hubLightCount]=light;hubIntensities[hubLightCount++]=source.Intensity;
                }
                Material cloth=Paint("Oracle cloth",new Color(.035f,.29f,.31f)),gold=Paint("Oracle brass",new Color(.63f,.43f,.12f));
                Material dark=Paint("Oracle dark",new Color(.035f,.045f,.04f)),gem=Paint("Oracle focus",new Color(.12f,.8f,.7f),2f);
                var hero=Node("Scripted Oracle 1",root.transform);firstOraclePose015=bindings.Count;Bind(hero,1,0);
                var heroBody=Node("Render-only Oracle 1",hero);oracle=new OracleAvatarModel(heroBody,cloth,gold,dark,gem);
                Bind(heroBody.Find("Rigid left arm"),2,0);Bind(oracle.Wand.parent,2,256);
                GroundVisual(heroBody);
                var partner=Node("Scripted Oracle 2",root.transform);secondOraclePose015=bindings.Count;Bind(partner,1,1);
                var partnerBody=Node("Render-only Oracle 2",partner);oraclePartner=new OracleAvatarModel(partnerBody,cloth,gold,dark,gem);
                oraclePartner.Apply(OracleAvatarAppearance.StartingLook(1));
                Bind(partnerBody.Find("Rigid left arm"),2,1);Bind(oraclePartner.Wand.parent,2,257);GroundVisual(partnerBody);
                Material stone=Paint("Monster stone",new Color(.22f,.25f,.26f)),joints=Paint("Monster joints",new Color(.07f,.08f,.10f)),rune=Paint("Monster rune",new Color(.5f,.18f,.8f),1.5f);
                for(int i=0;i<12;i++)
                {
                    Transform actor=Node("Scripted monster "+i,root.transform);Bind(actor,3,(uint)i);
                    Transform body=Node("Original render-only rigid body",actor);body.localScale=Vector3.one*1.20f;
                    Transform left=Node("Original left shoulder",body),right=Node("Original right shoulder",body);
                    left.localPosition=new Vector3(-.53f,1.36f,0);right.localPosition=new Vector3(.53f,1.36f,0);
                    var lease=MonsterVisualFactory.Create((MonsterVoxelCosmetics.Style)(i&3),body,left,right,stone,joints,rune,null);
                    monsterLeases.Add(lease);Bind(left,4,(uint)i);Bind(right,4,(uint)(i|256));
                    if(lease.SpiderLegs!=null)for(int leg=0;leg<lease.SpiderLegs.Length;leg++)Bind(lease.SpiderLegs[leg],6,(uint)(i|(leg<<8)));
                    else {if(lease.LeftLeg!=null)Bind(lease.LeftLeg,5,(uint)i);if(lease.RightLeg!=null)Bind(lease.RightLeg,5,(uint)(i|256));}
                    GroundVisual(body);
                }
                for(int type=0;type<4;type++)
                {
                    var template=towers.Get(type,1);Transform actor=Node("Scripted tower "+type,root.transform);actor.localPosition=catalog.Pads[type];Bind(actor,7,(uint)type);
                    var groups=new Transform[6];
                    for(int g=0;g<6;g++)
                    {
                        groups[g]=Node("Saved tower group "+g,g>1?groups[1]:actor);
                        if(g==1)groups[g].localPosition=TowerModelLibrary.Vector(template.Recipe.HeadPivot);
                        if(template.Groups[g]!=null)Draw("Saved tier-one group",groups[g],template.Groups[g],towers.GetMaterial(type));
                        uint kind=g==1?8u:g==2||g==5?9u:g==3?10u:g==4?11u:0u;if(kind!=0)Bind(groups[g],kind,(uint)type);
                    }
                }
                boltMesh=MakeBolt();
                for(int i=0;i<16;i++)
                {
                    Transform bolt=Node("Scripted visual bolt "+i,root.transform);bolt.localScale=new Vector3(.16f,.16f,.8f);Bind(bolt,12,(uint)i);
                    Draw("Preview bolt only",bolt,boltMesh,gem);
                }
                seeds=new NativeArray<MenuShowcaseSeed48>(seedScratch.ToArray(),Allocator.Persistent);
                poses=new NativeArray<MenuShowcasePose48>(bindings.Count,Allocator.Persistent);
                segments=new NativeArray<MenuShowcaseSegment32>(catalog.Segments,Allocator.Persistent);
                ranges=new NativeArray<int2>(12,Allocator.Persistent);for(int i=0;i<12;i++)ranges[i]=new int2(catalog.RouteRanges[i].x,catalog.RouteRanges[i].y);
                pads=new NativeArray<float3>(4,Allocator.Persistent);for(int i=0;i<4;i++)pads[i]=F(catalog.Pads[i]);
                muzzles=new NativeArray<float3>(4,Allocator.Persistent);headPivots=new NativeArray<float3>(4,Allocator.Persistent);
                for(int i=0;i<4;i++){var recipe=towers.Get(i,1).Recipe;muzzles[i]=F(TowerModelLibrary.Vector(recipe.Muzzle));headPivots[i]=F(TowerModelLibrary.Vector(recipe.HeadPivot));}
                seedScratch.Clear();
                cameraFrames=new NativeArray<MenuShowcaseFrame48>(1,Allocator.Persistent);
                Transform view=Node("Full-map orbit showcase camera",root.transform);camera=view.gameObject.AddComponent<Camera>();
                // Keep the private RT camera outside Game-only world graphics policies.
                camera.cameraType=CameraType.Preview;PresentationBounds=FocusBounds(catalog);
                camera.orthographic=true;camera.aspect=viewportAspect;camera.cullingMask=1<<StageLayer;camera.clearFlags=CameraClearFlags.SolidColor;
                camera.backgroundColor=new Color(.015f,.025f,.026f);camera.allowHDR=false;camera.allowMSAA=false;camera.depth=-30;camera.enabled=false;
                var data=camera.GetUniversalAdditionalCameraData();data.renderPostProcessing=false;data.renderShadows=false;data.volumeLayerMask=0;
                AddLight(new Vector3(-20,55,-25),3200f);AddLight(new Vector3(25,35,15),1800f);
                InitializeIsland015(catalog,startupSeed015);UpdateTourFrame(0,0);
                Isolate(root.transform);root.transform.position=new Vector3(0,-20000,0);SetQuality(initialQuality);RenderPipelineManager.endCameraRendering+=Rendered;
            }
            catch{Dispose();throw;}
        }
        public void SetQuality(int value)
        {
            if(disposed||value<0||value>2)return;
            if(value==quality&&(value==0||Target!=null))return;
            Complete();quality=value;ResizeTarget();dirty=true;staticRendered=false;
        }
        public void SetViewport(float aspect,float4 safe)
        {SetViewport(aspect,safe,Mathf.Max(1,Screen.width),Mathf.Max(1,Screen.height));}
        void ResizeTarget()
        {
            camera.enabled=false;camera.targetTexture=null;
            if(Target!=null){Target.Release();UnityEngine.Object.Destroy(Target);Target=null;}
            if(quality==0)return;
            int2 size=MenuIslandMath015.TargetSize(quality,viewportPixelsX015,viewportPixelsY015,viewportAspect);
            if(math.any(size<1))throw new InvalidOperationException("Finite bounded showcase target dimensions required.");
            var descriptor=new RenderTextureDescriptor(size.x,size.y,RenderTextureFormat.ARGB32,24){msaaSamples=1,useMipMap=false,autoGenerateMips=false};
            descriptor.msaaSamples=SupportedSamples015(descriptor);camera.allowMSAA=descriptor.msaaSamples>1;
            Target=new RenderTexture(descriptor){name="Scripted two-Oracle island showcase015",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            if(!Target.Create())throw new InvalidOperationException("Showcase render texture unavailable.");camera.targetTexture=Target;camera.aspect=viewportAspect;
            UpdateIslandPresentation015(acceptedPresentation?acceptedWeatherClock:0f);
        }
        void UpdateTourFrame(float elapsed,float delta)
        {UpdateIslandCamera015(elapsed,delta);}
        public void SetVisible(bool value)
        {
            value=value&&quality!=0&&!disposed;if(visible==value)return;Complete();visible=value;
            if(camera!=null)camera.enabled=false;if(root!=null)root.SetActive(value);dirty=true;staticRendered=false;
        }
        public void Tick(float dt,bool motion)
        {
            if(disposed||!visible||quality==0)return;
            dt=Mathf.Clamp(dt,0,.05f);
            if(lastMotion!=motion)
            {
                // Hold the last successfully published pose/camera/weather triple, including unrendered timer remainder.
                clock=acceptedPresentation?acceptedClock:0f;tourClock=acceptedPresentation?acceptedTourClock:0f;islandWeatherClock015=acceptedPresentation?acceptedWeatherClock:0f;
                timer=0;lastMotion=motion;dirty=true;staticRendered=false;
            }
            if(motion){clock=(clock+dt)%32f;tourClock=(tourClock+dt)%MenuShowcaseCameraMath.TourSeconds;islandWeatherClock015=(islandWeatherClock015+dt)%120f;}
            if(motion)timer+=dt;float interval=quality==2?1f/30f:1f/20f;
            if(!dirty&&(timer<interval||!motion&&staticRendered))return;float renderDelta=timer;timer=0;
            float presentationClock=clock;
            fence=new MenuShowcasePoseJob{Seeds=seeds,Poses=poses,Segments=segments,Ranges=ranges,Pads=pads,Muzzles=muzzles,HeadPivots=headPivots,Time=presentationClock,MonsterCount=quality==2?12:6}.Schedule();scheduled=true;Complete();
            UpdateTourFrame(tourClock,motion?renderDelta:0);
            UpdateIslandPresentation015(islandWeatherClock015);
            for(int i=0;i<bindings.Count;i++)
            {
                var pose=poses[i];bool active=pose.Visible!=0;Transform target=bindings[i];
                if(shown[i]!=active){shown[i]=active;target.gameObject.SetActive(active);}
                if(!active)continue;
                target.SetLocalPositionAndRotation(V(pose.Position),Q(pose.Rotation));target.localScale=V(pose.Scale);
                if(glowRenderers[i]!=null){glowBlocks[i].SetColor(Emission,Color.white*(1.8f*math.asfloat(pose.Reserved)));glowRenderers[i].SetPropertyBlock(glowBlocks[i]);}
            }
            for(int i=0;i<weatherCount;i++){weatherBlocks[i].SetFloat(PreviewClock,presentationClock);weatherRenderers[i].SetPropertyBlock(weatherBlocks[i]);}
            // Numeric local pulse only; cached bounded adapters never touch global lighting.
            float hubBreath=.94f+.06f*math.sin(presentationClock*(math.PI/4f));
            if(hubLightCount>0)hubLights[0].intensity=hubIntensities[0]*hubBreath;
            if(hubLightCount>1)hubLights[1].intensity=hubIntensities[1]*hubBreath;
            if(hubLightCount>2)hubLights[2].intensity=hubIntensities[2]*hubBreath;
            if(hubLightCount>3)hubLights[3].intensity=hubIntensities[3]*hubBreath;
            if(hubLightCount>4)hubLights[4].intensity=hubIntensities[4]*hubBreath;
            acceptedClock=presentationClock;acceptedTourClock=tourClock;acceptedWeatherClock=islandWeatherClock015;acceptedPresentation=true;
            dirty=false;camera.enabled=true;
        }
        void AddSnapshotDraw(MenuShowcaseCatalog.Draw draw,string name)
        {
            if(IsInactivePreviewGrid015(draw.Material)){ExcludedPreviewGridCount015++;return;}
            Renderer renderer=Draw(name,root.transform,draw.Mesh,draw.Material);
            string shader=draw.Material.shader==null?"":draw.Material.shader.name;
            if(shader!="OdysseyOracle/MenuLaneWeatherPreview"&&shader!="OdysseyOracle/MenuHubMotesPreview")return;
            if(weatherCount==weatherRenderers.Length)throw new InvalidOperationException("Private-clock renderer cap exceeded.");
            var block=new MaterialPropertyBlock();block.SetFloat(PreviewClock,31f);
            weatherRenderers[weatherCount]=renderer;weatherBlocks[weatherCount++]=block;renderer.SetPropertyBlock(block);
        }
        void Rendered(ScriptableRenderContext context,Camera rendered){if(rendered==camera){camera.enabled=false;staticRendered=true;}}
        void Complete(){if(scheduled){try{fence.Complete();}finally{scheduled=false;}}}
        void Bind(Transform target,uint kind,uint index)
        {
            if(target==null||bindings.Count==256)throw new InvalidOperationException("Showcase rig binding cap/contract failed.");
            bindings.Add(target);seedScratch.Add(new MenuShowcaseSeed48{Position=F(target.localPosition),Rotation=R(target.localRotation),Scale=F(target.localScale),Kind=kind,Index=index});
            shown[bindings.Count-1]=target.gameObject.activeSelf;
            if(kind==10){glowRenderers[bindings.Count-1]=target.GetComponentInChildren<MeshRenderer>();glowBlocks[bindings.Count-1]=new MaterialPropertyBlock();}
        }
        static Transform Node(string name,Transform parent){var value=new GameObject(name).transform;value.SetParent(parent,false);return value;}
        static Renderer Draw(string name,Transform parent,Mesh mesh,Material material)
        {var node=Node(name,parent);node.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=node.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;return renderer;}
        Material Paint(string name,Color color,float glow=0)
        {
            var value=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Showcase only / "+name};ownedMaterials.Add(value);
            value.SetColor("_BaseColor",color);if(glow>0){value.EnableKeyword("_EMISSION");value.SetColor("_EmissionColor",color*glow);}return value;
        }
        void AddLight(Vector3 position,float intensity)
        {
            var light=Node("Showcase scoped fill",root.transform).gameObject.AddComponent<Light>();light.transform.localPosition=position;
            light.type=LightType.Point;light.range=220;light.intensity=intensity;light.shadows=LightShadows.None;light.cullingMask=1<<StageLayer;light.renderingLayerMask=unchecked((int)RenderMask);
            light.gameObject.AddComponent<UniversalAdditionalLightData>().renderingLayers=RenderMask;TrackIslandFill015(light);
        }
        static void Isolate(Transform value)
        {value.gameObject.layer=StageLayer;if(value.TryGetComponent<Renderer>(out var renderer))renderer.renderingLayerMask=RenderMask;for(int i=0;i<value.childCount;i++)Isolate(value.GetChild(i));}
        static void GroundVisual(Transform body)
        {
            var renderers=new List<Renderer>(32);body.GetComponentsInChildren(true,renderers);float minimum=float.PositiveInfinity;
            for(int i=0;i<renderers.Count;i++)minimum=Mathf.Min(minimum,renderers[i].bounds.min.y);
            if(!float.IsFinite(minimum))throw new InvalidOperationException("Showcase model has no finite base.");
            body.localPosition+=Vector3.up*(body.parent.position.y-minimum);
        }
        static Bounds FocusBounds(MenuShowcaseCatalog value)
        {
            // Wide endpoint includes every original map/hub mesh plus the new supported underside.
            // A fresh source catalog bake remains mandatory after actual scenery changes.
            return OuterIslandPresentationBounds020(value);
        }
        static Mesh MakeBolt()
        {
            var mesh=new Mesh{name="Showcase-only shared visual bolt"};
            mesh.vertices=new[]{new Vector3(-.5f,-.5f,-.5f),new Vector3(.5f,-.5f,-.5f),new Vector3(.5f,.5f,-.5f),new Vector3(-.5f,.5f,-.5f),new Vector3(-.5f,-.5f,.5f),new Vector3(.5f,-.5f,.5f),new Vector3(.5f,.5f,.5f),new Vector3(-.5f,.5f,.5f)};
            mesh.triangles=new[]{0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,3,7,6,3,6,2,0,4,7,0,7,3,1,2,6,1,6,5};mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        static float3 F(Vector3 v)=>new float3(v.x,v.y,v.z);
        static Vector3 V(float3 v)=>new Vector3(v.x,v.y,v.z);
        static quaternion R(Quaternion v)=>new quaternion(v.x,v.y,v.z,v.w);
        static Quaternion Q(quaternion v)=>new Quaternion(v.value.x,v.value.y,v.value.z,v.value.w);
        public void Dispose()
        {
            if(disposed)return;disposed=true;Complete();RenderPipelineManager.endCameraRendering-=Rendered;
            if(camera!=null){camera.enabled=false;camera.targetTexture=null;}
            if(root!=null)root.SetActive(false);oracle?.Dispose();oracle=null;oraclePartner?.Dispose();oraclePartner=null;
            for(int i=0;i<monsterLeases.Count;i++)monsterLeases[i].Dispose();monsterLeases.Clear();towers?.Dispose();towers=null;
            DisposeIsland015();
            if(cameraFrames.IsCreated)cameraFrames.Dispose();
            if(seeds.IsCreated)seeds.Dispose();if(poses.IsCreated)poses.Dispose();if(segments.IsCreated)segments.Dispose();if(ranges.IsCreated)ranges.Dispose();if(pads.IsCreated)pads.Dispose();if(muzzles.IsCreated)muzzles.Dispose();if(headPivots.IsCreated)headPivots.Dispose();
            if(root!=null)UnityEngine.Object.Destroy(root);if(boltMesh!=null)UnityEngine.Object.Destroy(boltMesh);
            for(int i=0;i<ownedMaterials.Count;i++)if(ownedMaterials[i]!=null)UnityEngine.Object.Destroy(ownedMaterials[i]);ownedMaterials.Clear();
            if(Target!=null){Target.Release();UnityEngine.Object.Destroy(Target);Target=null;}
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: full original catalog+underside at isolated worldY-20000;48s conservative sphere wide/actor close cycle deliberately crops edges while camera/depth remain outside complete scene.
// Array Formats: unchanged bounded original pose/route stores; focus+velocity float3[2], four cold combined meshes, same pixel-capped private RT with supported0/1/2x samples.
// Dependencies/Pointers: original two avatar scales/routes and borrowed catalog preserved; exact inactive grid preview excluded. No shared HUD/tutorial, settings, room, collider, progression, global lighting or Random mutation; root native/browser/visual acceptance pending.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: last accepted scripted actor/tour/weather times remain fixed through pause/reduced-motion and dirty redraws; cold static starts at0, resume advances without31/0reset.
// Array Formats: three managed float clock checkpoints+bool only; same fenced focus/velocity float3[2], frame48B[1], existing pose/render stores and no hot allocation.
// Dependencies/Pointers: existing native jobs/adapters commit times only after success; held timer does not accumulate, RT resize borrows accepted weather phase. Source-facade/native/browser scopes remain distinct; no gameplay or saved-setting mutations.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: existing private menu stage with conservative bounds enclosing all six detached perimeter solids.
// Array Formats: original actor/focus/clock stores remain; one additional static1620vertex mesh uses existing rock material.
// Dependencies/Pointers: no clock/reset, actor route, game state or source catalog mutation. Actual source import, native pixels and browser acceptance pending.
