using Unity.Mathematics;
using Unity.Netcode;
using UnityEngine;
namespace OdysseyOracle
{
    public sealed partial class CoopTrainingLabDirector
    {
        int lastAcceptedPresentationShot;
        static float3 LabImpactNumeric(Vector3 p)=>new float3(p.x,p.y,p.z);
        bool RecordGuestAcceptedShot(SharedLabFrame frame)
        {
            if(rooms.IsHost)return true;
            int id=frame.AttackSequence;
            if(id<=lastAcceptedPresentationShot||id<=0||frame.MembershipGeneration==0||!AcceptedImpactRules.Point(LabImpactNumeric(frame.A))||
                (frame.Member!=NetworkManager.ServerClientId&&frame.Member!=rooms.LocalClientId))return false;
            lastAcceptedPresentationShot=id;
            shots[id%48]=new ShotRecord{Id=id,Epoch=epoch,Generation=dummyGeneration,Caster=frame.Member,CasterGeneration=frame.MembershipGeneration,
                Origin=frame.A,Expires=Time.realtimeSinceStartupAsDouble+4d,Hit=2,Nova=frame.Mask==15?1u:0u,Mask=frame.Mask,Element=(uint)frame.Dominant};return true;
        }
        void OnSpellImpact(int id,int ordinal,int element,Vector3 point,bool terminal)
        {
            if(!IsAuthorityContext||id<=0||element<0||element>3)return;var shot=shots[id%48];
            if(shot.Id!=id||shot.Epoch!=epoch||shot.Generation!=dummyGeneration||shot.Nova!=0||shot.Element!=(uint)element||shot.Expires<clock||
                (shot.Hit&2)==0||!rooms.TryGetAdmissionSlot(shot.Caster,out int slot)||slot<0||slot>1||peers[slot].Id!=shot.Caster||
                peers[slot].Generation!=shot.CasterGeneration||!rooms.IsCampaignMemberRegistered(shot.Caster))return;
            uint impacts=shot.Hit>>2;if(!AcceptedImpactRules.TryMark(ref impacts,ordinal,LabImpactNumeric(shot.Origin),LabImpactNumeric(point)))return;
            shot.Hit=(shot.Hit&3u)|(impacts<<2);shots[id%48]=shot;
            Emit(new SharedLabFrame{Kind=SharedLabKind.Impact,AttackSequence=id,Member=shot.Caster,MembershipGeneration=shot.CasterGeneration,
                Dominant=element,Mask=shot.Mask,Argument=(uint)ordinal,A=point,Flags=terminal?1u:0u});
        }
        void PresentAcceptedImpact(SharedLabFrame frame)
        {
            if(rooms.IsHost||frame.AttackSequence<=0||frame.Dominant<0||frame.Dominant>3||frame.Argument>2||frame.Flags>1||
                frame.LiveStatus!=0||frame.Reserved1!=0||frame.Reserved2!=0)return;
            int id=frame.AttackSequence;var shot=shots[id%48];
            if(shot.Id!=id||shot.Epoch!=epoch||shot.Generation!=dummyGeneration||shot.Nova!=0||(shot.Hit&2)==0||shot.Element!=(uint)frame.Dominant||
                shot.Mask!=frame.Mask||shot.Caster!=frame.Member||shot.CasterGeneration!=frame.MembershipGeneration||shot.Expires<Time.realtimeSinceStartupAsDouble)return;
            uint impacts=shot.Hit>>2;if(!AcceptedImpactRules.TryMark(ref impacts,(int)frame.Argument,LabImpactNumeric(shot.Origin),LabImpactNumeric(frame.A)))return;
            shot.Hit=(shot.Hit&3u)|(impacts<<2);shots[id%48]=shot;
            owner.Combat.PlayAcceptedPairNetworkImpact020(id,frame.Dominant,frame.A,(frame.Flags&1)!=0,ConfirmedSelection.ComboForMask((int)shot.Mask));
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: accepted host shot/current epoch, dummy/caster generations and actual origin -> authenticated bounded contact; guest uses local realtime expiry, not frozen host clock.
// Array Formats: existing48 native receipts add Origin/CasterGeneration, upper Hit bits carry three independent ordinals; wire remains256B Kind11 and LiveStatus word224 stays zero on Impact events.
// Dependencies/Pointers: Room server/token/sequence/epoch/generation checks run before Present; sole shared owner retains lifecycle/fences. Word224 is the version2 LiveStatus field; no damage/HP/session/permissions/actual Assets changes.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing accepted world contacts, projectile-local three-part visuals and cached shot identity; physical collision and gameplay semantics unchanged.
// Array Formats: Existing48/12 bolt pools,20 three-part impact slots, fixed six numeric/cue recipes and one extra local combo integer per accepted-shot receipt;64B/256B wire formats unchanged.
// Dependencies/Pointers: Existing host acceptance/dedupe/expiry and audio voice policies remain canonical. Presentation failures grant no damage, status, recovery or authority; root owns actual renderer/listening/browser/peer/performance acceptance.
