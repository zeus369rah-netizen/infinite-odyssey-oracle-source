using System;
using UnityEngine;
using Style=OdysseyOracle.MonsterVoxelCosmetics.Style;
using PartSpec=OdysseyOracle.MonsterVoxelCosmetics.PartSpec;
using BoxSpec=OdysseyOracle.MonsterVoxelCosmetics.BoxSpec;

namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // Cold V2 surface overrides only. V1 still owns all created transforms,
    // three borrowed palette bindings, gait, flashes and original base meshes.
    // Four caches contain at most 15 additional meshes; only 52 parts are drawn.
    // No self Update, extra renderer/light/material/collider, wire or stat state.
    internal static class MonsterVoxelCosmeticsV2
    {
        sealed class Entry {internal PartSpec[] Parts;internal Mesh[] Meshes;internal int Users;}
        static readonly Entry[] cache=new Entry[4];
        static readonly Vector3[] corners = {
            new Vector3(-.5f,-.5f,-.5f),new Vector3(.5f,-.5f,-.5f),new Vector3(.5f,.5f,-.5f),new Vector3(-.5f,.5f,-.5f),
            new Vector3(-.5f,-.5f,.5f),new Vector3(.5f,-.5f,.5f),new Vector3(.5f,.5f,.5f),new Vector3(-.5f,.5f,.5f)
        };
        static readonly int[] faces = {0,3,2,1,5,6,7,4,4,7,3,0,1,2,6,5,3,7,6,2,4,0,1,5};
        static readonly Vector3[] normals = {Vector3.back,Vector3.forward,Vector3.left,Vector3.right,Vector3.up,Vector3.down};
        static readonly Vector2[] uvs = {Vector2.zero,Vector2.up,Vector2.one,Vector2.right};


        sealed class OverrideLease:IDisposable
        {
            Entry entry;readonly int slot;
            internal OverrideLease(int slot,Entry entry){this.slot=slot;this.entry=entry;}
            public void Dispose()
            {
                Entry retired=entry;if(retired==null)return;entry=null;
                if(--retired.Users!=0)return;
                if(ReferenceEquals(cache[slot],retired))cache[slot]=null;
                for(int i=0;i<retired.Meshes.Length;i++)if(retired.Meshes[i]!=null)DestroyOwned(retired.Meshes[i]);
            }
        }
        internal static IDisposable Attach(Style style,Transform body,Transform leftArm,Transform rightArm)
        {
            int slot=(int)style;if(slot<0||slot>=4)throw new ArgumentOutOfRangeException(nameof(style));
            if(body==null||leftArm==null||rightArm==null)throw new ArgumentException("Existing V1 pivots required.");
            Entry entry=cache[slot];
            if(entry==null)
            {
                PartSpec[] baseline=MonsterDetailSourceV2.Build(style);
                PartSpec[] details=MonsterDetailAdapterV2.Apply(style,baseline);
                entry=new Entry{Parts=details,Meshes=new Mesh[details.Length]};
                try
                {
                    for(int i=0;i<details.Length;i++)
                        if(!ReferenceEquals(details[i].Boxes,baseline[i].Boxes))entry.Meshes[i]=BuildMesh(style,details[i]);
                }
                catch
                {for(int i=0;i<entry.Meshes.Length;i++)if(entry.Meshes[i]!=null)DestroyOwned(entry.Meshes[i]);throw;}
                cache[slot]=entry;
            }
            entry.Users++;var lease=new OverrideLease(slot,entry);
            try
            {
                for(int i=0;i<entry.Parts.Length;i++)
                {
                    if(entry.Meshes[i]==null)continue;
                    PartSpec part=entry.Parts[i];Transform parent=part.Anchor==1?leftArm:part.Anchor==2?rightArm:body;
                    Transform target=parent.Find(part.Label);
                    if(target==null||target.localPosition!=part.Origin||target.GetComponent<MeshRenderer>()==null)
                        throw new InvalidOperationException("Original V1 part/pivot changed; V2 override refused.");
                    MeshFilter filter=target.GetComponent<MeshFilter>();
                    if(filter==null||filter.sharedMesh==null)throw new InvalidOperationException("Original V1 mesh missing.");
                    filter.sharedMesh=entry.Meshes[i];
                }
                return lease;
            }
            catch{lease.Dispose();throw;}
        }
        static Mesh BuildMesh(Style style,PartSpec part)
        {
            int count=part.Boxes.Length;var vertices=new Vector3[count*24];var normalData=new Vector3[vertices.Length];
            var textureData=new Vector2[vertices.Length];var triangles=new int[count*36];
            for(int boxIndex=0;boxIndex<count;boxIndex++)
            {
                BoxSpec box=part.Boxes[boxIndex];Quaternion rotation=Quaternion.Euler(box.Euler);int start=boxIndex*24;
                for(int face=0;face<6;face++)
                {
                    int at=start+face*4;
                    for(int corner=0;corner<4;corner++)
                    {
                        vertices[at+corner]=box.Center-part.Origin+rotation*Vector3.Scale(corners[faces[face*4+corner]],box.Size);
                        normalData[at+corner]=rotation*normals[face];textureData[at+corner]=uvs[corner];
                    }
                    int index=boxIndex*36+face*6;triangles[index]=at;triangles[index+1]=at+1;triangles[index+2]=at+2;
                    triangles[index+3]=at;triangles[index+4]=at+2;triangles[index+5]=at+3;
                }
            }
            var mesh=new Mesh {name="Detail V2 "+style+" "+part.Label,vertices=vertices,normals=normalData,uv=textureData,triangles=triangles};
            mesh.RecalculateBounds();mesh.UploadMeshData(true);return mesh;
        }
        static void DestroyOwned(UnityEngine.Object value)
        {if(Application.isPlaying)UnityEngine.Object.Destroy(value);else UnityEngine.Object.DestroyImmediate(value);}
    }
}
