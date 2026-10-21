using OdysseyOracle.Foundation.Presentation;
using OdysseyOracle.Foundation.World;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class DungeonWorld
    {
        readonly MeshRenderer[] refugeTrailRenderers=new MeshRenderer[4];
        readonly Material[] refugeTrailMaterials=new Material[4];
        int activeRefugeTrail=-1,refugeTrailMask;bool refugePracticeTrailsAvailable;
        public int AvailableRefugeTrailMask=>refugeTrailMask;
        public bool HasRefugeNavigationTrail(int station)=>station>=0&&station<4&&(refugeTrailMask&(1<<station))!=0;
        static Color RefugeTrailColor(int station)=>station==0?new Color(.93f,.44f,.32f):station==1?new Color(.78f,.48f,1f):station==2?new Color(.30f,.73f,.96f):new Color(.95f,.75f,.32f);

        // Cold readiness/geometry-change adapter called by the existing HUD anchor snapshot.
        // This never creates an actor, control, collider, light, room or save owner.
        // Retained compatibility API. Human selected one HUD compass instead of floor arrows.
        // Original recipe remains preserved below; no gameplay caller can publish it here.
        public void EnsureRefugeNavigation(NpcBlacksmith smith)
        {
            for(int i=0;i<4;i++)if(refugeTrailRenderers[i]!=null)refugeTrailRenderers[i].enabled=false;
            activeRefugeTrail=-1;refugeTrailMask=0;refugePracticeTrailsAvailable=false;
        }
        void EnsureRefugeTrail(int station,Vector3 endpoint,bool ready)
        {
            if(!ready||HasRefugeNavigationTrail(station)||!RefugeTrailMath.ValidEndpoint(station,new float2(endpoint.x,endpoint.z)))return;
            NativeArray<float4> rooms=default,frames=default;NativeArray<RefugeFloorQuery32> queries=default;
            NativeArray<ulong> occupancy=default;NativeArray<RefugeFloorResult32> supports=default;NativeArray<int> count=default,valid=default;
            NativeList<float3> vertices=default,normals=default;NativeList<float2> uvs=default;NativeList<int> indices=default;JobHandle fence=default;
            try
            {
                rooms=new NativeArray<float4>(tutorialRefuge.Rooms.Length,Allocator.TempJob);
                for(int i=0;i<rooms.Length;i++){var r=tutorialRefuge.Rooms[i];rooms[i]=new float4(r.MinU,r.MinV,r.MaxU,r.MaxV);}
                frames=new NativeArray<float4>(RefugeTrailMath.MaximumMarkers,Allocator.TempJob);
                queries=new NativeArray<RefugeFloorQuery32>(RefugeTrailMath.MaximumMarkers,Allocator.TempJob);
                count=new NativeArray<int>(1,Allocator.TempJob);valid=new NativeArray<int>(1,Allocator.TempJob);
                fence=new RefugeTrailLayoutJob{Station=station,Endpoint=new float2(endpoint.x,endpoint.z),Frames=frames,Queries=queries,Count=count}.Schedule();fence.Complete();
                int markers=count[0];if(markers<1)return;
                fence=RefugeFloorSupportCold.Schedule(rooms,queries.GetSubArray(0,markers),Allocator.TempJob,out _,out occupancy,out supports);
                vertices=new NativeList<float3>(markers*RefugeTrailMath.VerticesPerMarker,Allocator.TempJob);
                normals=new NativeList<float3>(markers*RefugeTrailMath.VerticesPerMarker,Allocator.TempJob);
                uvs=new NativeList<float2>(markers*RefugeTrailMath.VerticesPerMarker,Allocator.TempJob);
                indices=new NativeList<int>(markers*RefugeTrailMath.IndicesPerMarker,Allocator.TempJob);
                fence=new RefugeTrailMeshJob{Frames=frames,Supports=supports,Vertices=vertices,Normals=normals,Uvs=uvs,Indices=indices,Valid=valid}.Schedule(fence);fence.Complete();
                if(valid[0]!=1)return;
                Shader shader=Shader.Find("Universal Render Pipeline/Lit");if(shader==null)return;
                var material=new Material(shader){name="Original refuge navigation arrows "+station};resources.Add(material);
                material.SetFloat("_Smoothness",.08f);material.EnableKeyword("_EMISSION");material.globalIlluminationFlags=MaterialGlobalIlluminationFlags.None;
                var mesh=new Mesh{name="Original cold supported trail "+station};resources.Add(mesh);
                mesh.SetVertices(vertices.AsArray());mesh.SetNormals(normals.AsArray());mesh.SetUVs(0,uvs.AsArray());
                mesh.SetIndices(indices.AsArray(),UnityEngine.MeshTopology.Triangles,0);mesh.RecalculateBounds();mesh.UploadMeshData(true);
                var owner=new GameObject("Original floor direction trail "+station,typeof(MeshFilter),typeof(MeshRenderer));owner.transform.SetParent(geometryRoot,false);
                owner.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=owner.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;
                renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
                renderer.lightProbeUsage=UnityEngine.Rendering.LightProbeUsage.Off;renderer.reflectionProbeUsage=UnityEngine.Rendering.ReflectionProbeUsage.Off;
                refugeTrailRenderers[station]=renderer;refugeTrailMaterials[station]=material;refugeTrailMask|=1<<station;
                ApplyRefugeTrailPresentation(station);
            }
            finally
            {
                fence.Complete();if(indices.IsCreated)indices.Dispose();if(uvs.IsCreated)uvs.Dispose();if(normals.IsCreated)normals.Dispose();if(vertices.IsCreated)vertices.Dispose();
                if(valid.IsCreated)valid.Dispose();if(count.IsCreated)count.Dispose();if(supports.IsCreated)supports.Dispose();if(occupancy.IsCreated)occupancy.Dispose();
                if(queries.IsCreated)queries.Dispose();if(frames.IsCreated)frames.Dispose();if(rooms.IsCreated)rooms.Dispose();
            }
        }
        public void SetActiveRefugeNavigationTrail(int station)=>EnsureRefugeNavigation(null);
        public void SetRefugePracticeNavigationAvailable(bool available)=>EnsureRefugeNavigation(null);
        void ApplyRefugeTrailPresentation(int station)
        {
            var renderer=refugeTrailRenderers[station];var material=refugeTrailMaterials[station];if(renderer==null||material==null)return;
            bool active=activeRefugeTrail==station;renderer.enabled=active||station==1||station==3||refugePracticeTrailsAvailable;
            Color color=RefugeTrailColor(station);material.SetColor("_BaseColor",color*(active ? .7f : .24f));
            material.SetColor("_EmissionColor",color*(active ? .28f : .055f));
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: sole World common Y-up metres, actual supported Raykon/Anvil/pad approaches; four parallel arrow lanes stay inside x=-2±.55, avoid central Shrine; marker bottoms exactlyfloorY0.
// Array Formats: maximum4 cold combined meshes/renderers/materials,96markers each,13440verts/19584indices total ceiling; existing rectangle->Morton support job, bounded temporary arrays/lists completed/disposed in finally.
// Dependencies/Pointers: World resources/geometryRoot alone own main-thread meshes/materials; no per-marker GameObjects/colliders/lights. Actual cold support established, native obstacle/walk/render/GC acceptance remains root's task. No gameplay/control/save/audio mutation; original procedural arrow recipe.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: existing World/Canvas; one objective compass replaces floor-arrow publication, actual map legend retained.
// Array Formats: one bounded HUD/native store with fenced disposal; preserved original floor recipe stays inactive.
// Dependencies/Pointers: original tutorial/campaign/physics/input authorities unchanged; source-only integration/native/browser acceptance pending.
