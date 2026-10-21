using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
namespace OdysseyOracle
{
    public readonly struct PortalLandingWorldSnapshot020
    {
        public readonly Transform Root;
        public readonly Vector3 SpawnFeet,Shrine,HomePortal;
        public readonly float SpawnYaw;
        public readonly Rect[] MapRooms;
        public readonly Rect MapBounds;
        public readonly uint Generation;
        public PortalLandingWorldSnapshot020(Transform root,Vector3 spawn,Vector3 shrine,Vector3 portal,float yaw,Rect[] rooms,Rect bounds,uint generation)
        {Root=root;SpawnFeet=spawn;Shrine=shrine;HomePortal=portal;SpawnYaw=yaw;MapRooms=rooms;MapBounds=bounds;Generation=generation;}
    }
    public sealed partial class DungeonWorld
    {
        Transform portalLandingRoot020;
        Mesh portalLandingMesh020;
        Material portalLandingSurface020;
        readonly UnityEngine.BoxCollider[] portalLandingGates020=new UnityEngine.BoxCollider[2];
        Rect[] portalLandingRooms020;
        uint portalLandingGeneration020;
        uint portalLandingHighWater020;
        bool portalLandingReady020;
        int portalLandingLastPlantSupportHits020,portalLandingLastPlantSupportMask020;
        uint portalLandingLastPlantSupportGeneration020;
        public bool PortalLandingPrototypeReady020=>portalLandingReady020&&portalLandingRoot020!=null&&portalLandingMesh020!=null&&portalLandingSurface020!=null;
        public uint PortalLandingPrototypeGeneration020=>portalLandingGeneration020;
        public Material PortalLandingSurfaceMaterial020=>PortalLandingPrototypeReady020?portalLandingSurface020:null;
        public int PortalLandingTorchCount020=>PortalLandingPrototypeGeometry020.TorchCount;
        public int PortalLandingPrototypeVertexCount020 {get;private set;}
        public int PortalLandingPrototypeColliderCount020 {get;private set;}
        public int PortalLandingPrototypeCulledFaces020 {get;private set;}
        public string PortalLandingPrototypeFailure020 {get;private set;}="";

