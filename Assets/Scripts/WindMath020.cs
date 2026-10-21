using System;

namespace OracleNature020
{
    public struct P020
    {
        public float X, Z;
        public P020(float x,float z){X=x;Z=z;}
    }
    public struct Edge020
    {
        public ulong Id;
        public P020 A,B;
    }
    public struct Wind020
    {
        public float Distance,Gain,Clock;
        public uint Revision;
        public byte Stage,Primed;
    }
    public struct Weight020
    {
        public float Sheltered,Breeze,Exposed;
    }
    public static class WindMath020
    {
        public const int Capacity=512;
        public const float Maximum=.095f,Rate=.035f;
        static bool Finite(float a)=>!float.IsNaN(a)&&!float.IsInfinity(a);
        static bool Valid(P020 a)=>Finite(a.X)&&Finite(a.Z)&&Math.Abs(a.X)<128&&Math.Abs(a.Z)<128;
        static float Clamp(float a,float lo,float hi)=>Math.Min(hi,Math.Max(lo,a));
        static float Smooth(float a){a=Clamp(a,0,1);return a*a*(3-2*a);}
        static bool EdgeValid(in Edge020 e)
        {
            float x=e.B.X-e.A.X,z=e.B.Z-e.A.Z;
            return e.Id!=0&&Valid(e.A)&&Valid(e.B)&&x*x+z*z>.000001f;
        }
        // A numeric oracle, not proof that inputs came from the accepted outer union.
        public static bool Validate(ReadOnlySpan<Edge020> edges)
        {
            if(edges.Length<1||edges.Length>Capacity)return false;
            for(int i=0;i<edges.Length;i++)
            {
                if(!EdgeValid(in edges[i]))return false;
                for(int j=0;j<i;j++)if(edges[j].Id==edges[i].Id)return false;
            }
            return true;
        }
        public static bool Measure(P020 p,ReadOnlySpan<Edge020> edges,out float distance)
        {
            distance=0;
            if(!Valid(p)||edges.Length<1||edges.Length>Capacity)return false;
            float best=float.MaxValue;
            for(int i=0;i<edges.Length;i++)
            {
                ref readonly Edge020 e=ref edges[i];
                if(!EdgeValid(in e))return false;
                float x=e.B.X-e.A.X,z=e.B.Z-e.A.Z;
                float t=Clamp(((p.X-e.A.X)*x+(p.Z-e.A.Z)*z)/(x*x+z*z),0,1);
                float dx=p.X-(e.A.X+t*x),dz=p.Z-(e.A.Z+t*z);
                float d=dx*dx+dz*dz;
                if(!Finite(d))return false;
                best=Math.Min(best,d);
            }
            distance=(float)Math.Sqrt(best);
            return Finite(distance);
        }
        public static bool Target(float distance,out float gain,out Weight020 weights)
        {
            gain=0;weights=default;
            if(!Finite(distance)||distance<0)return false;
            if(distance>=8)
            {
                float u=Smooth((18-distance)/10);
                weights.Sheltered=1-u;weights.Breeze=u;
            }
            else
            {
                float v=Smooth((8-distance)/8);
                weights.Breeze=1-v;weights.Exposed=v;
            }
            gain=Clamp(weights.Sheltered*.025f+weights.Breeze*.055f+weights.Exposed*Maximum,.025f,Maximum);
            return Finite(gain);
        }
        public static Wind020 Create()=>new Wind020{Distance=18,Gain=0,Revision=1};
        public static bool Advance(in Wind020 before,float rawDistance,float delta,bool held,out Wind020 after)
        {
            after=before;
            if(!Finite(rawDistance)||rawDistance<0||!Finite(delta)||delta<0||
                !Finite(before.Distance)||before.Distance<0||!Finite(before.Gain)||before.Gain<0||before.Gain>Maximum||
                !Finite(before.Clock)||before.Clock<0||before.Clock>=1024||before.Revision==0||before.Stage>2||before.Primed>1)return false;
            if(held||delta==0)return true;
            if(before.Revision==uint.MaxValue)return false;
            float dt=Math.Min(delta,.1f);
            if(after.Primed==0){after.Primed=1;after.Distance=rawDistance;after.Stage=rawDistance<=4?(byte)2:rawDistance<=12?(byte)1:(byte)0;}
            else if(Math.Abs(rawDistance-after.Distance)>.25f)
                after.Distance+=(rawDistance-after.Distance)*(1-(float)Math.Exp(-dt/.5f));
            float d=after.Distance;
            if(after.Stage==0&&d<=12)after.Stage=1;
            else if(after.Stage==1){if(d>=14)after.Stage=0;else if(d<=4)after.Stage=2;}
            else if(after.Stage==2&&d>=6)after.Stage=1;
            if(!Target(d,out float target,out _))return false;
            float seconds=target>after.Gain?.8f:1.4f;
            float proposed=(target-after.Gain)*(1-(float)Math.Exp(-dt/seconds));
            after.Gain=Clamp(after.Gain+Clamp(proposed,-Rate*dt,Rate*dt),0,Maximum);
            after.Clock=(after.Clock+dt)%1024;
            after.Revision++;
            return Finite(after.Distance)&&Finite(after.Gain)&&Finite(after.Clock);
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Pure World-XZ numeric edge fixtures; no accepted union/source authority is created.
// Array Formats: Read-only at-most512 edge span, fixed scalar wind state/convex weights; no frame allocation or Unity dependency.
// Dependencies/Pointers: Root must publish the actual exposed69-floor union under exact scope and port nearest search to validated native BVH/Burst; this is a .NET oracle only.
