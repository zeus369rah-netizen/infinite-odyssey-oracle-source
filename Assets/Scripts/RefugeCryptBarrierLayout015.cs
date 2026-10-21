using System;
using System.Runtime.InteropServices;

namespace OdysseyOracle
{
    [StructLayout(LayoutKind.Sequential,Pack=4)]
    public struct CryptBarrierPose32 {public float X,Y,Z,Yaw,SizeX,SizeY,SizeZ;public int Status;}
    [StructLayout(LayoutKind.Sequential,Pack=4)]
    public struct CryptBarrierSolid32 {public float MinX,MinY,MinZ;public uint Flags;public float MaxX,MaxY,MaxZ;public uint Reserved;}
    public interface ICryptBarrierPlacement015 {int Count{get;}CryptBarrierSolid32 Solid(int index);}
    public static class RefugeCryptBarrierLayout015
    {
        public const int MaximumSolids=4128;
        public const uint ActualContinuousFloor=1;
        public static int Evaluate<T>(ref T data,float minX,float minZ,float maxX,float maxZ,out CryptBarrierPose32 pose)where T:struct,ICryptBarrierPlacement015
        {
            pose=new CryptBarrierPose32{Status=1};
            if(!Finite(minX)||!Finite(minZ)||!Finite(maxX)||!Finite(maxZ)||minX<7.5f||minX>9f||Math.Abs(maxX-minX-4.5f)>.0001f||Math.Abs(minZ+5.6f)>.0001f||Math.Abs(maxZ+.4f)>.0001f||data.Count<0||data.Count>MaximumSolids)return 1;
            float x=(minX+maxX)*.5f-1f,z=minZ+.35f;
            pose=new CryptBarrierPose32{X=x,Y=0,Z=z,Yaw=0,SizeX=2,SizeY=1.5f,SizeZ=.5f};
            var bounds=new CryptBarrierSolid32{MinX=x,MinY=0,MinZ=z,MaxX=x+2,MaxY=1.5f,MaxZ=z+.5f};
            int failure=0;bool floor=false;
            if(x<minX+.35f||x+2>maxX-.35f||z<minZ+.35f-.0001f||z+.5f>maxZ-.35f)failure|=2;
            // Reservation consumes the canonical furnace's dimensions/scale; it does not spawn/move that owner.
            float scale=SmithFurnaceLayout015.Scale,cx=(minX+maxX)*.5f,cz=(minZ+maxZ)*.5f;
            var furnace=new CryptBarrierSolid32{MinX=cx-SmithFurnaceMath.HalfDepth*scale-.2f,MinY=0,MinZ=cz-SmithFurnaceMath.HalfWidth*scale-.2f,MaxX=cx+SmithFurnaceMath.HalfDepth*scale+.2f,MaxY=SmithFurnaceLayout015.DesiredHeight,MaxZ=cz+SmithFurnaceMath.HalfWidth*scale+.2f};
            if(Overlap(bounds,furnace))failure|=4;
            // Existing Smith/anvil/tray plus accepted approach and main compass corridor reservations.
            if(Overlap(bounds,new CryptBarrierSolid32{MinX=6.15f,MinY=0,MinZ=-4.05f,MaxX=8.5f,MaxY=2.2f,MaxZ=-1.85f})||
               Overlap(bounds,new CryptBarrierSolid32{MinX=-2.75f,MinY=0,MinZ=-2.95f,MaxX=7.85f,MaxY=2.2f,MaxZ=-.65f})||
               Overlap(bounds,new CryptBarrierSolid32{MinX=-2.75f,MinY=0,MinZ=-14,MaxX=-1.25f,MaxY=2.2f,MaxZ=28})||
               Overlap(bounds,new CryptBarrierSolid32{MinX=minX+1.3f-.375f,MinY=0,MinZ=cz-1.35f-.33f,MaxX=minX+1.3f+.375f,MaxY=.65f,MaxZ=cz-1.35f+.33f}))failure|=8;
            if(bounds.MaxX>maxX-.85f)failure|=16; // Keep clear of deep east-window reveal band, including margin.
            for(int i=0;i<data.Count;i++){
                var solid=data.Solid(i);if(!Valid(solid)){failure|=32;continue;}
                if((solid.Flags&ActualContinuousFloor)!=0&&Math.Abs(solid.MaxY)<.0001f&&solid.MinY<0&&solid.MinX<=x+.0001f&&solid.MaxX>=x+2-.0001f&&solid.MinZ<=z+.0001f&&solid.MaxZ>=z+.5f-.0001f)floor=true;
                if(Overlap(bounds,solid))failure|=64;
            }
            if(!floor)failure|=128;pose.Status=failure;return failure;
        }
        public static bool Overlap(CryptBarrierSolid32 a,CryptBarrierSolid32 b)=>Math.Min(a.MaxX,b.MaxX)-Math.Max(a.MinX,b.MinX)>.0001f&&Math.Min(a.MaxY,b.MaxY)-Math.Max(a.MinY,b.MinY)>.0001f&&Math.Min(a.MaxZ,b.MaxZ)-Math.Max(a.MinZ,b.MinZ)>.0001f;
        static bool Valid(CryptBarrierSolid32 b)=>Finite(b.MinX)&&Finite(b.MinY)&&Finite(b.MinZ)&&Finite(b.MaxX)&&Finite(b.MaxY)&&Finite(b.MaxZ)&&b.MinX<=b.MaxX&&b.MinY<=b.MaxY&&b.MinZ<=b.MaxZ&&(b.Flags&~ActualContinuousFloor)==0&&b.Reserved==0;
        static bool Finite(float f)=>!float.IsNaN(f)&&!float.IsInfinity(f);
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Actual first-east-alcove World/Y-up metres; original2x1.5x.5m panel bottomY0, new yaw0 south-edge reservation.
// Array Formats: Pose32/Solid32, bounded4128solid interface; numeric floor/solid/furnace/route checks only, no allocation or Object API.
// Dependencies/Pointers: Canonical SmithFurnaceMath/SmithFurnaceLayout015 dimensions and authored room envelope; native adapter supplies actual solids and floor then publishes only after completed certification.
