using System;
using OdysseyOracle.Foundation.World;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class DungeonWorld
    {
        private void BuildUnionBarrel(Vector3 p, float radius, float height)
        {
            // COLD authoring only. All7 prisms share one Schedule/Complete and one buffer set.
            NativeArray<CrossPrismInput32> inputs = default;
            NativeArray<CrossPrismVertex32> vertices = default;
            NativeArray<int> indices = default;
            JobHandle fence = default;
            try
            {
                inputs = new NativeArray<CrossPrismInput32>(7, Allocator.TempJob);
                vertices = new NativeArray<CrossPrismVertex32>(7 * ColdCrossPrismMath.VertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                indices = new NativeArray<int>(7 * ColdCrossPrismMath.IndexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                for (int tier = 0; tier < 5; ++tier)
                {
                    float r = radius * (tier == 0 || tier == 4 ? 0.83f : (tier == 2 ? 1f : 0.94f));
                    inputs[tier] = new CrossPrismInput32 { CenterXZ = new float2(p.x, p.z), Radius = r,
                        BottomY = tier == 0 ? p.y : p.y + height * tier / 5f,
                        TopY = tier == 4 ? p.y + height : p.y + height * (tier + 1) / 5f, Profile = 0u };
                }
                for (int band = 0; band < 2; ++band)
                {
                    float y = height * (band == 0 ? 0.18f : 0.78f);
                    inputs[5 + band] = new CrossPrismInput32 { CenterXZ = new float2(p.x, p.z), Radius = radius * 0.98f,
                        BottomY = p.y + y - 0.095f * 0.5f, TopY = p.y + y + 0.095f * 0.5f, Profile = 1u };
                }
                if (!ColdCrossPrismMath.ValidBuffers(inputs, vertices, indices))
                    throw new ArgumentException("Invalid bounded barrel cross-prism buffers or bounds.");
                fence = new ColdCrossPrismJob { Inputs = inputs, Vertices = vertices, Indices = indices }.Schedule();
                fence.Complete();
                for (int layer = 0; layer < inputs.Length; ++layer)
                    AppendBarrelPrism(layer < 5 ? Surface.Wood : Surface.Iron, inputs[layer], vertices, indices, layer);

                // Exactly one cold barrel physics object,21 per-layer boxes plus1 top cap.
                GameObject colliderRoot = new GameObject("Barrel exact tier cross colliders");
                colliderRoot.transform.SetParent(geometryRoot, false);
                colliderRoot.transform.localPosition = p;
                for (int layer = 0; layer < inputs.Length; ++layer)
                {
                    CrossPrismInput32 input = inputs[layer];
                    float3 half = (float3)ColdCrossPrismMath.HalfExtentUnits(input.Profile) * (input.Radius / 4000f);
                    Vector3 center = new Vector3(0, (input.BottomY + input.TopY) * .5f - p.y, 0);
                    AddBarrelBox(colliderRoot, center, new Vector3(half.x * 2, input.TopY - input.BottomY, half.x * 2));
                    AddBarrelBox(colliderRoot, center, new Vector3(half.y * 2, input.TopY - input.BottomY, half.z * 2));
                    AddBarrelBox(colliderRoot, center, new Vector3(half.z * 2, input.TopY - input.BottomY, half.y * 2));
                }
                AddBarrelBox(colliderRoot, new Vector3(0, height + .006f, 0), new Vector3(radius * 1.35f, .04f, radius * 1.35f));
            }
            finally
            {
                fence.Complete();
                if (indices.IsCreated) indices.Dispose();
                if (vertices.IsCreated) vertices.Dispose();
                if (inputs.IsCreated) inputs.Dispose();
            }
            Cube(Surface.WoodEnd, p + new Vector3(0, height + 0.006f, 0), new Vector3(radius * 1.35f, 0.04f, radius * 1.35f));
            Cube(Surface.Iron, p + new Vector3(0, height + 0.032f, 0.05f), new Vector3(0.14f, 0.025f, 0.14f));
        }

        private void AppendBarrelPrism(Surface surface, CrossPrismInput32 input,
            NativeArray<CrossPrismVertex32> vertices, NativeArray<int> indices, int layer)
        {
            Vector3 center = new Vector3(input.CenterXZ.x, (input.BottomY + input.TopY) * .5f, input.CenterXZ.y);
            float3 half = (float3)ColdCrossPrismMath.HalfExtentUnits(input.Profile) * (input.Radius / 4000f);
            float layerHeight = input.TopY - input.BottomY;
            CaptureVignetteKeepout(center, new Vector3(half.x * 2, layerHeight, half.x * 2), Quaternion.identity);
            CaptureVignetteKeepout(center, new Vector3(half.y * 2, layerHeight, half.z * 2), Quaternion.identity);
            CaptureVignetteKeepout(center, new Vector3(half.z * 2, layerHeight, half.y * 2), Quaternion.identity);
            Batch batch = GetGeometryBatch(surface, center, out Vector3 origin);
            int batchStart = batch.Vertices.Count, vertexStart = layer * ColdCrossPrismMath.VertexCount;
            for (int i = 0; i < ColdCrossPrismMath.VertexCount; ++i)
            {
                CrossPrismVertex32 v = vertices[vertexStart + i];
                batch.Vertices.Add(new Vector3(v.Position.x, v.Position.y, v.Position.z) - origin);
                batch.Normals.Add(new Vector3(v.Normal.x, v.Normal.y, v.Normal.z));
                batch.UVs.Add(new Vector2(v.UV.x, v.UV.y));
            }
            int indexStart = layer * ColdCrossPrismMath.IndexCount;
            for (int i = 0; i < ColdCrossPrismMath.IndexCount; ++i)
                batch.Triangles.Add(batchStart + indices[indexStart + i] - vertexStart);
        }

        private static void AddBarrelBox(GameObject owner, Vector3 center, Vector3 size)
        {
            UnityEngine.BoxCollider shape = owner.AddComponent<UnityEngine.BoxCollider>();
            shape.center = center;
            shape.size = size;
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing DungeonWorld geometry-root metres; input bounds share exact
// Y boundaries (first bottom=p.y). Existing spatial Batch origins are subtracted after Complete.
// Array Formats: Seven input32,854 vertex32,1680 indices; five wood/two iron prisms. One
// cold collider object with22 BoxColliders; decorative bung stays visually unchanged.
// Dependencies/Pointers: Existing World/Batch/resources retain sole scene lifetime. Three
// TempJob buffers disposed after explicit writer completion; Object calls are main-thread
// cold append/physics adapter only. No runtime Update work, new World, random calls or saves.
