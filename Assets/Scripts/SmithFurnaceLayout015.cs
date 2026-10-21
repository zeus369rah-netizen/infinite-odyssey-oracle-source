using System;
using System.Runtime.InteropServices;

namespace OdysseyOracle
{
    [StructLayout(LayoutKind.Sequential,Pack=4)]
    public struct SmithFurnacePose32 { public float X,Y,Z,Yaw,Scale,HalfX,HalfZ,Height; }
    [StructLayout(LayoutKind.Sequential,Pack=4)]
    public struct SmithFurnaceRect16 { public float MinX,MinZ,MaxX,MaxZ; }
    [StructLayout(LayoutKind.Sequential,Pack=4)]
    public struct SmithFurnaceBounds32 { public float MinX,MinY,MinZ,MaxX,MaxY,MaxZ; public uint Reserved0,Reserved1; }
    public interface ISmithFurnaceLayout015
    {
        int RoomCount{get;} int PropCount{get;} int PierCount{get;}
        SmithFurnaceRect16 Room(int index);SmithFurnaceBounds32 Prop(int index);float Pier(int index);
    }
    // Original finite workshop placement, executed by the production cold Burst job.
    public static class SmithFurnaceLayout015
    {
        public const float DesiredHeight=1.90f,Scale=DesiredHeight/SmithFurnaceMath.Height;
        public const int MaximumRooms=32,MaximumProps=32,MaximumPiers=16;
        // Called inside the native layout job. Cold scene adapters copy only
        // intrinsic local bounds and scalar placement frames, never rotate them.
        public static SmithFurnaceBounds32 Project(SmithFurnaceBounds32 local,float x,float y,float z,float yaw)
        {
            if(!Finite(x)||!Finite(y)||!Finite(z)||!Finite(yaw)||local.Reserved0>1)return new SmithFurnaceBounds32{MinX=float.NaN};
            double angle=yaw*Math.PI/180,c=Math.Cos(angle),s=Math.Sin(angle);
            if(local.Reserved0==1){x-=(float)(-.3535881*s);y-=.4175f;z-=(float)(-.3535881*c);}
            var result=new SmithFurnaceBounds32{MinX=float.PositiveInfinity,MinY=float.PositiveInfinity,MinZ=float.PositiveInfinity,MaxX=float.NegativeInfinity,MaxY=float.NegativeInfinity,MaxZ=float.NegativeInfinity};
            for(int i=0;i<8;i++){
                float lx=(i&1)==0?local.MinX:local.MaxX,ly=(i&2)==0?local.MinY:local.MaxY,lz=(i&4)==0?local.MinZ:local.MaxZ;
                float wx=(float)(lx*c+lz*s)+x,wy=ly+y,wz=(float)(-lx*s+lz*c)+z;
                result.MinX=Math.Min(result.MinX,wx);result.MaxX=Math.Max(result.MaxX,wx);result.MinY=Math.Min(result.MinY,wy);result.MaxY=Math.Max(result.MaxY,wy);result.MinZ=Math.Min(result.MinZ,wz);result.MaxZ=Math.Max(result.MaxZ,wz);
            }
            return result;
        }
        public static int Evaluate<T>(ref T data,float x,float y,float z,float yaw,bool unitScale,float halfWidth,float ceiling,out SmithFurnacePose32 pose) where T:struct,ISmithFurnaceLayout015
        {
            pose=default;
            if(!unitScale||!Finite(x)||!Finite(y)||!Finite(z)||!Finite(yaw)||Math.Abs(x-6.8f)>.0001f||Math.Abs(y)>.0001f||Math.Abs(z+3.6f)>.0001f||Math.Abs(yaw)>.01f)return 1;
            if(!Finite(halfWidth)||halfWidth<7.5f||halfWidth>9f||!Finite(ceiling)||ceiling<DesiredHeight+.1f||data.RoomCount<1||data.RoomCount>MaximumRooms||data.PropCount<0||data.PropCount>MaximumProps||data.PierCount<0||data.PierCount>MaximumPiers)return 2;
            int found=0;SmithFurnaceRect16 room=default;
            for(int i=0;i<data.RoomCount;i++){
                var r=data.Room(i);if(!Finite(r.MinX)||!Finite(r.MinZ)||!Finite(r.MaxX)||!Finite(r.MaxZ)||r.MinX>=r.MaxX||r.MinZ>=r.MaxZ)return 2;
                if(r.MinX>=halfWidth-.0001f&&z>r.MinZ&&z<r.MaxZ){found++;room=r;}
            }
            if(found!=1)return 2;
            pose=new SmithFurnacePose32{X=(room.MinX+room.MaxX)*.5f,Y=0,Z=(room.MinZ+room.MaxZ)*.5f,Yaw=-90,Scale=Scale,HalfX=SmithFurnaceMath.HalfDepth*Scale,HalfZ=SmithFurnaceMath.HalfWidth*Scale,Height=DesiredHeight};
            float a=pose.X-pose.HalfX,b=pose.X+pose.HalfX,c=pose.Z-pose.HalfZ,d=pose.Z+pose.HalfZ;
            int failure=0;
            if(a<room.MinX+.1f||b>room.MaxX-.1f||c<room.MinZ+.1f||d>room.MaxZ-.1f)failure|=2;
            for(int i=0;i<data.PierCount;i++){
                float row=data.Pier(i);if(!Finite(row))return 2;
                for(int side=-1;side<=1;side+=2)if(!Separate(a,b,c,d,side*(halfWidth-1.05f)-.7f,side*(halfWidth-1.05f)+.7f,row-.725f,row+.725f,.1f))failure|=4;
            }
            if(!Separate(a,b,c,d,x-.65f,x+.95f,z-.4f,z+.6f,.2f))failure|=8;
            if(!Separate(a,b,c,d,x+.7f,x+1.72f,z-.2f,z+.45f,.2f))failure|=16;
            if(!Separate(a,b,c,d,x+.6f,x+1.53f,z+.75f,z+1.6f,.2f))failure|=32;
            if(!Separate(a,b,c,d,x-.75f,x+.75f,z+.9f,z+2.8f,.3f)||!Separate(a,b,c,d,-2.55f,-1.45f,-14,28,.3f))failure|=64;
            // Actual eastern window reveal reaches .75m inward; the whole band
            // is conservative above the physical sill, not an aperture exemption.
            if(DesiredHeight>TutorialRoomV2Layout.WindowBottom&&!Separate(a,b,c,d,room.MaxX-.75f,room.MaxX+.45f,room.MinZ,room.MaxZ,.1f))failure|=256;
            // Existing element-lore book/desk reservation, including the rotated book.
            if(!Separate(a,b,c,d,8.125f,9.175f,-1.975f,-1.125f,.1f))failure|=512;
            for(int i=0;i<data.PropCount;i++){
                var p=data.Prop(i);if(!Finite(p.MinX)||!Finite(p.MinY)||!Finite(p.MinZ)||!Finite(p.MaxX)||!Finite(p.MaxY)||!Finite(p.MaxZ)||p.MinX>p.MaxX||p.MinY>p.MaxY||p.MinZ>p.MaxZ)return 2;
                if(p.MaxY>0&&p.MinY<DesiredHeight&&!Separate(a,b,c,d,p.MinX,p.MaxX,p.MinZ,p.MaxZ,.1f))failure|=128;
            }
            return failure;
        }
        public static bool Separate(float a,float b,float c,float d,float e,float f,float g,float h,float margin)=>b+margin<=e||f+margin<=a||d+margin<=g||h+margin<=c;
        static bool Finite(float x)=>!float.IsNaN(x)&&!float.IsInfinity(x);
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Actual stationary refuge metre rectangles and Smith pose; bottom-centred+Y, mouth+Z rotates-90toward world-X.
// Array Formats:32Bpose,16Brect,32Blocal prop+16Bframe ->32Bprojected bounds; bounded32rooms/32props/16pier rows through value-only generic storage.
// Dependencies/Pointers: Original furnace geometry dimensions and exact RoomV2 metadata; production Burst adapter owns native snapshots/fences, no Object/API/allocation inside numeric layout.
