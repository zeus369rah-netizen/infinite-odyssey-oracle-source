using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace OdysseyOracle
{
    // PSEUDOCODE: bind the positive published session/world/root and fresh epoch;
    // re-probe all144 floor samples and eight shared soil offsets; build six
    // immutable GPU meshes behind an inactive owner; expose only validated value
    // scope/cold contact copies; update uniforms from the one held clock and
    // actual capsules; hide before retiring, complete every writer before release.
    public struct CampaignGardenFoliageScope020
    {
        public ulong WorldToken,Epoch;
        public uint Generation,WorldRevision,GeometryRevision,FoliageRevision;
        public bool Positive=>WorldToken!=0&&Epoch!=0&&Generation!=0&&WorldRevision!=0&&GeometryRevision!=0&&FoliageRevision!=0;
        public bool Matches(in CampaignGardenFoliageScope020 b)=>Positive&&b.Positive&&WorldToken==b.WorldToken&&Epoch==b.Epoch&&
            Generation==b.Generation&&WorldRevision==b.WorldRevision&&GeometryRevision==b.GeometryRevision&&FoliageRevision==b.FoliageRevision;
    }
    public sealed class CampaignGardenFoliageLease020:IDisposable
    {
        public const uint CurrentFoliageRevision020=1;
        static readonly int ClockId=Shader.PropertyToID("_WindClock"),WindId=Shader.PropertyToID("_WindStrength"),MotionId=Shader.PropertyToID("_MotionEnabled"),
            Brush0Id=Shader.PropertyToID("_Brush0"),Brush1Id=Shader.PropertyToID("_Brush1"),BrushY0Id=Shader.PropertyToID("_BrushY0"),BrushY1Id=Shader.PropertyToID("_BrushY1");
        static readonly VertexAttributeDescriptor[] Format={
            new VertexAttributeDescriptor(VertexAttribute.Position,VertexAttributeFormat.Float32,3),new VertexAttributeDescriptor(VertexAttribute.Normal,VertexAttributeFormat.Float32,3),
            new VertexAttributeDescriptor(VertexAttribute.Color,VertexAttributeFormat.Float32,4),new VertexAttributeDescriptor(VertexAttribute.TexCoord0,VertexAttributeFormat.Float32,2),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord1,VertexAttributeFormat.Float32,4)};
        readonly Mesh[] meshes=new Mesh[6];readonly MeshRenderer[] renderers=new MeshRenderer[6];readonly LODGroup[] groups=new LODGroup[2];
        NativeArray<CampaignGardenFoliageAnchor020> anchors;
        NativeArray<CampaignGardenPlantContact020> contacts;
        NativeArray<CampaignGardenFoliageVertex020> vertices;
        NativeArray<int> indices,vertexCounts,indexCounts,failure;
        NativeArray<CampaignFoliageFloorPose020> floorPoses;
        NativeArray<CampaignFoliageSupportRay020> rays;
        JobHandle writer;bool scheduled,ready,attempted,disposed,subscribed,cleanupBlocked;
        GameSession session;DungeonWorld world;Transform worldRoot;GameObject root;Material material;
        CampaignGardenFoliageScope020 scope;
        int quality=-1,retiredFrame=-1;
        Vector4 heldBrush0,heldBrush1,heldVertical0,heldVertical1;
        ulong brushSequence;
        public string Failure020 {get;private set;}="";
        public int NativeSupportHits020 {get;private set;}
        public int MatchedSoilRecipes020 {get;private set;}
        public int RetiredFrame020=>retiredFrame;
        public bool CleanupBlocked020=>cleanupBlocked;
        public bool Ready020=>ready&&OwnsCurrent();
        public CampaignGardenFoliageScope020 Scope020=>Ready020?scope:default;

        bool OwnsCurrent()
        {
            if(disposed||session==null||world==null||worldRoot==null||!worldRoot.gameObject.activeInHierarchy||!scope.Positive||!session.isActiveAndEnabled||
                !ReferenceEquals(GameSession.Instance,session)||!ReferenceEquals(session.World,world)||!session.CampaignHubActive020||
                !world.CampaignHubActive020||!world.WorldPublished020||world.CampaignHubGeneration020!=scope.Generation||
                world.WorldRevision020!=scope.WorldRevision||!world.PortalLandingPrototypeReady020)return false;
            return world.TryGetPortalLandingPrototype020(scope.Generation,out var snapshot)&&snapshot.Root==worldRoot&&
                snapshot.Generation==scope.Generation&&EntityId.ToULong(worldRoot.GetEntityId())==scope.WorldToken;
        }
        public bool Prepare020(GameSession owner,DungeonWorld source,ulong epoch,uint geometryRevision)
        {
            if(attempted||disposed||cleanupBlocked||!Application.isPlaying)return false;attempted=true;
            session=owner;world=source;
            try
            {
                if(owner==null||source==null||epoch==0||geometryRevision==0||!owner.CampaignHubActive020||!source.CampaignHubActive020||
                    !source.WorldPublished020||!ReferenceEquals(owner.World,source)||source.CampaignHubGeneration020==0||source.WorldRevision020==0||
                    !source.TryGetPortalLandingPrototype020(source.CampaignHubGeneration020,out var snapshot)||snapshot.Root==null||!snapshot.Root.gameObject.activeInHierarchy)
                    throw new InvalidOperationException("A positive published current World/root is required for foliage admission.");
                worldRoot=snapshot.Root;
                if(worldRoot.position.sqrMagnitude>1e-8f||Quaternion.Angle(worldRoot.rotation,Quaternion.identity)>.001f||(worldRoot.lossyScale-Vector3.one).sqrMagnitude>1e-8f)
                    throw new InvalidOperationException("Foliage world-space vertices require an identity World root.");
                scope=new CampaignGardenFoliageScope020{WorldToken=EntityId.ToULong(worldRoot.GetEntityId()),Epoch=epoch,
                    Generation=snapshot.Generation,WorldRevision=source.WorldRevision020,GeometryRevision=geometryRevision,FoliageRevision=CurrentFoliageRevision020};
                if(!OwnsCurrent())throw new InvalidOperationException("Foliage source identity changed before admission.");
                if(UnsafeUtility.SizeOf<CampaignGardenFoliageVertex020>()!=64||UnsafeUtility.SizeOf<CampaignGardenPlantContact020>()!=40||UnsafeUtility.SizeOf<CampaignFoliageFloorPose020>()!=64)
                    throw new InvalidOperationException("Foliage native ABI changed.");
                anchors=new NativeArray<CampaignGardenFoliageAnchor020>(16,Allocator.Persistent);
                contacts=new NativeArray<CampaignGardenPlantContact020>(128,Allocator.Persistent);
                floorPoses=new NativeArray<CampaignFoliageFloorPose020>(69,Allocator.TempJob);
                rays=new NativeArray<CampaignFoliageSupportRay020>(144,Allocator.TempJob);
                failure=new NativeArray<int>(1,Allocator.TempJob);
                var floors=new BoxCollider[69];
                if(worldRoot.childCount<69)throw new InvalidOperationException("The current owned69-floor stream is incomplete.");
                // Core source creates exactly these first69 direct children as floors.
                // Every numeric pose is then source-matched in the worker; any reorder fails closed.
                for(int i=0;i<69;i++)
                {
                    var child=worldRoot.GetChild(i);
                    if(child.parent!=worldRoot||!child.TryGetComponent<BoxCollider>(out var floor)||floor.isTrigger||!floor.enabled)
                        throw new InvalidOperationException("Foliage floor identity does not match the owned source stream.");
                    floors[i]=floor;
                    floorPoses[i]=new CampaignFoliageFloorPose020{Position=(float3)child.localPosition,Rotation=new quaternion(child.localRotation.x,child.localRotation.y,child.localRotation.z,child.localRotation.w),
                        Scale=(float3)child.localScale,Center=(float3)floor.center,Size=(float3)floor.size};
                }
                for(int i=0;i<16;i++)
                {
                    uint key=i<8?6000u+(uint)i:6100u+(uint)(i-8);
                    if(!CampaignGardenFoliageRecipe020.TryTemplate(key,out var template))throw new InvalidOperationException("Foliage template is unavailable.");
                    anchors[i]=template;
                }
                writer=new CampaignFoliageSupportJob020{Floors=floorPoses,Anchors=anchors,Rays=rays,Failure=failure}.Schedule();scheduled=true;
                CompleteWriter();if(failure[0]!=0)throw new InvalidOperationException("Foliage exact floor geometry/support selection failed.");
                if(!OwnsCurrent())throw new InvalidOperationException("Foliage World changed after numeric support selection.");
                Physics.SyncTransforms();
                if(!CampaignFoliageNativeSupport020.Collect(floors,rays,anchors,scope,out int hits,out int soils,out var supportFailure))
                {NativeSupportHits020=hits;MatchedSoilRecipes020=soils;throw new InvalidOperationException(supportFailure);}
                NativeSupportHits020=hits;MatchedSoilRecipes020=soils;
                if(!OwnsCurrent())throw new InvalidOperationException("Foliage World changed after native support.");
                vertices=new NativeArray<CampaignGardenFoliageVertex020>(CampaignGardenFoliageRecipe020.MaximumVertexCount,Allocator.TempJob);
                indices=new NativeArray<int>(CampaignGardenFoliageRecipe020.MaximumIndexCount,Allocator.TempJob);
                vertexCounts=new NativeArray<int>(6,Allocator.TempJob);indexCounts=new NativeArray<int>(6,Allocator.TempJob);
                writer=new CampaignGardenFoliageBuildJob020{Anchors=anchors,Vertices=vertices,Indices=indices,MeshVertexCounts=vertexCounts,MeshIndexCounts=indexCounts,
                    Contacts=contacts,Failure=failure,WorldToken=scope.WorldToken,Generation=scope.Generation,WorldRevision=scope.WorldRevision,
                    GeometryRevision=scope.GeometryRevision,FoliageRevision=scope.FoliageRevision}.Schedule();scheduled=true;
                CompleteWriter();if(failure[0]!=0)throw new InvalidOperationException("Foliage bounded mesh/contact builder rejected its input.");
                if(!OwnsCurrent())throw new InvalidOperationException("Foliage World changed after mesh construction.");
                Shader shader=Resources.Load<Shader>("Shaders/CampaignGardenFoliage020");
                if(shader==null||!shader.isSupported)throw new InvalidOperationException("Foliage shader import/support is unavailable.");
                material=new Material(shader){name="Owned campaign foliage020",hideFlags=HideFlags.DontSave};
                if(!material.HasProperty(ClockId)||!material.HasProperty(WindId)||!material.HasProperty(MotionId)||!material.HasProperty(Brush0Id)||
                    !material.HasProperty(Brush1Id)||!material.HasProperty(BrushY0Id)||!material.HasProperty(BrushY1Id))
                    throw new InvalidOperationException("Foliage shader uniform contract changed.");
                root=new GameObject("Owned two-sector campaign foliage020");root.SetActive(false);root.transform.SetParent(worldRoot,false);
                for(int sector=0;sector<2;sector++)
                {
                    if(!CampaignGardenFoliageRecipe020.TrySectorBounds(anchors,scope.WorldToken,scope.Generation,scope.WorldRevision,scope.GeometryRevision,scope.FoliageRevision,(byte)sector,out var bounds))
                        throw new InvalidOperationException("Foliage conservative sector bounds are invalid.");
                    var sectorNode=new GameObject(sector==0?"Upper foliage sector020":"Lower foliage sector020");sectorNode.transform.SetParent(root.transform,false);
                    var group=sectorNode.AddComponent<LODGroup>();groups[sector]=group;group.fadeMode=LODFadeMode.None;
                    var lods=new LOD[3];
                    for(int lod=0;lod<3;lod++)
                    {
                        int slot=lod*2+sector,vCount=vertexCounts[slot],iCount=indexCounts[slot];
                        if(vCount<4||vCount>4096||iCount<6||iCount>12288)throw new InvalidOperationException("Foliage mesh output exceeds its fixed slice.");
                        var mesh=new Mesh{name="Owned foliage sector"+sector+" LOD"+lod,indexFormat=IndexFormat.UInt32,hideFlags=HideFlags.DontSave};meshes[slot]=mesh;
                        mesh.SetVertexBufferParams(vCount,Format);mesh.SetVertexBufferData(vertices,slot*4096,0,vCount,0,MeshUpdateFlags.DontRecalculateBounds);
                        mesh.SetIndexBufferParams(iCount,IndexFormat.UInt32);mesh.SetIndexBufferData(indices,slot*12288,0,iCount,MeshUpdateFlags.DontRecalculateBounds);
                        mesh.subMeshCount=1;mesh.SetSubMesh(0,new SubMeshDescriptor(0,iCount,MeshTopology.Triangles),MeshUpdateFlags.DontRecalculateBounds);
                        mesh.bounds=new Bounds((Vector3)((bounds.Minimum+bounds.Maximum)*.5f),(Vector3)(bounds.Maximum-bounds.Minimum+new float3(.002f)));mesh.UploadMeshData(true);
                        var node=new GameObject("Whole sector foliage LOD"+lod);node.transform.SetParent(sectorNode.transform,false);node.AddComponent<MeshFilter>().sharedMesh=mesh;
                        var renderer=node.AddComponent<MeshRenderer>();renderers[slot]=renderer;renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;
                        renderer.receiveShadows=false;renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
                        lods[lod]=new LOD(lod==0?.22f:lod==1?.08f:.008f,new Renderer[]{renderer});
                    }
                    group.SetLODs(lods);group.RecalculateBounds();
                }
                if(!TryReleaseScratch(out var cleanupFailure))throw new InvalidOperationException(cleanupFailure);
                if(!OwnsCurrent())throw new InvalidOperationException("Foliage World changed before coherent ready publication.");
                world.WorldModeChanged020+=WorldChanged;subscribed=true;
                ready=true;Failure020="";root.SetActive(true);return true;
            }
            catch(Exception error)
            {
                Failure020=error.Message;ready=false;
                if(cleanupBlocked)Hide020();
                else if(!TryDispose020(out var cleanup))Failure020=Failure020+"; retained cleanup: "+cleanup;
                return false;
            }
        }
        void CompleteWriter()
        {
            if(!scheduled)return;
            try{writer.Complete();scheduled=false;writer=default;}
            catch{cleanupBlocked=true;throw;}
        }
        public bool TryCopyContacts020(NativeArray<CampaignGardenPlantContact020> destination,in CampaignGardenFoliageScope020 actual)
        {
            if(!Ready020||!scope.Matches(actual)||!destination.IsCreated||destination.Length!=128||!contacts.IsCreated)return false;
            CompleteWriter();if(!OwnsCurrent()||!scope.Matches(actual))return false;
            NativeArray<CampaignGardenPlantContact020>.Copy(contacts,destination,128);
            return OwnsCurrent()&&scope.Matches(actual);
        }
        public bool TickPresentation020(in CampaignGardenFoliageScope020 actual,float heldSeconds,float wind,bool motion,bool held,int actualQuality,
            Vector4 brush0,Vector2 vertical0,Vector4 brush1,Vector2 vertical1,ulong sequence)
        {
            if(!Ready020||!scope.Matches(actual)||!float.IsFinite(heldSeconds)||heldSeconds<0f||heldSeconds>=4096f||!float.IsFinite(wind))return false;
            if(!held&&sequence>brushSequence&&ValidBrush(brush0,vertical0)&&ValidBrush(brush1,vertical1))
            {heldBrush0=brush0;heldBrush1=brush1;heldVertical0=new Vector4(vertical0.x,vertical0.y,0f,0f);heldVertical1=new Vector4(vertical1.x,vertical1.y,0f,0f);brushSequence=sequence;}
            //800 complete cycles/4096s; secondary.73 frequency is584 cycles, so both close on the shared clock wrap.
            material.SetFloat(ClockId,heldSeconds*(math.PI*2f*800f/4096f));material.SetFloat(WindId,Mathf.Clamp01(wind));material.SetFloat(MotionId,motion?1f:0f);
            material.SetVector(Brush0Id,heldBrush0);material.SetVector(Brush1Id,heldBrush1);material.SetVector(BrushY0Id,heldVertical0);material.SetVector(BrushY1Id,heldVertical1);
            int next=Mathf.Clamp(actualQuality,0,2);
            if(next!=quality){quality=next;for(int sector=0;sector<2;sector++)groups[sector].ForceLOD(quality==0?2:quality==1?1:-1);}
            return OwnsCurrent();
        }
        static bool ValidBrush(Vector4 b,Vector2 y)=>float.IsFinite(b.x)&&float.IsFinite(b.y)&&float.IsFinite(b.z)&&float.IsFinite(b.w)&&
            float.IsFinite(y.x)&&float.IsFinite(y.y)&&b.z>=0f&&b.z<=.5f&&b.w>=0f&&b.w<=1f&&y.y>=0f&&y.y<=2f&&(b.w==0f||y.y>=b.z*2f);
        public void Hide020(){ready=false;if(root!=null)root.SetActive(false);}
        void WorldChanged(DungeonWorld source){if(!ReferenceEquals(source,world)||!OwnsCurrent())Hide020();}
        static void Release<T>(ref NativeArray<T> value,ref Exception error) where T:unmanaged
        {if(!value.IsCreated)return;try{value.Dispose();value=default;}catch(Exception failure){if(error==null)error=failure;}}
        bool TryReleaseScratch(out string failureMessage)
        {
            failureMessage="";
            try{CompleteWriter();}catch(Exception error){failureMessage=error.Message;cleanupBlocked=true;return false;}
            Exception failed=null;Release(ref rays,ref failed);Release(ref floorPoses,ref failed);Release(ref vertices,ref failed);Release(ref indices,ref failed);
            Release(ref vertexCounts,ref failed);Release(ref indexCounts,ref failed);Release(ref failure,ref failed);
            if(failed!=null){failureMessage=failed.Message;cleanupBlocked=true;return false;}return true;
        }
        static void DestroyOwned(UnityEngine.Object value){if(value==null)return;if(Application.isPlaying)UnityEngine.Object.Destroy(value);else UnityEngine.Object.DestroyImmediate(value);}
        public bool TryDispose020(out string failureMessage)
        {
            failureMessage="";Hide020();
            if(subscribed&&!ReferenceEquals(world,null)){world.WorldModeChanged020-=WorldChanged;subscribed=false;}
            if(!TryReleaseScratch(out failureMessage))return false;
            Exception failed=null;Release(ref contacts,ref failed);Release(ref anchors,ref failed);
            if(failed!=null){failureMessage=failed.Message;cleanupBlocked=true;return false;}
            bool had=root!=null||material!=null;
            for(int i=0;i<6;i++)
            {
                if(renderers[i]!=null){renderers[i].sharedMaterial=null;renderers[i].GetComponent<MeshFilter>().sharedMesh=null;}
                had|=meshes[i]!=null;DestroyOwned(meshes[i]);meshes[i]=null;renderers[i]=null;
            }
            DestroyOwned(root);DestroyOwned(material);root=null;material=null;groups[0]=groups[1]=null;
            if(had&&Application.isPlaying)retiredFrame=Time.frameCount;
            session=null;world=null;worldRoot=null;scope=default;disposed=true;cleanupBlocked=false;return true;
        }
        public void Dispose(){if(!TryDispose020(out var failure))throw new InvalidOperationException("Foliage retirement retained: "+failure);}
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Identity current-world/root transform; immutable world-space vertices and rooted+.06m bed/cornerfloor geometry, full shader-swept2m bounds.
// Array Formats:16 persistent supported anchors/128 contacts,69 floor poses/144 rays and bounded mesh scratch; six immutable64B meshes, two sector LODGroups, one owned opaque material and two true capsule uniforms. No per-clump object, collider, compute buffer or frame mesh upload.
// Dependencies/Pointers: Exact session/world/root/token+epoch/generation/world/geometry/foliage scope gates every copy/tick/publication. CPU writer completion/disposal failures retain all remaining handles and prevent replacement. Mesh/material lifetime uses Unity-owned immutable resources and deferred Destroy after renderer unbinding; no CPU storage is reused as a GPU stream and no frame delay is claimed to prove GPU fence completion. Root owns native/import/graphics/GC/device acceptance.