        public bool PreparePortalLandingPrototype020(uint generation)
        {
            if(!Application.isPlaying){PortalLandingPrototypeFailure020="Portal landing preparation requires the running game.";return false;}
            if(PortalLandingScratch020.HasPending){PortalLandingPrototypeFailure020="Landing scratch cleanup is pending; explicit cold RetryPending is required.";return false;}
            if(generation==0||generation<portalLandingGeneration020||(!PortalLandingPrototypeReady020&&generation<=portalLandingHighWater020))return false;
            if(PortalLandingPrototypeReady020)
            {
                if(generation==portalLandingGeneration020)return true;
                if(portalLandingRoot020.gameObject.activeSelf){PortalLandingPrototypeFailure020="The active landing must be retired before a new geometry generation is prepared.";return false;}
                HidePortalLandingPrototype020();RebindCampaignAssetGeneration020(generation);RebindSupportedFootstepRegistry020(generation);portalLandingGeneration020=portalLandingHighWater020=generation;PortalLandingPrototypeFailure020="";return true;
            }
            if(transform.position.sqrMagnitude>1e-8f||Quaternion.Angle(transform.rotation,Quaternion.identity)>.001f||(transform.lossyScale-Vector3.one).sqrMagnitude>1e-8f)
            {PortalLandingPrototypeFailure020="Portal landing requires a stationary unit World frame.";return false;}
            DisposePortalLandingPrototype020();
            portalLandingHighWater020=generation;
            NativeArray<LandingDraw020> draws=default;NativeArray<int> count=default,written=default,indices=default;
            NativeArray<float3> extents=default;NativeArray<LandingVertex020> vertices=default;NativeArray<LandingStandQuery020> support=default;NativeArray<LandingStandResult020> supported=default;JobHandle fence=default;
            NativeArray<CampaignPlantSupportRay020> plantRays=default;
            NativeArray<CampaignPlantPatchAdmission020> plantAdmissions=default;
            NativeArray<int> assetFloorSpan=default;
            if(!PortalLandingScratch020.TryBeginBuild()){PortalLandingPrototypeFailure020="Another landing scratch scope is building or awaiting cold cleanup.";return false;}
            try
            {
                var shader=Resources.Load<Shader>("Shaders/PortalLandingSurface020");
                if(shader==null||!shader.isSupported)throw new InvalidOperationException("The owned portal-landing surface shader is not ready.");
                draws=new NativeArray<LandingDraw020>(PortalLandingPrototypeGeometry020.MaximumDraws,Allocator.TempJob);
                extents=new NativeArray<float3>(2,Allocator.TempJob);
                count=new NativeArray<int>(1,Allocator.TempJob);written=new NativeArray<int>(3,Allocator.TempJob);
                vertices=new NativeArray<LandingVertex020>(PortalLandingPrototypeGeometry020.MaximumVertices,Allocator.TempJob);
                indices=new NativeArray<int>(PortalLandingPrototypeGeometry020.MaximumIndices,Allocator.TempJob);
                // Numeric selection completes before any native support query.
                plantRays=new NativeArray<CampaignPlantSupportRay020>(CampaignPlantedForecourtRecipe020.PatchCount*9,Allocator.TempJob);
                plantAdmissions=new NativeArray<CampaignPlantPatchAdmission020>(CampaignPlantedForecourtRecipe020.PatchCount,Allocator.TempJob);
                fence=new CampaignPlantSupportSelectionJob020{Rays=plantRays}.Schedule(plantRays.Length,9);fence.Complete();
                var node=new GameObject("Isolated original PortalLanding020 prototype");node.SetActive(false);node.transform.SetParent(transform,false);portalLandingRoot020=node.transform;
                PrepareSupportedFootstepRegistry020(node.transform,generation);
                var floorColliders=new UnityEngine.BoxCollider[PortalLandingLayout020.SurfaceCount];
                portalLandingRooms020=new Rect[PortalLandingLayout020.SurfaceCount];
                for(int i=0;i<PortalLandingLayout020.SurfaceCount;i++)
                {
                    if(!PortalLandingLayout020.TrySurface(i,out var floor))throw new InvalidOperationException("The bootstrap floor stream is incomplete.");
                    floorColliders[i]=CreatePortalLandingCollider020(floor);floorColliders[i].gameObject.layer=2;
                    float x=(floor.MinX+floor.MaxX)*.5f,z=(floor.MinZ+floor.MaxZ)*.5f,hx=(floor.MaxX-floor.MinX)*.5f,hz=(floor.MaxZ-floor.MinZ)*.5f;
                    float ax=Mathf.Abs(floor.Cos)*hx+Mathf.Abs(floor.Sin)*hz,az=Mathf.Abs(floor.Sin)*hx+Mathf.Abs(floor.Cos)*hz;
                    portalLandingRooms020[i]=new Rect(x-ax,z-az,ax*2,az*2);
                }
                ulong plantWorldToken=CampaignPlantNativeSupport020.WorldToken(node);
                uint plantWorldRevision=(uint)PortalLandingLayout020.Version,plantGeometryRevision=CampaignLandingObstacleSet020.Revision;
                // No renderer, Ready publication, simulation, callback or yield exists in this short bootstrap phase.
                // IgnoreRaycast excludes ordinary default queries; direct Collider.Raycast addresses only our exact floors.
                node.layer=2;node.SetActive(true);Physics.SyncTransforms();
                portalLandingLastPlantSupportGeneration020=generation;
                if(!CampaignPlantNativeSupport020.CollectExactFloors(floorColliders,plantRays,plantAdmissions,plantWorldToken,
                    generation,plantWorldRevision,plantGeometryRevision,out portalLandingLastPlantSupportHits020,
                    out portalLandingLastPlantSupportMask020,out var plantFailure))throw new InvalidOperationException(plantFailure);
                PrepareCampaignAssets020(node.transform,floorColliders,generation);
                // Every scheduled NativeContainer is valid even when the optional catalogue is absent.
                assetFloorSpan=new NativeArray<int>(1,Allocator.TempJob);
                node.SetActive(false);Physics.SyncTransforms();node.layer=0;
                for(int i=0;i<floorColliders.Length;i++)floorColliders[i].gameObject.layer=0;
                fence=new PortalLandingRecipeJob020{Draws=draws,Count=count,PlantAdmissions=plantAdmissions,
                    PlantWorldToken=plantWorldToken,PlantGeneration=generation,PlantWorldRevision=plantWorldRevision,
                    PlantGeometryRevision=plantGeometryRevision}.Schedule();fence.Complete();
                if(count[0]<1||count[0]>draws.Length)throw new InvalidOperationException("Portal landing recipe exceeded its bounded command store.");
                fence=new PortalLandingMeshJob020{Draws=draws,Count=count[0],Vertices=vertices,Indices=indices,Written=written,Extents=extents,AssetFloorIndex=CampaignAssetFloorIndex020,AssetFloorSpan=assetFloorSpan}.Schedule();fence.Complete();
                if(written[0]<4||written[0]>vertices.Length||written[1]<6||written[1]>indices.Length)throw new InvalidOperationException("Portal landing mesh did not publish bounded output.");
                support=new NativeArray<LandingStandQuery020>(PortalLandingLayout020.AnchorCount,Allocator.TempJob);
                supported=new NativeArray<LandingStandResult020>(support.Length,Allocator.TempJob);
                for(int i=0;i<support.Length;i++){PortalLandingLayout020.TryAnchor(i,out var feet,out _);support[i]=new LandingStandQuery020{Feet=feet,Radius=PortalLandingLayout020.BodyRadius};}
                fence=new PortalLandingStandJob020{Admission=new LandingAdmission020{Kind=PortalLandingLayout020.ContextKind,Generation=generation,StoryRevision=1,Ready=1},Queries=support,Results=supported}.Schedule(support.Length,1);fence.Complete();
                for(int i=0;i<supported.Length;i++)if(supported[i].Supported!=1)throw new InvalidOperationException("Portal landing has an unsupported canonical anchor.");

                portalLandingSurface020=new Material(shader){name="Owned original portal landing surface020"};
                portalLandingMesh020=new Mesh{name="Bounded original PortalLanding020 combined mesh",indexFormat=IndexFormat.UInt32};
                portalLandingMesh020.SetVertexBufferParams(written[0],new VertexAttributeDescriptor(VertexAttribute.Position,VertexAttributeFormat.Float32,3),new VertexAttributeDescriptor(VertexAttribute.Normal,VertexAttributeFormat.Float32,3),new VertexAttributeDescriptor(VertexAttribute.Color,VertexAttributeFormat.Float32,4),new VertexAttributeDescriptor(VertexAttribute.TexCoord0,VertexAttributeFormat.Float32,2));
                portalLandingMesh020.SetVertexBufferData(vertices,0,0,written[0],0,MeshUpdateFlags.DontRecalculateBounds);
                portalLandingMesh020.SetIndexBufferParams(written[1],IndexFormat.UInt32);portalLandingMesh020.SetIndexBufferData(indices,0,0,written[1],MeshUpdateFlags.DontRecalculateBounds);
                portalLandingMesh020.subMeshCount=1;portalLandingMesh020.SetSubMesh(0,new SubMeshDescriptor(0,written[1],MeshTopology.Triangles),MeshUpdateFlags.DontRecalculateBounds);
                if(!math.all(math.isfinite(extents[0]))||!math.all(math.isfinite(extents[1]))||math.any(extents[1]<extents[0]))throw new InvalidOperationException("Landing mesh bounds are invalid.");
                portalLandingMesh020.bounds=new Bounds((Vector3)((extents[0]+extents[1])*.5f),(Vector3)(extents[1]-extents[0]+new float3(.02f)));portalLandingMesh020.UploadMeshData(true);
                var filter=node.AddComponent<MeshFilter>();filter.sharedMesh=portalLandingMesh020;var renderer=node.AddComponent<MeshRenderer>();renderer.sharedMaterial=portalLandingSurface020;
                renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
                PrepareCampaignAssetMask020(filter,vertices,indices,written[0],written[1],assetFloorSpan.IsCreated?assetFloorSpan[0]:-1,ref fence);
                for(int i=0;i<PortalLandingLayout020.ObstacleCount;i++){PortalLandingLayout020.TryObstacle(i,out var solid);var collider=CreatePortalLandingCollider020(solid);if(solid.Kind==3)portalLandingGates020[solid.Endpoint-1]=collider;}
                SealSupportedFootstepRegistry020();
                if(PortalLandingPrototypeColliderCount020!=PortalLandingLayout020.SurfaceCount+PortalLandingLayout020.ObstacleCount||portalLandingGates020[0]==null||portalLandingGates020[1]==null)throw new InvalidOperationException("Portal landing collider ownership is incomplete.");
                PortalLandingPrototypeVertexCount020=written[0];PortalLandingPrototypeCulledFaces020=written[2];portalLandingGeneration020=generation;portalLandingReady020=true;PortalLandingPrototypeFailure020="";
            }
            catch(Exception error){DisposePortalLandingPrototype020();PortalLandingPrototypeFailure020=error.Message;}
            finally
            {
                var scratch=new PortalLandingScratch020(ref draws,ref count,ref written,ref indices,ref vertices,ref extents,ref support,ref supported,ref plantRays,ref plantAdmissions,ref assetFloorSpan,ref fence);
                bool released=scratch.TryRelease(out var cleanupError);
                PortalLandingScratch020.FinishBuild(ref scratch);
                if(!released){DisposePortalLandingPrototype020();PortalLandingPrototypeFailure020=cleanupError!=null?cleanupError.Message:"Landing scratch remains pending for explicit cold retry.";}
            }
            return PortalLandingPrototypeReady020;
        }

