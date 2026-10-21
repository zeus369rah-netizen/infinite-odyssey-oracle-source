using System;
namespace OdysseyOracle
{
    public static class PortalLandingQueries020
    {
        public static bool TryFloor(float x,float z,float maximumY,out float height)
        {
            height=float.NegativeInfinity;
            if(!PortalLandingLayout020.TryMorton(x,z,out _)||!PortalLandingLayout020.Finite(maximumY))return false;
            for(int i=0;i<PortalLandingLayout020.SurfaceCount;i++)
            {
                PortalLandingLayout020.TrySurface(i,out var floor);
                var p=floor.Local(new LandingPoint020(x,0,z));
                if(p.X>=floor.MinX&&p.X<=floor.MaxX&&p.Z>=floor.MinZ&&p.Z<=floor.MaxZ&&floor.MaxY<=maximumY+.0001f&&floor.MaxY>height)height=floor.MaxY;
            }
            return !float.IsNegativeInfinity(height);
        }
        public static bool TryStand(in LandingAdmission020 context,LandingPoint020 feet,float radius,out float height)
        {
            height=0;
            if(!PortalLandingLayout020.Active(context)||!PortalLandingLayout020.Finite(feet)||!PortalLandingLayout020.Finite(radius)||radius<.05f||radius>.5f)return false;
            float low=float.PositiveInfinity,high=float.NegativeInfinity;
            for(int sample=0;sample<9;sample++)
            {
                float x=0,z=0;
                switch(sample){case 1:x=1;break;case 2:x=-1;break;case 3:z=1;break;case 4:z=-1;break;case 5:x=z=.7071068f;break;case 6:x=-.7071068f;z=.7071068f;break;case 7:x=.7071068f;z=-.7071068f;break;case 8:x=z=-.7071068f;break;}
                if(!TryFloor(feet.X+x*radius,feet.Z+z*radius,feet.Y+PortalLandingLayout020.MaximumStep,out float y)||feet.Y-y>PortalLandingLayout020.MaximumDrop)return false;
                low=Math.Min(low,y);high=Math.Max(high,y);
            }
            if(high-low>PortalLandingLayout020.MaximumStep+.001f)return false;
            for(int i=0;i<PortalLandingLayout020.ObstacleCount;i++)
            {
                PortalLandingLayout020.TryObstacle(i,out var box);
                var p=box.Local(feet);
                if(p.X+radius>box.MinX&&p.X-radius<box.MaxX&&p.Z+radius>box.MinZ&&p.Z-radius<box.MaxZ&&high+PortalLandingLayout020.BodyHeight>box.MinY&&high<box.MaxY)return false;
            }
            height=high;return true;
        }
        public static bool TryMove(in LandingAdmission020 context,LandingPoint020 from,LandingPoint020 to)
        {
            if(!TryStand(context,from,PortalLandingLayout020.BodyRadius,out float a)||!TryStand(context,to,PortalLandingLayout020.BodyRadius,out float b)||Math.Abs(a-b)>PortalLandingLayout020.MaximumStep+.001f)return false;
            float dx=to.X-from.X,dz=to.Z-from.Z;if(dx*dx+dz*dz>16f)return false;
            return ClearSegment(from,to,PortalLandingLayout020.BodyRadius,PortalLandingLayout020.BodyHeight,0);
        }
        public static bool EndpointReach(in LandingAdmission020 context,int endpoint,LandingPoint020 feet,LandingPoint020 aim)
        {
            if(!PortalLandingLayout020.TryEndpoint(endpoint,out var target)||!PortalLandingLayout020.Finite(aim)||!TryStand(context,feet,PortalLandingLayout020.BodyRadius,out float floor)||Math.Abs(floor)>.001f)return false;
            float x=feet.X-target.Request.X,z=feet.Z-target.Request.Z;
            if(x*x+z*z>2.2f*2.2f||Math.Abs(x*.309017f+z*.9510565f)>1.8f)return false;
            float ax=target.Target.X-feet.X,ay=target.Target.Y-(feet.Y+1.65f),az=target.Target.Z-feet.Z;
            float distance=ax*ax+ay*ay+az*az,length=aim.X*aim.X+aim.Y*aim.Y+aim.Z*aim.Z;
            if(distance<=.01f||distance>4.6f*4.6f||length<.9f||length>1.1f||(ax*aim.X+ay*aim.Y+az*aim.Z)/Math.Sqrt(distance*length)<.75d)return false;
            return ClearSegment(new LandingPoint020(feet.X,feet.Y+1.65f,feet.Z),target.Target,0,0,0);
        }
        public static bool ClearSegment(LandingPoint020 from,LandingPoint020 to,float radius,float height,int openedEndpoint)
        {
            if(!PortalLandingLayout020.Finite(from)||!PortalLandingLayout020.Finite(to)||!PortalLandingLayout020.Finite(radius)||!PortalLandingLayout020.Finite(height)||radius<0||radius>.5f||height<0||height>PortalLandingLayout020.BodyHeight||openedEndpoint<0||openedEndpoint>2)return false;
            for(int i=0;i<PortalLandingLayout020.ObstacleCount;i++)
            {
                PortalLandingLayout020.TryObstacle(i,out var box);
                if(box.Kind==3&&box.Endpoint==openedEndpoint)continue;
                var localFrom=box.Local(from);var localTo=box.Local(to);
                float enter=0,exit=1;
                if(Slab(localFrom.X,localTo.X-localFrom.X,box.MinX-radius,box.MaxX+radius,ref enter,ref exit)&&
                   Slab(localFrom.Y,localTo.Y-localFrom.Y,box.MinY-height,box.MaxY,ref enter,ref exit)&&
                   Slab(localFrom.Z,localTo.Z-localFrom.Z,box.MinZ-radius,box.MaxZ+radius,ref enter,ref exit))return false;
            }
            return true;
        }
        static bool Slab(float origin,float delta,float minimum,float maximum,ref float enter,ref float exit)
        {
            if(Math.Abs(delta)<1e-8f)return origin>=minimum&&origin<=maximum;
            float a=(minimum-origin)/delta,b=(maximum-origin)/delta;if(a>b){float swap=a;a=b;b=swap;}
            enter=Math.Max(enter,a);exit=Math.Min(exit,b);return enter<=exit;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Explicit admitted third-mode metre/Y-up feet, conservative.325m footprint and1.85m standing body; existing.30m step limit.
// Array Formats: Fixed Morton-ordered30 floor and27 obstacle value records, nine footprint samples and bounded slab intersections; no Object, heap, frame container or native ownership.
// Dependencies/Pointers: Pure numeric passes are intended for the borrowed-array job wrapper. ClearSegment opening input must come only from the matching committed travel owner; alpha has no collision meaning. Default TryMove keeps all gates closed; root owns actual collider/input acceptance.
