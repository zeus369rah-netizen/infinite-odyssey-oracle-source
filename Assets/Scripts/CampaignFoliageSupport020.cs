using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace OdysseyOracle
{
    // PSEUDOCODE: capture the69 exact owned floor collider poses once; validate
    // their complete numeric agreement with the source in a Burst job; select
    // nine exact floor slots for each16 template; complete; collect144 actual
    // native hits; bind floor roots and the separate shared-render soil offset.
    public struct CampaignFoliageFloorPose020
    {
        public float3 Position,Size,Center,Scale;
        public quaternion Rotation;
    }
    public struct CampaignFoliageSupportRay020
    {
        public float3 Origin;
        public int FloorSlot;
    }
    [BurstCompile]
    public struct CampaignFoliageSupportJob020:IJob
    {
        [ReadOnly]public NativeArray<CampaignFoliageFloorPose020> Floors;
        [ReadOnly]public NativeArray<CampaignGardenFoliageAnchor020> Anchors;
        public NativeArray<CampaignFoliageSupportRay020> Rays;
        public NativeArray<int> Failure;
        public void Execute()
        {
            if(!Failure.IsCreated||Failure.Length!=1)return;Failure[0]=1;
            if(!Floors.IsCreated||Floors.Length!=PortalLandingLayout020.SurfaceCount||
                !Anchors.IsCreated||Anchors.Length!=16||!Rays.IsCreated||Rays.Length!=144)return;
            for(int i=0;i<Floors.Length;i++)
            {
                if(!PortalLandingLayout020.TrySurface(i,out var source))return;
                var pose=Floors[i];
                float3 expected=new float3((source.MinX+source.MaxX)*.5f,(source.MinY+source.MaxY)*.5f,(source.MinZ+source.MaxZ)*.5f);
                float3 size=new float3(source.MaxX-source.MinX,source.MaxY-source.MinY,source.MaxZ-source.MinZ);
                quaternion turn=quaternion.RotateY(math.radians(source.Yaw));
                if(!math.all(math.isfinite(pose.Position))||!math.all(math.isfinite(pose.Size))||
                    !math.all(math.isfinite(pose.Rotation.value))||!math.all(math.isfinite(pose.Center))||!math.all(math.isfinite(pose.Scale))||
                    math.distance(pose.Position,expected)>.0005f||math.distance(pose.Size,size)>.0005f||
                    math.lengthsq(pose.Center)>1e-8f||math.distance(pose.Scale,new float3(1f))>.0001f||
                    math.abs(math.lengthsq(pose.Rotation.value)-1f)>.0001f||math.abs(math.dot(pose.Rotation.value,turn.value))<.99999f)return;
            }
            for(int anchor=0;anchor<16;anchor++)
            {
                var value=Anchors[anchor];float a=math.radians(value.YawDegrees),s=math.sin(a),c=math.cos(a);
                for(int sample=0;sample<9;sample++)
                {
                    float x=(sample%3-1)*value.HalfExtents.x,z=(sample/3-1)*value.HalfExtents.y;
                    float3 p=value.SupportedFeet+new float3(x*c+z*s,0f,-x*s+z*c);
                    var ray=new CampaignFoliageSupportRay020{Origin=p+new float3(0f,.25f,0f),FloorSlot=-1};
                    for(int floor=0;floor<PortalLandingLayout020.SurfaceCount;floor++)
                    {
                        if(!PortalLandingLayout020.TrySurface(floor,out var box)||math.abs(box.MaxY-p.y)>.001f)continue;
                        var local=box.Local(new LandingPoint020(p.x,p.y,p.z));
                        if(local.X>=box.MinX&&local.X<=box.MaxX&&local.Z>=box.MinZ&&local.Z<=box.MaxZ){ray.FloorSlot=floor;break;}
                    }
                    Rays[anchor*9+sample]=ray;if(ray.FloorSlot<0)return;
                }
            }
            Failure[0]=0;
        }
    }
    public static class CampaignFoliageNativeSupport020
    {
        // Main-thread native adapter only. It never substitutes analytic/default hits.
        public static bool Collect(BoxCollider[] floors,NativeArray<CampaignFoliageSupportRay020> rays,
            NativeArray<CampaignGardenFoliageAnchor020> anchors,in CampaignGardenFoliageScope020 scope,
            out int actualHits,out int matchedSoils,out string failure)
        {
            actualHits=matchedSoils=0;failure="";
            if(floors==null||floors.Length!=69||!rays.IsCreated||rays.Length!=144||!anchors.IsCreated||anchors.Length!=16||!scope.Positive)
            {failure="Foliage native support stores or current identity are unavailable.";return false;}
            for(int i=0;i<16;i++)
            {
                var value=anchors[i];float low=float.PositiveInfinity,high=float.NegativeInfinity;ushort mask=0;
                for(int sample=0;sample<9;sample++)
                {
                    var request=rays[i*9+sample];
                    if(request.FloorSlot<0||request.FloorSlot>=69){failure="Foliage has no exact selected floor.";return false;}
                    var floor=floors[request.FloorSlot];
                    if(floor==null||!floor.enabled||!floor.gameObject.activeInHierarchy||
                        !floor.Raycast(new Ray((Vector3)request.Origin,Vector3.down),out var hit,.5f)||hit.collider!=floor||
                        !Finite(hit.point)||!Finite(hit.normal)||hit.normal.y<.95f||math.abs(hit.point.y-value.SupportedFeet.y)>.001f)
                    {failure="Foliage native ray did not hit its exact current owned floor.";return false;}
                    low=math.min(low,hit.point.y);high=math.max(high,hit.point.y);mask|=(ushort)(1<<sample);actualHits++;
                }
                if(mask!=0x1ff||high-low>.002f){failure="Foliage support has an invalid native height spread.";return false;}
                float plane=(low+high)*.5f;
                if(value.PatchKind==CampaignGardenFoliageRecipe020.BedPatch)
                {
                    int bed=(int)(value.AnchorKey-6000u);
                    if(!CampaignPlantedForecourtRecipe020.TrySoilBox020(bed,out float3 center,out float3 size,out float yaw)||
                        math.abs(center.y-size.y*.5f-plane)>.001f||
                        math.distance(new float2(center.x,center.z),new float2(value.SupportedFeet.x,value.SupportedFeet.z))>.001f||
                        math.abs(yaw-value.YawDegrees)>.001f||math.abs(center.y+size.y*.5f-plane-value.RootOffsetY)>.001f)
                    {failure="Foliage planter root offset disagrees with the shared emitted soil box.";return false;}
                    for(uint ordinal=0;ordinal<8;ordinal++)
                    {
                        if(!CampaignGardenFoliageRecipe020.TryClumpOffset(value.PatchKind,ordinal,out var p)||
                            math.abs(p.x)>size.x*.5f||math.abs(p.y)>size.z*.5f)
                        {failure="Foliage root leaves its actual emitted soil top.";return false;}
                    }
                    matchedSoils++;
                }
                else if(value.PatchKind!=CampaignGardenFoliageRecipe020.CornerPatch||math.abs(value.RootOffsetY)>.0001f)
                {failure="Foliage ground root has an unsupported offset.";return false;}
                value.SupportedFeet.y=plane;value.WorldToken=scope.WorldToken;value.Generation=scope.Generation;
                value.WorldRevision=scope.WorldRevision;value.GeometryRevision=scope.GeometryRevision;value.FoliageRevision=scope.FoliageRevision;
                value.SupportMask=mask;value.Supported=1;anchors[i]=value;
            }
            return actualHits==144&&matchedSoils==8;
        }
        static bool Finite(Vector3 p)=>math.isfinite(p.x)&&math.isfinite(p.y)&&math.isfinite(p.z);
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Actual unit current World root;16 full2m support rectangles, native floor planes, bed roots at separately verified shared soil top+.06m and corner roots on floor.
// Array Formats:69 captured64B floor poses,144 sixteen-byte ray selections,16 positive identity/support anchors; no Object API in the Burst pass and no borrowed native store escape.
// Dependencies/Pointers: World must already be positively admitted/active; caller captures exact first69 direct-owned floor colliders and source-matches every pose before native queries.144 hits are fresh; prior8 disposed proofs are never reused. Lease owns all native arrays/fence through cleanup failure.
