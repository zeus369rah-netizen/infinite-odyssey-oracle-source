using Unity.Mathematics;
using OdysseyOracle.Foundation.World;

namespace OdysseyOracle
{
    public static class LaneBuildPreviewRules020
    {
        public const float CellSize=LaneBuildMath015.MacroMetres,CanonicalStep=LaneBuildMath015.RootStep,SurfaceTolerance=.004f,DrawLift=.012f;
        // Existing terrain query indexes floor(root+.5): block centres are integers.
        public static readonly float2 CellOrigin=new float2(-128.5f,-128.5f);
        public static bool TryRoot(float3 surface,bool snap,out float3 root)
        {
            root=default;if(!LaneBuildMath015.Finite(surface))return false;
            float2 point=surface.xz;
            // A multi-block base aligns to block-edge intersections, not an obstructed road-edge block centre.
            if(snap)point=(math.floor(point/CellSize)+.5f)*CellSize;
            return LaneBuildMath015.TryCell(new float3(point.x,0,point.y),out _,out root);
        }
        public static bool TryDisplayCell(float3 root,out uint key)
        {
            key=0;if(!LaneBuildMath015.Canonical(root,out _))return false;
            int2 cell=(int2)math.floor(root.xz/CellSize+.5f)+128;
            if(math.any(cell<0)||math.any(cell>=LaneBuildMath015.TerrainSide))return false;
            key=LaneBuildMath015.Morton(cell.x,cell.y,8);return true;
        }
        public static bool Surface(float hitY,float normalY,float supportY)=>math.isfinite(hitY)&&math.isfinite(normalY)&&math.isfinite(supportY)&&normalY>.999f&&math.abs(hitY-supportY)<=SurfaceTolerance;
        public static bool TryFloorRay(float3 eye,float3 direction,float floor,float reach,out float3 point,out float distance)
        {
            point=default;distance=0;
            if(!math.all(math.isfinite(eye))||!math.all(math.isfinite(direction))||!math.isfinite(floor)||!math.isfinite(reach)||reach<=0||reach>25||direction.y>=-.0001f||math.abs(math.lengthsq(direction)-1)>.01f)return false;
            distance=(floor-eye.y)/direction.y;if(distance<0||distance>reach)return false;point=eye+direction*distance;return math.all(math.isfinite(point));
        }
        public static bool TakeWheel(ref int consumedFrame,int frame,float value,bool active,out int step)
        {
            step=0;if(!active||frame<0||frame==consumedFrame||!math.isfinite(value)||value==0)return false;
            consumedFrame=frame;step=value>0?1:-1;return true;
        }
        public static int Choice(int current,int step,int count)=>count<4||count>5||current<0||current>=count||step!=1&&step!=-1?current:(current+step+count)%count;
        public static bool Retain(bool playing,bool paused,bool altHeld,bool free,bool locked,bool awaiting,bool guard)
            =>playing||!paused&&altHeld&&(free||locked)||paused&&awaiting||!paused&&locked&&guard;
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing arena metre/Y-up support;1m blocks centred on integers/boundaries at half-integers. SnapOn aligns multi-block bases to those intersections; SnapOff retains exactY0/TryCell quarter rounding.
// Array Formats: Pure finite scalar/vector math and one caller-owned consumed-frame integer; no Object, native store, heap, event or frame owner.
// Dependencies/Pointers: World supplies actual registered support and occlusion; Builder still runs existing full-footprint/host predicates. SnapOff means fine canonical placement, not unrestricted host coordinates; this helper grants no build, token, context, UI or campaign authority.
