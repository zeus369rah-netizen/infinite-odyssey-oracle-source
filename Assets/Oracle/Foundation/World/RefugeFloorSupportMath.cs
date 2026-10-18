using System;
using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace OdysseyOracle.Foundation.World
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct RefugeFloorGrid16
    {
        public float2 OriginXZ;
        public int Width;
        public int Depth;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct RefugeFloorQuery32
    {
        public float3 Origin;
        public float MaximumDistance;
        public float2 HalfFootprintXZ;
        public uint Flags; // Bit 0: require center AND four footprint corners.
        public uint QueryId;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct RefugeFloorResult32
    {
        public float3 Position;
        public uint QueryId;
        public uint MortonCell; // Local XZ only. Not a global chunk/save identity.
        public int CellX;
        public int CellZ;
        public uint Status; // 0 unsupported, 1 supported, 2 invalid query.
    }

    public static class RefugeFloorSupportMath
    {
        public const int MaximumDimension = 256;
        public const int WordCount = 1024;
        public const int MaximumRectangles = 32;
        public const int MaximumQueries = 4096;
        public const float CellSize = 0.25f;
        public const int FloorCellY = -1;
        public const float SurfaceY = 0f;
        public const uint CheckFootprintCorners = 1u;

        public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        // Rect layout is (xmin, zmin, xmax, zmax), in the same space as query origins.
        public static bool ValidRectangle(float4 rect) =>
            Finite(rect.x) && Finite(rect.y) && Finite(rect.z) && Finite(rect.w) &&
            rect.x < rect.z && rect.y < rect.w;

        public static bool TryGrid(float2 minimum, float2 maximum, out RefugeFloorGrid16 grid)
        {
            grid = default;
            if (!Finite(minimum.x) || !Finite(minimum.y) || !Finite(maximum.x) ||
                !Finite(maximum.y) || minimum.x >= maximum.x || minimum.y >= maximum.y)
                return false;
            double ox = Math.Floor((double)minimum.x * 4.0) * 0.25;
            double oz = Math.Floor((double)minimum.y * 4.0) * 0.25;
            double width = Math.Ceiling(((double)maximum.x - ox) * 4.0);
            double depth = Math.Ceiling(((double)maximum.y - oz) * 4.0);
            if (width < 1 || width > MaximumDimension || depth < 1 || depth > MaximumDimension)
                return false;
            grid = new RefugeFloorGrid16 { OriginXZ = new float2((float)ox, (float)oz),
                Width = (int)width, Depth = (int)depth };
            if (!ValidGrid(grid)) { grid = default; return false; }
            return true;
        }

        public static bool ValidGrid(RefugeFloorGrid16 grid)
        {
            if (grid.Width < 1 || grid.Width > MaximumDimension || grid.Depth < 1 ||
                grid.Depth > MaximumDimension || !Finite(grid.OriginXZ.x) || !Finite(grid.OriginXZ.y))
                return false;
            double x = grid.OriginXZ.x;
            double z = grid.OriginXZ.y;
            // Reject collapsed/rounded lattice coordinates instead of introducing offsets.
            return x * 4.0 == Math.Floor(x * 4.0) && z * 4.0 == Math.Floor(z * 4.0) &&
                (double)(float)(x + CellSize) == x + CellSize &&
                (double)(float)(z + CellSize) == z + CellSize &&
                (double)(float)(x + grid.Width * 0.25) == x + grid.Width * 0.25 &&
                (double)(float)(z + grid.Depth * 0.25) == z + grid.Depth * 0.25 &&
                (double)(float)(x + (grid.Width - 1) * 0.25) == x + (grid.Width - 1) * 0.25 &&
                (double)(float)(z + (grid.Depth - 1) * 0.25) == z + (grid.Depth - 1) * 0.25;
        }

        public static uint MortonXZ(int x, int z)
        {
            uint xx = (uint)x & 255u, zz = (uint)z & 255u;
            xx = (xx | (xx << 4)) & 0x0F0Fu;
            xx = (xx | (xx << 2)) & 0x3333u;
            xx = (xx | (xx << 1)) & 0x5555u;
            zz = (zz | (zz << 4)) & 0x0F0Fu;
            zz = (zz | (zz << 2)) & 0x3333u;
            zz = (zz | (zz << 1)) & 0x5555u;
            return xx | (zz << 1);
        }

        public static int2 DecodeMortonXZ(uint value)
        {
            uint x = value & 0x5555u, z = (value >> 1) & 0x5555u;
            x = (x | (x >> 1)) & 0x3333u; z = (z | (z >> 1)) & 0x3333u;
            x = (x | (x >> 2)) & 0x0F0Fu; z = (z | (z >> 2)) & 0x0F0Fu;
            x = (x | (x >> 4)) & 0x00FFu; z = (z | (z >> 4)) & 0x00FFu;
            return new int2((int)x, (int)z);
        }

        public static bool CellIntersectsRectangle(RefugeFloorGrid16 grid, int x, int z, float4 rect)
        {
            if ((uint)x >= (uint)grid.Width || (uint)z >= (uint)grid.Depth) return false;
            double left = (double)grid.OriginXZ.x + x * 0.25;
            double near = (double)grid.OriginXZ.y + z * 0.25;
            return left < rect.z && left + 0.25 > rect.x && near < rect.w && near + 0.25 > rect.y;
        }

        public static bool PointInRectangle(float2 point, float4 rect) =>
            point.x >= rect.x && point.x <= rect.z && point.y >= rect.y && point.y <= rect.w;

        public static bool RectangleGridAligned(RefugeFloorGrid16 grid, float4 rect) =>
            IsInteger(((double)rect.x - grid.OriginXZ.x) * 4.0) &&
            IsInteger(((double)rect.z - grid.OriginXZ.x) * 4.0) &&
            IsInteger(((double)rect.y - grid.OriginXZ.y) * 4.0) &&
            IsInteger(((double)rect.w - grid.OriginXZ.y) * 4.0);

        private static bool IsInteger(double value) => value == Math.Floor(value);

        public static bool ValidQuery(RefugeFloorQuery32 query) =>
            Finite(query.Origin.x) && Finite(query.Origin.y) && Finite(query.Origin.z) &&
            Finite(query.MaximumDistance) && query.MaximumDistance >= 0f &&
            Finite(query.HalfFootprintXZ.x) && Finite(query.HalfFootprintXZ.y) &&
            query.HalfFootprintXZ.x >= 0f && query.HalfFootprintXZ.y >= 0f &&
            (query.Flags & ~CheckFootprintCorners) == 0u;

        public static bool DownwardSweepReachesSurface(RefugeFloorQuery32 query) =>
            query.Origin.y >= SurfaceY && (double)query.MaximumDistance >= (double)query.Origin.y;

        // An exact grid-edge point can rest on either adjacent solid cell. No epsilon.
        // Candidate order: lower coordinates first. Invalid cells return false.
        public static bool TryCandidateCell(RefugeFloorGrid16 grid, float2 point, int candidate,
            out int2 cell, out uint morton)
        {
            cell = default; morton = 0;
            if ((uint)candidate > 3u || !Finite(point.x) || !Finite(point.y)) return false;
            double x = ((double)point.x - grid.OriginXZ.x) * 4.0;
            double z = ((double)point.y - grid.OriginXZ.y) * 4.0;
            if (x < 0 || z < 0 || x > grid.Width || z > grid.Depth) return false;
            int ix = (int)Math.Floor(x), iz = (int)Math.Floor(z);
            bool onX = x == ix, onZ = z == iz;
            if ((candidate & 1) == 0) { if (!onX) return false; --ix; }
            if ((candidate & 2) == 0) { if (!onZ) return false; --iz; }
            if ((uint)ix >= (uint)grid.Width || (uint)iz >= (uint)grid.Depth) return false;
            cell = new int2(ix, iz); morton = MortonXZ(ix, iz); return true;
        }

        // Adapter only: preserves VoxelChunkData's <=32 row-major byte storage.
        public static bool TryLegacyAddress(int3 cell, int3 size, out int3 chunk,
            out int3 local, out int rowMajorIndex)
        {
            chunk = default; local = default; rowMajorIndex = -1;
            if (size.x < 1 || size.x > 32 || size.y < 1 || size.y > 32 || size.z < 1 || size.z > 32)
                return false;
            chunk = new int3(FloorDivide(cell.x, size.x, out int lx),
                FloorDivide(cell.y, size.y, out int ly), FloorDivide(cell.z, size.z, out int lz));
            local = new int3(lx, ly, lz);
            rowMajorIndex = lx + size.x * (ly + size.y * lz);
            return true;
        }

        private static int FloorDivide(int value, int divisor, out int remainder)
        {
            int quotient = value / divisor; remainder = value % divisor;
            if (remainder < 0) { --quotient; remainder += divisor; }
            return quotient;
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Caller-owned common world/local Y-up metres; XZ quarter-metre grid,
// floor layer cellY=-1, exact top vertexY=0; local Morton16 XZ is not a global key.
// Array Formats: Grid16/query32/result32 bytes; ulong[1024] broadphase. Rect float4 is
// xmin,zmin,xmax,zmax. Existing VoxelChunkData byte row-major format is adapted unchanged.
// Dependencies/Pointers: Pure numeric values only; no Unity Objects, managed runtime state,
// borrowed pointers, save rewrites, renderer binding, or independent World owner.
