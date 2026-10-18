using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace OdysseyOracle.Foundation.World
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct CrossPrismInput32
    {
        public float2 CenterXZ;
        public float BottomY;
        public float TopY;
        public float Radius;
        public uint Profile; // 0 wood(1.42/1.87/.77), 1 iron(1.49/1.94/.82).
        public uint Reserved0;
        public uint Reserved1;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct CrossPrismVertex32
    {
        public float3 Position;
        public float3 Normal;
        public float2 UV;
    }

    public static class ColdCrossPrismMath
    {
        public const int OutlineCount = 20;
        public const int VertexCount = 122; // 21 top +21 bottom +20*4 hard-normal sides.
        public const int IndexCount = 240; // 20 top +20 bottom +40 side triangles.
        public const int MaximumPrisms = 64;
        public const float UvMetres = 0.8f;

        public static int3 HalfExtentUnits(uint profile) =>
            profile == 0u ? new int3(2840, 3740, 1540) : new int3(2980, 3880, 1640);

        // Clockwise in XZ viewed from +Y: a cap fan (origin,i,next) has +Y normal.
        public static int2 OutlineUnits(int i, int3 half)
        {
            int a = half.x, b = half.y, c = half.z;
            switch (i)
            {
                case 0: return new int2(-c, b); case 1: return new int2(c, b);
                case 2: return new int2(c, a); case 3: return new int2(a, a);
                case 4: return new int2(a, c); case 5: return new int2(b, c);
                case 6: return new int2(b, -c); case 7: return new int2(a, -c);
                case 8: return new int2(a, -a); case 9: return new int2(c, -a);
                case 10: return new int2(c, -b); case 11: return new int2(-c, -b);
                case 12: return new int2(-c, -a); case 13: return new int2(-a, -a);
                case 14: return new int2(-a, -c); case 15: return new int2(-b, -c);
                case 16: return new int2(-b, c); case 17: return new int2(-a, c);
                case 18: return new int2(-a, a); default: return new int2(-c, a);
            }
        }

        public static bool Valid(CrossPrismInput32 input)
        {
            if (!Finite(input.CenterXZ.x) || !Finite(input.CenterXZ.y) || !Finite(input.BottomY) ||
                !Finite(input.TopY) || !Finite(input.Radius) || input.Radius <= 0f ||
                input.BottomY >= input.TopY || input.Profile > 1u || input.Reserved0 != 0 || input.Reserved1 != 0)
                return false;
            if (!Finite((input.TopY - input.BottomY) / UvMetres)) return false;
            float3 half = (float3)HalfExtentUnits(input.Profile) * (input.Radius / 4000f);
            if (!(half.z > 0f && half.x > half.z && half.y > half.x)) return false;
            if (!Finite((8f * half.y) / UvMetres)) return false;
            return AxisRepresentable(input.CenterXZ.x, half) && AxisRepresentable(input.CenterXZ.y, half);
        }

        private static bool AxisRepresentable(float center, float3 half) =>
            Finite(center - half.y) && Finite(center + half.y) && center - half.y < center - half.x &&
            center - half.x < center - half.z && center - half.z < center && center < center + half.z &&
            center + half.z < center + half.x && center + half.x < center + half.y;

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        // COLD only: caller must complete input writers before inspecting these arrays.
        public static bool ValidBuffers(NativeArray<CrossPrismInput32> inputs,
            NativeArray<CrossPrismVertex32> vertices, NativeArray<int> indices)
        {
            if (!inputs.IsCreated || !vertices.IsCreated || !indices.IsCreated || inputs.Length < 1 ||
                inputs.Length > MaximumPrisms || vertices.Length != inputs.Length * VertexCount ||
                indices.Length != inputs.Length * IndexCount) return false;
            for (int i = 0; i < inputs.Length; ++i) if (!Valid(inputs[i])) return false;
            return true;
        }

        public static CrossPrismVertex32 Vertex(CrossPrismInput32 input, int index)
        {
            int3 half = HalfExtentUnits(input.Profile);
            float unit = input.Radius / 4000f;
            float outer = half.y * unit;
            if (index < 42)
            {
                bool top = index < 21;
                int local = top ? index : index - 21;
                float2 p = local == 0 ? new float2(0) : (float2)OutlineUnits(local - 1, half) * unit;
                return new CrossPrismVertex32 {
                    Position = new float3(input.CenterXZ.x + p.x, top ? input.TopY : input.BottomY, input.CenterXZ.y + p.y),
                    Normal = new float3(0, top ? 1 : -1, 0),
                    UV = new float2((p.x + outer) / UvMetres, ((top ? -p.y : p.y) + outer) / UvMetres) };
            }
            int side = (index - 42) / 4, corner = (index - 42) % 4;
            int2 a = OutlineUnits(side, half), b = OutlineUnits((side + 1) % OutlineCount, half);
            int2 delta = b - a;
            bool next = corner == 1 || corner == 2, upper = corner >= 2;
            float2 point = (float2)(next ? b : a) * unit;
            int perimeter = 0;
            for (int i = 0; i < side; ++i)
            {
                int2 edge = OutlineUnits((i + 1) % OutlineCount, half) - OutlineUnits(i, half);
                perimeter += Math.Abs(edge.x) + Math.Abs(edge.y);
            }
            if (next) perimeter += Math.Abs(delta.x) + Math.Abs(delta.y);
            return new CrossPrismVertex32 {
                Position = new float3(input.CenterXZ.x + point.x, upper ? input.TopY : input.BottomY, input.CenterXZ.y + point.y),
                Normal = new float3(-Math.Sign(delta.y), 0, Math.Sign(delta.x)),
                UV = new float2(perimeter * unit / UvMetres, upper ? (input.TopY - input.BottomY) / UvMetres : 0f) };
        }

        public static int Index(int index)
        {
            if (index < 120)
            {
                bool top = index < 60;
                int local = top ? index : index - 60;
                int edge = local / 3, corner = local % 3, offset = top ? 0 : 21;
                if (corner == 0) return offset;
                int first = 1 + edge, second = 1 + (edge + 1) % OutlineCount;
                return offset + ((corner == 1) == top ? first : second);
            }
            int localSide = index - 120, start = 42 + (localSide / 6) * 4;
            switch (localSide % 6)
            {
                case 0: return start; case 1: return start + 1; case 2: return start + 2;
                case 3: return start; case 4: return start + 2; default: return start + 3;
            }
        }
    }

    [BurstCompile]
    public struct ColdCrossPrismJob : IJob
    {
        [ReadOnly] public NativeArray<CrossPrismInput32> Inputs;
        [WriteOnly] public NativeArray<CrossPrismVertex32> Vertices;
        [WriteOnly] public NativeArray<int> Indices;

        // Cold caller validates all inputs and exact bounded output capacities before Schedule.
        // One job can build all7 barrel layers. No Unity Objects or native allocations here.
        public void Execute()
        {
            for (int prism = 0; prism < Inputs.Length; ++prism)
            {
                int vertexStart = prism * ColdCrossPrismMath.VertexCount;
                int indexStart = prism * ColdCrossPrismMath.IndexCount;
                CrossPrismInput32 input = Inputs[prism];
                for (int i = 0; i < ColdCrossPrismMath.VertexCount; ++i)
                    Vertices[vertexStart + i] = ColdCrossPrismMath.Vertex(input, i);
                for (int i = 0; i < ColdCrossPrismMath.IndexCount; ++i)
                    Indices[indexStart + i] = vertexStart + ColdCrossPrismMath.Index(i);
            }
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Caller common Y-up metres; axis-aligned cross union, exact supplied Y
// bounds. Twenty integer XZ outline points use denominator4000, scaled once by radius.
// Array Formats: Input32/vertex32 bytes; each prism122 vertex records/240 int indices;
// maximum64 prisms. Shared cap rings and hard-normal side duplicates coincide bit-for-bit.
// Dependencies/Pointers: Pure numeric Burst IJob; existing World owns validated immutable
// inputs, output buffers and completion/disposal fences. No Objects, mesh uploads or saves.
