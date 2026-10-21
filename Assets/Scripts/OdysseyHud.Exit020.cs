using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class OdysseyHud
    {
        JourneyPanel exitReturn020=JourneyPanel.Pause;
        UnityEngine.UI.Button exitLobby020,exitGame020,exitCancel020;
        UnityEngine.UI.Text exitStatus020;
        uint shownExitRevision020=uint.MaxValue;
        void BuildExitPanel020()
        {
            var content=PanelContent(JourneyPanel.Exit020,"ExitGamePanel020",new Vector2(620,360));
            exitLobby020=MenuButton(content,"ExitBackToLobby020","BACK TO LOBBY",()=>{if(!session.ExitPending020)ReturnToMainMenu();},54);
            exitLobby020.targetGraphic.color=new Color(.12f,.44f,.25f);
            exitGame020=MenuButton(content,"ExitGame020","EXIT GAME",()=>session.ExitGame020(),54);
            exitGame020.targetGraphic.color=new Color(.5f,.10f,.09f);
            exitCancel020=MenuButton(content,"ExitCancel020","CANCEL [ESC]",CancelExitPanel020,44);
            exitStatus020=MenuLabel(content,"ExitStatus020","",15,Muted,44);
        }
        void ShowExitPanel020()
        {
            if(!built||session==null||IntroActive||LoadingActive||session.ExitPending020)return;
            exitReturn020=currentPanel;session.Pause();shownExitRevision020=uint.MaxValue;SetPanel(JourneyPanel.Exit020);
            RefreshExitPanel020();
        }
        void CancelExitPanel020()
        {
            if(session==null||session.ExitPending020)return;
            SetPanel(exitReturn020);
        }
        void RefreshExitPanel020()
        {
            if(currentPanel!=JourneyPanel.Exit020||session==null||exitStatus020==null||shownExitRevision020==session.ExitRevision020)return;
            shownExitRevision020=session.ExitRevision020;bool ready=!session.ExitPending020;
            exitLobby020.interactable=exitGame020.interactable=exitCancel020.interactable=ready;
            exitStatus020.text=ready?session.ExitError020:"Leaving the game...";
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing fitted Canvas exit modal and menu ancestry; no world/player manipulation.
// Array Formats: Three cold buttons, one label, original parent panel and one cached revision; no frame-built strings.
// Dependencies/Pointers: Lobby delegates to existing full Restart/leave/refuge cleanup. Exit delegates to one room-fenced command; Cancel preserves the current session. Root accepts actual layout/colors/input/lifecycle/browser behavior.
