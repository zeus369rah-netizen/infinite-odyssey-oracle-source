using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class OdysseyHud
    {
        const string GameWaitMessage020="Please wait while the game loads. This may take a moment.";
        const string CampaignTownPreparing020="Shared Campaign town is preparing.";
        const string QuickHostPreparing020="Quick Host is preparing.";
        UnityEngine.UI.Text roomModeTitle020,roomCampaignNotice020,roomWait020;
        UnityEngine.UI.Text roomCampaignModeLabel020,roomDefenseModeLabel020;
        UnityEngine.UI.Text roomHostRetryLabel020,roomJoinRetryLabel020;
        UnityEngine.UI.Button roomCancel020,loadingCancel020,roomQuickHost020;
        UnityEngine.UI.Button roomCampaignMode020,roomDefenseMode020;
        CoopRoomMode020 roomMenuMode020=CoopRoomMode020.TowerDefense;
        bool roomSoloContext020,roomRosterReady020,roomRosterOnline020;
        bool roomRetryReady020,roomRetryFailed020;
        string roomRosterCode020;
        int roomRosterCount020=-1,roomRosterCapacity020=-1;
        void BuildPlayModePanels020()
        {
            var content=PanelContent(JourneyPanel.Play020,"StartGameModes020",new Vector2(700,420));
            MenuLabel(content,"PlayModeHeading020","START GAME",30,Ivory,64);
            MenuButton(content,"ChooseSolo020","SOLO",()=>SetPanel(JourneyPanel.Solo020),54);
            MenuButton(content,"ChooseMultiplayer020","MULTIPLAYER",OpenMultiplayer020,54);
            MenuButton(content,"PlayModesBack020","BACK",BackMenu015,44);
            content=PanelContent(JourneyPanel.Solo020,"SoloGameModes020",new Vector2(760,650));
            MenuLabel(content,"SoloModeHeading020","SOLO",30,Ivory,64);
            MenuButton(content,"SoloTowerDefense020","TOWER DEFENSE",StartSoloTowerDefense020,54);
            BuildCampaignEntry020(content);
            var quick=BuildRoomButtonRow020(content,"SoloQuickHostModes020",44);
            RoomRowButton020(quick,"SoloQuickHostDefense020","QUICK HOST · DEFENSE",()=>OpenQuickHost020(CoopRoomMode020.TowerDefense),44).interactable=false;
            RoomRowButton020(quick,"SoloQuickHostCampaign020","QUICK HOST · CAMPAIGN",()=>OpenQuickHost020(CoopRoomMode020.CampaignTown),44).interactable=false;
            MenuLabel(content,"SoloQuickHostPreparing020",QuickHostPreparing020,17,Muted,32);
            MenuButton(content,"SoloModesBack020","BACK",BackMenu015,44);
        }
        void OpenPlayModes020()
        {
            if(session==null||session.HasStarted||IntroActive||LoadingActive)return;
            session.Audio?.StartAudioFromGesture();SetPanel(JourneyPanel.Play020);
        }
        void OpenMultiplayer020()
        {
            if(session==null||IntroActive||LoadingActive||CampaignHubHudMode020)return;
            session.Audio?.StartAudioFromGesture();localRoomMessage="";SetRoomMode020(false);SetPanel(JourneyPanel.Choice);
            if(roomCodeInput!=null){roomCodeInput.Select();roomCodeInput.ActivateInputField();}
        }
        void StartSoloTowerDefense020()
        {
            if(session==null||session.HasStarted||IntroActive||LoadingActive||session.Rooms==null||session.Rooms.Busy||session.Rooms.Connected)return;
            session.Audio?.StartAudioFromGesture();localRoomMessage="";SetRoomMode020(true);
            session.StartSoloDefense();SetPanel(JourneyPanel.Choice);
        }
        void SetRoomMode020(bool solo)
        {
            roomSoloContext020=solo;roomMenuMode020=CoopRoomMode020.TowerDefense;
            RefreshRoomModeCopy020();
        }
        void SelectRoomMenuMode020(CoopRoomMode020 mode)
        {
            if(session==null||IntroActive||LoadingActive||CampaignHubHudMode020||session.Rooms!=null&&(session.Rooms.Busy||session.Rooms.Connected))return;
            if(mode!=CoopRoomMode020.CampaignTown&&mode!=CoopRoomMode020.TowerDefense)return;
            roomSoloContext020=false;roomMenuMode020=mode;localRoomMessage="";RefreshRoomModeCopy020();
        }
        void RefreshRoomModeCopy020()
        {
            bool campaign=roomMenuMode020==CoopRoomMode020.CampaignTown;
            if(roomModeTitle020!=null)roomModeTitle020.text=roomSoloContext020?"SOLO · TOWER DEFENSE":campaign?"MULTIPLAYER · CAMPAIGN":"MULTIPLAYER · TOWER DEFENSE";
            if(roomCampaignNotice020!=null){roomCampaignNotice020.text=CampaignTownPreparing020;roomCampaignNotice020.gameObject.SetActive(campaign&&(session?.Rooms==null||!session.Rooms.SupportsRoomMode020(roomMenuMode020)));}
            if(roomCampaignModeLabel020!=null)roomCampaignModeLabel020.color=campaign?Gold:Muted;
            if(roomDefenseModeLabel020!=null)roomDefenseModeLabel020.color=campaign?Muted:Gold;
        }
        RectTransform BuildRoomButtonRow020(RectTransform content,string name,float height)
        {
            var row=Rect(content,name,Vector2.zero,new Vector2(.5f,.5f),Vector2.zero,new Vector2(0,height));Height(row,height);
            var layout=row.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();layout.spacing=14;
            layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandHeight=true;layout.childForceExpandWidth=false;return row;
        }
        UnityEngine.UI.Button RoomRowButton020(RectTransform row,string name,string text,UnityEngine.Events.UnityAction action,float height)
        {
            var button=MenuButton(row,name,text,action,height);var size=button.GetComponent<UnityEngine.UI.LayoutElement>();size.minWidth=0;size.flexibleWidth=1;return button;
        }
        void BuildRoomModeChoices020(RectTransform content)
        {
            var row=BuildRoomButtonRow020(content,"MultiplayerModeChoices020",52);
            roomCampaignMode020=RoomRowButton020(row,"MultiplayerCampaign020","CAMPAIGN · UP TO 2",()=>SelectRoomMenuMode020(CoopRoomMode020.CampaignTown),52);
            roomDefenseMode020=RoomRowButton020(row,"MultiplayerDefense020","TOWER DEFENSE",()=>SelectRoomMenuMode020(CoopRoomMode020.TowerDefense),52);
            roomCampaignModeLabel020=roomCampaignMode020.GetComponentInChildren<UnityEngine.UI.Text>();roomDefenseModeLabel020=roomDefenseMode020.GetComponentInChildren<UnityEngine.UI.Text>();
        }
        bool RoomMenuRequestAllowed020()
        {
            return session!=null&&!IntroActive&&!LoadingActive&&!CampaignHubHudMode020&&session.Rooms!=null;
        }
        void HostSelectedRoom020()
        {
            if(!RoomMenuRequestAllowed020())return;var rooms=session.Rooms;
            if(!rooms.SupportsRoomMode020(roomMenuMode020)){localRoomMessage=CampaignTownPreparing020;return;}
            if(!rooms.CanHostRoom020(roomMenuMode020)){localRoomMessage=rooms.RoomStatus020;return;}
            session.Audio?.StartAudioFromGesture();localRoomMessage="";rooms.CreateRoom(roomMenuMode020);
        }
        void JoinSelectedRoom020()
        {
            if(!RoomMenuRequestAllowed020())return;var rooms=session.Rooms;
            if(!rooms.SupportsRoomMode020(roomMenuMode020)){localRoomMessage=CampaignTownPreparing020;return;}
            if(!rooms.CanJoinRoom020(roomMenuMode020)){localRoomMessage=rooms.RoomStatus020;return;}
            var code=roomCodeInput.text.Trim().ToUpperInvariant();if(string.IsNullOrEmpty(code)){localRoomMessage="Enter a room code to join.";return;}
            session.Audio?.StartAudioFromGesture();localRoomMessage="";rooms.JoinRoom(code,roomMenuMode020);
        }
        void OpenQuickHost020(CoopRoomMode020 mode)
        {
            if(session==null||IntroActive||LoadingActive||CampaignHubHudMode020||session.Rooms!=null&&(session.Rooms.Busy||session.Rooms.Connected))return;
            session.Audio?.StartAudioFromGesture();SetRoomMode020(false);SelectRoomMenuMode020(mode);SetPanel(JourneyPanel.Choice);
            localRoomMessage=mode==CoopRoomMode020.CampaignTown?CampaignTownPreparing020:QuickHostPreparing020;
        }
        void QuickHostSelectedRoom020(){OpenQuickHost020(roomMenuMode020);}
        void RefreshRoomRoster020(bool online,string code,int count,int capacity)
        {
            if(roomRosterReady020&&roomRosterOnline020==online&&roomRosterCode020==code&&roomRosterCount020==count&&roomRosterCapacity020==capacity)return;
            roomRosterReady020=true;roomRosterOnline020=online;roomRosterCode020=code;roomRosterCount020=count;roomRosterCapacity020=capacity;
            string text=online?"ROOM  "+code+"\n"+count+" / "+capacity+" ONLINE":"";
            if(roomRoster!=null)roomRoster.text=text;if(roomHudStatus!=null)roomHudStatus.text=text;
        }
        void RefreshRoomRetry020(bool failed)
        {
            if(roomRetryReady020&&roomRetryFailed020==failed)return;roomRetryReady020=true;roomRetryFailed020=failed;
            if(roomHostRetryLabel020!=null)roomHostRetryLabel020.text=failed?"RETRY HOST":"HOST";
            if(roomJoinRetryLabel020!=null)roomJoinRetryLabel020.text=failed?"RETRY JOIN":"JOIN";
        }
        void BuildRoomWait020(RectTransform content)
        {
            roomWait020=MenuLabel(content,"FriendlyActualConnectionWait020",GameWaitMessage020,17,Muted,44);
            roomCancel020=MenuButton(content,"CancelActualConnection020","CANCEL",CancelGameConnection020,40);
            roomWait020.gameObject.SetActive(false);roomCancel020.gameObject.SetActive(false);
        }
        void RefreshGameWait020(bool busy)
        {
            if(roomWait020!=null&&roomWait020.gameObject.activeSelf!=busy)roomWait020.gameObject.SetActive(busy);
            if(roomCancel020!=null&&roomCancel020.gameObject.activeSelf!=busy)roomCancel020.gameObject.SetActive(busy);
        }
        void CancelGameConnection020(){if(session?.Rooms!=null&&session.Rooms.Busy)session.Rooms.LeaveRoom();}
        void CancelGameLoading020(){if(LoadingActive)ReturnToMainMenu();}
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing retained Canvas cards, real connection state and menu ancestry; no world/network authority.
// Array Formats: Explicit finite core mode identity, cold UI controls and cached connected/code/count/capacity scalars; no unchanged-frame text formatting.
// Dependencies/Pointers: Typed Phase1 RoomConnection admission owns Host/Join and actual Online/share code. Campaign and Quick Host stay closed until supported core ownership exists; Quick Host does not call Solo/room/ready/wave APIs. Root alone integrates and accepts native/browser behavior.
