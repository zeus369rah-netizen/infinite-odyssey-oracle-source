using System;

namespace OdysseyOracle
{
    public static class SmithFurnacePlacement
    {
        // Flags: pose1, floor/wall2, pier4, Smith/body8, anvil16, coaltray32, approach64, saved prop128.
        public static int Check(TutorialRoomV2Layout layout,float x,float y,float z,float yaw,bool unitScale)
        {
            if(layout==null||!unitScale||!Finite(x)||!Finite(y)||!Finite(z)||!Finite(yaw)||Math.Abs(x-6.8f)>.0001f||Math.Abs(y)>.0001f||Math.Abs(z+3.6f)>.0001f||Math.Abs(yaw)>.01f)return 1;
            float a=x-.56f,b=x+.56f,c=z-1.67f,d=z-.83f;int failure=0;var hall=layout.Rooms[0];
            if(a<hall.MinU+.1f||b>hall.MaxU-.1f||c<hall.MinV+.1f||d>hall.MaxV-.1f||layout.CeilingY<1.66f)failure|=2;
            foreach(float row in layout.PierRows)foreach(int side in new[]{-1,1})
                if(!Separate(a,b,c,d,side*layout.PierX-.7f,side*layout.PierX+.7f,row-.725f,row+.725f,.1f))failure|=4;
            if(!Separate(a,b,c,d,x-.65f,x+.95f,z-.4f,z+.6f,.2f))failure|=8;
            if(!Separate(a,b,c,d,x+.7f,x+1.72f,z-.2f,z+.45f,.2f))failure|=16;
            if(!Separate(a,b,c,d,x+.6f,x+1.53f,z+.75f,z+1.6f,.2f))failure|=32;
            if(!Separate(a,b,c,d,x-.75f,x+.75f,z+.9f,z+2.8f,.3f)||!Separate(a,b,c,d,-2.55f,-1.45f,-14,28,.3f))failure|=64;
            foreach(var placement in layout.PropPlacements)
            {
                var e=RefugePlacementPolicy.At(placement);if(e.Min.Y>=SmithFurnaceMath.Height)continue;
                if(!Separate(a,b,c,d,e.Min.X,e.Max.X,e.Min.Z,e.Max.Z,.1f))failure|=128;
            }
            return failure;
        }
        public static bool Separate(float ax0,float ax1,float az0,float az1,float bx0,float bx1,float bz0,float bz1,float margin)
            =>ax1+margin<=bx0||bx1+margin<=ax0||az1+margin<=bz0||bz1+margin<=az0;
        static bool Finite(float x)=>!float.IsNaN(x)&&!float.IsInfinity(x);
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Actual fixed Smith world pose, full furnace floor footprint and real RoomV2 hall/pier/saved-prop bounds; +Yup/+Zforward.
// Array Formats: Borrowed immutable cold layout arrays and scalar clearance checks; no native/scene Object ownership or active-frame calls.
// Dependencies/Pointers:20mm recipe envelope and existing saved-prop policy; reject changed pose/support instead of moving actors. Original talk/navigation paths retain explicit margins.
