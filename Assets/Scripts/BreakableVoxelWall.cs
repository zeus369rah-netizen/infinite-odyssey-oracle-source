using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace OdysseyOracle
{
    /// <summary>A small destructible panel: one renderer, one collision mesh, no per-cell objects.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public sealed class BreakableVoxelWall : MonoBehaviour
    {
        public const int MaximumPanelCells = 4096;
        public bool IsInitialized => chunk != null && chunk.IsCreated;
        public int LastQuadCount { get; private set; }
        public int RebuildCount { get; private set; }
        private VoxelChunkData chunk;
        private Mesh mesh;
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private MeshCollider meshCollider;
        private int lastRebuildFrame = -1;

        /// <summary>Position is the lower local corner. Existing rotation is preserved.</summary>
        public void Initialize(Vector3 worldPosition, Vector3Int size, float voxelSize, Material material)
        {
            if (material == null) throw new ArgumentNullException(nameof(material));
            if ((long)size.x * size.y * size.z > MaximumPanelCells)
                throw new ArgumentOutOfRangeException(nameof(size), "Breakable panels are bounded to 4096 cells.");
            // Validate and allocate the replacement before releasing a currently working panel.
            VoxelChunkData replacement = new VoxelChunkData(size, voxelSize);
            ReleaseOwnedData();
            chunk = replacement;
            chunk.Fill(1);
            transform.position = worldPosition;
            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
            meshCollider = GetComponent<MeshCollider>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = ShadowCastingMode.On;
            meshRenderer.receiveShadows = true;
            meshCollider.convex = false;
            meshCollider.cookingOptions = MeshColliderCookingOptions.CookForFasterSimulation |
                MeshColliderCookingOptions.EnableMeshCleaning | MeshColliderCookingOptions.WeldColocatedVertices;
            mesh = new Mesh { name = "Bounded greedy voxel panel", indexFormat = IndexFormat.UInt32 };
            mesh.MarkDynamic();
            meshFilter.sharedMesh = mesh;
            RebuildCount = 0;
            Rebuild();
        }

        public void DamageAt(Vector3 worldHit, float radius)
        {
            if (!IsInitialized || float.IsNaN(radius) || float.IsInfinity(radius) || radius <= 0f) return;
            radius = Mathf.Min(radius, 2.5f);
            Vector3 localHit = transform.InverseTransformPoint(worldHit);
            Vector3 scale = transform.lossyScale;
            float minimumScale = Mathf.Max(0.001f, Mathf.Min(Mathf.Abs(scale.x), Mathf.Min(Mathf.Abs(scale.y), Mathf.Abs(scale.z))));
            float localRadius = radius / minimumScale;
            // Include cells whose boxes intersect the sphere; use closest-point distance in world space.
            Vector3Int min = chunk.PointToCell(localHit - Vector3.one * localRadius);
            Vector3Int max = chunk.PointToCell(localHit + Vector3.one * localRadius);
            min = Vector3Int.Max(Vector3Int.zero, min);
            max = Vector3Int.Min(chunk.Size - Vector3Int.one, max);
            float squaredRadius = radius * radius;
            for (int z = min.z; z <= max.z; z++)
            for (int y = min.y; y <= max.y; y++)
            for (int x = min.x; x <= max.x; x++)
            {
                if (chunk.GetCell(x, y, z) == 0) continue;
                Vector3 lower = new Vector3(x, y, z) * chunk.VoxelSize;
                Vector3 upper = lower + Vector3.one * chunk.VoxelSize;
                Vector3 closest = new Vector3(Mathf.Clamp(localHit.x, lower.x, upper.x),
                    Mathf.Clamp(localHit.y, lower.y, upper.y), Mathf.Clamp(localHit.z, lower.z, upper.z));
                if ((transform.TransformPoint(closest) - worldHit).sqrMagnitude <= squaredRadius)
                    chunk.SetCell(x, y, z, 0);
            }
            // Several impacts coalesce into one bounded LateUpdate mesh/collider rebuild.
        }

        public void Restore()
        {
            if (IsInitialized) chunk.Fill(1);
        }

        private void LateUpdate()
        {
            if (IsInitialized && chunk.Dirty && Time.frameCount != lastRebuildFrame) Rebuild();
        }

        private void Rebuild()
        {
            if (!IsInitialized || !chunk.Dirty) return;
            using (VoxelMeshBuffers data = VoxelGreedyMesher.Build(chunk))
            {
                // Unity Mesh uploads and MeshCollider cooking happen here on the main thread.
                // Assigning sharedMesh still has physics cooking cost; the native job does not eliminate it.
                meshCollider.sharedMesh = null;
                mesh.Clear();
                if (data.Vertices.Length > 0)
                {
                    mesh.SetVertices(data.Vertices.AsArray());
                    mesh.SetNormals(data.Normals.AsArray());
                    mesh.SetUVs(0, data.UVs.AsArray());
                    mesh.SetIndices(data.Indices.AsArray(), MeshTopology.Triangles, 0, true);
                    mesh.RecalculateBounds();
                    meshCollider.sharedMesh = mesh;
                }
                bool hasGeometry = data.Vertices.Length > 0;
                meshRenderer.enabled = hasGeometry;
                meshCollider.enabled = hasGeometry;
                LastQuadCount = data.QuadCount;
                RebuildCount++;
                lastRebuildFrame = Time.frameCount;
                chunk.MarkClean();
            }
        }

        private void OnDestroy() { ReleaseOwnedData(); }

        private void ReleaseOwnedData()
        {
            if (meshCollider != null) meshCollider.sharedMesh = null;
            if (meshFilter != null) meshFilter.sharedMesh = null;
            if (chunk != null) chunk.Dispose();
            chunk = null;
            if (mesh != null)
            {
                if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
            }
            mesh = null;
        }
    }
}
