using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // Builder-owned, fixed-capacity presentation object; no MonoBehaviour/Update or input handling.
    // One mesh/renderer, four submeshes sharing one borrowed material, four cold cached property blocks.
    // Rebuild only fixed-capacity index lists when a pad state changes; no lights/colliders/material clones.
    public sealed class TowerPadHighlightBatch : IDisposable
    {
        readonly List<int>[] groups=new List<int>[4];
        readonly TowerPadDisplayState[] previous=new TowerPadDisplayState[TowerPlatformGeometry.PadCount];
        readonly MaterialPropertyBlock[] blocks=new MaterialPropertyBlock[4];
        readonly GameObject root;readonly Mesh mesh;readonly MeshRenderer renderer;readonly int count;
        bool populated;
        public int Count=>count;
        public Renderer Renderer=>renderer;

        public TowerPadHighlightBatch(Transform parent,Vector3[] pads,Material borrowed)
        {
            if(parent==null||pads==null||borrowed==null||pads.Length!=TowerPlatformGeometry.PadCount)throw new ArgumentException("Exactly four pad centres and a borrowed material are required.");
            count=pads.Length;
            root=new GameObject("Tower platform state outlines");root.layer=2;root.transform.SetParent(parent,false);
            mesh=new Mesh{name="Bounded tower platform border outlines"};mesh.MarkDynamic();
            var vertices=new List<Vector3>(count*16);var normals=new List<Vector3>(count*16);var uvs=new List<Vector2>(count*16);
            for(int pad=0;pad<count;pad++)
            {
                Vector3 center=pads[pad]+Vector3.up*TowerPadPolicy.HighlightHeight;
                AddQuad(parent,center,new Vector2(-.83f,-.83f),new Vector2(-.71f,.83f),vertices,normals,uvs);
                AddQuad(parent,center,new Vector2(.71f,-.83f),new Vector2(.83f,.83f),vertices,normals,uvs);
                AddQuad(parent,center,new Vector2(-.71f,-.83f),new Vector2(.71f,-.71f),vertices,normals,uvs);
                AddQuad(parent,center,new Vector2(-.71f,.71f),new Vector2(.71f,.83f),vertices,normals,uvs);
            }
            mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uvs);mesh.subMeshCount=4;
            renderer=root.AddComponent<MeshRenderer>();root.AddComponent<MeshFilter>().sharedMesh=mesh;
            renderer.sharedMaterials=new[]{borrowed,borrowed,borrowed,borrowed};
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            Color[] colors={new Color(.12f,.78f,.92f,.46f),new Color(.64f,1f,.8f,.82f),new Color(.95f,.62f,.16f,.50f),new Color(.58f,.14f,.16f,.27f)};
            for(int state=0;state<4;state++)
            {
                groups[state]=new List<int>(TowerPlatformGeometry.PadCount*24);
                blocks[state]=new MaterialPropertyBlock();blocks[state].SetColor("_BaseColor",colors[state]);blocks[state].SetColor("_Color",colors[state]);
                blocks[state].SetColor("_EmissionColor",new Color(colors[state].r,colors[state].g,colors[state].b)*(state==1?1.25f:state==0?.7f:.35f));
                renderer.SetPropertyBlock(blocks[state],state);
            }
            renderer.enabled=false;
        }
        public void Apply(TowerPadDisplayState[] states,bool visible)
        {
            if(renderer==null)return;renderer.enabled=visible&&count>0;if(!visible)return;
            if(states==null||states.Length<count)throw new ArgumentException("Pad states must cover all authored IDs.");
            bool changed=!populated;for(int i=0;i<count;i++)if(states[i]!=previous[i])changed=true;
            if(!changed)return;
            for(int state=0;state<4;state++)groups[state].Clear();
            for(int pad=0;pad<count;pad++)
            {
                int state=(int)states[pad];if(state<0||state>=4)throw new ArgumentOutOfRangeException(nameof(states));
                previous[pad]=states[pad];List<int> target=groups[state];
                for(int quad=0;quad<4;quad++){int v=pad*16+quad*4;target.Add(v);target.Add(v+1);target.Add(v+2);target.Add(v);target.Add(v+2);target.Add(v+3);}
            }
            for(int state=0;state<4;state++)mesh.SetTriangles(groups[state],state,false);
            mesh.RecalculateBounds();populated=true;
        }
        static void AddQuad(Transform parent,Vector3 p,Vector2 min,Vector2 max,List<Vector3> vertices,List<Vector3> normals,List<Vector2> uvs)
        {
            vertices.Add(parent.InverseTransformPoint(p+new Vector3(min.x,0,min.y)));vertices.Add(parent.InverseTransformPoint(p+new Vector3(min.x,0,max.y)));
            vertices.Add(parent.InverseTransformPoint(p+new Vector3(max.x,0,max.y)));vertices.Add(parent.InverseTransformPoint(p+new Vector3(max.x,0,min.y)));
            Vector3 normal=parent.InverseTransformDirection(Vector3.up);for(int i=0;i<4;i++)normals.Add(normal);
            uvs.Add(Vector2.zero);uvs.Add(Vector2.up);uvs.Add(Vector2.one);uvs.Add(Vector2.right);
        }
        public void Dispose(){if(root!=null){root.SetActive(false);Release(root);}if(mesh!=null)Release(mesh);}
        static void Release(UnityEngine.Object value){if(Application.isPlaying)UnityEngine.Object.Destroy(value);else UnityEngine.Object.DestroyImmediate(value);}
    }
}
