using System;
using System.Runtime.InteropServices;

namespace OdysseyOracle.Foundation.Collision015
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct MonsterRawPose80
    {
        public float X,Y,Z; public uint Flags;
        public float CenterX,CenterY,CenterZ; public int StableId;
        public float ExtentX,ExtentY,ExtentZ; public uint Generation;
        public float RotationX,RotationY,RotationZ,RotationW;
        public float ScaleX,ScaleY,ScaleZ; public uint Reserved;
    }
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct MonsterTarget64
    {
        public float X,Y,Z,Radius;
        public float ExtentX,ExtentY,ExtentZ,HalfHeight;
        public float InverseX,InverseY,InverseZ,InverseW;
        public int StableId; public uint Generation,Shape,Flags;
    }
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct MonsterQuery80
    {
        public float X,Y,Z,Radius;
        public float EndX,EndY,EndZ,WorldFraction;
        public int Ignore0,Ignore1,Ignore2,IgnoreCount;
        public uint RequiredFlags,ForbiddenFlags,SnapshotGeneration,Sequence;
        public uint WorldFound,Reserved0,Reserved1,Reserved2;
    }
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct MonsterResult64
    {
        public float Fraction,Distance,X,Y,Z,NormalX,NormalY,NormalZ;
        public int Slot,StableId; public uint Generation,SnapshotGeneration;
        public uint Sequence,Kind,Flags,Reserved;
    }

    // Pure scalar implementation also executable outside Unity. Jobs wrap it without Object calls.
    public static class MonsterContactMath015
    {
        public const int Capacity=48,MaximumCells=4096;
        public const float WorldBound=8192f,MaximumExtent=16f,MaximumRadius=2f,CellSize=2f;
        public const uint Active=1,Flying=2,Box=1,Capsule=2;
        public const uint Miss=0,Hit=1,Invalid=2;
        public static bool Finite(double a)=>!double.IsNaN(a)&&!double.IsInfinity(a);
        public static bool Position(double x,double y,double z)=>Finite(x)&&Finite(y)&&Finite(z)&&
            Math.Abs(x)<=WorldBound&&Math.Abs(y)<=WorldBound&&Math.Abs(z)<=WorldBound;
        public static bool QueryValid(in MonsterQuery80 q)=>Position(q.X,q.Y,q.Z)&&Position(q.EndX,q.EndY,q.EndZ)&&
            Finite(q.Radius)&&q.Radius>=0&&q.Radius<=MaximumRadius&&q.IgnoreCount>=0&&q.IgnoreCount<=3&&
            (q.RequiredFlags&~3u)==0&&(q.ForbiddenFlags&~3u)==0&&q.SnapshotGeneration!=0&&q.Sequence!=0&&
            q.WorldFound<=1&&Finite(q.WorldFraction)&&q.WorldFraction>=0&&q.WorldFraction<=1&&
            q.Reserved0==0&&q.Reserved1==0&&q.Reserved2==0;
        public static bool TargetValid(in MonsterTarget64 t)
        {
            double norm=(double)t.InverseX*t.InverseX+(double)t.InverseY*t.InverseY+(double)t.InverseZ*t.InverseZ+(double)t.InverseW*t.InverseW;
            return t.StableId>0&&t.Generation!=0&&(t.Flags&~3u)==0&&(t.Flags&Active)!=0&&Position(t.X,t.Y,t.Z)&&
                Finite(norm)&&Math.Abs(norm-1d)<=.00001d&&
                (t.Shape==Box&&Finite(t.ExtentX)&&Finite(t.ExtentY)&&Finite(t.ExtentZ)&&
                    t.ExtentX>0&&t.ExtentY>0&&t.ExtentZ>0&&t.ExtentX<=MaximumExtent&&t.ExtentY<=MaximumExtent&&t.ExtentZ<=MaximumExtent&&t.Radius==0&&t.HalfHeight==0||
                 t.Shape==Capsule&&Finite(t.Radius)&&Finite(t.HalfHeight)&&t.Radius>0&&t.Radius<=MaximumExtent&&t.HalfHeight>=0&&t.HalfHeight<=MaximumExtent&&
                    t.ExtentX==0&&t.ExtentY==0&&t.ExtentZ==0);
        }
        public static bool TryBuild(in MonsterRawPose80 p,out MonsterTarget64 t)
        {
            t=default;
            if((p.Flags&Active)==0)return p.Flags==0;
            if((p.Flags&~3u)!=0||p.Reserved!=0||p.StableId<=0||p.Generation==0||!Position(p.X,p.Y,p.Z)||
                !Position(p.CenterX,p.CenterY,p.CenterZ)||!Finite(p.ScaleX)||!Finite(p.ScaleY)||!Finite(p.ScaleZ)||
                p.ScaleX<=0||p.ScaleY<=0||p.ScaleZ<=0||p.ScaleX>16||p.ScaleY>16||p.ScaleZ>16)return false;
            double norm=(double)p.RotationX*p.RotationX+(double)p.RotationY*p.RotationY+(double)p.RotationZ*p.RotationZ+(double)p.RotationW*p.RotationW;
            if(!Finite(norm)||norm<.99999d||norm>1.00001d)return false;
            double scale=1d/Math.Sqrt(norm),rx=p.RotationX*scale,ry=p.RotationY*scale,rz=p.RotationZ*scale,rw=p.RotationW*scale;
            Rotate(rx,ry,rz,rw,(double)p.CenterX*p.ScaleX,(double)p.CenterY*p.ScaleY,(double)p.CenterZ*p.ScaleZ,out double x,out double y,out double z);
            t=new MonsterTarget64{X=(float)(p.X+x),Y=(float)(p.Y+y),Z=(float)(p.Z+z),
                ExtentX=p.ExtentX*p.ScaleX,ExtentY=p.ExtentY*p.ScaleY,ExtentZ=p.ExtentZ*p.ScaleZ,
                InverseX=(float)-rx,InverseY=(float)-ry,InverseZ=(float)-rz,InverseW=(float)rw,
                StableId=p.StableId,Generation=p.Generation,Shape=Box,Flags=p.Flags};
            return TargetValid(t);
        }
        public static double Bound(in MonsterTarget64 t)=>t.Shape==Box?
            Math.Sqrt((double)t.ExtentX*t.ExtentX+(double)t.ExtentY*t.ExtentY+(double)t.ExtentZ*t.ExtentZ):t.Radius+t.HalfHeight;
        public static void Rotate(double qx,double qy,double qz,double qw,double x,double y,double z,out double ox,out double oy,out double oz)
        {
            double tx=2*(qy*z-qz*y),ty=2*(qz*x-qx*z),tz=2*(qx*y-qy*x);
            ox=x+qw*tx+qy*tz-qz*ty;oy=y+qw*ty+qz*tx-qx*tz;oz=z+qw*tz+qx*ty-qy*tx;
        }
        public static bool TryHit(in MonsterQuery80 q,in MonsterTarget64 t,int slot,out MonsterResult64 result)
        {
            result=default;
            if(!QueryValid(q)||!TargetValid(t)||slot<0||slot>=Capacity)return false;
            if((t.Flags&q.RequiredFlags)!=q.RequiredFlags||(t.Flags&q.ForbiddenFlags)!=0||
                q.IgnoreCount>0&&t.StableId==q.Ignore0||q.IgnoreCount>1&&t.StableId==q.Ignore1||q.IgnoreCount>2&&t.StableId==q.Ignore2)return false;
            Rotate(t.InverseX,t.InverseY,t.InverseZ,t.InverseW,q.X-t.X,q.Y-t.Y,q.Z-t.Z,out double x,out double y,out double z);
            Rotate(t.InverseX,t.InverseY,t.InverseZ,t.InverseW,(double)q.EndX-q.X,(double)q.EndY-q.Y,(double)q.EndZ-q.Z,out double dx,out double dy,out double dz);
            bool found=t.Shape==Box?SweepBox(x,y,z,dx,dy,dz,t.ExtentX,t.ExtentY,t.ExtentZ,q.Radius,out double fraction):
                SweepCapsule(x,y,z,dx,dy,dz,t.HalfHeight,(double)t.Radius+q.Radius,out fraction);
            // World adapter and result ABI use float fractions: compare in that same domain.
            // Comparing double .4 to a float .4 promoted back to double would incorrectly defeat the tie.
            if(!found||q.WorldFound!=0&&(float)fraction>=q.WorldFraction)return false;
            double px=x+dx*fraction,py=y+dy*fraction,pz=z+dz*fraction;
            double cx=t.Shape==Box?Clamp(px,-t.ExtentX,t.ExtentX):0,cy=t.Shape==Box?Clamp(py,-t.ExtentY,t.ExtentY):Clamp(py,-t.HalfHeight,t.HalfHeight),cz=t.Shape==Box?Clamp(pz,-t.ExtentZ,t.ExtentZ):0;
            double nx=px-cx,ny=py-cy,nz=pz-cz,nn=Math.Sqrt(nx*nx+ny*ny+nz*nz);
            if(nn>0){nx/=nn;ny/=nn;nz/=nn;}else{nn=Math.Sqrt(dx*dx+dy*dy+dz*dz);nx=nn>0?-dx/nn:0;ny=nn>0?-dy/nn:1;nz=nn>0?-dz/nn:0;}
            if(t.Shape==Capsule){cx+=nx*t.Radius;cy+=ny*t.Radius;cz+=nz*t.Radius;}
            Rotate(-t.InverseX,-t.InverseY,-t.InverseZ,t.InverseW,cx,cy,cz,out double wx,out double wy,out double wz);
            Rotate(-t.InverseX,-t.InverseY,-t.InverseZ,t.InverseW,nx,ny,nz,out double wnx,out double wny,out double wnz);
            double length=Math.Sqrt(dx*dx+dy*dy+dz*dz);
            result=new MonsterResult64{Kind=Hit,Fraction=(float)fraction,Distance=(float)(length*fraction),X=(float)(t.X+wx),Y=(float)(t.Y+wy),Z=(float)(t.Z+wz),
                NormalX=(float)wnx,NormalY=(float)wny,NormalZ=(float)wnz,Slot=slot,StableId=t.StableId,Generation=t.Generation,
                SnapshotGeneration=q.SnapshotGeneration,Sequence=q.Sequence,Flags=t.Flags};return true;
        }
        public static bool Earlier(in MonsterResult64 candidate,in MonsterResult64 current)=>current.Kind!=Hit||candidate.Fraction<current.Fraction||
            candidate.Fraction==current.Fraction&&(candidate.StableId<current.StableId||candidate.StableId==current.StableId&&candidate.Slot<current.Slot);
        public static bool Matches(in MonsterResult64 hit,int slot,int stableId,uint generation,uint snapshot,uint sequence,bool targetable)=>
            targetable&&hit.Kind==Hit&&slot==hit.Slot&&stableId==hit.StableId&&generation!=0&&generation==hit.Generation&&snapshot!=0&&snapshot==hit.SnapshotGeneration&&sequence!=0&&sequence==hit.Sequence;
        public static bool TryConsume(ref uint acceptedSequence,in MonsterResult64 hit,int slot,int stableId,uint generation,uint snapshot,uint sequence,bool targetable)
        {
            if(hit.Sequence<=acceptedSequence||!Matches(hit,slot,stableId,generation,snapshot,sequence,targetable))return false;
            acceptedSequence=hit.Sequence;return true;
        }
        public static double Clamp(double x,double lo,double hi)=>x<lo?lo:x>hi?hi:x;
        static double Outside(double x,double extent)=>x<-extent?x+extent:x>extent?x-extent:0;
        static bool LocalSegment(double x,double y,double z,double dx,double dy,double dz)=>
            Finite(x)&&Finite(y)&&Finite(z)&&Finite(dx)&&Finite(dy)&&Finite(dz)&&
            Math.Abs(x)<=65536&&Math.Abs(y)<=65536&&Math.Abs(z)<=65536&&
            Math.Abs(x+dx)<=65536&&Math.Abs(y+dy)<=65536&&Math.Abs(z+dz)<=65536;
        public static bool SweepCapsule(double x,double y,double z,double dx,double dy,double dz,double half,double radius,out double fraction)
        {
            fraction=0;if(!LocalSegment(x,y,z,dx,dy,dz)||!Finite(half)||half<0||half>MaximumExtent||!Finite(radius)||radius<0||radius>MaximumExtent+MaximumRadius)return false;
            double cy=Clamp(y,-half,half),oy=y-cy;
            if(x*x+oy*oy+z*z<=radius*radius)return true;
            double best=double.PositiveInfinity;
            if(Root(dx*dx+dz*dz,x*dx+z*dz,x*x+z*z-radius*radius,0,1,out double t)&&Math.Abs(y+dy*t)<=half)best=t;
            if(Sphere(x,y-half,z,dx,dy,dz,radius,out t)&&t<best)best=t;
            if(Sphere(x,y+half,z,dx,dy,dz,radius,out t)&&t<best)best=t;
            if(!Finite(best))return false;fraction=best;return true;
        }
        public static bool Sphere(double x,double y,double z,double dx,double dy,double dz,double radius,out double fraction)
        {
            fraction=0;if(!LocalSegment(x,y,z,dx,dy,dz)||!Finite(radius)||radius<0||radius>MaximumExtent+MaximumRadius)return false;
            double c=x*x+y*y+z*z-radius*radius;if(c<=0)return true;
            return Root(dx*dx+dy*dy+dz*dz,x*dx+y*dy+z*dz,c,0,1,out fraction);
        }
        // a*u^2 +2*b*u+c. Stable q formulation, inclusive roots, no geometric skin expansion.
        public static bool Root(double a,double b,double c,double lo,double hi,out double root)
        {
            root=0;if(c<=0&&lo==0){root=0;return true;}if(a==0){if(b==0)return false;double linear=-c/(2*b);if(linear<lo||linear>hi)return false;root=linear;return true;}
            double discriminant=b*b-a*c;
            // Only arithmetic roundoff: relative32 ulps of the evaluated terms, not a world-distance epsilon.
            double roundoff=32*2.2204460492503131e-16*(Math.Abs(b*b)+Math.Abs(a*c));
            if(discriminant<0){if(discriminant < -roundoff)return false;discriminant=0;}
            double sqrt=Math.Sqrt(discriminant),q=-b-(b>=0?sqrt:-sqrt);
            double r0=q==0?-b/a:q/a,r1=q==0?r0:c/q;if(r0>r1){double swap=r0;r0=r1;r1=swap;}
            if(r0>=lo&&r0<=hi){root=r0;return true;}if(r1>=lo&&r1<=hi){root=r1;return true;}return false;
        }
        public static unsafe bool SweepBox(double x,double y,double z,double dx,double dy,double dz,double ex,double ey,double ez,double radius,out double fraction)
        {
            fraction=0;if(!LocalSegment(x,y,z,dx,dy,dz)||!Finite(ex)||!Finite(ey)||!Finite(ez)||ex<0||ey<0||ez<0||
                ex>MaximumExtent||ey>MaximumExtent||ez>MaximumExtent||!Finite(radius)||radius<0||radius>MaximumRadius)return false;
            double ox=Outside(x,ex),oy=Outside(y,ey),oz=Outside(z,ez);
            if(ox*ox+oy*oy+oz*oz<=radius*radius)return true;
            if(dx==0&&dy==0&&dz==0)return false;
            if(radius==0)
            {
                double lo=0,hi=1;
                if(!Slab(x,dx,ex,ref lo,ref hi)||!Slab(y,dy,ey,ref lo,ref hi)||!Slab(z,dz,ez,ref lo,ref hi))return false;
                fraction=lo;return true;
            }
            double* times=stackalloc double[8];int count=2;times[0]=0;times[1]=1;
            Breaks(x,dx,ex,times,ref count);Breaks(y,dy,ey,times,ref count);Breaks(z,dz,ez,times,ref count);
            for(int i=1;i<count;i++){double v=times[i];int j=i;while(j>0&&v<times[j-1]){times[j]=times[j-1];j--;}times[j]=v;}
            for(int i=0;i<count-1;i++)
            {
                double left=times[i],right=times[i+1];if(right==left)continue;
                double lx=x+dx*left,ly=y+dy*left,lz=z+dz*left;
                ox=Outside(lx,ex);oy=Outside(ly,ey);oz=Outside(lz,ez);
                if(ox*ox+oy*oy+oz*oz<=radius*radius){fraction=left;return true;}
                double midpoint=(left+right)*.5,a=0,b=0,c=-radius*radius;
                Term(lx,dx,x+dx*midpoint,ex,ref a,ref b,ref c);
                Term(ly,dy,y+dy*midpoint,ey,ref a,ref b,ref c);
                Term(lz,dz,z+dz*midpoint,ez,ref a,ref b,ref c);
                if(Root(a,b,c,0,right-left,out double u)){fraction=left+u;return true;}
            }
            ox=Outside(x+dx,ex);oy=Outside(y+dy,ey);oz=Outside(z+dz,ez);
            if(ox*ox+oy*oy+oz*oz<=radius*radius){fraction=1;return true;}return false;
        }
        static bool Slab(double p,double d,double e,ref double lo,ref double hi)
        {if(d==0)return p>=-e&&p<=e;double a=(-e-p)/d,b=(e-p)/d;if(a>b){double s=a;a=b;b=s;}lo=Math.Max(lo,a);hi=Math.Min(hi,b);return lo<=hi;}
        static unsafe void Breaks(double p,double d,double e,double* times,ref int count)
        {if(d==0)return;double a=(-e-p)/d,b=(e-p)/d;if(a>0&&a<1)times[count++]=a;if(b>0&&b<1)times[count++]=b;}
        static void Term(double atLeft,double d,double atMid,double e,ref double a,ref double b,ref double c)
        {if(atMid>=-e&&atMid<=e)return;double v=atLeft+(atMid<0?e:-e);a+=d*d;b+=v*d;c+=v*v;}
        public static bool Morton(int x,int y,int z,out ulong key)
        {
            key=0;const int bias=1048576;if(x < -bias||x>=bias||y < -bias||y>=bias||z < -bias||z>=bias)return false;
            uint a=(uint)(x+bias),b=(uint)(y+bias),c=(uint)(z+bias);
            for(int i=0;i<21;i++){key|=((ulong)((a>>i)&1))<<(i*3);key|=((ulong)((b>>i)&1))<<(i*3+1);key|=((ulong)((c>>i)&1))<<(i*3+2);}return true;
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: World metres/Y-up; oriented actor-local boxes or local-Y finite capsules, closed finite segment fractions.
// Array Formats: Blittable Raw80/Target64/Query80/Result64 records, capacity48; exact rounded-box kernel uses8 stack doubles.
// Dependencies/Pointers: Pure bounded scalar math, no engine objects/Physics/HP/allocations; jobs own candidate iteration and root owner validates lease/pool generation before existing damage dispatch.
