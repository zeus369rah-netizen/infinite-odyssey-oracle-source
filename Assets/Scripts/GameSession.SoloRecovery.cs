namespace OdysseyOracle
{
    public sealed partial class GameSession
    {
        bool soloRestartSimulationHold;
        internal bool SoloRestartHoldsSimulation=>soloRestartSimulationHold;
        CoopCampaignDirector soloRestartCampaign;
        internal void BindSoloRestartCampaign(CoopCampaignDirector campaign){soloRestartCampaign=campaign;}
        internal void SetSoloRestartHold(bool hold){soloRestartSimulationHold=hold;if(hold)Pause();}
        internal bool TryArmSoloRestartResume()=>!soloRestartSimulationHold||soloRestartCampaign!=null&&soloRestartCampaign.TryArmSoloRestartResume();
        internal void CompleteSoloRestartResume(){if(soloRestartSimulationHold&&soloRestartCampaign!=null&&soloRestartCampaign.CompleteSoloRestartResume())soloRestartSimulationHold=false;}
        internal void BeginSoloRestartConnection()
        {
            Loading.Cancel();Revival.ResetState();CoopMode=true;World.Build();Combat.ResetEncounter();
            if(Rooms==null||!Rooms.Connected||!Rooms.IsHost||Rooms.PlayerCount!=1||!Rooms.TryGetLocalHubSpawn(out var feet,out float yaw))
                throw new System.InvalidOperationException("Accepted sole recovery admission is not ready.");
            Rooms.CommitLocalHubSpawn();
            Player.Teleport(feet,yaw);
            HasStarted=true;CombatUnlocked=false;HP=MaxHP;Victory=false;FailureReason="";outcomeHandled=false;
            Tutorial.EndTutorial();Opening.EnterGameplayDirect();Hud.ClosePanels();
            // Async connection completion never calls Resume or requests pointer lock.
            SetSoloRestartHold(true);Audio.SetGameplayMix(true);
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: existing accepted hub admission followed by normal direct stage loading; no tutorial/campfire restart.
// Array Formats: scalar hold and borrowed campaign pointer; no active allocations or new runtime owner.
// Dependencies/Pointers: ordinary Resume/browser pointer-capture completion releases hold only after visible gesture; host SimulationActive must also honor this hold; town saves untouched.
