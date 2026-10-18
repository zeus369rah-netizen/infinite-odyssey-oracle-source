using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // Build-time shared assets:12 cached recipes, bounded meshes, four palette-atlas materials.
    // Eight original palette cells per tower replace per-color submeshes. No external texture dependency.
    // Owner must Dispose AFTER all presentations are destroyed, once per session/campaign lifetime.
    public sealed class TowerModelLibrary:IDisposable
    {
        public sealed class Template
        {
            public readonly TowerModelRecipe Recipe;
            public readonly Mesh[] Groups=new Mesh[TowerModelRecipes.GroupCount];
            internal Template(TowerModelRecipe recipe){Recipe=recipe;}
        }
        readonly Template[,] templates=new Template[4,3];
        readonly Material[] materials=new Material[4];
        readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>(80);
        bool disposed;
        public TowerModelLibrary()
        {
            Shader shader=Shader.Find("Universal Render Pipeline/Lit");
            if(shader==null)throw new InvalidOperationException("Existing URP Lit shader is required for tower presentation.");
            for(int type=0;type<4;type++)
            {
                var palette=new Texture2D(8,1,TextureFormat.RGBA32,false,true){name="Original tower palette "+type,filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
                var emission=new Texture2D(8,1,TextureFormat.RGBA32,false,true){name="Original tower emission mask "+type,filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
                Color element=type==0?new Color(1,.36f,.12f):type==1?new Color(.18f,.68f,1):type==2?new Color(.62f,.78f,.29f):new Color(.66f,.91f,1);
                Color[] colors={new Color(.13f,.16f,.14f),new Color(.60f,.63f,.53f),new Color(.085f,.27f,.14f),
                    new Color(.63f,.43f,.14f),new Color(.17f,.20f,.19f),new Color(.35f,.23f,.12f),element,element};
                palette.SetPixels(colors);palette.Apply(false,true);
                for(int i=0;i<8;i++)colors[i]=i==7?element:Color.black;
                emission.SetPixels(colors);emission.Apply(false,true);
                var material=new Material(shader){name="Shared original eight-color tower "+type};
                material.SetTexture("_BaseMap",palette);material.SetColor("_BaseColor",Color.white);
                material.SetTexture("_EmissionMap",emission);material.SetColor("_EmissionColor",Color.white*1.8f);
                material.EnableKeyword("_EMISSION");material.SetFloat("_Metallic",.13f);material.SetFloat("_Smoothness",.18f);
                material.globalIlluminationFlags=MaterialGlobalIlluminationFlags.None;
                materials[type]=material;owned.Add(palette);owned.Add(emission);owned.Add(material);
                for(int tier=1;tier<=3;tier++)templates[type,tier-1]=Build(TowerModelRecipes.Build(type,tier));
            }
        }
        public Template Get(int type,int tier)
        {if(disposed||type<0||type>=4||tier<1||tier>3)throw new ArgumentOutOfRangeException();return templates[type,tier-1];}
        public Material GetMaterial(int type)
        {if(disposed||type<0||type>=4)throw new ArgumentOutOfRangeException();return materials[type];}
        Template Build(TowerModelRecipe recipe)
        {
            var template=new Template(recipe);
            for(int group=0;group<TowerModelRecipes.GroupCount;group++)
            {
                var data=new MeshData();Vector3 pivot=group==0?Vector3.zero:Vector(recipe.HeadPivot);
                for(int i=0;i<recipe.Count;i++)
                {
                    TowerModelPiece p=recipe.Piece(i);if((int)p.Group!=group)continue;
                    data.Append(p,pivot);
                }
                if(data.Vertices.Count==0)continue;
                Mesh mesh=new Mesh{name=recipe.ArtName+" rank"+recipe.Tier+" "+(TowerModelGroup)group,indexFormat=IndexFormat.UInt16};
                mesh.SetVertices(data.Vertices);mesh.SetNormals(data.Normals);mesh.SetUVs(0,data.UV);mesh.SetTriangles(data.Indices,0);
                mesh.RecalculateBounds();mesh.UploadMeshData(true);owned.Add(mesh);template.Groups[group]=mesh;
            }
            return template;
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;
            foreach(UnityEngine.Object item in owned)if(item!=null)UnityEngine.Object.Destroy(item);
            owned.Clear();
        }
        internal static Vector3 Vector(TowerModelVector p)=>new Vector3((float)p.X,(float)p.Y,(float)p.Z);
        sealed class MeshData
        {
            public readonly List<Vector3> Vertices=new List<Vector3>();
            public readonly List<Vector3> Normals=new List<Vector3>();
            public readonly List<Vector2> UV=new List<Vector2>();
            public readonly List<int> Indices=new List<int>();
            Vector3 center;Quaternion rotation;Vector2 paletteUV;
            void Triangle(Vector3 a,Vector3 b,Vector3 c)
            {
                a=center+rotation*a;b=center+rotation*b;c=center+rotation*c;
                int n=Vertices.Count;Vector3 normal=Vector3.Cross(b-a,c-a).normalized;
                Vertices.Add(a);Vertices.Add(b);Vertices.Add(c);
                for(int i=0;i<3;i++){Normals.Add(normal);UV.Add(paletteUV);Indices.Add(n+i);}
            }
            void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d){Triangle(a,b,c);Triangle(a,c,d);}
            public void Append(TowerModelPiece p,Vector3 pivot)
            {
                center=Vector(p.Center)-pivot;rotation=Quaternion.Euler(Vector(p.Euler));paletteUV=new Vector2((p.Palette+.5f)/8f,.5f);
                Vector3 size=Vector(p.Size),h=size*.5f;
                if(p.Primitive==TowerModelPrimitive.Box)
                {
                    Quad(new Vector3(-h.x,-h.y,h.z),new Vector3(h.x,-h.y,h.z),new Vector3(h.x,h.y,h.z),new Vector3(-h.x,h.y,h.z));
                    Quad(new Vector3(h.x,-h.y,-h.z),new Vector3(-h.x,-h.y,-h.z),new Vector3(-h.x,h.y,-h.z),new Vector3(h.x,h.y,-h.z));
                    Quad(new Vector3(h.x,-h.y,h.z),new Vector3(h.x,-h.y,-h.z),new Vector3(h.x,h.y,-h.z),new Vector3(h.x,h.y,h.z));
                    Quad(new Vector3(-h.x,-h.y,-h.z),new Vector3(-h.x,-h.y,h.z),new Vector3(-h.x,h.y,h.z),new Vector3(-h.x,h.y,-h.z));
                    Quad(new Vector3(-h.x,h.y,h.z),new Vector3(h.x,h.y,h.z),new Vector3(h.x,h.y,-h.z),new Vector3(-h.x,h.y,-h.z));
                    Quad(new Vector3(-h.x,-h.y,-h.z),new Vector3(h.x,-h.y,-h.z),new Vector3(h.x,-h.y,h.z),new Vector3(-h.x,-h.y,h.z));
                    return;
                }
                // Clockwise perimeter viewed from above, with clipped voxel corners.
                Vector3[] lower=new Vector3[8],upper=new Vector3[8];
                for(int i=0;i<8;i++)
                {
                    double a=(i*45+22.5)*Math.PI/180;
                    float x=(float)Math.Cos(a)*h.x,z=(float)Math.Sin(a)*h.z;
                    lower[i]=new Vector3(x,-h.y,z);upper[i]=new Vector3(x*(float)p.Taper,h.y,z*(float)p.Taper);
                }
                for(int i=0;i<8;i++)
                {
                    int j=(i+1)%8;
                    if(p.Primitive==TowerModelPrimitive.Crystal)
                    {
                        Vector3 a=new Vector3(lower[i].x,0,lower[i].z),b=new Vector3(lower[j].x,0,lower[j].z);
                        Triangle(a,new Vector3(0,h.y,0),b);Triangle(b,new Vector3(0,-h.y,0),a);
                    }
                    else
                    {
                        Quad(lower[i],upper[i],upper[j],lower[j]);
                        Triangle(new Vector3(0,h.y,0),upper[j],upper[i]);
                        Triangle(new Vector3(0,-h.y,0),lower[i],lower[j]);
                    }
                }
            }
        }
    }
}
