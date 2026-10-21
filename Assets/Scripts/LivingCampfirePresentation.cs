using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace OdysseyOracle
{
    // Presentation only. Existing World owns floor/logs/stones/collision/fire illumination.
    [DisallowMultipleComponent]
    public sealed class LivingCampfirePresentation : MonoBehaviour
    {
        public const int FlameLobes=5,Embers=8,SmokeWisps=4,Segments=10,Rings=7;
        public const int VertexCount=FlameLobes*(Segments*Rings+2)+Embers*6+SmokeWisps*4;
        public const int IndexCount=FlameLobes*(Segments*(Rings-1)*6+Segments*6)+Embers*24+SmokeWisps*6;
        private static readonly int ClockId=Shader.PropertyToID("_LocalClock"),DetailId=Shader.PropertyToID("_Detail");
        [SerializeField] private Mesh mesh;
        [SerializeField] private Material material;
        [SerializeField] private MeshRenderer display;
        [SerializeField] private float clock=.31f;
        [SerializeField] private int quality=1;
        private MaterialPropertyBlock block;
        [SerializeField] private float roomReactionFuel=1f;
        public void SetRoomReactionFuel(float value){value=float.IsNaN(value)||float.IsInfinity(value)?0:Mathf.Clamp01(value);if(roomReactionFuel==value)return;roomReactionFuel=value;Apply();}
        private void Awake(){block=new MaterialPropertyBlock();}
        public bool ShaderPresentationReady=>mesh!=null&&material!=null&&display!=null;
        public float PresentationClock=>clock;
        public int ParticleQuality=>quality;
        public static bool TryCreate(Transform borrowedParent,Vector3 localBase,float height,out LivingCampfirePresentation presentation)
        {
            presentation=null;
            if(borrowedParent==null)throw new ArgumentNullException(nameof(borrowedParent));
            if(float.IsNaN(height)||float.IsInfinity(height)||height<.15f||height>1.2f)throw new ArgumentOutOfRangeException(nameof(height));
            Shader shader=Resources.Load<Shader>("Shaders/OracleLivingCampfire");
            if(shader==null||!shader.isSupported)
            {
                Debug.LogWarning("Original tapered campfire unavailable; existing voxel fallback is not refined-flame visual acceptance.");return false;
            }
            GameObject root=null;
            try
            {
                root=new GameObject("Original tapered living campfire",typeof(MeshFilter),typeof(MeshRenderer));root.transform.SetParent(borrowedParent,false);root.transform.localPosition=localBase;
                presentation=root.AddComponent<LivingCampfirePresentation>();
                presentation.mesh=BuildMesh(height);
                presentation.material=new Material(shader){name="Original tapered campfire private material"};presentation.material.SetFloat("_Height",height);
                root.GetComponent<MeshFilter>().sharedMesh=presentation.mesh;presentation.display=root.GetComponent<MeshRenderer>();presentation.display.sharedMaterial=presentation.material;
                presentation.display.shadowCastingMode=ShadowCastingMode.Off;presentation.display.receiveShadows=false;
                presentation.display.lightProbeUsage=LightProbeUsage.Off;presentation.display.reflectionProbeUsage=ReflectionProbeUsage.Off;
                presentation.Apply();return true;
            }
            catch{if(root!=null){root.SetActive(false);Release(root);}presentation=null;throw;}
        }
        private static Mesh BuildMesh(float height)
        {
            var vertices=new Vector3[VertexCount];var uv=new Vector2[VertexCount];var tags=new Vector4[VertexCount];var indices=new int[IndexCount];int vertex=0,index=0;
            for(int lobe=0;lobe<FlameLobes;lobe++)
            {
                int first=vertex;
                for(int ring=0;ring<Rings;ring++)for(int side=0;side<Segments;side++)
                {
                    float angle=side*Mathf.PI*2/Segments,y=ring*.15f;
                    Put(new Vector3(Mathf.Cos(angle),y,Mathf.Sin(angle)),new Vector2((float)side/Segments,y),0,lobe);
                }
                int bottom=vertex;Put(Vector3.zero,Vector2.zero,0,lobe);int top=vertex;Put(new Vector3(0,1.05f,0),new Vector2(.5f,1.05f),0,lobe);
                for(int side=0;side<Segments;side++)
                {
                    int next=(side+1)%Segments;Tri(bottom,first+side,first+next);
                    for(int ring=0;ring<Rings-1;ring++)
                    {
                        int a=first+ring*Segments+side,b=first+(ring+1)*Segments+side,c=first+(ring+1)*Segments+next,d=first+ring*Segments+next;
                        Tri(a,b,c);Tri(a,c,d);
                    }
                    Tri(top,first+(Rings-1)*Segments+next,first+(Rings-1)*Segments+side);
                }
            }
            for(int ember=0;ember<Embers;ember++)
            {
                int at=vertex;Put(Vector3.up,Vector2.zero,1,ember);Put(Vector3.down,Vector2.zero,1,ember);
                Put(Vector3.right,Vector2.zero,1,ember);Put(Vector3.forward,Vector2.zero,1,ember);Put(Vector3.left,Vector2.zero,1,ember);Put(Vector3.back,Vector2.zero,1,ember);
                for(int side=0;side<4;side++){int a=at+2+side,b=at+2+(side+1)%4;Tri(at,a,b);Tri(at+1,b,a);}
            }
            for(int smoke=0;smoke<SmokeWisps;smoke++)
            {
                int at=vertex;Put(new Vector3(-1,-1,0),new Vector2(0,0),2,smoke);Put(new Vector3(1,-1,0),new Vector2(1,0),2,smoke);
                Put(new Vector3(1,1,0),new Vector2(1,1),2,smoke);Put(new Vector3(-1,1,0),new Vector2(0,1),2,smoke);
                Tri(at,at+1,at+2);Tri(at,at+2,at+3);
            }
            if(vertex!=VertexCount||index!=IndexCount)throw new InvalidOperationException("Original living campfire fixed capacity changed.");
            var result=new Mesh{name="Original five tapered lobes eight embers four smoke wisps"};
            try
            {
                result.vertices=vertices;result.uv=uv;result.SetUVs(1,tags);result.triangles=indices;
                result.bounds=new Bounds(new Vector3(0,height*1.5f,0),new Vector3(height*2.2f,height*3.6f,height*2.2f));result.UploadMeshData(true);return result;
            }
            catch{Release(result);throw;}
            void Put(Vector3 position,Vector2 tex,int kind,int id){vertices[vertex]=position;uv[vertex]=tex;tags[vertex]=new Vector4(kind,id,id*2.39996323f,0);vertex++;}
            void Tri(int a,int b,int c){indices[index++]=a;indices[index++]=b;indices[index++]=c;}
        }
        private void Update()
        {
            if(!ShaderPresentationReady)return;
            GameSession owner=GameSession.Instance;
            int next=owner!=null&&owner.Graphics!=null?Mathf.Clamp(owner.Graphics.ParticleQuality,0,2):1;
            bool held=!Application.isFocused||OracleBookAccessibility.PrefersReducedMotion||owner!=null&&owner.HasStarted&&owner.Paused;
            TickPresentation(Time.unscaledDeltaTime,held,next);
        }
        // Reusable presentation-only adapter and bounded background fixture entry.
        // Supplying a clock step never changes gameplay time, physics, saves or lights.
        public bool TickPresentation(float deltaSeconds,bool held,int particleQuality)
        {
            if(float.IsNaN(deltaSeconds)||float.IsInfinity(deltaSeconds))throw new ArgumentException("Finite presentation delta required.");
            if(!ShaderPresentationReady)return false;
            int next=Mathf.Clamp(particleQuality,0,2);bool changed=next!=quality;quality=next;
            float delta=held?0:Mathf.Clamp(deltaSeconds,0,.1f);
            if(delta>0){clock+=delta;changed=true;}
            if(changed)Apply();
            return delta>0;
        }
        private void Apply(){if(display==null||block==null)return;block.SetFloat(ClockId,clock);block.SetFloat(DetailId,quality);block.SetFloat("_Fuel",roomReactionFuel);display.SetPropertyBlock(block);}
        private void OnEnable(){Apply();}
        private void OnDestroy(){if(mesh!=null){Release(mesh);mesh=null;}if(material!=null){Release(material);material=null;}}
        private static void Release(UnityEngine.Object value){if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing campfire base in geometry-root local+Z/Y-up; private bounded clock seconds. Numeric shader shape stays inside fixed local AABB; no floor/collider displacement.
// Array Formats: Fixed424 vertices/2316indices, five 3D tapered lobes/eight octahedral embers/four billboard smoke wisps; UV1(type,id,seed,0). Immutable mesh, one cached MPB; no vertex uploads or per-frame arrays/Objects.
// Dependencies/Pointers: Owns one mesh/material/child presentation; World retains logs/floor/colliders/lighting/audio. Existing quality/pause/focus/accessibility hold private clock. Shader failure is explicit fallback, not animation acceptance.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing refuge geometry/world axes and support pivots unchanged; elemental numeric state applies only to the supported refuge.
// Array Formats: Existing owner arrays remain canonical; fixed512-node/8word reaction state supplies bounded scalar presentation gains.
// Dependencies/Pointers: Host accepted single Fire/Earth/Wind shots only; existing Object adapters retain materials/lights/lifetime, no rewards/saves or shrine/portal gameplay mutation.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: existing anchored presentation receives the same bounded passive world-XZ field; geometry/physics/accepted room fuel stay canonical.
// Array Formats: existing fixed mesh/vertex/material owner and one shared float4; no hot collections, per-window objects or mutable gameplay alias.
// Dependencies/Pointers: work-only shared-breeze adaptation; original shaders and fallback lifetimes remain. Root alone imports/tests actual native/WebGL visuals, culling, allocation and lifecycle.
