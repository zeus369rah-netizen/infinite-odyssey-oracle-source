using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using WorkshopReload015=UnityEditor.AssemblyReloadEvents.AssemblyReloadCallback;
#else
using WorkshopReload015=System.Action;
#endif
namespace OdysseyOracle
{
    public sealed class WorkshopBuildPool015:IDisposable
    {
        readonly Transform[] roots=new Transform[4],previewModels=new Transform[4];readonly SavedTowerModelPresentation[] models=new SavedTowerModelPresentation[4];readonly MeshRenderer[] renderers=new MeshRenderer[4];readonly MaterialPropertyBlock block=new MaterialPropertyBlock();readonly float2[] ranges=new float2[12];readonly int[] tiers=new int[4];
        readonly Transform home;NativeArray<WorkshopBuildClock32> clocks;NativeArray<WorkshopBuildPose32> poses;JobHandle fence;bool disposed,acquired,subscribed,playing;WorkshopReload015 reload;
        public bool Active=>!disposed&&playing;
        static readonly int BuildId=Shader.PropertyToID("_WorkshopBuild"),HeightId=Shader.PropertyToID("_WorkshopHeight");
        public WorkshopBuildPool015(Transform owner,SavedTowerModelLibrary library,int layer,uint renderingMask)
        {
            if(owner==null||library==null)throw new ArgumentException();home=owner;NativeArray<WorkshopBuildBounds32> bounds=default;NativeArray<float2> output=default;
            try
            {
                WorkshopBuildResources015.Acquire(out Mesh mesh,out Material material);acquired=true;
                clocks=new NativeArray<WorkshopBuildClock32>(4,Allocator.Persistent);poses=new NativeArray<WorkshopBuildPose32>(4,Allocator.Persistent);
                bounds=new NativeArray<WorkshopBuildBounds32>(72,Allocator.TempJob);output=new NativeArray<float2>(12,Allocator.TempJob);
                for(int type=0;type<4;type++)for(int tier=1;tier<=3;tier++){var template=library.Get(type,tier);for(int g=0;g<6;g++){Mesh source=template.Groups[g];if(source==null)continue;Bounds b=source.bounds;bounds[(type*3+tier-1)*6+g]=new WorkshopBuildBounds32{Center=new float3(b.center.x,b.center.y,b.center.z),Extents=new float3(b.extents.x,b.extents.y,b.extents.z),PivotY=g==0?0:(float)template.Recipe.HeadPivot.Y,Present=1};}}
                fence=new WorkshopBuildBoundsJob015{Bounds=bounds,Ranges=output}.Schedule(12,1);fence.Complete();for(int i=0;i<12;i++){ranges[i]=output[i];if(ranges[i].y<=.01f)throw new InvalidOperationException("Actual saved workshop bounds are invalid.");}
                for(int i=0;i<4;i++){var go=new GameObject("Original pooled workshop assembly015",typeof(MeshFilter),typeof(MeshRenderer));go.layer=layer;roots[i]=go.transform;roots[i].SetParent(owner,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;var r=go.GetComponent<MeshRenderer>();renderers[i]=r;r.sharedMaterial=material;r.renderingLayerMask=renderingMask;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;r.lightProbeUsage=LightProbeUsage.Off;r.reflectionProbeUsage=ReflectionProbeUsage.Off;r.enabled=false;}
                reload=Dispose;LightPoolLifecycle.SubscribeBeforeReload(reload);subscribed=true;
            }
            catch{Dispose();throw;}
            finally{fence.Complete();if(output.IsCreated)output.Dispose();if(bounds.IsCreated)bounds.Dispose();}
        }
        public float2 Range(int type,int tier)=>type>=0&&type<4&&tier>=1&&tier<=3?ranges[type*3+tier-1]:default;
        public bool Bind(int id,int revision,int type,int tier,SavedTowerModelPresentation model,Transform preview,Transform anchor,bool accepted,double now)
        {
            float2 range=Range(type,tier);if(disposed||anchor==null||model==null&&preview==null||!WorkshopBuildMath015.Valid(now,id,revision,type,range.y))return false;fence.Complete();int slot=-1;
            for(int i=0;i<4;i++)if(clocks[i].Id==id){slot=i;break;}
            if(slot<0)for(int i=0;i<4;i++)if(clocks[i].Id==0){slot=i;break;}
            if(slot<0||!WorkshopBuildMath015.CanRebind(clocks[slot].Id,clocks[slot].Revision,tiers[slot],id,revision,tier))return false;
            if(clocks[slot].Id==id&&tiers[slot]==tier)
            {
                // A newer metadata revision is not another accepted construction.
                // Retain its live clock, lease and borrowed model until the original completion.
                var current=clocks[slot];current.Revision=revision;clocks[slot]=current;return false;
            }
            if(!WorkshopBuildMath015.NextLease(clocks[slot].Lease,out uint lease))return false;
            accepted=WorkshopBuildMath015.AnimateAccepted(clocks[slot].Id,tiers[slot],tier,accepted);
            Restore(slot);models[slot]=model;previewModels[slot]=preview;tiers[slot]=tier;
            // World effect roots stay under the stable pool owner; borrowed towers may be destroyed independently.
            roots[slot].SetParent(model!=null?home:anchor,false);if(model!=null){roots[slot].SetPositionAndRotation(anchor.TransformPoint(new Vector3(0,range.x,0)),anchor.rotation);}else{roots[slot].localPosition=new Vector3(0,range.x,0);roots[slot].localRotation=Quaternion.identity;}roots[slot].localScale=Vector3.one;
            clocks[slot]=new WorkshopBuildClock32{Started=now,Lease=lease,Id=id,Revision=revision,Type=type,Active=accepted?1u:0u,Height=range.y};renderers[slot].enabled=accepted;
            if(accepted){playing=true;Apply(slot,WorkshopBuildMath015.Evaluate(clocks[slot],now,OracleBookAccessibility.PrefersReducedMotion));}
            return accepted;
        }
        public void Tick(double now,bool reduced)
        {
            if(disposed)return;fence.Complete();fence=new WorkshopBuildPoseJob015{Clocks=clocks,Poses=poses,Now=now,Reduced=reduced}.Schedule(4,1);fence.Complete();
            playing=false;for(int i=0;i<4;i++){if(clocks[i].Active==0)continue;var p=poses[i];if(models[i]!=null&&!models[i].WorkshopLive015){Remove(clocks[i].Id);continue;}Apply(i,p);if(p.Build.z==0){var c=clocks[i];c.Active=0;clocks[i]=c;Restore(i);}else playing=true;}
        }
        void Apply(int i,WorkshopBuildPose32 pose)
        {
            var c=clocks[i];
            // Preview floor is captured at bind, independent of its changing turntable matrix.
            if(models[i]!=null)models[i].ApplyWorkshopScale015(pose.Build.y,ranges[c.Type*3+tiers[i]-1].x);
            else if(previewModels[i]!=null){float actualFloor=roots[i].localPosition.y;previewModels[i].localPosition=new Vector3(0,actualFloor*(1-pose.Build.y),0);previewModels[i].localScale=new Vector3(1,pose.Build.y,1);}
            block.SetVector(BuildId,new Vector4(pose.Build.x,pose.Build.z,pose.Build.w,c.Type));block.SetFloat(HeightId,c.Height);renderers[i].SetPropertyBlock(block);renderers[i].enabled=pose.Build.z>0;
        }
        void Restore(int i){if(models[i]!=null)models[i].ApplyWorkshopScale015(1,0);if(previewModels[i]!=null){previewModels[i].localPosition=Vector3.zero;previewModels[i].localScale=Vector3.one;}if(renderers[i]!=null)renderers[i].enabled=false;}
        public void Remove(int id)
        {
            if(disposed||!clocks.IsCreated)return;fence.Complete();for(int i=0;i<4;i++)if(clocks[i].Id==id){Restore(i);models[i]=null;previewModels[i]=null;tiers[i]=0;uint lease=clocks[i].Lease;clocks[i]=new WorkshopBuildClock32{Lease=lease};if(roots[i]!=null)roots[i].SetParent(home,false);}
        }
        public void Clear(){if(disposed||!clocks.IsCreated)return;fence.Complete();playing=false;for(int i=0;i<4;i++){Restore(i);models[i]=null;previewModels[i]=null;tiers[i]=0;uint lease=clocks[i].Lease;clocks[i]=new WorkshopBuildClock32{Lease=lease};if(roots[i]!=null)roots[i].SetParent(home,false);}}
        public void Dispose()
        {
            if(disposed)return;Clear();disposed=true;fence.Complete();if(subscribed)LightPoolLifecycle.UnsubscribeBeforeReload(reload);subscribed=false;
            for(int i=0;i<4;i++)if(roots[i]!=null){roots[i].gameObject.SetActive(false);WorkshopBuildResources015.Drop(roots[i].gameObject);}if(poses.IsCreated)poses.Dispose();if(clocks.IsCreated)clocks.Dispose();if(acquired)WorkshopBuildResources015.Release();acquired=false;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Four cached floor-local effect roots, saved base/head cosmetic scale or private atlas assembly only; canonical roots/collision untouched.
// Array Formats: Clock32[4]/Pose32[4] persistent with completed fences, twelve cached float2 bounds and four renderer/model references. Shared original mesh/material are cold leased.
// Dependencies/Pointers: Accepted caller supplies identity/revision after authority commit; baseline is silent. Clear/Remove restores before release, dispose/reload fences before native free; no hot allocation, gameplay or audio ownership.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing cosmetic tower-local floor and accepted snapshot identity only; roots/collision/authority unchanged.
// Array Formats: Existing fixed four-slot Clock32/Pose32 leases; same-tier metadata advances Revision without replacing the active lease.
// Dependencies/Pointers: Enabled Builder alone admits effects/baselines; disable invalidates snapshot state. Work-only reviewed follow-up; native/render/peer acceptance remains root-owned.
