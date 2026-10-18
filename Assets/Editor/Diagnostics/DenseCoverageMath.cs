using System;
using System.Collections.Generic;

namespace OdysseyOracle.Diagnostics
{
    // # ARCHITECTURE SNAPSHOT
    // Double-precision world metres. Pure value math; caller owns fixed-per-surface
    // coordinate/flag arrays. No Unity objects, physics, jobs or native ownership.
    // Certified cells require complete rectangle coverage, not only a center hit.
    [Serializable] public struct CoveragePoint
    {
        public double x, y, z;
        public CoveragePoint(double x, double y, double z) { this.x=x; this.y=y; this.z=z; }
        public double Axis(int axis) => axis==0?x:axis==1?y:z;
    }
    [Serializable] public struct CoverageMask
    {
        public string Name;
        public double U0,U1,V0,V1;
        public CoverageMask(string name,double u0,double u1,double v0,double v1){Name=name;U0=u0;U1=u1;V0=v0;V1=v1;}
    }
    public struct CoverageTriangle
    {
        public CoveragePoint A,B,C;
        public bool KnownOpaque;
        public int Cull, Source;
    }
    public sealed class CoveragePlane
    {
        public string Name;
        public int Axis, Sign;
        public double U0,U1,V0,V1,D0,D1;
        public CoverageMask[] Openings=Array.Empty<CoverageMask>();
        public CoveragePoint Point(double u,double v,double d)=>Axis==0?new CoveragePoint(d,v,u):
            Axis==1?new CoveragePoint(u,d,v):new CoveragePoint(u,v,d);
        public double U(CoveragePoint p)=>Axis==0?p.z:p.x;
        public double V(CoveragePoint p)=>Axis==1?p.z:p.y;
    }
    public struct ProjectedCoverage
    {
        public double AU,AV,AD,BU,BV,BD,CU,CV,CD,MinU,MaxU,MinV,MaxV,MinD,MaxD,Det;
        public bool Known, Rectangle;
    }
    public static class DenseCoverageMath
    {
        public const double GeometryTolerance=0.000002; // Two micrometres, explicitly not centimetre hole masking.
        public const byte Certified=1, CenterHit=2, Potential=4, Unknown=8, Excluded=16;

        public static double[] Coordinates(double low,double high,double spacing,CoverageMask[] masks,bool u)
        {
            if(!(high>low)||!(spacing>0)||spacing>.020000001)throw new ArgumentOutOfRangeException(nameof(spacing));
            int divisions=(int)Math.Ceiling((high-low)/spacing);
            if(divisions>100000)throw new ArgumentOutOfRangeException(nameof(spacing));
            var values=new SortedSet<double>();
            for(int i=0;i<=divisions;i++)values.Add(low+(high-low)*i/divisions);
            foreach(var mask in masks){double a=u?mask.U0:mask.V0,b=u?mask.U1:mask.V1;if(a>low&&a<high)values.Add(a);if(b>low&&b<high)values.Add(b);}
            var result=new List<double>(values.Count);
            foreach(double value in values)if(result.Count==0||value-result[result.Count-1]>1e-9)result.Add(value);
            return result.ToArray();
        }
        public static bool IsOpening(CoveragePlane plane,double u,double v)
        {foreach(var mask in plane.Openings)if(u>mask.U0&&u<mask.U1&&v>mask.V0&&v<mask.V1)return true;return false;}

        public static bool Project(CoverageTriangle triangle,CoveragePlane plane,out ProjectedCoverage p)
        {
            p=new ProjectedCoverage {AU=plane.U(triangle.A),AV=plane.V(triangle.A),AD=triangle.A.Axis(plane.Axis),
                BU=plane.U(triangle.B),BV=plane.V(triangle.B),BD=triangle.B.Axis(plane.Axis),
                CU=plane.U(triangle.C),CV=plane.V(triangle.C),CD=triangle.C.Axis(plane.Axis),Known=triangle.KnownOpaque};
            double abx=triangle.B.x-triangle.A.x,aby=triangle.B.y-triangle.A.y,abz=triangle.B.z-triangle.A.z;
            double acx=triangle.C.x-triangle.A.x,acy=triangle.C.y-triangle.A.y,acz=triangle.C.z-triangle.A.z;
            double normal=plane.Axis==0?aby*acz-abz*acy:plane.Axis==1?abz*acx-abx*acz:abx*acy-aby*acx;
            if(triangle.KnownOpaque&&((triangle.Cull==2&&normal*plane.Sign>=-1e-12)||(triangle.Cull==1&&normal*plane.Sign<=1e-12)))return false;
            p.Det=(p.BV-p.CV)*(p.AU-p.CU)+(p.CU-p.BU)*(p.AV-p.CV);
            if(Math.Abs(p.Det)<1e-14)return false;
            p.MinU=Math.Min(p.AU,Math.Min(p.BU,p.CU));p.MaxU=Math.Max(p.AU,Math.Max(p.BU,p.CU));
            p.MinV=Math.Min(p.AV,Math.Min(p.BV,p.CV));p.MaxV=Math.Max(p.AV,Math.Max(p.BV,p.CV));
            p.MinD=Math.Min(p.AD,Math.Min(p.BD,p.CD));p.MaxD=Math.Max(p.AD,Math.Max(p.BD,p.CD));
            return p.MaxU>=plane.U0&&p.MinU<=plane.U1&&p.MaxV>=plane.V0&&p.MinV<=plane.V1&&p.MaxD>=plane.D0&&p.MinD<=plane.D1;
        }

