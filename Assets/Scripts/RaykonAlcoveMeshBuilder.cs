using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace OdysseyOracle.VisualDressing
{
    public static class RaykonAlcoveMeshBuilder
    {
        public static readonly Vector3 Minimum=new Vector3(-.7f,0,-.25f),Maximum=new Vector3(.7f,2.8f,.55f);
        public static Mesh Build(int variant)
        {
            NativeArray<RaykonAlcoveBox32> boxes=default;
            NativeArray<RaykonAlcoveAnchor32> anchors=default;
            NativeArray<RaykonAlcoveStrand32> strands=default;
            NativeArray<int> counts=default;JobHandle fence=default;
            try
            {
                boxes=new NativeArray<RaykonAlcoveBox32>(RaykonAlcoveRecipeJob.MaximumBoxes,Allocator.TempJob);
                anchors=new NativeArray<RaykonAlcoveAnchor32>(RaykonAlcoveRecipeJob.MaximumAnchors,Allocator.TempJob);
                strands=new NativeArray<RaykonAlcoveStrand32>(RaykonAlcoveRecipeJob.MaximumStrands,Allocator.TempJob);
                counts=new NativeArray<int>(4,Allocator.TempJob);
                fence=new RaykonAlcoveRecipeJob{Variant=variant,Boxes=boxes,Anchors=anchors,Strands=strands,Counts=counts}.Schedule();fence.Complete();
                if(counts[3]!=0||counts[0]!=101||counts[1]!=31||counts[2]!=34)throw new InvalidOperationException("Raykon alcove cold recipe/capacity changed.");
                ValidateSupports(boxes,anchors,counts[0],counts[1]);
                var meshData=new ColdMesh((counts[0]+counts[2])*24);
                for(int i=0;i<counts[0];i++)
                {
                    RaykonAlcoveBox32 b=boxes[i];Vector3 a=V((float3)b.MinimumMillimetres*.001f),c=V((float3)b.MaximumMillimetres*.001f);
                    if((b.ColorTag&RaykonAlcoveRecipeJob.Pointed)!=0)meshData.Point(a,c,b.ColorTag);else meshData.Box(a,c,b.ColorTag);
                }
                for(int i=0;i<counts[2];i++)meshData.Thread(strands[i]);
                return meshData.Finish();
            }
            finally
            {
                fence.Complete();if(counts.IsCreated)counts.Dispose();if(strands.IsCreated)strands.Dispose();
                if(anchors.IsCreated)anchors.Dispose();if(boxes.IsCreated)boxes.Dispose();
            }
        }
        // Numeric completed-data validation only: no Object access or spatial scene query.
        private static void ValidateSupports(NativeArray<RaykonAlcoveBox32> boxes,NativeArray<RaykonAlcoveAnchor32> anchors,int boxCount,int anchorCount)
        {
            for(int slot=0;slot<24;slot++)
            {
                uint id=(uint)(1000+slot);float3 position=default;bool found=false;
                for(int a=0;a<anchorCount;a++)if(anchors[a].Id==id){position=anchors[a].Position;found=true;break;}
                int bottom=int.MaxValue,top=int.MinValue;bool member=false;
                for(int i=0;i<boxCount;i++)if(boxes[i].SupportAnchor==id)
                {
                    RaykonAlcoveBox32 box=boxes[i];member=true;bottom=math.min(bottom,box.MinimumMillimetres.y);top=math.max(top,box.MaximumMillimetres.y);
                    if(box.MinimumMillimetres.x<math.round(position.x*1000)-90||box.MaximumMillimetres.x>math.round(position.x*1000)+90||
                        box.MinimumMillimetres.z< -140||box.MaximumMillimetres.z>160)throw new InvalidOperationException("Raykon shelf prop leaves its nonoverlapping slot.");
                }
                if(!found||!member||bottom!=(int)math.round(position.y*1000)||top>bottom+400)throw new InvalidOperationException("Raykon shelf prop lost exact named plank support.");
            }
            for(int a=0;a<boxCount;a++)for(int b=a+1;b<boxCount;b++)
                if(math.all(boxes[a].MinimumMillimetres==boxes[b].MinimumMillimetres)&&math.all(boxes[a].MaximumMillimetres==boxes[b].MaximumMillimetres))
                    throw new InvalidOperationException("Duplicate complete Raykon alcove recipe volume.");
        }
        private static Vector3 V(float3 p)=>new Vector3(p.x,p.y,p.z);
        private sealed class ColdMesh
        {
            private readonly List<Vector3> positions,normals;
            private readonly List<Color> colors;
            private readonly List<Vector2> uv;
            private readonly List<int> indices;
            internal ColdMesh(int capacity)
            {
                positions=new List<Vector3>(capacity);normals=new List<Vector3>(capacity);colors=new List<Color>(capacity);
                uv=new List<Vector2>(capacity);indices=new List<int>(capacity*3/2);
            }
            internal void Box(Vector3 a,Vector3 b,uint color)
            {
                Quad(new Vector3(a.x,a.y,a.z),new Vector3(a.x,b.y,a.z),new Vector3(b.x,b.y,a.z),new Vector3(b.x,a.y,a.z),color);
                Quad(new Vector3(b.x,a.y,b.z),new Vector3(b.x,b.y,b.z),new Vector3(a.x,b.y,b.z),new Vector3(a.x,a.y,b.z),color);
                Quad(new Vector3(a.x,a.y,b.z),new Vector3(a.x,b.y,b.z),new Vector3(a.x,b.y,a.z),new Vector3(a.x,a.y,a.z),color);
                Quad(new Vector3(b.x,a.y,a.z),new Vector3(b.x,b.y,a.z),new Vector3(b.x,b.y,b.z),new Vector3(b.x,a.y,b.z),color);
                Quad(new Vector3(a.x,b.y,a.z),new Vector3(a.x,b.y,b.z),new Vector3(b.x,b.y,b.z),new Vector3(b.x,b.y,a.z),color);
                Quad(new Vector3(a.x,a.y,b.z),new Vector3(a.x,a.y,a.z),new Vector3(b.x,a.y,a.z),new Vector3(b.x,a.y,b.z),color);
            }
            internal void Point(Vector3 a,Vector3 b,uint color)
            {
                Vector3 p0=new Vector3(a.x,a.y,a.z),p1=new Vector3(b.x,a.y,a.z),p2=new Vector3(b.x,a.y,b.z),p3=new Vector3(a.x,a.y,b.z),tip=new Vector3((a.x+b.x)*.5f,b.y,(a.z+b.z)*.5f);
                Tri(p0,tip,p1,color);Tri(p1,tip,p2,color);Tri(p2,tip,p3,color);Tri(p3,tip,p0,color);Quad(p0,p1,p2,p3,color);
            }
            internal void Thread(RaykonAlcoveStrand32 line)
            {
                float3 direction=math.normalizesafe(line.B-line.A);
                float3 right=math.normalizesafe(math.cross(direction,new float3(0,0,1)))*line.HalfWidth;
                float3 depth=math.normalizesafe(math.cross(direction,right))*line.HalfWidth;
                Vector3 a=V(line.A-right-depth),b=V(line.A+right-depth),c=V(line.A+right+depth),d=V(line.A-right+depth);
                Vector3 e=V(line.B-right-depth),f=V(line.B+right-depth),g=V(line.B+right+depth),h=V(line.B-right+depth);
                Quad(a,d,c,b,12);Quad(e,f,g,h,12);Quad(a,b,f,e,12);Quad(d,h,g,c,12);Quad(a,e,h,d,12);Quad(b,c,g,f,12);
            }
            private void Tri(Vector3 a,Vector3 b,Vector3 c,uint tag)
            {
                int at=positions.Count;Vector3 n=Vector3.Cross(b-a,c-a).normalized;Color color=Palette(tag);float glow=Glow(tag);
                positions.Add(a);positions.Add(b);positions.Add(c);
                for(int i=0;i<3;i++){normals.Add(n);colors.Add(color);uv.Add(new Vector2(glow,(tag&RaykonAlcoveRecipeJob.Torch)!=0?4:0));}
                indices.Add(at);indices.Add(at+1);indices.Add(at+2);
            }
            private void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,uint tag)
            {
                int at=positions.Count;Vector3 n=Vector3.Cross(b-a,c-a).normalized;Color color=Palette(tag);float glow=Glow(tag);
                positions.Add(a);positions.Add(b);positions.Add(c);positions.Add(d);
                for(int i=0;i<4;i++){normals.Add(n);colors.Add(color);uv.Add(new Vector2(glow,(tag&RaykonAlcoveRecipeJob.Torch)!=0?4:0));}
                indices.Add(at);indices.Add(at+1);indices.Add(at+2);indices.Add(at);indices.Add(at+2);indices.Add(at+3);
            }
            internal Mesh Finish()
            {
                if(positions.Count!=3184||indices.Count!=4734)throw new InvalidOperationException("Raykon original case mesh count changed.");
                var mesh=new Mesh{name="Original Raykon case books skulls crystals potions anchored webs and shaded torch"};
                try
                {
                    mesh.SetVertices(positions);mesh.SetNormals(normals);mesh.SetColors(colors);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();
                    Bounds b=mesh.bounds;if(b.min.x<Minimum.x-.001f||b.min.y<Minimum.y-.001f||b.min.z<Minimum.z-.001f||b.max.x>Maximum.x+.001f||b.max.y>Maximum.y+.001f||b.max.z>Maximum.z+.001f)throw new InvalidOperationException("Raykon case left declared geometry bounds.");
                    return mesh;
                }
                catch{if(Application.isPlaying)UnityEngine.Object.Destroy(mesh);else UnityEngine.Object.DestroyImmediate(mesh);throw;}
            }
            private static float Glow(uint tag)=>(tag&255)==8?.42f:(tag&255)==9?.10f:0;
            private static Color Palette(uint tag)
            {
                switch(tag&255)
                {
                    case 0:return new Color(.19f,.09f,.055f);case 1:return new Color(.115f,.055f,.05f);
                    case 2:return new Color(.13f,.11f,.17f);case 3:return new Color(.19f,.055f,.28f);
                    case 4:return new Color(.45f,.11f,.18f);case 5:return new Color(.15f,.30f,.20f);
                    case 6:return new Color(.12f,.22f,.40f);case 7:return new Color(.38f,.17f,.50f);
                    case 8:return new Color(.63f,.18f,.92f);case 9:return new Color(.60f,.43f,.20f);
                    case 10:return new Color(.76f,.71f,.57f);case 11:return new Color(.035f,.025f,.045f);
                    default:return new Color(.47f,.42f,.52f);
                }
            }
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Case-local +Z front/Y-up; integer-mm pure recipe, exact plank support and nonoverlapping slot footprints, declared local AABB.
// Array Formats: Fixed TempJob384 Box32/32 Anchor32/64 Strand32 and int4 counts, fence-completed before reads; actual101 volumes/31 anchors/34 strands ->3184vertices/4734indices per case, cold lists only.
// Dependencies/Pointers: Caller owns returned mesh; every TempJob allocation disposed in finally after fence. All Mesh/Object binding is after numeric loops; original vertex-color/glow Raykon material borrowed, no runtime Update/physics/light/audio owner.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing refuge geometry/world axes and support pivots unchanged; elemental numeric state applies only to the supported refuge.
// Array Formats: Existing owner arrays remain canonical; fixed512-node/8word reaction state supplies bounded scalar presentation gains.
// Dependencies/Pointers: Host accepted single Fire/Earth/Wind shots only; existing Object adapters retain materials/lights/lifetime, no rewards/saves or shrine/portal gameplay mutation.
