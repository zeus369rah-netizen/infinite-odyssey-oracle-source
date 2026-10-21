using Unity.Mathematics;
using Unity.Netcode;
using UnityEngine;
namespace OdysseyOracle
{
    public sealed partial class CoopDefenseDirector
    {
        string impactObservedRun;ulong impactRunLow,impactRunHigh;uint impactSerial;
        static float3 ImpactNumeric(Vector3 p)=>new float3(p.x,p.y,p.z);
        void ResetArenaImpactRun(){impactObservedRun=null;impactRunLow=impactRunHigh=0;impactSerial=0;}
        bool EnsureImpactRun()
        {
            if(ReferenceEquals(impactObservedRun,runId))return impactRunLow!=0||impactRunHigh!=0;
            impactObservedRun=runId;
            if(!ArenaImpactFrameRules.TryRun(runId,out ulong low,out ulong high)){impactRunLow=impactRunHigh=0;impactSerial=0;return false;}
            if(low!=impactRunLow||high!=impactRunHigh)impactSerial=0;impactRunLow=low;impactRunHigh=high;return true;
        }
        void OnAuthoritativeSpellImpact(int id,int ordinal,int element,Vector3 point,bool terminal)
        {
            if(!Running||!IsAuthority||!EnsureImpactRun()||impactSerial==uint.MaxValue||!ValidFeedbackPoint(point)||element<0||element>3||!pendingShots.TryGetValue(id,out var shot)||shot.Nova||shot.Element!=element||
                shot.Expires<simulationClock||!peers.TryGetValue(shot.Caster,out var peer)||!peer.ArenaReady||
                !AcceptedImpactRules.TryMark(ref shot.ImpactBits,ordinal,ImpactNumeric(shot.Origin),ImpactNumeric(point)))return;
            pendingShots[id]=shot;
            var frame=new ArenaImpactFrame64{Magic=ArenaImpactFrameRules.Magic,Version=ArenaImpactFrameRules.Version,RunLow=impactRunLow,RunHigh=impactRunHigh,
                Sequence=++impactSerial,ShotId=id,Caster=shot.Caster,Element=(uint)element,Ordinal=(uint)ordinal,Point=point,Flags=terminal?1u:0u};
            foreach(var recipient in peers)if(recipient.Key!=rooms.LocalClientId&&recipient.Value.ArenaReady)rooms.SendArenaImpact(frame,recipient.Key);
        }
        void OnArenaImpactFrame(ulong sender,ArenaImpactFrame64 frame)
        {
            if(!Running||IsAuthority||sender!=NetworkManager.ServerClientId||!EnsureImpactRun()||!ArenaImpactFrameRules.Shape(frame)||
                frame.RunLow!=impactRunLow||frame.RunHigh!=impactRunHigh||!ValidFeedbackPoint(frame.Point)||!KnownCaster(frame.Caster)||!pendingShots.TryGetValue(frame.ShotId,out var shot)||
                shot.Nova||shot.Caster!=frame.Caster||shot.Element!=(int)frame.Element||shot.Expires<simulationClock||
                !AcceptedImpactRules.TryMark(ref shot.ImpactBits,(int)frame.Ordinal,ImpactNumeric(shot.Origin),ImpactNumeric(frame.Point)))return;
            pendingShots[frame.ShotId]=shot;owner.Combat.PlayAcceptedPairNetworkImpact020(frame.ShotId,(int)frame.Element,frame.Point,(frame.Flags&1)!=0,shot.Combo020);
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: accepted defense run encoded losslessly128bits, host contact within20m and accepted caster/shot/ordinal receipt; no hot string serialization.
// Array Formats: existing128 bounded launch receipts with element/three bits, scalar cached numeric run and64B Room-owned binary frame; no temporary writer/byte array or new socket.
// Dependencies/Pointers: cold run parse, existing server-only Room transport and receipt expiry gate every landing. Independent ordinal bits permit distinct contacts without duplicate replay; old cast JSON remains untouched legacy only. Root native/protocol/order/audio acceptance pending.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing accepted world contacts, projectile-local three-part visuals and cached shot identity; physical collision and gameplay semantics unchanged.
// Array Formats: Existing48/12 bolt pools,20 three-part impact slots, fixed six numeric/cue recipes and one extra local combo integer per accepted-shot receipt;64B/256B wire formats unchanged.
// Dependencies/Pointers: Existing host acceptance/dedupe/expiry and audio voice policies remain canonical. Presentation failures grant no damage, status, recovery or authority; root owns actual renderer/listening/browser/peer/performance acceptance.
