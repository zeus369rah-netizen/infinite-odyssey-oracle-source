using System;
using System.Collections.Generic;
namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // Cold pure geometry envelope from the frozen original recipe vertices.
    // One radial/Y band per authored piece includes arbitrary head/core/rotor yaw,
    // full accepted recoil/pitch and maximum core pulse. Hot queries read scalars.
    // Navigation owner decides movement/lease/range; this class changes no gameplay.
    public sealed class TowerModelNavigationProfile
    {
        public readonly struct Band
        {
            public readonly double MinimumY,MaximumY,Radius;
            public Band(double min,double max,double radius){MinimumY=min;MaximumY=max;Radius=radius;}
        }
        readonly Band[] bands;
        public int Count=>bands.Length;
        public Band GetBand(int index)=>bands[index];
        public TowerModelNavigationProfile(TowerModelRecipe recipe)
        {
            if(recipe==null)throw new ArgumentNullException(nameof(recipe));
            bands=new Band[recipe.Count];
            for(int i=0;i<bands.Length;i++)bands[i]=BuildBand(recipe,recipe.Piece(i));
        }
        public double RadiusBetween(double minimumLocalY,double maximumLocalY)
        {
            if(!Finite(minimumLocalY)||!Finite(maximumLocalY)||minimumLocalY>maximumLocalY)return 1d;
            double radius=0;
            for(int i=0;i<bands.Length;i++)if(bands[i].MaximumY>=minimumLocalY&&bands[i].MinimumY<=maximumLocalY)
                radius=Math.Max(radius,bands[i].Radius);
            return radius;
        }
        static Band BuildBand(TowerModelRecipe recipe,TowerModelPiece piece)
        {
            double min=double.PositiveInfinity,max=double.NegativeInfinity,radius=0;
            double amplitude=recipe.Type==2?.12:recipe.Type==0?.085:recipe.Type==1?.04:.025;
            foreach(TowerModelVector local in Vertices(piece))
            {
                TowerModelVector p=Rotate(local,piece.Euler);
                double x=p.X+piece.Center.X,y=p.Y+piece.Center.Y,z=p.Z+piece.Center.Z;
                double ymin=y,ymax=y,zmin=z,zmax=z,radial=Math.Sqrt(x*x+z*z);
                if(piece.Group==TowerModelGroup.Recoil)
                {
                    double pitch=-(recipe.Type==2?1.8:.8)*Math.PI/180d;
                    double relativeY=y-recipe.HeadPivot.Y;
                    TrigBounds(relativeY,-z,pitch,0,out ymin,out ymax);ymin+=recipe.HeadPivot.Y;ymax+=recipe.HeadPivot.Y;
                    TrigBounds(z,relativeY,pitch,0,out zmin,out zmax);zmin-=amplitude;
                    double az=Math.Max(Math.Abs(zmin),Math.Abs(zmax));radial=Math.Sqrt(x*x+az*az);
                }
                else if(piece.Group==TowerModelGroup.Core)
                {
                    double relativeY=y-recipe.HeadPivot.Y;
                    ymin=recipe.HeadPivot.Y+Math.Min(relativeY,relativeY*1.068)-.018;
                    ymax=recipe.HeadPivot.Y+Math.Max(relativeY,relativeY*1.068)+.018;
                    radial*=1.068;
                }
                else if(piece.Group==TowerModelGroup.Bolt)
                {
                    zmin=z-amplitude*1.35;
                    double az=Math.Max(Math.Abs(zmin),Math.Abs(z));radial=Math.Sqrt(x*x+az*az);
                }
                min=Math.Min(min,ymin);max=Math.Max(max,ymax);radius=Math.Max(radius,radial);
            }
            // Small arithmetic tolerance, not an extra gameplay clearance constant.
            return new Band(min-.00001,max+.00001,radius+.00001);
        }
        public static TowerModelVector Rotate(TowerModelVector v,TowerModelVector euler)
        {
            double x=v.X,y=v.Y,z=v.Z,rx=euler.X*Math.PI/180,ry=euler.Y*Math.PI/180,rz=euler.Z*Math.PI/180;
            double a=x*Math.Cos(rz)-y*Math.Sin(rz),b=x*Math.Sin(rz)+y*Math.Cos(rz);x=a;y=b;
            a=y*Math.Cos(rx)-z*Math.Sin(rx);b=y*Math.Sin(rx)+z*Math.Cos(rx);y=a;z=b;
            return new TowerModelVector(x*Math.Cos(ry)+z*Math.Sin(ry),y,-x*Math.Sin(ry)+z*Math.Cos(ry));
        }
        static void TrigBounds(double a,double b,double start,double end,out double min,out double max)
        {
            double first=a*Math.Cos(start)+b*Math.Sin(start),last=a*Math.Cos(end)+b*Math.Sin(end);
            min=Math.Min(first,last);max=Math.Max(first,last);double critical=Math.Atan2(b,a);
            for(int k=-2;k<=2;k++)
            {
                double angle=critical+k*Math.PI;
                if(angle<start||angle>end)continue;
                double value=a*Math.Cos(angle)+b*Math.Sin(angle);min=Math.Min(min,value);max=Math.Max(max,value);
            }
        }
        public static TowerModelVector[] Vertices(TowerModelPiece piece)
        {
            double x=piece.Size.X*.5,y=piece.Size.Y*.5,z=piece.Size.Z*.5;
            var vertices=new List<TowerModelVector>(18);
            if(piece.Primitive==TowerModelPrimitive.Box)
                for(int i=0;i<8;i++)vertices.Add(new TowerModelVector((i&1)==0?-x:x,(i&2)==0?-y:y,(i&4)==0?-z:z));
            else
            {
                for(int i=0;i<8;i++)
                {
                    double angle=(i*45+22.5)*Math.PI/180d,px=Math.Cos(angle)*x,pz=Math.Sin(angle)*z;
                    if(piece.Primitive==TowerModelPrimitive.Crystal)vertices.Add(new TowerModelVector(px,0,pz));
                    else{vertices.Add(new TowerModelVector(px,-y,pz));vertices.Add(new TowerModelVector(px*piece.Taper,y,pz*piece.Taper));}
                }
                vertices.Add(new TowerModelVector(0,-y,0));vertices.Add(new TowerModelVector(0,y,0));
            }
            return vertices.ToArray();
        }
        static bool Finite(double value)=>!double.IsNaN(value)&&!double.IsInfinity(value);
    }
}
