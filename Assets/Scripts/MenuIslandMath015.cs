// # ARCHITECTURE SNAPSHOT
// Coordinate Space: catalog-local metres/Y-up, normalized safe viewport, physical viewport pixels.
// Array Formats: numeric scalars/int2/float3 and one persistent focus/frame, owned by the showcase lease.
// Dependencies/Pointers: no Object APIs; existing full-map fit remains the strict wide-frame contract.
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace OdysseyOracle
{
    public static class MenuIslandMath015
    {
        public const int Side=32,Depth=12,CellCount=Side*Depth*Side,MaximumPixels=1920*1080;
        public const float ZoomSeconds=48f,MaximumZoom=.94f,MaximumAimOffset=128f,FocusFrequency=3f;
        public static int2 TargetSize(int quality,int pixelWidth,int pixelHeight,float aspect)
        {
            if(quality<1||quality>2||pixelWidth<1||pixelHeight<1||!math.isfinite(aspect)||aspect<.5f||aspect>4)return int2.zero;
            int limitX=quality==2?1920:640,limitY=quality==2?1080:360;
            int width=math.max(1,math.min(math.min(limitX,pixelWidth),(int)math.floor(math.min(limitY,pixelHeight)*aspect)));
            int height=math.max(1,math.min(math.min(limitY,pixelHeight),(int)math.floor(width/aspect)));
            return new int2(width,height);
        }
        public static uint Mix(uint value)
        {
            value^=value>>16;value*=0x7feb352du;value^=value>>15;value*=0x846ca68bu;return value^(value>>16);
        }
        public static float Unit(uint value)=>(Mix(value)&0x00ffffffu)*(1f/16777216f);
        public static int3 DecodeMorton(uint code)
        {
            int3 point=int3.zero;
            for(int bit=0;bit<5;bit++){point.x|=(int)((code>>(bit*3))&1u)<<bit;point.y|=(int)((code>>(bit*3+1))&1u)<<bit;point.z|=(int)((code>>(bit*3+2))&1u)<<bit;}
            return point;
        }
        public static byte Cell(int3 point,uint seed)
        {
            if(math.any(point<0)||point.x>=Side||point.y>=Depth||point.z>=Side)return 0;
            float2 p=(new float2(point.x,point.z)+.5f-16f)/16f;
            float upper=(point.y+.5f)/Depth;
            // Broad squared silhouette, narrowing to a supported inverted rocky wedge.
            float outline=math.cmax(math.abs(p))+.10f*math.cmin(math.abs(p));
            float stepNoise=(Unit(seed+(uint)(point.x/3)*97u+(uint)(point.z/3)*311u)-.5f)*.045f;
            float radius=.20f+.86f*math.pow(upper,.64f)+stepNoise;
            if(outline>radius)return 0;
            return (byte)(1+((point.y/3+(int)(Mix(seed+(uint)(point.x/4)*17u+(uint)(point.z/4)*53u)&1u))%3));
        }
        public static float CloseWeight(float time)
        {
            float phase=math.frac(math.max(0,time)/ZoomSeconds)*ZoomSeconds;
            float enter=math.saturate((phase-6f)/14f),leave=math.saturate((phase-29f)/18f);
            return Ease(enter)*(1f-Ease(leave));
        }
        static float Ease(float value)=>value*value*value*(value*(value*6f-15f)+10f);
        // Compatibility overload; production retains velocity through the overload below.
        public static bool TryTour(float3 center,float3 extents,float aspect,float4 safe,float time,float dt,float3 first,float3 second,
            float3 previous,out MenuShowcaseFrame48 frame,out float3 focus)
            =>TryTour(center,extents,aspect,safe,time,dt,first,second,previous,float3.zero,out frame,out focus,out _);
        public static bool TryTour(float3 center,float3 extents,float aspect,float4 safe,float time,float dt,float3 first,float3 second,
            float3 previous,float3 velocity,out MenuShowcaseFrame48 frame,out float3 focus,out float3 nextVelocity)
        {
            focus=previous;nextVelocity=velocity;
            if(!MenuShowcaseCameraMath.TryFit(center,extents,aspect,safe,time,out frame))return false;
            if(!math.all(math.isfinite(first))||!math.all(math.isfinite(second))||!math.all(math.isfinite(previous))||!math.all(math.isfinite(velocity))||!math.isfinite(dt)||
                math.cmax(math.abs(first))>4096f||math.cmax(math.abs(second))>4096f||math.length(previous-center)>MaximumAimOffset+.01f||math.length(velocity)>512f)return false;
            float tour=math.frac(math.max(0,time)/MenuShowcaseCameraMath.TourSeconds);
            float3 target=tour<.5f?first:second,offset=target-center;
            float length=math.length(offset);if(length>MaximumAimOffset)target=center+offset*(MaximumAimOffset/length);
            float step=math.clamp(dt,0,.1f);
            if(step>0)
            {
                // Exact critically damped response to the current accepted presentation target.
                float decay=math.exp(-FocusFrequency*step);float3 difference=previous-target;
                float3 temporary=(velocity+FocusFrequency*difference)*step;
                focus=target+(difference+temporary)*decay;
                nextVelocity=(velocity-FocusFrequency*temporary)*decay;
            }
            // A fixed circumscribed sphere avoids angle-dependent AABB fit kinks.
            // The same complete-scene depth is retained even during the deliberate close crop.
            float radius=math.max(.1f,math.length(extents)),depth=radius+12f;
            float2 span=safe.zw-safe.xy,ndc=safe.xy+safe.zw-1f;
            float fit=math.min(span.y,aspect*span.x);
            float wide=radius/fit*1.01f,close=math.length(new float3(4,3,4))/fit*1.01f;
            close=math.min(wide,math.max(close,wide*(1f-MaximumZoom)));
            // Proportional size interpolation avoids a rapid final push when the wide frame is much larger.
            float weight=CloseWeight(time),size=math.exp(math.lerp(math.log(wide),math.log(close),weight));
            float3 right=math.rotate(frame.Rotation,new float3(1,0,0)),up=math.rotate(frame.Rotation,new float3(0,1,0)),forward=math.rotate(frame.Rotation,new float3(0,0,1));
            float3 delta=focus-center,aim=(right*math.dot(right,delta)+up*math.dot(up,delta))*weight;
            frame.Position=center-forward*depth-right*(ndc.x*size*aspect)-up*(ndc.y*size)+aim;
            frame.OrthographicSize=size;frame.Near=.1f;frame.Far=depth+radius+12f+900f;
            return math.all(math.isfinite(frame.Position))&&math.all(math.isfinite(focus))&&math.all(math.isfinite(nextVelocity))&&math.isfinite(size)&&size>0;
        }
    }
    [BurstCompile] public struct MenuIslandFillJob015:IJob
    {
        public NativeArray<byte> Cells;public uint Seed;
        public void Execute()
        {
            if(Cells.Length!=MenuIslandMath015.CellCount)return;
            for(uint code=0;code<32768;code++)
            {
                int3 p=MenuIslandMath015.DecodeMorton(code);if(p.y>=MenuIslandMath015.Depth)continue;
                // The existing greedy mesher compatibility store is deliberately row-major.
                Cells[p.x+32*(p.y+MenuIslandMath015.Depth*p.z)]=MenuIslandMath015.Cell(p,Seed);
            }
        }
    }
    [BurstCompile] public struct MenuIslandCameraJob015:IJob
    {
        public float3 Center,Extents,First,Second;public float Aspect,Time,Delta;public float4 Safe;
        public NativeArray<float3> Focus;
        [WriteOnly] public NativeArray<MenuShowcaseFrame48> Result;
        public void Execute()
        {
            if(Result.Length!=1||Focus.Length!=2)return;
            bool valid=MenuIslandMath015.TryTour(Center,Extents,Aspect,Safe,Time,Delta,First,Second,Focus[0],Focus[1],out var frame,out var focus,out var velocity);
            if(!valid)frame=default;else {Focus[0]=focus;Focus[1]=velocity;}Result[0]=frame;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: unchanged catalog+underside sphere -> private orthographic camera outside every scene support plane by12m;48s C2 zoom with16s actor close hold,96s existing orbit.
// Array Formats:12288 native byte cells and bounded Morton fill unchanged; critically damped focus+velocity float3[2], frame48B[1], exactly12additional persistent bytes.
// Dependencies/Pointers: existing IJob fence completes before camera publication/disposal; original009catalog/meshes/actor routes/render bounds unchanged. Wide endpoints fit full island; close views intentionally crop edges. Native occlusion/readability/device/browser acceptance remains root-owned; no gameplay/global mutations.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: unchanged catalog-local sphere/camera frame and48s alternating close sequence;14s enter/18s exit quintic transitions preserve C2 endpoints.
// Array Formats: same focus/velocity float3[2], frame48B[1] and numeric scalar state;94percent maximum zoom, log-size interpolation, critical focus frequency3/s.
// Dependencies/Pointers: original held/hidden/resize policies and completed job owner unchanged. No new allocation, gameplay authority or camera tick; native motion/readability/performance acceptance pending.
