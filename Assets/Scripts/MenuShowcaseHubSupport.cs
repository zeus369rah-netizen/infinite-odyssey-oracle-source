using System;
namespace OdysseyOracle
{
    // Cold authoring math only. Actual floor triangles replace guesses/Physics.
    public static class MenuShowcaseHubSupport
    {
        public static bool TryHeight(float x,float z,float ax,float ay,float az,float bx,float by,float bz,float cx,float cy,float cz,out float y)
        {
            y=0;
            if(!Finite(x)||!Finite(z)||!Finite(ax)||!Finite(ay)||!Finite(az)||!Finite(bx)||!Finite(by)||!Finite(bz)||!Finite(cx)||!Finite(cy)||!Finite(cz))return false;
            double ux=(double)bx-ax,uz=(double)bz-az,vx=(double)cx-ax,vz=(double)cz-az;
            double det=ux*vz-uz*vx;
            // Either winding is valid; vertical faces/near-zero projected areas cannot support a base.
            if(Math.Abs(det)<1e-8)return false;
            double dx=(double)x-ax,dz=(double)z-az,u=(dx*vz-dz*vx)/det,v=(ux*dz-uz*dx)/det;
            if(u<-.000001||v<-.000001||u+v>1.000001)return false;
            double value=ay+u*((double)by-ay)+v*((double)cy-ay);
            if(double.IsNaN(value)||double.IsInfinity(value)||value>float.MaxValue||value< -float.MaxValue)return false;
            y=(float)value;return true;
        }
        public static float Fade(float age)
        {
            if(!Finite(age)||age<=0||age>=.96f)return 0;
            float a=Clamp(age/.12f),b=Clamp((age-.62f)/.34f);
            return a*a*(3-2*a)*(1-b*b*(3-2*b));
        }
        static float Clamp(float v)=>v<0?0:v>1?1:v;
        static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: captured arena-local metre/Y-up triangles; exact vertical barycentric projection has no Physics or scene state.
// Array Formats: scalar double intermediate support query and finite float result; private-clock normalized lifetime alpha/size envelope.
// Dependencies/Pointers: cold baker queries only actual captured floor support; pure CPU validation does not establish native clearance, shader or visual acceptance.
