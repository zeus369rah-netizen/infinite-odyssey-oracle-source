using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace OdysseyOracle
{
    // Cold shared first-party recipe, independently owned from saved tower art.
    internal static class WorkshopBuildResources015
    {
        static Mesh mesh;static Material material;static int leases;
        internal static void Acquire(out Mesh sharedMesh,out Material sharedMaterial)
        {
            if(leases==0)
            {
                var saved=Resources.Load<Material>("Materials/WorkshopBuild015");
                if(saved==null||saved.shader==null||!saved.shader.isSupported||saved.shader.name!="OdysseyOracle/WorkshopBuild015")throw new InvalidOperationException("Original saved WorkshopBuild015 material/shader is required.");
                Mesh owned=null;try{owned=Build();mesh=owned;material=saved;}catch{if(owned!=null)Drop(owned);throw;}
            }
            leases++;sharedMesh=mesh;sharedMaterial=material;
        }
        internal static void Release(){if(leases<=0)return;if(--leases==0){if(mesh!=null)Drop(mesh);mesh=null;material=null;}}
        internal static void Drop(UnityEngine.Object value){if(value==null)return;if(Application.isPlaying)UnityEngine.Object.Destroy(value);else UnityEngine.Object.DestroyImmediate(value);}
        static Mesh Build()
        {
            var v=new List<Vector3>(2400);var n=new List<Vector3>(2400);var c=new List<Color>(2400);var uv=new List<Vector4>(2400);var t=new List<int>(3600);
            for(int i=0;i<48;i++)Box(v,n,c,uv,t,Vector3.zero,new Vector3(.065f,.065f,.065f),new Color(.60f,.49f,.35f),0,i);
            for(int i=0;i<24;i++)Box(v,n,c,uv,t,Vector3.zero,new Vector3(.032f,.045f,.032f),new Color(.95f,.69f,.31f),1,i);
            Box(v,n,c,uv,t,new Vector3(0,.11f,0),new Vector3(.24f,.13f,.13f),new Color(.43f,.48f,.53f),2,0);
            Box(v,n,c,uv,t,new Vector3(0,-.07f,0),new Vector3(.045f,.29f,.045f),new Color(.37f,.20f,.08f),2,0);
            Box(v,n,c,uv,t,Vector3.zero,new Vector3(.31f,.11f,.028f),new Color(.67f,.72f,.75f),3,0);
            for(int i=0;i<6;i++)Box(v,n,c,uv,t,new Vector3(-.13f+i*.052f,-.067f,0),new Vector3(.026f,.026f,.032f),new Color(.52f,.57f,.62f),3,0);
            Box(v,n,c,uv,t,new Vector3(.20f,.015f,0),new Vector3(.085f,.16f,.045f),new Color(.46f,.23f,.08f),3,0);
            Box(v,n,c,uv,t,Vector3.zero,new Vector3(.92f,.075f,.015f),new Color(.09f,.10f,.12f),4,0);
            Box(v,n,c,uv,t,Vector3.zero,new Vector3(.86f,.045f,.02f),new Color(.95f,.74f,.32f),5,0);
            var result=new Mesh{name="Original workshop dust spiral hammer saw progress015",indexFormat=IndexFormat.UInt16};
            try{result.SetVertices(v);result.SetNormals(n);result.SetColors(c);result.SetUVs(1,uv);result.SetTriangles(t,0);result.bounds=new Bounds(new Vector3(0,4,0),new Vector3(8,12,8));result.UploadMeshData(true);return result;}catch{Drop(result);throw;}
        }
        static void Box(List<Vector3> v,List<Vector3> n,List<Color> colors,List<Vector4> uv,List<int> triangles,Vector3 center,Vector3 size,Color color,int kind,int index)
        {
            for(int face=0;face<6;face++)
            {
                int axis=face/2;float sign=(face&1)==0?1:-1;Vector3 normal=axis==0?Vector3.right*sign:axis==1?Vector3.up*sign:Vector3.forward*sign;
                Vector3 u=axis==0?Vector3.forward:Vector3.right;Vector3 w=Vector3.Cross(normal,u);int start=v.Count;
                for(int corner=0;corner<4;corner++){float x=corner==0||corner==3?-1:1,y=corner<2?-1:1;Vector3 p=normal+u*x+w*y;v.Add(center+Vector3.Scale(p,size)*.5f);n.Add(normal);colors.Add(color);uv.Add(new Vector4(kind,index,0,0));}
                triangles.Add(start);triangles.Add(start+1);triangles.Add(start+2);triangles.Add(start);triangles.Add(start+2);triangles.Add(start+3);
            }
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Original tool-local voxel boxes, shader-authored bounded motion in tower floor-local space; no colliders or copied models.
// Array Formats: One cold shared84-box mesh/2016vertices/1008triangles, saved material borrowed by refcounted gameplay/atlas leases; no runtime material clones.
// Dependencies/Pointers: Cold Acquire only; last lease destroys only owned mesh, never saved material/tower catalog. Root must create the exact saved material via official native API and verify shader/browser support.
