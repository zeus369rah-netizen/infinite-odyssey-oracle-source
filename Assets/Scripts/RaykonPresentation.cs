using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace OdysseyOracle.VisualDressing
{
    // Explicit cold visual lease, not an NPC/talk/simulation component.
    public sealed partial class RaykonPresentation : IDisposable
    {
        private GameObject visualRoot;
        private RaykonMeshes meshes;
        private MeshRenderer staffRenderer,bodyRenderer;
        private readonly MaterialPropertyBlock bodyBlock=new MaterialPropertyBlock();
        private float speaking;
        private static readonly int SpeakingId=Shader.PropertyToID("_Speaking");
        public float Speaking
        {
            get=>speaking;
            set
            {
                if(float.IsNaN(value)||float.IsInfinity(value))throw new ArgumentException("Finite speaking amount required.");
                float next=Mathf.Clamp01(value);if(next==speaking)return;speaking=next;
                if(bodyRenderer!=null){bodyBlock.SetFloat(SpeakingId,speaking);bodyRenderer.SetPropertyBlock(bodyBlock);}
            }
        }
        private RaykonPurpleWispPresentation smoke;
        public bool SmokeReady=>smoke!=null&&smoke.IsReady;
        private readonly MaterialPropertyBlock staffBlock=new MaterialPropertyBlock();
        private float talkBlend;
        private static readonly int StaffModeId=Shader.PropertyToID("_StaffMode"),TalkId=Shader.PropertyToID("_TalkBlend");
        public float TalkBlend
        {
            get=>talkBlend;
            set
            {
                if(float.IsNaN(value)||float.IsInfinity(value))throw new ArgumentException("Finite talk progress required.");
                talkBlend=Mathf.Clamp01(value);
                if(staffRenderer!=null){staffBlock.SetFloat(TalkId,talkBlend);staffRenderer.SetPropertyBlock(staffBlock);}
            }
        }
        public static RaykonPresentation Create(Transform logicalOwner,Material borrowedShaderMaterial,RaykonPalette palette,float supportWorldY=0f)
        {
            if(logicalOwner==null||borrowedShaderMaterial==null||float.IsNaN(supportWorldY)||float.IsInfinity(supportWorldY))throw new ArgumentException("Valid owner/material/support required.");
            if((logicalOwner.lossyScale-Vector3.one).sqrMagnitude>1e-8f||Vector3.Dot(logicalOwner.up,Vector3.up)<.99999f)throw new ArgumentException("Raykon requires yaw-only unit-scale root.");
            var owner=new RaykonPresentation();
            try
            {
                owner.meshes=RaykonModelBuilder.Build(palette);
                owner.visualRoot=new GameObject("Original Raykon rigid presentation");owner.visualRoot.transform.SetParent(logicalOwner,false);
                owner.bodyRenderer=Attach(owner.visualRoot.transform,"Raykon original body",owner.meshes.Body,borrowedShaderMaterial);
                owner.bodyBlock.SetFloat(SpeakingId,0f);owner.bodyRenderer.SetPropertyBlock(owner.bodyBlock);
                owner.InitializeAttention009(logicalOwner);
                owner.staffRenderer=Attach(owner.visualRoot.transform,"Raykon shader staff",owner.meshes.Staff,borrowedShaderMaterial);
                Attach(owner.visualRoot.transform,"Raykon cauldron",owner.meshes.Cauldron,borrowedShaderMaterial);
                owner.staffBlock.SetFloat(StaffModeId,1f);owner.staffBlock.SetFloat(TalkId,0f);
                owner.staffBlock.SetVector("_StaffPivot",RaykonMeshes.StaffPivot);
                Vector3 tip=owner.meshes.SlamSupportLocal;
                owner.staffBlock.SetVector("_TipAndSupport",new Vector4(tip.x,tip.y,tip.z,supportWorldY));
                owner.staffRenderer.SetPropertyBlock(owner.staffBlock);
                owner.smoke=RaykonPurpleWispPresentation.CreateOptional(owner.visualRoot.transform);
                return owner;
            }
            catch{owner.Dispose();throw;}
        }
        public static void ConfigureStaticBody(BoxCollider borrowedCollider)
        {
            if(borrowedCollider==null)throw new ArgumentNullException(nameof(borrowedCollider));
            Bounds b=RaykonMeshes.BodyColliderBounds;borrowedCollider.center=b.center;borrowedCollider.size=b.size;borrowedCollider.isTrigger=false;
        }
        private static MeshRenderer Attach(Transform parent,string name,Mesh mesh,Material material)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;MeshRenderer renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=true;return renderer;
        }
        public void Dispose()
        {
            DisposeAttention009();
            smoke?.Dispose();smoke=null;
            if(visualRoot!=null){visualRoot.SetActive(false);if(Application.isPlaying)UnityEngine.Object.Destroy(visualRoot);else UnityEngine.Object.DestroyImmediate(visualRoot);visualRoot=null;}
            staffRenderer=null;bodyRenderer=null;meshes?.Dispose();meshes=null;
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space:borrowed logical owner at root-approved support,local+Z/Y-up,unit-scale/yaw-only;slam floor uses supplied world Y.
// Array Formats:Three original mesh owners, cached body/staff MPBs, one owned six-quad smoke lease; TalkBlend/Speaking0..1, no vertex CPU loop. Speaking is an approximate source-state envelope.
// Dependencies/Pointers:Borrows original material/parent/collider; owns visual meshes/children and disposable smoke material/mesh. Root alone owns dialogue/input/health/collision.
