using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using OdysseyOracle.CampaignNpcs;

namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // One presentation owner updates TWO neutral actors. Cold recipes become9 articulated mesh
    // groups each, one shared16x1 palette material and two tiny owned textures,0Lights/0AudioSources.
    // Serialized ownership survives domain reload;64 audio samples are reused at20Hz, no jobs/native
    // buffers. Caller owns refuge visibility, input/dialogue, authority and any borrowed voice source.
    [DisallowMultipleComponent]
    public sealed class CampaignNpcPresentation : MonoBehaviour
    {
        [Serializable] sealed class Actor
        {
            public int Role;public string Name;public GameObject Root;public Transform[] Parts;public Transform Gaze,Voice,Interaction;
            public Transform Torso,Head,LeftArm,RightArm,Mouth,Lids;public CapsuleCollider Collider;public AudioSource BorrowedVoice;
            public Vector3 TorsoRest;public float Age,NextBlink,BlinkAge=-1,Pitch,Yaw,Rms,SampleAt,ManualUntil;public bool Talking;
        }
        [SerializeField] Actor[] actors=new Actor[2];
        [SerializeField] List<UnityEngine.Object> owned=new List<UnityEngine.Object>(24);
        [SerializeField] OraclePlayer viewer;
        [SerializeField] bool visible;
        readonly float[] audioScratch=new float[64];
        public bool IsVisible=>visible;
        public const float InteractionReach=3.25f;
        public static Vector3 RunekeeperPosition=>new Vector3(-6.35f,0,9f);
        public static Vector3 GatekeeperPosition=>new Vector3(6.35f,0,18.8f);
        public delegate bool ExistingNpcResolver(int role,out Vector3 feet,out Vector3 aim);
        public ExistingNpcResolver ResolveExistingNpc {get;set;}

        public void Initialize(OraclePlayer player,Material retainedLitTemplate,bool interactionColliders=true)
        {
            if(retainedLitTemplate==null||retainedLitTemplate.shader==null||retainedLitTemplate.shader.name!="Universal Render Pipeline/Lit")
                throw new ArgumentException("Borrow an existing retained URP Lit material; shared template is never changed or destroyed.");
            Retire();viewer=player;Material material=CreateSharedPalette(retainedLitTemplate,owned);
            actors=new[]{BuildActor(RefugeNpcRole.Runekeeper,transform,material,owned,interactionColliders),BuildActor(RefugeNpcRole.Gatekeeper,transform,material,owned,interactionColliders)};
            actors[0].Root.transform.SetPositionAndRotation(RunekeeperPosition,Quaternion.Euler(0,90,0));
            actors[1].Root.transform.SetPositionAndRotation(GatekeeperPosition,Quaternion.Euler(0,270,0));
            // Caller explicitly enables only the real refuge/intermission; no automatic campaign gate.
            SetPresentationVisible(false);
        }
        public void SetPresentationVisible(bool value)
        {
            visible=value;foreach(var actor in actors)if(actor!=null&&actor.Root!=null){actor.Root.SetActive(value);if(!value){actor.Rms=0;actor.Talking=false;}}
        }
        public bool TryGetNpcTarget(int role,out Vector3 feet,out Vector3 aim)
        {
            Actor actor=Find(role);if(actor!=null&&visible&&actor.Root!=null&&actor.Root.activeInHierarchy){feet=actor.Root.transform.position;aim=actor.Interaction.position;return true;}
            if(role==(int)RefugeNpcRole.Smith&&ResolveExistingNpc!=null)return ResolveExistingNpc(role,out feet,out aim);
            feet=aim=Vector3.zero;return false;
        }
        public bool TryGetNpcRoot(int role,out Transform root)
        {Actor actor=Find(role);root=actor!=null&&visible&&actor.Root!=null&&actor.Root.activeInHierarchy?actor.Root.transform:null;return root!=null;}
        public bool TryGetLookTarget(out int role)
        {
            role=0;if(!visible||viewer==null||viewer.ViewCamera==null)return false;Transform eye=viewer.ViewCamera.transform;
            if(!Physics.Raycast(eye.position,eye.forward,out RaycastHit hit,InteractionReach,~(1<<2),QueryTriggerInteraction.Ignore))return false;
            foreach(var actor in actors)if(actor!=null&&actor.Collider!=null&&hit.collider==actor.Collider){role=actor.Role;return true;}
            return false;
        }
        public Transform GetVoiceAnchor(int role)=>Find(role)?.Voice;
        public void BindVoiceSource(int role,AudioSource source){Actor actor=Find(role);if(actor!=null){actor.BorrowedVoice=source;actor.SampleAt=0;}}
        public void SetVoiceRms(int role,float rms)
        {Actor actor=Find(role);if(actor!=null){actor.Rms=Finite(rms)?Mathf.Clamp01(rms/.08f):0;actor.ManualUntil=Time.unscaledTime+.15f;}}
        public void SetSpeaking(int role,bool speaking){foreach(var actor in actors)if(actor!=null){if(speaking)actor.Talking=actor.Role==role;else if(actor.Role==role){actor.Talking=false;actor.ManualUntil=0;}}}
        Actor Find(int role){foreach(var actor in actors)if(actor!=null&&actor.Role==role)return actor;return null;}
        void LateUpdate()
        {
            if(!visible||!Application.isFocused)return;float dt=Mathf.Min(Time.unscaledDeltaTime,.05f);if(!Finite(dt)||dt<=0)return;
            foreach(var actor in actors)if(actor!=null&&actor.Root!=null&&actor.Root.activeInHierarchy)Tick(actor,dt);
        }
        void Tick(Actor actor,float dt)
        {
            actor.Age+=dt;float breath=Mathf.Sin(actor.Age*1.55f+actor.Role*.71f);
            actor.Torso.localPosition=actor.TorsoRest+Vector3.up*(breath*.0025f);actor.Torso.localScale=new Vector3(1,1+breath*.0035f,1+breath*.0015f);
            actor.LeftArm.localRotation=Quaternion.Euler(Mathf.Sin(actor.Age*.7f)*.3f,0,Mathf.Sin(actor.Age*.8f)*.35f);
            actor.RightArm.localRotation=Quaternion.Euler(Mathf.Sin(actor.Age*.63f+.8f)*.3f,0,Mathf.Sin(actor.Age*.75f)*.35f);
            float pitch=0,yaw=0;if(viewer!=null){Vector3 eye=viewer.ViewCamera!=null?viewer.ViewCamera.transform.position:viewer.transform.position+Vector3.up*1.65f;
                Vector3 toward=actor.Root.transform.InverseTransformPoint(eye)-new Vector3(0,1.68f,0);
                if(toward.z>.1f&&toward.sqrMagnitude<64){yaw=Mathf.Clamp(Mathf.Atan2(toward.x,toward.z)*Mathf.Rad2Deg,-20,20);pitch=Mathf.Clamp(-Mathf.Atan2(toward.y,new Vector2(toward.x,toward.z).magnitude)*Mathf.Rad2Deg,-8,8);}}
            float blend=1-Mathf.Exp(-dt*3);actor.Pitch=Mathf.Lerp(actor.Pitch,pitch,blend);actor.Yaw=Mathf.Lerp(actor.Yaw,yaw,blend);
            actor.Head.localRotation=Quaternion.Euler(actor.Pitch+Mathf.Sin(actor.Age*.55f)*.25f,actor.Yaw,0);
            float blink=0;if(actor.BlinkAge<0&&actor.Age>=actor.NextBlink)actor.BlinkAge=0;
            if(actor.BlinkAge>=0){actor.BlinkAge+=dt;blink=Mathf.Sin(Mathf.Clamp01(actor.BlinkAge/.16f)*Mathf.PI);if(actor.BlinkAge>=.16f){actor.BlinkAge=-1;actor.NextBlink=actor.Age+3.2f+Mathf.Sin(actor.Age)*.65f;}}
            actor.Lids.localScale=new Vector3(1,Mathf.Max(.001f,blink),1);
            if(Time.unscaledTime>=actor.SampleAt){actor.SampleAt=Time.unscaledTime+.05f;float rms=0;
                if(actor.Talking&&actor.BorrowedVoice!=null&&actor.BorrowedVoice.isActiveAndEnabled&&actor.BorrowedVoice.isPlaying&&!actor.BorrowedVoice.mute){
                    actor.BorrowedVoice.GetOutputData(audioScratch,0);double sum=0;for(int i=0;i<audioScratch.Length;i++)sum+=audioScratch[i]*audioScratch[i];rms=Mathf.Clamp01((float)Math.Sqrt(sum/audioScratch.Length)/.08f);}
                else if(Time.unscaledTime<actor.ManualUntil)rms=actor.Rms;
                actor.Rms=rms;
            }
            // RMS jaw approximation only; speaking without audio/RMS does not invent lip motion.
            float opening=actor.Talking?actor.Rms:0;Vector3 scale=actor.Mouth.localScale;scale.y=Mathf.Lerp(scale.y,1+opening*2.6f,1-Mathf.Exp(-dt*14));actor.Mouth.localScale=scale;
        }
        public static Material CreateSharedPalette(Material source,List<UnityEngine.Object> owned)
        {
            if(source==null||source.shader==null||source.shader.name!="Universal Render Pipeline/Lit"||owned==null)throw new ArgumentException("Retained URP Lit template and explicit ownership list required.");
            Color[] colors={new Color(.075f,.09f,.10f),new Color(.66f,.43f,.29f),new Color(.84f,.81f,.68f),new Color(.12f,.30f,.48f),new Color(.17f,.60f,.35f),new Color(.70f,.37f,.15f),new Color(.16f,.68f,.64f),new Color(.18f,.16f,.15f),new Color(.76f,.55f,.40f),new Color(.85f,.66f,.22f),new Color(.94f,.93f,.86f),new Color(.035f,.045f,.04f),new Color(.08f,.95f,.40f),new Color(.12f,.85f,.78f),Color.black,Color.black};
            var palette=new Texture2D(16,1,TextureFormat.RGB24,false){name="Original shared refuge NPC palette",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};owned.Add(palette);palette.SetPixels(colors);palette.Apply(false,true);
            var emission=new Texture2D(16,1,TextureFormat.RGB24,false,true){name="Refuge staff and lantern emission mask",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};owned.Add(emission);
            var light=new Color[16];for(int i=0;i<16;i++)light[i]=i==12||i==13?colors[i]:Color.black;emission.SetPixels(light);emission.Apply(false,true);
            var material=new Material(source){name="Shared original refuge NPC palette /1material"};owned.Add(material);
            material.SetTexture("_BaseMap",palette);material.SetColor("_BaseColor",Color.white);material.SetFloat("_Metallic",.08f);material.SetFloat("_Smoothness",.23f);
            material.SetTextureScale("_BaseMap",Vector2.one);material.SetTextureOffset("_BaseMap",Vector2.zero);
            material.SetTexture("_EmissionMap",emission);material.SetColor("_EmissionColor",Color.white*1.35f);material.EnableKeyword("_EMISSION");
            material.SetFloat("_Surface",0);material.SetFloat("_AlphaClip",0);material.SetFloat("_ZWrite",1);material.SetFloat("_SrcBlend",1);material.SetFloat("_DstBlend",0);material.SetFloat("_Cull",2);
            foreach(string keyword in new[]{"_ALPHATEST_ON","_SURFACE_TYPE_TRANSPARENT","_ALPHAPREMULTIPLY_ON","_ALPHAMODULATE_ON","_NORMALMAP","_PARALLAXMAP","_DETAIL_MULX2","_DETAIL_SCALED","_METALLICSPECGLOSSMAP"})material.DisableKeyword(keyword);
            material.SetOverrideTag("RenderType","Opaque");material.SetShaderPassEnabled("ShadowCaster",true);material.renderQueue=2000;return material;
        }
        public static GameObject CreateNeutralModel(RefugeNpcRole role,Transform parent,Material sharedPalette,List<UnityEngine.Object> ownedMeshes)
        {if(parent==null||sharedPalette==null||ownedMeshes==null)throw new ArgumentException("Neutral model parent/material/mesh ownership required.");return BuildActor(role,parent,sharedPalette,ownedMeshes,false,true).Root;}
        static Actor BuildActor(RefugeNpcRole role,Transform parent,Material material,List<UnityEngine.Object> owned,bool collider,bool keepReadable=false)
        {
            NpcRecipe recipe=CampaignNpcRecipes.Build(role);var root=new GameObject(recipe.Name+" the "+recipe.RoleLabel);root.transform.SetParent(parent,false);
            var actor=new Actor {Role=(int)role,Name=recipe.Name,Root=root,Parts=new Transform[recipe.Parts.Count],NextBlink=2.1f+(int)role*.37f};
            for(int i=0;i<recipe.Parts.Count;i++){
                NpcPart part=recipe.Parts[i];var child=new GameObject(part.Name,typeof(MeshFilter),typeof(MeshRenderer));child.transform.SetParent(part.Parent>=0?actor.Parts[part.Parent]:root.transform,false);child.transform.localPosition=Vector(part.Pivot);actor.Parts[i]=child.transform;
                var vertices=new Vector3[part.Positions.Count];var normals=new Vector3[vertices.Length];var uv=new Vector2[vertices.Length];
                for(int v=0;v<vertices.Length;v++){vertices[v]=Vector(part.Positions[v]);normals[v]=Vector(part.Normals[v]);uv[v]=new Vector2((part.Paints[v]+.5f)/16,.5f);}
                var mesh=new Mesh {name=recipe.Name+" / "+part.Name};owned.Add(mesh);mesh.vertices=vertices;mesh.normals=normals;mesh.uv=uv;mesh.SetTriangles(part.Indices,0);mesh.RecalculateBounds();mesh.UploadMeshData(!keepReadable);
                child.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=child.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            }
            actor.Torso=actor.Parts[recipe.Torso];actor.TorsoRest=actor.Torso.localPosition;actor.Head=actor.Parts[recipe.Head];actor.LeftArm=actor.Parts[recipe.LeftArm];actor.RightArm=actor.Parts[recipe.RightArm];actor.Mouth=actor.Parts[recipe.Mouth];actor.Lids=actor.Parts[recipe.Eyelids];actor.Lids.localScale=new Vector3(1,.001f,1);
            actor.Gaze=Anchor(root.transform,"Anchor_Gaze",new Vector3(0,1.70f,.16f));actor.Voice=Anchor(root.transform,"Anchor_Voice",new Vector3(0,1.56f,.18f));actor.Interaction=Anchor(root.transform,"Anchor_Interaction",new Vector3(0,1.52f,0));
            Anchor(root.transform,"Anchor_Mount",Vector3.zero);
            if(collider){actor.Collider=root.AddComponent<CapsuleCollider>();actor.Collider.height=1.82f;actor.Collider.radius=.29f;actor.Collider.center=new Vector3(0,.91f,0);}
            return actor;
        }
        static Transform Anchor(Transform parent,string name,Vector3 position){var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=position;return t;}
        static Vector3 Vector(NpcVector v)=>new Vector3(v.X,v.Y,v.Z);
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        void Retire()
        {
            foreach(var actor in actors)if(actor!=null&&actor.Root!=null){actor.Root.SetActive(false);Release(actor.Root);}
            foreach(var resource in owned)if(resource!=null)Release(resource);owned.Clear();actors=new Actor[2];visible=false;
        }
        static void Release(UnityEngine.Object resource)
        {
#if UNITY_EDITOR
            if(!Application.isPlaying){DestroyImmediate(resource);return;}
#endif
            Destroy(resource);
        }
        void OnDisable(){foreach(var actor in actors)if(actor!=null){actor.Rms=0;actor.Talking=false;}}
        void OnDestroy()=>Retire();
    }
}