        UnityEngine.BoxCollider CreatePortalLandingCollider020(in LandingBox020 box)
        {
            var node=new GameObject(box.Kind==1?"Continuous supported floor020":box.Kind==3?"Fixed endpoint gate020":"Closed landing solid020");node.transform.SetParent(portalLandingRoot020,false);
            node.transform.localPosition=new Vector3((box.MinX+box.MaxX)*.5f,(box.MinY+box.MaxY)*.5f,(box.MinZ+box.MaxZ)*.5f);node.transform.localRotation=Quaternion.Euler(0,box.Yaw,0);
            var collider=node.AddComponent<UnityEngine.BoxCollider>();collider.size=new Vector3(box.MaxX-box.MinX,box.MaxY-box.MinY,box.MaxZ-box.MinZ);collider.isTrigger=false;PortalLandingPrototypeColliderCount020++;RecordSupportedFootstepCollider020(collider,box);return collider;
        }
        public bool TryGetPortalLandingPrototype020(uint generation,out PortalLandingWorldSnapshot020 snapshot)
        {
            snapshot=default;if(!PortalLandingPrototypeReady020||generation!=portalLandingGeneration020)return false;
            PortalLandingLayout020.TryAnchor(0,out var feet,out var yaw);
            snapshot=new PortalLandingWorldSnapshot020(portalLandingRoot020,new Vector3(feet.X,feet.Y,feet.Z),new Vector3(-30*.9510565f,0,-30*.309017f),new Vector3(0,6,50),yaw,portalLandingRooms020,new Rect(-98,-86,196,142),generation);return true;
        }
        public bool TryGetPortalLandingTorch020(int index,out Vector3 position)
        {position=default;if(!PortalLandingPrototypeReady020||!PortalLandingPrototypeGeometry020.TryTorch(index,out var p))return false;position=new Vector3(p.x,p.y,p.z);return true;}
        public bool TryGetPortalLandingCapture020(int angleIndex,out Vector3 eye,out Vector3 target)
        {
            eye=target=default;if(!PortalLandingPrototypeReady020||angleIndex<0||angleIndex>9)return false;
            eye=new Vector3(0,1.65f,6);float yaw=angleIndex*36f*Mathf.Deg2Rad;target=eye+new Vector3(Mathf.Sin(yaw)*40,angleIndex==0?8:0,Mathf.Cos(yaw)*40);return true;
        }
        public void SynchronizePortalLandingGates020(in LandingAdmission020 admission,in LandingTravelState020 travel,double now)
        {
            for(int i=0;i<2;i++)if(portalLandingGates020[i]!=null)portalLandingGates020[i].enabled=!PortalLandingPrototypeReady020||admission.Generation!=portalLandingGeneration020||PortalLandingTravel020.GateClosed(travel,admission,i+1,now);
        }
        public void HidePortalLandingPrototype020()
        {for(int i=0;i<2;i++)if(portalLandingGates020[i]!=null)portalLandingGates020[i].enabled=true;if(portalLandingRoot020!=null)portalLandingRoot020.gameObject.SetActive(false);HideCampaignAssets020();}
        public void DisposePortalLandingPrototype020()
        {
            DisposeSupportedFootstepRegistry020();RetireCampaignAssets020();HidePortalLandingPrototype020();portalLandingReady020=false;portalLandingGeneration020=0;
            if(portalLandingRoot020!=null)Destroy(portalLandingRoot020.gameObject);if(portalLandingMesh020!=null)Destroy(portalLandingMesh020);if(portalLandingSurface020!=null)Destroy(portalLandingSurface020);
            portalLandingRoot020=null;portalLandingMesh020=null;portalLandingSurface020=null;portalLandingRooms020=null;
            portalLandingGates020[0]=portalLandingGates020[1]=null;PortalLandingPrototypeVertexCount020=PortalLandingPrototypeColliderCount020=PortalLandingPrototypeCulledFaces020=0;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Stationary unit World-local=world metre/Y-up prototype, positive ready generation; first-person capture headings0..324 clockwise.
// Array Formats: One owned immutable base mesh/material/inactive root plus optional catalogue lease/masked base;231 bounded simple collider nodes before street integration, two borrowed inner-gate refs,69 conservative drawing rectangles and completed/disposed native scratch.
// Dependencies/Pointers: Exact owned69-floor bootstrap collects72 native support rays without Physics.Simulate/yield/Ready publication before Recipe; failure hides and destroys the same root. Root third-mode owner publishes snapshot/root/mode/actors/maps together; borrowed map bounds confer no support authority. Optional catalogue prepares351native floor samples inside the same unpublished bootstrap; a one-int floor span joins scratch ownership and original base remains rollback authority. Cold preparation reserves one scratch scope before allocation; finally transfers ten native arrays/fence into a value owner, retains failures across World teardown, blocks replacement and requires explicit cold RetryPending. Generation high-water, gate closure and owned mesh/material/root disposal remain unchanged; actual native/physics/pixels/device acceptance remains separate.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing immutable69floor/162obstacle root and actual collider identities.
// Array Formats: Optional bounded72row support metadata owner cold-registers the same already-created colliders.
// Dependencies/Pointers: New prepare/seal/rebind/retire hooks add no body, floor, renderer or movement rule; original Ready/bootstrap/asset-mask/scratch control flow remains canonical.
