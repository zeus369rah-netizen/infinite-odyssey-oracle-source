using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using OdysseyOracle.Foundation.Pools015;

namespace OdysseyOracle
{
    public sealed partial class CoopDefenseDirector
    {
        NativeParallelMultiHashMap<ulong,int> xpCells015;
        NativeArray<XpMergePair16> xpPairs015;
        NativeArray<XpMergeClearance8> xpClearance015;
        NativeArray<XpCluster64> xpClusters015;
        NativeArray<int> xpParents015,xpNext015,xpPairCount015,xpClusterStatus015,xpMatrixCount015,xpNearest015;
        NativeArray<float> xpReadyAt015,xpRetryAt015;
        NativeArray<float4x4> xpNumericMatrices015;
        NativeArray<Matrix4x4> xpUnityMatrices015;
        NativeArray<uint> xpClusterIds015;
        Matrix4x4[] xpBatch0015,xpBatch1015,xpBatch2015,xpBatch3015,xpBatch4015;
        Mesh xpMesh015; Material xpMaterial015;
        uint xpBuiltRevision015,xpBuiltWorld015;
        DungeonWorld xpBuiltWorldOwner015;
        bool xpHasMergePairs015;
        float xpPresentationSeconds015,xpNextScan015;
        public int XpVisibleInstances015 { get; private set; }
        public int XpMergeCandidatePairs015 { get; private set; }
        public ulong XpGeometryLeaseCaptures015 { get; private set; }
        public bool XpMergeClearanceAvailable015 => xpBuiltWorld015!=0&&owner.World!=null&&owner.World==xpBuiltWorldOwner015&&owner.World.XpClearanceGeneration015==xpBuiltWorld015;
        uint CaptureXpMergeGeometry015()
        {
            if(owner.World==null)return 0;
            if(XpGeometryLeaseCaptures015!=ulong.MaxValue)XpGeometryLeaseCaptures015++;
            return owner.World.CaptureXpClearance015();
        }
        void InitializeXpPresentation015()
        {
            const int n=PoolXpMath015.SourceCapacity;
            xpCells015=new NativeParallelMultiHashMap<ulong,int>(n,Allocator.Persistent);
            xpPairs015=new NativeArray<XpMergePair16>(n-1,Allocator.Persistent);
            xpClearance015=new NativeArray<XpMergeClearance8>(n-1,Allocator.Persistent); // Status0 stays UNSUPPORTED, never invented CLEAR.
            xpClusters015=new NativeArray<XpCluster64>(n,Allocator.Persistent);
            xpParents015=new NativeArray<int>(n,Allocator.Persistent);xpNext015=new NativeArray<int>(n,Allocator.Persistent);
            xpPairCount015=new NativeArray<int>(1,Allocator.Persistent);xpClusterStatus015=new NativeArray<int>(1,Allocator.Persistent);
            xpMatrixCount015=new NativeArray<int>(1,Allocator.Persistent);xpNearest015=new NativeArray<int>(1,Allocator.Persistent);
            xpReadyAt015=new NativeArray<float>(n,Allocator.Persistent);xpRetryAt015=new NativeArray<float>(n,Allocator.Persistent);
            xpNumericMatrices015=new NativeArray<float4x4>(n,Allocator.Persistent);xpUnityMatrices015=new NativeArray<Matrix4x4>(n,Allocator.Persistent);
            xpClusterIds015=new NativeArray<uint>(n,Allocator.Persistent);
            xpBatch0015=new Matrix4x4[1023];xpBatch1015=new Matrix4x4[1023];xpBatch2015=new Matrix4x4[1023];xpBatch3015=new Matrix4x4[1023];xpBatch4015=new Matrix4x4[4];
            xpMesh015=new Mesh{name="Original015 XP octahedron"};
            xpMesh015.vertices=new[]{Vector3.up,Vector3.down,Vector3.left,Vector3.right,Vector3.forward,Vector3.back};
            xpMesh015.triangles=new[]{0,4,3,0,3,5,0,5,2,0,2,4,1,3,4,1,5,3,1,2,5,1,4,2};
            xpMesh015.RecalculateNormals();xpMesh015.RecalculateBounds();
            Shader shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Unlit/Color");
            xpMaterial015=new Material(shader){name="Original015 pooled XP green",enableInstancing=true};
            xpMaterial015.SetColor("_BaseColor",new Color(.23f,1f,.66f));xpMaterial015.SetColor("_Color",new Color(.23f,1f,.66f));
            xpMaterial015.EnableKeyword("_EMISSION");xpMaterial015.SetColor("_EmissionColor",new Color(.46f,2f,1.32f));
        }
        void MarkXpSourceReady015(int index)
        {xpReadyAt015[index]=Time.unscaledTime+.45f;xpRetryAt015[index]=0f;}
        void RebuildXpClusters015()
        {
            CompleteXpReaders015();
            ScheduleXpCatchupProjection015();
            xpReaders015=new BuildXpMergePairs015{Sources=xpEffectiveSources015,Cells=xpCells015,Pairs=xpPairs015,PairCount=xpPairCount015,
                MaximumBaseXp=CampaignStagePacing.MaximumBaseXp}.Schedule(xpReaders015);
            CompleteXpReaders015();
            XpMergeCandidatePairs015=xpPairCount015[0];xpHasMergePairs015=XpMergeCandidatePairs015>0;
            uint world=xpHasMergePairs015?CaptureXpMergeGeometry015():0;
            if(world!=0)xpReaders015=owner.World.ScheduleXpClearance015(xpEffectiveSources015,xpPairs015,xpPairCount015,xpClearance015,world,xpSourceRevision015,xpReaders015);
            xpReaders015=new MergeXpPresentation015{Sources=xpEffectiveSources015,Pairs=xpPairs015,Clearance=xpClearance015,PairCount=xpPairCount015,
                Clusters=xpClusters015,Parent=xpParents015,Next=xpNext015,Status=xpClusterStatus015,
                WorldGeneration=world,SourceRevision=xpSourceRevision015,ClearanceSourceRevision=world!=0?xpSourceRevision015:0,MaximumBaseXp=CampaignStagePacing.MaximumBaseXp}.Schedule(xpReaders015);
            CompleteXpReaders015();xpBuiltRevision015=xpSourceRevision015;xpBuiltWorld015=world;xpBuiltWorldOwner015=owner.World;
            if(xpClusterStatus015[0]!=1)xpFault015=true;
        }
        void TickXpPresentation015()
        {
            XpVisibleInstances015=0;
            if(!EnsureXpRun015()||xpWireGeneration015<=0||owner.Player==null)return;
            if(xpBuiltRevision015!=xpSourceRevision015||xpBuiltWorldOwner015!=owner.World)RebuildXpClusters015();
            else if(xpHasMergePairs015)
            {
                uint world=CaptureXpMergeGeometry015();
                if(xpBuiltWorld015!=world)RebuildXpClusters015();
            }
            if(owner.SimulationActive)xpPresentationSeconds015=(xpPresentationSeconds015+Mathf.Clamp(Time.deltaTime,0f,.1f))%120f;
            if(!rooms.TryGetAdmissionSlot(rooms.LocalClientId,out int slot)||slot<0||slot>1)return;
            xpReaders015=new BuildXpOrbMatrices015{Clusters=xpClusters015,Sources=xpEffectiveSources015,ClusterStatus=xpClusterStatus015,
                Matrices=xpNumericMatrices015,ClusterIds=xpClusterIds015,Count=xpMatrixCount015,WrappedSeconds=xpPresentationSeconds015,
                RecipientBit=1u<<slot,AuthenticatedRecipientId=rooms.LocalClientId}.Schedule();
            xpReaders015=new CopyXpMatrices015{Input=xpNumericMatrices015,Count=xpMatrixCount015,SourceIds=xpClusterIds015,Sources=xpEffectiveSources015,Output=xpUnityMatrices015}.Schedule(xpReaders015);
            CompleteXpReaders015();
            int count=xpMatrixCount015[0];XpVisibleInstances015=count;
            DrawXpBatch015(xpBatch0015,0,Mathf.Min(1023,count));
            DrawXpBatch015(xpBatch1015,1023,Mathf.Min(1023,Mathf.Max(0,count-1023)));
            DrawXpBatch015(xpBatch2015,2046,Mathf.Min(1023,Mathf.Max(0,count-2046)));
            DrawXpBatch015(xpBatch3015,3069,Mathf.Min(1023,Mathf.Max(0,count-3069)));
            DrawXpBatch015(xpBatch4015,4092,Mathf.Max(0,count-4092));
            if(!owner.IsPlaying||!owner.SimulationActive||Time.unscaledTime<xpNextScan015)return;
            xpNextScan015=Time.unscaledTime+.1f;
            Vector3 feet=owner.Player.transform.position;
            xpReaders015=new FindNearestXpSource015{Sources=xpEffectiveSources015,Cells=xpCells015,ReadyAt=xpReadyAt015,RetryAt=xpRetryAt015,
                Result=xpNearest015,Feet=new float3(feet.x,feet.y,feet.z),Now=Time.unscaledTime,
                Radius=owner.Progression.PickupRangeMetres,Recipient=rooms.LocalClientId,MaximumBaseXp=CampaignStagePacing.MaximumBaseXp}.Schedule();
            CompleteXpReaders015();int sourceId=xpNearest015[0];
            if(sourceId<=0)return;
            var source=xpSources015[sourceId-1];Vector3 destination=new Vector3(source.X,source.Y+.4f,source.Z);
            xpRetryAt015[sourceId-1]=Time.unscaledTime+.5f;
            if(!xpClaimVisibility.IsClear(feet+Vector3.up*.95f,destination,owner.Player.transform))return;
            RequestSourceXp015(sourceId);
        }
        void DrawXpBatch015(Matrix4x4[] staging,int start,int count)
        {
            if(count<=0)return;
            NativeArray<Matrix4x4>.Copy(xpUnityMatrices015,start,staging,0,count);
            Graphics.DrawMeshInstanced(xpMesh015,0,xpMaterial015,staging,count,null,ShadowCastingMode.Off,false,0,null,LightProbeUsage.Off);
        }
        void DisposeXpPresentation015()
        {
            if(xpCells015.IsCreated)xpCells015.Dispose();
            if(xpPairs015.IsCreated)xpPairs015.Dispose();if(xpClearance015.IsCreated)xpClearance015.Dispose();
            if(xpClusters015.IsCreated)xpClusters015.Dispose();
            if(xpParents015.IsCreated)xpParents015.Dispose();if(xpNext015.IsCreated)xpNext015.Dispose();
            if(xpPairCount015.IsCreated)xpPairCount015.Dispose();if(xpClusterStatus015.IsCreated)xpClusterStatus015.Dispose();
            if(xpMatrixCount015.IsCreated)xpMatrixCount015.Dispose();if(xpNearest015.IsCreated)xpNearest015.Dispose();
            if(xpReadyAt015.IsCreated)xpReadyAt015.Dispose();if(xpRetryAt015.IsCreated)xpRetryAt015.Dispose();
            if(xpNumericMatrices015.IsCreated)xpNumericMatrices015.Dispose();if(xpUnityMatrices015.IsCreated)xpUnityMatrices015.Dispose();
            if(xpClusterIds015.IsCreated)xpClusterIds015.Dispose();
            if(xpMesh015!=null)Destroy(xpMesh015);if(xpMaterial015!=null)Destroy(xpMaterial015);
            xpMesh015=null;xpMaterial015=null;xpBatch0015=xpBatch1015=xpBatch2015=xpBatch3015=xpBatch4015=null;
            xpBuiltWorldOwner015=null;xpBuiltWorld015=0;xpHasMergePairs015=false;
            XpMergeCandidatePairs015=0;XpGeometryLeaseCaptures015=0;
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Original per-source arena anchors, bounded bob/rigid rotation; claim reach uses each original source, never cluster centroid.
// Array Formats: Preallocated4096source/cluster/matrix/recipient buffers and Morton map; five fixed managed draw staging arrays; one original6vertex/8triangle mesh/material cold-owned.
// Dependencies/Pointers: Source owner completes all numeric readers before writes/reuse/disposal; draw uses existing main-thread renderer with no per-source GameObject. Missing certified World clearances leave all sources unmerged, explicitly pending actual Mega Orb behavior and native allocation/performance proof.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Current world solid AABB supersets and original XP anchors; floor and complete rotating/bobbing octahedron envelope included.
// Array Formats: Fixed4104 native solids/Morton grid and4095 source-pair certificates; independent world/source generations.
// Dependencies/Pointers: World and source owners complete readers before mutation/dispose. Existing claims/recipient masks/range/LOS and XP arithmetic remain canonical; native real merges/performance pending root.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Same immutable XP anchors and current World geometry; candidate pairs depend only on the current source revision.
// Array Formats: Cached pair-presence bool and two diagnostic scalars; existing source, pair and geometry buffers retain their fixed capacities.
// Dependencies/Pointers: Source changes rebuild pairs first. Zero candidates require no World certificate; nonzero candidates still recapture current geometry before drawing. No reward, claim, merge or disposal authority changes.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Accepted parent/wave/owner/run identities and original source positions; literal claim-time catch-up children.
// Array Formats: Existing4096source64 ledger,905entitlement32 sidecars/dirty words,4096readonly projection, exact128BVersion2 frames.
// Dependencies/Pointers: Root composes structural clearance and these guarded owner hooks; base-onlyForge, no doublebonus, native/peer/carry/visual/performance acceptance pending.
