using System;
using OdysseyOracle.CampaignNpcs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace OdysseyOracle
{
    // One cold mesh flyweight/view for this session. No part or mesh is created
    // on interaction, word, pose update or re-entry into the same cache lease.
    public sealed class NibwickActorLease020 : IDisposable
    {
        public GameObject Root {get;private set;}
        public readonly Transform[] Parts=new Transform[17];
        public readonly float3[] Pivots=new float3[17];
        readonly Mesh[] meshes=new Mesh[17];
        public NibwickActorLease020(Transform parent,Material borrowedPalette)
        {
            if(parent==null||borrowedPalette==null)throw new ArgumentException("Current session parent and retained borrowed palette required.");
            try{
                var recipe=NibwickRuntimeRecipe020.Build();
                if(recipe.Parts.Count!=17||recipe.Triangles!=1392)throw new InvalidOperationException("Frozen original Nibwick runtime partition drifted.");
                Root=new GameObject("Nibwick020 - owned rigid hub actor");Root.SetActive(false);Root.transform.SetParent(parent,false);
                for(int p=0;p<17;p++)
                {
                    var source=recipe.Parts[p];
                    var item=new GameObject(source.Name,typeof(MeshFilter),typeof(MeshRenderer));
                    item.transform.SetParent(source.Parent<0?Root.transform:Parts[source.Parent],false);
                    var pivot=new Vector3(source.Pivot.X,source.Pivot.Y,source.Pivot.Z);item.transform.localPosition=pivot;
                    Parts[p]=item.transform;Pivots[p]=new float3(pivot.x,pivot.y,pivot.z);
                    var vertices=new Vector3[source.Positions.Count];var normals=new Vector3[vertices.Length];var uv=new Vector2[vertices.Length];
                    for(int v=0;v<vertices.Length;v++)
                    {
                        var a=source.Positions[v];var n=source.Normals[v];vertices[v]=new Vector3(a.X,a.Y,a.Z);normals[v]=new Vector3(n.X,n.Y,n.Z);
                        uv[v]=new Vector2((source.Paints[v]+.5f)/NpcModelBuilder.PaletteWidth,.5f);
                    }
                    var mesh=new Mesh{name="Nibwick020 / "+source.Name};meshes[p]=mesh;
                    mesh.vertices=vertices;mesh.normals=normals;mesh.uv=uv;mesh.SetTriangles(source.Indices,0);mesh.RecalculateBounds();mesh.UploadMeshData(true);
                    item.GetComponent<MeshFilter>().sharedMesh=mesh;
                    var renderer=item.GetComponent<MeshRenderer>();renderer.sharedMaterial=borrowedPalette;
                    renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;renderer.lightProbeUsage=LightProbeUsage.Off;
                }
            }catch{Dispose();throw;}
        }
        public void Apply(in NibwickTourState020 state,NibwickMotionLease020 motion)
        {
            if(Root==null)return;
            Root.transform.SetPositionAndRotation(new Vector3(state.Position.X,state.BodyY,state.Position.Z),Quaternion.Euler(0,state.Yaw,0));
            // Fixed main-thread engine publication only, after worker completion.
            for(int p=0;p<17;p++)
            {
                var pose=motion.Pose(p);var q=pose.Rotation.value;
                Parts[p].localPosition=new Vector3(pose.Position.x,pose.Position.y,pose.Position.z);
                Parts[p].localRotation=new Quaternion(q.x,q.y,q.z,q.w);
                Parts[p].localScale=new Vector3(pose.Scale.x,pose.Scale.y,pose.Scale.z);
            }
        }
        public void SetVisible(bool value){if(Root!=null&&Root.activeSelf!=value)Root.SetActive(value);}
        public void Dispose()
        {
            if(Root!=null){Root.SetActive(false);Release(Root);}Root=null;
            for(int i=0;i<meshes.Length;i++){if(meshes[i]!=null)Release(meshes[i]);meshes[i]=null;Parts[i]=null;}
            // Borrowed resource material/palette/texture is never released here.
        }
        static void Release(UnityEngine.Object item)
        {
#if UNITY_EDITOR
            if(!Application.isPlaying){UnityEngine.Object.DestroyImmediate(item);return;}
#endif
            UnityEngine.Object.Destroy(item);
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Original thirteen-group geometry partitioned into seventeen cached rigid groups, supported Y-up/Z-forward root and positive joint scales; no mirroring.
// Array Formats: Cold fixed17 Transform/Mesh/base-pivot cache plus shared borrowed material; no skin, Animator, per-step/word allocation or additional audio source.
// Dependencies/Pointers: One session cache lease owns hierarchy/meshes; numeric motion lease completes before cached renderer writes/retirement. Material/textures remain borrowed, and root owns native render/ownership acceptance.
