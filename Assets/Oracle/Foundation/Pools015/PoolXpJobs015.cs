using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace OdysseyOracle.Foundation.Pools015
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct MonsterEnvelope48
    {
        public float3 Position;
        public float Radius;
        public float3 DesiredStep;
        public uint Active;
        public uint StableId, Generation;
        public float MinimumY, MaximumY; // Complete step/hover envelope, supplied by owner.
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct XpMergePair16 { public int A, B, RunEpoch, RewardGeneration; }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct XpMergeClearance8
    {
        public uint Status; //1 CLEAR only.0 unsupported/unloaded,2 blocked,3 stale/invalid.
        public uint WorldGeneration;
    }

    [BurstCompile]
    public struct AcquireActorSlot015 : IJob
    {
        public NativeArray<PoolSlot16> Slots;
        public NativeArray<PoolHandle8> Result; // Exactly1; invalid sentinel slot=-1.
        public int StableId;
        public void Execute()
        {
            if (!Result.IsCreated || Result.Length != 1) return;
            Result[0] = new PoolHandle8 { Slot = -1 };
            if (!Slots.IsCreated || Slots.Length != PoolXpMath015.ActorCapacity || StableId < 1) return;
            for (int i = 0; i < Slots.Length; i++)
                if (Slots[i].State != 0u && Slots[i].StableId == StableId) return;
            for (int i = 0; i < Slots.Length; i++)
            {
                PoolSlot16 slot = Slots[i];
                if (!PoolXpMath015.TryActivate(ref slot, StableId)) continue;
                Slots[i] = slot; Result[0] = new PoolHandle8 { Slot = i, Generation = slot.Generation }; return;
            }
        }
    }

    // Complete immutable actor snapshot; no containers borrowed from existing SpatialTargetGrid.
    [BurstCompile]
    public struct BuildMonsterDistanceGrid015 : IJob
    {
        [ReadOnly] public NativeArray<MonsterEnvelope48> Actors;
        public NativeParallelMultiHashMap<ulong, int> Cells;
        public NativeArray<int> Status; //1 valid,0 invalid/unloaded.
        public int Count;
        public void Execute()
        {
            if (!Status.IsCreated || Status.Length != 1) return;
            Status[0] = 0;
            if (!Cells.IsCreated || !Actors.IsCreated) return;
            Cells.Clear();
            if (Count < 0 || Count > PoolXpMath015.ActorCapacity || Count > Actors.Length || Cells.Capacity < Count) return;
            uint lastId = 0u;
            for (int i = 0; i < Count; i++)
            {
                MonsterEnvelope48 a = Actors[i];
                if (a.Active == 0u) continue;
                if (a.Active != 1u || a.StableId <= lastId || a.Generation == 0u ||
                    !PoolXpMath015.Position(a.Position.x, a.Position.y, a.Position.z) ||
                    !math.all(math.isfinite(a.DesiredStep)) || a.DesiredStep.y != 0f ||
                    math.lengthsq(a.DesiredStep) > PoolXpMath015.MaximumStep * PoolXpMath015.MaximumStep ||
                    !math.isfinite(a.Radius) || a.Radius <= 0f || a.Radius > PoolXpMath015.MaximumActorRadius ||
                    !math.isfinite(a.MinimumY) || !math.isfinite(a.MaximumY) || a.MinimumY > a.MaximumY ||
                    !PoolXpMath015.TryMortonCell(a.Position.x, 0f, a.Position.z, 4f, out ulong key)) return;
                lastId = a.StableId; Cells.Add(key, i);
            }
            Status[0] = 1;
        }
    }

    // One bounded sequential Burst solve yields a deterministic accepted trajectory per stable ID.
    // This replaces actor spacing only. World/tower/floor clearance remains a separate prerequisite.
    [BurstCompile]
    public struct ResolveMonsterDistances015 : IJob
    {
        [ReadOnly] public NativeArray<MonsterEnvelope48> Actors;
        [ReadOnly] public NativeParallelMultiHashMap<ulong, int> Cells;
        [ReadOnly] public NativeArray<int> GridStatus;
        public NativeArray<float3> ResolvedSteps;
        public NativeArray<int> NeighborScratch; //48, reused by this serialized job only.
        public NativeArray<int> Status; //1 solved;2 preexisting overlap/invalid trajectory.0 invalid snapshot.
        public int Count;
        public void Execute()
        {
            if (!Status.IsCreated || Status.Length != 1) return;
            Status[0] = 0;
            if (!ResolvedSteps.IsCreated) return;
            for (int i = 0; i < ResolvedSteps.Length; i++) ResolvedSteps[i] = float3.zero;
            if (!Actors.IsCreated || !Cells.IsCreated || !GridStatus.IsCreated || GridStatus.Length != 1 ||
                !NeighborScratch.IsCreated || GridStatus[0] != 1 || Count < 0 || Count > 48 || Count > Actors.Length ||
                ResolvedSteps.Length < Count || NeighborScratch.Length < 48) return;
            for (int i = 0; i < Count; i++)
            {
                MonsterEnvelope48 a = Actors[i]; if (a.Active != 1u) continue;
                int count = 0, cx = (int)math.floor(a.Position.x / 4f), cz = (int)math.floor(a.Position.z / 4f);
                //2-cell stencil covers both max2m radii +.04m skin +both.6m displacements.
                for (int x = cx - 2; x <= cx + 2; x++) for (int z = cz - 2; z <= cz + 2; z++)
                {
                    if (!PoolXpMath015.TryMortonCoordinates(x, 0, z, out ulong key) ||
                        !Cells.TryGetFirstValue(key, out int other, out var iterator)) continue;
                    do
                    {
                        if (other == i || other < 0 || other >= Count) continue;
                        MonsterEnvelope48 b = Actors[other];
                        if (a.MaximumY < b.MinimumY || b.MaximumY < a.MinimumY) continue;
                        if (count >= 48) return; // A complete single-entry-per-actor grid cannot reach this.
                        NeighborScratch[count++] = other;
                    } while (Cells.TryGetNextValue(out other, ref iterator));
                }
                if (!AllClear(i, count, 0f)) { Status[0] = 2; return; }
                float safe = 1f;
                if (!AllClear(i, count, 1f))
                {
                    float blocked = 1f; safe = 0f;
                    // Predicate need not be globally monotone: safe endpoint is rechecked every iteration.
                    for (int step = 0; step < 16; step++)
                    {
                        float candidate = (safe + blocked) * .5f;
                        if (AllClear(i, count, candidate)) safe = candidate; else blocked = candidate;
                    }
                }
                ResolvedSteps[i] = a.DesiredStep * safe;
            }
            Status[0] = 1;
        }
        bool AllClear(int index, int neighborCount, float fraction)
        {
            MonsterEnvelope48 a = Actors[index]; float3 step = a.DesiredStep * fraction;
            for (int n = 0; n < neighborCount; n++)
            {
                int other = NeighborScratch[n]; MonsterEnvelope48 b = Actors[other];
                float3 otherStep = other < index ? ResolvedSteps[other] : float3.zero;
                if (!PoolXpMath015.SweptDiscsClear(a.Position.x, a.Position.z, step.x, step.z,
                    b.Position.x, b.Position.z, otherStep.x, otherStep.z, a.Radius + b.Radius + .04f)) return false;
            }
            return true;
        }
    }

    // At most one proposed earlier-neighbor pair/source; no runtime arrays or quadratic duplicate scan.
    [BurstCompile]
    public struct BuildXpMergePairs015 : IJob
    {
        [ReadOnly] public NativeArray<XpSource64> Sources;
        public NativeParallelMultiHashMap<ulong, int> Cells;
        public NativeArray<XpMergePair16> Pairs; //4095.
        public NativeArray<int> PairCount; //1; -1 invalid.
        public int MaximumBaseXp;
        public void Execute()
        {
            if (!PairCount.IsCreated || PairCount.Length != 1) return;
            PairCount[0] = -1;
            if (!Cells.IsCreated || !Sources.IsCreated || !Pairs.IsCreated) return;
            Cells.Clear();
            if (Sources.Length != 4096 || Cells.Capacity < 4096 || Pairs.Length < 4095) return;
            int count = 0;
            for (int i = 0; i < Sources.Length; i++)
            {
                XpSource64 a = Sources[i]; if (a.Flags == 0u) continue;
                if (a.SourceId != (uint)(i + 1) || !PoolXpMath015.SourceValid(a, MaximumBaseXp)) return;
                if ((a.EligibleMask & ~a.ClaimedMask) == 0u) continue;
                int nearest = -1, cx = (int)math.floor(a.X / 2f), cy = (int)math.floor(a.Y / 2f), cz = (int)math.floor(a.Z / 2f);
                double best = 4d;
                for (int x = cx - 1; x <= cx + 1; x++) for (int y = cy - 1; y <= cy + 1; y++) for (int z = cz - 1; z <= cz + 1; z++)
                {
                    if (!PoolXpMath015.TryMortonCoordinates(x, y, z, out ulong key) ||
                        !Cells.TryGetFirstValue(key, out int candidate, out var iterator)) continue;
                    int budget = 0;
                    do
                    {
                        if (++budget > 32) break; //Presentation-only missed merge leaves every ticket/instance intact.
                        XpSource64 b = Sources[candidate];
                        if (a.RunEpoch != b.RunEpoch || a.RewardGeneration != b.RewardGeneration ||
                            a.EligibleMask != b.EligibleMask || a.ClaimedMask != b.ClaimedMask || a.Owner0 != b.Owner0 || a.Owner1 != b.Owner1) continue;
                        double dx = (double)a.X - b.X, dy = (double)a.Y - b.Y, dz = (double)a.Z - b.Z;
                        double d = dx * dx + dy * dy + dz * dz;
                        if (d > best || d == best && nearest >= 0 && candidate > nearest) continue;
                        best = d; nearest = candidate;
                    } while (Cells.TryGetNextValue(out candidate, ref iterator));
                }
                if (nearest >= 0) Pairs[count++] = new XpMergePair16 { A = nearest, B = i, RunEpoch = a.RunEpoch, RewardGeneration = a.RewardGeneration };
                PoolXpMath015.TryMortonCell(a.X, a.Y, a.Z, 2f, out ulong ownKey); Cells.Add(ownKey, i);
            }
            PairCount[0] = count;
        }
    }

    [BurstCompile]
    public struct MergeXpPresentation015 : IJob
    {
        [ReadOnly] public NativeArray<XpSource64> Sources;
        [ReadOnly] public NativeArray<XpMergePair16> Pairs;
        [ReadOnly] public NativeArray<XpMergeClearance8> Clearance;
        [ReadOnly] public NativeArray<int> PairCount;
        public NativeArray<XpCluster64> Clusters; //4096.
        public NativeArray<int> Parent, Next; //4096 each; intrusive source lists are presentation only.
        public NativeArray<int> Status; //1 complete.0 invalid. No clear lease still yields unmerged views.
        public uint WorldGeneration, SourceRevision, ClearanceSourceRevision;
        public int MaximumBaseXp;
        public void Execute()
        {
            if (!Status.IsCreated || Status.Length != 1) return;
            Status[0] = 0;
            if (!Sources.IsCreated || !Clusters.IsCreated || !Parent.IsCreated || !Next.IsCreated || !Pairs.IsCreated ||
                !Clearance.IsCreated || !PairCount.IsCreated || PairCount.Length != 1 ||
                Sources.Length != 4096 || Clusters.Length != 4096 || Parent.Length != 4096 || Next.Length != 4096) return;
            for (int i = 0; i < 4096; i++)
            {
                Clusters[i] = default; Parent[i] = Next[i] = -1;
                XpSource64 source = Sources[i]; if (source.Flags == 0u) continue;
                if (source.SourceId != (uint)(i + 1) || !PoolXpMath015.SourceValid(source, MaximumBaseXp)) return;
                if ((source.EligibleMask & ~source.ClaimedMask) == 0u) continue;
                Clusters[i] = PoolXpMath015.ClusterFor(source, i); Parent[i] = i;
            }
            int count = PairCount[0];
            if (count < 0 || count > 4095 || count > Pairs.Length || count > Clearance.Length) return;
            if (WorldGeneration != 0u && SourceRevision != 0u && SourceRevision == ClearanceSourceRevision)
            for (int p = 0; p < count; p++)
            {
                XpMergePair16 pair = Pairs[p]; XpMergeClearance8 clear = Clearance[p];
                if (clear.Status != 1u || clear.WorldGeneration != WorldGeneration || pair.A < 0 || pair.B < 0 || pair.A >= 4096 || pair.B >= 4096) continue;
                int a = Find(pair.A), b = Find(pair.B); if (a < 0 || b < 0 || a == b) continue;
                XpCluster64 ca = Clusters[a], cb = Clusters[b];
                //Clearance was for original pair endpoints; do not reuse it for a different merged anchor.
                if (ca.FirstSourceId != Sources[pair.A].SourceId || cb.FirstSourceId != Sources[pair.B].SourceId ||
                    Sources[pair.A].Owner0 != Sources[pair.B].Owner0 || Sources[pair.A].Owner1 != Sources[pair.B].Owner1 ||
                    pair.RunEpoch != ca.RunEpoch || pair.RewardGeneration != ca.RewardGeneration ||
                    !PoolXpMath015.TryMerge(ca, cb, true, out XpCluster64 merged)) continue;
                int root = ca.FirstSourceId < cb.FirstSourceId ? a : b, retired = root == a ? b : a;
                XpCluster64 first = Clusters[root], last = Clusters[retired];
                Next[first.Tail] = last.Head; merged.Head = first.Head; merged.Tail = last.Tail;
                Clusters[root] = merged; Clusters[retired] = default; Parent[retired] = root;
            }
            Status[0] = 1;
        }
        int Find(int index)
        {
            int root = index;
            for (int n = 0; n < 4096; n++)
            {
                if (Parent[root] < 0) return -1;
                if (Parent[root] == root) { Parent[index] = root; return root; }
                root = Parent[root]; if (root < 0 || root >= 4096) return -1;
            }
            return -1;
        }
    }

    [BurstCompile]
    public struct BuildXpOrbMatrices015 : IJob
    {
        [ReadOnly] public NativeArray<XpCluster64> Clusters;
        [ReadOnly] public NativeArray<XpSource64> Sources;
        [ReadOnly] public NativeArray<int> ClusterStatus;
        public NativeArray<float4x4> Matrices; //4096 once; cold managed staging arrays belong to renderer.
        public NativeArray<uint> ClusterIds; //4096.
        public NativeArray<int> Count; //1;0 on invalid.
        public float WrappedSeconds; //Owner publishes finite periodic presentation clock,0..120.
        public uint RecipientBit; //1 or2; identity membership checked by owner before publishing this view.
        public ulong AuthenticatedRecipientId;
        public void Execute()
        {
            if (!Count.IsCreated || Count.Length != 1) return;
            Count[0] = 0;
            if (!ClusterStatus.IsCreated || ClusterStatus.Length != 1 || !Clusters.IsCreated || !Sources.IsCreated ||
                !Matrices.IsCreated || !ClusterIds.IsCreated || ClusterStatus[0] != 1 || Sources.Length != 4096 ||
                Clusters.Length != 4096 || Matrices.Length < 4096 || ClusterIds.Length < 4096 ||
                (RecipientBit != 1u && RecipientBit != 2u) || !math.isfinite(WrappedSeconds) || WrappedSeconds < 0f || WrappedSeconds > 120f) return;
            int count = 0;
            for (int i = 0; i < 4096; i++)
            {
                XpCluster64 c = Clusters[i]; if (c.Active != 1u || (c.PendingMask & RecipientBit) == 0u) continue;
                if (c.Head < 0 || c.Head >= 4096) { Count[0] = 0; return; }
                XpSource64 source = Sources[c.Head];
                if ((RecipientBit == 1u ? source.Owner0 : source.Owner1) != AuthenticatedRecipientId) continue;
                float phase = WrappedSeconds * math.PI / 3f + c.FirstSourceId * .17f;
                float size = math.min(2.5f, math.pow((float)c.BaseXpSum, 1f / 3f));
                float3 position = new float3(c.X, c.Y + .55f + math.sin(phase) * .065f, c.Z);
                Matrices[count] = float4x4.TRS(position, quaternion.EulerXYZ(.61f, phase, .35f), new float3(.17f, .23f, .17f) * size);
                ClusterIds[count++] = c.FirstSourceId;
            }
            Count[0] = count;
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing world metres/Y-up; complete actor swept XZ envelopes; signed Morton63 keys; original source anchors.
// Array Formats:48 actor envelopes48B;fixed4096source64B/cluster64B/matrix64B;4095pairs16B/clearances8B;native maps preallocated to48/4096.
// Dependencies/Pointers: Root-owned cold allocation/fences/renderer/object adapters; numeric World supplies matched merge-clearance lease; no runtime auto-bind, engine object, HP/XP/forge mutation or native acceptance claim.
