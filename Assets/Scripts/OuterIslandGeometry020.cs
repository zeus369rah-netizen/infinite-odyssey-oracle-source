using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace OdysseyOracle
{
    public static class OuterIslandGeometry020
    {
        public static bool TryLayout(Bounds source,float ground,out OuterIslandLayout020 layout,out Bounds bounds)
        {
            bounds=default;
            if(!OuterIslandMath020.TryLayout(F(source.center),F(source.extents),ground,out layout))return false;
            bounds=new Bounds(V(layout.BoundsCenter),V(layout.BoundsExtents*2f));return true;
        }
        public static Mesh Build(OuterIslandLayout020 layout,Bounds bounds)
        {
            using(var vertices=new NativeArray<OuterIslandVertex020>(OuterIslandMath020.VertexCount,Allocator.TempJob))
            using(var indices=new NativeArray<int>(OuterIslandMath020.IndexCount,Allocator.TempJob))
            using(var written=new NativeArray<int>(1,Allocator.TempJob))
            {
                new OuterIslandBuildJob020{Layout=layout,Vertices=vertices,Indices=indices,Written=written}.Schedule().Complete();
                if(written[0]!=vertices.Length)throw new InvalidOperationException("Separated outer-island mesh output count failed.");
                var positions=new Vector3[vertices.Length];var normals=new Vector3[vertices.Length];var colors=new Color32[vertices.Length];
                for(int i=0;i<vertices.Length;i++)
                {
                    var value=vertices[i];var position=V(value.Position);
                    if(!math.all(math.isfinite(value.Position))||!math.all(math.isfinite(value.Normal))||!bounds.Contains(position))
                        throw new InvalidOperationException("Separated outer-island vertex escaped its admitted bounds.");
                    positions[i]=position;normals[i]=V(value.Normal);
                    colors[i]=new Color32((byte)(value.Color>>24),(byte)(value.Color>>16),(byte)(value.Color>>8),(byte)value.Color);
                }
                var mesh=new Mesh{name="Original six separated outer islands020"};
                try{mesh.vertices=positions;mesh.normals=normals;mesh.colors32=colors;mesh.triangles=indices.ToArray();mesh.bounds=bounds;mesh.UploadMeshData(true);return mesh;}
                catch{UnityEngine.Object.Destroy(mesh);throw;}
            }
        }
        static float3 F(Vector3 value)=>new float3(value.x,value.y,value.z);
        static Vector3 V(float3 value)=>new Vector3(value.x,value.y,value.z);
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: shared original environment-local layout; caller supplies the menu or gameplay parent/layer without altering mesh coordinates.
// Array Formats:1620position/normal/color vertices and2340indices in one immutable mesh; fixed54724B temporary numeric scratch plus cold managed upload arrays.
// Dependencies/Pointers: completed cold numeric job precedes validated main-thread Object upload. Returned Mesh belongs exclusively to its calling lease/World; no hot calls, shared mutable mesh, provider asset or gameplay authority.
