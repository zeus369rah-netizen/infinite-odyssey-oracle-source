using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using OdysseyOracle.Foundation.Pools015;
#if UNITY_EDITOR
using XpWorldReload015=UnityEditor.AssemblyReloadEvents.AssemblyReloadCallback;
#else
using XpWorldReload015=System.Action;
#endif

namespace OdysseyOracle
{
    public sealed partial class DungeonWorld
    {
        readonly List<Collider> xpRegistration015=new List<Collider>(XpSolidLimits015.Colliders);
        readonly Collider[] xpSolidColliders015=new Collider[XpSolidLimits015.Colliders];
        readonly BoxCollider[] xpSolidBoxes015=new BoxCollider[XpSolidLimits015.Colliders];
        readonly MeshCollider[] xpSolidMeshes015=new MeshCollider[XpSolidLimits015.Colliders];
        readonly Transform[] xpSolidTransforms015=new Transform[XpSolidLimits015.Colliders];
        readonly Transform[] xpSolidTowerRoots015=new Transform[XpSolidLimits015.Towers];
        readonly ulong[] xpSolidTowerIds015=new ulong[XpSolidLimits015.Towers];
        readonly ulong[] xpRejectedTowerIds015=new ulong[XpSolidLimits015.Towers];
        int xpTowerAcceptedCount015,xpTowerRejectedCount015,xpTowerInventoryCount015;
        bool xpTowerUnknown015,xpTowerInventoryKnown015,xpRebuildingTowerTruth015;
        readonly Bounds[] xpSolidTowerBounds015=new Bounds[XpSolidLimits015.Towers];
        readonly Matrix4x4[] xpSolidTowerMatrices015=new Matrix4x4[XpSolidLimits015.Towers];
        NativeArray<XpSolidInput96> xpSolidInput015;
        NativeArray<XpSolid32> xpSolidCurrent015,xpSolidPrevious015;
        NativeArray<int> xpSolidCompare015,xpSolidGridStatus015;
        NativeParallelMultiHashMap<ulong,int> xpSolidCells015;
        JobHandle xpSolidReader015;
        XpWorldReload015 xpSolidReload015;
        bool xpSolidSubscribed015;
        int xpSolidColliderCount015;
        uint xpSolidGeneration015=1;
        bool xpSolidReady015,xpSolidMembershipKnown015,xpSolidKnown015,xpSolidExhausted015,xpSolidTowerFault015,xpSolidCoverage015,xpSolidNeedsBuild015;
        public uint XpClearanceGeneration015=>xpSolidKnown015&&!xpSolidExhausted015?xpSolidGeneration015:0;
        public int XpClearanceColliderCount015=>xpSolidColliderCount015;
        public int XpAcceptedTowerCount015=>xpTowerAcceptedCount015;
        public int XpRejectedTowerCount015=>xpTowerRejectedCount015;
        public bool XpUnknownTowerRegistration015=>xpTowerUnknown015;
        public int XpCanonicalTowerInventoryCount015=>xpTowerInventoryCount015;
        public bool XpCanonicalTowerInventoryKnown015=>xpTowerInventoryKnown015;

