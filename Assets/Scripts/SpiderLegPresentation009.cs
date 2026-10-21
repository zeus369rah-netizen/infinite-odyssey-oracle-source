using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace OdysseyOracle
{
    internal sealed class SpiderLegPresentation009 : IDisposable
    {
        sealed class Shared
        {
            internal readonly Mesh[] Meshes = new Mesh[8];
            internal readonly Bounds[] AuthorityBounds = new Bounds[8];
            internal int Users;
        }
        static Shared cached;
        static readonly Vector3[] Corners = {
            new Vector3(-.5f,-.5f,-.5f),new Vector3(.5f,-.5f,-.5f),new Vector3(.5f,.5f,-.5f),new Vector3(-.5f,.5f,-.5f),
            new Vector3(-.5f,-.5f,.5f),new Vector3(.5f,-.5f,.5f),new Vector3(.5f,.5f,.5f),new Vector3(-.5f,.5f,.5f)};
        static readonly int[] Faces = {0,3,2,1,5,6,7,4,4,7,3,0,1,2,6,5,3,7,6,2,4,0,1,5};
        static readonly Vector3[] Normals = {Vector3.back,Vector3.forward,Vector3.left,Vector3.right,Vector3.up,Vector3.down};
        readonly Transform body;
        readonly Transform leg0, leg1, leg2, leg3, leg4, leg5, leg6, leg7;
        NativeArray<SpiderLegPose009> poses;
        NativeArray<float> minimumSole;
        JobHandle fence;
        Shared entry;

        internal SpiderLegPresentation009(Transform body, Transform[] legs, Bounds[] originalBounds)
        {
            if (body == null || legs == null || legs.Length != 8 || originalBounds == null || originalBounds.Length != 8)
                throw new ArgumentException("Eight original spider leg pivots and bounds are required.");
            this.body = body;
            leg0=legs[0];leg1=legs[1];leg2=legs[2];leg3=legs[3];leg4=legs[4];leg5=legs[5];leg6=legs[6];leg7=legs[7];
            var filters=new MeshFilter[8];var originals=new Mesh[8];
            for(int i=0;i<8;i++)
            {
                if(legs[i]==null)throw new ArgumentException("Missing original spider pivot.");
                filters[i]=legs[i].GetComponent<MeshFilter>();
                if(filters[i]==null || filters[i].sharedMesh==null)throw new ArgumentException("Missing original spider leg mesh.");
                originals[i]=filters[i].sharedMesh;
            }
            try
            {
                if(cached==null)
                {
                    var built=new Shared();
                    try{for(int i=0;i<8;i++){built.AuthorityBounds[i]=originalBounds[i];built.Meshes[i]=BuildMesh(i);}}
                    catch{DestroyShared(built);throw;}
                    cached=built;
                }
                for(int i=0;i<8;i++)if(cached.AuthorityBounds[i]!=originalBounds[i])
                    throw new InvalidOperationException("Original spider authority bounds changed; review this presentation revision.");
                entry=cached;entry.Users++;
                poses=new NativeArray<SpiderLegPose009>(8,Allocator.Persistent,NativeArrayOptions.ClearMemory);
                minimumSole=new NativeArray<float>(1,Allocator.Persistent,NativeArrayOptions.ClearMemory);
                for(int i=0;i<8;i++)filters[i].sharedMesh=entry.Meshes[i];
                Pose(0f,0f,false);
            }
            catch
            {
                for(int i=0;i<8;i++)if(filters[i]!=null)filters[i].sharedMesh=originals[i];
                Dispose();throw;
            }
        }

        // Cold admission only: keep original trigger, navigation and crowd authority exact.
        internal static Bounds AuthorityBounds(Mesh mesh)
        {
            Shared current=cached;
            if(current!=null)for(int i=0;i<8;i++)if(ReferenceEquals(current.Meshes[i],mesh))return current.AuthorityBounds[i];
            return mesh.bounds;
        }
        internal void Pose(float gait,float weight,bool flying)
        {
            if(entry==null)return;
            fence=new SpiderLegPoseJob009{Gait=gait,Weight=weight,Flying=flying,Poses=poses,MinimumSole=minimumSole}.Schedule();
            fence.Complete();
            Apply(leg0,poses[0]);Apply(leg1,poses[1]);Apply(leg2,poses[2]);Apply(leg3,poses[3]);
            Apply(leg4,poses[4]);Apply(leg5,poses[5]);Apply(leg6,poses[6]);Apply(leg7,poses[7]);
        }
        // A verified grounded native profile publishes minY0; never reuse a stale flying+.06 sole.
        internal void SetNativeGroundedSole015(){if(entry==null)return;fence.Complete();minimumSole[0]=0f;}
        internal float LowestSoleY => body.TransformPoint(new Vector3(0f,minimumSole[0],0f)).y;
        static void Apply(Transform leg,SpiderLegPose009 pose)
        {
            float4 q=pose.Rotation.value;
            leg.SetLocalPositionAndRotation(new Vector3(pose.Position.x,pose.Position.y,pose.Position.z),new Quaternion(q.x,q.y,q.z,q.w));
        }
        public void Dispose()
        {
            fence.Complete();
            if(poses.IsCreated)poses.Dispose();
            if(minimumSole.IsCreated)minimumSole.Dispose();
            Shared retired=entry;entry=null;
            if(retired!=null && --retired.Users==0)
            {if(ReferenceEquals(cached,retired))cached=null;DestroyShared(retired);}
        }
        static Mesh BuildMesh(int index)
        {
            var vertices=new Vector3[72];var normals=new Vector3[72];var uv=new Vector2[72];var triangles=new int[108];
            float3 hip=SpiderLegMath009.Hip(index);
            for(int section=0;section<3;section++)
            {
                SpiderLegBox009 spec=SpiderLegMath009.Box(index,section);float4 q=spec.Rotation.value;
                var rotation=new Quaternion(q.x,q.y,q.z,q.w);
                var center=new Vector3(spec.Center.x-hip.x,spec.Center.y-hip.y,spec.Center.z-hip.z);
                var size=new Vector3(spec.Size.x,spec.Size.y,spec.Size.z);
                for(int face=0;face<6;face++)
                {
                    int at=section*24+face*4;
                    for(int corner=0;corner<4;corner++)
                    {
                        vertices[at+corner]=center+rotation*Vector3.Scale(Corners[Faces[face*4+corner]],size);
                        normals[at+corner]=rotation*Normals[face];uv[at+corner]=corner==0?Vector2.zero:corner==1?Vector2.up:corner==2?Vector2.one:Vector2.right;
                    }
                    int tri=section*36+face*6;triangles[tri]=at;triangles[tri+1]=at+1;triangles[tri+2]=at+2;
                    triangles[tri+3]=at;triangles[tri+4]=at+2;triangles[tri+5]=at+3;
                }
            }
            Mesh mesh=null;
            try
            {
                mesh=new Mesh{name="Original Spider009 leg "+index,vertices=vertices,normals=normals,uv=uv,triangles=triangles};
                mesh.RecalculateBounds();mesh.UploadMeshData(true);return mesh;
            }
            catch
            {
                if(mesh!=null){if(Application.isPlaying)UnityEngine.Object.Destroy(mesh);else UnityEngine.Object.DestroyImmediate(mesh);}
                throw;
            }
        }
        static void DestroyShared(Shared shared)
        {for(int i=0;i<8;i++)if(shared.Meshes[i]!=null){if(Application.isPlaying)UnityEngine.Object.Destroy(shared.Meshes[i]);else UnityEngine.Object.DestroyImmediate(shared.Meshes[i]);}}
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Eight original actor-local spider pivots and world sole adapter; body1.20 scale remains external.
// Array Formats: Shared8meshes/24boxes, persistent8x32Bposes+1float per spider; fixed cached transforms, no frame allocations/material cloning.
// Dependencies/Pointers: Existing V1 lease owns renderer lifetime; complete owned job before reads/disposal; cold baseline bounds preserve gameplay authority, no engine objects enter jobs.
