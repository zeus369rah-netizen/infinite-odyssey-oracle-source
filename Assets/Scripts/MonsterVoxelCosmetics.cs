using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // Build-time cosmetic data: unscaled actor-local metres, +Z face, +Y up.
    // Caller owns body/shoulder pivots and supplies the existing1.20 visual scale once.
    // Meshes are shared per style while leases exist. Caller owns materials and actor lifecycle.
    // No simulation, target ID, damage, collider, light, Animator, Update or native-grid ownership.
    internal static class MonsterVoxelCosmetics
    {
        internal enum Style { GuardianGolem, Stalker, Shade, Spider }
        internal sealed class Entry { internal Mesh[] Meshes; internal PartSpec[] Parts; internal int Users; }
        internal struct BoxSpec
        {
            internal Vector3 Center, Size, Euler;
            internal BoxSpec(Vector3 center,Vector3 size,Vector3 euler){Center=center;Size=size;Euler=euler;}
        }
        internal struct PartSpec
        {
            internal string Label; internal int Anchor,Channel; internal Vector3 Origin; internal BoxSpec[] Boxes;
            internal PartSpec(string label,int anchor,int channel,Vector3 origin,BoxSpec[] boxes)
            {Label=label;Anchor=anchor;Channel=channel;Origin=origin;Boxes=boxes;}
        }
        static readonly Entry[] cache = new Entry[4];
        static readonly Vector3[] corners = {
            new Vector3(-.5f,-.5f,-.5f),new Vector3(.5f,-.5f,-.5f),new Vector3(.5f,.5f,-.5f),new Vector3(-.5f,.5f,-.5f),
            new Vector3(-.5f,-.5f,.5f),new Vector3(.5f,-.5f,.5f),new Vector3(.5f,.5f,.5f),new Vector3(-.5f,.5f,.5f)
        };
        static readonly int[] faces = {0,3,2,1,5,6,7,4,4,7,3,0,1,2,6,5,3,7,6,2,4,0,1,5};
        static readonly Vector3[] normals = {Vector3.back,Vector3.forward,Vector3.left,Vector3.right,Vector3.up,Vector3.down};
        static readonly Vector2[] uvs = {Vector2.zero,Vector2.up,Vector2.one,Vector2.right};

        internal sealed class Lease : IDisposable
        {
            internal Transform LeftLeg,RightLeg;
            internal readonly Transform[] SpiderLegs;
            internal readonly Bounds[] SpiderLegBounds;
            readonly Vector3[] spiderOrigins;
            readonly List<GameObject> objects;
            readonly List<Renderer> flashes;
            readonly List<Renderer> addedFlashes;
            Entry entry; readonly int slot;
            internal Lease(int slot,Entry entry,List<Renderer> flashes)
            {
                this.slot=slot;this.entry=entry;this.flashes=flashes;
                objects=new List<GameObject>(entry.Parts.Length);addedFlashes=new List<Renderer>(entry.Parts.Length);
                if(slot==(int)Style.Spider){SpiderLegs=new Transform[8];SpiderLegBounds=new Bounds[8];spiderOrigins=new Vector3[8];}
            }
            internal void Attach(Transform body,Transform leftArm,Transform rightArm,Material stone,Material joints,Material rune)
            {
                for(int i=0;i<entry.Parts.Length;i++)
                {
                    PartSpec part=entry.Parts[i];Transform parent=part.Anchor==1?leftArm:part.Anchor==2?rightArm:body;
                    var go=new GameObject(part.Label,typeof(MeshFilter),typeof(MeshRenderer));objects.Add(go);
                    go.transform.SetParent(parent,false);go.transform.localPosition=part.Origin;
                    go.GetComponent<MeshFilter>().sharedMesh=entry.Meshes[i];
                    MeshRenderer renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=part.Channel==0?stone:part.Channel==1?joints:rune;
                    renderer.shadowCastingMode=part.Channel==2?ShadowCastingMode.Off:ShadowCastingMode.On;
                    renderer.receiveShadows=part.Channel!=2;renderer.lightProbeUsage=LightProbeUsage.BlendProbes;
                    renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
                    if(part.Channel==0 && flashes!=null){flashes.Add(renderer);addedFlashes.Add(renderer);}
                    if(part.Label=="Left leg")LeftLeg=go.transform;
                    else if(part.Label=="Right leg")RightLeg=go.transform;
                    if(SpiderLegs!=null && part.Label.StartsWith("Spider leg ",StringComparison.Ordinal))
                    {
                        int index=int.Parse(part.Label.Substring(11));SpiderLegs[index]=go.transform;
                        SpiderLegBounds[index]=entry.Meshes[i].bounds;spiderOrigins[index]=part.Origin;
                    }
                }
                if(SpiderLegs!=null){LeftLeg=SpiderLegs[0];RightLeg=SpiderLegs[1];}
            }
            // Called by the existing owner motion tick only. Eight independent rigid leg pivots,
            // alternating tripod-like pairs; no biped clip, damage event or new Update is introduced.
            internal void PoseSpider(float gait,float weight,bool flying)
            {
                if(entry==null || SpiderLegs==null)return;
                weight=Mathf.Clamp01(weight);
                for(int i=0;i<8;i++)
                {
                    float phase=gait+(i/2%2==i%2?0f:Mathf.PI),wave=Mathf.Sin(phase)*weight;
                    SpiderLegs[i].localPosition=spiderOrigins[i]+Vector3.up*(Mathf.Max(0f,wave)*.055f+(flying?.06f:0f));
                    SpiderLegs[i].localRotation=Quaternion.Euler(0f,wave*4f,0f);
                }
            }
            internal float LowestSpiderSole()
            {
                if(entry==null || SpiderLegs==null)throw new InvalidOperationException("A live Spider visual lease is required.");
                float minimum=float.PositiveInfinity;
                for(int i=0;i<8;i++)
                {
                    Matrix4x4 m=SpiderLegs[i].localToWorldMatrix;Bounds b=SpiderLegBounds[i];Vector3 e=b.extents;
                    minimum=Mathf.Min(minimum,m.MultiplyPoint3x4(b.center).y-(Mathf.Abs(m.m10)*e.x+Mathf.Abs(m.m11)*e.y+Mathf.Abs(m.m12)*e.z));
                }
                return minimum;
            }
            public void Dispose()
            {
                if(entry==null)return;
                for(int i=0;i<objects.Count;i++)if(objects[i]!=null)DestroyOwned(objects[i]);
                if(flashes!=null)for(int i=0;i<addedFlashes.Count;i++)flashes.Remove(addedFlashes[i]);
                objects.Clear();addedFlashes.Clear();
                Entry retired=entry;entry=null;
                if(--retired.Users==0){cache[slot]=null;for(int i=0;i<retired.Meshes.Length;i++)if(retired.Meshes[i]!=null)DestroyOwned(retired.Meshes[i]);}
            }
        }

        // Invoke instead of the old Block body section, before mesh-derived hitbox/sole caches.
        // Existing shoulder pivots retain x±.53/y1.36 and unit scale; no existing blocks may remain.
        internal static Lease Create(Style style,Transform body,Transform leftArm,Transform rightArm,
            Material stone,Material joints,Material rune,List<Renderer> flashRenderers)
        {
            if(body==null || leftArm==null || rightArm==null || stone==null || joints==null || rune==null)
                throw new ArgumentException("Existing visual pivots and three borrowed shared materials are required.");
            int slot=(int)style;if(slot<0 || slot>=cache.Length)throw new ArgumentOutOfRangeException(nameof(style));
            Entry entry=cache[slot];
            if(entry==null)
            {
                entry=new Entry {Parts=Recipe(style)};entry.Meshes=new Mesh[entry.Parts.Length];
                try{for(int i=0;i<entry.Parts.Length;i++)entry.Meshes[i]=BuildMesh(style,entry.Parts[i]);}
                catch{for(int i=0;i<entry.Meshes.Length;i++)if(entry.Meshes[i]!=null)DestroyOwned(entry.Meshes[i]);throw;}
                cache[slot]=entry;
            }
            entry.Users++;var lease=new Lease(slot,entry,flashRenderers);
            try{lease.Attach(body,leftArm,rightArm,stone,joints,rune);return lease;}
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
            var mesh=new Mesh {name="Shared "+style+" "+part.Label,vertices=vertices,normals=normalData,uv=textureData,triangles=triangles};
            mesh.RecalculateBounds();mesh.UploadMeshData(true);return mesh;
        }
        static void DestroyOwned(UnityEngine.Object value)
        {if(Application.isPlaying)UnityEngine.Object.Destroy(value);else UnityEngine.Object.DestroyImmediate(value);}
        static PartSpec[] Recipe(Style style)
        {
            switch(style)
            {
                case Style.GuardianGolem: return new PartSpec[] {
                    new PartSpec("Torso",0,0,new Vector3(0f,1.04f,0f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0f,1.03f,0f),new Vector3(0.82f,0.68f,0.49f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,1.35f,-0.015f),new Vector3(1.04f,0.2f,0.53f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(-0.46f,1.36f,0.015f),new Vector3(0.27f,0.24f,0.55f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0.46f,1.36f,0.015f),new Vector3(0.27f,0.24f,0.55f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,0.82f,0.275f),new Vector3(0.54f,0.16f,0.075f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Stone brow",0,0,new Vector3(0f,1.68f,-0.015f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0f,1.71f,-0.05f),new Vector3(0.64f,0.38f,0.47f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,1.86f,0.06f),new Vector3(0.7f,0.09f,0.5f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(-0.255f,1.61f,0.22f),new Vector3(0.1f,0.22f,0.12f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0.255f,1.61f,0.22f),new Vector3(0.1f,0.22f,0.12f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Face shadow",0,1,new Vector3(0f,1.65f,0.258f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0f,1.65f,0.258f),new Vector3(0.46f,0.18f,0.035f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Left rune eye",0,2,new Vector3(-0.13f,1.67f,0.282f),new BoxSpec[] {
                        new BoxSpec(new Vector3(-0.13f,1.67f,0.282f),new Vector3(0.1f,0.07f,0.025f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Right rune eye",0,2,new Vector3(0.13f,1.67f,0.282f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0.13f,1.67f,0.282f),new Vector3(0.1f,0.07f,0.025f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Chest rune",0,2,new Vector3(0f,1.12f,0.257f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0f,1.12f,0.257f),new Vector3(0.16f,0.24f,0.035f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Hip core",0,1,new Vector3(0f,0.58f,0f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0f,0.58f,0f),new Vector3(0.54f,0.2f,0.34f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Left leg",0,0,new Vector3(-0.23f,0.29f,0f),new BoxSpec[] {
                        new BoxSpec(new Vector3(-0.23f,0.44f,0f),new Vector3(0.34f,0.24f,0.31f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(-0.23f,0.18f,-0.015f),new Vector3(0.3128f,0.23f,0.29f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(-0.23f,0.045f,0.07f),new Vector3(0.34f,0.09f,0.43f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(-0.23f,0.3f,0.185f),new Vector3(0.2652f,0.16f,0.085f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(-0.23f,0.068f,0.235f),new Vector3(0.2788f,0.045f,0.045f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Left stone arm",1,0,new Vector3(0f,-0.33f,0.02f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0f,-0.15f,0.015f),new Vector3(0.34f,0.3f,0.34f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,-0.435f,0.025f),new Vector3(0.3128f,0.22f,0.31f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,-0.615f,0.06f),new Vector3(0.34f,0.14f,0.34f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,-0.1f,0.205f),new Vector3(0.2822f,0.2f,0.07f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,-0.615f,0.26f),new Vector3(0.272f,0.1f,0.08f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Right leg",0,0,new Vector3(0.23f,0.29f,0f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0.23f,0.44f,0f),new Vector3(0.34f,0.24f,0.31f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0.23f,0.18f,-0.015f),new Vector3(0.3128f,0.23f,0.29f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0.23f,0.045f,0.07f),new Vector3(0.34f,0.09f,0.43f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0.23f,0.3f,0.185f),new Vector3(0.2652f,0.16f,0.085f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0.23f,0.068f,0.235f),new Vector3(0.2788f,0.045f,0.045f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Right stone arm",2,0,new Vector3(0f,-0.33f,0.02f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0f,-0.15f,0.015f),new Vector3(0.34f,0.3f,0.34f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,-0.435f,0.025f),new Vector3(0.3128f,0.22f,0.31f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,-0.615f,0.06f),new Vector3(0.34f,0.14f,0.34f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,-0.1f,0.205f),new Vector3(0.2822f,0.2f,0.07f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,-0.615f,0.26f),new Vector3(0.272f,0.1f,0.08f),new Vector3(0f,0f,0f)),
                    }),
                };
                case Style.Stalker: return new PartSpec[] {
                    new PartSpec("Torso",0,0,new Vector3(0f,1.04f,0f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0f,1.05f,-0.055f),new Vector3(0.6f,0.66f,0.36f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,1.31f,-0.07f),new Vector3(0.78f,0.18f,0.39f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,0.83f,0.16f),new Vector3(0.42f,0.19f,0.09f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(-0.32f,1.32f,-0.13f),new Vector3(0.18f,0.16f,0.26f),new Vector3(0f,-13f,-12f)),
                        new BoxSpec(new Vector3(0.32f,1.32f,-0.13f),new Vector3(0.18f,0.16f,0.26f),new Vector3(0f,13f,12f)),
                    }),
                    new PartSpec("Stone brow",0,0,new Vector3(0f,1.68f,-0.015f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0f,1.69f,-0.035f),new Vector3(0.51f,0.37f,0.4f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,1.85f,0.1f),new Vector3(0.57f,0.075f,0.35f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,1.55f,0.205f),new Vector3(0.24f,0.13f,0.1f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Face shadow",0,1,new Vector3(0f,1.66f,0.224f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0f,1.66f,0.224f),new Vector3(0.36f,0.18f,0.035f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Left rune eye",0,2,new Vector3(-0.13f,1.68f,0.251f),new BoxSpec[] {
                        new BoxSpec(new Vector3(-0.13f,1.68f,0.251f),new Vector3(0.1f,0.07f,0.025f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Right rune eye",0,2,new Vector3(0.13f,1.68f,0.251f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0.13f,1.68f,0.251f),new Vector3(0.1f,0.07f,0.025f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Chest rune",0,2,new Vector3(0f,1.12f,0.257f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0f,1.12f,0.257f),new Vector3(0.16f,0.24f,0.035f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Hip core",0,1,new Vector3(0f,0.58f,0f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0f,0.58f,0f),new Vector3(0.43f,0.2f,0.34f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Left leg",0,0,new Vector3(-0.23f,0.29f,0f),new BoxSpec[] {
                        new BoxSpec(new Vector3(-0.23f,0.44f,0f),new Vector3(0.27f,0.24f,0.31f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(-0.23f,0.18f,-0.015f),new Vector3(0.2484f,0.23f,0.29f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(-0.23f,0.045f,0.07f),new Vector3(0.27f,0.09f,0.43f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(-0.23f,0.39f,0.17f),new Vector3(0.18f,0.1f,0.07f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(-0.23f,0.068f,0.245f),new Vector3(0.23f,0.06f,0.07f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Left stone arm",1,0,new Vector3(0f,-0.33f,0.02f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0f,-0.15f,0.015f),new Vector3(0.255f,0.3f,0.34f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,-0.435f,0.025f),new Vector3(0.2346f,0.22f,0.31f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,-0.615f,0.06f),new Vector3(0.255f,0.14f,0.34f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,-0.68f,0.15f),new Vector3(0.12f,0.1f,0.16f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Right leg",0,0,new Vector3(0.23f,0.29f,0f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0.23f,0.44f,0f),new Vector3(0.27f,0.24f,0.31f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0.23f,0.18f,-0.015f),new Vector3(0.2484f,0.23f,0.29f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0.23f,0.045f,0.07f),new Vector3(0.27f,0.09f,0.43f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0.23f,0.39f,0.17f),new Vector3(0.18f,0.1f,0.07f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0.23f,0.068f,0.245f),new Vector3(0.23f,0.06f,0.07f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Right stone arm",2,0,new Vector3(0f,-0.33f,0.02f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0f,-0.15f,0.015f),new Vector3(0.255f,0.3f,0.34f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,-0.435f,0.025f),new Vector3(0.2346f,0.22f,0.31f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,-0.615f,0.06f),new Vector3(0.255f,0.14f,0.34f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,-0.68f,0.15f),new Vector3(0.12f,0.1f,0.16f),new Vector3(0f,0f,0f)),
                    }),
                };
                case Style.Shade: return new PartSpec[] {
                    new PartSpec("Torso",0,0,new Vector3(0f,1.04f,0f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0f,1.06f,-0.105f),new Vector3(0.15f,0.65f,0.21f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,0.75f,-0.07f),new Vector3(0.44f,0.14f,0.32f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(-0.2f,0.87f,0.045f),new Vector3(0.29f,0.065f,0.32f),new Vector3(0f,-8f,-7f)),
                        new BoxSpec(new Vector3(0.2f,0.87f,0.045f),new Vector3(0.29f,0.065f,0.32f),new Vector3(0f,8f,7f)),
                        new BoxSpec(new Vector3(-0.2f,1f,0.045f),new Vector3(0.29f,0.065f,0.32f),new Vector3(0f,-8f,-5f)),
                        new BoxSpec(new Vector3(0.2f,1f,0.045f),new Vector3(0.29f,0.065f,0.32f),new Vector3(0f,8f,5f)),
                        new BoxSpec(new Vector3(-0.2f,1.13f,0.045f),new Vector3(0.29f,0.065f,0.32f),new Vector3(0f,-8f,-3f)),
                        new BoxSpec(new Vector3(0.2f,1.13f,0.045f),new Vector3(0.29f,0.065f,0.32f),new Vector3(0f,8f,3f)),
                        new BoxSpec(new Vector3(-0.2f,1.26f,0.045f),new Vector3(0.29f,0.065f,0.32f),new Vector3(0f,-8f,-1f)),
                        new BoxSpec(new Vector3(0.2f,1.26f,0.045f),new Vector3(0.29f,0.065f,0.32f),new Vector3(0f,8f,1f)),
                    }),
                    new PartSpec("Stone brow",0,0,new Vector3(0f,1.68f,-0.015f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0f,1.86f,-0.035f),new Vector3(0.64f,0.13f,0.48f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(-0.265f,1.67f,-0.015f),new Vector3(0.11f,0.34f,0.45f),new Vector3(0f,0f,-5f)),
                        new BoxSpec(new Vector3(0.265f,1.67f,-0.015f),new Vector3(0.11f,0.34f,0.45f),new Vector3(0f,0f,5f)),
                        new BoxSpec(new Vector3(0f,1.58f,-0.21f),new Vector3(0.47f,0.28f,0.08f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Face shadow",0,1,new Vector3(0f,1.69f,0.18f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0f,1.69f,0.18f),new Vector3(0.38f,0.18f,0.035f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Left rune eye",0,2,new Vector3(-0.13f,1.71f,0.21f),new BoxSpec[] {
                        new BoxSpec(new Vector3(-0.13f,1.71f,0.21f),new Vector3(0.1f,0.07f,0.025f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Right rune eye",0,2,new Vector3(0.13f,1.71f,0.21f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0.13f,1.71f,0.21f),new Vector3(0.1f,0.07f,0.025f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Shade crown",0,2,new Vector3(0f,1.98f,0f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0f,1.98f,0f),new Vector3(0.25f,0.2f,0.25f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,2.085f,0f),new Vector3(0.1f,0.07f,0.1f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(-0.2f,1.96f,-0.015f),new Vector3(0.07f,0.17f,0.07f),new Vector3(0f,0f,-12f)),
                        new BoxSpec(new Vector3(0.2f,1.96f,-0.015f),new Vector3(0.07f,0.17f,0.07f),new Vector3(0f,0f,12f)),
                    }),
                    new PartSpec("Shade focus",2,2,new Vector3(0f,-0.66f,0.19f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0f,-0.66f,0.19f),new Vector3(0.22f,0.22f,0.22f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,-0.66f,0.19f),new Vector3(0.085f,0.25f,0.085f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Chest rune",0,2,new Vector3(0f,1.12f,0.228f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0f,1.12f,0.228f),new Vector3(0.16f,0.24f,0.035f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Hip core",0,1,new Vector3(0f,0.58f,0f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0f,0.58f,0f),new Vector3(0.37f,0.2f,0.34f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Left leg",0,0,new Vector3(-0.23f,0.29f,0f),new BoxSpec[] {
                        new BoxSpec(new Vector3(-0.23f,0.44f,0f),new Vector3(0.19f,0.24f,0.31f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(-0.23f,0.18f,-0.015f),new Vector3(0.1748f,0.23f,0.29f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(-0.23f,0.045f,0.07f),new Vector3(0.19f,0.09f,0.43f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(-0.29f,0.04f,0.26f),new Vector3(0.055f,0.08f,0.13f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(-0.17f,0.04f,0.26f),new Vector3(0.055f,0.08f,0.13f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Left stone arm",1,0,new Vector3(0f,-0.33f,0.02f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0f,-0.15f,0.015f),new Vector3(0.2f,0.3f,0.34f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,-0.435f,0.025f),new Vector3(0.184f,0.22f,0.31f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,-0.615f,0.06f),new Vector3(0.2f,0.14f,0.34f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(-0.07f,-0.7f,0.18f),new Vector3(0.045f,0.16f,0.075f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,-0.7f,0.18f),new Vector3(0.045f,0.16f,0.075f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0.07f,-0.7f,0.18f),new Vector3(0.045f,0.16f,0.075f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Right leg",0,0,new Vector3(0.23f,0.29f,0f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0.23f,0.44f,0f),new Vector3(0.19f,0.24f,0.31f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0.23f,0.18f,-0.015f),new Vector3(0.1748f,0.23f,0.29f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0.23f,0.045f,0.07f),new Vector3(0.19f,0.09f,0.43f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0.17f,0.04f,0.26f),new Vector3(0.055f,0.08f,0.13f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0.29f,0.04f,0.26f),new Vector3(0.055f,0.08f,0.13f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Right stone arm",2,0,new Vector3(0f,-0.33f,0.02f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0f,-0.15f,0.015f),new Vector3(0.2f,0.3f,0.34f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,-0.435f,0.025f),new Vector3(0.184f,0.22f,0.31f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,-0.615f,0.06f),new Vector3(0.2f,0.14f,0.34f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(-0.07f,-0.7f,0.18f),new Vector3(0.045f,0.16f,0.075f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,-0.7f,0.18f),new Vector3(0.045f,0.16f,0.075f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0.07f,-0.7f,0.18f),new Vector3(0.045f,0.16f,0.075f),new Vector3(0f,0f,0f)),
                    }),
                };
                case Style.Spider: return new PartSpec[] {
                    new PartSpec("Torso",0,0,new Vector3(0f,1.04f,0f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0f,0.94f,-0.25f),new Vector3(0.71f,0.46f,0.69f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,1.105f,-0.29f),new Vector3(0.54f,0.15f,0.54f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,0.88f,0.24f),new Vector3(0.53f,0.35f,0.47f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,1.19f,-0.3f),new Vector3(0.3f,0.055f,0.36f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Stone brow",0,0,new Vector3(0f,1.68f,-0.015f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0f,0.76f,0.36f),new Vector3(0.55f,0.29f,0.4f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,0.86f,0.41f),new Vector3(0.61f,0.1f,0.3f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,0.66f,0.5f),new Vector3(0.4f,0.1f,0.2f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Face shadow",0,1,new Vector3(0f,0.77f,0.575f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0f,0.77f,0.575f),new Vector3(0.43f,0.18f,0.035f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Left rune eye",0,2,new Vector3(-0.13f,0.79f,0.602f),new BoxSpec[] {
                        new BoxSpec(new Vector3(-0.075f,0.79f,0.602f),new Vector3(0.045f,0.045f,0.026f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(-0.14f,0.825f,0.602f),new Vector3(0.045f,0.045f,0.026f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(-0.205f,0.79f,0.602f),new Vector3(0.045f,0.045f,0.026f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Right rune eye",0,2,new Vector3(0.13f,0.79f,0.602f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0.075f,0.79f,0.602f),new Vector3(0.045f,0.045f,0.026f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0.14f,0.825f,0.602f),new Vector3(0.045f,0.045f,0.026f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0.205f,0.79f,0.602f),new Vector3(0.045f,0.045f,0.026f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Chest rune",0,2,new Vector3(0f,1.12f,0.257f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0f,1.035f,0.516f),new Vector3(0.14f,0.2f,0.03f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Hip core",0,1,new Vector3(0f,0.58f,0f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0f,0.69f,-0.05f),new Vector3(0.56f,0.22f,0.67f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Left stone arm",1,0,new Vector3(0f,-0.33f,0.02f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0.3f,-0.65f,0.46f),new Vector3(0.07f,0.18f,0.1f),new Vector3(0f,0f,12f)),
                        new BoxSpec(new Vector3(0.3f,-0.7f,0.48f),new Vector3(0.08f,0.08f,0.14f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Right stone arm",2,0,new Vector3(0f,-0.33f,0.02f),new BoxSpec[] {
                        new BoxSpec(new Vector3(-0.3f,-0.65f,0.46f),new Vector3(0.07f,0.18f,0.1f),new Vector3(0f,0f,-12f)),
                        new BoxSpec(new Vector3(-0.3f,-0.7f,0.48f),new Vector3(0.08f,0.08f,0.14f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Spider leg 0",0,0,new Vector3(-0.29f,0.75f,-0.3168f),new BoxSpec[] {
                        new BoxSpec(new Vector3(-0.37f,0.65f,-0.3144f),new Vector3(0.105f,0.105f,0.2711699f),new Vector3(51.3276197f,-88.281642f,0f)),
                        new BoxSpec(new Vector3(-0.475f,0.295f,-0.312f),new Vector3(0.085f,0.085f,0.5274451f),new Vector3(84.4006607f,-90f,0f)),
                        new BoxSpec(new Vector3(-0.5f,0.025f,-0.287f),new Vector3(0.11f,0.05f,0.13f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Spider leg 1",0,0,new Vector3(0.29f,0.75f,-0.3168f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0.37f,0.65f,-0.3144f),new Vector3(0.105f,0.105f,0.2711699f),new Vector3(51.3276197f,88.281642f,0f)),
                        new BoxSpec(new Vector3(0.475f,0.295f,-0.312f),new Vector3(0.085f,0.085f,0.5274451f),new Vector3(84.4006607f,90f,0f)),
                        new BoxSpec(new Vector3(0.5f,0.025f,-0.287f),new Vector3(0.11f,0.05f,0.13f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Spider leg 2",0,0,new Vector3(-0.29f,0.75f,-0.1056f),new BoxSpec[] {
                        new BoxSpec(new Vector3(-0.37f,0.65f,-0.1048f),new Vector3(0.105f,0.105f,0.27113f),new Vector3(51.3387944f,-89.4270613f,0f)),
                        new BoxSpec(new Vector3(-0.475f,0.295f,-0.104f),new Vector3(0.085f,0.085f,0.5274451f),new Vector3(84.4006607f,-90f,0f)),
                        new BoxSpec(new Vector3(-0.5f,0.025f,-0.079f),new Vector3(0.11f,0.05f,0.13f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Spider leg 3",0,0,new Vector3(0.29f,0.75f,-0.1056f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0.37f,0.65f,-0.1048f),new Vector3(0.105f,0.105f,0.27113f),new Vector3(51.3387944f,89.4270613f,0f)),
                        new BoxSpec(new Vector3(0.475f,0.295f,-0.104f),new Vector3(0.085f,0.085f,0.5274451f),new Vector3(84.4006607f,90f,0f)),
                        new BoxSpec(new Vector3(0.5f,0.025f,-0.079f),new Vector3(0.11f,0.05f,0.13f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Spider leg 4",0,0,new Vector3(-0.29f,0.75f,0.1056f),new BoxSpec[] {
                        new BoxSpec(new Vector3(-0.37f,0.65f,0.1048f),new Vector3(0.105f,0.105f,0.27113f),new Vector3(51.3387944f,-90.5729387f,0f)),
                        new BoxSpec(new Vector3(-0.475f,0.295f,0.104f),new Vector3(0.085f,0.085f,0.5274451f),new Vector3(84.4006607f,-90f,0f)),
                        new BoxSpec(new Vector3(-0.5f,0.025f,0.129f),new Vector3(0.11f,0.05f,0.13f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Spider leg 5",0,0,new Vector3(0.29f,0.75f,0.1056f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0.37f,0.65f,0.1048f),new Vector3(0.105f,0.105f,0.27113f),new Vector3(51.3387944f,90.5729387f,0f)),
                        new BoxSpec(new Vector3(0.475f,0.295f,0.104f),new Vector3(0.085f,0.085f,0.5274451f),new Vector3(84.4006607f,90f,0f)),
                        new BoxSpec(new Vector3(0.5f,0.025f,0.129f),new Vector3(0.11f,0.05f,0.13f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Spider leg 6",0,0,new Vector3(-0.29f,0.75f,0.3168f),new BoxSpec[] {
                        new BoxSpec(new Vector3(-0.37f,0.65f,0.3144f),new Vector3(0.105f,0.105f,0.2711699f),new Vector3(51.3276197f,-91.718358f,0f)),
                        new BoxSpec(new Vector3(-0.475f,0.295f,0.312f),new Vector3(0.085f,0.085f,0.5274451f),new Vector3(84.4006607f,-90f,0f)),
                        new BoxSpec(new Vector3(-0.5f,0.025f,0.337f),new Vector3(0.11f,0.05f,0.13f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Spider leg 7",0,0,new Vector3(0.29f,0.75f,0.3168f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0.37f,0.65f,0.3144f),new Vector3(0.105f,0.105f,0.2711699f),new Vector3(51.3276197f,91.718358f,0f)),
                        new BoxSpec(new Vector3(0.475f,0.295f,0.312f),new Vector3(0.085f,0.085f,0.5274451f),new Vector3(84.4006607f,90f,0f)),
                        new BoxSpec(new Vector3(0.5f,0.025f,0.337f),new Vector3(0.11f,0.05f,0.13f),new Vector3(0f,0f,0f)),
                    }),
                };
                default:throw new ArgumentOutOfRangeException(nameof(style));
            }
        }
    }
}
