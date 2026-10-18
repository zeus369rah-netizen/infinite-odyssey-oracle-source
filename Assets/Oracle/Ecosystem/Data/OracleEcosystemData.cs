using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace OdysseyOracle.Ecosystem
{
    public enum AmbientElementType : uint { Fire = 0, Nature = 1, Wind = 2, Earth = 3 }
    public enum RaykonState : uint { IdleSpin = 0, TalkSlam = 1 }

    [Flags]
    public enum ElementStatus : uint { None = 0, Burning = 1, Freezing = 2, Staggered = 4, Pushed = 8 }

    [Flags]
    public enum NpcTrigger : uint { None = 0, StopIdleSpin = 1, StartTalkSlam = 2, StaffImpact = 4, OpenDialogue = 8 }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct SubVoxelData
    {
        public uint ChildIndex; // Six-bit Morton child index,0..63; parent key is a global coarse-cell int3.
        public ushort MaterialId; // Existing palette compatibility: 0..255. Never truncate higher IDs.
        public byte Density; // 0=air,255=solid; missing record inherits parent, explicit0 overrides it.
        public byte Flags; // Bit0=explicit override; higher bits reserved0. Unique(parent,ChildIndex).
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct ChunkOccupancyData
    {
        public ulong MortonKey; // Unbound=ulong.MaxValue; signed21-bit/axis chunk coordinates use63bits.
        public ulong OccupiedLeaves; // Bit i is1 iff leaf mask i contains any occupied cell.
        public uint LeafStart; //64ulong masks for a16^3 chunk; leaf=localMorton>>6,bit=localMorton&63.
        public uint EditedCount; //Only edited deltas may be persisted; base occupancy is a transient cache.
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct TreeInstance
    {
        public float4x4 LocalToWorld;
        public float RotationAngle; // Radians; scale3 is already in LocalToWorld.
        public float NormalizedHealth;
        public float BaseIntensity;
        public float CrackFrequency;
        public uint FoliageStart;
        public uint FoliageCount;
        public uint FoliageLayerCount;
        public uint StatusMask;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct FoliageOffset
    {
        public float3 LocalPosition; // Apply the tree matrix once; do not multiply by3 again.
        public uint Layer;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct DragonflyPoint
    {
        public float3 Position; // Immutable anchor in world metres; future GPU motion is stateless.
        public uint ElementType; // Ambient ID0..3, NOT the combat-element enum or a onehot mask.
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct NpcStateData
    {
        public uint EntityIndex;
        public uint EntityGeneration;
        public uint StaffVisualId;
        public uint CauldronVisualId;
        public float4x4 LocalToWorld;
        public float RotationAngle;
        public float NormalizedHealth;
        public float AnimationPhase;
        public float StaffSlamProgress;
        public uint State; //0=IdleSpin,1=TalkSlam. No transition behavior runs in this data setup.
        public uint StateRevision;
        public uint PendingTriggers;
        public uint StatusMask;
        public uint AudioCueId;
        public uint DialogId;
        public uint LastAudioRevision;
        public uint LastDialogRevision;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct NpcTriggerPacket
    {
        public uint EntityIndex;
        public uint EntityGeneration;
        public uint StateRevision;
        public uint Sequence;
        public uint TriggerMask;
        public uint AudioCueId;
        public uint DialogId;
        public uint Reserved;
    }

    // Borrowed read-only handles: never dispose/retain past the storage owner's registered fence.
    // This view is not a particle payload and must not be uploaded as a raw GPU struct.
    public readonly struct DragonflyBuffers
    {
        public readonly NativeArray<float3>.ReadOnly Positions;
        public readonly NativeArray<uint>.ReadOnly ElementTypes;
        public readonly NativeArray<uint>.ReadOnly ElementMasks;
        public readonly NativeArray<DragonflyPoint>.ReadOnly GpuSeeds;
        public readonly NativeArray<float4>.ReadOnly ElementColors;
        internal DragonflyBuffers(NativeArray<float3> positions, NativeArray<uint> types,
            NativeArray<uint> masks, NativeArray<DragonflyPoint> seeds, NativeArray<float4> colors)
        {
            Positions = positions.AsReadOnly(); ElementTypes = types.AsReadOnly();
            ElementMasks = masks.AsReadOnly(); GpuSeeds = seeds.AsReadOnly(); ElementColors = colors.AsReadOnly();
        }
    }

    // Cold array/resource initialization only. Sealed ownership prevents native-handle owner copies.
    // No MonoBehaviour, active frame loop, asset generation, dispatch, dialogue or audio playback.
    public sealed class EcosystemModelArrays : IDisposable
    {
        public const int Subdivisions = 4;
        public const int ChildrenPerParent = 64;
        public const int FoliageLayers = 3;
        public const int FoliageGridEdge = 4;
        public const int FoliagePerTree = 48;
        public const int TriggerCapacity = 64;
        public const int ChunkCapacity = 16;
        public const int OccupancyLeavesPerChunk = 64;
        NativeParallelMultiHashMap<int3, SubVoxelData> subVoxels;
        NativeArray<ChunkOccupancyData> chunks;
        NativeArray<ulong> occupancyLeaves;
        NativeArray<TreeInstance> trees;
        NativeArray<FoliageOffset> foliage;
        NativeArray<float3> positions;
        NativeArray<uint> elementTypes, elementMasks;
        NativeArray<DragonflyPoint> gpuSeeds;
        NativeArray<float4> colors;
        NativeArray<NpcStateData> npcs;
        NativeArray<NpcTriggerPacket> triggers;
        NativeArray<uint> triggerCounters; //0=head,1=tail; single writer, no overwrite on full.
        GraphicsBuffer seedBuffer, colorBuffer; //Private main-thread resources; never passed to jobs.
        JobHandle readers;
        bool disposed;

        public int ActiveTreeCount => 0; //Authoring/actor binding is deliberately outside this setup.
        public int ActiveNpcCount => 0;
        public int ActiveChunkCount => 0; //Empty unbound cache slots are not generated/empty world chunks.
        public bool HasGpuBuffers => !disposed && seedBuffer != null && colorBuffer != null;
        public NativeParallelMultiHashMap<int3, SubVoxelData>.ReadOnly SubVoxels => subVoxels.AsReadOnly();
        public NativeArray<TreeInstance>.ReadOnly Trees => trees.AsReadOnly();
        public NativeArray<FoliageOffset>.ReadOnly Foliage => foliage.AsReadOnly();
        public NativeArray<NpcStateData>.ReadOnly Npcs => npcs.AsReadOnly();
        public NativeArray<NpcTriggerPacket>.ReadOnly Triggers => triggers.AsReadOnly();
        public NativeArray<uint>.ReadOnly TriggerCounters => triggerCounters.AsReadOnly();
        public NativeArray<ChunkOccupancyData>.ReadOnly Chunks => chunks.AsReadOnly();
        public NativeArray<ulong>.ReadOnly OccupancyLeaves => occupancyLeaves.AsReadOnly();
        public DragonflyBuffers Dragonflies => new DragonflyBuffers(positions, elementTypes, elementMasks, gpuSeeds, colors);

        EcosystemModelArrays() { }

        // Cold factory: a failed cleanup returns its live owner so the caller can retry Dispose.
        // On false, inspect error and retain any non-null owner; never use it for gameplay.
        public static bool TryCreate(out EcosystemModelArrays owner, out Exception error,
            int subVoxelCapacity = 32768, int treeCapacity = 16,
            int dragonflyCount = 1024, int npcCapacity = 1, uint seed = 1, bool createGpuBuffers = false)
        {
            owner = new EcosystemModelArrays();
            try
            {
                owner.Initialize(subVoxelCapacity, treeCapacity, dragonflyCount, npcCapacity, seed, createGpuBuffers);
                error = null;
                return true;
            }
            catch (Exception initializationError)
            {
                try
                {
                    owner.Dispose();
                    owner = null;
                    error = initializationError;
                }
                catch (Exception cleanupError)
                {
                    error = new AggregateException(initializationError, cleanupError);
                }
                return false;
            }
        }

        void Initialize(int subVoxelCapacity, int treeCapacity, int dragonflyCount,
            int npcCapacity, uint seed, bool createGpuBuffers)
        {
            if (subVoxelCapacity < 1 || subVoxelCapacity > 32768 || treeCapacity < 1 || treeCapacity > 16 ||
                dragonflyCount < 1 || dragonflyCount > 1024 || npcCapacity != 1)
                throw new ArgumentOutOfRangeException(null, "Capacities must fit the fixed initialization budgets.");
            if (UnsafeUtility.SizeOf<SubVoxelData>() != 8 || UnsafeUtility.SizeOf<TreeInstance>() != 96 ||
                UnsafeUtility.SizeOf<FoliageOffset>() != 16 || UnsafeUtility.SizeOf<DragonflyPoint>() != 16 ||
                UnsafeUtility.SizeOf<NpcStateData>() != 128 || UnsafeUtility.SizeOf<NpcTriggerPacket>() != 32 ||
                UnsafeUtility.SizeOf<ChunkOccupancyData>() != 24)
                throw new InvalidOperationException("Unexpected native/GPU data layout.");
            {
                subVoxels = new NativeParallelMultiHashMap<int3, SubVoxelData>(subVoxelCapacity, Allocator.Persistent);
                chunks = new NativeArray<ChunkOccupancyData>(ChunkCapacity, Allocator.Persistent);
                occupancyLeaves = new NativeArray<ulong>(ChunkCapacity * OccupancyLeavesPerChunk, Allocator.Persistent);
                trees = new NativeArray<TreeInstance>(treeCapacity, Allocator.Persistent);
                foliage = new NativeArray<FoliageOffset>(treeCapacity * FoliagePerTree, Allocator.Persistent);
                positions = new NativeArray<float3>(dragonflyCount, Allocator.Persistent);
                elementTypes = new NativeArray<uint>(dragonflyCount, Allocator.Persistent);
                elementMasks = new NativeArray<uint>(dragonflyCount, Allocator.Persistent);
                gpuSeeds = new NativeArray<DragonflyPoint>(dragonflyCount, Allocator.Persistent);
                colors = new NativeArray<float4>(4, Allocator.Persistent);
                npcs = new NativeArray<NpcStateData>(npcCapacity, Allocator.Persistent);
                triggers = new NativeArray<NpcTriggerPacket>(TriggerCapacity, Allocator.Persistent);
                triggerCounters = new NativeArray<uint>(2, Allocator.Persistent);
                readers = new InitializeArrays
                {
                    Trees = trees, Foliage = foliage, Positions = positions, Types = elementTypes,
                    Masks = elementMasks, Seeds = gpuSeeds, Colors = colors, Npcs = npcs, Chunks = chunks, Seed = seed
                }.Schedule();
                readers.Complete(); readers = default;
                if (createGpuBuffers)
                {
                    if (!SystemInfo.supportsComputeShaders)
                        throw new NotSupportedException("This optional swarm path needs compute support; disable it on WebGL.");
                    seedBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, dragonflyCount, 16);
                    colorBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 4, 16);
                    seedBuffer.SetData(gpuSeeds); colorBuffer.SetData(colors);
                }
            }
        }

        public void RegisterReader(JobHandle handle)
        {
            if (disposed) throw new ObjectDisposedException(nameof(EcosystemModelArrays));
            readers = JobHandle.CombineDependencies(readers, handle);
        }

        public void Dispose()
        {
            if (disposed) return;
            readers.Complete(); //On failure retain every allocation for an explicit retry.
            seedBuffer?.Dispose(); seedBuffer = null;
            colorBuffer?.Dispose(); colorBuffer = null;
            if (subVoxels.IsCreated) subVoxels.Dispose();
            if (chunks.IsCreated) chunks.Dispose();
            if (occupancyLeaves.IsCreated) occupancyLeaves.Dispose();
            if (trees.IsCreated) trees.Dispose();
            if (foliage.IsCreated) foliage.Dispose();
            if (positions.IsCreated) positions.Dispose();
            if (elementTypes.IsCreated) elementTypes.Dispose();
            if (elementMasks.IsCreated) elementMasks.Dispose();
            if (gpuSeeds.IsCreated) gpuSeeds.Dispose();
            if (colors.IsCreated) colors.Dispose();
            if (npcs.IsCreated) npcs.Dispose();
            if (triggers.IsCreated) triggers.Dispose();
            if (triggerCounters.IsCreated) triggerCounters.Dispose();
            disposed = true;
        }

        [BurstCompile]
        struct InitializeArrays : IJob
        {
            public NativeArray<TreeInstance> Trees;
            public NativeArray<FoliageOffset> Foliage;
            public NativeArray<float3> Positions;
            public NativeArray<uint> Types, Masks;
            public NativeArray<DragonflyPoint> Seeds;
            public NativeArray<float4> Colors;
            public NativeArray<NpcStateData> Npcs;
            public NativeArray<ChunkOccupancyData> Chunks;
            public uint Seed;
            public void Execute()
            {
                for (int i = 0; i < Chunks.Length; i++)
                    Chunks[i] = new ChunkOccupancyData
                    {
                        MortonKey = ulong.MaxValue, LeafStart = (uint)(i * OccupancyLeavesPerChunk)
                    };
                for (int i = 0; i < Trees.Length; i++)
                    Trees[i] = new TreeInstance
                    {
                        LocalToWorld = float4x4.TRS(float3.zero, quaternion.identity, new float3(3f)),
                        NormalizedHealth = 1f, BaseIntensity = 1f, CrackFrequency = 32f,
                        FoliageStart = (uint)(i * FoliagePerTree), FoliageCount = FoliagePerTree,
                        FoliageLayerCount = FoliageLayers
                    };
                for (int i = 0; i < Foliage.Length; i++)
                {
                    int cell = i % FoliagePerTree, layer = cell / 16, local = cell % 16;
                    Foliage[i] = new FoliageOffset
                    {
                        LocalPosition = new float3((local % 4 - 1.5f) * .125f,
                            1f + layer * .25f, (local / 4 - 1.5f) * .125f), Layer = (uint)layer
                    };
                }
                Colors[0] = new float4(1f, .03f, .01f, 1f);
                Colors[1] = new float4(.1f, 1f, .2f, 1f);
                Colors[2] = new float4(1f);
                Colors[3] = new float4(.28f, .12f, .04f, 1f);
                for (int i = 0; i < Positions.Length; i++)
                {
                    uint hash = math.hash(new uint2(Seed, (uint)i)), type = (uint)i & 3u;
                    float angle = 2f * math.PI * i / Positions.Length;
                    float radius = 2f + (hash & 1023u) * (3f / 1023f);
                    float3 point = new float3(math.cos(angle) * radius,
                        1f + ((hash >> 10) & 1023u) * (2f / 1023f), math.sin(angle) * radius);
                    Positions[i] = point; Types[i] = type; Masks[i] = 1u << (int)type;
                    Seeds[i] = new DragonflyPoint { Position = point, ElementType = type };
                }
                for (int i = 0; i < Npcs.Length; i++)
                    Npcs[i] = new NpcStateData
                    {
                        EntityIndex = uint.MaxValue, StaffVisualId = uint.MaxValue,
                        CauldronVisualId = uint.MaxValue, LocalToWorld = float4x4.identity,
                        NormalizedHealth = 1f, State = (uint)RaykonState.IdleSpin,
                        AudioCueId = uint.MaxValue, DialogId = uint.MaxValue
                    };
            }
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Unity world metres,+Y up,+Z forward; foliage is tree-local; angles radians; tree matrix contains uniform3.0scale once. New child indices use six-bit Morton order; existing world/save layout is outside this module.
// Array Formats: Fixed8/96/16/16/128/32/24-byte SubVoxel/Tree/Foliage/Dragonfly/NPC/trigger/chunk records; persistent NativeArray slabs and bounded compatibility NativeParallelMultiHashMap<int3,SubVoxelData>. Each16^3chunk cache has64ulong leaf masks plus one summary. Swarm IDs0..3 and onehot masks are distinct.
// Dependencies/Pointers: One sealed main-thread owner; pure Burst initialization; readonly views require reader fences. Chunk slots are unbound transient caches, no base matrices/deltas are persisted. Morton encoding/adapters, occupancy maintenance, greedy meshing, stateless generation, runtime dispatch/state/audio and gameplay wiring are outside this raw initialization scope. Future spatial consumers must use Morton indices before compatibility-map queries. GPU buffers remain private/no dispatch; no raw pointers, GameObject creation or frame loops.
