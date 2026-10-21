using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace OdysseyOracle
{
    // PSEUDOCODE: select one exact accepted floor slot per support point in a
    // numeric job; complete the job; temporarily enable only the owned floors;
    // raycast those exact colliders on the main thread; publish real nine-hit
    // admissions only after all height/normal/identity checks pass.
    public struct CampaignPlantSupportRay020
    {
        public float3 Origin;
        public int FloorSlot;
    }

    [BurstCompile]
    public struct CampaignPlantSupportSelectionJob020:IJobParallelFor
    {
        public NativeArray<CampaignPlantSupportRay020> Rays;
        public void Execute(int index)
        {
            var ray=new CampaignPlantSupportRay020{FloorSlot=-1};
            int patch=index/9,sample=index%9;
            if(index<0||index>=CampaignPlantedForecourtRecipe020.PatchCount*9||
                !CampaignPlantedForecourtRecipe020.TrySupportPoint(patch,sample,out var point))
            {Rays[index]=ray;return;}
            ray.Origin=point+new float3(0f,.25f,0f);
            // Existing SurfaceCount stream is Morton ordered; choose the first
            // exact-height containing floor, never a nearby obstacle or map rect.
            for(int floor=0;floor<PortalLandingLayout020.SurfaceCount;floor++)
            {
                if(!PortalLandingLayout020.TrySurface(floor,out var box)||math.abs(box.MaxY-point.y)>.001f)continue;
                var local=box.Local(new LandingPoint020(point.x,point.y,point.z));
                if(local.X<box.MinX||local.X>box.MaxX||local.Z<box.MinZ||local.Z>box.MaxZ)continue;
                ray.FloorSlot=floor;break;
            }
            Rays[index]=ray;
        }
    }

    // Cold main-thread physics adapter. This is never scheduled as a job and
    // holds no fields, GameObjects, native allocations or callbacks of its own.
    public static class CampaignPlantNativeSupport020
    {
        // Preserve the complete process-local Unity identity; generation is a
        // separate proof field. Do not truncate, hash or persist its raw bits.
        public static ulong WorldToken(GameObject root)=>root!=null?EntityId.ToULong(root.GetEntityId()):0UL;
        public static bool CollectExactFloors(BoxCollider[] floors,NativeArray<CampaignPlantSupportRay020> rays,
            NativeArray<CampaignPlantPatchAdmission020> admissions,ulong worldToken,uint generation,
            uint worldRevision,uint geometryRevision,out int nativeHits,out int acceptedMask,out string failure)
        {
            nativeHits=acceptedMask=0;failure="";
            if(floors==null||floors.Length!=PortalLandingLayout020.SurfaceCount||!rays.IsCreated||
                rays.Length!=CampaignPlantedForecourtRecipe020.PatchCount*9||!admissions.IsCreated||
                admissions.Length!=CampaignPlantedForecourtRecipe020.PatchCount||worldToken==0||generation==0||worldRevision==0||geometryRevision==0)
            {failure="Garden native support requires exact owned floor and current scratch identities.";return false;}
            for(int patch=0;patch<CampaignPlantedForecourtRecipe020.PatchCount;patch++)
            {
                if(!CampaignPlantedForecourtRecipe020.TryCandidate(patch,out var candidate))
                {failure="Garden native support candidate is unavailable.";return false;}
                var proof=new CampaignPlantPatchAdmission020{WorldToken=worldToken,Generation=generation,
                    WorldRevision=worldRevision,GeometryRevision=geometryRevision,PatchIndex=(byte)patch,
                    SurfaceY=float.NaN,MinimumSupportY=float.PositiveInfinity,
                    MaximumSupportY=float.NegativeInfinity,MinimumNormalY=1f};
                for(int sample=0;sample<9;sample++)
                {
                    var request=rays[patch*9+sample];
                    if(request.FloorSlot<0||request.FloorSlot>=floors.Length||!math.all(math.isfinite(request.Origin)))
                    {failure="Garden native support has an invalid exact floor selection.";return false;}
                    var floor=floors[request.FloorSlot];
                    if(floor==null||!floor.enabled||!floor.gameObject.activeInHierarchy)
                    {failure="Garden native support requires an active owned bootstrap floor.";return false;}
                    var ray=new Ray(new Vector3(request.Origin.x,request.Origin.y,request.Origin.z),Vector3.down);
                    if(!floor.Raycast(ray,out var hit,.5f)||hit.collider!=floor||!Finite(hit.point)||!Finite(hit.normal)||
                        !math.isfinite(hit.distance)||hit.distance<0f||hit.distance>.5f||hit.normal.y<.95f||
                        math.abs(hit.point.y-candidate.SurfaceY)>.02f)
                    {failure="Garden native ray did not hit its exact owned support floor with an accepted normal/height.";return false;}
                    proof.MinimumSupportY=math.min(proof.MinimumSupportY,hit.point.y);
                    proof.MaximumSupportY=math.max(proof.MaximumSupportY,hit.point.y);
                    proof.MinimumNormalY=math.min(proof.MinimumNormalY,hit.normal.y);
                    proof.SupportMask|=(ushort)(1<<sample);
                    nativeHits++;
                }
                proof.SurfaceY=(proof.MinimumSupportY+proof.MaximumSupportY)*.5f;
                admissions[patch]=proof;
            }
            if(!CampaignPlantedForecourtRecipe020.TryCurrentAdmissionMask(admissions,worldToken,generation,
                worldRevision,geometryRevision,out acceptedMask)||acceptedMask!=255)
            {failure="Garden native support did not admit all eight matching physical/rendered beds.";return false;}
            return true;
        }
        static bool Finite(Vector3 value)=>math.isfinite(value.x)&&math.isfinite(value.y)&&math.isfinite(value.z);
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Stationary unit World/world metres and exact accepted floor Y;72 origins .25m above the eight garden support planes, down-rays limited to.5m.
// Array Formats:72 sixteen-byte numeric floor selections and8 current admission values; Morton floor traversal stays in a Burst job, while native Collider.Raycast is a cold main-thread adapter only.
// Dependencies/Pointers: World owns all69 floor colliders and performs synchronization without simulation/Ready publication. World transfers both native arrays and the last writer fence to PortalLandingScratch020 on every exit; the adapter owns no persistent resources and never fabricates hit evidence.
