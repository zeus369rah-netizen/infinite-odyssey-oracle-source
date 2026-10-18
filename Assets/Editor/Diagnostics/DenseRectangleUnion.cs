using System;

namespace OdysseyOracle.Diagnostics
{
    // # ARCHITECTURE SNAPSHOT
    // Editor audit only; double world metres, four value rectangles per sparse candidate cell.
    // Rectangle edges subdivide the cell into <=81 elementary rectangles. Every entire tile
    // must be contained by an eligible opaque rectangle: no center/corner/area-only inference.
    // No UV expansion, tolerance increase, triangle-union assumption, Unity or native ownership.
    public struct CoverageRectangleSet
    {
        private CoverageRectangle a,b,c,d;
        public int Count { get; private set; }
        public CoverageRectangle Get(int i) => i==0?a:i==1?b:i==2?c:i==3?d:throw new ArgumentOutOfRangeException(nameof(i));

        public bool Add(ProjectedCoverage p,CoveragePlane plane)
        {
            if(plane==null || !p.Known || !p.Rectangle || !Finite(p.MinU) || !Finite(p.MaxU) ||
               !Finite(p.MinV) || !Finite(p.MaxV) || !Finite(p.MinD) || !Finite(p.MaxD) ||
               p.MaxU<=p.MinU || p.MaxV<=p.MinV || p.MaxD<p.MinD ||
               p.MinD<plane.D0-DenseCoverageMath.GeometryTolerance || p.MaxD>plane.D1+DenseCoverageMath.GeometryTolerance)
                return false;
            var r=new CoverageRectangle(p.MinU,p.MaxU,p.MinV,p.MaxV);
            for(int i=0;i<Count;i++)if(Get(i).Same(r))return true;
            if(Count==4)return false; // More rectangles may remain unproved; a retained subset can still prove coverage.
            if(Count==0)a=r;else if(Count==1)b=r;else if(Count==2)c=r;else d=r;
            Count++;return true;
        }
        private static bool Finite(double value)=>!double.IsNaN(value)&&!double.IsInfinity(value);
    }

    public readonly struct CoverageRectangle
    {
        public readonly double U0,U1,V0,V1;
        public CoverageRectangle(double u0,double u1,double v0,double v1){U0=u0;U1=u1;V0=v0;V1=v1;}
        internal bool Same(CoverageRectangle r)=>U0==r.U0&&U1==r.U1&&V0==r.V0&&V1==r.V1;
    }

    public static class DenseRectangleUnion
    {
        public static bool Covers(CoverageRectangleSet rectangles,double u0,double u1,double v0,double v1)
        {
            if(rectangles.Count==0 || !Finite(u0)||!Finite(u1)||!Finite(v0)||!Finite(v1)||u1<=u0||v1<=v0)return false;
            Span<double> u=stackalloc double[10],v=stackalloc double[10];
            int nu=2,nv=2;u[0]=u0;u[1]=u1;v[0]=v0;v[1]=v1;
            for(int i=0;i<rectangles.Count;i++)
            {
                CoverageRectangle r=rectangles.Get(i);
                Insert(u,ref nu,r.U0,u0,u1);Insert(u,ref nu,r.U1,u0,u1);
                Insert(v,ref nv,r.V0,v0,v1);Insert(v,ref nv,r.V1,v0,v1);
            }
            for(int y=0;y+1<nv;y++)for(int x=0;x+1<nu;x++)
            {
                bool covered=false;
                for(int i=0;i<rectangles.Count;i++)
                {
                    CoverageRectangle r=rectangles.Get(i);
                    if(r.U0<=u[x]&&r.U1>=u[x+1]&&r.V0<=v[y]&&r.V1>=v[y+1]){covered=true;break;}
                }
                if(!covered)return false;
            }
            return true;
        }
        private static void Insert(Span<double> values,ref int count,double value,double low,double high)
        {
            if(value<=low||value>=high)return;
            int at=0;while(at<count&&values[at]<value)at++;
            if(at<count&&values[at]==value)return;
            for(int i=count;i>at;i--)values[i]=values[i-1];values[at]=value;count++;
        }
        private static bool Finite(double value)=>!double.IsNaN(value)&&!double.IsInfinity(value);
    }
}
