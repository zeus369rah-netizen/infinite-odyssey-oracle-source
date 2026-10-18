using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace OdysseyOracle
{
    /// <summary>
    /// Persistent native-data XZ grid for main-thread target snapshots. IDs belong to
    /// the caller, survive insertion order changes, and may be negative GetInstanceIDs.
    /// This managed bridge deliberately schedules no jobs: its four-target Web client
    /// does not need a worker thread, a Schedule/Complete barrier, or frame allocations.
    /// </summary>
    public sealed class SpatialTargetGrid : IDisposable
    {
        private const int MaximumCapacity = 1 << 20;
        private const int MaximumCellsPerQuery = 4096;

        private struct Entry
        {
            public int Id;
            public float3 Position;
            public int2 Cell;
        }

        // Native containers never leave this class. No external job can retain a copy
        // while Clear, capacity growth, or Dispose mutates their storage.
        private NativeParallelMultiHashMap<int2, int> cells;
        private NativeParallelHashMap<int, int> indicesById;
        private NativeList<Entry> entries;
        private readonly float cellSize;
        private int capacity;
        private bool disposed;

        public int Count { get { ThrowIfDisposed(); return entries.Length; } }
        public int Capacity { get { ThrowIfDisposed(); return capacity; } }
        public float CellSize => cellSize;
        public bool IsCreated => !disposed && cells.IsCreated && indicesById.IsCreated && entries.IsCreated;

        public SpatialTargetGrid(int capacity = 512, float cellSize = 2f)
        {
            if (capacity < 1 || capacity > MaximumCapacity)
                throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be between 1 and 1048576.");
            if (!IsFinite(cellSize) || cellSize <= 0f)
                throw new ArgumentOutOfRangeException(nameof(cellSize), "Cell size must be finite and positive.");
            this.capacity = capacity;
            this.cellSize = cellSize;
            try
            {
                cells = new NativeParallelMultiHashMap<int2, int>(capacity, Allocator.Persistent);
                indicesById = new NativeParallelHashMap<int, int>(capacity, Allocator.Persistent);
                entries = new NativeList<Entry>(capacity, Allocator.Persistent);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        /// <summary>Retains all allocated capacity for the next live-enemy snapshot.</summary>
        public void Clear()
        {
            ThrowIfDisposed();
            cells.Clear();
            indicesById.Clear();
            entries.Clear();
        }

        /// <summary>Adds a stable ID, or updates its existing position without duplicating it.</summary>
        public void Add(int stableId, Vector3 position)
        {
            ThrowIfDisposed();
            int2 cell = GetCell(position);
            Entry entry = new Entry
            {
                Id = stableId,
                Position = new float3(position.x, position.y, position.z),
                Cell = cell
            };
            if (indicesById.TryGetValue(stableId, out int existingIndex))
            {
                int2 previousCell = entries[existingIndex].Cell;
                if (!previousCell.Equals(cell))
                {
                    if (cells.TryGetFirstValue(previousCell, out int index, out var iterator))
                    {
                        do
                        {
                            if (index != existingIndex) continue;
                            cells.Remove(iterator);
                            break;
                        }
                        while (cells.TryGetNextValue(out index, ref iterator));
                    }
                    cells.Add(cell, existingIndex);
                }
                entries[existingIndex] = entry;
                return;
            }

            EnsureCapacity(entries.Length + 1);
            int newIndex = entries.Length;
            entries.Add(entry);
            indicesById.Add(stableId, newIndex);
            cells.Add(cell, newIndex);
        }

        /// <summary>Floor mapping, including coordinates immediately below zero. Y is ignored.</summary>
        public int2 GetCell(Vector3 position)
        {
            ThrowIfDisposed();
            ValidatePosition(position);
            // Double division avoids negative subnormal coordinates rounding to -0,
            // and detects out-of-range cell indices before casting to int.
            double x = Math.Floor((double)position.x / cellSize);
            double z = Math.Floor((double)position.z / cellSize);
            if (x < int.MinValue || x > int.MaxValue || z < int.MinValue || z > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(position), "Position exceeds the grid's signed 32-bit cell coordinates.");
            return new int2((int)x, (int)z);
        }

        /// <summary>
        /// Finds the nearest target by XZ distance, including the radius boundary.
        /// Equal-distance targets choose the lower signed stable ID deterministically.
        /// A false result sets id=-1 and position=zero; the bool remains authoritative.
        /// </summary>
        public bool TryFindNearest(Vector3 origin, float radius, out int id, out Vector3 position)
        {
            ThrowIfDisposed();
            ValidatePosition(origin);
            if (!IsFinite(radius) || radius < 0f)
                throw new ArgumentOutOfRangeException(nameof(radius), "Radius must be finite and nonnegative.");
            id = -1;
            position = Vector3.zero;
            if (entries.Length == 0) return false;

            double minimumX = Math.Floor(((double)origin.x - radius) / cellSize);
            double maximumX = Math.Floor(((double)origin.x + radius) / cellSize);
            double minimumZ = Math.Floor(((double)origin.z - radius) / cellSize);
            double maximumZ = Math.Floor(((double)origin.z + radius) / cellSize);
            if (maximumX < int.MinValue || minimumX > int.MaxValue ||
                maximumZ < int.MinValue || minimumZ > int.MaxValue) return false;
            int minX = (int)Math.Max(int.MinValue, minimumX);
            int maxX = (int)Math.Min(int.MaxValue, maximumX);
            int minZ = (int)Math.Max(int.MinValue, minimumZ);
            int maxZ = (int)Math.Min(int.MaxValue, maximumZ);
            double cellCount = ((double)maxX - minX + 1d) * ((double)maxZ - minZ + 1d);
            double nearestSquared = (double)radius * radius;
            int bestIndex = -1;

            if (cellCount > MaximumCellsPerQuery)
            {
                // Bounded fallback for huge radii/tiny cells; never walk billions of
                // empty cells or overflow a cell-loop counter.
                for (int i = 0; i < entries.Length; i++)
                    Consider(i, origin, ref nearestSquared, ref bestIndex);
            }
            else
            {
                for (long x = minX; x <= maxX; x++)
                for (long z = minZ; z <= maxZ; z++)
                {
                    if (!cells.TryGetFirstValue(new int2((int)x, (int)z), out int index, out var iterator)) continue;
                    do { Consider(index, origin, ref nearestSquared, ref bestIndex); }
                    while (cells.TryGetNextValue(out index, ref iterator));
                }
            }

            if (bestIndex < 0) return false;
            Entry nearest = entries[bestIndex];
            id = nearest.Id;
            position = new Vector3(nearest.Position.x, nearest.Position.y, nearest.Position.z);
            return true;
        }

        private void Consider(int index, Vector3 origin, ref double nearestSquared, ref int bestIndex)
        {
            Entry candidate = entries[index];
            double dx = (double)candidate.Position.x - origin.x;
            double dz = (double)candidate.Position.z - origin.z;
            double squared = dx * dx + dz * dz;
            if (squared > nearestSquared) return;
            if (bestIndex >= 0 && squared == nearestSquared && candidate.Id >= entries[bestIndex].Id) return;
            nearestSquared = squared;
            bestIndex = index;
        }

        /// <summary>Nearest XZ neighbors into caller storage; distance ties use signed stable IDs.</summary>
        public int QueryRadius(Vector3 origin, float radius, int excludedStableId, int[] resultIds)
        {
            ThrowIfDisposed(); ValidatePosition(origin);
            if (resultIds == null) throw new ArgumentNullException(nameof(resultIds));
            if (!IsFinite(radius) || radius < 0f) throw new ArgumentOutOfRangeException(nameof(radius));
            if (resultIds.Length == 0 || entries.Length == 0) return 0;
            double minXd = Math.Floor(((double)origin.x - radius) / cellSize), maxXd = Math.Floor(((double)origin.x + radius) / cellSize);
            double minZd = Math.Floor(((double)origin.z - radius) / cellSize), maxZd = Math.Floor(((double)origin.z + radius) / cellSize);
            if (maxXd < int.MinValue || minXd > int.MaxValue || maxZd < int.MinValue || minZd > int.MaxValue) return 0;
            int minX = (int)Math.Max(int.MinValue, minXd), maxX = (int)Math.Min(int.MaxValue, maxXd);
            int minZ = (int)Math.Max(int.MinValue, minZd), maxZ = (int)Math.Min(int.MaxValue, maxZd), count = 0;
            double squaredRadius = (double)radius * radius;
            if (((double)maxX - minX + 1d) * ((double)maxZ - minZ + 1d) > MaximumCellsPerQuery)
            {
                for (int i = 0; i < entries.Length; i++) ConsiderRadius(i, origin, squaredRadius, excludedStableId, resultIds, ref count);
            }
            else for (long x = minX; x <= maxX; x++) for (long z = minZ; z <= maxZ; z++)
            {
                if (!cells.TryGetFirstValue(new int2((int)x, (int)z), out int index, out var iterator)) continue;
                do { ConsiderRadius(index, origin, squaredRadius, excludedStableId, resultIds, ref count); }
                while (cells.TryGetNextValue(out index, ref iterator));
            }
            return count;
        }

        private void ConsiderRadius(int index, Vector3 origin, double radiusSquared, int excludedId, int[] output, ref int count)
        {
            Entry candidate = entries[index];
            if (candidate.Id == excludedId) return;
            double dx = (double)candidate.Position.x - origin.x, dz = (double)candidate.Position.z - origin.z;
            double squared = dx * dx + dz * dz;
            if (squared > radiusSquared) return;
            int slot = 0;
            for (; slot < count; slot++)
            {
                Entry previous = entries[indicesById[output[slot]]];
                double px = (double)previous.Position.x - origin.x, pz = (double)previous.Position.z - origin.z;
                double previousSquared = px * px + pz * pz;
                if (squared < previousSquared || squared == previousSquared && candidate.Id < previous.Id) break;
            }
            if (slot >= output.Length) return;
            int newCount = Math.Min(count + 1, output.Length);
            for (int j = newCount - 1; j > slot; j--) output[j] = output[j - 1];
            output[slot] = candidate.Id; count = newCount;
        }

        private void EnsureCapacity(int required)
        {
            if (required <= capacity) return;
            if (required > MaximumCapacity)
                throw new InvalidOperationException("The spatial target grid reached its 1048576-target limit.");
            // There are no outstanding jobs: this class owns all container access.
            int next = Math.Min(MaximumCapacity, Math.Max(required, capacity * 2));
            cells.Capacity = next;
            indicesById.Capacity = next;
            entries.Capacity = next;
            capacity = next;
        }

        public void Dispose()
        {
            if (disposed) return;
            if (cells.IsCreated) cells.Dispose();
            if (indicesById.IsCreated) indicesById.Dispose();
            if (entries.IsCreated) entries.Dispose();
            disposed = true;
        }

        private void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(nameof(SpatialTargetGrid));
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static void ValidatePosition(Vector3 position)
        {
            if (!IsFinite(position.x) || !IsFinite(position.y) || !IsFinite(position.z))
                throw new ArgumentException("Position must contain finite coordinates.", nameof(position));
        }
    }
}
