using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class GameSession
    {
        bool campaignHubEntry020,campaignHubRoutingBound020;
        uint campaignHubAcceptedGeneration020,campaignHubAcceptedRevision020,campaignHubPreparedGeneration020;
        partial void TickNibwick020();
        partial void RetireNibwick020();
        partial void TickCampaignHubAtmosphere020();
        partial void RetireCampaignHubAtmosphere020();
        partial void CampaignHubArrivalAccepted020(uint generation,uint revision);
        public bool CampaignHubActive020=>campaignHubEntry020&&World!=null&&World.CampaignHubActive020&&
            World.CampaignHubGeneration020==campaignHubAcceptedGeneration020&&World.WorldRevision020==campaignHubAcceptedRevision020;
        public bool WorldControlReady020=>World!=null&&World.WorldPublished020&&WorldModeRules020.ControlReady(World.Mode020,CampaignHubActive020);
        public bool LegacyCombatWorld020=>World!=null&&World.WorldPublished020&&WorldModeRules020.LegacyCombat(World.Mode020);

        void InitializeCampaignHubRouting020()
        {
            if(campaignHubRoutingBound020||Hud==null)return;
            Hud.CampaignIntroFinished020+=CampaignPrologueFinished020;campaignHubRoutingBound020=true;
        }
        void DisposeCampaignHubRouting020()
        {
            RetireNibwick020();RetireCampaignHubAtmosphere020();
            if(campaignHubRoutingBound020&&Hud!=null)Hud.CampaignIntroFinished020-=CampaignPrologueFinished020;
            campaignHubRoutingBound020=false;campaignHubEntry020=false;campaignHubAcceptedGeneration020=campaignHubAcceptedRevision020=0;
        }
        void CampaignPrologueFinished020(CampaignBookResult020 result)
        {
            if(result==CampaignBookResult020.Complete||result==CampaignBookResult020.Skipped)TryStartCampaignHub020();
        }
        public bool PrepareCampaignHub020()
        {
            if(!CampaignHubEntryAllowed020||World==null)return false;
            uint generation=World.PortalLandingPrototypeGeneration020;
            if(!World.PortalLandingPrototypeReady020)
            {
                if(generation<campaignHubPreparedGeneration020)generation=campaignHubPreparedGeneration020;
                if(generation==uint.MaxValue)return false;
                generation++;campaignHubPreparedGeneration020=generation;
                if(!World.PreparePortalLandingPrototype020(generation))return false;
            }
            return World.TryGetPortalLandingPrototype020(World.PortalLandingPrototypeGeneration020,out PortalLandingWorldSnapshot020 snapshot)&&snapshot.Root!=null&&snapshot.Generation!=0;
        }
        bool CampaignHubEntryAllowed020=>WorldModeRules020.CanEnterHub(HasStarted,Rooms!=null&&Rooms.Connected,Rooms!=null&&Rooms.Busy,
            CampaignBootstrap.IsTerminal(this),Loading!=null&&Loading.IsLoading,Opening!=null&&(Opening.IsIntroPlaying||Opening.IsTransitioning),
            isActiveAndEnabled&&World!=null&&Player!=null&&Hud!=null&&Audio!=null&&Tutorial!=null&&Combat!=null&&Defense!=null&&Builder!=null&&Revival!=null&&Opening!=null);
        public bool CanEnterPreparedCampaignHub020=>CampaignHubEntryAllowed020&&World.PortalLandingPrototypeReady020&&World.PortalLandingPrototypeGeneration020!=0;
        public bool TryStartCampaignHub020()
        {
            if(!CampaignHubEntryAllowed020||!PrepareCampaignHub020())return false;
            var admission=new LandingAdmission020{Kind=PortalLandingLayout020.ContextKind,Generation=World.PortalLandingPrototypeGeneration020,StoryRevision=1,Ready=1,Authority=0,UnlockedEndpoints=0};
            Loading.Cancel();Revival.ResetState();Tutorial.ResetTutorial();TrainingLab?.EndSession();SharedTrainingLab?.StopContext();
            Defense.ResetDefense();Builder.ResetBuildings();Combat.ResetEncounter();RefugeActivities?.StopMechanicsGuide();RefugeActivities?.StopNpcGreeting009(null);
            if(!World.CommitCampaignHub020(admission))return false;
            campaignHubEntry020=true;campaignHubAcceptedGeneration020=admission.Generation;campaignHubAcceptedRevision020=World.WorldRevision020;
            Combat.SetLegacyWorldPresentation020(false);if(breakableWall!=null)breakableWall.gameObject.SetActive(false);
            CoopMode=false;Victory=false;outcomeHandled=false;pendingCoopStart=portalPrompted=false;
            HP=MaxHP;Defeated=Goal=0;CombatUnlocked=false;FailureReason="";
            Player.Teleport(World.PlayerSpawn,World.CampaignHubSpawnYaw020);HasStarted=true;
            Opening.EnterGameplayDirect();Hud.ClosePanels();Objective="Portal Landing · Explore the arrival island.";
            Graphics?.RefreshWorldSettings();Audio.SetGameplayMix(true);Audio.StartAudioFromGesture();Resume();
            CampaignHubArrivalAccepted020(admission.Generation,World.WorldRevision020);return true;
        }
        void TickCampaignHubMode020()
        {
            if(World==null||World.Mode020!=WorldMode020.CampaignHub){RetireNibwick020();RetireCampaignHubAtmosphere020();return;}
            if(!CampaignHubActive020)
            {RetireNibwick020();RetireCampaignHubAtmosphere020();if(HasStarted){Objective="Portal Landing is unavailable. Return to the main menu.";if(!Paused)Pause();}return;}
            TickNibwick020();TickCampaignHubAtmosphere020();
        }
        void ResetCampaignHubSession020(){RetireNibwick020();RetireCampaignHubAtmosphere020();campaignHubEntry020=false;campaignHubAcceptedGeneration020=campaignHubAcceptedRevision020=0;}
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Actual accepted offline main-menu/prologue -> supported world spawn and existing first-person camera/control owner.
// Array Formats: One session entry flag/generation/subscription and builder value admission; no new progression, network or quest records.
// Dependencies/Pointers: Missing/busy/stale/not-ready prototype fails closed. Intro/Skip grants no bridge authority; existing pointer Resume owns native/browser capture. Root accepts actual geometry/NPC/atmosphere/native/browser behavior before launch.
