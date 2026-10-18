using System;
using System.Runtime.InteropServices;

namespace OdysseyOracle
{
    [StructLayout(LayoutKind.Sequential)]
    public struct CosmicNovaVertex
    {
        public float X, Y, Z, NormalX, NormalY, NormalZ;
    }

    // # ARCHITECTURE SNAPSHOT
    // Pure presentation mesh writer. Local coordinates stay inside a unit sphere;
    // the owner applies the accepted gameplay radius in world metres. Fixed caller
    // arrays own all data; no Unity objects, queries, random state or per-build arrays.
    // Main-thread use only today; this plain float-data boundary is suitable for a
    // later Burst/native-buffer adapter, but no background job execution is claimed.
    public static class CosmicNovaGeometry
    {
        public const int PaletteCount = 5, MaximumVertices = 6000, MaximumIndices = 8400;
        public const int MaximumTriangles = 2800;
        public const float LifetimeSeconds = 1.2f;

        private struct Point
        {
            public float X, Y, Z;
            public Point(float x, float y, float z) { X=x; Y=y; Z=z; }
            public static Point operator +(Point a,Point b)=>new Point(a.X+b.X,a.Y+b.Y,a.Z+b.Z);
            public static Point operator -(Point a,Point b)=>new Point(a.X-b.X,a.Y-b.Y,a.Z-b.Z);
            public static Point operator *(Point a,float b)=>new Point(a.X*b,a.Y*b,a.Z*b);
        }
        private struct Writer
        {
            public CosmicNovaVertex[] Vertices;
            public ushort[] Indices;
            public int Vertex, Index;
            public bool WriteIndices;
            private void Put(Point p, Point n)
            {
                Vertices[Vertex++]=new CosmicNovaVertex {X=p.X,Y=p.Y,Z=p.Z,NormalX=n.X,NormalY=n.Y,NormalZ=n.Z};
            }
            private void IndexTriangle(int a,int b,int c)
            {
                if(WriteIndices){Indices[Index]=(ushort)a;Indices[Index+1]=(ushort)b;Indices[Index+2]=(ushort)c;}
                Index+=3;
            }
            public void Triangle(Point a,Point b,Point c)
            {
                Point normal=Normal(a,b,c);int first=Vertex;
                Put(a,normal);Put(b,normal);Put(c,normal);IndexTriangle(first,first+1,first+2);
            }
            public void Quad(Point a,Point b,Point c,Point d,bool doubleSided)
            {
                Point normal=Normal(a,b,c);int first=Vertex;
                Put(a,normal);Put(b,normal);Put(c,normal);Put(d,normal);
                IndexTriangle(first,first+1,first+2);IndexTriangle(first,first+2,first+3);
                if(!doubleSided)return;
                first=Vertex;normal=normal*-1f;
                Put(a,normal);Put(b,normal);Put(c,normal);Put(d,normal);
                IndexTriangle(first,first+2,first+1);IndexTriangle(first,first+3,first+2);
            }
            public void Box(Point center,Point size)
            {
                float x=size.X*.5f,y=size.Y*.5f,z=size.Z*.5f;
                Point a=center+new Point(-x,-y,-z),b=center+new Point(x,-y,-z),c=center+new Point(x,y,-z),d=center+new Point(-x,y,-z);
                Point e=center+new Point(-x,-y,z),f=center+new Point(x,-y,z),g=center+new Point(x,y,z),h=center+new Point(-x,y,z);
                Quad(a,d,c,b,false);Quad(e,f,g,h,false);Quad(a,e,h,d,false);
                Quad(b,c,g,f,false);Quad(d,h,g,c,false);Quad(a,b,f,e,false);
            }
        }

        public static int ClampQuality(int quality)=>Math.Max(0,Math.Min(2,quality));
        public static int CapQuality(int requested,float effectiveParticleFactor)
        {
            int cap=!Finite(effectiveParticleFactor)||effectiveParticleFactor<=.4f?0:effectiveParticleFactor<=.7f?1:2;
            return Math.Min(ClampQuality(requested),cap);
        }
        public static float Envelope(float phase)
        {
            if(!Finite(phase)||phase<=0f||phase>=1f)return 0f;
            return Smooth(phase/.12f)*(1f-Smooth((phase-.65f)/.35f));
        }
        public static bool ValidRadius(float radius)=>Finite(radius)&&radius>0f&&radius<=32f;
        public static bool ValidCenter(float x,float y,float z)=>Finite(x)&&Finite(y)&&Finite(z)&&
            Math.Abs(x)<=10000f&&Math.Abs(y)<=10000f&&Math.Abs(z)<=10000f;

