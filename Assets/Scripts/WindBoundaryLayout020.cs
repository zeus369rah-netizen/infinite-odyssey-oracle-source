using Unity.Collections;
using OdysseyOracle;
namespace OracleNatureBoundary020
{
    public static class WindBoundaryLayout020
    {
        public static bool TryCopy(NativeArray<BoundaryQuad020> destination,int capacity,out int count)
        {
            count=0;
            if(!destination.IsCreated||capacity<WindBoundaryMath020.Quads||capacity>destination.Length||PortalLandingLayout020.Version!=2||PortalLandingLayout020.SurfaceCount!=69)return false;
            for(int i=0;i<69;i++)
            {
                if(!PortalLandingLayout020.TrySurface(i,out var box)||box.Kind!=1||
                    !WindBoundaryMath020.FromBox(box.Key,box.MinX,box.MinZ,box.MaxX,box.MaxZ,box.Cos,box.Sin,box.MaxY,out var quad))return false;
                destination[i]=quad;
            }
            count=69;return true;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Exact final version2/69-floor layout, correct inverse of LandingBox.Local yaw transform into World-XZ.
// Array Formats: One caller-owned at-least69-quad destination, no allocation, Object API, obstacle data or floor-AABB substitution.
// Dependencies/Pointers: Root verifies the frozen source pins and actual published matching geometry before this numeric copy; copy failure leaves count0 and its staging unpublished.
