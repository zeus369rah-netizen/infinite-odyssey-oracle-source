using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace OdysseyOracle
{
    public struct TreeLifePlacement020
    {
        public float3 Feet;
        public float FinalHeight,ReservedRootRadius;
        public uint Generation,GeometryRevision,Seed;
        public byte Supported,Reserved;
    }
    // One explicit region-owned lease; never attach a new per-frame component.
    public sealed class TreeLifeLease020:IDisposable
    {
        static readonly int ClockId=Shader.PropertyToID("_WindClock"),StrengthId=Shader.PropertyToID("_WindStrength");
        static readonly VertexAttributeDescriptor[] Format={
            new VertexAttributeDescriptor(VertexAttribute.Position,VertexAttributeFormat.Float32,3),new VertexAttributeDescriptor(VertexAttribute.Normal,VertexAttributeFormat.Float32,3),
            new VertexAttributeDescriptor(VertexAttribute.Color,VertexAttributeFormat.Float32,4),new VertexAttributeDescriptor(VertexAttribute.TexCoord0,VertexAttributeFormat.Float32,2),new VertexAttributeDescriptor(VertexAttribute.TexCoord1,VertexAttributeFormat.Float32,4)};
        readonly Mesh[] meshes=new Mesh[3];readonly MeshRenderer[] renderers=new MeshRenderer[3];
        NativeArray<TreeLifeVertex020> vertices;NativeArray<int> indices,written;
        JobHandle fence;bool scheduled,disposed;int retiredFrame=-1;
        GameObject root;Material material;uint generation,revision;
        TreeLifePlacement020 descriptor;
        public bool Ready {get;private set;}
        public string Failure {get;private set;}="";
        public uint Generation=>generation;
        public Bounds ConservativeWorldBounds {get;private set;}
        public bool Prepare(Transform parent,in TreeLifePlacement020 placement)
        {
            if(disposed)return false;
            if(!math.all(math.isfinite(placement.Feet))||!math.isfinite(placement.FinalHeight)||placement.FinalHeight<36||placement.FinalHeight>48||
                !math.isfinite(placement.ReservedRootRadius)||placement.ReservedRootRadius<4.5f||placement.Generation==0||placement.GeometryRevision==0||placement.Supported!=1||placement.Reserved!=1||
                parent==null||(parent.lossyScale-Vector3.one).sqrMagnitude>.000001f)
            {Failure="An actual supported/reserved generation, unit parent and36..48m final height are required.";return false;}
            if(Ready&&generation==placement.Generation&&revision==placement.GeometryRevision)
            {
                if(math.all(descriptor.Feet==placement.Feet)&&descriptor.FinalHeight==placement.FinalHeight&&descriptor.Seed==placement.Seed)return true;
                Failure="A ready geometry revision cannot change its immutable tree descriptor.";return false;
            }
            if(root!=null&&root.activeSelf){Failure="Retire the visible current-region tree before replacement.";return false;}
            if(Application.isPlaying&&Time.frameCount<=retiredFrame){Failure="Deferred resource retirement must drain before rebuilding.";return false;}
            if(!ReleaseOwned())return false;
            if(Application.isPlaying&&Time.frameCount<=retiredFrame){Failure="Old resources were retired; prepare again after their normal frame drain.";return false;}
            try
            {
                var shader=Resources.Load<Shader>("Shaders/OracleTreeLife020");
                if(shader==null||!shader.isSupported)throw new InvalidOperationException("Tree shader import/support is pending.");
                if(Unity.Collections.LowLevel.Unsafe.UnsafeUtility.SizeOf<TreeLifeVertex020>()!=TreeLifeRecipe020.VertexStride)throw new InvalidOperationException("Tree native vertex ABI mismatch.");
                material=new Material(shader){name="Owned original Tree of Life020"};
                root=new GameObject("One current-region Tree of Life020");root.SetActive(false);root.transform.SetParent(parent,false);
                root.transform.position=new Vector3(placement.Feet.x,placement.Feet.y,placement.Feet.z);root.transform.rotation=Quaternion.identity;root.transform.localScale=Vector3.one;
                vertices=new NativeArray<TreeLifeVertex020>(TreeLifeRecipe020.MaximumVertices,Allocator.TempJob,NativeArrayOptions.UninitializedMemory);
                indices=new NativeArray<int>(TreeLifeRecipe020.MaximumIndices,Allocator.TempJob,NativeArrayOptions.UninitializedMemory);written=new NativeArray<int>(2,Allocator.TempJob);
                for(int lod=0;lod<3;lod++)
                {
                    fence=new TreeLifeMeshJob020{FinalHeight=placement.FinalHeight,Lod=lod,Seed=placement.Seed,Vertices=vertices,Indices=indices,Written=written}.Schedule();scheduled=true;fence.Complete();scheduled=false;
                    if(written[0]<4||written[0]>vertices.Length||written[1]<6||written[1]>indices.Length)throw new InvalidOperationException("Finite tree mesh cap rejected.");
                    var mesh=new Mesh{name="Original Tree of Life020 LOD"+lod,indexFormat=IndexFormat.UInt32};meshes[lod]=mesh;
                    mesh.SetVertexBufferParams(written[0],Format);mesh.SetVertexBufferData(vertices,0,0,written[0],0,MeshUpdateFlags.DontRecalculateBounds);
                    mesh.SetIndexBufferParams(written[1],IndexFormat.UInt32);mesh.SetIndexBufferData(indices,0,0,written[1],MeshUpdateFlags.DontRecalculateBounds);
                    mesh.subMeshCount=1;mesh.SetSubMesh(0,new SubMeshDescriptor(0,written[1]),MeshUpdateFlags.DontRecalculateBounds);
                    var bounds=new Bounds(new Vector3(0,placement.FinalHeight*.5f,0),new Vector3(44,placement.FinalHeight,44));bounds.Expand(TreeLifeRecipe020.WindExpansion*2);mesh.bounds=bounds;mesh.UploadMeshData(true);
                    var node=new GameObject("Batched whole tree LOD"+lod);node.transform.SetParent(root.transform,false);node.AddComponent<MeshFilter>().sharedMesh=mesh;
                    var renderer=node.AddComponent<MeshRenderer>();renderers[lod]=renderer;renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.TwoSided;renderer.receiveShadows=true;
                    renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
                }
                if(!ReleaseScratch())throw new InvalidOperationException("Tree scratch cleanup remains pending.");
                var group=root.AddComponent<LODGroup>();group.fadeMode=LODFadeMode.None;
                group.SetLODs(new[]{new LOD(.22f,new Renderer[]{renderers[0]}),new LOD(.09f,new Renderer[]{renderers[1]}),new LOD(.025f,new Renderer[]{renderers[2]})});group.RecalculateBounds();
                generation=placement.Generation;revision=placement.GeometryRevision;descriptor=placement;ConservativeWorldBounds=new Bounds(root.transform.position+new Vector3(0,placement.FinalHeight*.5f,0),new Vector3(45.6f,placement.FinalHeight+1.6f,45.6f));
                Ready=true;Failure="";SetHeldClock(0,.35f);return true;
            }
            catch(Exception error){Ready=false;Failure=error.Message;ReleaseOwned();return false;}
        }
        public bool SetVisible(uint actualGeneration,uint actualGeometryRevision,bool visible)
        {if(!Ready||root==null||actualGeneration!=generation||actualGeometryRevision!=revision)return false;root.SetActive(visible);return true;}
        public void SetHeldClock(float seconds,float strength)
        {
            if(!Ready||material==null||!float.IsFinite(seconds)||!float.IsFinite(strength)||seconds<0)return;
            material.SetFloat(ClockId,Mathf.Repeat(seconds*.05f,1));material.SetFloat(StrengthId,Mathf.Clamp(strength,0,.6f));
        }
        bool ReleaseScratch()
        {
            // On completion failure retain the owned arrays/fence; another Prepare cannot allocate over them.
            if(scheduled){try{fence.Complete();scheduled=false;}catch(Exception error){Failure="Pending tree fence: "+error.Message;return false;}}
            bool okay=true;
            try{if(written.IsCreated)written.Dispose();}catch(Exception error){Failure=error.Message;okay=false;}
            try{if(indices.IsCreated)indices.Dispose();}catch(Exception error){Failure=error.Message;okay=false;}
            try{if(vertices.IsCreated)vertices.Dispose();}catch(Exception error){Failure=error.Message;okay=false;}
            return okay;
        }
        static void Retire(UnityEngine.Object value){if(value==null)return;if(Application.isPlaying)UnityEngine.Object.Destroy(value);else UnityEngine.Object.DestroyImmediate(value);}
        bool ReleaseOwned()
        {
            Ready=false;if(root!=null)root.SetActive(false);
            if(!ReleaseScratch())return false;
            bool hadObjects=root!=null||material!=null;for(int i=0;i<3;i++)hadObjects|=meshes[i]!=null;
            Retire(root);root=null;for(int i=0;i<3;i++){Retire(meshes[i]);meshes[i]=null;renderers[i]=null;}Retire(material);material=null;
            generation=revision=0;if(hadObjects&&Application.isPlaying)retiredFrame=Time.frameCount;return true;
        }
        public void Dispose(){if(disposed)return;if(!ReleaseOwned())throw new InvalidOperationException(Failure);disposed=true;}
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: explicitly admitted world feet/unit rigid parent; final36..48m is already applied once in the job, no second root scale. Swept conservative bounds expand each axis0.8m.
// Array Formats: one root/three whole-tree LOD nodes, three immutable64B meshes, one material/LODGroup; reused6000vertex/9000index/2counter native scratch is completed then disposed before Ready. No leaf GameObjects/colliders/RT/lights/frame callback.
// Dependencies/Pointers: region owns this one lease and actual generation/support/retirement proof; physics owns R4.5 reservation, atmosphere supplies finite held clock. Completion failure retains bounded ownership for explicit retry; Edit mode destroys owned resources immediately. Root native import/LOD/shadow/GC acceptance pending.