        public static void Build(float phase,int quality,CosmicNovaVertex[] vertices,ushort[] indices,
            int[] submeshStarts,int[] submeshCounts,bool writeIndices,out int vertexCount,out int indexCount)
        {
            if(vertices==null||vertices.Length<MaximumVertices||indices==null||indices.Length<MaximumIndices||
                submeshStarts==null||submeshStarts.Length<PaletteCount||submeshCounts==null||submeshCounts.Length<PaletteCount)
                throw new ArgumentException("Cosmic nova requires its fixed storage capacities.");
            float t=Finite(phase)?Clamp01(phase):0f,envelope=Envelope(t);
            quality=ClampQuality(quality);
            int ringSegments=quality==0?12:quality==1?24:32;
            int arms=quality==0?1:2,spiralSegments=quality==0?12:quality==1?24:40;
            int glyphs=quality==0?8:quality==1?16:28;
            int layers=quality==0?0:quality==1?1:2,layerSegments=quality==1?12:20;
            int stars=quality==0?4:quality==1?8:12,voxels=quality==0?4:quality==1?8:16;
            Writer writer=new Writer{Vertices=vertices,Indices=indices,WriteIndices=writeIndices};
            float expansion=.06f+.88f*(1f-(1f-t)*(1f-t));
            for(int palette=0;palette<PaletteCount;palette++)
            {
                submeshStarts[palette]=writer.Index;
                if(palette==0) Core(ref writer,envelope,t);
                else
                {
                    float band=1f-(palette-1)*.10f;
                    Ring(ref writer,ringSegments,expansion*band,.052f*envelope,t,palette);
                    for(int layer=0;layer<layers;layer++)
                        TiltedRing(ref writer,layerSegments,expansion*(.78f-layer*.14f),.033f*envelope,
                            t,palette,layer);
                    for(int arm=0;arm<arms;arm++)
                        for(int segment=0;segment<spiralSegments;segment++)
                        {
                            if(1+(segment/5+arm)%4!=palette)continue;
                            float u=(float)segment/spiralSegments,v=(float)(segment+1)/spiralSegments;
                            SpiralSegment(ref writer,u,v,arm,t,.038f*envelope);
                        }
                    for(int glyph=0;glyph<glyphs;glyph++)
                        if(1+glyph%4==palette) Glyph(ref writer,glyph,t,envelope);
                    for(int star=0;star<stars;star++)if(1+star%4==palette)
                        StarRibbon(ref writer,star,t,envelope);
                    for(int voxel=0;voxel<voxels;voxel++)if(1+voxel%4==palette)
                        ShockVoxel(ref writer,voxel,voxels,expansion,t,envelope);
                }
                submeshCounts[palette]=writer.Index-submeshStarts[palette];
            }
            vertexCount=writer.Vertex;indexCount=writer.Index;
        }

