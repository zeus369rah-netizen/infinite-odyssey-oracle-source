using Unity.Collections;
using Unity.Jobs;
using Unity.Netcode;
using UnityEngine;

namespace OdysseyOracle
{
    [DisallowMultipleComponent]
    public sealed partial class CoopTrainingLabDirector:MonoBehaviour
    {
        struct ShotRecord {public int Id;public uint Epoch,Generation;public ulong Caster;public double Expires;public uint Hit,Nova,Mask,Element,CasterGeneration;public Vector3 Origin;}
        GameSession owner;RoomConnection rooms;TrainingLabDirector lab;
        NativeArray<SharedLabCastState> peers;
        NativeArray<SharedLabFrame> requests;
        NativeArray<byte> ready;
        NativeArray<ShotRecord> shots;
        JobHandle fence;
        readonly RaycastHit[] sight=new RaycastHit[16];
        bool initialized,entered,priorRemote,stopping,blocked;
        int head,count,shotSerial;
        uint epochCounter,epoch,revision,dummyGeneration,towerGeneration,wireSerial,lastWire,localSequence,localTap,localToken,localGeneration;
        double clock,nextState,nextHello,nextHostHello0,nextHostHello1;
        public bool ContextActive=>entered&&Eligible;
        public bool IsAuthorityContext=>ContextActive&&rooms.IsHost;
        public bool HostSimulationActive=>IsAuthorityContext;
        public bool ClientReady=>ContextActive&&!rooms.IsHost&&epoch!=0&&localGeneration!=0;
        bool Eligible=>initialized&&enabled&&lab!=null&&lab.isActiveAndEnabled&&owner!=null&&rooms!=null&&rooms.Connected&&owner.HasStarted&&owner.CoopMode&&
            owner.World!=null&&owner.World.IsTutorialRefuge&&owner.Combat!=null&&owner.CombatUnlocked&&owner.Tutorial!=null&&owner.Tutorial.Finished&&
            !owner.Victory&&!owner.IsDead&&!owner.IsDowned&&(owner.Loading==null||!owner.Loading.IsLoading)&&
            (owner.Defense==null||!owner.Defense.Running)&&!owner.Combat.CosmicNovaTrialActive&&!owner.Combat.TutorialTowerPracticeActive;
        public void Initialize(GameSession session)
        {
            if(initialized||session==null||session.Rooms==null||session.TrainingLab==null)return;
            if(!System.BitConverter.IsLittleEndian||Unity.Collections.LowLevel.Unsafe.UnsafeUtility.SizeOf<SharedLabFrame>()!=256||
                Unity.Collections.LowLevel.Unsafe.UnsafeUtility.SizeOf<SharedLabCastState>()!=128)
                throw new System.InvalidOperationException("Shared lab fixed frame/native ABI is unsupported on this target.");
            owner=session;rooms=session.Rooms;lab=session.TrainingLab;
            try
            {
                peers=new NativeArray<SharedLabCastState>(2,Allocator.Persistent);requests=new NativeArray<SharedLabFrame>(16,Allocator.Persistent);
                ready=new NativeArray<byte>(2,Allocator.Persistent);shots=new NativeArray<ShotRecord>(48,Allocator.Persistent);
                fence=new SharedLabReadyJob{Peers=peers,Ready=ready,Now=0}.Schedule(2,1);fence.Complete();
                rooms.SharedLabMessage+=Receive;owner.Combat.AuthoritativeShotHit+=OnHit;owner.Combat.AuthoritativeNovaImpact+=OnNova;owner.Combat.AuthoritativeSpellImpact+=OnSpellImpact;
                initialized=true;
            }
            catch
            {
                rooms.SharedLabMessage-=Receive;
                if(owner.Combat!=null){owner.Combat.AuthoritativeShotHit-=OnHit;owner.Combat.AuthoritativeNovaImpact-=OnNova;owner.Combat.AuthoritativeSpellImpact-=OnSpellImpact;}
                DisposeBuffers();throw;
            }
        }
        void Update()
        {
            if(!initialized)return;
            if(!Eligible){blocked=false;if(entered)StopContext();return;}
            if(!entered&&!blocked)EnterContext();
            if(!ContextActive)return;
            if(!rooms.IsHost)
            {
                if(Time.realtimeSinceStartupAsDouble>=nextHello)
                {nextHello=Time.realtimeSinceStartupAsDouble+1d;rooms.SendSharedLab(new SharedLabFrame{Version=SharedLabFrame.LabVersion,Kind=SharedLabKind.Hello,ClientToken=localToken},NetworkManager.ServerClientId);}
                return;
            }
            fence.Complete();clock+=Mathf.Clamp(Time.unscaledDeltaTime,0,.1f);ReconcileGuest();
            DrainInputs();
            fence=new SharedLabReadyJob{Peers=peers,Ready=ready,Now=clock}.Schedule(2,1);fence.Complete();
            bool held0=TickHeldLab015(0),held1=TickHeldLab015(1);
            if(!held0&&ready[0]!=0)TryLaunch(0,true,default);if(!held1&&ready[1]!=0)TryLaunch(1,true,default);
            if(clock>=nextState){nextState=clock+.1d;SendState(0);SendState(1);}
        }
        void EnterContext()
        {
            fence.Complete();lab.EndSession();lab.ClearSharedSnapshot();
            priorRemote=owner.Combat.SharedLabRemote;owner.Combat.SetRemoteSimulation(!rooms.IsHost);owner.Combat.ResetSharedLabInput();
            ResetHeldLab015();peers[0]=peers[1]=default;ready[0]=ready[1]=0;head=count=0;clock=nextState=nextHello=nextHostHello0=nextHostHello1=0;
            revision=dummyGeneration=towerGeneration=1;wireSerial=lastWire=localSequence=localTap=localGeneration=0;
            lastAcceptedPresentationShot=0;
            localToken++;if(localToken==0)localToken=1;
            epoch=0;entered=true;
            if(rooms.IsHost)
            {
                epoch=++epochCounter;if(epoch==0){enabled=false;StopContext();return;}
                Bind(rooms.LocalClientId,0);var state=peers[0];state.Token=localToken;peers[0]=state;localGeneration=state.Generation;
                if(!lab.PrepareSharedCastSession()){StopContext();blocked=true;return;}SendState(0);
            }
        }
        public void StopContext()
        {
            if(!initialized||stopping||!entered)return;stopping=true;entered=false;
            try
            {
                fence.Complete();ResetHeldLab015();head=count=0;peers[0]=peers[1]=default;ready[0]=ready[1]=0;
                for(int i=0;i<shots.Length;i++)shots[i]=default;
                epoch=localGeneration=0;if(lab!=null){lab.EndSession();lab.ClearSharedSnapshot();}
            }
            finally
            {
                try{if(owner!=null&&owner.Combat!=null){owner.Combat.ResetSharedLabInput();owner.Combat.SetRemoteSimulation(priorRemote);}}
                finally{stopping=false;}
            }
        }
        bool Bind(ulong id,int slot)
        {
            if(slot<0||slot>1||!rooms.IsCampaignMemberRegistered(id)||!rooms.TryGetAdmissionSlot(id,out int accepted)||accepted!=slot)return false;
            var state=peers[slot];if(state.Generation!=0&&state.Id==id)return true;
            uint generation=state.Generation+1;if(generation==0)return false;
            SetHeldLab015(slot,default);peers[slot]=new SharedLabCastState{Id=id,Generation=generation,Dominant=-1};return true;
        }
        void ReconcileGuest()
        {
            var guest=peers[1];if(guest.Generation!=0&&(!rooms.IsCampaignMemberRegistered(guest.Id)||!rooms.TryGetAdmissionSlot(guest.Id,out int slot)||slot!=1))
            {uint generation=guest.Generation+1;SetHeldLab015(1,default);peers[1]=new SharedLabCastState{Generation=generation,Dominant=-1};}
        }
        bool Slot(ulong id,out int slot)
        {
            slot=-1;if(!rooms.TryGetAdmissionSlot(id,out int value)||value<0||value>1||!Bind(id,value))return false;
            slot=value;return true;
        }
        void Receive(ulong sender,SharedLabFrame frame)
        {
            if(!ContextActive)return;
            if(rooms.IsHost)
            {
                fence.Complete();
                if(!SharedLabCastMath.RequestShape(frame)||!Slot(sender,out int slot))return;
                var state=peers[slot];
                if(frame.Kind==SharedLabKind.Hello)
                {
                    if(clock<(slot==0?nextHostHello0:nextHostHello1))return;
                    if(slot==0)nextHostHello0=clock+1;else nextHostHello1=clock+1;
                    if(state.Token!=(uint)frame.ClientToken)SetHeldLab015(slot,default);
                    state.Token=(uint)frame.ClientToken;peers[slot]=state;SendState(slot);return;
                }
                if(!SharedLabCastMath.AdmitRequest(ref state,frame,epoch))return;peers[slot]=state;
                // Explicit network/UI control is a cold scene mutation boundary.
                // Drain preceding scalar input first; never create actors in our Update pass.
                if(frame.Kind==SharedLabKind.Control){DrainInputs();AcceptRequest(sender,frame);return;}
                if(count==16){SendState(slot);return;}
                frame.Member=sender;requests[(head+count)&15]=frame;count++;return;
            }
            if(sender!=NetworkManager.ServerClientId||frame.Version!=SharedLabFrame.LabVersion||frame.ClientToken!=localToken||frame.Sequence==0)return;
            if(epoch==0&&frame.Kind!=SharedLabKind.State)return;
            bool sameEpoch=epoch==0||frame.Epoch==epoch;
            if(!sameEpoch&&(frame.Kind!=SharedLabKind.State||!SharedLabCastMath.Newer(frame.Epoch,epoch))||sameEpoch&&!SharedLabCastMath.Newer(frame.Sequence,lastWire))return;
            if(frame.Kind==SharedLabKind.State)
            {
                if(frame.Member!=rooms.LocalClientId||!SharedLabCastMath.StateShape(frame)||
                    (frame.A-owner.World.TrainingLabDummyPosition).sqrMagnitude>1f)return;
                if(!sameEpoch){lab.EndSession();lab.ClearSharedSnapshot();owner.Combat.ResetSharedLabInput();localTap=0;lastAcceptedPresentationShot=0;for(int i=0;i<shots.Length;i++)shots[i]=default;}
                epoch=frame.Epoch;revision=frame.Revision;localGeneration=frame.MembershipGeneration;lastWire=frame.Sequence;
                dummyGeneration=frame.DummyGeneration;towerGeneration=frame.TowerGeneration;
                lab.ApplySharedReplica(frame);owner.Combat.ApplySharedLabInput(frame);return;
            }
            if(frame.Epoch!=epoch||frame.DummyGeneration!=dummyGeneration||frame.TowerGeneration!=towerGeneration||
                frame.LiveStatus!=0||!SharedLabCastMath.Finite(frame.A)||!SharedLabCastMath.Finite(frame.B)||!SharedLabCastMath.Finite(frame.C))return;
            lastWire=frame.Sequence;Present(frame);
        }
        bool Queue(SharedLabKind kind,uint argument,uint mask=0,Vector3 aim=default)
        {
            if(!ContextActive||epoch==0||localGeneration==0)return false;
            uint serial=++localSequence;if(serial==0)return false;
            if(kind==SharedLabKind.Tap||kind==SharedLabKind.HeldFire&&argument==1)localTap=serial;
            bool sent=rooms.SendSharedLab(new SharedLabFrame{Version=SharedLabFrame.LabVersion,Kind=kind,Epoch=epoch,Revision=revision,Sequence=serial,Argument=argument,Mask=mask,
                MembershipGeneration=localGeneration,SelectionSequence=localTap,DummyGeneration=dummyGeneration,TowerGeneration=towerGeneration,
                A=aim,ClientToken=localToken},NetworkManager.ServerClientId);
            return sent&&(!rooms.IsHost||kind!=SharedLabKind.Control||peers[0].LastResult==0);
        }
        public bool RequestControl(SharedLabControl command,uint argument)=>Queue(SharedLabKind.Control,argument,(uint)command);
        public bool RequestTap(int element)=>element>=0&&element<4&&Queue(SharedLabKind.Tap,(uint)element);
        public bool RequestCast()
        {return owner!=null&&owner.IsPlaying&&rooms.TryGetPlayerAim(rooms.LocalClientId,out var aim)&&Queue(SharedLabKind.Cast,0,0,aim);}
        public bool RequestAuto(bool value)=>Queue(SharedLabKind.Auto,value?1u:0u);
        void DrainInputs()
        {
            fence.Complete();int drained=0;
            while(count>0&&drained++<16){var frame=requests[head];head=(head+1)&15;count--;AcceptRequest(frame.Member,frame);}
        }
        void AcceptRequest(ulong sender,SharedLabFrame frame)
        {
            if(!IsAuthorityContext||!Slot(sender,out int slot))return;var state=peers[slot];
            if(frame.Epoch!=epoch||frame.MembershipGeneration!=state.Generation||frame.ClientToken!=state.Token)return;
            if(frame.Kind==SharedLabKind.HeldFire){AcceptHeldLab015(slot,frame);return;}
            bool accepted=false;
            if(frame.Kind==SharedLabKind.Tap||frame.Kind==SharedLabKind.Cast)SetHeldLab015(slot,default);
            if(frame.Kind==SharedLabKind.Control)
            {
                if(clock>=state.NextControl&&frame.Revision==revision&&frame.DummyGeneration==dummyGeneration&&frame.TowerGeneration==towerGeneration&&ControlReach(sender))
                {
                    state.NextControl=clock+.25d;peers[slot]=state;
                    var command=(SharedLabControl)frame.Mask;
                    if(command<=SharedLabControl.SpawnPair){StopAutos();}
                    var oldDummy=lab.Dummy;var oldTower=lab.Tower;
                    accepted=lab.ExecuteSharedControl(command,frame.Argument);
                    // Preflight failure can stop attacks; publish that actual accepted state too.
                    revision++;if(!object.ReferenceEquals(oldDummy,lab.Dummy))dummyGeneration++;
                    if(!object.ReferenceEquals(oldTower,lab.Tower))towerGeneration++;
                    state=peers[slot];
                }
            }
            else if(frame.Kind==SharedLabKind.Tap&&SharedLabCastMath.AdmitInput(ref state,clock,false))
            {accepted=SharedLabCastMath.Select(ref state,(int)frame.Argument,clock,frame.Sequence);}
            else if(frame.Kind==SharedLabKind.Auto&&SharedLabCastMath.AdmitInput(ref state,clock,true))
            {state.Auto=frame.Argument;accepted=true;}
            else if(frame.Kind==SharedLabKind.Cast&&clock>=state.NextManual)
            {
                state.NextManual=clock+.08d;peers[slot]=state;
                if(frame.SelectionSequence==state.LastTap)accepted=TryLaunch(slot,false,frame.A);
                state=peers[slot];
            }
            state.LastResult=accepted?0u:1u;peers[slot]=state;
            if(accepted)rooms.RecordAcceptedPeerActivity(sender);
            SendState(0);SendState(1);
        }
        void StopAutos()
        {ResetHeldLab015();var first=peers[0];first.Auto=0;peers[0]=first;var second=peers[1];second.Auto=0;peers[1]=second;for(int i=0;i<shots.Length;i++)shots[i]=default;}
        bool TryLaunch(int slot,bool automatic,Vector3 requested)
        {
            if(!IsAuthorityContext||!lab.SessionActive)return false;
            var state=peers[slot];if(state.Generation==0||!rooms.IsCampaignMemberRegistered(state.Id)||!SharedLabCastMath.Ready(state,clock,automatic)||
                !rooms.TryGetPlayerPose(state.Id,out var feet,out _)||!SharedLabCastMath.Finite(feet))return false;
            Vector3 aim=requested;if(automatic&&!rooms.TryGetPlayerAim(state.Id,out aim))return false;
            if(!SharedLabCastMath.Finite(aim)||aim.sqrMagnitude<.9f||aim.sqrMagnitude>1.1f)return false;
            int mask=(int)state.Mask,element=state.Dominant,combo=ConfirmedSelection.ComboForMask(mask);bool nova=mask==15;
            if(SpellIngredientRules.Count(mask)==3)return TryLaunchThreeLab015(slot,ref state,origin:feet+Vector3.up*1.65f,aim:aim);
            if(shotSerial==int.MaxValue)return false;
            int id=shotSerial+1;if(id<=0)return false;int shotSlot=id%48;
            var prior=shots[shotSlot];if(prior.Id!=0&&prior.Epoch==epoch&&prior.Expires>=clock)return false;
            shotSerial=id;
            Vector3 origin=feet+Vector3.up*1.65f;
            shots[shotSlot]=new ShotRecord{Id=id,Epoch=epoch,Generation=dummyGeneration,Caster=state.Id,CasterGeneration=state.Generation,Origin=origin,Expires=clock+4,Nova=nova?1u:0u,Mask=(uint)mask,Element=(uint)element};
            bool fired=owner.Combat.AutoCastFrom(origin,element,1f,aim,combo,id,nova,0,mask);
            if(!fired){shots[shotSlot]=default;return false;}
            var acceptedShot=shots[shotSlot];acceptedShot.Hit|=2;shots[shotSlot]=acceptedShot;
            SharedLabCastMath.Commit(ref state,clock,true);peers[slot]=state;if(nova)lab.ObserveNovaLaunch();
            Emit(new SharedLabFrame{Kind=SharedLabKind.Shot,Member=state.Id,MembershipGeneration=state.Generation,Argument=(uint)combo,Mask=(uint)mask,Dominant=element,A=origin,B=aim,AttackSequence=id});
            return true;
        }
        bool ControlReach(ulong sender)
        {
            if(!rooms.TryGetPlayerPose(sender,out var feet,out _)||!rooms.TryGetPlayerAim(sender,out var facing)||
                !SharedLabCastMath.Finite(feet)||!SharedLabCastMath.Finite(facing))return false;
            Vector3 eye=feet+Vector3.up*1.65f,ray=owner.World.TrainingLabTowerPosition+Vector3.up*.7f-eye;float length=ray.magnitude;
            if(length<=.001f||length>5.5f||Vector3.Dot(facing,ray/length)<=.82f)return false;
            int hits=Physics.RaycastNonAlloc(eye,ray/length,sight,Mathf.Max(0,length-.12f),~0,QueryTriggerInteraction.Ignore);if(hits==sight.Length)return false;
            Transform dummy=lab.Dummy!=null?lab.Dummy.transform:null,tower=lab.Tower!=null?lab.Tower.transform:null;
            for(int i=0;i<hits;i++)
            {
                var collider=sight[i].collider;if(collider==null)continue;var hit=collider.transform;
                if(owner.Combat.IsPlayerCollider(collider)||dummy!=null&&(hit==dummy||hit.IsChildOf(dummy))||tower!=null&&(hit==tower||hit.IsChildOf(tower)))continue;
                return false;
            }
            return true;
        }
        internal bool PeersClear(Vector3 centre,float radiusSquared)
        {
            if(!IsAuthorityContext)return true;var ids=rooms.ConnectedClientIds;if(ids.Count<1||ids.Count>2)return false;
            return PeerClear(ids[0],centre,radiusSquared)&&(ids.Count==1||PeerClear(ids[1],centre,radiusSquared));
        }
        bool PeerClear(ulong id,Vector3 point,float squared)
        {if(!rooms.TryGetPlayerPose(id,out var feet,out _)||!SharedLabCastMath.Finite(feet))return false;float x=feet.x-point.x,z=feet.z-point.z;return x*x+z*z>=squared;}
        void SendState(int slot)
        {
            if(!IsAuthorityContext)return;var peer=peers[slot];if(peer.Generation==0||peer.Token==0||!rooms.IsCampaignMemberRegistered(peer.Id))return;
            var s=lab.ReadSnapshot();var dummy=lab.Dummy;
            var f=new SharedLabFrame{Version=SharedLabFrame.LabVersion,Kind=SharedLabKind.State,Epoch=epoch,Revision=revision,Sequence=++wireSerial,Member=peer.Id,
                MembershipGeneration=peer.Generation,ClientToken=peer.Token,DummyGeneration=dummyGeneration,TowerGeneration=towerGeneration,
                SelectionSequence=peer.LastTap,Mask=peer.Mask,Dominant=peer.Mask==0?-1:peer.Dominant,Prepared=(int)peer.Pending-1,Auto=peer.Auto,Result=peer.LastResult,
                A=dummy!=null?dummy.transform.position:owner.World.TrainingLabDummyPosition,D=dummy!=null?dummy.transform.eulerAngles:Vector3.zero,
                B=dummy!=null?dummy.LockedAttackDirection:Vector3.forward,C=dummy!=null?dummy.LockedAttackEndpoint:owner.World.TrainingLabTowerPosition,
                Prototype=lab.ActivePrototype,TowerType=lab.ActiveTowerType,Flags=(lab.HasMonster?1u:0u)|(lab.HasTower?2u:0u)|(s.TowerAttacking?4u:0u)|(s.MonsterAttackingTower?8u:0u),
                DummyHP=s.DummyHealth,DummyMax=s.DummyMaximum,TowerHP=s.TowerHealth,TowerMax=s.TowerMaximum,LiveStatus=dummy!=null?dummy.LiveStatus009:0u,
                LastDummy=s.LastDummyDamage,TotalDummy=s.TotalDummyDamage,LastTower=s.LastTowerDamage,TotalTower=s.TotalTowerDamage,TowerDamage=s.TowerDamageToDummy,
                DummyHits=s.DummyHits,TowerHits=s.TowerHits,TowerShots=s.TowerShots,NovaLaunches=s.NovaLaunches,
                R0=Remaining(peer.R0),R1=Remaining(peer.R1),R2=Remaining(peer.R2),R3=Remaining(peer.R3),Primary=Remaining(peer.NextCast),
                Phase=dummy!=null?(int)dummy.AttackPhase:0,Progress=dummy!=null?dummy.AttackProgress:0,AttackSequence=dummy!=null?dummy.AttackPresentationSequence:0,
                AttackStyle=dummy!=null?dummy.AttackPresentationStyle:0,Windup=dummy!=null?dummy.AttackWindupSeconds:0,Strike=dummy!=null?dummy.AttackStrikeSeconds:0,Recovery=dummy!=null?dummy.AttackRecoverySeconds:0};
            if(peer.Id==rooms.LocalClientId){revision=f.Revision;owner.Combat.ApplySharedLabInput(f);return;}
            rooms.SendSharedLab(f,peer.Id);
        }
        float Remaining(double until)=>(float)System.Math.Max(0,System.Math.Min(5,until-clock));
        internal bool TryGetAcceptedShot(int id,out ulong caster,out int ingredientMask)
        {
            caster=0;ingredientMask=0;if(!IsAuthorityContext||id<=0)return false;
            var shot=shots[id%48];if(shot.Id!=id||shot.Epoch!=epoch||shot.Generation!=dummyGeneration||shot.Expires<clock||(shot.Hit&2)==0)return false;
            caster=shot.Caster;ingredientMask=(int)shot.Mask;return true;
        }
        internal void ObserveTowerShot(Vector3 point){if(IsAuthorityContext)Emit(new SharedLabFrame{Kind=SharedLabKind.TowerShot,A=point});}
        void OnHit(int id,Vector3 point)
        {
            if(!IsAuthorityContext||id<=0||!SharedLabCastMath.Finite(point))return;var shot=shots[id%48];
            if(shot.Id!=id||shot.Epoch!=epoch||shot.Generation!=dummyGeneration||shot.Expires<clock||(shot.Hit&2)==0||(shot.Hit&1)!=0)return;
            shot.Hit|=1;shots[id%48]=shot;Emit(new SharedLabFrame{Kind=SharedLabKind.Hit,Member=shot.Caster,A=point});
        }
        void OnNova(int id,Vector3 point,float radius)
        {
            if(!IsAuthorityContext||id<=0||!SharedLabCastMath.Finite(point)||!SharedLabCastMath.Finite(radius)||radius<=0||radius>4.5f)return;var shot=shots[id%48];
            if(shot.Id!=id||shot.Epoch!=epoch||shot.Generation!=dummyGeneration||shot.Expires<clock||shot.Nova!=1||(shot.Hit&2)==0)return;
            shot.Nova=2;shots[id%48]=shot;Emit(new SharedLabFrame{Kind=SharedLabKind.Nova,A=point,Primary=radius});
        }
        void Emit(SharedLabFrame frame)
        {
            if(!IsAuthorityContext||frame.LiveStatus!=0)return;frame.Version=SharedLabFrame.LabVersion;frame.Epoch=epoch;frame.DummyGeneration=dummyGeneration;frame.TowerGeneration=towerGeneration;
            Present(frame);var peer=peers[1];if(peer.Generation==0||peer.Token==0||!rooms.IsCampaignMemberRegistered(peer.Id))return;
            frame.Sequence=++wireSerial;frame.ClientToken=peer.Token;rooms.SendSharedLab(frame,peer.Id);
        }
        void Present(SharedLabFrame frame)
        {
            switch(frame.Kind)
            {
                case SharedLabKind.Shot:
                    if(frame.Dominant<0||frame.Dominant>3||!SpellIngredientRules.ValidProjectileMask((int)frame.Mask)||frame.Argument>6||frame.B.sqrMagnitude<.9f||frame.B.sqrMagnitude>1.1f)return;
                    if(!RecordGuestAcceptedShot(frame))return;
                    owner.Combat.PlayNetworkShotFeedback(frame.Dominant,frame.A,frame.B,(int)frame.Argument,frame.Member==rooms.LocalClientId,frame.Mask==15,frame.AttackSequence);
                    rooms.PresentAcceptedPlayerCast015(frame.Member);break;
                case SharedLabKind.Nova:if(!rooms.IsHost&&frame.Primary>0&&frame.Primary<=4.5f)owner.Combat.PlayCosmicNovaFeedback(frame.A,frame.Primary,1);break;
                case SharedLabKind.TowerShot:if(!rooms.IsHost)owner.Combat.SharedLabTowerFeedback(frame.A);break;
                case SharedLabKind.Impact:PresentAcceptedImpact(frame);break;
                case SharedLabKind.Hit:if(frame.Member==rooms.LocalClientId)owner.Combat.PlayOwnHitConfirmation(frame.A);break;
            }
        }
        void OnDisable(){StopContext();}
        void OnDestroy()
        {
            if(!initialized)return;
            try{StopContext();}
            finally
            {
                if(rooms!=null)rooms.SharedLabMessage-=Receive;
                if(owner!=null&&owner.Combat!=null){owner.Combat.AuthoritativeShotHit-=OnHit;owner.Combat.AuthoritativeNovaImpact-=OnNova;owner.Combat.AuthoritativeSpellImpact-=OnSpellImpact;}
                DisposeBuffers();initialized=false;
            }
        }
        void DisposeBuffers()
        {
            fence.Complete();
            if(peers.IsCreated)peers.Dispose();if(requests.IsCreated)requests.Dispose();if(ready.IsCreated)ready.Dispose();if(shots.IsCreated)shots.Dispose();initialized=false;
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Current supported refuge world metres/Y-up and authenticated accepted NGO pose/aim ledger.
// Array Formats:2 native128B peer records,16 native256B requests,2 readiness bytes,48 bounded shot receipts; existing48 gameplay pool only.
// Dependencies/Pointers: One owner carries/completes jobs before reads/reset/disposal; host sole damage/HP/cadence; guest only validates/applies presentation; no reward, migration, narrator, save or native input bypass.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing actor world healthbar anchor and authenticated host status snapshot.
// Array Formats: Five live bits31 distinct from caster12bit passive mask; fixed status presentation slots,256B lab word224 dedicated.
// Dependencies/Pointers: Existing HP/damage/timer owners unchanged; root composes protocol/version bumps and verifies native/two-peer visuals.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: host accepted World contacts and existing run/epoch/shot identity; no damage/HP/path permission changes.
// Array Formats: existing bounded launch receipts add three-bit landing dedupe; fixed256B shared wire unchanged; new64B arena frame through existing NGO pipeline.
// Dependencies/Pointers: sole host/network/audio owners retain authority, lifetime and gesture/focus policies; actual peer/audio acceptance pending.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: authenticated existing NGO room and Y-up lab world; no new physics or gameplay authority.
// Array Formats: fixed256-byte LabVersion3 frame, word224 LiveStatus only on State; fixed64-byte arena impact event.
// Dependencies/Pointers: exact room tl3/rc1/ls1/bi1 capability gate and v3 lab channel reject older peers; requests and Impact require zero LiveStatus; root native/two-peer acceptance pending.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing accepted world metres/Y-up, input bits and owner simulation seconds.
// Array Formats: Four ingredient deadlines, fixed48 projectile pool, at most three component shots; existing host frame ABIs.
// Dependencies/Pointers: Work-only rapid-casting candidate. Existing host hit/damage/reward owners retained; root owns native/browser acceptance.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Original Oracle accepted world roots and camera/child-local poses only; no authority geometry changes.
// Array Formats: Fixed31shared body slots/2prewarmed player graphs and original palette/pose/feedback buffers; no wire ABI growth.
// Dependencies/Pointers: Player/Rooms/Combat remain sole owners; root integrates default-OFF original native player pilot and verifies real bindings/peer/clearance.
