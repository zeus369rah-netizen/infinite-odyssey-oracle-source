using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace OdysseyOracle
{
    [StructLayout(LayoutKind.Sequential)] public struct MenuShowcaseFrame48
    {
        public float3 Position;public float OrthographicSize;
        public quaternion Rotation;
        public float Near,Far,YawDegrees;public uint Valid;
    }
    public static class MenuShowcaseCameraMath
    {
        public const float TourSeconds=96f;
        public static bool ValidViewport(float aspect,float4 safe)=>math.isfinite(aspect)&&aspect>=.5f&&aspect<=4f&&math.all(math.isfinite(safe))&&
            math.all(safe.xy>=0)&&math.all(safe.zw<=1)&&math.all(safe.zw-safe.xy>=new float2(.12f));
        public static bool TryFit(float3 center,float3 extents,float aspect,float4 safe,float elapsed,out MenuShowcaseFrame48 frame)
        {
            frame=default;
            if(!ValidViewport(aspect,safe)||!math.all(math.isfinite(center))||!math.all(math.isfinite(extents))||math.any(extents<0)||
                math.cmax(math.abs(center))>2048||math.length(extents)>256||!math.isfinite(elapsed))return false;
            float phase=math.frac(math.max(0,elapsed)/TourSeconds),yaw=math.radians(218f)+phase*(2f*math.PI);
            float elevation=math.radians(50f+4f*math.sin(phase*4f*math.PI));
            float3 back=new float3(math.sin(yaw)*math.cos(elevation),math.sin(elevation),math.cos(yaw)*math.cos(elevation));
            float3 forward=-back,right=math.normalizesafe(math.cross(new float3(0,1,0),forward)),up=math.cross(forward,right);
            float halfX=math.dot(math.abs(right),extents),halfY=math.dot(math.abs(up),extents),halfZ=math.dot(math.abs(forward),extents);
            float2 span=safe.zw-safe.xy,ndcCenter=safe.xy+safe.zw-1f;
            float size=math.max(.1f,math.max(halfY/span.y,halfX/(aspect*span.x)))*1.01f,depth=halfZ+8f;
            frame=new MenuShowcaseFrame48{Position=center+back*depth-right*(ndcCenter.x*size*aspect)-up*(ndcCenter.y*size),OrthographicSize=size,
                Rotation=quaternion.LookRotationSafe(forward,new float3(0,1,0)),Near=.1f,Far=depth+halfZ+8f,YawDegrees=math.degrees(yaw) % 360f,Valid=1};
            return math.all(math.isfinite(frame.Position))&&math.isfinite(size)&&math.isfinite(frame.Far);
        }
    }
    [BurstCompile] public struct MenuShowcaseCameraFitJob:IJob
    {
        public float3 Center,Extents;public float Aspect,Time;public float4 SafeViewport;
        [WriteOnly] public NativeArray<MenuShowcaseFrame48> Result;
        public void Execute(){if(Result.Length>0){MenuShowcaseCameraMath.TryFit(Center,Extents,Aspect,SafeViewport,Time,out var frame);Result[0]=frame;}}
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: complete actual catalog-local Y-up AABB -> private orthographic camera, normalized free-left Canvas rectangle;96s full-yaw tour with46..54degree elevation.
// Array Formats: sequential48B camera frame[1], scalar center/extents/aspect/safe-rectangle/time; no allocation, Object API, physics, gameplay or global camera references.
// Dependencies/Pointers: caller validates catalog bounds extent<=256m and center<=2048m, completes job before publication/disposal. Fit includes every AABB corner/depth with1percent framing margin; does not prove mesh occlusion/readability/browser performance.
