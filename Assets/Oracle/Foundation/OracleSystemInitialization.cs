using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace OdysseyOracle.Foundation
{
    // Foundation storage only. Existing game/session owners are never replaced or attached implicitly.
    [StructLayout(LayoutKind.Sequential)]
    public struct FoundationChunkSlot
    {
        public ulong MortonKey;
        public ulong OccupancySummary;
        public uint LeafStart;
        public uint Revision;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct FoundationActorSlot
    {
        public float3 Position;
        public float3 Velocity;
        public quaternion Rotation;
        public uint Generation;
        public uint Kind;
        public uint StatusMask;
        public float NormalizedHealth;
        public uint Active;
        public uint Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct FoundationItemSlot
    {
        public ulong InstanceId;
        public ulong Seed;
        public uint DefinitionId;
        public uint OwnerIndex;
        public ushort Slot;
        public ushort StackCount;
        public uint Revision;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct FoundationRenderSlot
    {
        public float4x4 Matrix;
        public float RotationAngle;
        public float NormalizedHealth;
        public uint PaletteId;
        public uint Active;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct FoundationAudioFxPacket
    {
        public float3 Position;
        public uint CueId;
        public uint EntityIndex;
        public uint EntityGeneration;
        public uint Revision;
        public uint StatusMask;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct FoundationCloudDelta
    {
        public ulong ChunkMortonKey;
        public ulong StableId;
        public uint LocalMicroMorton;
        public uint Revision;
        public uint Kind;
        public uint Flags;
        public ulong PayloadBits;
    }

    public readonly struct FoundationCapacities
    {
        public readonly int Chunks, Actors, Items, Instances, AudioFx, CloudDeltas;

        public FoundationCapacities(int chunks, int actors, int items, int instances, int audioFx, int cloudDeltas)
        {
            Chunks = chunks;
            Actors = actors;
            Items = items;
            Instances = instances;
            AudioFx = audioFx;
            CloudDeltas = cloudDeltas;
        }

        public static FoundationCapacities Default => new FoundationCapacities(64, 512, 256, 2048, 256, 256);

        internal bool IsValid => Chunks > 0 && Chunks <= 128 && Actors > 0 && Actors <= 2048 &&
            Items > 0 && Items <= 1024 && Instances > 0 && Instances <= 4096 &&
            AudioFx > 0 && AudioFx <= 1024 && CloudDeltas > 0 && CloudDeltas <= 1024;
    }

    public static class FoundationGridContract
    {
        public const int ParentChunkSide = 16;
        public const int OccupancyLeavesPerChunk = 64;
        public const int MicroSubdivisions = 4;
        public const float MinimumStructuralOverlap = 0.001f;
        public const ulong InvalidChunkKey = ulong.MaxValue;

        // Global CHUNK coordinates, not finest-cell coordinates. Local micro Morton remains a separate18-bit field.
        public static bool TryEncodeChunkMorton(int3 chunk, out ulong key)
        {
            const int bias = 1 << 20;
            if (math.any(chunk < -bias) || math.any(chunk >= bias))
            {
                key = InvalidChunkKey;
                return false;
            }
            uint3 biased = (uint3)(chunk + bias);
            key = Spread(biased.x) | (Spread(biased.y) << 1) | (Spread(biased.z) << 2);
            return true;
        }

        private static ulong Spread(uint value)
        {
            ulong v = value & 0x1fffffu;
            v = (v | (v << 32)) & 0x001f00000000ffffUL;
            v = (v | (v << 16)) & 0x001f0000ff0000ffUL;
            v = (v | (v << 8)) & 0x100f00f00f00f00fUL;
            v = (v | (v << 4)) & 0x10c30c30c30c30c3UL;
            return (v | (v << 2)) & 0x1249249249249249UL;
        }
    }

    public sealed class OracleFoundationBuffers : IDisposable
    {
        private NativeArray<FoundationChunkSlot> _chunks;
        private NativeArray<ulong> _occupancyLeaves;
        private NativeArray<FoundationActorSlot> _actors;
        private NativeArray<FoundationItemSlot> _items;
        private NativeArray<FoundationRenderSlot> _instances;
        private NativeArray<FoundationAudioFxPacket> _audioFx;
        private NativeArray<FoundationCloudDelta> _cloudDeltas;
        private JobHandle _readers;
        private bool _disposed;

        private OracleFoundationBuffers() { }

        public bool IsDisposed => _disposed;
        public NativeArray<FoundationChunkSlot>.ReadOnly Chunks { get { RequireAlive(); return _chunks.AsReadOnly(); } }
        public NativeArray<ulong>.ReadOnly OccupancyLeaves { get { RequireAlive(); return _occupancyLeaves.AsReadOnly(); } }
        public NativeArray<FoundationActorSlot>.ReadOnly Actors { get { RequireAlive(); return _actors.AsReadOnly(); } }
        public NativeArray<FoundationItemSlot>.ReadOnly Items { get { RequireAlive(); return _items.AsReadOnly(); } }
        public NativeArray<FoundationRenderSlot>.ReadOnly Instances { get { RequireAlive(); return _instances.AsReadOnly(); } }
        public NativeArray<FoundationAudioFxPacket>.ReadOnly AudioFx { get { RequireAlive(); return _audioFx.AsReadOnly(); } }
        public NativeArray<FoundationCloudDelta>.ReadOnly CloudDeltas { get { RequireAlive(); return _cloudDeltas.AsReadOnly(); } }

        // Main-thread cold startup only. On rollback failure the owner remains reachable for a disposal retry.
        public static bool TryCreate(FoundationCapacities capacities, out OracleFoundationBuffers owner, out Exception error)
        {
            owner = null;
            error = null;
            if (!capacities.IsValid)
            {
                error = new ArgumentOutOfRangeException(nameof(capacities), "Foundation capacities exceed fixed initialization limits.");
                return false;
            }
            owner = new OracleFoundationBuffers();
            try
            {
                owner.AllocateAndInitialize(capacities);
                return true;
            }
            catch (Exception initializationError)
            {
                error = initializationError;
                try { owner.Dispose(); owner = null; }
                catch (Exception rollbackError) { error = new AggregateException(initializationError, rollbackError); }
                return false;
            }
        }

        public void RegisterReader(JobHandle reader)
        {
            RequireAlive();
            _readers = JobHandle.CombineDependencies(_readers, reader);
        }

        private void AllocateAndInitialize(FoundationCapacities c)
        {
            _chunks = new NativeArray<FoundationChunkSlot>(c.Chunks, Allocator.Persistent);
            _occupancyLeaves = new NativeArray<ulong>(c.Chunks * FoundationGridContract.OccupancyLeavesPerChunk, Allocator.Persistent);
            _actors = new NativeArray<FoundationActorSlot>(c.Actors, Allocator.Persistent);
            _items = new NativeArray<FoundationItemSlot>(c.Items, Allocator.Persistent);
            _instances = new NativeArray<FoundationRenderSlot>(c.Instances, Allocator.Persistent);
            _audioFx = new NativeArray<FoundationAudioFxPacket>(c.AudioFx, Allocator.Persistent);
            _cloudDeltas = new NativeArray<FoundationCloudDelta>(c.CloudDeltas, Allocator.Persistent);
            _readers = new InitializeJob { Chunks = _chunks, Actors = _actors, Items = _items, Instances = _instances, CloudDeltas = _cloudDeltas }.Schedule();
            _readers.Complete();
        }

        private void RequireAlive()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(OracleFoundationBuffers));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _readers.Complete();
            if (_cloudDeltas.IsCreated) _cloudDeltas.Dispose();
            if (_audioFx.IsCreated) _audioFx.Dispose();
            if (_instances.IsCreated) _instances.Dispose();
            if (_items.IsCreated) _items.Dispose();
            if (_actors.IsCreated) _actors.Dispose();
            if (_occupancyLeaves.IsCreated) _occupancyLeaves.Dispose();
            if (_chunks.IsCreated) _chunks.Dispose();
            _disposed = true;
        }

        [BurstCompile]
        private struct InitializeJob : IJob
        {
            public NativeArray<FoundationChunkSlot> Chunks;
            public NativeArray<FoundationActorSlot> Actors;
            public NativeArray<FoundationItemSlot> Items;
            public NativeArray<FoundationRenderSlot> Instances;
            public NativeArray<FoundationCloudDelta> CloudDeltas;

            public void Execute()
            {
                for (int i = 0; i < Chunks.Length; i++)
                    Chunks[i] = new FoundationChunkSlot { MortonKey = FoundationGridContract.InvalidChunkKey,
                        LeafStart = (uint)(i * FoundationGridContract.OccupancyLeavesPerChunk) };
                for (int i = 0; i < Actors.Length; i++)
                    Actors[i] = new FoundationActorSlot { Rotation = quaternion.identity, Generation = 1, NormalizedHealth = 1f };
                for (int i = 0; i < Items.Length; i++)
                    Items[i] = new FoundationItemSlot { OwnerIndex = uint.MaxValue, Slot = ushort.MaxValue };
                for (int i = 0; i < Instances.Length; i++)
                    Instances[i] = new FoundationRenderSlot { Matrix = float4x4.identity, NormalizedHealth = 1f };
                for (int i = 0; i < CloudDeltas.Length; i++)
                    CloudDeltas[i] = new FoundationCloudDelta { ChunkMortonKey = FoundationGridContract.InvalidChunkKey };
            }
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Unity metres/Y-up; global signed21-bit chunk Morton keys plus separate local18-bit micro indices.
// Array Formats: Fixed24/64/32/80/32/40-byte chunk/actor/item/render/audio/cloud records;64 ulong occupancy leaves per16^3 chunk.
// Dependencies/Pointers: Cold private Persistent owner, pure Burst initialization job, borrowed readonly views and registered reader fences.
// Geometry Law: Future map writers must validate neighbor halos/duplicates/holes, consolidate or overlap walls>=0.001, and anchor props to exact support vertices.
// Scope: No active loops, gameplay binding, world generation, placement, GPU/audio dispatch or edge calls; existing saves/assets/owners are unchanged.
