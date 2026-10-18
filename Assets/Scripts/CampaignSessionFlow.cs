using System;
using UnityEngine;

namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // Cold phase boundaries only. Reuses root's actual PrepareSharedArena action,
    // ArenaPreloader and public session objects; never owns room/service lifetime.
    // No coroutine can request pointer lock. The visible Resume gesture owns it.
    public sealed class CampaignSessionFlow
    {
        readonly GameSession session;
        readonly CoopCampaignDirector campaign;
        readonly Action prepareArena;
        int preparedStage;
        string preparedCampaign;

        public CampaignSessionFlow(GameSession session,CoopCampaignDirector campaign,Action prepareArena)
        {
            this.session=session??throw new ArgumentNullException(nameof(session));
            this.campaign=campaign??throw new ArgumentNullException(nameof(campaign));
            this.prepareArena=prepareArena??throw new ArgumentNullException(nameof(prepareArena));
        }
        public void EnterRefuge(bool newCampaign)
        {
            session.Loading.Cancel();
            session.Revival.ResetState();
            session.Victory=false;
            session.World.Build();
            session.Combat.ResetEncounter();
            session.Builder.ClearPlacedShrines();
            session.Progression.ResetNetworkXp();
            if(!session.Rooms.TryGetAdmissionSlot(session.Rooms.LocalClientId,out int slot))
                throw new InvalidOperationException("The accepted room admission slot is required before refuge entry.");
            session.Player.Teleport(CampaignRefugeSpawns.ForAdmission(session.World.PlayerSpawn,slot),0f);
            session.HP=session.MaxHP;session.Wave=campaign.StageNumber;
            session.HasStarted=true;session.CombatUnlocked=false;
            session.Tutorial.EndTutorial();session.World.UnlockPortal();session.Hud.ClosePanels();
            session.Defense.EnterCampaignRefuge(campaign.Context,newCampaign);
            session.Graphics?.RefreshWorldSettings();
            session.Audio?.SetPortalPosition(session.World.PortalPosition+Vector3.up*1.4f);
            session.Audio?.PlayCue("portal_travel");
            session.Objective=campaign.Status;
            PauseForGesture();
        }
        public void PrepareArena()
        {
            if(preparedCampaign==campaign.CampaignId&&preparedStage==campaign.StageNumber)return;
            preparedCampaign=campaign.CampaignId;preparedStage=campaign.StageNumber;
            int stage=preparedStage;string id=preparedCampaign;
            session.Loading.Begin(prepareArena,()=>
            {
                if(campaign.CampaignId!=id||campaign.StageNumber!=stage||campaign.Phase!=CampaignPhase.Preparing)return;
                session.Defense.StartCampaignStage();
                if(!session.Defense.Running){campaign.ReportPreparationFailure(session.Defense.Status);return;}
                session.Loading.FinishWhenReady(()=>campaign.CampaignId==id&&campaign.StageNumber==stage&&
                    session.Defense.ArenaReady);
            });
        }
        public void AbortPreparation()
        {preparedStage=0;preparedCampaign=null;EnterRefuge(false);}
        public void StageClear()
        {
            session.Defense.HaltCampaignStageView();
            session.Loading.Cancel();session.Revival.ResetState();
            session.CombatUnlocked=false;session.Victory=false;session.HP=session.MaxHP;
            session.Hud.ClosePanels();session.Objective=campaign.Status;PauseForGesture();
        }
        public void Ending(bool completed)
        {
            session.Defense.HaltCampaignStageView();session.Revival.ResetState();
            session.Loading.Cancel();session.CombatUnlocked=false;
            session.Victory=completed;session.Objective=campaign.Status;
            session.Pause();
        }
        static void ReleasePointer()
        {Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
        void PauseForGesture(){session.Paused=true;ReleasePointer();}
    }
}
