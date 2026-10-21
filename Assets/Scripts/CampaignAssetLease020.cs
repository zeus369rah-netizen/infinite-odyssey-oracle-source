// PSEUDOCODE: prepare exact native support and immutable catalogue data cold; retain an original base binding; draw only completed current-scope banks for one accepted camera; retire independently with explicit pending retry.
using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using ReloadCallback=UnityEditor.AssemblyReloadEvents.AssemblyReloadCallback;
#else
using ReloadCallback=System.Action;
#endif
namespace OdysseyOracle
{
    [Serializable]public struct CampaignAssetDiagnostics020
    {
        public ulong WorldToken,Epoch,Sequence,CameraToken,Ticket,OriginalMeshToken,MaskedMeshToken,MaskSequence,EndSequence;
        public uint Generation,SupportGeneration,WorldRevision,CatalogRevision;public int Callbacks,VisibleInstances,DrawCalls,SourceInstances,BucketCount,SupportHits,MaskIndexCount,PracticalLights,MaskCommits,MaskRestores;
        public bool Prepared,Bound,FrameReady,FrameMasked,OriginalRetained,BaseBindingCommitted,OriginalBindingCurrent,CleanupPending;public byte TreeAllowedLods;
    }
    public sealed class CampaignAssetLease020:IDisposable
    {
        static readonly int ClockId=Shader.PropertyToID("_WindClock020"),WindId=Shader.PropertyToID("_WindStrength020"),DirectionId=Shader.PropertyToID("_WindDirection020"),WetId=Shader.PropertyToID("_Wetness020"),EmissionId=Shader.PropertyToID("_EmissionScale020");
        static ulong serial,tickets;static CampaignAssetLease020 pending;
        public static bool HasPending=>pending!=null;
        DungeonWorld world;GameSession session;Transform root;Camera liveCamera,captureCamera,activeCamera;
        CampaignAssetCatalog020 catalogue;CampaignAssetEntry020 paving;Material material;Mesh originalBase,maskedBase;MeshFilter baseFilter;
        NativeArray<CampaignAssetItem020> items;NativeArray<CampaignAssetBucket020> buckets;NativeArray<int> partitionCount,maskIndices;
        NativeArray<CampaignAssetSupport020> support;readonly NativeArray<float4x4>[] matrices=new NativeArray<float4x4>[2];
        readonly NativeArray<byte>[] lods=new NativeArray<byte>[2];readonly NativeArray<int>[] counts=new NativeArray<int>[2],starts=new NativeArray<int>[2];
        readonly NativeArray<CampaignAssetFrame020>[] results=new NativeArray<CampaignAssetFrame020>[2];
        JobHandle fence;bool scheduled,prepared,bound,disposed,blocked,renderSubscribed,endSubscribed,reloadSubscribed;int readBank,bucketCount;
        ulong token,epoch,sequence,ticket,activeSequence;uint generation,revision;ReloadCallback reload;Action<ScriptableRenderContext,Camera> renderer,endRenderer;
        CampaignAssetDiagnostics020 diagnostics;
        public string Failure {get;private set;}="";
        public bool Prepared=>prepared&&!disposed&&!blocked;
        public int FloorIndex {get;private set;}=-1;
        public bool CleanupPending=>blocked;
        static void Need(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        static float3 F(Vector3 p)=>new float3(p.x,p.y,p.z);
        static float4x4 M(Matrix4x4 m)=>new float4x4(new float4(m.m00,m.m10,m.m20,m.m30),new float4(m.m01,m.m11,m.m21,m.m31),new float4(m.m02,m.m12,m.m22,m.m32),new float4(m.m03,m.m13,m.m23,m.m33));
        static ulong Token(UnityEngine.Object value)=>value!=null?EntityId.ToULong(value.GetEntityId()):0;
        void Complete(){if(!scheduled)return;fence.Complete();scheduled=false;}
        bool FixedFrame()=>world!=null&&root!=null&&world.transform.localToWorldMatrix.Equals(Matrix4x4.identity)&&root.localToWorldMatrix.Equals(Matrix4x4.identity);
        bool MaskBound()=>baseFilter!=null&&maskedBase!=null&&ReferenceEquals(baseFilter.sharedMesh,maskedBase);
        bool OriginalBound()=>baseFilter!=null&&originalBase!=null&&ReferenceEquals(baseFilter.sharedMesh,originalBase);
        public bool Prepare(DungeonWorld source,Transform candidate,BoxCollider[] floors,uint actualGeneration)
        {
            Need(!prepared&&!disposed&&!blocked&&!HasPending,"Catalogue owner is not reusable");
            world=source;session=GameSession.Instance;root=candidate;generation=actualGeneration;token=Token(root!=null?root.gameObject:null);
            try
            {
                catalogue=Resources.Load<CampaignAssetCatalog020>(CampaignAssetCatalog020.LoadKey);
                if(catalogue==null){Failure="Catalogue not installed; legacy surface retained";return false;}
                Need(catalogue.ValidateCold(out var why),why);Need(world!=null&&world.isActiveAndEnabled&&session!=null&&ReferenceEquals(session.World,world)&&root!=null&&root.gameObject.activeInHierarchy&&root.IsChildOf(world.transform)&&FixedFrame()&&token!=0&&generation!=0,"Exact active identity World/root candidate required");
                Need(floors!=null&&floors.Length==PortalLandingLayout020.SurfaceCount,"Exact owned floor array required");
                for(int i=0;i<catalogue.Entries.Length;i++)if(catalogue.Entries[i].Key==5)paving=catalogue.Entries[i];
                Need(paving!=null&&serial<ulong.MaxValue,"Catalogue epoch unavailable");epoch=++serial;
                support=new NativeArray<CampaignAssetSupport020>(CampaignAssetCatalog020.PilotCount*9,Allocator.Persistent);
                fence=new CampaignAssetSupportJob020{Rays=support}.Schedule(support.Length,9);scheduled=true;Complete();
                int hits=0;for(int i=0;i<support.Length;i++)
                {
                    var sample=support[i];Need(sample.FloorSlot>=0&&sample.FloorSlot<floors.Length,"Pilot support selection failed");var floor=floors[sample.FloorSlot];
                    Need(floor!=null&&floor.enabled&&floor.gameObject.activeInHierarchy&&floor.transform.IsChildOf(root),"Pilot floor ownership unavailable");
                    if(FloorIndex<0)FloorIndex=sample.FloorSlot;Need(FloorIndex==sample.FloorSlot,"Pilot must use exactly floor key10");
                    Need(floor.Raycast(new Ray((Vector3)sample.Origin,Vector3.down),out var hit,.5f)&&hit.collider==floor&&Mathf.Abs(hit.point.y) <=.005f&&hit.normal.y>=.995f,"Actual pilot floor hit missing");hits++;
                }
                Need(hits==351&&token==Token(root.gameObject)&&FixedFrame(),"Pilot support receipt incomplete");support.Dispose();support=default;
                Need(UnsafeUtility.SizeOf<float4x4>()==64&&UnsafeUtility.SizeOf<Matrix4x4>()==64,"Matrix ABI changed");
                material=new Material(catalogue.SharedMaterial);material.name="Owned campaign catalogue profile020";material.enableInstancing=true;
                material.SetFloat("_ReflectionPreferenceWeight020",1);material.SetFloat("_Reflections020",1);material.SetFloat(WetId,1);material.SetFloat(EmissionId,1);
                items=new NativeArray<CampaignAssetItem020>(CampaignAssetCatalog020.MaximumInstances,Allocator.Persistent);
                buckets=new NativeArray<CampaignAssetBucket020>(CampaignAssetCatalog020.MaximumBuckets,Allocator.Persistent);partitionCount=new NativeArray<int>(1,Allocator.Persistent);
                for(int bank=0;bank<2;bank++)
                {
                    matrices[bank]=new NativeArray<float4x4>(CampaignAssetCatalog020.MaximumInstances,Allocator.Persistent);
                    lods[bank]=new NativeArray<byte>(CampaignAssetCatalog020.MaximumBuckets,Allocator.Persistent);counts[bank]=new NativeArray<int>(CampaignAssetCatalog020.MaximumBuckets,Allocator.Persistent);starts[bank]=new NativeArray<int>(CampaignAssetCatalog020.MaximumBuckets,Allocator.Persistent);results[bank]=new NativeArray<CampaignAssetFrame020>(1,Allocator.Persistent);
                }
                Bounds bounds=paving.Lod0.bounds;for(int i=0;i<39;i++)items[i]=new CampaignAssetItem020{Feet=F(catalogue.Placements[i].Feet),Center=F(bounds.center),Extents=F(bounds.extents),Key=catalogue.Placements[i].Key};
                fence=new CampaignAssetPartitionJob020{Items=items,Buckets=buckets,Count=partitionCount,FirstLods=lods[0],SecondLods=lods[1],Length=39}.Schedule();scheduled=true;Complete();bucketCount=partitionCount[0];Need(bucketCount>0&&bucketCount<=256,"Pilot bucket capacity rejected");
                renderer=BeforeCamera;endRenderer=AfterCamera;reload=PrepareForReload;LightPoolLifecycle.SubscribeBeforeReload(reload);reloadSubscribed=true;prepared=true;
                diagnostics=new CampaignAssetDiagnostics020{WorldToken=token,Epoch=epoch,Generation=generation,SupportGeneration=generation,CatalogRevision=catalogue.Revision,SourceInstances=39,BucketCount=bucketCount,SupportHits=hits,Prepared=true,TreeAllowedLods=3};return true;
            }
            catch(Exception error){Failure=error.Message;return false;}
        }
        public void PrepareMaskedBase(MeshFilter filter,NativeArray<LandingVertex020> vertices,NativeArray<int> indices,int vertexCount,int indexCount,int skip,ref JobHandle worldFence)
        {
            Need(Prepared&&FixedFrame()&&filter!=null&&filter.transform==root&&filter.sharedMesh!=null&&skip>=0&&skip+6<=indexCount&&indexCount<=indices.Length&&vertexCount<=vertices.Length,"Exact original identity floor span unavailable");
            baseFilter=filter;originalBase=filter.sharedMesh;
            maskIndices=new NativeArray<int>(indexCount-6,Allocator.Persistent);
            fence=new CampaignAssetMaskJob020{Source=indices,Destination=maskIndices,Length=indexCount,Skip=skip}.Schedule(worldFence);worldFence=fence;scheduled=true;Complete();
            maskedBase=new Mesh{name="Campaign base with key10 top omitted020",indexFormat=IndexFormat.UInt32};
            maskedBase.SetVertexBufferParams(vertexCount,new VertexAttributeDescriptor(VertexAttribute.Position,VertexAttributeFormat.Float32,3),new VertexAttributeDescriptor(VertexAttribute.Normal,VertexAttributeFormat.Float32,3),new VertexAttributeDescriptor(VertexAttribute.Color,VertexAttributeFormat.Float32,4),new VertexAttributeDescriptor(VertexAttribute.TexCoord0,VertexAttributeFormat.Float32,2));
            maskedBase.SetVertexBufferData(vertices,0,0,vertexCount,0,MeshUpdateFlags.DontRecalculateBounds);maskedBase.SetIndexBufferParams(maskIndices.Length,IndexFormat.UInt32);maskedBase.SetIndexBufferData(maskIndices,0,0,maskIndices.Length);maskedBase.subMeshCount=1;maskedBase.SetSubMesh(0,new SubMeshDescriptor(0,maskIndices.Length),MeshUpdateFlags.DontRecalculateBounds);maskedBase.bounds=originalBase.bounds;maskedBase.UploadMeshData(true);
            maskIndices.Dispose();maskIndices=default;diagnostics.MaskIndexCount=6;diagnostics.OriginalRetained=true;diagnostics.OriginalMeshToken=Token(originalBase);diagnostics.MaskedMeshToken=Token(maskedBase);
        }
        public bool Bind()
        {
            if(!Prepared||maskedBase==null||baseFilter==null||originalBase==null||!ReferenceEquals(baseFilter.sharedMesh,originalBase)||!WorldCurrent(false))return false;
            Complete();if(!WorldCurrent(false))return false;revision=world.WorldRevision020;Need(revision!=0,"Published revision unavailable");
            try{bound=true;liveCamera=session.Player!=null?session.Player.ViewCamera:null;renderSubscribed=true;RenderPipelineManager.beginCameraRendering+=renderer;endSubscribed=true;RenderPipelineManager.endCameraRendering+=endRenderer;diagnostics.Bound=true;diagnostics.WorldRevision=revision;return true;}
            catch{Hide();throw;}
        }
        bool WorldCurrent(bool published)=>!disposed&&!blocked&&world!=null&&world.isActiveAndEnabled&&session!=null&&session.isActiveAndEnabled&&root!=null&&FixedFrame()&&catalogue!=null&&catalogue.Revision==diagnostics.CatalogRevision&&ReferenceEquals(GameSession.Instance,session)&&ReferenceEquals(session.World,world)&&world.CampaignHubActive020&&world.CampaignHubGeneration020==generation&&world.PortalLandingPrototypeGeneration020==generation&&token==Token(root.gameObject)&&world.TryGetPortalLandingPrototype020(generation,out var snapshot)&&snapshot.Root==root&&(!published||world.WorldRevision020==revision);
        public bool RebindGeneration(uint next)
        {
            // The existing builder reuses this same immutable root/geometry on later entries.
            // SupportGeneration stays explicit: this does not claim a new native support collection.
            if(!Prepared||bound||root==null||root.gameObject.activeSelf||token!=Token(root.gameObject)||next<=generation||serial==ulong.MaxValue||!FixedFrame())return false;
            Complete();if(!FixedFrame()||root.gameObject.activeSelf)return false;generation=next;epoch=++serial;revision=0;diagnostics.Generation=next;diagnostics.Epoch=epoch;return true;
        }
        public void Tick(Camera camera,float clock,bool held,bool reduced,Vector4 wind)
        {
            if(!bound&&Prepared&&WorldCurrent(false))Bind();if(!bound||!WorldCurrent(true)){Hide();return;}liveCamera=camera;
            if(reduced)material.SetFloat(WindId,0);if(captureCamera!=null||held)return;
            material.SetFloat(ClockId,float.IsFinite(clock)?Mathf.Repeat(clock,4096):0);material.SetFloat(WindId,reduced?0:.35f);material.SetVector(DirectionId,new Vector4(wind.x,0,wind.y,0));
        }
        void BeforeCamera(ScriptableRenderContext context,Camera camera)
        {
            Camera accepted=captureCamera!=null?captureCamera:liveCamera;
            if(!bound||camera==null)return;
            try
            {
                if(!ReferenceEquals(camera,accepted))
                {
                    // A nested/overlapping foreign camera always sees the original base.
                    // Invalidating an unfinished accepted frame is safer than sharing its global binding.
                    if(!ReferenceEquals(activeCamera,null)||MaskBound()){RestoreBase();activeCamera=null;activeSequence=0;ClearFrame();}
                    return;
                }
                RestoreBase();activeCamera=null;activeSequence=0;ClearFrame();if(!WorldCurrent(true)||!session.CampaignHubActive020||!OriginalBound()){Hide();return;}
                Complete();if(!WorldCurrent(true))return;Need(sequence<ulong.MaxValue,"Catalogue sequence exhausted");int write=1-readBank;ulong step=++sequence;
                float reach=session.Graphics!=null?session.Graphics.EffectiveRenderDistanceMetres:world.CampaignHubFarClip020;
                var view=new CampaignAssetView020{Eye=F(camera.transform.position),Clip=M(camera.cullingMatrix),Tangent=Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f),Near=Mathf.Max(.01f,camera.nearClipPlane),Reach=reach};
                fence=new CampaignAssetFrameJob020{Items=items,Buckets=buckets,Previous=lods[readBank],Next=lods[write],Counts=counts[write],Starts=starts[write],Matrices=matrices[write],Result=results[write],View=view,Thresholds=F(paving.Thresholds),Allowed=paving.AllowedLods,BucketCount=bucketCount,ItemCount=39,Sequence=step}.Schedule();scheduled=true;Complete();
                var frame=results[write][0];if(!WorldCurrent(true)||!OriginalBound()||frame.Ready!=1||frame.Sequence!=step)return;
                int draws=0;
                for(int b=0;b<bucketCount;b++)
                {
                    int length=counts[write][b];if(length<=0)continue;var box=buckets[b];var parameters=new RenderParams(material){camera=camera,worldBounds=new Bounds((Vector3)box.Center,(Vector3)(box.Extents*2)),shadowCastingMode=ShadowCastingMode.Off,receiveShadows=true,lightProbeUsage=LightProbeUsage.BlendProbes,reflectionProbeUsage=ReflectionProbeUsage.BlendProbes};
                    var mesh=paving.MeshAt(lods[write][b]);int start=starts[write][b];
                    if(SystemInfo.supportsInstancing)
                    {
                        for(int at=0;at<length;at+=128){int n=Math.Min(128,length-at);Graphics.RenderMeshInstanced(parameters,mesh,0,matrices[write].GetSubArray(start+at,n).Reinterpret<Matrix4x4>(),n);draws++;}
                    }
                    else{Need(39<=256,"Noninstancing admission exceeded");for(int at=0;at<length;at++){Graphics.RenderMesh(parameters,mesh,0,(Matrix4x4)matrices[write][start+at]);draws++;}}
                }
                if(!WorldCurrent(true)||!OriginalBound()){Hide();return;}
                activeCamera=camera;activeSequence=step;
                if(frame.Visible>0){baseFilter.sharedMesh=maskedBase;Need(MaskBound(),"Owned camera mask binding failed");diagnostics.MaskCommits++;diagnostics.MaskSequence=step;diagnostics.FrameMasked=true;}
                diagnostics.Callbacks++;diagnostics.Sequence=step;diagnostics.CameraToken=Token(camera);diagnostics.Ticket=ticket;diagnostics.FrameReady=true;diagnostics.VisibleInstances=frame.Visible;diagnostics.DrawCalls=draws;readBank=write;
            }
            catch(Exception error){FailFrame(error);}
        }
        void AfterCamera(ScriptableRenderContext context,Camera camera)
        {
            if(ReferenceEquals(activeCamera,null)||!ReferenceEquals(camera,activeCamera))return;
            ulong step=activeSequence;
            try
            {
                RestoreBase();activeCamera=null;activeSequence=0;
                if(!WorldCurrent(true)||!session.CampaignHubActive020||!OriginalBound()||!diagnostics.FrameReady||diagnostics.Sequence!=step){Hide();return;}
                diagnostics.EndSequence=step;
            }
            catch(Exception error){FailFrame(error);}
        }
        void FailFrame(Exception error){Failure=error.Message;blocked=true;try{Hide();}catch{Retain(this);}}
        void ClearFrame(){diagnostics.FrameReady=false;diagnostics.FrameMasked=false;diagnostics.MaskSequence=0;diagnostics.EndSequence=0;diagnostics.CameraToken=0;diagnostics.Ticket=0;diagnostics.VisibleInstances=0;diagnostics.DrawCalls=0;}
        void RestoreBase(){if(MaskBound()){baseFilter.sharedMesh=originalBase;Need(OriginalBound(),"Original camera base restoration failed");diagnostics.MaskRestores++;}}
        public void Hide()
        {
            bound=false;diagnostics.Bound=false;ClearFrame();Exception first=null;
            if(renderSubscribed)try{RenderPipelineManager.beginCameraRendering-=renderer;renderSubscribed=false;}catch(Exception error){first=error;}
            if(endSubscribed)try{RenderPipelineManager.endCameraRendering-=endRenderer;endSubscribed=false;}catch(Exception error){if(first==null)first=error;}
            try{RestoreBase();activeCamera=null;activeSequence=0;}catch(Exception error){if(first==null)first=error;}
            captureCamera=null;ticket=0;if(first!=null){blocked=true;throw first;}
        }
        public CampaignAssetDiagnostics020 Diagnostics
        {
            get{var value=diagnostics;value.Prepared=Prepared;value.Bound=bound&&WorldCurrent(true);value.BaseBindingCommitted=MaskBound();value.OriginalBindingCurrent=OriginalBound();value.OriginalRetained=originalBase!=null;value.CleanupPending=blocked;return value;}
        }
#if UNITY_EDITOR
        public bool BeginCapture(Camera camera,out ulong value)
        {
            value=0;if(camera==null||captureCamera!=null||!ReferenceEquals(activeCamera,null)||MaskBound()||!bound||!WorldCurrent(true)||!OriginalBound()||!session.CampaignHubActive020||tickets==ulong.MaxValue)return false;
            try
            {
                Complete();fence=new CampaignAssetResetLodJob020{First=lods[0],Second=lods[1]}.Schedule();scheduled=true;Complete();
                if(!bound||!WorldCurrent(true)||!OriginalBound()||!session.CampaignHubActive020||camera==null)return false;
                ClearFrame();captureCamera=camera;ticket=++tickets;value=ticket;return true;
            }
            catch{blocked=true;Retain(this);throw;}
        }
        public void EndCapture(ulong value)
        {
            if(value==0||value!=ticket)return;captureCamera=null;ticket=0;ClearFrame();
            try{RestoreBase();activeCamera=null;activeSequence=0;Complete();fence=new CampaignAssetResetLodJob020{First=lods[0],Second=lods[1]}.Schedule();scheduled=true;Complete();}
            catch{blocked=true;Retain(this);throw;}
        }
#endif
        static void Release<T>(ref NativeArray<T> array,ref Exception first)where T:unmanaged{if(!array.IsCreated)return;try{array.Dispose();array=default;}catch(Exception error){if(first==null)first=error;}}
        void PrepareForReload(){try{Dispose();}catch{Retain(this);}}
        public void Dispose()
        {
            if(disposed)return;Exception first=null;try{Hide();}catch(Exception error){first=error;}try{Complete();}catch{blocked=true;throw;}
            Release(ref support,ref first);Release(ref maskIndices,ref first);Release(ref partitionCount,ref first);Release(ref items,ref first);Release(ref buckets,ref first);
            for(int i=0;i<2;i++){Release(ref matrices[i],ref first);Release(ref lods[i],ref first);Release(ref counts[i],ref first);Release(ref starts[i],ref first);Release(ref results[i],ref first);}
            bool retained=baseFilter!=null&&maskedBase!=null&&ReferenceEquals(baseFilter.sharedMesh,maskedBase);
            if(retained){if(first==null)first=new InvalidOperationException("Masked base binding still retained");}
            else try{DestroyOwned(maskedBase);maskedBase=null;}catch(Exception error){if(first==null)first=error;}
            try{DestroyOwned(material);material=null;}catch(Exception error){if(first==null)first=error;}
            if(first!=null){blocked=true;throw first;}if(reloadSubscribed){LightPoolLifecycle.UnsubscribeBeforeReload(reload);reloadSubscribed=false;}disposed=true;prepared=false;blocked=false;catalogue=null;paving=null;originalBase=null;baseFilter=null;
        }
        static void DestroyOwned(UnityEngine.Object value){if(value==null)return;if(Application.isPlaying)UnityEngine.Object.Destroy(value);else UnityEngine.Object.DestroyImmediate(value);}
        public static void Retain(CampaignAssetLease020 value){if(pending!=null&&!ReferenceEquals(pending,value))throw new InvalidOperationException("Another catalogue cleanup is retained");pending=value;value.blocked=true;}
        public static bool RetryPending(out string failure)
        {failure="";if(pending==null)return true;try{pending.Dispose();pending=null;return true;}catch(Exception error){failure=error.Message;return false;}}
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Exact accepted campaign root/token/generation/revision, world-space paving matrices and local8UV immutable catalogue meshes.
// Array Formats:39instances/351cold native rays, <=256Morton buckets, fixed2048matrix banks,0owned lights; one original base plus one private masked base.
// Dependencies/Pointers: Completed support/partition/frame/mask fences precede reuse and disposal. Original base is the default; only a current Ready successfully submitted camera binds the mask until its owned end-camera restore. Both callbacks retain cleanup, identity World/root scope is mandatory, and borrowed assets are never destroyed. Root native URP ordering/GC/render acceptance remains pending.
