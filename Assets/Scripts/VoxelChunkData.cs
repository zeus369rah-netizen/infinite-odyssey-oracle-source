using System;
using Unity.Collections;
using UnityEngine;

namespace OdysseyOracle
{
    /// <summary>Dense chunk cells: zero is air; other bytes identify solid merge groups.</summary>
    public sealed class VoxelChunkData : IDisposable
    {
        public const int MaximumDimension = 32;
        public const int MaximumCellCount = 32768;
        public Vector3Int Size { get; }
        public float VoxelSize { get; }
        public int Length => cells.IsCreated ? cells.Length : 0;
        public bool IsCreated => cells.IsCreated;
        public bool Dirty { get; private set; }
        public int Revision { get; private set; }
        private NativeArray<byte> cells;

        // The mesher receives a read-only job field; mutation remains behind dirty tracking.
        internal NativeArray<byte> Cells
        {
            get { RequireCreated(); return cells; }
        }

        public VoxelChunkData(Vector3Int size, float voxelSize)
        {
            if (size.x <= 0 || size.y <= 0 || size.z <= 0 ||
                size.x > MaximumDimension || size.y > MaximumDimension || size.z > MaximumDimension ||
                (long)size.x * size.y * size.z > MaximumCellCount)
                throw new ArgumentOutOfRangeException(nameof(size), "Chunk dimensions must be 1..32, with at most 32768 cells.");
            if (float.IsNaN(voxelSize) || float.IsInfinity(voxelSize) || voxelSize <= 0f)
                throw new ArgumentOutOfRangeException(nameof(voxelSize));
            Size = size;
            VoxelSize = voxelSize;
            cells = new NativeArray<byte>(size.x * size.y * size.z, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            Dirty = true;
        }

        public bool Contains(int x, int y, int z)
        {
            return x >= 0 && y >= 0 && z >= 0 && x < Size.x && y < Size.y && z < Size.z;
        }

        public byte GetCell(int x, int y, int z)
        {
            RequireCreated();
            return Contains(x, y, z) ? cells[x + Size.x * (y + Size.y * z)] : (byte)0;
        }

        /// <returns>True only when an in-bounds cell actually changed.</returns>
        public bool SetCell(int x, int y, int z, byte value)
        {
            RequireCreated();
            if (!Contains(x, y, z)) return false;
            int index = x + Size.x * (y + Size.y * z);
            if (cells[index] == value) return false;
            cells[index] = value;
            Dirty = true;
            Revision++;
            return true;
        }

        public void Fill(byte value)
        {
            RequireCreated();
            bool changed = false;
            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i] == value) continue;
                cells[i] = value;
                changed = true;
            }
            if (!changed) return;
            Dirty = true;
            Revision++;
        }

        public Vector3Int PointToCell(Vector3 chunkLocalPoint)
        {
            return new Vector3Int(Mathf.FloorToInt(chunkLocalPoint.x / VoxelSize),
                Mathf.FloorToInt(chunkLocalPoint.y / VoxelSize), Mathf.FloorToInt(chunkLocalPoint.z / VoxelSize));
        }

        public Vector3 CellCenter(int x, int y, int z)
        {
            return new Vector3((x + 0.5f) * VoxelSize, (y + 0.5f) * VoxelSize, (z + 0.5f) * VoxelSize);
        }

        public void MarkClean()
        {
            RequireCreated();
            Dirty = false;
        }

        public void Dispose()
        {
            if (cells.IsCreated) cells.Dispose();
            cells = default(NativeArray<byte>);
            Dirty = false;
        }

        private void RequireCreated()
        {
            if (!cells.IsCreated) throw new ObjectDisposedException(nameof(VoxelChunkData));
        }
    }
}
