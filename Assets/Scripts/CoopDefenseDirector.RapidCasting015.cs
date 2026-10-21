using UnityEngine;
namespace OdysseyOracle
{
    public sealed partial class CoopDefenseDirector
    {
        uint localHeldSelection015;
        readonly PresentationMessage rapidShot015=new PresentationMessage();
        public bool RequestHeldFire015(int freshMask)
        {
            if(!Running||rooms==null||!rooms.Connected||!ConfirmedSelection.ValidMask(freshMask)||
                localSelectionSequence==uint.MaxValue||localCastSequence==uint.MaxValue)return false;
            uint select=++localSelectionSequence,nonce=++localCastSequence;localHeldSelection015=select;
            if(IsAuthority)return TryHeldFire015(rooms.LocalClientId,freshMask,nonce,select);
            rooms.SendReliable(JsonUtility.ToJson(new Message{kind="oracle.held.v1",run=runId,selectionMask=freshMask,castSequence=nonce,selectionSequence=select}));return true;
        }
        public void CancelHeldFire015()
        {
            uint selection=localHeldSelection015;localHeldSelection015=0;
            if(selection==0||!Running||rooms==null||!rooms.Connected||localCastSequence==uint.MaxValue)return;
            uint nonce=++localCastSequence;
            if(IsAuthority)TryHeldFire015(rooms.LocalClientId,0,nonce,selection);
            else rooms.SendReliable(JsonUtility.ToJson(new Message{kind="oracle.held.v1",run=runId,selectionMask=0,castSequence=nonce,selectionSequence=selection}));
        }
        bool TryHeldFire015(ulong id,int mask,uint nonce,uint sequence)
        {
            if(!Running||!clockStarted||!IsAuthority||!peers.TryGetValue(id,out var peer)||!peer.ArenaReady||
                !peer.CastRequests.Accept(nonce))return false;
            if(mask==0)return peer.Held015.Cancel(sequence);
            if(!ConfirmedSelection.ValidMask(mask)||sequence==0||owner.Revival!=null&&owner.Revival.IsPeerDowned(id)||
                !peer.TapRequests.Accept(sequence))return false;
            peer.Held015.Clear();
            if(!peer.Held015.Begin(mask,sequence,simulationClock)||!RapidCastRules015.FreshSelection(ref peer.Selection,mask))return false;
            peer.Element=peer.Selection.ConfirmedElement;peer.AutoSelectionWait.NotifyInput(simulationClock);
            rooms.RecordAcceptedPeerActivity(id);TickHeldPeer015(id,peer,simulationClock,true);SendPersonal(id,0);return true;
        }
        void TickHeldPeer015(ulong id,Peer peer,double now,bool justBegan=false)
        {
            if(id==rooms.LocalClientId)
            {
                if(!owner.Combat.HeldFireContext015){peer.Held015.Clear();return;}
                if(!justBegan&&OracleCombat.ReadPhysicalElementEdges015()!=0)return;
            }
            if(!peer.ArenaReady||!ConnectedId(id)||owner.Revival!=null&&owner.Revival.IsPeerDowned(id)||
                !peer.Held015.Valid(peer.Selection.ConfirmedMask,peer.TapRequests.Last,now))
            {peer.Held015.Clear();return;}
            if(!peer.Selection.Ready(now,peer.NextCast,peer.ReturnReadyAt))return;
            // One first-eligible attempt; failures keep selection but cannot become delayed retries.
            peer.Held015.Clear();
            TryCast(id,peer.Selection.ConfirmedElement,peer.Selection.ConfirmedMask,now,null,false,0,peer.TapRequests.Last,true);
        }
        bool TryCastThree015(ulong id,Peer peer,int mask,double now,Vector3 origin,Vector3 direction,bool held,bool automatic)
        {
            PrunePendingShots();
            if(!RapidCastRules015.Capacity(mask,3,pendingShots.Count,128,shotSerial))return false;
            // Preflight conservative existing per-second presentation budget for
            // all three messages; no intermediate Update can spend it mid-batch.
            double realtime=Time.realtimeSinceStartupAsDouble;
            if(realtime-presentationWindow>=1d){presentationWindow=realtime;presentationCount=presentationBytes=0;}
            if(presentationCount>29||presentationBytes>29*1024||presentationSerial>int.MaxValue-3)return false;
            float support=owner.Builder!=null?owner.Builder.SupportDamageMultiplierAt(origin-Vector3.up*1.65f):1f;
            float d0=ElementProgression.GetDamageMultiplier(peer.Mask,0,peer.Level)*support;
            float d1=ElementProgression.GetDamageMultiplier(peer.Mask,1,peer.Level)*support;
            float d2=ElementProgression.GetDamageMultiplier(peer.Mask,2,peer.Level)*support;
            float d3=ElementProgression.GetDamageMultiplier(peer.Mask,3,peer.Level)*support;
            int first=shotSerial+1;
            if(!owner.Combat.TryLaunchThree015(origin,direction,mask,d0,d1,d2,d3,first,peer.Mask))return false;
            shotSerial+=3;
            for(int n=0;n<3;n++)
            {
                int element=RapidCastRules015.ElementAt(mask,n),shot=first+n;
                pendingShots[shot]=new PendingShot{Caster=id,Origin=origin,Expires=simulationClock+4d,Element=element};
            }
            ChargeAcceptedShot(peer,mask,false,now);peer.Nova.Reset();
            if(held)peer.Selection.Clear();if(!automatic)rooms.RecordAcceptedPeerActivity(id);
            for(int n=0;n<3;n++)
            {
                int element=RapidCastRules015.ElementAt(mask,n),shot=first+n;
                owner.Combat.PlayNetworkShotFeedback(element,origin,direction,0,id==rooms.LocalClientId,false,shot);
                rapidShot015.kind="oracle.shot.v1";rapidShot015.shotId=shot;rapidShot015.casterId=id;rapidShot015.element=element;
                rapidShot015.combo=0;rapidShot015.origin=origin;rapidShot015.direction=direction;rapidShot015.cosmicNova=false;
                SendPresentation(rapidShot015);
            }
            SendPersonal(id,0);return true;
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing accepted world metres/Y-up, input bits and owner simulation seconds.
// Array Formats: Four ingredient deadlines, fixed48 projectile pool, at most three component shots; existing host frame ABIs.
// Dependencies/Pointers: Work-only rapid-casting candidate. Existing host hit/damage/reward owners retained; root owns native/browser acceptance.
