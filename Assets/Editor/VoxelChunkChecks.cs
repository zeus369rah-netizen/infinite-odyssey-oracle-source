using System;
using UnityEditor;
using UnityEngine;

namespace OdysseyOracle.EditorChecks
{
    public static class VoxelChunkChecks
    {
        [MenuItem("Odyssey/Checks/Run bounded voxel checks")]
        private static void RunMenu() { Debug.Log(Run()); }

        /// <summary>Deterministic checks; throws on failure and allocates no scene objects.</summary>
        public static string Run()
        {
            using (VoxelChunkData solid = new VoxelChunkData(new Vector3Int(2, 2, 2), 0.25f))
            {
                solid.Fill(1);
                using (VoxelMeshBuffers mesh = VoxelGreedyMesher.Build(solid))
                {
                    Require(mesh.QuadCount == 6, "Solid 2x2x2 must merge to six quads.");
                    Require(mesh.Vertices.Length == 24 && mesh.Indices.Length == 36, "Solid output buffer lengths.");
                    Near(SurfaceArea(mesh), 1.5f, "Solid surface area.");
                    ValidateMesh(mesh, 0.25f);
                }
                solid.MarkClean();
                Require(!solid.SetCell(-1, 0, 0, 1) && !solid.Dirty, "Negative cell writes must be rejected without dirtying.");
                Require(solid.GetCell(-1, 0, 0) == 0 && solid.GetCell(2, 0, 0) == 0, "Out-of-bounds reads are air.");
                Require(solid.PointToCell(new Vector3(-0.01f, -0.25f, -1f)) == new Vector3Int(-1, -1, -4), "Negative coordinates use floor, not truncation.");
                Vector3 origin = new Vector3(-10f, -2f, -5f);
                Require(solid.PointToCell(new Vector3(-10.01f, -2.01f, -5.51f) - origin) == new Vector3Int(-1, -1, -3), "Negative world origins preserve local cell addressing.");
            }
            using (VoxelChunkData groups = new VoxelChunkData(new Vector3Int(2, 1, 1), 1f))
            {
                groups.SetCell(0, 0, 0, 1); groups.SetCell(1, 0, 0, 2);
                using (VoxelMeshBuffers mesh = VoxelGreedyMesher.Build(groups))
                {
                    Require(mesh.QuadCount == 10, "Adjacent merge groups may not merge coplanar faces.");
                    Near(SurfaceArea(mesh), 10f, "Solid interfaces must remain culled across group boundaries.");
                    int first = 0, second = 0;
                    for (int i = 0; i < mesh.QuadGroups.Length; i++)
                    {
                        if (mesh.QuadGroups[i] == 1) first++;
                        if (mesh.QuadGroups[i] == 2) second++;
                    }
                    Require(first == 5 && second == 5, "Every exposed face retains its merge group.");
                    ValidateMesh(mesh, 1f);
                }
            }
            using (VoxelChunkData hole = new VoxelChunkData(new Vector3Int(3, 3, 3), 1f))
            {
                hole.Fill(1); hole.SetCell(1, 1, 1, 0);
                using (VoxelMeshBuffers mesh = VoxelGreedyMesher.Build(hole))
                {
                    Require(mesh.QuadCount == 12, "Enclosed single-voxel cavity has six inner and six outer quads.");
                    Near(SurfaceArea(mesh), 60f, "Cavity adds six correctly exposed voxel faces.");
                    ValidateMesh(mesh, 1f);
                }
                hole.Fill(0);
                using (VoxelMeshBuffers mesh = VoxelGreedyMesher.Build(hole))
                    Require(mesh.QuadCount == 0 && mesh.Vertices.Length == 0 && mesh.Indices.Length == 0, "Empty chunk has no geometry.");
            }
            VoxelChunkData disposed = new VoxelChunkData(Vector3Int.one, 1f);
            disposed.Dispose(); disposed.Dispose();
            Require(!disposed.IsCreated && disposed.Length == 0 && !disposed.Dirty, "Persistent native cells dispose idempotently.");
            bool threw = false;
            try { disposed.GetCell(0, 0, 0); } catch (ObjectDisposedException) { threw = true; }
            Require(threw, "Disposed storage rejects access.");
            return "Voxel checks passed: six-quad solid, merge-group boundaries, cavity surface, empty, negative coordinates, winding/normals/UVs and disposal.";
        }

        private static void ValidateMesh(VoxelMeshBuffers mesh, float voxelSize)
        {
            Require(mesh.Vertices.Length == mesh.Normals.Length && mesh.Vertices.Length == mesh.UVs.Length, "Vertex stream lengths agree.");
            for (int i = 0; i < mesh.Indices.Length; i += 3)
            {
                int a = mesh.Indices[i], b = mesh.Indices[i + 1], c = mesh.Indices[i + 2];
                Require(a >= 0 && b >= 0 && c >= 0 && a < mesh.Vertices.Length && b < mesh.Vertices.Length && c < mesh.Vertices.Length, "Triangle indices in range.");
                Vector3 cross = Vector3.Cross(mesh.Vertices[b] - mesh.Vertices[a], mesh.Vertices[c] - mesh.Vertices[a]);
                Require(Vector3.Dot(cross, mesh.Normals[a]) > 0f, "Every triangle winds consistently with its outward normal.");
            }
            for (int q = 0; q < mesh.QuadCount; q++)
            {
                int v = q * 4;
                Near(mesh.Normals[v].sqrMagnitude, 1f, "Unit face normals.");
                Near(mesh.UVs[v + 1].x, (mesh.Vertices[v + 1] - mesh.Vertices[v]).magnitude / voxelSize, "Merged U texel scale.");
                Near(mesh.UVs[v + 3].y, (mesh.Vertices[v + 3] - mesh.Vertices[v]).magnitude / voxelSize, "Merged V texel scale.");
            }
        }

        private static float SurfaceArea(VoxelMeshBuffers mesh)
        {
            float area = 0f;
            for (int i = 0; i < mesh.Indices.Length; i += 3)
            {
                Vector3 a = mesh.Vertices[mesh.Indices[i]], b = mesh.Vertices[mesh.Indices[i + 1]], c = mesh.Vertices[mesh.Indices[i + 2]];
                area += Vector3.Cross(b - a, c - a).magnitude * 0.5f;
            }
            return area;
        }

        private static void Near(float actual, float expected, string label)
        {
            Require(Mathf.Abs(actual - expected) < 0.0001f, label + " Expected " + expected + ", received " + actual);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
