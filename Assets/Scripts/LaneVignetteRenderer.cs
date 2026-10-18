using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // Main-thread owned mesh/material lifecycle, separate from pure layout/recipes.
    // One shared shader, three shared materials, spatial10m batches. No Update/LateUpdate.
    // CPU work occurs only during build/settings changes; shader time supplies cosmetic wind/flow.
    [DisallowMultipleComponent]
    public sealed class LaneVignetteRenderer : MonoBehaviour
    {
        const string MotionKey = "Oracle.World.WeatherMotion";
        const int MaximumBuckets = 192;
        static readonly int WeatherId = Shader.PropertyToID("_OracleLaneWeather");
        static Vector2 windDirection = new Vector2(.82f,.57f);
        static float windStrength = .72f;
        readonly Dictionary<Key, Data> batches = new Dictionary<Key, Data>();
        // Preserve ownership references across supported Unity serialization/reload paths.
        // The root compile guard still stops Play before source imports; no runtime rebuild.
        [SerializeField,HideInInspector] List<UnityEngine.Object> resources = new List<UnityEngine.Object>();
        [SerializeField,HideInInspector] Material[] materials = new Material[3];
        bool initialized;
        public bool ShaderReady { get; private set; }
        public int MeshCount { get; private set; }
        public int VertexCount { get; private set; }
        public float MaximumChunkSpan { get; private set; }

        public static bool WorldMotionEnabled => PlayerPrefs.GetInt(MotionKey,1)!=0;
        public static void SetWorldMotionEnabled(bool enabled)
        {
            PlayerPrefs.SetInt(MotionKey,enabled?1:0); PlayerPrefs.Save(); ApplyWeather();
        }
        public static void ConfigureWeather(Vector2 direction, float strength)
        {
            if(float.IsNaN(direction.x)||float.IsNaN(direction.y)||float.IsInfinity(direction.x)||float.IsInfinity(direction.y)
                ||float.IsNaN(strength)||float.IsInfinity(strength))throw new ArgumentException("Finite weather descriptor required.");
            windDirection=direction.sqrMagnitude>.0001f?direction.normalized:Vector2.right;
            windStrength=Mathf.Clamp01(strength); ApplyWeather();
        }
        static void ApplyWeather() => Shader.SetGlobalVector(WeatherId,
            new Vector4(windDirection.x,windDirection.y,windStrength,WorldMotionEnabled?1f:0f));

        public void Initialize()
        {
            if(initialized)throw new InvalidOperationException("Build one vignette renderer per arena root.");
            initialized=true; ApplyWeather();
            Shader shader=Resources.Load<Shader>("Shaders/OracleLaneWeather");
            ShaderReady=shader!=null && shader.isSupported;
            if(ShaderReady)
            {
                for(int mode=0;mode<3;mode++)
                {
                    var material=new Material(shader){name=mode==0?"Shared elemental canopy weather":mode==1?"Shared lava flow":"Shared sparse volcanic steam"};
                    material.SetFloat("_Mode",mode);material.SetColor("_BaseColor",Color.white);
                    material.SetFloat("_SrcBlend",mode==2?(float)BlendMode.SrcAlpha:(float)BlendMode.One);
                    material.SetFloat("_DstBlend",mode==2?(float)BlendMode.OneMinusSrcAlpha:(float)BlendMode.Zero);
                    material.SetFloat("_ZWrite",mode==2?0:1);material.renderQueue=mode==2?3000:2000;
                    materials[mode]=material;resources.Add(material);
                }
            }
            // Existing directional light and GraphicsPreferences retain all shadow ownership.
        }

        public void Append(LaneVignettePiece piece)
        {
            if(!ShaderReady)throw new InvalidOperationException("World must use static existing-palette fallback when shader is unavailable.");
            int mode=piece.Primitive==VignettePrimitive.CanopyBox?0:piece.Primitive==VignettePrimitive.LavaTop?1:2;
            var key=new Key(Mathf.FloorToInt((float)piece.Center.X/10f),Mathf.FloorToInt((float)piece.Center.Z/10f),mode);
            if(!batches.TryGetValue(key,out Data data))
            {
                if(batches.Count>=MaximumBuckets)throw new InvalidOperationException("Vignette renderer bucket cap exceeded.");
                data=new Data();batches.Add(key,data);
            }
            var center=new Vector3((float)piece.Center.X-key.X*10,(float)piece.Center.Y,(float)piece.Center.Z-key.Z*10);
            var size=new Vector3((float)piece.Size.X,(float)piece.Size.Y,(float)piece.Size.Z);
            Quaternion q=Quaternion.Euler(0,(float)piece.YawDegrees,0);
            var anchor=new Vector4((float)piece.TreeRoot.X,(float)piece.TreeRoot.Y,(float)piece.TreeRoot.Z,(float)piece.TreeHeight);
            var tint=new Color((float)piece.Red,(float)piece.Green,(float)piece.Blue,(float)piece.Glow);
            if(mode==0)data.Box(center,size,q,anchor,tint);
            else if(mode==1)data.Face(center,q,Vector3.up,new Vector3(-size.x/2,0,size.z/2),new Vector3(size.x/2,0,size.z/2),
                new Vector3(size.x/2,0,-size.z/2),new Vector3(-size.x/2,0,-size.z/2),Vector4.zero,Color.white,false);
            else data.Face(center,q,Vector3.forward,new Vector3(-size.x/2,-size.y/2,0),new Vector3(size.x/2,-size.y/2,0),
                new Vector3(size.x/2,size.y/2,0),new Vector3(-size.x/2,size.y/2,0),Vector4.zero,Color.white,true);
        }
        public void Finish()
        {
            foreach(var pair in batches)
            {
                Data data=pair.Value; Key key=pair.Key;
                var mesh=new Mesh{name="Lane weather chunk",indexFormat=IndexFormat.UInt32};
                mesh.SetVertices(data.Vertices);mesh.SetNormals(data.Normals);mesh.SetColors(data.Colors);
                mesh.SetUVs(0,data.UV);mesh.SetUVs(1,data.Anchors);mesh.SetTriangles(data.Indices,0);mesh.RecalculateBounds();
                Bounds bounds=mesh.bounds;bounds.Expand(new Vector3(1.6f,.1f,1.6f));mesh.bounds=bounds;mesh.UploadMeshData(true);resources.Add(mesh);
                var go=new GameObject("Lane weather chunk "+key.X+","+key.Z+" mode"+key.Mode);
                go.transform.SetParent(transform,false);go.transform.localPosition=new Vector3(key.X*10,0,key.Z*10);
                go.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=materials[key.Mode];
                renderer.shadowCastingMode=key.Mode==0?ShadowCastingMode.On:ShadowCastingMode.Off;
                renderer.receiveShadows=key.Mode==0;renderer.lightProbeUsage=LightProbeUsage.BlendProbes;
                renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
                MeshCount++;VertexCount+=mesh.vertexCount;MaximumChunkSpan=Mathf.Max(MaximumChunkSpan,Mathf.Max(bounds.size.x,bounds.size.z));
            }
            batches.Clear();
        }
        void OnDestroy()
        {
            batches.Clear();
            foreach(UnityEngine.Object resource in resources)if(resource!=null)Destroy(resource);
            resources.Clear();ShaderReady=false;
        }
        readonly struct Key:IEquatable<Key>
        {
            public readonly int X,Z,Mode;public Key(int x,int z,int mode){X=x;Z=z;Mode=mode;}
            public bool Equals(Key other)=>X==other.X&&Z==other.Z&&Mode==other.Mode;
            public override bool Equals(object other)=>other is Key key&&Equals(key);
            public override int GetHashCode()=>unchecked((X*397^Z)*397^Mode);
        }
        sealed class Data
        {
            public readonly List<Vector3> Vertices=new List<Vector3>(),Normals=new List<Vector3>();
            public readonly List<Color> Colors=new List<Color>();
            public readonly List<Vector2> UV=new List<Vector2>();
            public readonly List<Vector4> Anchors=new List<Vector4>();
            public readonly List<int> Indices=new List<int>();
            public void Box(Vector3 p,Vector3 size,Quaternion q,Vector4 anchor,Color tint)
            {
                Vector3 h=size*.5f;
                Face(p,q,Vector3.forward,new Vector3(-h.x,-h.y,h.z),new Vector3(h.x,-h.y,h.z),new Vector3(h.x,h.y,h.z),new Vector3(-h.x,h.y,h.z),anchor,tint,false);
                Face(p,q,Vector3.back,new Vector3(h.x,-h.y,-h.z),new Vector3(-h.x,-h.y,-h.z),new Vector3(-h.x,h.y,-h.z),new Vector3(h.x,h.y,-h.z),anchor,tint,false);
                Face(p,q,Vector3.right,new Vector3(h.x,-h.y,h.z),new Vector3(h.x,-h.y,-h.z),new Vector3(h.x,h.y,-h.z),new Vector3(h.x,h.y,h.z),anchor,tint,false);
                Face(p,q,Vector3.left,new Vector3(-h.x,-h.y,-h.z),new Vector3(-h.x,-h.y,h.z),new Vector3(-h.x,h.y,h.z),new Vector3(-h.x,h.y,-h.z),anchor,tint,false);
                Face(p,q,Vector3.up,new Vector3(-h.x,h.y,h.z),new Vector3(h.x,h.y,h.z),new Vector3(h.x,h.y,-h.z),new Vector3(-h.x,h.y,-h.z),anchor,tint,false);
                Face(p,q,Vector3.down,new Vector3(-h.x,-h.y,-h.z),new Vector3(h.x,-h.y,-h.z),new Vector3(h.x,-h.y,h.z),new Vector3(-h.x,-h.y,h.z),anchor,tint,false);
            }
            public void Face(Vector3 p,Quaternion q,Vector3 normal,Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector4 anchor,Color tint,bool back)
            {
                int n=Vertices.Count;Vector3 nn=q*normal;
                Vertices.Add(p+q*a);Vertices.Add(p+q*b);Vertices.Add(p+q*c);Vertices.Add(p+q*d);
                for(int i=0;i<4;i++){Normals.Add(nn);Colors.Add(tint);Anchors.Add(anchor);}
                UV.Add(Vector2.zero);UV.Add(Vector2.right);UV.Add(Vector2.one);UV.Add(Vector2.up);
                Indices.Add(n);Indices.Add(n+1);Indices.Add(n+2);Indices.Add(n);Indices.Add(n+2);Indices.Add(n+3);
                if(back){Indices.Add(n);Indices.Add(n+2);Indices.Add(n+1);Indices.Add(n);Indices.Add(n+3);Indices.Add(n+2);}
            }
        }
    }
}
