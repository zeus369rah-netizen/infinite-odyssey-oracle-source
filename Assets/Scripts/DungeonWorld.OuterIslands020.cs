using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace OdysseyOracle
{
    public sealed partial class DungeonWorld
    {
        Transform outerIslandRoot020;
        public bool IsOuterIslandPresentation020(Transform value)=>outerIslandRoot020!=null&&value!=null&&value.IsChildOf(outerIslandRoot020);
        void BuildOuterIslands020()
        {
            if(outerIslandRoot020!=null||arenaRoot==null)return;
            var shader=Resources.Load<Shader>("Shaders/OracleMenuIsland015");
            if(shader==null||!shader.isSupported)return;
            // Cold Object adapter mirrors the catalog's environment-local mesh bounds. Never run this scan per frame.
            var renderers=arenaRoot.GetComponentsInChildren<MeshRenderer>(true);
            if(renderers.Length==0||renderers.Length>4096)return;
            Bounds source;
            using(var input=new NativeArray<OuterIslandInput020>(renderers.Length,Allocator.TempJob))
            using(var output=new NativeArray<OuterIslandBounds020>(1,Allocator.TempJob))
            {
                int count=0;var writableInput=input;
                for(int i=0;i<renderers.Length;i++)
                {
                    var renderer=renderers[i];var filter=renderer.GetComponent<MeshFilter>();
                    if(filter==null||filter.sharedMesh==null)continue;
                    var box=filter.sharedMesh.bounds;
                    writableInput[count++]=new OuterIslandInput020{Center=new float3(box.center.x,box.center.y,box.center.z),
                        Extents=new float3(box.extents.x,box.extents.y,box.extents.z),Matrix=OuterMatrix020(renderer.localToWorldMatrix)};
                }
                new OuterIslandBoundsJob020{Input=input,Count=count,RootInverse=OuterMatrix020(arenaRoot.worldToLocalMatrix),
                    Initial=new OuterIslandBounds020{Center=new float3(0,3,0),Extents=new float3(ArenaRadius+.5f,7f,ArenaRadius+.5f)},Output=output}.Schedule().Complete();
                var value=output[0];source=new Bounds(new Vector3(value.Center.x,value.Center.y,value.Center.z),
                    new Vector3(value.Extents.x*2f,value.Extents.y*2f,value.Extents.z*2f));
            }
            if(!OuterIslandGeometry020.TryLayout(source,0f,out var layout,out var bounds))return;
            Mesh mesh=OuterIslandGeometry020.Build(layout,bounds);resources.Add(mesh);
            var material=new Material(shader){name="Original separated floating islands020"};resources.Add(material);
            material.SetFloat("_Daylight",.55f);
            var node=new GameObject("Original six separate floating perimeter islands020");node.transform.SetParent(arenaRoot,false);
            outerIslandRoot020=node.transform;
            node.AddComponent<MeshFilter>().sharedMesh=mesh;
            var target=node.AddComponent<MeshRenderer>();target.sharedMaterial=material;target.shadowCastingMode=ShadowCastingMode.Off;
            target.receiveShadows=false;target.lightProbeUsage=LightProbeUsage.Off;target.reflectionProbeUsage=ReflectionProbeUsage.Off;
            // No Collider, route edge, floor support, path, bridge, voice or authoritative room record is registered.
        }
        static float4x4 OuterMatrix020(Matrix4x4 value)=>new float4x4(
            new float4(value.m00,value.m10,value.m20,value.m30),new float4(value.m01,value.m11,value.m21,value.m31),
            new float4(value.m02,value.m12,value.m22,value.m32),new float4(value.m03,value.m13,value.m23,value.m33));
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: original gameplay arena-local metres/Y-up/XZ; nonwalkable detached scenery shares the same layout math as the preview.
// Array Formats: one cold immutable1620vertex mesh, one material/renderer, existing resource ledger; cold input88B[<=4096]+output24B[1] bounds pass, no Update/persistent native store.
// Dependencies/Pointers: arena-root visibility owns the scenery through tutorial/arena switches; existing World OnDestroy owns Mesh/Material cleanup. Exact owned-root capture exclusion avoids duplicate menu scenery and nonwalkable route support. Native pixels/lifecycle/GC pending.
