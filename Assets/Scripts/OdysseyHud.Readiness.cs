using UnityEngine;
using UnityEngine.UI;

namespace OdysseyOracle
{
    public sealed partial class OdysseyHud
    {
        RectTransform readinessHudRoot;Text readinessCountText,readinessPhaseText,readinessLevelText;
        Image readinessKeycap;bool readinessFooterFits;int readinessColorMode=-1;
        float ReadinessFooterExtraHeight=>readinessFooterFits?ReadinessHudMath.FooterHeight+ReadinessHudMath.Gap:0;
        void BuildReadinessHud()
        {
            ReadinessHudLabels.Warm();
            readinessHudRoot=Frame(minimapRoot,"AcceptedPartyReadiness",Vector2.zero,new Vector2(0,1),new Vector2(0,-ReadinessHudMath.Gap),new Vector2(229,ReadinessHudMath.FooterHeight),Panel,MutedGold);
            var pass=readinessHudRoot.gameObject.AddComponent<CanvasGroup>();pass.blocksRaycasts=false;pass.interactable=false;
            var key=Rect(readinessHudRoot,"ReadyCountKeycap",new Vector2(0,1),new Vector2(0,1),new Vector2(8,-7),new Vector2(107,27));
            readinessKeycap=Paint(key,new Color(.07f,.10f,.09f,.95f));readinessKeycap.raycastTarget=false;Border(key,MutedGold,1);
            readinessCountText=Label(key,"AcceptedReadyCount","READY —/2",14,Gold,TextAnchor.MiddleCenter,Vector2.zero,new Vector2(103,25),new Vector2(.5f,.5f));readinessCountText.fontStyle=FontStyle.Bold;readinessCountText.raycastTarget=false;
            readinessPhaseText=Label(readinessHudRoot,"ActualReadyPhase","WAITING",9,Muted,TextAnchor.MiddleRight,new Vector2(-8,-8),new Vector2(100,25),new Vector2(1,1));readinessPhaseText.raycastTarget=false;
            readinessLevelText=Label(readinessHudRoot,"ChallengeLevelSeparateFromParty","CHALLENGES · 8 LEVELS",10,Ivory,TextAnchor.MiddleLeft,new Vector2(9,-39),new Vector2(145,16),new Vector2(0,1));readinessLevelText.raycastTarget=false;
            var cap=Label(readinessHudRoot,"ActualMaximumTwoPlayers","MAX 2 PLAYERS",8,Muted,TextAnchor.MiddleRight,new Vector2(-9,-39),new Vector2(70,16),new Vector2(1,1));cap.raycastTarget=false;
            readinessHudRoot.gameObject.SetActive(false);
        }
        void RefreshReadinessHud()
        {
            if(readinessHudRoot==null||session==null)return;
            var rooms=session.Rooms;bool connected=rooms!=null&&rooms.Connected;
            bool campaign=campaignUi!=null&&campaignUi.Active;
            int members=connected?rooms.PlayerCount:0;
            var input=new ReadinessHudInput{
                Show=session.HasStarted&&readinessFooterFits&&minimapRoot!=null&&minimapRoot.gameObject.activeInHierarchy&&!IntroActive&&!LoadingActive&&!ShopVisible&&!DownedActive&&!session.IsDead&&!session.IsDowned&&!session.Victory,
                Connected=connected,OfflineSolo=!connected&&!session.CoopMode,CampaignActive=campaign,
                MembershipMatches=!campaign||campaignUi.MemberCount==members,Members=members,
                Ready=campaign?campaignUi.ReadyPlayerCount:connected?rooms.ReadyPlayerCount:0,
                Level=campaign?campaignUi.StageNumber:0,Phase=campaign?campaignUi.Phase:CampaignPhase.None,
                Running=session.Defense!=null&&session.Defense.Running,ArenaReady=session.Defense!=null&&session.Defense.ArenaReady,
                RemainingSeconds=session.Defense!=null?session.Defense.RemainingSeconds:float.NaN};
            var view=ReadinessHudMath.Evaluate(input);
            if(readinessHudRoot.gameObject.activeSelf!=view.Visible)readinessHudRoot.gameObject.SetActive(view.Visible);
            if(!view.Visible)return;
            string ready=ReadinessHudLabels.Ready(view),phase=ReadinessHudLabels.Status(view),level=ReadinessHudLabels.Level[view.Level];
            if(readinessCountText.text!=ready)readinessCountText.text=ready;
            if(readinessPhaseText.text!=phase)readinessPhaseText.text=phase;
            if(readinessLevelText.text!=level)readinessLevelText.text=level;
            int color=view.CountsKnown?view.AllReady?2:1:0;
            if(color!=readinessColorMode)
            {
                readinessColorMode=color;readinessCountText.color=color==2?Emerald:color==1?Gold:Muted;
                readinessKeycap.color=color==2?new Color(.055f,.18f,.13f,.96f):new Color(.07f,.10f,.09f,.95f);
            }
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Minimap-local below-bottom anchor, inherits actual map scale/opacity and parent/menu visibility; no world/input ownership.
// Array Formats: One cold footer/keycap,3cached texts+static max2 label, value-only accepted scalar input; no hot arrays/ToString/LINQ/GetComponent or queries.
// Dependencies/Pointers: Existing bound Campaign/Room ready counts and real Defense.RemainingSeconds only; no ready writes/resume/timer/enum mutation. Existing HUD hierarchy owns disposal; root accepts native text/layout allocations and two-peer UI.
