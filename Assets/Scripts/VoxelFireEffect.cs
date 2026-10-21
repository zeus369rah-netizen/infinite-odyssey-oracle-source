using UnityEngine;
using UnityEngine.Rendering;

namespace OdysseyOracle
{
    /// <summary>Original stepped flame and rising voxel smoke. One mesh, eight reusable cubes, no physics or light.</summary>
    [DisallowMultipleComponent]
    public sealed class VoxelFireEffect : MonoBehaviour
    {
        // # ARCHITECTURE SNAPSHOT
        // Main-thread presentation retains eight cached cubes and three submeshes.
        // Two crossed opaque hot-core strips replace one hidden core/outer wisp;
        // no light, renderer, per-frame allocation or new shader is introduced.
        const int Bits = 8, VerticesPerBit = 24;
        static Material sharedSmoke;
        static int smokeUsers;
        static readonly int[] Faces = { 0,1,2,0,2,3 };
        static readonly Vector3[] Normals = { Vector3.forward,Vector3.back,Vector3.right,Vector3.left,Vector3.up,Vector3.down };
        static readonly Vector3[] Corners = {
            new Vector3(-1,-1,1),new Vector3(1,-1,1),new Vector3(1,1,1),new Vector3(-1,1,1),
            new Vector3(1,-1,-1),new Vector3(-1,-1,-1),new Vector3(-1,1,-1),new Vector3(1,1,-1),
            new Vector3(1,-1,1),new Vector3(1,-1,-1),new Vector3(1,1,-1),new Vector3(1,1,1),
            new Vector3(-1,-1,-1),new Vector3(-1,-1,1),new Vector3(-1,1,1),new Vector3(-1,1,-1),
            new Vector3(-1,1,1),new Vector3(1,1,1),new Vector3(1,1,-1),new Vector3(-1,1,-1),
            new Vector3(-1,-1,-1),new Vector3(1,-1,-1),new Vector3(1,-1,1),new Vector3(-1,-1,1)
        };
        static readonly int[] OrangeHigh = Indices(0,2), OrangeLow = Indices(0,1);
        static readonly int[] HotHigh = Indices(2,2), HotLow = Indices(3,1);
        static readonly int[] SmokeHigh = Indices(4,4), SmokeLow = Indices(4,2), Empty = new int[0];
        readonly Vector3[] vertices = new Vector3[Bits*VerticesPerBit];
        Mesh mesh;
        [SerializeField] float roomReactionFuel=1f;
        public void SetRoomReactionFuel(float value){value=float.IsNaN(value)||float.IsInfinity(value)?0:Mathf.Clamp01(value);if(roomReactionFuel==value)return;roomReactionFuel=value;if(mesh!=null)Animate();}
        MeshRenderer meshRenderer;
        float height = .65f, clock, phase;
        int quality = -1, lastFrame = -1;
        bool smoke, ownsSmoke;
        Vector2 passiveWindOffset015;

