using System;
using OdysseyOracle.Foundation.World;
using OdysseyOracle.VisualDressing;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace OdysseyOracle
{
    public sealed partial class DungeonWorld
    {
        private GameObject refugeRaykonAlcoveRoot;
        public bool RefugeRaykonAlcoveReady=>refugeRaykonAlcoveRoot!=null&&refugeRaykonAlcoveRoot.activeInHierarchy;
        public int RefugeRaykonAlcovePurpleLightCount{get;private set;}
        private void BuildRefugeRaykonAlcove(Vector3 raykonFeet)
        {
            if(refugeRaykonAlcoveRoot!=null||tutorialRefuge==null||refugeRaykonMaterial==null)return;
            Vector3 south=new Vector3(-tutorialRefuge.OuterX+.45f,raykonFeet.y,raykonFeet.z-1.65f);
            Vector3 north=new Vector3(south.x,raykonFeet.y,raykonFeet.z+1.65f);
            CheckRaykonCaseSite(south,raykonFeet);CheckRaykonCaseSite(north,raykonFeet);
            CheckRaykonCaseFloor(south,north);
            Mesh first=null,second=null;GameObject owner=null;
            try
            {
                first=RaykonAlcoveMeshBuilder.Build(0);second=RaykonAlcoveMeshBuilder.Build(1);
                owner=new GameObject("Original Raykon supported library alcove");owner.transform.SetParent(geometryRoot,false);
                var southRenderer=AttachRaykonCase(owner.transform,south,first,"Raykon south original bookcase");
                RegisterRoomReactionTorch(southRenderer,south+new Vector3(.55f,2.41f,0),0);
                var northRenderer=AttachRaykonCase(owner.transform,north,second,"Raykon north original bookcase");
                RegisterRoomReactionTorch(northRenderer,north+new Vector3(.55f,2.41f,0),1);
                // Existing World factory owns light setup/pulse registry. No second light controller.
                // Lack of two fixed registry slots keeps the purple emissive fixtures only.
                if(spatialPulseCount<=MaximumSpatialPulseLights-2)
                {
                    Light a=PointLight("Raykon south localized purple torch",south+new Vector3(.55f,2.41f,0),new Color(.63f,.12f,.94f),3.2f,1.1f,false);
                    a.transform.SetParent(owner.transform,true);
                    Light b=PointLight("Raykon north localized purple torch",north+new Vector3(.55f,2.41f,0),new Color(.63f,.12f,.94f),3.2f,1.1f,false);
                    b.transform.SetParent(owner.transform,true);RefugeRaykonAlcovePurpleLightCount=2;
                }
                resources.Add(first);resources.Add(second);refugeRaykonAlcoveRoot=owner;
            }
            catch
            {
                if(owner!=null){owner.SetActive(false);ReleaseRaykonAlcoveObject(owner);}
                if(first!=null){resources.Remove(first);ReleaseRaykonAlcoveObject(first);}
                if(second!=null){resources.Remove(second);ReleaseRaykonAlcoveObject(second);}
                RefugeRaykonAlcovePurpleLightCount=0;throw;
            }
        }
        private MeshRenderer AttachRaykonCase(Transform parent,Vector3 feet,Mesh mesh,string name)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
            go.transform.localPosition=feet;go.transform.localRotation=Quaternion.Euler(0,90,0);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;MeshRenderer renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=refugeRaykonMaterial;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=true;
            var solid=go.AddComponent<UnityEngine.BoxCollider>();solid.center=new Vector3(0,1.4f,0);solid.size=new Vector3(1.4f,2.8f,.5f);solid.isTrigger=false;return renderer;
        }
        private void CheckRaykonCaseSite(Vector3 feet,Vector3 raykon)
        {
            // Exact yaw90 transformation of the declared complete local recipe AABB.
            Vector3 a=feet+new Vector3(-.25f,0,-.7f),b=feet+new Vector3(.55f,2.8f,.7f);
            bool contained=false;
            for(int i=0;i<tutorialRefuge.Rooms.Length;i++)
            {
                var r=tutorialRefuge.Rooms[i];
                if(a.x+.0001f>=r.MinU+.2f&&b.x-.0001f<=r.MaxU-.2f&&a.z+.0001f>=r.MinV+.2f&&b.z-.0001f<=r.MaxV-.2f){contained=true;break;}
            }
            if(!contained||b.y>tutorialRefuge.CeilingY-.2f)throw new InvalidOperationException("Raykon case lacks full room/wall clearance.");
            for(int i=0;i<tutorialRefuge.PropPlacements.Length;i++)
            {
                var obstacle=RefugePlacementPolicy.At(tutorialRefuge.PropPlacements[i]);
                if(Overlap(a,b,new Vector3(obstacle.Min.X,obstacle.Min.Y,obstacle.Min.Z),new Vector3(obstacle.Max.X,obstacle.Max.Y,obstacle.Max.Z)))
                    throw new InvalidOperationException("Raykon case overlaps a saved library prop.");
            }
            Vector3 table=raykon+new Vector3(-.15f,0,1.55f);
            if(Overlap(a,b,table+new Vector3(-.75f,0,-.35f),table+new Vector3(.75f,1.29f,.35f))||
               Overlap(a,b,raykon+new Vector3(-.5f,0,-1.5f),raykon+new Vector3(3f,3.3f,1.5f)))
                throw new InvalidOperationException("Raykon case overlaps original table or actor/talk sweep.");
            // Corridor and talk approach are visual/collision keepouts, even though these sites are farther west.
            if(a.x< -1.45f&&b.x> -2.55f||
               Overlap(a,b,new Vector3(Mathf.Min(-2,raykon.x+2.2f),0,raykon.z-.55f),new Vector3(Mathf.Max(-2,raykon.x+2.2f),3,raykon.z+.55f))||
               Overlap(a,b,raykon+new Vector3(0,0,-.55f),raykon+new Vector3(2.2f,3,.55f)))
                throw new InvalidOperationException("Raykon case enters reserved walking/talk lanes.");
        }
        private static bool Overlap(Vector3 a,Vector3 b,Vector3 c,Vector3 d)=>a.x<d.x&&b.x>c.x&&a.y<d.y&&b.y>c.y&&a.z<d.z&&b.z>c.z;
        private void CheckRaykonCaseFloor(Vector3 south,Vector3 north)
        {
            NativeArray<float4> rectangles=default;NativeArray<RefugeFloorQuery32> queries=default;
            NativeArray<ulong> occupancy=default;NativeArray<RefugeFloorResult32> results=default;JobHandle fence=default;
            try
            {
                rectangles=new NativeArray<float4>(tutorialRefuge.Rooms.Length,Allocator.TempJob);
                for(int i=0;i<rectangles.Length;i++){var r=tutorialRefuge.Rooms[i];rectangles[i]=new float4(r.MinU,r.MinV,r.MaxU,r.MaxV);}
                queries=new NativeArray<RefugeFloorQuery32>(2,Allocator.TempJob);
                queries[0]=RaykonCaseQuery(south,0);queries[1]=RaykonCaseQuery(north,1);
                fence=RefugeFloorSupportCold.Schedule(rectangles,queries,Allocator.TempJob,out _,out occupancy,out results);fence.Complete();
                if(results[0].Status!=1u||results[0].QueryId!=0u||results[0].Position.y!=south.y||results[1].Status!=1u||results[1].QueryId!=1u||results[1].Position.y!=north.y)
                    throw new InvalidOperationException("Raykon bookcase lacks exact existing numeric floor support.");
            }
            finally{fence.Complete();if(results.IsCreated)results.Dispose();if(occupancy.IsCreated)occupancy.Dispose();if(queries.IsCreated)queries.Dispose();if(rectangles.IsCreated)rectangles.Dispose();}
        }
        private static RefugeFloorQuery32 RaykonCaseQuery(Vector3 site,uint id)=>new RefugeFloorQuery32{
            Origin=new float3(site.x+.15f,site.y+2,site.z),MaximumDistance=4,HalfFootprintXZ=new float2(.4f,.7f),Flags=RefugeFloorSupportMath.CheckFootprintCorners,QueryId=id};
        private void DisposeRefugeRaykonAlcove()
        {
            // Existing World geometry hierarchy and resources list own destruction.
            if(refugeRaykonAlcoveRoot!=null)refugeRaykonAlcoveRoot.SetActive(false);
            refugeRaykonAlcoveRoot=null;RefugeRaykonAlcovePurpleLightCount=0;
        }
        private static void ReleaseRaykonAlcoveObject(UnityEngine.Object value)
        {if(Application.isPlaying)UnityEngine.Object.Destroy(value);else UnityEngine.Object.DestroyImmediate(value);}
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Actual supported Raykon feet/current OuterX; two local cases yaw90 face +X, exact floor Y and full recipe AABB margins. Keep x=-2±.55 and z≈8 approach/talk lanes clear.
// Array Formats: Two recipe meshes/renderers/case colliders; completed/disposed TempJob room float4/query32/result32[2]/Morton occupancy. At most two range3.2 shadowless purple lights through existing World32 pulse registry.
// Dependencies/Pointers: Existing World geometry/resources/PointLight own all lifetime and spatial light attenuation. Borrow original vertex-color/glow material; no new Update, sky/ambient/fog wash, saved prop/model rewrite, audio or quest authority. Native visual/clearance acceptance remains root-owned.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing refuge geometry/world axes and support pivots unchanged; elemental numeric state applies only to the supported refuge.
// Array Formats: Existing owner arrays remain canonical; fixed512-node/8word reaction state supplies bounded scalar presentation gains.
// Dependencies/Pointers: Host accepted single Fire/Earth/Wind shots only; existing Object adapters retain materials/lights/lifetime, no rewards/saves or shrine/portal gameplay mutation.
