using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace OdysseyOracle
{
    /// <summary>
    /// Serial Burst-compatible greedy face meshing. Run synchronously on the calling thread;
    /// this baseline makes no assumption about browser worker threads.
    /// </summary>
    [BurstCompile]
    public struct VoxelGreedyMesher : IJob
    {
        [ReadOnly] public NativeArray<byte> Cells;
        public Vector3Int Size;
        public float VoxelSize;
        public NativeArray<int> Mask;
        public NativeList<Vector3> Vertices;
        public NativeList<Vector3> Normals;
        public NativeList<Vector2> UVs;
        public NativeList<int> Indices;
        public NativeList<byte> QuadGroups;

        public static VoxelMeshBuffers Build(VoxelChunkData chunk)
        {
            if (chunk == null) throw new ArgumentNullException(nameof(chunk));
            if (!chunk.IsCreated) throw new ObjectDisposedException(nameof(chunk));
            VoxelMeshBuffers output = new VoxelMeshBuffers();
            int maximum = Mathf.Max(chunk.Size.x, Mathf.Max(chunk.Size.y, chunk.Size.z));
            NativeArray<int> scratch = default(NativeArray<int>);
            try
            {
                scratch = new NativeArray<int>(maximum * maximum, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                VoxelGreedyMesher job = new VoxelGreedyMesher
                {
                    Cells = chunk.Cells, Size = chunk.Size, VoxelSize = chunk.VoxelSize, Mask = scratch,
                    Vertices = output.Vertices, Normals = output.Normals, UVs = output.UVs,
                    Indices = output.Indices, QuadGroups = output.QuadGroups
                };
                job.Run();
                return output;
            }
            catch
            {
                output.Dispose();
                throw;
            }
            finally
            {
                if (scratch.IsCreated) scratch.Dispose();
            }
        }

        public void Execute()
        {
            Vertices.Clear(); Normals.Clear(); UVs.Clear(); Indices.Clear(); QuadGroups.Clear();
            for (int axis = 0; axis < 3; axis++)
            {
                int u = (axis + 1) % 3, v = (axis + 2) % 3;
                int width = Dimension(u), height = Dimension(v), depth = Dimension(axis);
                for (int slice = -1; slice < depth; slice++)
                {
                    int n = 0;
                    for (int j = 0; j < height; j++)
                    for (int i = 0; i < width; i++)
                    {
                        Vector3Int a = Coordinates(axis, slice, u, i, v, j);
                        Vector3Int b = a;
                        if (axis == 0) b.x++; else if (axis == 1) b.y++; else b.z++;
                        int back = Read(a), front = Read(b);
                        // Touching solids have no exposed interface, even when their groups differ.
                        Mask[n++] = back != 0 && front == 0 ? back : (back == 0 && front != 0 ? -front : 0);
                    }
                    for (int j = 0; j < height; j++)
                    for (int i = 0; i < width;)
                    {
                        int signedGroup = Mask[i + j * width];
                        if (signedGroup == 0) { i++; continue; }
                        int w = 1;
                        while (i + w < width && Mask[i + w + j * width] == signedGroup) w++;
                        int h = 1;
                        bool fullRow = true;
                        while (j + h < height && fullRow)
                        {
                            for (int x = 0; x < w; x++)
                                if (Mask[i + x + (j + h) * width] != signedGroup) { fullRow = false; break; }
                            if (fullRow) h++;
                        }
                        Vector3Int origin = Coordinates(axis, slice + 1, u, i, v, j);
                        EmitQuad(origin, axis, u, v, w, h, signedGroup);
                        for (int yy = 0; yy < h; yy++)
                        for (int xx = 0; xx < w; xx++)
                            Mask[i + xx + (j + yy) * width] = 0;
                        i += w;
                    }
                }
            }
        }

        private int Dimension(int axis) { return axis == 0 ? Size.x : (axis == 1 ? Size.y : Size.z); }

        private int Read(Vector3Int p)
        {
            if (p.x < 0 || p.y < 0 || p.z < 0 || p.x >= Size.x || p.y >= Size.y || p.z >= Size.z) return 0;
            return Cells[p.x + Size.x * (p.y + Size.y * p.z)];
        }

        private static Vector3Int Coordinates(int axis, int slice, int u, int i, int v, int j)
        {
            Vector3Int p = new Vector3Int();
            if (axis == 0) p.x = slice; else if (axis == 1) p.y = slice; else p.z = slice;
            if (u == 0) p.x = i; else if (u == 1) p.y = i; else p.z = i;
            if (v == 0) p.x = j; else if (v == 1) p.y = j; else p.z = j;
            return p;
        }

        private void EmitQuad(Vector3Int origin, int axis, int u, int v, int width, int height, int signedGroup)
        {
            Vector3 p = new Vector3(origin.x, origin.y, origin.z) * VoxelSize;
            Vector3 du = Axis(u) * (width * VoxelSize), dv = Axis(v) * (height * VoxelSize);
            Vector3 normal = Axis(axis) * (signedGroup > 0 ? 1f : -1f);
            int start = Vertices.Length;
            Vertices.Add(p); Vertices.Add(p + du); Vertices.Add(p + du + dv); Vertices.Add(p + dv);
            Normals.Add(normal); Normals.Add(normal); Normals.Add(normal); Normals.Add(normal);
            // Repeat once per voxel, preserving texel scale across merged rectangles.
            UVs.Add(Vector2.zero); UVs.Add(new Vector2(width, 0));
            UVs.Add(new Vector2(width, height)); UVs.Add(new Vector2(0, height));
            if (signedGroup > 0)
            {
                Indices.Add(start); Indices.Add(start + 1); Indices.Add(start + 2);
                Indices.Add(start); Indices.Add(start + 2); Indices.Add(start + 3);
            }
            else
            {
                Indices.Add(start); Indices.Add(start + 2); Indices.Add(start + 1);
                Indices.Add(start); Indices.Add(start + 3); Indices.Add(start + 2);
            }
            QuadGroups.Add((byte)(signedGroup > 0 ? signedGroup : -signedGroup));
        }

        private static Vector3 Axis(int axis)
        {
            return axis == 0 ? new Vector3(1, 0, 0) : (axis == 1 ? new Vector3(0, 1, 0) : new Vector3(0, 0, 1));
        }
    }

    /// <summary>Temporary native output; caller must dispose after copying it into a Unity Mesh.</summary>
    public sealed class VoxelMeshBuffers : IDisposable
    {
        public NativeList<Vector3> Vertices;
        public NativeList<Vector3> Normals;
        public NativeList<Vector2> UVs;
        public NativeList<int> Indices;
        // Groups constrain merging. The bounded wall consumer deliberately renders one supplied stone material.
        public NativeList<byte> QuadGroups;
        public int QuadCount => QuadGroups.IsCreated ? QuadGroups.Length : 0;

        public VoxelMeshBuffers()
        {
            try
            {
                Vertices = new NativeList<Vector3>(128, Allocator.TempJob);
                Normals = new NativeList<Vector3>(128, Allocator.TempJob);
                UVs = new NativeList<Vector2>(128, Allocator.TempJob);
                Indices = new NativeList<int>(192, Allocator.TempJob);
                QuadGroups = new NativeList<byte>(32, Allocator.TempJob);
            }
            catch { Dispose(); throw; }
        }

        public void Dispose()
        {
            if (Vertices.IsCreated) Vertices.Dispose();
            if (Normals.IsCreated) Normals.Dispose();
            if (UVs.IsCreated) UVs.Dispose();
            if (Indices.IsCreated) Indices.Dispose();
            if (QuadGroups.IsCreated) QuadGroups.Dispose();
            Vertices = default(NativeList<Vector3>); Normals = default(NativeList<Vector3>);
            UVs = default(NativeList<Vector2>); Indices = default(NativeList<int>); QuadGroups = default(NativeList<byte>);
        }
    }
}