        /// <summary>Caller supplies the existing shared fire materials. Transform is the local flame base.</summary>
        public void Initialize(Material orange, Material hotCore, float flameHeight, bool emitSmoke = true)
        {
            if (mesh != null) return;
            if (orange == null || hotCore == null) throw new System.ArgumentNullException("Fire requires the existing shared flame materials.");
            height = float.IsNaN(flameHeight) || float.IsInfinity(flameHeight) ? .65f : Mathf.Clamp(flameHeight,.15f,1.2f);
            smoke = emitSmoke;
            if (sharedSmoke == null) {
                // Clone the already retained URP shader; no Shader.Find or custom shader requirement.
                sharedSmoke = new Material(orange) { name = "Original soft charcoal voxel smoke / shared" };
                sharedSmoke.SetColor("_BaseColor",new Color(.24f,.27f,.30f,.23f));
                if(sharedSmoke.HasProperty("_Color"))sharedSmoke.SetColor("_Color",new Color(.24f,.27f,.30f,.23f));
                if(sharedSmoke.HasProperty("_BaseMap"))sharedSmoke.SetTexture("_BaseMap",Texture2D.whiteTexture);
                if(sharedSmoke.HasProperty("_EmissionColor"))sharedSmoke.SetColor("_EmissionColor",Color.black);
                sharedSmoke.DisableKeyword("_EMISSION");
                sharedSmoke.SetFloat("_Surface",1f);sharedSmoke.SetFloat("_Blend",0f);
                sharedSmoke.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);
                sharedSmoke.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
                sharedSmoke.SetFloat("_ZWrite",0f);
                sharedSmoke.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                sharedSmoke.DisableKeyword("_ALPHATEST_ON");
                sharedSmoke.SetOverrideTag("RenderType","Transparent");
                sharedSmoke.renderQueue = (int)RenderQueue.Transparent;
            }
            smokeUsers++;ownsSmoke=true;
            mesh = new Mesh { name = "Eight reusable original flame and smoke voxels" };
            mesh.MarkDynamic();
            var normals = new Vector3[vertices.Length];
            var uv = new Vector2[vertices.Length];
            for(int bit=0;bit<Bits;bit++)for(int i=0;i<VerticesPerBit;i++) {
                normals[bit*VerticesPerBit+i]=Normals[i/4];
                uv[bit*VerticesPerBit+i]=new Vector2(i%4==1||i%4==2?1:0,i%4>=2?1:0);
            }
            mesh.vertices=vertices;mesh.normals=normals;mesh.uv=uv;mesh.subMeshCount=3;
            var filter=gameObject.AddComponent<MeshFilter>();filter.sharedMesh=mesh;
            meshRenderer=gameObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterials=new[]{orange,hotCore,sharedSmoke};
            meshRenderer.shadowCastingMode=ShadowCastingMode.Off;meshRenderer.receiveShadows=false;
            meshRenderer.lightProbeUsage=LightProbeUsage.BlendProbes;
            meshRenderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            phase=Mathf.Repeat(transform.position.x*.71f+transform.position.z*.39f,6.283185f);
            SetQuality(1);Animate();
        }
        public void SetQuality(int particleQuality)
        {
            int next=Mathf.Clamp(particleQuality,0,2);
            if(mesh==null||next==quality)return;
            quality=next;lastFrame=-1;
            mesh.SetTriangles(quality==0?OrangeLow:OrangeHigh,0,false);
            mesh.SetTriangles(quality==0?HotLow:HotHigh,1,false);
            mesh.SetTriangles(!smoke?Empty:quality==0?SmokeLow:SmokeHigh,2,false);
            // Fixed conservative bounds avoid per-frame RecalculateBounds and smoke popping at frame edges.
            mesh.bounds=new Bounds(new Vector3(0,height*2.3f,0),new Vector3(height*2.8f,height*5.4f,height*2.8f));
        }
        void Update()
        {
            if(mesh==null)return;
            var session=GameSession.Instance;
            if(session!=null&&session.Graphics!=null)SetQuality(session.Graphics.ParticleQuality);
            // Fire is alive in the lobby, then freezes in an explicit gameplay pause.
            if(!Application.isFocused||OracleBookAccessibility.PrefersReducedMotion||session!=null&&session.HasStarted&&session.Paused)return;
            clock+=Mathf.Min(Time.unscaledDeltaTime,.1f);
            int frame=Mathf.FloorToInt(clock*(quality==0?8f:12f));
            if(frame==lastFrame)return;
            lastFrame=frame;Animate();
        }
        void Animate()
        {
            var owner=GameSession.Instance;Vector4 field=owner!=null&&owner.World!=null?owner.World.PassiveWindField015:Vector4.zero;
            Vector3 localVelocity=transform.worldToLocalMatrix.MultiplyVector(new Vector3(field.x,0,field.y));
            var bend=OdysseyOracle.Foundation.World.PassiveWindMath015.Offset(new Unity.Mathematics.float2(localVelocity.x,localVelocity.z),field.z,field.w,phase,height*.08f);
            passiveWindOffset015=new Vector2(bend.x,bend.y);
            float t=Mathf.Floor(clock*(quality==0?8f:12f))/(quality==0?8f:12f);
            float flicker=1f+Step(Mathf.Sin(t*11f+phase)*.15f,.05f);
            Write(0,new Vector3(0,height*.31f,0),new Vector3(height*.49f,height*.64f*flicker,height*.46f));
            Write(1,new Vector3(Step(Mathf.Sin(t*7f+phase)*height*.16f,height*.08f),height*.77f,0),new Vector3(height*.24f,height*.42f,height*.22f));
            // The old hot cube sat inside the opaque orange base. Slightly
            // exposed crossed faces keep the hot center visible at side angles.
            Write(2,new Vector3(0,height*.24f,0),new Vector3(height*.53f,height*.47f,height*.30f));
            Write(3,new Vector3(0,height*.24f,0),new Vector3(height*(quality==0?.53f:.30f),height*.47f,height*.52f));
            for(int i=0;i<4;i++) {
                float life=Mathf.Repeat(t*.28f+i*.25f+phase*.07f,1f);
                float y=height*(1.05f+Step(life*3.4f,.16f));
                float drift=Step(Mathf.Sin(i*2.1f+phase+life*2.2f)*height*.32f,height*.10f);
                float size=height*(.19f+Step(life*.39f,.065f));
                // Last 15% shrinks out in discrete voxels instead of a per-emitter material fade.
                if(life>.85f)size*=Mathf.Max(0f,1f-Mathf.Floor((life-.85f)/.05f)*.34f);
                Write(4+i,new Vector3(drift,y,Step((life-.5f)*height*.42f,height*.10f)),Vector3.one*size);
            }
            mesh.vertices=vertices;
        }
        void Write(int bit,Vector3 center,Vector3 size)
        {
            center*=roomReactionFuel;Vector3 half=size*(.5f*roomReactionFuel);
            for(int i=0;i<VerticesPerBit;i++){Vector3 point=center+Vector3.Scale(Corners[i],half);float q=Mathf.Clamp01((point.y-height*.02f)/Mathf.Max(height*.98f,.01f));point.x+=passiveWindOffset015.x*q*q;point.z+=passiveWindOffset015.y*q*q;vertices[bit*VerticesPerBit+i]=point;}
        }
        static float Step(float value,float step)=>Mathf.Round(value/step)*step;
        static int[] Indices(int first,int count)
        {
            var result=new int[count*36];int n=0;
            for(int bit=first;bit<first+count;bit++)for(int face=0;face<6;face++)for(int i=0;i<6;i++)
                result[n++]=bit*VerticesPerBit+face*4+Faces[i];
            return result;
        }
        void OnDestroy()
        {
            if(mesh!=null)Destroy(mesh);
            if(!ownsSmoke)return;
            ownsSmoke=false;smokeUsers=Mathf.Max(0,smokeUsers-1);
            if(smokeUsers==0&&sharedSmoke!=null){Destroy(sharedSmoke);sharedSmoke=null;}
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing refuge geometry/world axes and support pivots unchanged; elemental numeric state applies only to the supported refuge.
// Array Formats: Existing owner arrays remain canonical; fixed512-node/8word reaction state supplies bounded scalar presentation gains.
// Dependencies/Pointers: Host accepted single Fire/Earth/Wind shots only; existing Object adapters retain materials/lights/lifetime, no rewards/saves or shrine/portal gameplay mutation.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: existing anchored presentation receives the same bounded passive world-XZ field; geometry/physics/accepted room fuel stay canonical.
// Array Formats: existing fixed mesh/vertex/material owner and one shared float4; no hot collections, per-window objects or mutable gameplay alias.
// Dependencies/Pointers: work-only shared-breeze adaptation; original shaders and fallback lifetimes remain. Root alone imports/tests actual native/WebGL visuals, culling, allocation and lifecycle.
