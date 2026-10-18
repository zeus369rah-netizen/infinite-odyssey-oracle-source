using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OdysseyOracle
{
    /// <summary>
    /// Original animated ferrofluid/accretion surface. This is a bounded visual
    /// lens effect, with no camera movement, force, gameplay or physical simulation.
    /// Attach to a NEW child of the portal aperture; the caller keeps its frame,
    /// collider, seal and interaction. Local +Z faces outward. An inherited XY
    /// scale may make the circular surface elliptical.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PortalAnomaly : MonoBehaviour
    {
        const string ResourcePath="Shaders/PortalAnomaly";
        const float OuterRadius=1.22f;
        static readonly int ClockId=Shader.PropertyToID("_AnomalyTime");
        static readonly int QualityId=Shader.PropertyToID("_HighQuality");
        static readonly int OpaqueId=Shader.PropertyToID("_UseOpaqueTexture");
        static readonly int RadiusId=Shader.PropertyToID("_Radius");
        static readonly int DistortionId=Shader.PropertyToID("_MaxDistortionPixels");
        static Material sharedSurface;
        static int materialUsers;

        GameObject surface;
        Mesh surfaceMesh;
        MeshFilter filter;
        MeshRenderer surfaceRenderer;
        MaterialPropertyBlock properties;
        float radius=1.4f,clock;
        bool highQuality=true,ownsMaterial,subscribed;

        public bool IsReady => surfaceRenderer!=null;

        /// <summary>World center only; existing rotation and parent scale are preserved.</summary>
        public void Initialize(Vector3 center,float radius=1.4f)
        {
            Retire();
            transform.position=center;
            this.radius=Mathf.Clamp(radius,.35f,4f);clock=0f;
            if(sharedSurface==null)
            {
                // Resources retention prevents the only shader consumer being stripped.
                var shader=Resources.Load<Shader>(ResourcePath);
                if(shader==null || !shader.isSupported)
                {
                    Debug.LogWarning("Portal anomaly shader is unavailable; retain the caller's portal surface.",this);
                    return;
                }
                sharedSurface=new Material(shader){name="Original portal ferrofluid / shared"};
            }
            materialUsers++;ownsMaterial=true;
            surface=new GameObject("Original black ferrofluid and purple accretion lens");
            surface.layer=gameObject.layer;surface.transform.SetParent(transform,false);
            filter=surface.AddComponent<MeshFilter>();
            surfaceRenderer=surface.AddComponent<MeshRenderer>();
            surfaceRenderer.sharedMaterial=sharedSurface;
            surfaceRenderer.shadowCastingMode=ShadowCastingMode.Off;
            surfaceRenderer.receiveShadows=false;
            surfaceRenderer.lightProbeUsage=LightProbeUsage.Off;
            surfaceRenderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            properties=new MaterialPropertyBlock();
            BuildMesh();ApplyProperties(false);Subscribe();
        }

        /// <summary>Low quality uses 1,200 triangles and never samples scene color.</summary>
        public void SetQuality(bool highQuality)
        {
            if(this.highQuality==highQuality)return;
            this.highQuality=highQuality;
            if(surfaceRenderer!=null){BuildMesh();ApplyProperties(false);}
        }

        void Update()
        {
            if(surfaceRenderer==null)return;
            var session=GameSession.Instance;
            if(session==null || (session.HasStarted && !session.Paused && !session.Victory && !session.IsDead))
                clock+=Mathf.Min(Time.deltaTime,.05f);
            // A local uniform freezes the animation on pause; no global shader time.
            if(properties==null)properties=new MaterialPropertyBlock();
            properties.SetFloat(ClockId,clock);
            surfaceRenderer.SetPropertyBlock(properties);
        }

        void BeforeCamera(ScriptableRenderContext context,Camera camera)
        {
            if(this==null || !isActiveAndEnabled || surfaceRenderer==null || camera==null)return;
            bool useOpaque=false;
            // Never request or allocate an opaque copy here. Use only an already
            // configured URP base game camera; overlay/reflection/preview use fallback.
            var pipeline=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if(highQuality && pipeline!=null && camera.cameraType==CameraType.Game)
            {
                if(camera.TryGetComponent<UniversalAdditionalCameraData>(out var data))
                    useOpaque=data.renderType==CameraRenderType.Base && data.requiresColorTexture;
                else useOpaque=pipeline.supportsCameraOpaqueTexture;
            }
            ApplyProperties(useOpaque);
        }

        void ApplyProperties(bool useOpaque)
        {
            if(surfaceRenderer==null)return;
            if(properties==null)properties=new MaterialPropertyBlock();
            properties.SetFloat(ClockId,clock);
            properties.SetFloat(QualityId,highQuality?1f:0f);
            properties.SetFloat(OpaqueId,useOpaque?1f:0f);
            properties.SetFloat(RadiusId,radius);
            properties.SetFloat(DistortionId,highQuality?14f:0f);
            surfaceRenderer.SetPropertyBlock(properties);
        }

        void BuildMesh()
        {
            int segments=highQuality?160:80,rings=highQuality?16:8;
            // One center + duplicated seam per ring. 2,577 / 649 vertices;
            // 4,960 / 1,200 triangles. No per-frame CPU mesh updates.
            var vertices=new Vector3[1+rings*(segments+1)];
            var normals=new Vector3[vertices.Length];var uv=new Vector2[vertices.Length];
            var indices=new int[(segments+(rings-1)*segments*2)*3];
            normals[0]=Vector3.forward;
            for(int ring=0;ring<rings;ring++)
            {
                float r=OuterRadius*(ring+1)/rings;
                for(int i=0;i<=segments;i++)
                {
                    float angle=i*Mathf.PI*2f/segments;
                    int at=1+ring*(segments+1)+i;
                    Vector2 p=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*r;
                    vertices[at]=new Vector3(p.x*radius,p.y*radius,0f);
                    normals[at]=Vector3.forward;uv[at]=p;
                }
            }
            int index=0;
            for(int i=0;i<segments;i++){indices[index++]=0;indices[index++]=1+i;indices[index++]=2+i;}
            for(int ring=1;ring<rings;ring++)for(int i=0;i<segments;i++)
            {
                int inner=1+(ring-1)*(segments+1)+i,outer=inner+segments+1;
                indices[index++]=inner;indices[index++]=outer;indices[index++]=outer+1;
                indices[index++]=inner;indices[index++]=outer+1;indices[index++]=inner+1;
            }
            var mesh=new Mesh{name=highQuality?"Original portal lens / 4960 triangles":"Original portal lens / 1200 triangles"};
            mesh.vertices=vertices;mesh.normals=normals;mesh.uv=uv;mesh.triangles=indices;
            // Shader displacement stays below 1.30r XY and .12r Z. Explicit
            // bounds avoid edge/spike culling without giant scene-wide bounds.
            mesh.bounds=new Bounds(Vector3.zero,new Vector3(radius*2.60f,radius*2.60f,radius*.26f));
            mesh.UploadMeshData(true);
            var previous=surfaceMesh;surfaceMesh=mesh;filter.sharedMesh=mesh;
            if(previous!=null)Release(previous);
        }

        void OnEnable(){if(surfaceRenderer!=null)Subscribe();}
        void OnDisable(){Unsubscribe();}
        void OnDestroy(){Retire();}
        void Subscribe()
        {
            if(subscribed || !isActiveAndEnabled)return;
            RenderPipelineManager.beginCameraRendering+=BeforeCamera;subscribed=true;
        }
        void Unsubscribe()
        {
            if(!subscribed)return;
            RenderPipelineManager.beginCameraRendering-=BeforeCamera;subscribed=false;
        }
        void Retire()
        {
            Unsubscribe();
            if(surface!=null){surface.SetActive(false);Release(surface);}
            if(surfaceMesh!=null)Release(surfaceMesh);
            if(ownsMaterial)
            {
                materialUsers=Mathf.Max(0,materialUsers-1);ownsMaterial=false;
                if(materialUsers==0 && sharedSurface!=null){Release(sharedSurface);sharedSurface=null;}
            }
            surface=null;surfaceMesh=null;filter=null;surfaceRenderer=null;properties=null;
        }
        static void Release(Object item)
        {
            if(Application.isPlaying)Destroy(item);else DestroyImmediate(item);
        }
    }
}
