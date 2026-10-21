using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace OdysseyOracle.VisualDressing
{
    // Cold six-quad presentation lease. No simulation, input, physics or audio owner.
    public sealed class RaykonPurpleWispPresentation : IDisposable
    {
        public const int WispCount=6;
        private GameObject root;
        private Mesh mesh;
        private Material material;
        public bool IsReady=>root!=null&&mesh!=null&&material!=null;

        public static RaykonPurpleWispPresentation CreateOptional(Transform borrowedParent)
        {
            if(borrowedParent==null)throw new ArgumentNullException(nameof(borrowedParent));
            Shader shader=Resources.Load<Shader>("Shaders/OracleRaykonPurpleWisps");
            if(shader==null||!shader.isSupported)
            {
                Debug.LogWarning("Raykon purple wisps unavailable: import the original Resources shader before visual acceptance.");
                return null;
            }
            var lease=new RaykonPurpleWispPresentation();
            try
            {
                Vector3 emitter=RaykonMeshes.CauldronColliderBounds.center;
                emitter.y=.53f; // Intentional smoke emission above the .475 m liquid, not a structural floor offset.
                var positions=new Vector3[WispCount*4];
                var uvs=new Vector2[WispCount*4];
                var phases=new Vector2[WispCount*4];
                var indices=new int[WispCount*6];
                for(int wisp=0;wisp<WispCount;wisp++)
                {
                    int v=wisp*4,i=wisp*6;
                    positions[v]=new Vector3(-1,-1,0);positions[v+1]=new Vector3(1,-1,0);
                    positions[v+2]=new Vector3(1,1,0);positions[v+3]=new Vector3(-1,1,0);
                    uvs[v]=new Vector2(0,0);uvs[v+1]=new Vector2(1,0);
                    uvs[v+2]=new Vector2(1,1);uvs[v+3]=new Vector2(0,1);
                    Vector2 phase=new Vector2((wisp+.5f)/WispCount,wisp*2.39996323f);
                    phases[v]=phase;phases[v+1]=phase;phases[v+2]=phase;phases[v+3]=phase;
                    indices[i]=v;indices[i+1]=v+1;indices[i+2]=v+2;
                    indices[i+3]=v;indices[i+4]=v+2;indices[i+5]=v+3;
                }
                lease.mesh=new Mesh {name="Original Raykon six purple wisps"};
                lease.mesh.vertices=positions;lease.mesh.uv=uvs;lease.mesh.uv2=phases;
                lease.mesh.triangles=indices;
                lease.mesh.bounds=new Bounds(emitter+new Vector3(0,.47f,0),new Vector3(.84f,1.30f,.84f));
                lease.mesh.UploadMeshData(true);
                lease.material=new Material(shader) {name="Original Raykon purple wisp material"};
                lease.material.SetVector("_Emitter",new Vector4(emitter.x,emitter.y,emitter.z,0));
                lease.root=new GameObject("Raykon six purple wisps",typeof(MeshFilter),typeof(MeshRenderer));
                lease.root.transform.SetParent(borrowedParent,false);
                lease.root.GetComponent<MeshFilter>().sharedMesh=lease.mesh;
                MeshRenderer renderer=lease.root.GetComponent<MeshRenderer>();renderer.sharedMaterial=lease.material;
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
                return lease;
            }
            catch{lease.Dispose();throw;}
        }

        public void Dispose()
        {
            if(root!=null){root.SetActive(false);Release(root);root=null;}
            if(mesh!=null){Release(mesh);mesh=null;}
            if(material!=null){Release(material);material=null;}
        }
        private static void Release(UnityEngine.Object owned)
        {
            if(Application.isPlaying)UnityEngine.Object.Destroy(owned);else UnityEngine.Object.DestroyImmediate(owned);
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Original Raykon yaw-only/unit-scale local +Z/Y-up; emitter sits above brew, shader billboards in world; bounds include all phases.
// Array Formats: Exactly six quads, 24 cold vertices/UV0/UV1 phase-angle entries and 36 indices; immutable mesh, no per-frame arrays or Object loops.
// Dependencies/Pointers: Borrows parent and Resources shader; owns one child/mesh/material, disposed by RaykonPresentation. No light, physics, skin, audio or gameplay authority.