        // Cold topology registration, not a free-space inference from acoustic settings.
        // All canonical new/removed world solid owners must call this before the next presentation.
        public void RebuildXpClearance015()
        {
            xpSolidReader015.Complete();
            if(!xpSolidReady015)
            {
                xpSolidCurrent015=new NativeArray<XpSolid32>(XpSolidLimits015.Solids,Allocator.Persistent);
                xpSolidInput015=new NativeArray<XpSolidInput96>(XpSolidLimits015.Solids,Allocator.Persistent);
                xpSolidPrevious015=new NativeArray<XpSolid32>(XpSolidLimits015.Solids,Allocator.Persistent);
                xpSolidCompare015=new NativeArray<int>(2,Allocator.Persistent);
                xpSolidGridStatus015=new NativeArray<int>(1,Allocator.Persistent);
                xpSolidCells015=new NativeParallelMultiHashMap<ulong,int>(XpSolidLimits015.Cells,Allocator.Persistent);
                xpSolidReady015=true;
                if(!xpSolidSubscribed015)
                {
                    xpSolidReload015=DisposeXpClearance015;
                    LightPoolLifecycle.SubscribeBeforeReload(xpSolidReload015);xpSolidSubscribed015=true;
                }
            }
            xpSolidKnown015=false;xpSolidNeedsBuild015=true;
            xpRegistration015.Clear();GetComponentsInChildren(true,xpRegistration015);
            var session=GameSession.Instance;
            OracleShrine objective=session!=null&&session.World==this&&session.Combat!=null?session.Combat.Shrine:null;
            Collider objectiveCollider=objective!=null?objective.GetComponent<Collider>():null;
            if(objectiveCollider!=null&&!objectiveCollider.transform.IsChildOf(transform))xpRegistration015.Add(objectiveCollider);
            xpSolidMembershipKnown015=objectiveCollider!=null&&xpRegistration015.Count>0&&xpRegistration015.Count<=XpSolidLimits015.Colliders;
            xpSolidColliderCount015=Mathf.Min(xpRegistration015.Count,XpSolidLimits015.Colliders);
            for(int i=0;i<xpSolidColliders015.Length;i++)
            {
                Collider collider=i<xpSolidColliderCount015?xpRegistration015[i]:null;
                xpSolidColliders015[i]=collider;xpSolidBoxes015[i]=collider as BoxCollider;
                xpSolidMeshes015[i]=collider as MeshCollider;xpSolidTransforms015[i]=collider!=null?collider.transform:null;
            }
            xpRegistration015.Clear();RebuildXpTowerTruth015();AdvanceXpWorld015();
        }
        void AdvanceXpWorld015()
        {
            if(xpSolidGeneration015==uint.MaxValue){xpSolidExhausted015=true;xpSolidKnown015=false;return;}
            xpSolidGeneration015++;
        }
        public void RegisterXpTower015(ulong identity,Transform root,Bounds bounds,bool active)
        {TryRegisterXpTower015(identity,root,bounds,active);}
        internal bool TryRegisterXpTower015(ulong identity,Transform root,Bounds bounds,bool active)
        {
            xpSolidReader015.Complete();xpSolidKnown015=false;
            if(!xpRebuildingTowerTruth015)xpTowerInventoryKnown015=false;
            if(identity==0)
            {
                if(active)RejectXpTower015(0);
                AdvanceXpWorld015();return false; // An anonymous removal never clears UNKNOWN.
            }
            int slot=-1;
            for(int i=0;i<XpSolidLimits015.Towers;i++)if(xpSolidTowerIds015[i]==identity){slot=i;break;}
            if(active)
            {
                Vector3 lower=bounds.min,upper=bounds.max;
                float3 lo=new float3(lower.x,lower.y,lower.z),hi=new float3(upper.x,upper.y,upper.z);
                bool valid=root!=null&&math.all(math.isfinite(lo))&&math.all(math.isfinite(hi))&&math.all(hi>lo)&&
                    math.all(math.abs(lo)<=8192)&&math.all(math.abs(hi)<=8192);
                if(!valid){RejectXpTower015(identity);AdvanceXpWorld015();return false;}
                if(slot<0)for(int i=0;i<XpSolidLimits015.Towers;i++)if(xpSolidTowerIds015[i]==0){slot=i;break;}
                if(slot<0){RejectXpTower015(identity);AdvanceXpWorld015();return false;}
            }
            if(slot>=0)
            {
                xpSolidTowerIds015[slot]=active?identity:0;xpSolidTowerRoots015[slot]=active?root:null;
                xpSolidTowerBounds015[slot]=active?bounds:default;
                xpSolidTowerMatrices015[slot]=active?root.localToWorldMatrix:default;
            }
            // Also retires a rejected identity that never obtained an accepted slot.
            for(int i=0;i<XpSolidLimits015.Towers;i++)if(xpRejectedTowerIds015[i]==identity)xpRejectedTowerIds015[i]=0;
            UpdateXpTowerFault015();AdvanceXpWorld015();return true;
        }
        void RejectXpTower015(ulong identity)
        {
            if(identity==0)xpTowerUnknown015=true;
            else
            {
                int selected=-1;
                for(int i=0;i<XpSolidLimits015.Towers;i++)if(xpRejectedTowerIds015[i]==identity){selected=i;break;}
                if(selected<0)for(int i=0;i<XpSolidLimits015.Towers;i++)if(xpRejectedTowerIds015[i]==0){selected=i;break;}
                if(selected<0)xpTowerUnknown015=true;else xpRejectedTowerIds015[selected]=identity;
            }
            UpdateXpTowerFault015();
        }
        void UpdateXpTowerFault015()
        {
            xpTowerAcceptedCount015=xpTowerRejectedCount015=0;
            for(int i=0;i<XpSolidLimits015.Towers;i++)
            {if(xpSolidTowerIds015[i]!=0)xpTowerAcceptedCount015++;if(xpRejectedTowerIds015[i]!=0)xpTowerRejectedCount015++;}
            xpSolidTowerFault015=xpTowerUnknown015||xpTowerRejectedCount015!=0;
        }
        void RebuildXpTowerTruth015()
        {
            // Only a complete current canonical inventory may replace an unknowable rejected set.
            xpSolidKnown015=false;xpRebuildingTowerTruth015=true;xpTowerInventoryKnown015=false;bool complete=false;
            try
            {
                for(int i=0;i<XpSolidLimits015.Towers;i++)
                {
                    xpSolidTowerIds015[i]=xpRejectedTowerIds015[i]=0;xpSolidTowerRoots015[i]=null;
                    xpSolidTowerBounds015[i]=default;xpSolidTowerMatrices015[i]=default;
                }
                xpTowerUnknown015=false;UpdateXpTowerFault015();
                bool trusted=PlacedElementShrine.RebuildXpBodies015(this,out int expected,out int accepted);
                xpTowerInventoryCount015=expected;UpdateXpTowerFault015();
                xpTowerInventoryKnown015=trusted&&expected==accepted&&accepted==xpTowerAcceptedCount015&&
                    expected<=PlacedElementShrine.MaximumRegisteredTowers&&!xpSolidTowerFault015;
                if(!xpTowerInventoryKnown015)xpTowerUnknown015=true;
                UpdateXpTowerFault015();complete=true;
            }
            finally
            {
                xpRebuildingTowerTruth015=false;
                if(!complete){xpTowerUnknown015=true;xpTowerInventoryKnown015=false;UpdateXpTowerFault015();}
            }
        }
        // Main-thread scene adapter gathers local geometry/matrices only; no Physics bounds/sync/query dependency.
        // All projection/comparison/grid/query math runs in jobs.
        // Includes inactive registered barriers so opening/closing lanes cannot introduce an unregistered solid.
        public uint CaptureXpClearance015()
        {
            if(!xpSolidReady015||!IsCoopArena||xpSolidExhausted015)return 0;
            xpSolidReader015.Complete();bool known=xpSolidMembershipKnown015&&!xpSolidTowerFault015;
            for(int i=0;i<XpSolidLimits015.Colliders;i++)
            {
                XpSolidInput96 value=default;
                if(i<xpSolidColliderCount015)
                {
                    Collider collider=xpSolidColliders015[i];
                    if(collider==null)known=false;
                    else if(collider.enabled&&collider.gameObject.activeInHierarchy&&!collider.isTrigger)
                    {
                        if(collider.attachedRigidbody!=null)known=false;
                        else if(xpSolidBoxes015[i]!=null)
                        {
                            var box=xpSolidBoxes015[i];Vector3 center=box.center,size=box.size;
                            value=new XpSolidInput96{Matrix=XpMatrix015(xpSolidTransforms015[i].localToWorldMatrix),
                                Center=new float3(center.x,center.y,center.z),Extents=new float3(size.x,size.y,size.z),Active=1,FullSize=1};
                        }
                        else if(xpSolidMeshes015[i]!=null&&xpSolidMeshes015[i].sharedMesh!=null)
                            value=XpInput015(xpSolidMeshes015[i].sharedMesh.bounds,xpSolidTransforms015[i].localToWorldMatrix);
                        else known=false;
                    }
                }
                xpSolidInput015[i]=value;
            }
            for(int i=0;i<XpSolidLimits015.Towers;i++)
            {
                XpSolidInput96 value=default;
                if(xpSolidTowerIds015[i]!=0)
                {
                    Transform root=xpSolidTowerRoots015[i];
                    if(root==null)known=false;
                    else if(root.gameObject.activeInHierarchy)
                    {
                        // Root relocation is not an animation. It needs a newly accepted body-envelope registration.
                        if(root.localToWorldMatrix!=xpSolidTowerMatrices015[i])known=false;
                        else value=XpInput015(xpSolidTowerBounds015[i],Matrix4x4.identity);
                    }
                }
                xpSolidInput015[XpSolidLimits015.Colliders+i]=value;
            }
            xpSolidReader015=new ProjectXpSolids015{Input=xpSolidInput015,Output=xpSolidCurrent015}.Schedule(XpSolidLimits015.Solids,64);
            xpSolidReader015=new CompareXpSolids015{Current=xpSolidCurrent015,Previous=xpSolidPrevious015,
                Status=xpSolidCompare015,CoverageKnown=known?1:0}.Schedule(xpSolidReader015);
            xpSolidReader015.Complete();bool next=xpSolidCompare015[1]==1;
            bool changed=xpSolidCompare015[0]!=0||next!=xpSolidCoverage015;
            xpSolidCoverage015=next;
            if(changed)AdvanceXpWorld015();
            if(next&&!xpSolidExhausted015&&(changed||xpSolidNeedsBuild015))
            {
                xpSolidReader015=new BuildXpSolidCells015{Solids=xpSolidCurrent015,Cells=xpSolidCells015,Status=xpSolidGridStatus015}.Schedule();
                xpSolidReader015.Complete();
                xpSolidNeedsBuild015=false;
            }
            xpSolidKnown015=next&&!xpSolidExhausted015&&xpSolidGridStatus015[0]==1;
            return XpClearanceGeneration015;
        }
        static float4x4 XpMatrix015(Matrix4x4 m)=>new float4x4(m.m00,m.m01,m.m02,m.m03,m.m10,m.m11,m.m12,m.m13,m.m20,m.m21,m.m22,m.m23,m.m30,m.m31,m.m32,m.m33);
        static XpSolidInput96 XpInput015(Bounds bounds,Matrix4x4 matrix)
        {
            Vector3 center=bounds.center,extents=bounds.extents;
            return new XpSolidInput96{Matrix=XpMatrix015(matrix),Center=new float3(center.x,center.y,center.z),Extents=new float3(extents.x,extents.y,extents.z),Active=1};
        }
        public JobHandle ScheduleXpClearance015(NativeArray<XpSource64> sources,NativeArray<XpMergePair16> pairs,
            NativeArray<int> pairCount,NativeArray<XpMergeClearance8> output,uint generation,uint sourceRevision,JobHandle dependency)
        {
            if(generation==0||generation!=XpClearanceGeneration015)return dependency;
            xpSolidReader015=new CertifyXpPairs015{Sources=sources,Pairs=pairs,PairCount=pairCount,GridStatus=xpSolidGridStatus015,
                Solids=xpSolidCurrent015,Cells=xpSolidCells015,Output=output,WorldGeneration=generation,
                SourceRevision=sourceRevision,ExpectedSourceRevision=sourceRevision,MaximumBaseXp=CampaignStagePacing.MaximumBaseXp}
                .Schedule(output.Length,32,JobHandle.CombineDependencies(dependency,xpSolidReader015));
            return xpSolidReader015;
        }
        void DisposeXpClearance015()
        {
            xpSolidReader015.Complete();xpSolidKnown015=false;
            if(xpSolidSubscribed015){LightPoolLifecycle.UnsubscribeBeforeReload(xpSolidReload015);xpSolidSubscribed015=false;}
            if(xpSolidInput015.IsCreated)xpSolidInput015.Dispose();
            if(xpSolidCurrent015.IsCreated)xpSolidCurrent015.Dispose();if(xpSolidPrevious015.IsCreated)xpSolidPrevious015.Dispose();
            if(xpSolidCompare015.IsCreated)xpSolidCompare015.Dispose();if(xpSolidGridStatus015.IsCreated)xpSolidGridStatus015.Dispose();
            if(xpSolidCells015.IsCreated)xpSolidCells015.Dispose();xpSolidReady015=false;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Current BoxCollider local center/size or MeshCollider local mesh bounds projected through actual matrices in Burst; explicit external objective and collider-free tower envelopes, floors included.
// Array Formats:4096 cached collider/shape/transform references gathered to4104 XpSolidInput96 -> XpSolid32 plus prior snapshot,2comparison ints,1grid status,131072 Morton references and8 tower records.
// Dependencies/Pointers: World owns buffers/fence. Canonical topology includes inactive barriers and objective; triggers are non-solids, active Rigidbody/unsupported shape/moved tower/invalid/cap failures return0. New external topology requires explicit registration; no arbitrary-editor-mutation certification or physics-sync dependency.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing accepted world tower envelopes, canonical unscaled Y-up roots and current identity/lifetime authority.
// Array Formats: Eight accepted proxies plus eight bounded rejected ulong IDs, scalar unknown/current counts; cold complete maximum-four accepted registry reconstruction.
// Dependencies/Pointers: Only matching successful registration/removal clears an identified rejection. Invalid-ID/overflow UNKNOWN clears only after validated canonical cold inventory; unsupported/incomplete geometry remains0. Native jobs/XP claims/economy unchanged; root native recovery proof pending.
