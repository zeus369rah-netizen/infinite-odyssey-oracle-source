using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace OdysseyOracle
{
    // Engine publication boundary. Meshes are shared and built once per active-avatar lifetime.
    // Appearance changes reuse exactly31 renderers, four material-property blocks and rigid pivots.
    public sealed class OracleAvatarModel:IDisposable
    {
        static readonly Color[] Cloth={new Color(.035f,.29f,.31f),new Color(.47f,.055f,.10f),new Color(.065f,.17f,.50f),new Color(.29f,.085f,.43f),
            new Color(.73f,.70f,.58f),new Color(.65f,.31f,.04f),new Color(.11f,.37f,.13f),new Color(.055f,.075f,.13f)};
        static readonly Color[] Hair={new Color(.70f,.76f,.78f),new Color(.025f,.026f,.03f),new Color(.19f,.09f,.045f),new Color(.58f,.19f,.055f),
            new Color(.75f,.53f,.15f),new Color(.32f,.13f,.38f),new Color(.34f,.36f,.39f),new Color(.12f,.43f,.41f)};
        static readonly Mesh[] Templates=new Mesh[OracleAvatarRecipe.MeshSlots];
        static int users;
        static readonly int BaseColor=Shader.PropertyToID("_BaseColor"),LegacyColor=Shader.PropertyToID("_Color");
        readonly MeshRenderer[] renderers=new MeshRenderer[OracleAvatarRecipe.MeshSlots];
        readonly MaterialPropertyBlock robe=new MaterialPropertyBlock(),hat=new MaterialPropertyBlock(),hair=new MaterialPropertyBlock(),paper=new MaterialPropertyBlock();
        readonly Transform leftArm,rightArm,leftLeg,rightLeg;
        public Transform Wand {get;}
        bool disposed,hasAppearance;
        uint applied;

        public OracleAvatarModel(Transform visualBody,Material cloth,Material gold,Material dark,Material gem)
        {
            if(visualBody==null||cloth==null||gold==null||dark==null||gem==null)throw new ArgumentNullException("Avatar model inputs");
            AcquireTemplates();
            try
            {
                leftArm=Pivot("Rigid left arm",visualBody,new Vector3(-.35f,1.15f,0));
                rightArm=Pivot("Rigid right arm",visualBody,new Vector3(.35f,1.15f,0));
                Wand=Pivot("Wrapped Oracle wand",rightArm,new Vector3(.09f,-.36f,.22f));
                leftLeg=Pivot("Rigid left leg",visualBody,new Vector3(-.17f,.71f,0));
                rightLeg=Pivot("Rigid right leg",visualBody,new Vector3(.17f,.71f,0));
                // This bounded loop binds published assets; geometry traversal has already completed in pure math.
                for(int slot=0;slot<renderers.Length;slot++)
                {
                    var descriptor=OracleAvatarRecipe.Slot(slot);
                    Transform parent=descriptor.Bone==1?leftArm:descriptor.Bone==2?rightArm:descriptor.Bone==3?Wand:descriptor.Bone==4?leftLeg:descriptor.Bone==5?rightLeg:visualBody;
                    var part=new GameObject("Oracle rigid mesh "+slot);part.transform.SetParent(parent,false);
                    part.AddComponent<MeshFilter>().sharedMesh=Templates[slot];
                    var renderer=part.AddComponent<MeshRenderer>();renderer.sharedMaterial=descriptor.Palette==3?gold:descriptor.Palette==4?dark:descriptor.Palette==5?gem:cloth;
                    renderer.shadowCastingMode=ShadowCastingMode.On;renderers[slot]=renderer;
                }
                Paint(paper,new Color(.69f,.66f,.53f));
                Apply(OracleAvatarAppearance.StartingLook(0));
            }
            catch{ReleaseTemplates();throw;}
        }
        public static Color LookColor(int index)=>Cloth[index>=0&&index<Cloth.Length?index:0];
        public bool Apply(uint packed)=>OracleAvatarAppearance.TryDecode(packed,out var value)&&Apply(value);
        public bool Apply(OracleAvatarAppearance appearance)
        {
            if(disposed||!OracleAvatarAppearance.TryDecode(appearance.Packed,out _))return false;
            if(hasAppearance&&applied==appearance.Packed)return true;
            Paint(robe,Cloth[appearance.RobeColor]);Paint(hat,Cloth[appearance.HatColor]);Paint(hair,Hair[appearance.HairColor]);
            for(int i=0;i<renderers.Length;i++)
            {
                var descriptor=OracleAvatarRecipe.Slot(i);var renderer=renderers[i];
                renderer.enabled=descriptor.Visible(appearance);
                renderer.SetPropertyBlock(descriptor.Palette==0?robe:descriptor.Palette==1?hat:descriptor.Palette==2?hair:descriptor.Palette==6?paper:null);
            }
            applied=appearance.Packed;hasAppearance=true;return true;
        }
        public void Animate(float phase,float motion)
        {
            if(disposed)return;
            float swing=Mathf.Sin(phase)*Mathf.Clamp01(motion)*9f;
            leftArm.localRotation=Quaternion.Euler(swing,0,-3f);rightArm.localRotation=Quaternion.Euler(-swing,0,3f);
        }
        static void Paint(MaterialPropertyBlock block,Color color){block.SetColor(BaseColor,color);block.SetColor(LegacyColor,color);}
        static Transform Pivot(string label,Transform parent,Vector3 position)
        {var node=new GameObject(label).transform;node.SetParent(parent,false);node.localPosition=position;return node;}
        static void AcquireTemplates()
        {
            if(users>0){users++;return;}
            var boxes=new OracleAvatarBox[OracleAvatarRecipe.MaximumBoxes];int count=OracleAvatarRecipe.Fill(boxes);
            try{for(int slot=0;slot<Templates.Length;slot++)Templates[slot]=Publish(OracleAvatarMeshBuilder.Build(boxes,count,slot),slot);users=1;}
            catch{ClearTemplates();throw;}
        }
        static Mesh Publish(OracleAvatarMeshData data,int slot)
        {
            var vertices=new Vector3[data.VertexCount];var normals=new Vector3[data.VertexCount];
            for(int i=0;i<data.VertexCount;i++)
            {var p=data.Positions[i];var n=data.Normals[i];vertices[i]=new Vector3(p.X,p.Y,p.Z);normals[i]=new Vector3(n.X,n.Y,n.Z);}
            var mesh=new Mesh{name="Shared rigid Oracle slot "+slot,indexFormat=IndexFormat.UInt16};
            try{mesh.vertices=vertices;mesh.normals=normals;mesh.triangles=data.Indices;mesh.RecalculateBounds();mesh.UploadMeshData(true);return mesh;}
            catch{if(Application.isPlaying)UnityEngine.Object.Destroy(mesh);else UnityEngine.Object.DestroyImmediate(mesh);throw;}
        }
        static void ReleaseTemplates(){if(users<=0)return;if(--users==0)ClearTemplates();}
        static void ClearTemplates(){for(int i=0;i<Templates.Length;i++){if(Templates[i]!=null){if(Application.isPlaying)UnityEngine.Object.Destroy(Templates[i]);else UnityEngine.Object.DestroyImmediate(Templates[i]);}Templates[i]=null;}}
        public void Dispose(){if(disposed)return;disposed=true;ReleaseTemplates();}
    }
}

// # ARCHITECTURE SNAPSHOT
// Y-up/+Z front, feet origin. Shared bounded31 rigid meshes; no skinning, colliders, gameplay tracking or native allocation.
// Existing RemoteOracleAvatar owns materials/body lifetime; this owner retains/releases shared templates and fixed renderer/property-block arrays.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Original Oracle accepted world roots and camera/child-local poses only; no authority geometry changes.
// Array Formats: Fixed31shared body slots/2prewarmed player graphs and original palette/pose/feedback buffers; no wire ABI growth.
// Dependencies/Pointers: Player/Rooms/Combat remain sole owners; root integrates default-OFF original native player pilot and verifies real bindings/peer/clearance.