        public static bool TryRectangle(CoverageTriangle a,CoverageTriangle b,CoveragePlane plane,out ProjectedCoverage result)
        {
            result=default;
            if(!a.KnownOpaque||!b.KnownOpaque||a.Source!=b.Source||a.Cull!=b.Cull||!Project(a,plane,out var p)||!Project(b,plane,out var q))return false;
            double d0=Math.Min(p.MinD,q.MinD),d1=Math.Max(p.MaxD,q.MaxD);
            if(d1-d0>GeometryTolerance)return false;
            double u0=Math.Min(p.MinU,q.MinU),u1=Math.Max(p.MaxU,q.MaxU),v0=Math.Min(p.MinV,q.MinV),v1=Math.Max(p.MaxV,q.MaxV);
            if(u1-u0<=GeometryTolerance||v1-v0<=GeometryTolerance)return false;
            int am=Corner(p.AU,p.AV,u0,u1,v0,v1)|Corner(p.BU,p.BV,u0,u1,v0,v1)|Corner(p.CU,p.CV,u0,u1,v0,v1);
            int bm=Corner(q.AU,q.AV,u0,u1,v0,v1)|Corner(q.BU,q.BV,u0,u1,v0,v1)|Corner(q.CU,q.CV,u0,u1,v0,v1);
            if(am<0||bm<0||Bits(am)!=3||Bits(bm)!=3||(am|bm)!=15||((am&bm)!=9&&(am&bm)!=6))return false;
            result=p;result.Rectangle=true;result.MinU=u0;result.MaxU=u1;result.MinV=v0;result.MaxV=v1;result.MinD=d0;result.MaxD=d1;return true;
        }
        private static int Corner(double u,double v,double u0,double u1,double v0,double v1)
        {
            int x=Math.Abs(u-u0)<=GeometryTolerance?0:Math.Abs(u-u1)<=GeometryTolerance?1:-1;
            int y=Math.Abs(v-v0)<=GeometryTolerance?0:Math.Abs(v-v1)<=GeometryTolerance?2:-1;
            return x<0||y<0?-1:1<<(x+y);
        }
        private static int Bits(int value){int n=0;while(value>0){n+=value&1;value>>=1;}return n;}

        public static bool Hit(ProjectedCoverage p,CoveragePlane plane,double u,double v)
        {
            if(p.Rectangle)return u>=p.MinU-GeometryTolerance&&u<=p.MaxU+GeometryTolerance&&v>=p.MinV-GeometryTolerance&&v<=p.MaxV+GeometryTolerance&&p.MinD>=plane.D0-GeometryTolerance&&p.MaxD<=plane.D1+GeometryTolerance;
            double a=((p.BV-p.CV)*(u-p.CU)+(p.CU-p.BU)*(v-p.CV))/p.Det;
            double b=((p.CV-p.AV)*(u-p.CU)+(p.AU-p.CU)*(v-p.CV))/p.Det,c=1-a-b;
            if(a< -1e-10||b< -1e-10||c< -1e-10)return false;
            double d=a*p.AD+b*p.BD+c*p.CD;return d>=plane.D0-GeometryTolerance&&d<=plane.D1+GeometryTolerance;
        }
        public static byte TestCell(ProjectedCoverage p,CoveragePlane plane,double u0,double u1,double v0,double v1)
        {
            byte flags=Potential;
            if(!p.Known)return (byte)(flags|Unknown);
            bool center=Hit(p,plane,(u0+u1)*.5,(v0+v1)*.5);
            if(center)flags|=CenterHit;
            if(Hit(p,plane,u0,v0)&&Hit(p,plane,u1,v0)&&Hit(p,plane,u0,v1)&&Hit(p,plane,u1,v1))flags|=Certified;
            return flags;
        }
        public static int CellAt(double[] coordinates,double value)
        {int lo=0,hi=coordinates.Length;while(lo<hi){int m=(lo+hi)/2;if(coordinates[m]<value)lo=m+1;else hi=m;}return Math.Max(0,Math.Min(coordinates.Length-2,lo-1));}
    }
}
