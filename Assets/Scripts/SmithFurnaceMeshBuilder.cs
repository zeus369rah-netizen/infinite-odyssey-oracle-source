using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace OdysseyOracle
{
    public static class SmithFurnaceMeshBuilder
    {
        public static Mesh Build(out int quadCount)
        {
            var job=new SmithFurnaceJob();JobHandle fence=default;quadCount=0;
            try
            {
                job.Materials=new NativeArray<byte>(SmithFurnaceMath.Cells,Allocator.TempJob);
                job.Occupancy=new NativeArray<ulong>(SmithFurnaceMath.Words,Allocator.TempJob);
                job.Summaries=new NativeArray<ulong>(SmithFurnaceMath.ChunkCapacity,Allocator.TempJob);
                job.FaceMask=new NativeArray<int>(SmithFurnaceMath.MaskCells,Allocator.TempJob);
                job.Quads=new NativeArray<SmithFurnaceQuad32>(SmithFurnaceMath.MaximumQuads,Allocator.TempJob);
                job.Result=new NativeArray<int>(1,Allocator.TempJob);
                fence=job.Schedule();fence.Complete();quadCount=job.Result[0];
                if(quadCount<=0||quadCount>SmithFurnaceMath.MaximumQuads)throw new InvalidOperationException("Original Smith furnace exceeded its cold greedy capacity.");
                var vertices=new Vector3[quadCount*4];var normals=new Vector3[quadCount*4];var uv=new Vector2[quadCount*4];
                var slots=new List<int>[5];for(int m=0;m<slots.Length;m++)slots[m]=new List<int>(quadCount*6/5+12);
                for(int i=0;i<quadCount;i++)
                {
                    SmithFurnaceQuad32 q=job.Quads[i];int at=i*4,u=(q.Axis+1)%3,v=(q.Axis+2)%3;
                    Vector3 a=new Vector3(q.X,q.Y,q.Z)*SmithFurnaceMath.CellMetres-new Vector3(.56f,0,.42f);
                    Vector3 du=Vector3.zero,dv=Vector3.zero,n=Vector3.zero;du[u]=q.Width*SmithFurnaceMath.CellMetres;dv[v]=q.Height*SmithFurnaceMath.CellMetres;n[q.Axis]=q.Sign;
                    vertices[at]=a;vertices[at+1]=a+du;vertices[at+2]=a+du+dv;vertices[at+3]=a+dv;
                    for(int k=0;k<4;k++)normals[at+k]=n;
                    uv[at]=Vector2.zero;uv[at+1]=new Vector2(du[u],0);uv[at+2]=new Vector2(du[u],dv[v]);uv[at+3]=new Vector2(0,dv[v]);
                    var triangles=slots[q.Material-1];triangles.Add(at);triangles.Add(at+(q.Sign>0?1:2));triangles.Add(at+(q.Sign>0?2:1));
                    triangles.Add(at);triangles.Add(at+(q.Sign>0?2:3));triangles.Add(at+(q.Sign>0?3:2));
                }
                var mesh=new Mesh{name="Original small Smith brick furnace greedy20mm"};
                try
                {
                    mesh.vertices=vertices;mesh.normals=normals;mesh.uv=uv;mesh.subMeshCount=5;
                    for(int m=0;m<5;m++)mesh.SetTriangles(slots[m],m);
                    mesh.bounds=new Bounds(new Vector3(0,.78f,0),new Vector3(1.12f,1.56f,.84f));mesh.UploadMeshData(true);return mesh;
                }
                catch{if(Application.isPlaying)UnityEngine.Object.Destroy(mesh);else UnityEngine.Object.DestroyImmediate(mesh);throw;}
            }
            finally
            {
                try{fence.Complete();}
                finally
                {
                    if(job.Result.IsCreated)job.Result.Dispose();if(job.Quads.IsCreated)job.Quads.Dispose();
                    if(job.FaceMask.IsCreated)job.FaceMask.Dispose();if(job.Summaries.IsCreated)job.Summaries.Dispose();
                    if(job.Occupancy.IsCreated)job.Occupancy.Dispose();if(job.Materials.IsCreated)job.Materials.Dispose();
                }
            }
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Completed local20mm quads convert to bottom-centred metre mesh; conservative1.12x1.56x.84 bounds.
// Array Formats: Fixed TempJob cold buffers -> at most8192 vertices/12288indices and5 existing material slots; immutable UInt16 mesh, no per-frame buffers.
// Dependencies/Pointers: All Object mesh publication follows completed numeric job; finally completes/disposes every allocation, partial failures included. Caller owns returned mesh; no borrowed material destruction.
