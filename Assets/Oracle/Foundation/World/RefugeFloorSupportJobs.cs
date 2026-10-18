using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace OdysseyOracle.Foundation.World
{
    [BurstCompile]
    public struct RefugeFloorBuildJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float4> Rectangles;
        public RefugeFloorGrid16 Grid;
        [WriteOnly] public NativeArray<ulong> Occupancy;

        public void Execute(int wordIndex)
        {
            ulong word = 0;
            uint first = (uint)(wordIndex * 64);
            for (int bit = 0; bit < 64; ++bit)
            {
                int2 cell = RefugeFloorSupportMath.DecodeMortonXZ(first + (uint)bit);
                if (cell.x >= Grid.Width || cell.y >= Grid.Depth) continue;
                for (int rect = 0; rect < Rectangles.Length; ++rect)
                {
                    if (!RefugeFloorSupportMath.CellIntersectsRectangle(Grid, cell.x, cell.y, Rectangles[rect]))
                        continue;
                    word |= 1ul << bit;
                    break;
                }
            }
            // Exactly one writer per word; intersecting rooms never duplicate a cell.
            Occupancy[wordIndex] = word;
        }
    }

    [BurstCompile]
    public struct RefugeFloorSupportJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float4> Rectangles;
        [ReadOnly] public NativeArray<ulong> Occupancy;
        [ReadOnly] public NativeArray<RefugeFloorQuery32> Queries;
        public RefugeFloorGrid16 Grid;
        [WriteOnly] public NativeArray<RefugeFloorResult32> Results;

        public void Execute(int index)
        {
            RefugeFloorQuery32 query = Queries[index];
            RefugeFloorResult32 result = new RefugeFloorResult32 {
                QueryId = query.QueryId, CellX = -1, CellZ = -1, Status = 0u };
            if (!RefugeFloorSupportMath.ValidQuery(query)) { result.Status = 2u; Results[index] = result; return; }
            if (!RefugeFloorSupportMath.DownwardSweepReachesSurface(query)) { Results[index] = result; return; }
            float2 center = new float2(query.Origin.x, query.Origin.z);
            if (!TrySupport(center, out int2 cell, out uint morton)) { Results[index] = result; return; }
            if ((query.Flags & RefugeFloorSupportMath.CheckFootprintCorners) != 0u)
            {
                for (int corner = 0; corner < 4; ++corner)
                {
                    float2 offset = new float2((corner & 1) == 0 ? -query.HalfFootprintXZ.x : query.HalfFootprintXZ.x,
                        (corner & 2) == 0 ? -query.HalfFootprintXZ.y : query.HalfFootprintXZ.y);
                    if (!TrySupport(center + offset, out _, out _)) { Results[index] = result; return; }
                }
            }
            result.Position = new float3(query.Origin.x, RefugeFloorSupportMath.SurfaceY, query.Origin.z);
            result.CellX = cell.x; result.CellZ = cell.y; result.MortonCell = morton; result.Status = 1u;
            Results[index] = result;
        }

        private bool TrySupport(float2 point, out int2 cell, out uint morton)
        {
            cell = default; morton = uint.MaxValue;
            bool exactCoverage = false;
            for (int i = 0; i < Rectangles.Length; ++i)
                if (RefugeFloorSupportMath.PointInRectangle(point, Rectangles[i])) { exactCoverage = true; break; }
            if (!exactCoverage) return false;
            bool found = false;
            for (int i = 0; i < 4; ++i)
            {
                if (!RefugeFloorSupportMath.TryCandidateCell(Grid, point, i, out int2 candidate, out uint key)) continue;
                if ((Occupancy[(int)(key >> 6)] & (1ul << (int)(key & 63u))) == 0 || key >= morton) continue;
                found = true; cell = candidate; morton = key;
            }
            return found;
        }
    }

    public static class RefugeFloorSupportCold
    {
        // COLD scheduling API. Completes input writers before inspecting borrowed rects.
        // The caller owns all four arrays and must retain every input through completion.
        public static JobHandle Schedule(NativeArray<float4> rectangles,
            NativeArray<RefugeFloorQuery32> queries, Allocator allocator,
            out RefugeFloorGrid16 grid, out NativeArray<ulong> occupancy,
            out NativeArray<RefugeFloorResult32> results, JobHandle inputDependencies = default)
        {
            grid = default; occupancy = default; results = default;
            inputDependencies.Complete();
            if (allocator != Allocator.Persistent && allocator != Allocator.TempJob)
                throw new ArgumentException("Cold support buffers require Persistent or TempJob ownership.", nameof(allocator));
            if (!rectangles.IsCreated || rectangles.Length < 1 || rectangles.Length > RefugeFloorSupportMath.MaximumRectangles)
                throw new ArgumentException("Supply 1..32 immutable floor rectangles.", nameof(rectangles));
            if (!queries.IsCreated || queries.Length > RefugeFloorSupportMath.MaximumQueries)
                throw new ArgumentException("Supply a created bounded query array (0..4096).", nameof(queries));
            float2 minimum = new float2(float.PositiveInfinity), maximum = new float2(float.NegativeInfinity);
            for (int i = 0; i < rectangles.Length; ++i)
            {
                float4 rect = rectangles[i];
                if (!RefugeFloorSupportMath.ValidRectangle(rect)) throw new ArgumentException("Invalid finite floor rectangle.", nameof(rectangles));
                minimum = math.min(minimum, rect.xy); maximum = math.max(maximum, rect.zw);
            }
            if (!RefugeFloorSupportMath.TryGrid(minimum, maximum, out grid))
                throw new ArgumentException("Floor extent must fit a representable 256x256 quarter-metre grid.", nameof(rectangles));
            JobHandle submitted = default;
            try
            {
                occupancy = new NativeArray<ulong>(RefugeFloorSupportMath.WordCount, allocator, NativeArrayOptions.UninitializedMemory);
                results = new NativeArray<RefugeFloorResult32>(queries.Length, allocator, NativeArrayOptions.UninitializedMemory);
                submitted = new RefugeFloorBuildJob { Rectangles = rectangles, Grid = grid, Occupancy = occupancy }
                    .Schedule(RefugeFloorSupportMath.WordCount, 16);
                if (queries.Length != 0)
                    submitted = new RefugeFloorSupportJob { Rectangles = rectangles, Occupancy = occupancy,
                        Grid = grid, Queries = queries, Results = results }.Schedule(queries.Length, 32, submitted);
                return submitted;
            }
            catch
            {
                submitted.Complete();
                if (results.IsCreated) results.Dispose();
                if (occupancy.IsCreated) occupancy.Dispose();
                results = default; occupancy = default;
                throw;
            }
        }

        // Pass the COMBINED fence of every consumer, including downstream World readers.
        // Retain/complete the returned disposal fence before the owner itself retires.
        public static JobHandle DisposeAfter(ref NativeArray<ulong> occupancy,
            ref NativeArray<RefugeFloorResult32> results, JobHandle allReaders)
        {
            JobHandle a = occupancy.IsCreated ? occupancy.Dispose(allReaders) : allReaders;
            JobHandle b = results.IsCreated ? results.Dispose(allReaders) : allReaders;
            occupancy = default; results = default;
            return JobHandle.CombineDependencies(a, b);
        }

        public static void CompleteAndDispose(ref NativeArray<ulong> occupancy,
            ref NativeArray<RefugeFloorResult32> results, JobHandle allReaders)
        {
            allReaders.Complete();
            if (results.IsCreated) results.Dispose();
            if (occupancy.IsCreated) occupancy.Dispose();
            results = default; occupancy = default;
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Single immutable refuge floor slab cellY=-1/topY=0; .25m common
// XZ coordinates. Exact supplied rectangle union refines Morton broadphase edge cells.
// Array Formats: Rect float4[1..32], ulong[1024], query32/result32[0..4096]; Grid16.
// One writer per occupancy word/query result; inputs immutable for the submitted fence.
// Dependencies/Pointers: World caller alone owns borrowed rectangles/queries and returned
// native buffers; complete input writers before cold validation, combine all reader fences
// before disposal, retain disposal fence. No UnityEngine.Object calls, hidden World, or saves.
