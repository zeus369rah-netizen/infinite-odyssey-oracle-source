using UnityEngine;
using UnityEngine.Rendering;
namespace OdysseyOracle
{
    [DisallowMultipleComponent]
    public sealed class CampaignArrivalPresentation020:MonoBehaviour
    {
        static readonly int PhaseId=Shader.PropertyToID("_ArrivalPhase020"),AspectId=Shader.PropertyToID("_ArrivalAspect020"),MotionId=Shader.PropertyToID("_ArrivalMotion020");
        const float Duration=1.1f;
        GameSession session;
        Material material;
        Mesh mesh;
        uint generation,revision;
        uint lastPresentedRevision;
        DungeonWorld lastPresentedWorld;
        float elapsed;
        int lastFrame=-1;
        bool active,subscribed,attempted,ready;
        public bool Active020=>active;
        public float Remaining020=>active?Mathf.Max(0,Duration-elapsed):0;
        public int AcceptedVisualStarts020 {get;private set;}
        public string Preparation020 {get;private set;}="Unprepared";
        internal bool PlayAccepted(GameSession owner,uint acceptedGeneration,uint acceptedRevision)
        {
            if(owner==null||!owner.CampaignHubActive020||owner.World==null||!owner.World.CampaignHubActive020||
                acceptedGeneration==0||acceptedGeneration!=owner.World.CampaignHubGeneration020||acceptedRevision==0||acceptedRevision!=owner.World.WorldRevision020||
                owner.CampaignArrivalAcceptedRevision020!=acceptedRevision)return false;
            if(ReferenceEquals(session,owner)&&ReferenceEquals(lastPresentedWorld,owner.World)&&acceptedRevision<=lastPresentedRevision)return false;
            session=owner;generation=acceptedGeneration;revision=acceptedRevision;elapsed=0;lastFrame=-1;
            lastPresentedWorld=owner.World;lastPresentedRevision=acceptedRevision;
            if(!Prepare())return false;
            active=true;if(AcceptedVisualStarts020<int.MaxValue)AcceptedVisualStarts020++;return true;
        }
        bool Prepare()
        {
            if(attempted)return ready;attempted=true;
            Shader shader=Resources.Load<Shader>("Shaders/PortalArrivalOverlay020");
            if(shader==null||!shader.isSupported){Preparation020="Unsupported arrival shader; accepted token stays consumed";return false;}
            try
            {
                material=new Material(shader){name="Original accepted purple arrival veil020",hideFlags=HideFlags.DontSave};
                mesh=new Mesh{name="One reused accepted arrival screen quad020",hideFlags=HideFlags.DontSave};
                mesh.vertices=new[]{new Vector3(-1,-1,0),new Vector3(1,-1,0),new Vector3(1,1,0),new Vector3(-1,1,0)};
                mesh.uv=new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1)};mesh.triangles=new[]{0,1,2,0,2,3};
                mesh.bounds=new Bounds(Vector3.zero,Vector3.one*4);mesh.UploadMeshData(true);
                RenderPipelineManager.beginCameraRendering+=BeforeCamera;subscribed=true;ready=true;Preparation020="Prepared; actual visual acceptance pending";return true;
            }
            catch{Release();Preparation020="Preparation failed; accepted token stays consumed";throw;}
        }
        bool Valid()=>session!=null&&session.isActiveAndEnabled&&ReferenceEquals(GameSession.Instance,session)&&session.CampaignHubActive020&&session.World!=null&&
            session.World.CampaignHubActive020&&session.World.CampaignHubGeneration020==generation&&session.World.WorldRevision020==revision;
        void BeforeCamera(ScriptableRenderContext context,Camera camera)
        {
            if(!active)return;if(!Valid()){Retire();return;}
            if(camera==null||session.Player==null||camera!=session.Player.ViewCamera)return;
            if(lastFrame!=Time.frameCount)
            {
                lastFrame=Time.frameCount;
                if(session.IsPlaying&&!session.Paused&&Application.isFocused&&!OracleBookAccessibility.PrefersReducedMotion)
                    elapsed+=Mathf.Clamp(Time.unscaledDeltaTime,0,.15f);
                // Reduced motion uses a short stationary peripheral fade instead of a frozen overlay.
                if(OracleBookAccessibility.PrefersReducedMotion&&session.IsPlaying&&Application.isFocused)elapsed+=Mathf.Clamp(Time.unscaledDeltaTime,0,.15f);
            }
            if(elapsed>=Duration){Retire();return;}
            material.SetFloat(PhaseId,elapsed/Duration);material.SetFloat(AspectId,Mathf.Max(.5f,camera.aspect));
            material.SetFloat(MotionId,OracleBookAccessibility.PrefersReducedMotion?0:1);
            var parameters=new RenderParams(material){camera=camera,worldBounds=new Bounds(camera.transform.position,Vector3.one*4),shadowCastingMode=ShadowCastingMode.Off,
                receiveShadows=false,lightProbeUsage=LightProbeUsage.Off,reflectionProbeUsage=ReflectionProbeUsage.Off};
            Graphics.RenderMesh(parameters,mesh,0,Matrix4x4.identity);
        }
        public void Retire(){active=false;generation=revision=0;elapsed=0;lastFrame=-1;}
        void OnDisable(){Retire();}
        void OnDestroy(){Retire();Release();session=null;}
        void Release()
        {
            if(subscribed)RenderPipelineManager.beginCameraRendering-=BeforeCamera;subscribed=ready=false;
            DestroyOwned(material);DestroyOwned(mesh);material=null;mesh=null;
        }
        static void DestroyOwned(UnityEngine.Object value){if(value==null)return;if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: One accepted positive hub generation/revision; normalized full-screen quad, no world geometry/scene sampling/camera motion. Sound remains existing-pool owned.
// Array Formats: One cold quad/material/adapter and bounded1.1active-second scalar phase; one draw only while active, no Update, hot allocation, new camera or render texture.
// Dependencies/Pointers: Session owns consumed receipt; camera observer validates actual admission each render and retires stale/disabled contexts. Private GPU resources reused and destroyed only by this adapter; root checks native center readability/focus holds/actual pooled sound once.
