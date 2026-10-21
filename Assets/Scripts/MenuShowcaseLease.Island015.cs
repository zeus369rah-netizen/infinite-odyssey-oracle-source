// # ARCHITECTURE SNAPSHOT
// Coordinate Space: private layer30/rendering-layer7 preview; original catalog-local metres only.
// Array Formats: four owned immutable combined meshes, three private materials, two cached fill lights, focus+velocity float3[2].
// Dependencies/Pointers: MenuShowcaseLease owns all native/Object lifetime; no gameplay, settings or room authority.
using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace OdysseyOracle
{
    public sealed partial class MenuShowcaseLease
    {
        static uint islandSequence015;
        static readonly int IslandClock015=Shader.PropertyToID("_LocalClock"),IslandDay015=Shader.PropertyToID("_Daylight");
        readonly Mesh[] islandMeshes015=new Mesh[4];
        readonly Light[] islandFill015=new Light[2];
        int islandFillCount015,firstOraclePose015,secondOraclePose015;
        NativeArray<float3> islandFocus015;
        Material islandCloud015,islandRain015;
        Renderer islandRainRenderer015;
        int viewportPixelsX015,viewportPixelsY015;
        uint islandSeed015;
        float islandWeatherClock015;
        bool islandNight015,islandWet015;
        public uint MenuSeed015=>islandSeed015;
        public bool MenuNight015=>islandNight015;
        public bool MenuRainEligible015=>islandWet015&&quality==2;
        public int OwnedIslandMeshCount015{get;private set;}
        public int OwnedIslandVertexCount015{get;private set;}
        public int ExcludedPreviewGridCount015{get;private set;}
        public int PreviewSamples015=>Target==null?0:Target.antiAliasing;

        void InitializeIsland015(MenuShowcaseCatalog catalog,uint startupSeed)
        {
            // Local startup presentation only. Never consume UnityEngine.Random or an authoritative room generator.
            islandSeed015=startupSeed!=0?startupSeed:MenuIslandMath015.Mix(unchecked((uint)DateTime.UtcNow.Ticks+ ++islandSequence015));
            islandNight015=(islandSeed015&1u)!=0;islandWet015=(MenuIslandMath015.Mix(islandSeed015+29u)&3u)==0;
            Shader shader=Resources.Load<Shader>("Shaders/OracleMenuIsland015");
            if(shader==null||!shader.isSupported)throw new InvalidOperationException("Original menu island shader unavailable; static menu fallback required.");
            var rock=new Material(shader){name="Original menu rock and lower skyline015"};ownedMaterials.Add(rock);
            islandCloud015=new Material(shader){name="Original menu cloud silhouettes015"};ownedMaterials.Add(islandCloud015);islandCloud015.SetFloat("_Mode",1);
            islandRain015=new Material(shader){name="Original restrained outdoor menu rain015"};ownedMaterials.Add(islandRain015);islandRain015.SetFloat("_Mode",2);
            islandRain015.SetFloat("_SrcBlend",(float)UnityEngine.Rendering.BlendMode.SrcAlpha);islandRain015.SetFloat("_DstBlend",(float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            islandRain015.SetFloat("_ZWrite",0);islandRain015.renderQueue=3000;islandRain015.SetOverrideTag("RenderType","Transparent");
            float daylight=islandNight015?0:1;rock.SetFloat(IslandDay015,daylight);islandCloud015.SetFloat(IslandDay015,daylight);islandRain015.SetFloat(IslandDay015,daylight);
            islandMeshes015[0]=MenuIslandGeometry015.BuildIsland(catalog.ViewBounds,catalog.HubAnchor.y,islandSeed015);
            islandMeshes015[1]=MenuIslandGeometry015.BuildDistant(catalog.ViewBounds,islandSeed015,1);
            islandMeshes015[2]=MenuIslandGeometry015.BuildDistant(catalog.ViewBounds,islandSeed015,2);
            islandMeshes015[3]=MenuIslandGeometry015.BuildDistant(catalog.ViewBounds,islandSeed015,3);
            Draw("Original floating island underside015",root.transform,islandMeshes015[0],rock);
            Draw("Original distant lower city015",root.transform,islandMeshes015[1],rock);
            Draw("Original below-island clouds015",root.transform,islandMeshes015[2],islandCloud015);
            islandRainRenderer015=Draw("Original outdoor preview rain015",root.transform,islandMeshes015[3],islandRain015);
            for(int i=0;i<islandMeshes015.Length;i++){OwnedIslandMeshCount015++;OwnedIslandVertexCount015+=islandMeshes015[i].vertexCount;}
            InitializeOuterIslands020(catalog,rock);
            camera.backgroundColor=islandNight015?new Color(.025f,.045f,.085f):islandWet015?new Color(.31f,.40f,.45f):new Color(.47f,.65f,.73f);
            for(int i=0;i<islandFillCount015;i++)
            {
                islandFill015[i].color=islandNight015?new Color(.55f,.70f,1f):new Color(1f,.93f,.79f);
                islandFill015[i].intensity=(i==0?3200f:1800f)*(islandNight015?.72f:1.4f);
            }
            islandFocus015=new NativeArray<float3>(2,Allocator.Persistent);islandFocus015[0]=F(PresentationBounds.center);islandFocus015[1]=float3.zero;
            viewportPixelsX015=Mathf.Max(1,Screen.width);viewportPixelsY015=Mathf.Max(1,Screen.height);
            UpdateIslandPresentation015(0);
        }
        void TrackIslandFill015(Light value){if(islandFillCount015<islandFill015.Length)islandFill015[islandFillCount015++]=value;}
        void UpdateIslandPresentation015(float presentationClock)
        {
            if(islandCloud015!=null)islandCloud015.SetFloat(IslandClock015,presentationClock);
            if(islandRain015!=null)islandRain015.SetFloat(IslandClock015,presentationClock);
            if(islandRainRenderer015!=null)islandRainRenderer015.enabled=islandWet015&&quality==2;
        }
        void UpdateIslandCamera015(float elapsed,float delta)
        {
            Bounds bounds=PresentationBounds;float3 center=F(bounds.center);
            float3 first=center,second=center;
            if(poses.IsCreated&&poses.Length>secondOraclePose015&&poses[firstOraclePose015].Visible!=0&&poses[secondOraclePose015].Visible!=0)
            {first=poses[firstOraclePose015].Position+new float3(0,1,0);second=poses[secondOraclePose015].Position+new float3(0,1,0);}
            fence=new MenuIslandCameraJob015{Center=center,Extents=F(bounds.extents),Aspect=viewportAspect,Safe=safeViewport,Time=elapsed,Delta=delta,
                First=first,Second=second,Focus=islandFocus015,Result=cameraFrames}.Schedule();scheduled=true;Complete();
            var frame=cameraFrames[0];if(frame.Valid==0)throw new InvalidOperationException("Finite bounded menu island camera frame required.");
            camera.transform.SetLocalPositionAndRotation(V(frame.Position),Q(frame.Rotation));camera.orthographicSize=frame.OrthographicSize;camera.nearClipPlane=frame.Near;camera.farClipPlane=frame.Far;
        }
        public void SetViewport(float aspect,float4 safe,int pixelWidth,int pixelHeight)
        {
            if(disposed)return;
            if(!MenuShowcaseCameraMath.ValidViewport(aspect,safe)||pixelWidth<1||pixelHeight<1)throw new ArgumentOutOfRangeException(nameof(safe));
            bool pixels=pixelWidth!=viewportPixelsX015||pixelHeight!=viewportPixelsY015;
            if(!pixels&&aspect==viewportAspect&&math.all(safe==safeViewport))return;
            Complete();bool resize=pixels||aspect!=viewportAspect;viewportAspect=aspect;safeViewport=safe;camera.aspect=aspect;
            viewportPixelsX015=pixelWidth;viewportPixelsY015=pixelHeight;
            if(resize)ResizeTarget();dirty=true;staticRendered=false;
        }
        int SupportedSamples015(RenderTextureDescriptor descriptor)
        {
            if(quality!=2)return 1;
            var renderer=camera.GetUniversalAdditionalCameraData().scriptableRenderer;
            if(renderer==null||!renderer.supportedRenderingFeatures.msaa)return 1;
            descriptor.msaaSamples=2;
            return SystemInfo.GetRenderTextureSupportedMSAASampleCount(descriptor)>=2?2:1;
        }
        static bool IsInactivePreviewGrid015(Material value)
        {
            // Exact source identity only; no tint/material substring heuristic and no live world barrier mutation.
            return value!=null&&(value.shader!=null&&value.shader.name=="OdysseyOracle/LaneGateGrid"||value.name=="Inactive elemental corridor red grid"||
                value.name=="Original arena static preview / Inactive elemental corridor red grid");
        }
        void DisposeIsland015()
        {
            DisposeOuterIslands020();
            if(islandFocus015.IsCreated)islandFocus015.Dispose();
            for(int i=0;i<islandMeshes015.Length;i++)if(islandMeshes015[i]!=null){UnityEngine.Object.Destroy(islandMeshes015[i]);islandMeshes015[i]=null;}
            islandCloud015=null;islandRain015=null;islandRainRenderer015=null;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: private preview stage/layer30/renderlayer7; physical screen pixels cap Ultra1920×1080, Low640×360; no OS focus mutation.
// Array Formats: four owned combined immutable meshes, three private materials, two cached fill lights, persistent float3 focus+velocity[2]. Same IJob fence completes before read/publication/disposal;12additional bytes only.
// Dependencies/Pointers: local startup day/night/light rain only; original catalog borrowed unchanged, exact inactive grid excluded only from preview. No RenderSettings/global shader/Random/room/progression/preferences writes. Dispose runs after existing fence; owned materials destroyed by existing owner. Actual shader/native/browser acceptance pending.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: original catalog-local presentation plus shared detached outer-island geometry.
// Array Formats: old four island/weather meshes keep their existing counts; new public outer-island count1 and1620vertices remain separate.
// Dependencies/Pointers: existing cold rock material is borrowed and disposed once by its original owner; extra mesh disposed explicitly before native focus storage. No new presentation clock or gameplay mutation.
