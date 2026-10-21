using System;
using System.Threading.Tasks;
using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class CoopCampaignDirector
    {
        readonly SoloStageRestartGate soloRestart=new SoloStageRestartGate();
        CampaignSnapshot soloAcceptedBoundary;
        int soloAcceptedRoomOperation;
        ulong soloAcceptedLocalClient;
        string soloRestartNotice="";
        bool soloRestartTaskStarted;
        bool soloRestartUnexpectedLoss;
        int soloExpectedRoomOperation;
        public bool SoloRestartAwaitingResume=>soloRestart.Phase==SoloRestartPhase.AwaitingResume||soloRestart.Phase==SoloRestartPhase.ResumeArmed;
        public bool HasSoloRestartNotice=>soloRestart.HoldsSimulation||soloRestart.Phase==SoloRestartPhase.Complete&&Active&&StageNumber==soloRestart.Ticket.Stage;
        public string SoloRestartNotice=>soloRestartNotice;
        void InitializeSoloRestart()
        {
            owner.BindSoloRestartCampaign(this);
            rooms.UnexpectedSoloRestartPeerDeparture+=OnSoloRestartPeerDeparture;
            rooms.UnexpectedSoloRestartConnectionLoss+=OnSoloRestartConnectionLoss;
        }
        void DisposeSoloRestart()
        {
            if(rooms!=null){rooms.UnexpectedSoloRestartPeerDeparture-=OnSoloRestartPeerDeparture;rooms.UnexpectedSoloRestartConnectionLoss-=OnSoloRestartConnectionLoss;}
            CancelSoloRestart();
        }
        void CacheAcceptedSoloRestartBoundary(CampaignSnapshot accepted)
        {
            if(rooms==null||!rooms.Connected||!CampaignQuestState.ValidSnapshot(accepted,2)||accepted.peers.Length!=2)return;
            soloAcceptedBoundary=accepted;soloAcceptedRoomOperation=rooms.SoloRestartOperation;soloAcceptedLocalClient=rooms.LocalClientId;
        }
        void OnSoloRestartPeerDeparture(ulong id){if(id!=soloAcceptedLocalClient)CaptureSoloRestart(false);}
        void OnSoloRestartConnectionLoss(){CaptureSoloRestart(true);}
        void CaptureSoloRestart(bool unexpectedLoss)
        {
            if(owner==null||!owner.HasStarted||soloRestart.HoldsSimulation||soloAcceptedBoundary==null||
                rooms.SoloRestartOperation!=soloAcceptedRoomOperation||
                (soloAcceptedBoundary.phase!=(int)CampaignPhase.Preparing&&soloAcceptedBoundary.phase!=(int)CampaignPhase.Defense))return;
            if(!owner.Defense.TryCaptureSoloRestartProgress(soloAcceptedBoundary.campaign,soloAcceptedBoundary.stage,soloAcceptedLocalClient,out var progress)||
                !SoloStageRestartTicket.TryCreate(Guid.NewGuid().ToString("N"),soloAcceptedBoundary,soloAcceptedLocalClient,soloAcceptedRoomOperation,progress,out var ticket)||
                !soloRestart.Begin(ticket,Time.realtimeSinceStartupAsDouble)){
                owner.Defense.FreezeForSoloRestart();FailSoloRestart("No complete accepted stage/upgrade state was available. No ranks, rewards or saved wallet were invented. Return to the menu to reconnect.");return;
            }
            soloRestartUnexpectedLoss=unexpectedLoss;
            owner.SetSoloRestartHold(true);owner.CombatUnlocked=false;owner.Defense.FreezeForSoloRestart();
            soloRestartNotice="SOLO RECOVERY - STAGE "+ticket.Stage+"\nAccepted progress/upgrades kept. No pickup rewards this stage.\nClosing the old room safely...";
            owner.Objective=soloRestartNotice;StateChanged?.Invoke();
        }
        bool TickSoloRestart()
        {
            if(!owner.HasStarted){
                if(soloRestart.Phase!=SoloRestartPhase.Idle||soloRestartTaskStarted||soloExpectedRoomOperation!=0||soloAcceptedBoundary!=null)CancelSoloRestart();
                return false;
            }
            if(!soloRestart.HoldsSimulation)return false;
            if(soloExpectedRoomOperation!=0&&rooms.SoloRestartOperation!=soloExpectedRoomOperation){CancelSoloRestart(false);return false;}
            if(soloRestart.TimedOut(Time.realtimeSinceStartupAsDouble))FailSoloRestart("Recovery timed out. The stage remains paused; return to the menu to reconnect safely.");
            if(soloRestart.Phase==SoloRestartPhase.Captured&&!soloRestartTaskStarted){soloRestartTaskStarted=true;_ = RunSoloRestartAsync(soloRestart.Generation);}
            owner.Objective=soloRestartNotice;return true;
        }
        async Task RunSoloRestartAsync(int generation)
        {
            try{
                Advance(generation,SoloRestartPhase.Captured,SoloRestartPhase.Leaving,30);
                if(!rooms.TryBeginOrReuseSoloRestartLeave(soloRestart.Ticket,soloRestartUnexpectedLoss,out Task<string> leave)){CancelSoloRestart(false);return;}
                soloExpectedRoomOperation=rooms.SoloRestartOperation;
                if(soloExpectedRoomOperation!=soloRestart.Ticket.SourceRoomOperation+1){CancelSoloRestart(false);throw new OperationCanceledException();}
                while(!leave.IsCompleted){Ensure(generation);await Awaitable.NextFrameAsync();}
                string cleanup=await leave;Ensure(generation);
                if(cleanup!=null)throw new InvalidOperationException("Old room cleanup failed: "+cleanup);
                while(!rooms.SoloRestartTransportQuiescent){Ensure(generation);await Awaitable.NextFrameAsync();}
                // Hub admission requires the existing nonarena World. No campaign/tutorial start.
                CancelCampaign();owner.World.Build();owner.Player.Teleport(owner.World.PlayerSpawn,0f);owner.Combat.ResetEncounter();owner.Builder.ClearPlacedShrines();
                Advance(generation,SoloRestartPhase.Leaving,SoloRestartPhase.Connecting,90);
                rooms.CreateSoloRestartRoom();
                soloExpectedRoomOperation=rooms.SoloRestartOperation;
                while(!rooms.Connected){Ensure(generation);if(!rooms.Busy)throw new InvalidOperationException(rooms.Status);await Awaitable.NextFrameAsync();}
                while(!rooms.CampaignRosterSettled){Ensure(generation);if(!rooms.Connected)throw new InvalidOperationException("The solo admission closed before it settled.");await Awaitable.NextFrameAsync();}
                Ensure(generation);
                if(!rooms.IsHost||rooms.PlayerCount!=1||!rooms.TryGetAdmissionSlot(rooms.LocalClientId,out int slot)||slot!=0)
                    throw new InvalidOperationException("A genuine sole admitted host at slot0 is required.");
                Advance(generation,SoloRestartPhase.Connecting,SoloRestartPhase.Preparing,120);
                if(!model.BeginRecoveredSoloStage(Guid.NewGuid().ToString("N"),soloRestart.Ticket)||!owner.Defense.QueueSoloRestartSeed(soloRestart,generation))
                    throw new InvalidOperationException("Accepted recovery state could not seed the new stage.");
                Publish();
                while(!owner.Defense.ArenaReady||owner.Loading.IsLoading){Ensure(generation);await Awaitable.NextFrameAsync();}
                Ensure(generation);
                if(!model.BeginDefense(owner.Defense.CurrentRunId))throw new InvalidOperationException("Fresh stage authority did not reach its ready barrier.");
                Publish();Advance(generation,SoloRestartPhase.Preparing,SoloRestartPhase.AwaitingResume,180);
                soloRestartNotice="SOLO STAGE "+StageNumber+" READY - PRESS RESUME\nStarts at the beginning; accepted progress/upgrades kept.\nNo pickup rewards this stage. Rewards return next stage.";
                owner.SetSoloRestartHold(true);owner.Objective=soloRestartNotice;StateChanged?.Invoke();
            }catch(OperationCanceledException){if(generation==soloRestart.Generation&&soloRestart.HoldsSimulation&&soloRestart.Phase!=SoloRestartPhase.Failed)CancelSoloRestart(false);}
            catch(Exception error){if(generation==soloRestart.Generation)FailSoloRestart(error.Message);}
        }
        void Advance(int generation,SoloRestartPhase expected,SoloRestartPhase next,double seconds)
        {
            if(!soloRestart.Advance(generation,expected,next,Time.realtimeSinceStartupAsDouble,seconds)){
                if(generation!=soloRestart.Generation)throw new OperationCanceledException("Stale recovery owner.");
                throw new InvalidOperationException("The stage recovery missed its expected safe lifecycle barrier.");
            }
            if(next==SoloRestartPhase.Connecting||next==SoloRestartPhase.Preparing){
                soloRestartNotice="SOLO RECOVERY - STAGE "+soloRestart.Ticket.Stage+"\nAccepted progress/upgrades kept. No pickup rewards this stage.\n"+
                    (next==SoloRestartPhase.Connecting?"Connecting a fresh solo room...":"Preparing the stage from its beginning...");
                owner.Objective=soloRestartNotice;StateChanged?.Invoke();
            }
        }
        void Ensure(int generation)
        {
            if(this==null||owner==null||!owner.HasStarted||generation!=soloRestart.Generation||soloRestart.Phase==SoloRestartPhase.Failed)throw new OperationCanceledException();
            if(soloExpectedRoomOperation!=0&&rooms.SoloRestartOperation!=soloExpectedRoomOperation){CancelSoloRestart(false);throw new OperationCanceledException();}
            if(soloRestart.TimedOut(Time.realtimeSinceStartupAsDouble))throw new TimeoutException("Recovery lifecycle timed out before a safe authority was ready.");
        }
        void FailSoloRestart(string reason)
        {
            bool closeNew=soloRestart.Phase==SoloRestartPhase.Connecting||soloRestart.Phase==SoloRestartPhase.Preparing;
            soloRestart.Fail();
            if(closeNew&&rooms!=null){rooms.CancelSoloRestartConnection(soloExpectedRoomOperation);soloExpectedRoomOperation=0;}
            if(reason==null)reason="The owned room did not reach its safe ready barrier.";
            if(reason.Length>110)reason=reason.Substring(0,107)+"...";
            soloRestartNotice="SOLO RECOVERY STOPPED SAFELY\n"+reason+"\nStage paused. Return to the menu to reconnect.";owner.SetSoloRestartHold(true);owner.CombatUnlocked=false;owner.Objective=soloRestartNotice;StateChanged?.Invoke();
        }
        void CancelSoloRestart(bool leaveOwnedConnection=true)
        {
            bool ownsNew=soloRestart.Phase>=SoloRestartPhase.Connecting&&soloRestart.Phase<=SoloRestartPhase.ResumeArmed;
            int previousOperation=soloExpectedRoomOperation;
            soloRestart.Cancel();soloRestartTaskStarted=soloRestartUnexpectedLoss=false;soloAcceptedBoundary=null;
            soloExpectedRoomOperation=0;
            if(rooms!=null)rooms.CancelSoloRestartConnection(previousOperation,ownsNew&&leaveOwnedConnection);
            if(owner!=null)owner.SetSoloRestartHold(false);
        }
        internal bool TryArmSoloRestartResume()=>soloRestart.ArmResume();
        internal bool CompleteSoloRestartResume()
        {
            if(!soloRestart.CompleteResume())return false;
            owner.CombatUnlocked=true;soloRestartNotice="SOLO STAGE "+StageNumber+" RESTARTED\nProgress/upgrades kept. No pickup rewards this stage.\nNormal rewards return next stage.";owner.Objective=soloRestartNotice;StateChanged?.Invoke();return true;
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: accepted two-peer campaign boundary -> fenced new sole-host campaign/current stage; existing rotated world lane policy.
// Array Formats: one cold restart gate/ticket and existing accepted snapshot reference; per-frame tick uses scalar state only.
// Dependencies/Pointers: existing owned LeaveRoomAsync/manager fence precedes new Relay room; source view captured before cancel; existing loading barrier and explicit native/browser Resume gesture retain control. No actor/economy replay or automatic host migration claim.
