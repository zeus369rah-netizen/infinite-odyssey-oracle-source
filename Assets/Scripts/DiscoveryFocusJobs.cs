using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace OdysseyOracle
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct DiscoveryFocusSlot48
    {
        public float3 Center; public uint Slot;
        public float3 HalfExtents; public uint Active;
        public quaternion Rotation;
    }
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct DiscoveryFocusResult16
    {
        public int Slot; public float Distance; public uint Found; public uint ReservedZero;
    }
    [BurstCompile]
    public struct DiscoveryFocusJob : IJob
    {
        [ReadOnly] public NativeArray<DiscoveryFocusSlot48> Slots;
        public NativeArray<DiscoveryFocusResult16> Result;
        public float3 Origin, Direction;
        public float Range;
        public void Execute()
        {
            var best = new DiscoveryFocusResult16 { Slot = -1, Distance = Range };
            if (!Result.IsCreated || Result.Length != 1) return;
            Result[0] = best;
            if (!Slots.IsCreated || Slots.Length > 16 || !math.all(math.isfinite(Origin)) ||
                !math.all(math.isfinite(Direction)) || !math.isfinite(Range) || Range <= 0 || Range > 2.5f ||
                math.any(math.abs(Origin) > 4096f)) return;
            float length = math.lengthsq(Direction);
            if (!math.isfinite(length) || length < .999f || length > 1.001f) return;
            for (int i = 0; i < Slots.Length; ++i)
            {
                DiscoveryFocusSlot48 slot = Slots[i];
                if (slot.Active != 1 || slot.Slot >= 14 || !math.all(math.isfinite(slot.Center)) ||
                    !math.all(math.isfinite(slot.HalfExtents)) || math.any(slot.HalfExtents <= 0) ||
                    math.any(slot.HalfExtents > 2) || math.any(math.abs(slot.Center) > 4096f) ||
                    !math.all(math.isfinite(slot.Rotation.value))) continue;
                float q = math.lengthsq(slot.Rotation.value);
                if (q < .999f || q > 1.001f) continue;
                quaternion inverse = math.conjugate(math.normalize(slot.Rotation));
                float3 o = math.rotate(inverse, Origin - slot.Center), d = math.rotate(inverse, Direction);
                float enter = 0, exit = Range;
                bool hit = true;
                for (int axis = 0; axis < 3; ++axis)
                {
                    float p = o[axis], v = d[axis], half = slot.HalfExtents[axis];
                    if (v == 0) { if (p < -half || p > half) { hit = false; break; } continue; }
                    float a = (-half - p) / v, b = (half - p) / v;
                    enter = math.max(enter, math.min(a, b)); exit = math.min(exit, math.max(a, b));
                    if (enter > exit) { hit = false; break; }
                }
                if (hit && (best.Found == 0 || enter < best.Distance || enter == best.Distance && slot.Slot < (uint)best.Slot))
                    best = new DiscoveryFocusResult16 { Slot = (int)slot.Slot, Distance = enter, Found = 1 };
            }
            Result[0] = best;
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Finite Y-up world metres, +Z eye ray, closed0..2.5m OBB intersections.
// Array Formats: Persistent16x48B fixed interaction registry and1x16B result; stable slot ties; no voxel/hash neighbor lookup.
// Dependencies/Pointers: Job borrows immutable slots until fence; owner completes before updates/dispose; one selected engine LOS adapter remains outside all loops.
