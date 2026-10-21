using Unity.Mathematics;
using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class DungeonWorld
    {
        RefugeFlameReceipt020 refugeFlameReceipt020;
        [SerializeField] int refugeBrazierBindingCount020;
        public uint RefugeEligibleFlameCount020=>roomReactionState!=null?roomReactionState.EligibleFlameCount:0;
        public uint RefugeLitFlameCount020=>roomReactionState!=null?roomReactionState.EligibleLitFlameCount:0;
        public int RefugeBrazierBindingCount020=>refugeBrazierBindingCount020;
        uint RefugeFlameFlags020(Vector3 worldCenter,RoomReactionKind kind,uint flags)
        {
            if(!RefugeFlameRules020.Visual(kind)||(flags&1u)!=0||tutorialRefuge==null||tutorialRoot==null)return flags;
            Vector3 local=tutorialRoot.InverseTransformPoint(worldCenter);
            return RefugeFlameRules020.MainRoom(new float3(local.x,local.y,local.z),tutorialRefuge.HalfWidth)?flags|RefugeFlameRules020.EligibleFlag:flags;
        }
        internal bool BindRefugeBrazier020(Transform original,int ordinal)
        {
            if(original==null||tutorialRefuge==null||geometryRoot!=tutorialRoot||ordinal<0||ordinal>1024)return false;
            Transform anchor=original.Find("Anchor_Fire");var body=original.GetComponent<MeshRenderer>();
            if(anchor==null||body==null){roomReactionRejected++;return false;}
            const float height=.48f;
            int node=RegisterRoomNode(anchor.TransformPoint(Vector3.up*(height*.35f)),.44f,RoomReactionKind.LivingFlame,720020u+(uint)ordinal);
            if(node<0)return false;
            if(roomReactionLiving[node]!=null||roomReactionVoxel[node]!=null)return true;
            if(LivingCampfirePresentation.TryCreate(original,anchor.localPosition,height,out var living))roomReactionLiving[node]=living;
            else
            {
                var fallback=new GameObject("Original brazier voxel flame fallback020");fallback.transform.SetParent(original,false);fallback.transform.localPosition=anchor.localPosition;
                var voxel=fallback.AddComponent<VoxelFireEffect>();voxel.Initialize(batches[Surface.Flame].Material,batches[Surface.FlameCore].Material,height,true);
                roomReactionVoxel[node]=voxel;
            }
            roomReactionGlow[node]=RoomReactionGlowBinding.Create(body,null,body.sharedMaterials);
            roomReactionGlow[node].Apply(1f); // Cold property-block preparation before any active frame.
            RegisterFireEmitter(geometryRoot.InverseTransformPoint(anchor.position)+Vector3.up*.18f,TutorialFireLightPool.Fixture.SmallFlame,720020+ordinal);
            roomReactionApplied[node]=float.NaN;refugeBrazierBindingCount020++;return true;
        }
        internal bool BindRefugeLanternStatic020(Transform original)
        {
            if(original==null||tutorialRefuge==null||geometryRoot!=tutorialRoot)return false;
            Transform anchor=original.Find("Anchor_Fire");var body=original.GetComponent<MeshRenderer>();
            if(anchor==null||body==null||!body.enabled){roomReactionRejected++;return false;}
            int ordinal=roomLanternOrdinal++;
            int node=RegisterRoomNode(anchor.position,.65f,RoomReactionKind.Lantern,600000u+(uint)ordinal,uint.MaxValue);
            if(node<0)return false;
            roomReactionGlow[node]=RoomReactionGlowBinding.Create(body,null,body.sharedMaterials);
            roomReactionGlow[node].Apply(1f);roomReactionApplied[node]=float.NaN;
            return true;
        }
        void PublishRefugeFlameTransition020(ulong caster,uint shot,int ingredient,Vector3 point,in RoomReactionAccepted24 accepted)
        {
            if(ingredient!=8||accepted.Extinguished==0||!RoomReactionReady)return;
            var owner=GameSession.Instance;if(owner==null||owner.World!=this)return;
            var frame=new RefugeFlameEvent56{Epoch=owner.Rooms!=null&&owner.Rooms.Connected?owner.Rooms.RoomReactionEpoch:0,
                Generation=accepted.Generation,Revision=accepted.Revision,Shot=shot,Caster=caster,LayoutHash=roomReactionState.LayoutHash,
                Position=new float3(point.x,point.y,point.z),Extinguished=accepted.Extinguished,EligibleCount=accepted.EligibleCount,
                Flags=RefugeFlameRules020.Completed(ingredient,accepted.EligibleCount,accepted.BeforeLit,accepted.AfterLit)?1u:0u};
            if(owner.Rooms!=null&&owner.Rooms.Connected)owner.Rooms.PublishRefugeFlameEvent020(this,frame);
            else ReceiveRefugeFlameEvent020(frame,0,0);
        }
        internal bool ReceiveRefugeFlameEvent020(in RefugeFlameEvent56 frame,uint expectedEpoch,ulong localCaster)
        {
            if(!IsTutorialRefuge||IsCoopArena)return false;TickElementalRoomReactions();
            if(!RoomReactionReady||frame.Epoch!=expectedEpoch||frame.Generation==0||frame.Revision==0||frame.Shot==0||frame.LayoutHash!=roomReactionState.LayoutHash||
                frame.Extinguished==0||frame.Extinguished>ElementalRoomReactionState.Capacity||frame.EligibleCount!=roomReactionState.EligibleFlameCount||frame.Flags>1||
                frame.Complete&&frame.EligibleCount==0||!ElementalRoomReactionState.Point(frame.Position))return false;
            var owner=GameSession.Instance;if(owner==null||owner.World!=this)return false;
            bool remote=owner.Rooms!=null&&owner.Rooms.Connected&&!owner.Rooms.IsHost;
            if(!remote&&(frame.Generation!=roomReactionState.Generation||frame.Complete&&roomReactionState.EligibleLitFlameCount!=0))return false;
            if(remote&&roomReactionState.HasSnapshot&&frame.Generation!=roomReactionState.Generation&&!RefugeFlameRules020.Newer(frame.Generation,roomReactionState.Generation))return false;
            if(!refugeFlameReceipt020.Accept(frame.Epoch,frame.Generation,frame.Revision))return false;
            owner.Audio?.TryPlayWindExtinguish020(new Vector3(frame.Position.x,frame.Position.y,frame.Position.z),frame.Epoch,frame.Generation,frame.Revision);
            if(frame.Complete&&frame.Caster==localCaster)owner.RefugeActivities?.RecordAllRefugeFlamesOut020(this);
            return true;
        }
        void ResetRefugeFlameReceipt020(){refugeFlameReceipt020.Reset();}
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: original saved brazier Anchor_Fire/localY.55, central main-room membership and accepted host world contacts; saved assets/collider unchanged.
// Array Formats: one cold existing flame presentation plus glow binding per authored brazier; fixed LivingFlame identity independent of shader fallback, existing native stores and one scalar event receipt.
// Dependencies/Pointers: original World/reaction authority owns fuel and nodes; one accepted Air aggregate publishes one cue/event, final local caster alone records independent achievement. No snapshot award, target mutation from events, new physics framework or per-frame allocation.
