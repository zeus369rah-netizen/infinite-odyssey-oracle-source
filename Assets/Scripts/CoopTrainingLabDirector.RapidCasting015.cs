using UnityEngine;
namespace OdysseyOracle
{
    public sealed partial class CoopTrainingLabDirector
    {
        HeldFireIntent015 heldFirst015,heldSecond015;
        HeldFireIntent015 HeldLab015(int slot)=>slot==0?heldFirst015:heldSecond015;
        void SetHeldLab015(int slot,HeldFireIntent015 value){if(slot==0)heldFirst015=value;else heldSecond015=value;}
        void ResetHeldLab015(){heldFirst015=heldSecond015=default;}
        public bool RequestHeldFire015(int mask)=>ConfirmedSelection.ValidMask(mask)&&Queue(SharedLabKind.HeldFire,1,(uint)mask);
        public void CancelHeldFire015(){if(localTap!=0)Queue(SharedLabKind.HeldFire,0);}
        void AcceptHeldLab015(int slot,SharedLabFrame frame)
        {
            var state=peers[slot];bool accepted=false;
            if(frame.Argument==0)
            {var held=HeldLab015(slot);accepted=held.Cancel(frame.SelectionSequence);SetHeldLab015(slot,held);}
            else if(frame.Revision==revision&&frame.DummyGeneration==dummyGeneration&&frame.TowerGeneration==towerGeneration&&
                SharedLabCastMath.AdmitInput(ref state,clock,false)&&SharedLabCastMath.Newer(frame.Sequence,state.LastTap))
            {
                var selection=new ConfirmedSelection();var held=new HeldFireIntent015();
                if(held.Begin((int)frame.Mask,frame.Sequence,clock)&&RapidCastRules015.FreshSelection(ref selection,(int)frame.Mask))
                {
                    state.Pending=0;state.Mask=(uint)selection.ConfirmedMask;state.Dominant=selection.ConfirmedElement;
                    state.Flags&=~1u;state.LastTap=frame.Sequence;state.AutoReady=clock+.5d;
                    SetHeldLab015(slot,held);accepted=true;
                }
            }
            state.LastResult=accepted?0u:1u;peers[slot]=state;
            if(accepted){rooms.RecordAcceptedPeerActivity(state.Id);if(frame.Argument==1)TickHeldLab015(slot,true);}
            SendState(0);SendState(1);
        }
        // Called only after the readiness fence is complete. These two scalar
        // records never enter a job; membership/actor/control resets clear them.
        bool TickHeldLab015(int slot,bool justBegan=false)
        {
            var held=HeldLab015(slot);if(!held.Active)return false;
            var state=peers[slot];
            if(state.Id==rooms.LocalClientId)
            {
                if(!owner.Combat.HeldFireContext015){SetHeldLab015(slot,default);return true;}
                if(!justBegan&&OracleCombat.ReadPhysicalElementEdges015()!=0)return true;
            }
            if(state.Generation==0||!rooms.IsCampaignMemberRegistered(state.Id)||!lab.SessionActive||
                !held.Valid((int)state.Mask,state.LastTap,clock))
            {SetHeldLab015(slot,default);return true;}
            if(!SharedLabCastMath.Ready(state,clock,false))return true;
            SetHeldLab015(slot,default);
            if(rooms.TryGetPlayerAim(state.Id,out var aim)&&TryLaunch(slot,false,aim))
            {
                state=peers[slot];state.Mask=state.Pending=0;state.Dominant=-1;state.Flags&=~1u;peers[slot]=state;
                SendState(slot);
            }
            return true;
        }
        bool TryLaunchThreeLab015(int slot,ref SharedLabCastState state,Vector3 origin,Vector3 aim)
        {
            int mask=(int)state.Mask;
            if(!RapidCastRules015.Capacity(mask,3,0,48,shotSerial))return false;
            int first=shotSerial+1;
            for(int n=0;n<3;n++)
            {
                var old=shots[(first+n)%48];
                if(old.Id!=0&&old.Epoch==epoch&&old.Expires>=clock)return false;
            }
            if(!owner.Combat.TryLaunchThree015(origin,aim,mask,1,1,1,1,first,0))return false;
            shotSerial+=3;
            for(int n=0;n<3;n++)
            {
                int id=first+n,element=RapidCastRules015.ElementAt(mask,n);
                shots[id%48]=new ShotRecord{Id=id,Epoch=epoch,Generation=dummyGeneration,Caster=state.Id,
                    CasterGeneration=state.Generation,Origin=origin,Expires=clock+4,Hit=2,Mask=1u<<element,Element=(uint)element};
            }
            SharedLabCastMath.Commit(ref state,clock,true);peers[slot]=state;
            for(int n=0;n<3;n++)
            {
                int element=RapidCastRules015.ElementAt(mask,n);
                Emit(new SharedLabFrame{Kind=SharedLabKind.Shot,Member=state.Id,MembershipGeneration=state.Generation,Mask=1u<<element,
                    Dominant=element,A=origin,B=aim,AttackSequence=first+n});
            }
            return true;
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing accepted world metres/Y-up, input bits and owner simulation seconds.
// Array Formats: Four ingredient deadlines, fixed48 projectile pool, at most three component shots; existing host frame ABIs.
// Dependencies/Pointers: Work-only rapid-casting candidate. Existing host hit/damage/reward owners retained; root owns native/browser acceptance.
