using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // Cold, main-thread tutorial construction only. Clip existing triangle surfaces into4m XZ cells;
    // interpolate the original UVs/normals, preserve face winding, and share borrowed materials.
    // Render cells own no colliders, Updates, lights or gameplay. DungeonWorld owns resulting meshes.
    // No per-block GameObjects, duplicated solid walls or camera-distance enable/disable policy.
    public static class TutorialRenderPartition
    {
        public const float CellSize = 4f;
        public readonly struct Vertex
        {
            public readonly Vector3 Position,Normal;
            public readonly Vector2 UV;
            public Vertex(Vector3 position,Vector3 normal,Vector2 uv){Position=position;Normal=normal;UV=uv;}
            public static Vertex Lerp(Vertex a,Vertex b,float t) => new Vertex(
                Vector3.LerpUnclamped(a.Position,b.Position,t),Vector3.LerpUnclamped(a.Normal,b.Normal,t),Vector2.LerpUnclamped(a.UV,b.UV,t));
        }
        public sealed class Part
        {
            public readonly int X,Z;
            public readonly List<Vector3> Positions=new List<Vector3>(),Normals=new List<Vector3>();
            public readonly List<Vector2> UVs=new List<Vector2>();
            public readonly List<int> Indices=new List<int>();
            public Vector3 Origin => new Vector3(X*CellSize,0,Z*CellSize);
            internal Part(int x,int z){X=x;Z=z;}
            internal void Triangle(Vertex a,Vertex b,Vertex c)
            {
                if(Vector3.Cross(b.Position-a.Position,c.Position-a.Position).sqrMagnitude<1e-16f)return;
                Vector3 origin=Origin,localA=a.Position-origin,localB=b.Position-origin,localC=c.Position-origin;
                // Native regression: distinct world sliver vertices may both round to local(4,0,4).
                // Validate the actual float positions uploaded to the mesh, at the SAME area threshold.
                if(Vector3.Cross(localB-localA,localC-localA).sqrMagnitude<1e-16f)return;
                int start=Positions.Count;
                Positions.Add(localA);Positions.Add(localB);Positions.Add(localC);
                Normals.Add(a.Normal);Normals.Add(b.Normal);Normals.Add(c.Normal);
                UVs.Add(a.UV);UVs.Add(b.UV);UVs.Add(c.UV);Indices.Add(start);Indices.Add(start+1);Indices.Add(start+2);
            }
        }
        public static List<Part> Partition(IReadOnlyList<Vector3> positions,IReadOnlyList<Vector3> normals,IReadOnlyList<Vector2> uvs,IReadOnlyList<int> indices)
        {
            if(positions==null||normals==null||uvs==null||indices==null||positions.Count!=normals.Count||positions.Count!=uvs.Count||indices.Count%3!=0)
                throw new ArgumentException("Complete triangle positions/normals/UVs are required.");
            var cells=new Dictionary<Vector2Int,Part>();var ordered=new List<Part>();
            var a=new Vertex[12];var b=new Vertex[12];
            for(int triangle=0;triangle<indices.Count;triangle+=3)
            {
                int i0=indices[triangle],i1=indices[triangle+1],i2=indices[triangle+2];
                if(i0<0||i1<0||i2<0||i0>=positions.Count||i1>=positions.Count||i2>=positions.Count)throw new ArgumentOutOfRangeException(nameof(indices));
                Vertex v0=new Vertex(positions[i0],normals[i0],uvs[i0]),v1=new Vertex(positions[i1],normals[i1],uvs[i1]),v2=new Vertex(positions[i2],normals[i2],uvs[i2]);
                float minX=Mathf.Min(v0.Position.x,Mathf.Min(v1.Position.x,v2.Position.x)),maxX=Mathf.Max(v0.Position.x,Mathf.Max(v1.Position.x,v2.Position.x));
                float minZ=Mathf.Min(v0.Position.z,Mathf.Min(v1.Position.z,v2.Position.z)),maxZ=Mathf.Max(v0.Position.z,Mathf.Max(v1.Position.z,v2.Position.z));
                int x0=Mathf.FloorToInt(minX/CellSize),x1=Mathf.FloorToInt(maxX/CellSize);
                int z0=Mathf.FloorToInt(minZ/CellSize),z1=Mathf.FloorToInt(maxZ/CellSize);
                if(x1-x0>64||z1-z0>64)throw new InvalidOperationException("Unexpected tutorial triangle exceeds bounded partition dimensions.");
                for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++)
                {
                    // Every candidate begins from the original triangle, not the previous cell's result.
                    a[0]=v0;a[1]=v1;a[2]=v2;int count=3;
                    count=Clip(a,count,b,0,x*CellSize,true);
                    count=Clip(b,count,a,0,(x+1)*CellSize,false);
                    count=Clip(a,count,b,2,z*CellSize,true);
                    count=Clip(b,count,a,2,(z+1)*CellSize,false);
                    if(count<3)continue;
                    // Degenerate edge-only polygons must not create empty render objects.
                    bool solid=false;for(int i=1;i+1<count;i++)if(Vector3.Cross(a[i].Position-a[0].Position,a[i+1].Position-a[0].Position).sqrMagnitude>=1e-16f){solid=true;break;}
                    if(!solid)continue;
                    var key=new Vector2Int(x,z);
                    if(!cells.TryGetValue(key,out Part part)){part=new Part(x,z);cells.Add(key,part);ordered.Add(part);}
                    for(int i=1;i+1<count;i++)part.Triangle(a[0],a[i],a[i+1]);
                }
            }
            // A world-space sliver can be the only candidate in a cell yet collapse after localization.
            for(int i=ordered.Count-1;i>=0;i--)if(ordered[i].Indices.Count==0)ordered.RemoveAt(i);
            return ordered;
        }
        static int Clip(Vertex[] input,int count,Vertex[] output,int axis,float boundary,bool keepGreater)
        {
            if(count==0)return 0;int written=0;Vertex previous=input[count-1];
            float before=Coordinate(previous.Position,axis)-boundary;bool wasInside=keepGreater?before>=0:before<=0;
            for(int i=0;i<count;i++)
            {
                Vertex current=input[i];float after=Coordinate(current.Position,axis)-boundary;bool inside=keepGreater?after>=0:after<=0;
                if(wasInside!=inside)
                {
                    float t=before/(before-after);Vertex crossing=Vertex.Lerp(previous,current,t);
                    // Snap the split coordinate to its shared cell plane, preventing float hairline gaps.
                    Vector3 p=crossing.Position;if(axis==0)p.x=boundary;else p.z=boundary;
                    output[written++]=new Vertex(p,crossing.Normal,crossing.UV);
                }
                if(inside)output[written++]=current;
                previous=current;before=after;wasInside=inside;
            }
            return written;
        }
        static float Coordinate(Vector3 p,int axis) => axis==0?p.x:p.z;

        public static int Build(string label,Material material,Transform parent,List<UnityEngine.Object> ownedMeshes,bool castShadows,
            IReadOnlyList<Vector3> positions,IReadOnlyList<Vector3> normals,IReadOnlyList<Vector2> uvs,IReadOnlyList<int> indices)
        {
            if(material==null||parent==null||ownedMeshes==null)throw new ArgumentNullException("Tutorial render-cell ownership is required.");
            List<Part> parts=Partition(positions,normals,uvs,indices);
            foreach(Part part in parts)
            {
                string name="Tutorial light cell "+part.X+","+part.Z+" "+label;
                var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};ownedMeshes.Add(mesh);
                mesh.SetVertices(part.Positions);mesh.SetNormals(part.Normals);mesh.SetUVs(0,part.UVs);mesh.SetTriangles(part.Indices,0);mesh.RecalculateBounds();mesh.UploadMeshData(true);
                var child=new GameObject(name+" • combined voxel mesh");child.transform.SetParent(parent,false);child.transform.localPosition=part.Origin;
                child.AddComponent<MeshFilter>().sharedMesh=mesh;MeshRenderer renderer=child.AddComponent<MeshRenderer>();
                renderer.sharedMaterial=material;renderer.shadowCastingMode=castShadows?ShadowCastingMode.On:ShadowCastingMode.Off;
                renderer.receiveShadows=true;renderer.lightProbeUsage=LightProbeUsage.BlendProbes;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            }
            return parts.Count;
        }
    }
}
