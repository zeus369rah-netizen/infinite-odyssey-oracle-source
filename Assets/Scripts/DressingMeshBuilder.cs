using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace OdysseyOracle.VisualDressing
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct GrassSeed
    {
        public Vector3 RootLocal;
        public float YawRadians, Height, Width, PhaseRadians;
        public uint Rgba;
    }
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct CandleFlameSeed
    {
        public Vector3 WickLocal;
        public float PhaseRadians, Height, Radius;
        public uint StableId, ReservedZero;
    }
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct CrystalEmitterSeed
    {
        public Vector3 RootLocal;
        public float OrbitRadius, RiseHeight, ParticleSize, CyclesPerSecond;
        public uint StableId;
    }

    // COLD MAIN-THREAD BAKER ONLY. Returns one owned mesh, never one object per blade/flame/mote.
    // Root supplies accepted supported positions and shared materials, then owns the result lifecycle.
    public static class DressingMeshBuilder
    {
        public const int MaximumGrassBlades = 512, MaximumCandleFlames = 256;
        public const int MaximumCrystalEmitters = 8, MotesPerEmitter = 24;

        public static Mesh Grass(IReadOnlyList<GrassSeed> seeds)
        {
            RequireCount(seeds, MaximumGrassBlades);
            var b = new Buffer(seeds.Count * 10, seeds.Count * 48);
            for (int i = 0; i < seeds.Count; i++)
            {
                GrassSeed s = seeds[i];
                if (!Finite(s.RootLocal) || !Finite(s.YawRadians) || !Finite(s.PhaseRadians) ||
                    !Between(s.Height, .05f, 1f) || !Between(s.Width, .005f, .12f)) throw new ArgumentException("Invalid grass seed.");
                int start = b.Positions.Count;
                Color32 color = new Color32((byte)s.Rgba, (byte)(s.Rgba >> 8), (byte)(s.Rgba >> 16), (byte)(s.Rgba >> 24));
                float sin = Mathf.Sin(s.YawRadians), cos = Mathf.Cos(s.YawRadians);
                for (int ring = 0; ring < 2; ring++)
                for (int side = 0; side < 4; side++)
                {
                    float a = side * Mathf.PI * .5f, width = s.Width * .5f * (ring == 0 ? 1f : .72f);
                    float x = Mathf.Cos(a) * width, z = Mathf.Sin(a) * width * .22f;
                    float weight = ring == 0 ? 0f : .55f;
                    b.Add(s.RootLocal + new Vector3(cos*x+sin*z, weight*s.Height, -sin*x+cos*z),
                        new Vector4(weight,s.PhaseRadians,0f,s.Height), s.RootLocal, color);
                }
                b.Add(s.RootLocal+Vector3.up*s.Height,new Vector4(1,s.PhaseRadians,0,s.Height),s.RootLocal,color);
                b.Add(s.RootLocal,new Vector4(0,s.PhaseRadians,0,s.Height),s.RootLocal,color);
                for (int side = 0; side < 4; side++)
                {
                    int next = (side+1)%4;
                    b.Tri(start+side,start+next,start+4+next); b.Tri(start+side,start+4+next,start+4+side);
                    b.Tri(start+4+side,start+4+next,start+8); b.Tri(start+9,start+next,start+side);
                }
            }
            return b.Finish("Original volumetric grass blades", .22f, false);
        }

        public static Mesh CandleFlames(IReadOnlyList<CandleFlameSeed> seeds)
        {
            RequireCount(seeds, MaximumCandleFlames);
            var b = new Buffer(seeds.Count*20,seeds.Count*108);
            for (int i = 0; i < seeds.Count; i++)
            {
                CandleFlameSeed s = seeds[i];
                if (!Finite(s.WickLocal) || !Finite(s.PhaseRadians) || s.ReservedZero != 0 ||
                    !Between(s.Height,.03f,.3f) || !Between(s.Radius,.005f,.08f)) throw new ArgumentException("Invalid candle seed.");
                int start = b.Positions.Count;
                for (int ring = 0; ring < 3; ring++)
                for (int side = 0; side < 6; side++)
                {
                    float weight = ring==0?0f:ring==1?.35f:.75f;
                    float radius = s.Radius*(ring==1?1f:.35f), a = side*Mathf.PI/3f;
                    b.Add(s.WickLocal+new Vector3(Mathf.Cos(a)*radius,weight*s.Height,Mathf.Sin(a)*radius),
                        new Vector4(weight,s.PhaseRadians,1f,s.Height),new Vector4(s.WickLocal.x,s.WickLocal.y,s.WickLocal.z,i),new Color32(255,255,255,255));
                }
                b.Add(s.WickLocal+Vector3.up*s.Height,new Vector4(1,s.PhaseRadians,1,s.Height),new Vector4(s.WickLocal.x,s.WickLocal.y,s.WickLocal.z,i),new Color32(255,255,255,255));
                b.Add(s.WickLocal,new Vector4(0,s.PhaseRadians,1,s.Height),new Vector4(s.WickLocal.x,s.WickLocal.y,s.WickLocal.z,i),new Color32(255,255,255,255));
                for (int side=0;side<6;side++)
                {
                    int next=(side+1)%6;
                    for(int ring=0;ring<2;ring++)
                    { int a=start+ring*6+side,c=start+(ring+1)*6+side,d=start+(ring+1)*6+next,e=start+ring*6+next; b.Tri(a,e,d);b.Tri(a,d,c); }
                    b.Tri(start+12+side,start+12+next,start+18); b.Tri(start+19,start+next,start+side);
                }
            }
            return b.Finish("Original shader candle flames", .08f, false);
        }

        public static Mesh CrystalMotes(IReadOnlyList<CrystalEmitterSeed> seeds)
        {
            RequireCount(seeds,MaximumCrystalEmitters);
            var b=new Buffer(seeds.Count*MotesPerEmitter*4,seeds.Count*MotesPerEmitter*6);
            Vector3 min=Vector3.zero,max=Vector3.zero;
            for(int emitter=0;emitter<seeds.Count;emitter++)
            {
                CrystalEmitterSeed s=seeds[emitter];
                if(!Finite(s.RootLocal)||!Between(s.OrbitRadius,.02f,.6f)||!Between(s.RiseHeight,.1f,2f)||
                    !Between(s.ParticleSize,.005f,.06f)||!Between(s.CyclesPerSecond,.05f,1f))throw new ArgumentException("Invalid crystal seed.");
                float billboardRadius=s.ParticleSize*1.414213563f;
                Vector3 extent=new Vector3(s.OrbitRadius+billboardRadius,billboardRadius,s.OrbitRadius+billboardRadius);
                Vector3 low=s.RootLocal-extent,high=s.RootLocal+new Vector3(extent.x,s.RiseHeight+billboardRadius,extent.z);
                if(emitter==0){min=low;max=high;}else{min=Vector3.Min(min,low);max=Vector3.Max(max,high);}
                for(int mote=0;mote<MotesPerEmitter;mote++)
                {
                    int start=b.Positions.Count;
                    float phase=(mote+.37f)/MotesPerEmitter;
                    float angle=(mote*2.39996323f)+(s.StableId%4096u)*.013f;
                    for(int corner=0;corner<4;corner++)
                        b.Add(s.RootLocal,new Vector4((corner==0||corner==3)?-1:1,corner<2?-1:1,phase,angle),
                            new Vector4(s.OrbitRadius,s.RiseHeight,s.ParticleSize,s.CyclesPerSecond),new Color32(255,255,255,255));
                    b.Tri(start,start+1,start+2);b.Tri(start,start+2,start+3);
                }
            }
            Mesh mesh=b.Finish("Bounded original purple crystal motes",0f,true);
            mesh.bounds=seeds.Count==0?new Bounds(Vector3.zero,Vector3.zero):new Bounds((min+max)*.5f,max-min);
            return mesh;
        }

        private static bool Finite(float x)=>!float.IsNaN(x)&&!float.IsInfinity(x);
        private static bool Finite(Vector3 p)=>Finite(p.x)&&Finite(p.y)&&Finite(p.z)&&Mathf.Abs(p.x)<=1000&&Mathf.Abs(p.y)<=1000&&Mathf.Abs(p.z)<=1000;
        private static bool Between(float value,float min,float max)=>Finite(value)&&value>=min&&value<=max;
        private static void RequireCount<T>(IReadOnlyList<T> seeds,int maximum)
        {if(seeds==null||seeds.Count>maximum)throw new ArgumentException("Missing or over-cap cold seed array.");}

        private sealed class Buffer
        {
            internal readonly List<Vector3> Positions;
            private readonly List<Vector4> data,pivots;
            private readonly List<Color32> colors;
            private readonly List<int> indices;
            internal Buffer(int vertices,int triangles)
            {Positions=new List<Vector3>(vertices);data=new List<Vector4>(vertices);pivots=new List<Vector4>(vertices);colors=new List<Color32>(vertices);indices=new List<int>(triangles);}
            internal void Add(Vector3 p,Vector4 uv,Vector3 pivot,Color32 color)=>Add(p,uv,new Vector4(pivot.x,pivot.y,pivot.z,0),color);
            internal void Add(Vector3 p,Vector4 uv,Vector4 pivot,Color32 color)
            {Positions.Add(p);data.Add(uv);pivots.Add(pivot);colors.Add(color);}
            // XZ rings were authored counter-clockwise from above; reverse for outward mesh faces.
            internal void Tri(int a,int b,int c){indices.Add(a);indices.Add(c);indices.Add(b);}
            internal Mesh Finish(string name,float padding,bool motes)
            {
                var mesh=new Mesh{name=name};
                try
                {
                    mesh.SetVertices(Positions);mesh.SetUVs(0,data);mesh.SetUVs(1,pivots);mesh.SetColors(colors);mesh.SetTriangles(indices,0);
                    if(!motes)mesh.RecalculateNormals();
                    mesh.RecalculateBounds();Bounds bounds=mesh.bounds;bounds.Expand(padding*2f);mesh.bounds=bounds;
                    return mesh;
                }
                catch{if(Application.isPlaying)UnityEngine.Object.Destroy(mesh);else UnityEngine.Object.DestroyImmediate(mesh);throw;}
            }
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: supported geometry-root local metres, Y-up; all baked roots/pivots are already accepted by World.
// Array Formats:32B seed structs; grass<=512x10 vertices, candle<=256x20, crystal<=8x24x4; immutable mesh UV0/UV1.
// Dependencies/Pointers: COLD Mesh/List work only, no Update/per-blade GameObject/material clone/light/collider; caller owns returned mesh.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing refuge geometry/world axes and support pivots unchanged; elemental numeric state applies only to the supported refuge.
// Array Formats: Existing owner arrays remain canonical; fixed512-node/8word reaction state supplies bounded scalar presentation gains.
// Dependencies/Pointers: Host accepted single Fire/Earth/Wind shots only; existing Object adapters retain materials/lights/lifetime, no rewards/saves or shrine/portal gameplay mutation.
