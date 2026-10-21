// PSEUDOCODE: register only actual newly created owned support colliders; cold-sort bounded metadata; copy values only into caller-owned arrays after current-scope validation; retire before root destruction.
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
namespace OdysseyOracle
{
    public sealed partial class DungeonWorld
    {
        NativeArray<SupportedStepRow020> supportedStepRows020;
        JobHandle supportedStepSort020;
        Transform supportedStepRoot020;
        uint supportedStepGeneration020;
        int supportedStepCount020;
        bool supportedStepReady020;
        public bool SupportedFootstepRegistryReady020=>supportedStepReady020&&supportedStepRows020.IsCreated;
        void PrepareSupportedFootstepRegistry020(Transform root,uint generation)
        {
            DisposeSupportedFootstepRegistry020();
            supportedStepRows020=new NativeArray<SupportedStepRow020>(SupportedFootstepRules020.Capacity,Allocator.Persistent);
            supportedStepRoot020=root;supportedStepGeneration020=generation;supportedStepCount020=0;
        }
        void RecordSupportedFootstepCollider020(BoxCollider collider,in LandingBox020 box)
        {
            if(!supportedStepRows020.IsCreated||!SupportedFootstepRules020.Metadata(box,out byte wood,out byte flight))return;
            if(collider==null||collider.isTrigger||collider.transform.parent!=supportedStepRoot020||supportedStepCount020>=supportedStepRows020.Length)
            {supportedStepCount020=SupportedFootstepRules020.Capacity+1;return;}
            supportedStepRows020[supportedStepCount020++]=new SupportedStepRow020{Collider=EntityId.ToULong(collider.GetEntityId()),Key=box.Key,Morton=box.Morton,
                Minimum=new float3(box.MinX,box.MinY,box.MinZ),Maximum=new float3(box.MaxX,box.MaxY,box.MaxZ),Rotation=quaternion.RotateY(math.radians(box.Yaw)),Wood=wood,Flight=flight};
        }
        void SealSupportedFootstepRegistry020()
        {
            supportedStepReady020=false;
            if(!supportedStepRows020.IsCreated||supportedStepCount020!=SupportedFootstepRules020.Capacity)return;
            supportedStepSort020=new SupportedStepSortJob020{Rows=supportedStepRows020}.Schedule();supportedStepSort020.Complete();supportedStepReady020=true;
        }
        void RebindSupportedFootstepRegistry020(uint generation)
        {supportedStepSort020.Complete();if(supportedStepReady020&&supportedStepRoot020==portalLandingRoot020&&generation!=0)supportedStepGeneration020=generation;else supportedStepReady020=false;}
        internal bool TryCopySupportedFootstepRows020(NativeArray<SupportedStepRow020> destination,Transform root,uint generation,uint revision)
        {
            if(!supportedStepReady020||!supportedStepRows020.IsCreated||!destination.IsCreated||destination.Length!=SupportedFootstepRules020.Capacity||
                generation==0||revision==0||generation!=supportedStepGeneration020||revision!=WorldRevision020||generation!=CampaignHubGeneration020||
                !CampaignHubActive020||!ReferenceEquals(root,supportedStepRoot020)||root==null||!root.gameObject.activeInHierarchy||
                !transform.localToWorldMatrix.Equals(Matrix4x4.identity)||!root.localToWorldMatrix.Equals(Matrix4x4.identity)||
                !TryGetPortalLandingPrototype020(generation,out var snapshot)||!ReferenceEquals(root,snapshot.Root))return false;
            supportedStepSort020.Complete();NativeArray<SupportedStepRow020>.Copy(supportedStepRows020,destination);return true;
        }
        void DisposeSupportedFootstepRegistry020()
        {
            supportedStepReady020=false;supportedStepSort020.Complete();
            if(supportedStepRows020.IsCreated)supportedStepRows020.Dispose();
            supportedStepRoot020=null;supportedStepGeneration020=0;supportedStepCount020=0;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Sole actual cold-built current portal root and exact native collider64bit identities.
// Array Formats: One owned72row NativeArray and completed cold sort fence; external readers receive private value copies only.
// Dependencies/Pointers: Narrow prototype create/seal/rebind/retire hooks only. No new collider, renderer, floor, material, geometry or Ready authority; source support metadata is not native traversal/audio acceptance.