        private static void Core(ref Writer writer,float envelope,float t)
        {
            Point center=new Point(0f,.10f,0f);
            float radius=.155f*envelope*(1f+.08f*Sin(t*15f));
            Point a=center+new Point(radius,0f,0f),b=center+new Point(0f,0f,radius),
                c=center+new Point(-radius,0f,0f),d=center+new Point(0f,0f,-radius),
                top=center+new Point(0f,.24f*envelope,0f),bottom=center+new Point(0f,-.13f*envelope,0f);
            writer.Triangle(top,b,a);writer.Triangle(top,c,b);writer.Triangle(top,d,c);writer.Triangle(top,a,d);
            writer.Triangle(bottom,a,b);writer.Triangle(bottom,b,c);writer.Triangle(bottom,c,d);writer.Triangle(bottom,d,a);
        }
        private static void Ring(ref Writer writer,int segments,float radius,float width,float phase,int band)
        {
            float inner=Math.Max(0f,radius-width*.5f),outer=radius+width*.5f;
            float y=.018f+.008f*Sin(phase*11f+band);
            for(int segment=0;segment<segments;segment++)
            {
                float a=segment*(6.28318530718f/segments),b=(segment+1)*(6.28318530718f/segments);
                writer.Quad(new Point(Cos(a)*inner,y,Sin(a)*inner),new Point(Cos(a)*outer,y,Sin(a)*outer),
                    new Point(Cos(b)*outer,y,Sin(b)*outer),new Point(Cos(b)*inner,y,Sin(b)*inner),true);
            }
        }
        private static void SpiralSegment(ref Writer writer,float u,float v,int arm,float phase,float width)
        {
            Point a=SpiralPoint(u,arm,phase),b=SpiralPoint(v,arm,phase);
            Point across=Normalize(new Point(-(b.Z-a.Z),0f,b.X-a.X))*width*.5f;
            writer.Quad(a-across,a+across,b+across,b-across,true);
        }
        private static Point SpiralPoint(float u,int arm,float phase)
        {
            float angle=phase*5.5f+arm*3.14159265359f+u*7.5f;
            float radius=.12f+.72f*u;
            return new Point(Cos(angle)*radius,.06f+.14f*Sin(u*9f+phase*7f),Sin(angle)*radius);
        }
        private static void Glyph(ref Writer writer,int id,float phase,float envelope)
        {
            float seed=Fraction(id*.61803398875f+.13f),angle=id*2.39996323f+phase*.18f;
            float radius=.24f+.40f*Fraction(id*.371f+.2f);
            // Three chunky bars per original rune. Stepped fall is presentation,
            // never a particle simulation or a collision/damage participant.
            float fall=(float)Math.Floor(Fraction(phase*1.8f+seed)*10f)/10f;
            Point position=new Point(Cos(angle)*radius,.055f+(1f-fall)*(.34f+.16f*seed),Sin(angle)*radius);
            float sign=(id&1)==0?1f:-1f;
            writer.Box(position,new Point(.027f,.11f,.027f)*envelope);
            writer.Box(position+new Point(sign*.025f*envelope,-.036f*envelope,0f),new Point(.075f,.026f,.027f)*envelope);
            writer.Box(position+new Point(-sign*.021f*envelope,.028f*envelope,0f),new Point(.052f,.024f,.027f)*envelope);
        }
        // # ARCHITECTURE SNAPSHOT
        // Optional denser presentation only. All detail is written into the same
        // five persistent submeshes; topology is quality-fixed, with no new objects,
        // materials, lights, gameplay events or radius/lifetime changes.
        private static Point PlanePoint(float angle,float radius,float heading,float tilt)
        {
            float x=Cos(angle)*radius,z=Sin(angle)*radius;
            return new Point(Cos(heading)*x-Sin(heading)*Cos(tilt)*z,
                .025f+Sin(tilt)*z,Sin(heading)*x+Cos(heading)*Cos(tilt)*z);
        }
        private static void TiltedRing(ref Writer writer,int segments,float radius,float width,float phase,int band,int layer)
        {
            float heading=band*.82f+phase*.55f+layer*1.1f,tilt=.65f+layer*.42f;
            float inner=Math.Max(0f,radius-width*.5f),outer=radius+width*.5f;
            for(int segment=0;segment<segments;segment++)
            {
                float a=segment*(6.28318530718f/segments),b=(segment+1)*(6.28318530718f/segments);
                writer.Quad(PlanePoint(a,inner,heading,tilt),PlanePoint(a,outer,heading,tilt),
                    PlanePoint(b,outer,heading,tilt),PlanePoint(b,inner,heading,tilt),true);
            }
        }
        private static void StarRibbon(ref Writer writer,int id,float phase,float envelope)
        {
            float angle=id*2.39996323f+phase*.65f,reach=.20f+.62f*Clamp01(phase*2.2f),
                lift=.12f+.24f*Sin(id*1.17f+phase*2f);
            Point direction=new Point(Cos(angle),lift,Sin(angle));
            Point a=direction*.15f,b=direction*reach;
            Point across=new Point(-Sin(angle),0f,Cos(angle))*(.022f*envelope);
            Point vertical=new Point(0f,.018f*envelope,0f);
            writer.Quad(a-across,a+across,b+across*.35f,b-across*.35f,true);
            writer.Quad(a-vertical,a+vertical,b+vertical*.35f,b-vertical*.35f,true);
        }
        private static void ShockVoxel(ref Writer writer,int id,int total,float expansion,float phase,float envelope)
        {
            float angle=id*(6.28318530718f/total)+phase*.4f;
            float radius=expansion*.93f;
            Point center=new Point(Cos(angle)*radius,.06f+.035f*Sin(id+phase*10f),Sin(angle)*radius);
            writer.Box(center,new Point(.055f,.045f,.055f)*envelope);
        }

        private static Point Normal(Point a,Point b,Point c)
        {
            Point u=b-a,v=c-a;
            return Normalize(new Point(u.Y*v.Z-u.Z*v.Y,u.Z*v.X-u.X*v.Z,u.X*v.Y-u.Y*v.X));
        }
        private static Point Normalize(Point value)
        {
            float squared=value.X*value.X+value.Y*value.Y+value.Z*value.Z;
            return squared>.000000000001f?value*(1f/(float)Math.Sqrt(squared)):new Point(0f,1f,0f);
        }
        private static float Fraction(float value)=>value-(float)Math.Floor(value);
        private static float Sin(float value)=>(float)Math.Sin(value);
        private static float Cos(float value)=>(float)Math.Cos(value);
        private static float Clamp01(float value)=>Math.Max(0f,Math.Min(1f,value));
        private static float Smooth(float value){value=Clamp01(value);return value*value*(3f-2f*value);}
        private static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
    }
}
