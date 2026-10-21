using System;
using System.Collections.Generic;
using UnityEngine;

namespace OdysseyOracle.VisualDressing
{
    public struct RaykonPalette { public Color Robe, Bone, Metal, Rune; }

    public sealed class RaykonMeshes : IDisposable
    {
        public Mesh Body { get; internal set; }
        public Mesh Staff { get; internal set; }
        public Mesh Cauldron { get; internal set; }
        public Vector3 SlamSupportLocal { get; internal set; }
        public static Vector3 StaffPivot => new Vector3(.5f,1.28f,.43f);
        public static Bounds BodyColliderBounds => new Bounds(new Vector3(0,1.085f,0),new Vector3(.88f,2.17f,.65f));
        public static Bounds CauldronColliderBounds => new Bounds(new Vector3(.58f,.32f,.78f),new Vector3(.82f,.64f,.82f));
        public void Dispose()
        {
            Release(Body);Body=null;Release(Staff);Staff=null;Release(Cauldron);Cauldron=null;
        }
        private static void Release(Mesh mesh)
        {if(mesh!=null){if(Application.isPlaying)UnityEngine.Object.Destroy(mesh);else UnityEngine.Object.DestroyImmediate(mesh);}}
    }

    // Original cold mathematical voxel composition; borrowed palette colors are sampled by root once.
    public static class RaykonModelBuilder
    {
        public const float SlamAngleRadians=.8f;
        public const int MaximumBoxesPerMesh=96;
        public static RaykonMeshes Build(RaykonPalette p)
        {
            var owner=new RaykonMeshes();
            try
            {
                var body=new Model();
                body.Box(new Vector3(0,.50f,0),new Vector3(.76f,1,.44f),p.Robe);
                body.Box(new Vector3(0,1.27f,0),new Vector3(.59f,.58f,.41f),p.Robe);
                body.Box(new Vector3(0,.98f,.035f),new Vector3(.79f,.10f,.49f),p.Metal);
                body.Box(new Vector3(0,1.0f,.294f),new Vector3(.12f,.12f,.04f),p.Rune,1);
                for(int side=-1;side<=1;side+=2)
                {
                    body.Box(new Vector3(side*.18f,.10f,.09f),new Vector3(.25f,.20f,.36f),p.Metal);
                    body.Box(new Vector3(side*.36f,1.32f,0),new Vector3(.19f,.30f,.27f),p.Robe);
                    body.Box(new Vector3(side*.39f,1.06f,.06f),new Vector3(.16f,.28f,.21f),p.Robe);
                    body.Box(new Vector3(side*.39f,.90f,.10f),new Vector3(.13f,.15f,.16f),p.Bone);
                    body.Box(new Vector3(side*.19f,1.83f,.05f),new Vector3(.09f,.34f,.31f),p.Robe);
                }
                body.Box(new Vector3(0,1.84f,.035f),new Vector3(.33f,.34f,.28f),p.Bone);
                body.Box(new Vector3(0,2.055f,-.015f),new Vector3(.49f,.12f,.40f),p.Robe);
                body.Box(new Vector3(0,2.14f,-.035f),new Vector3(.31f,.05f,.27f),p.Robe);
                body.Box(new Vector3(0,1.83f,-.14f),new Vector3(.46f,.37f,.08f),p.Robe);
                for(int side=-1;side<=1;side+=2)
                {
                    body.Box(new Vector3(side*.075f,1.89f,.187f),new Vector3(.09f,.055f,.025f),p.Metal);
                    body.Box(new Vector3(side*.075f,1.89f,.205f),new Vector3(.035f,.035f,.015f),p.Rune,1);
                }
                body.Box(new Vector3(0,1.80f,.216f),new Vector3(.065f,.10f,.07f),p.Bone);
                // Original dark cavity is hidden behind the closed rigid beard/jaw.
                body.Box(new Vector3(0,1.725f,.205f),new Vector3(.18f,.045f,.03f),Color.Lerp(p.Metal,Color.black,.85f));
                body.Box(new Vector3(0,1.63f,.20f),new Vector3(.30f,.23f,.12f),p.Bone,0,3f);
                for(int strand=0;strand<5;strand++)
                    body.Box(new Vector3((strand-2)*.052f,1.49f+(strand%2)*.025f,.23f),new Vector3(.042f,.16f,.055f),p.Bone,0,3f);
                for(int panel=0;panel<5;panel++)
                    body.Box(new Vector3((panel-2)*.13f,.43f,.24f),new Vector3(.085f,.64f,.035f),panel%2==0?p.Metal:p.Robe);
                owner.Body=body.Finish("Original Raykon layered robe hood and beard",.48f);

                var staff=new Model();
                Color wood=Color.Lerp(p.Robe,p.Metal,.45f);
                staff.Box(new Vector3(.58f,1.35f,.55f),new Vector3(.065f,1.90f,.065f),wood);
                for(int band=0;band<8;band++)
                    staff.Box(new Vector3(.58f,.60f+band*.19f,.55f),new Vector3(.085f,.035f,.085f),p.Metal);
                staff.Box(new Vector3(.58f,2.34f,.55f),new Vector3(.17f,.20f,.17f),p.Metal);
                staff.Box(new Vector3(.58f,2.49f,.55f),new Vector3(.11f,.18f,.11f),p.Rune,1);
                owner.SlamSupportLocal=staff.LowestAtSlam(SlamAngleRadians);
                owner.Staff=staff.Finish("Raykon original rigid cauldron staff",2.6f);

                var pot=new Model(true);Vector3 c=new Vector3(.58f,0,.78f);
                for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)
                    pot.Box(c+new Vector3(x*.25f,.055f,z*.25f),new Vector3(.14f,.11f,.14f),p.Metal);
                pot.Box(c+new Vector3(0,.18f,0),new Vector3(.58f,.16f,.58f),p.Metal);
                for(int side=-1;side<=1;side+=2)
                {
                    pot.Box(c+new Vector3(side*.33f,.40f,0),new Vector3(.08f,.40f,.67f),p.Metal);
                    pot.Box(c+new Vector3(0,.40f,side*.33f),new Vector3(.59f,.40f,.08f),p.Metal);
                    pot.Box(c+new Vector3(side*.35f,.61f,0),new Vector3(.11f,.06f,.78f),p.Metal);
                    pot.Box(c+new Vector3(0,.61f,side*.35f),new Vector3(.62f,.06f,.11f),p.Metal);
                }
                pot.Box(c+new Vector3(0,.475f,0),new Vector3(.57f,.025f,.57f),p.Rune,.6f,1f);
                // Six original rigid bubble cubes share this cauldron mesh; no particles/Objects/lights.
                for(int bubble=0;bubble<6;bubble++)
                {
                    float angle=bubble*2.39996323f;
                    Vector3 center=c+new Vector3(Mathf.Cos(angle)*.18f,.50f,Mathf.Sin(angle)*.18f);
                    pot.Box(center,new Vector3(.045f,.045f,.045f),p.Rune,.8f,2f,(bubble+.5f)/6f);
                }
                owner.Cauldron=pot.Finish("Raykon original supported alchemy cauldron",.02f);
                Bounds brewBounds=owner.Cauldron.bounds;
                brewBounds.Encapsulate(c+new Vector3(.23f,.78f,.23f));
                brewBounds.Encapsulate(c+new Vector3(-.23f,.45f,-.23f));
                owner.Cauldron.bounds=brewBounds;
                return owner;
            }
            catch{owner.Dispose();throw;}
        }

