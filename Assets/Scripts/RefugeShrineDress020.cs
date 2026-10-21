using System;

namespace OdysseyOracle
{
    public enum RefugeShrineDressPaint020 { Dark, Pale, Gold, Glow }
    public interface IRefugeShrineDressSink020
    {
        void Box(RefugeShrineDressPaint020 paint,float x,float y,float z,float width,float height,float depth,float yaw);
        void Crystal(RefugeShrineDressPaint020 paint,float x,float y,float z,float radius,float height,float yaw);
    }
    // Cold intrinsic presentation recipe. No Unity Object, authority or collider API.
    public static class RefugeShrineDress020
    {
        public const int GroupCount=16,BoxCount=36,CrystalCount=4,TriangleCount=528;
        public const float MaximumRadius=1.40f,OpenCenterRadius=.90f;
        public static void Build<T>(ref T output) where T:struct,IRefugeShrineDressSink020
        {
            for(int index=0;index<8;index++)
            {
                float yaw=22.5f+index*45f;
                double angle=yaw*Math.PI/180.0;
                float x=(float)Math.Sin(angle),z=(float)Math.Cos(angle);
                output.Box((index&1)==0?RefugeShrineDressPaint020.Pale:RefugeShrineDressPaint020.Dark,
                    x*1.29f,.34f,z*1.29f,.58f,.18f,.07f,yaw);
                output.Box(RefugeShrineDressPaint020.Gold,x*1.328f,.34f,z*1.328f,.025f,.08f,.014f,yaw);
            }
            for(int index=0;index<4;index++)
            {
                float yaw=45f+index*90f;
                double angle=yaw*Math.PI/180.0;
                float x=(float)Math.Sin(angle),z=(float)Math.Cos(angle),radius=1.025807f;
                output.Box(RefugeShrineDressPaint020.Pale,x*radius,1.01f,z*radius,.18f,.34f,.24f,yaw);
                output.Box(RefugeShrineDressPaint020.Gold,x*(radius+.022f),1.18f,z*(radius+.022f),.16f,.06f,.28f,yaw);
                for(int mark=0;mark<3;mark++)
                    output.Box(RefugeShrineDressPaint020.Glow,x*(radius+.126f),.96f+mark*.05f,z*(radius+.126f),
                        mark==1?.07f:.03f,.035f,.014f,yaw);
                output.Crystal(RefugeShrineDressPaint020.Glow,x*(radius+.10f),1.26f,z*(radius+.10f),.035f,.16f,yaw);
            }
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: unchanged ward-local Y-up metres, yaw degrees; open centre radius.90 and existing outer radius1.40.
// Array Formats: exactly16 decorative groups,36 boxes and4 six-sided crystals through a generic cold struct sink;528 added triangles into existing four material batches.
// Dependencies/Pointers: pure deterministic intrinsic recipe only; existing OracleShrine owns main-thread mesh/material lifetime. No collider, Object, light, Update, gameplay, profile or transport calls.