        private sealed class Model
        {
            private readonly List<Vector3> vertices=new List<Vector3>(MaximumBoxesPerMesh*24);
            private readonly List<Vector3> normals=new List<Vector3>(MaximumBoxesPerMesh*24);
            private readonly List<Color> colors=new List<Color>(MaximumBoxesPerMesh*24);
            private readonly List<Vector2> uv=new List<Vector2>(MaximumBoxesPerMesh*24);
            private readonly List<Vector4> fx;
            internal Model(bool enableFx=false){if(enableFx)fx=new List<Vector4>(MaximumBoxesPerMesh*24);}
            private readonly List<int> indices=new List<int>(MaximumBoxesPerMesh*36);
            private int boxes;
            internal void Box(Vector3 c,Vector3 s,Color color,float glow=0,float effect=0,float phase=0)
            {
                if(++boxes>MaximumBoxesPerMesh)throw new InvalidOperationException("Raykon box cap.");
                Vector3 r=s*.5f;
                Face(c,new Vector3(-r.x,-r.y,-r.z),new Vector3(-r.x,r.y,-r.z),new Vector3(r.x,r.y,-r.z),new Vector3(r.x,-r.y,-r.z),Vector3.back,color,glow,effect,phase);
                Face(c,new Vector3(r.x,-r.y,r.z),new Vector3(r.x,r.y,r.z),new Vector3(-r.x,r.y,r.z),new Vector3(-r.x,-r.y,r.z),Vector3.forward,color,glow,effect,phase);
                Face(c,new Vector3(-r.x,-r.y,r.z),new Vector3(-r.x,r.y,r.z),new Vector3(-r.x,r.y,-r.z),new Vector3(-r.x,-r.y,-r.z),Vector3.left,color,glow,effect,phase);
                Face(c,new Vector3(r.x,-r.y,-r.z),new Vector3(r.x,r.y,-r.z),new Vector3(r.x,r.y,r.z),new Vector3(r.x,-r.y,r.z),Vector3.right,color,glow,effect,phase);
                Face(c,new Vector3(-r.x,r.y,-r.z),new Vector3(-r.x,r.y,r.z),new Vector3(r.x,r.y,r.z),new Vector3(r.x,r.y,-r.z),Vector3.up,color,glow,effect,phase);
                Face(c,new Vector3(-r.x,-r.y,r.z),new Vector3(-r.x,-r.y,-r.z),new Vector3(r.x,-r.y,-r.z),new Vector3(r.x,-r.y,r.z),Vector3.down,color,glow,effect,phase);
            }
            private void Face(Vector3 c,Vector3 a,Vector3 b,Vector3 d,Vector3 e,Vector3 n,Color color,float glow,float effect,float phase)
            {
                int at=vertices.Count;
                vertices.Add(c+a);vertices.Add(c+b);vertices.Add(c+d);vertices.Add(c+e);
                for(int i=0;i<4;i++){normals.Add(n);colors.Add(color);uv.Add(new Vector2(glow,effect));if(fx!=null)fx.Add(new Vector4(c.x,c.y,c.z,phase));}
                indices.Add(at);indices.Add(at+1);indices.Add(at+2);indices.Add(at);indices.Add(at+2);indices.Add(at+3);
            }
            internal Vector3 LowestAtSlam(float angle)
            {
                // Pure cold vertex math; yaw-only logical roots preserve world Y.
                float sine=Mathf.Sin(angle),cosine=Mathf.Cos(angle),lowest=float.PositiveInfinity;Vector3 result=Vector3.zero;
                for(int i=0;i<vertices.Count;i++)
                {Vector3 v=vertices[i];float y=cosine*v.y-sine*v.z;if(y<lowest){lowest=y;result=v;}}
                return result;
            }
            internal Mesh Finish(string name,float padding)
            {
                var mesh=new Mesh{name=name};
                try{mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetColors(colors);mesh.SetUVs(0,uv);if(fx!=null)mesh.SetUVs(1,fx);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();Bounds b=mesh.bounds;b.Expand(padding*2);mesh.bounds=b;return mesh;}
                catch{if(Application.isPlaying)UnityEngine.Object.Destroy(mesh);else UnityEngine.Object.DestroyImmediate(mesh);throw;}
            }
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: original local+Z front,+Y up/metres, supported floorY0, yaw-only unit-scale root.
// Array Formats:3 cold combined meshes,<=96 boxes/mesh,24 vertices/12 triangles per box; vertex COLOR,UV0(glow,effect0ordinary/1brew/2bubble/3rigidJaw),cauldron-only UV1(centerXYZ,phase);body/staff streams unchanged.
// Dependencies/Pointers:root supplies borrowed palette values/material, owns talk/physics/scene; no SkinnedMeshRenderer/Update/quest authority.

