using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class OdysseyHud
    {
        // # ARCHITECTURE SNAPSHOT
        // Presentation in the existing HUD Canvas/menu ownership. The field Smith
        // supplies a CoopMatch adapter; only accepted host snapshots change charge
        // or tiers. This view never writes saves, points, tokens or tower ranks.
        private UnityEngine.UI.Text forgeBalance,forgeStatus,forgeScope,forgeApplication;
        private readonly UnityEngine.UI.Text[] forgeTierLabels=new UnityEngine.UI.Text[4];
        private readonly UnityEngine.UI.Text[,] forgeRungs=new UnityEngine.UI.Text[4,3];
        private readonly UnityEngine.UI.Text[] forgeBuyLabels=new UnityEngine.UI.Text[4];
        private readonly UnityEngine.UI.Button[] forgeBuyButtons=new UnityEngine.UI.Button[4];
        private readonly string[,] forgeRankText=new string[4,3];
        private readonly string[] forgeUpgradeText=new string[3];
        private readonly int[] displayedForgeTiers={-1,-1,-1,-1};
        private UnityEngine.UI.Button laneRemoveButton015,laneUpgradeButton015;
        internal bool LaneBuildMenuVisible015=>built&&CompactBuildPickerVisible015;
        private string displayedLaneFeedback015;
        private uint displayedLaneContext015;private bool displayedLaneWaiting015,displayedLaneOwned015;
        private string[] laneBuildLabels015,laneBuildFeedback015;
        private void PrepareLaneBuildText015()
        {
            laneBuildLabels015=new string[32];laneBuildFeedback015=new string[20];
            for(int owned=0;owned<2;owned++)for(int type=0;type<4;type++)for(int tier=0;tier<4;tier++)
                laneBuildLabels015[owned*16+type*4+tier]=(owned!=0?"REPLACE\n":"")+TowerForgeDefinitions.Get(type).Name.ToUpperInvariant()+"\n"+(tier>0?"TIER "+tier:"BLUEPRINT");
            for(int tokens=0;tokens<4;tokens++)for(int capacity=0;capacity<5;capacity++)
                laneBuildFeedback015[tokens*5+capacity]="TOKENS "+tokens+"  |  "+capacity+" SHARED TOWERS MAX";
        }
        private UnityEngine.UI.Button[] towerBuildButtons;
        private UnityEngine.UI.Text[] towerBuildLabels;
        private string forgeLocalMessage="",displayedForgeModelStatus,displayedBuildModelStatus;
        private int displayedForgeBalance=-1,displayedBuildTokens=-1,displayedBuildCapacity=-1,displayedBuildBinding=-1;
        private bool displayedFirstTowerUpgrade,forgeBuilt,forgeSnapshotValid,forgeTierBaselineValid,displayedForgeReady,displayedForgePending,displayedForgeContext,displayedForgeModels,displayedForgeCampaign,displayedForgeUpgradePhase;
        private bool buildSnapshotValid,displayedBuildContext,displayedBuildModels,displayedBuildForgeReady;
        private readonly int[] displayedBuildTiers={-1,-1,-1,-1};
        private MatchForgeOutcome displayedForgeOutcome;

        public bool IsFieldTowerForgeVisible=>forgeBuilt&&currentPanel==JourneyPanel.FieldForge&&
            panels[JourneyPanel.FieldForge].gameObject.activeInHierarchy;

        public void InitializeFieldTowerForge()
        {
            if(forgeBuilt)return;
            if(!built||session==null)throw new System.InvalidOperationException("Build the existing HUD before its field forge.");
            var content=PanelContent(JourneyPanel.FieldForge,"MatchTowerForge",new Vector2(1240,1024));
            MenuLabel(content,"FieldForgeTitle","TOWER ANVIL | UPGRADES",32,Ivory,40);
            forgeBalance=MenuLabel(content,"AcceptedMatchForgeBalance","WAITING FOR HOST STATE",23,Gold,28);
            forgeScope=MenuLabel(content,"MatchOnlyForgeScope","Four tier-one blueprints are included. Upgrades are optional.\nHearthstones in this campaign: +1 per accepted XP pickup. Separate from saved Heartcrystals and run skill points.",17,Muted,44);
            MenuLabel(content,"TowerPreviewScrollHelp","SCROLL / DRAG FOR ALL ELEMENTS · PAGE UP / PAGE DOWN",12,Muted,18);
            var gridRoot=ScrollingContent(content,"FourCoreTowerRankCards",480,out anvilPreviewScroll,true);
            var grid=gridRoot.gameObject.AddComponent<UnityEngine.UI.GridLayoutGroup>();
            grid.constraint=UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;grid.constraintCount=2;
            grid.cellSize=new Vector2(540,356);grid.spacing=new Vector2(16,16);grid.padding=new RectOffset(12,12,12,12);grid.childAlignment=TextAnchor.UpperCenter;
            for(int type=0;type<TowerForgeDefinitions.BaseTypes;type++)
            {
                int tower=type;var definition=TowerForgeDefinitions.Get(type);
                Color tint=new Color(definition.Red,definition.Green,definition.Blue);
                var card=Frame(gridRoot,"TowerRanks"+type,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(540,356),Panel,MutedGold);
                Label(card,"TowerName",definition.Name.ToUpperInvariant(),25,tint,TextAnchor.UpperLeft,new Vector2(18,-12),new Vector2(440,34),new Vector2(0,1));
                forgeTierLabels[type]=Label(card,"AcceptedTier","WAITING FOR HOST",18,Gold,TextAnchor.UpperLeft,new Vector2(18,-47),new Vector2(475,25),new Vector2(0,1));
                Label(card,"TargetRole",definition.Role,17,Muted,TextAnchor.UpperLeft,new Vector2(18,-77),new Vector2(500,26),new Vector2(0,1));
                var sprite=OracleElementEmblems.Get(type);
                var emblem=Rect(card,"CoreElementEmblem",new Vector2(1,1),new Vector2(1,1),new Vector2(-14,-10),new Vector2(42,42));
                if(sprite!=null){var image=Paint(emblem,Color.white);image.sprite=sprite;image.preserveAspect=true;}
                else foreach(var graphic in DrawSigil(emblem,type))graphic.color=tint;
                for(int tier=1;tier<=TowerForgeDefinitions.MaximumTier;tier++)
                {
                    BuildAnvilPreviewTile(card,type,tier);
                    forgeRankText[type,tier-1]=TowerForgeDefinitions.RankName(type,tier)+"\n"+TowerForgeDefinitions.EffectText(tier);
                    forgeRungs[type,tier-1]=Label(card,"Tier"+tier,forgeRankText[type,tier-1],13,Muted,TextAnchor.UpperCenter,
                        new Vector2(18+(tier-1)*168,-251),new Vector2(156,42),new Vector2(0,1));
                }
                var buy=Button(card,"RequestUpgrade"+type,"WAITING FOR HOST",new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,14),new Vector2(504,42),()=>RequestTowerUpgrade(tower));
                forgeBuyButtons[type]=buy;forgeBuyLabels[type]=buy.GetComponentInChildren<UnityEngine.UI.Text>();forgeBuyLabels[type].fontSize=18;
            }
            forgeUpgradeText[0]="UPGRADE TO TIER 2 | 8 HEARTHSTONES";
            forgeUpgradeText[1]="UPGRADE TO TIER 3 | 18 HEARTHSTONES";
            forgeUpgradeText[2]="MAXIMUM TIER";
            forgeApplication=MenuLabel(content,"AcceptedUpgradeApplication","Accepted upgrades improve your surviving towers of that type and future builds.\nPlacement uses the build tokens shown in your HUD; visiting the Tower Anvil never requires a purchase.",16,Muted,64);
            forgeStatus=MenuLabel(content,"ActualForgeReceipt","",17,Gold,44);
            MenuButton(content,"ReturnFromFieldForge","RETURN & LOCK MOUSE",()=>session.FieldSmith?.Close(true),44);
            forgeBuilt=true;
            lastCanvasSize=Vector2.zero; // This late-created card must enter the next responsive fit.
            panels[JourneyPanel.FieldForge].gameObject.SetActive(false);
        }

        public bool ShowFieldTowerForge()
        {
            if(!forgeBuilt||session.FieldSmith==null||!session.FieldSmith.ClaimsInteract||IntroActive||LoadingActive||DownedActive||ShopVisible)return false;
            AbilityBindings.CancelRebind();session.Pause();forgeLocalMessage="";forgeSnapshotValid=forgeTierBaselineValid=false;
            SetPanel(JourneyPanel.FieldForge);RefreshFieldTowerForge();return IsFieldTowerForgeVisible;
        }

        public void HideFieldTowerForge()
        {
            if(forgeBuilt&&currentPanel==JourneyPanel.FieldForge)SetPanel(JourneyPanel.None);
            forgeSnapshotValid=false;forgeLocalMessage="";
        }

        private void RequestTowerUpgrade(int type)
        {
            var smith=session.FieldSmith;if(smith==null||!IsFieldTowerForgeVisible)return;
            var result=smith.Submit(type);
            forgeLocalMessage=result==TowerForgeSubmission.Rejected?"Upgrade was not submitted. Check charge, tier and Tower Anvil range.":"";
            // AwaitingHost is deliberately not a purchase animation or debit.
            forgeSnapshotValid=false;RefreshFieldTowerForge();
        }

        public void RefreshFieldTowerForge()
        {
            if(!IsFieldTowerForgeVisible)return;
            var smith=session.FieldSmith;var adapter=smith!=null?smith.ForgeAdapter:null;
            bool ready=smith!=null&&smith.StateReady&&adapter!=null&&adapter.Mode==TowerForgeMode.CoopMatch;
            bool pending=adapter!=null&&adapter.Pending,context=smith!=null&&smith.CanTransact(),models=smith!=null&&smith.ModelsReady;
            bool campaign=smith!=null&&smith.CampaignForgeActive;
            bool upgradePhase=smith!=null&&smith.UpgradesAvailableHere;
            bool firstUpgrade=session.RefugeActivities!=null&&session.RefugeActivities.FirstTowerUpgradeAccepted;
            int balance=ready?adapter.Balance:0;
            MatchForgeOutcome outcome=smith!=null?smith.LastOutcome:MatchForgeOutcome.None;
            string modelStatus=smith!=null?smith.ModelStatus:"Tower models are unavailable.";
            bool changed=!forgeSnapshotValid||firstUpgrade!=displayedFirstTowerUpgrade||ready!=displayedForgeReady||pending!=displayedForgePending||context!=displayedForgeContext||
                models!=displayedForgeModels||campaign!=displayedForgeCampaign||upgradePhase!=displayedForgeUpgradePhase||balance!=displayedForgeBalance||outcome!=displayedForgeOutcome||modelStatus!=displayedForgeModelStatus;
            int acceptedElements011=0;
            for(int type=0;type<4;type++)
            {
                int tier=ready?adapter.GetTier(type):0;
                if(forgeTierBaselineValid&&displayedForgeReady&&tier>displayedForgeTiers[type])acceptedElements011|=1<<type;
                changed|=tier!=displayedForgeTiers[type];
            }
            RefreshAnvilPreviewAccepted(ready,adapter);
            if(!changed)return;
            for(int type=0;type<4;type++)if((acceptedElements011&(1<<type))!=0&&session.Audio!=null)
                if(!session.Audio.TryPlayElementUpgrade011(type))session.Audio.PlayProductionCue(ProductionCue.TowerUpgrade);
            forgeScope.text=campaign?
                "Four tier-one blueprints are included. Upgrades open only in the Refuge between levels.\nCampaign Hearthstones and accepted tower tiers carry between stages for this connected party.":
                "Four tier-one blueprints are included. Placement stays in the arena.\nUpgrade purchases are available only in campaign Refuge intermissions.";
            forgeApplication.text=campaign?
                "Accepted upgrades apply to your towers and future builds across this campaign.\nCampaign Hearthstones are separate from saved Heartcrystals and skill points. Visiting the Tower Anvil never requires a purchase.":
                "Accepted upgrades improve your surviving towers of that type and future builds.\nPlacement uses the build tokens shown in your HUD; visiting the Tower Anvil never requires a purchase.";
            forgeApplication.text+=(firstUpgrade?"\nFirst tower upgrade · COMPLETE for this profile.":"\nFirst tower upgrade · Optional: an accepted interstage rank upgrade completes it; a request alone does not.");
            forgeBalance.text=ready?(campaign?"HEARTHSTONES · THIS CAMPAIGN  ":"HEARTHSTONES · THIS MATCH  ")+balance:"WAITING FOR HOST STATE";
            for(int type=0;type<4;type++)
            {
                int tier=ready?adapter.GetTier(type):0;bool valid=TowerForgeDefinitions.ValidTier(tier);
                forgeTierLabels[type].text=valid?"ACCEPTED TIER "+tier+" / 3":"WAITING FOR HOST";
                for(int rung=0;rung<3;rung++)forgeRungs[type,rung].color=valid&&rung==tier-1?Ivory:Muted;
                int cost=valid?TowerForgeDefinitions.GetRank(tier).NextCost:0;
                forgeBuyLabels[type].text=!valid?"WAITING FOR HOST":pending?"AWAITING HOST":!upgradePhase?"REFUGE BETWEEN STAGES ONLY":forgeUpgradeText[tier-1];
                forgeBuyButtons[type].interactable=upgradePhase&&ready&&valid&&tier<3&&!pending&&context&&models&&balance>=cost;
                displayedForgeTiers[type]=tier;
            }
            forgeStatus.text=!models?modelStatus:!ready?"Waiting for the host's accepted match state.":pending?"Upgrade request sent. Waiting for the host; charge and tiers remain accepted values.":
                !upgradePhase?(smith!=null&&smith.InCampaignRefuge?
                    "Initial refuge: tier-one blueprints are included. Visit the Tower Anvil and Raykon; optional upgrades open after stage one.":
                    "Build and defend in the arena. Return to the Refuge between stages to upgrade towers."):
                smith!=null&&smith.AwaitingRefugeVisit?"Waiting for the host to record your Tower Anvil visit. No purchase is required.":
                !context?"Return to the Tower Anvil and focus this window to upgrade.":!string.IsNullOrEmpty(forgeLocalMessage)?forgeLocalMessage:ForgeOutcomeText(outcome);
            displayedFirstTowerUpgrade=firstUpgrade;displayedForgeReady=ready;displayedForgePending=pending;displayedForgeContext=context;displayedForgeModels=models;
            displayedForgeUpgradePhase=upgradePhase;displayedForgeCampaign=campaign;displayedForgeBalance=balance;displayedForgeOutcome=outcome;displayedForgeModelStatus=modelStatus;forgeSnapshotValid=true;forgeTierBaselineValid=ready;
        }

        private static string ForgeOutcomeText(MatchForgeOutcome outcome)
        {
            switch(outcome)
            {
                case MatchForgeOutcome.Applied:return "Upgrade accepted by the host.";
                case MatchForgeOutcome.InsufficientCharge:return "Not enough Forge charge. Upgrades are optional.";
                case MatchForgeOutcome.MaximumTier:return "This tower type is already at tier three.";
                case MatchForgeOutcome.StaleState:return "The host state changed. Review the current tier before trying again.";
                case MatchForgeOutcome.ContextDenied:return "Upgrades require an accepted Smith visit in the Refuge between stages, within reach.";
                case MatchForgeOutcome.RateLimited:return "Requests arrived too quickly. Wait before trying again.";
                case MatchForgeOutcome.InvalidRequest:
                case MatchForgeOutcome.SequenceRejected:return "The host rejected that request. Review accepted state before retrying.";
                default:return "Choose an optional upgrade, or return to your party.";
            }
        }

        private void RefreshTowerBuildMenu()
        {
            if(!CompactBuildLifetime015()||towerBuildButtons==null||session.Builder==null||session.Defense==null)return;
            bool context=session.Builder.BuildMenuContextValid,models=session.Builder.ModelsReady,forgeReady=session.Defense.ForgeStateReady;
            bool waiting=session.Builder.LaneBuildWaiting015,owned=session.Builder.LaneOwnedTowerActions015;string feedback=session.Builder.LaneBuildFeedback015;
            uint lease=session.Builder.LaneContextNonce015;int tokens=session.Defense.LocalBuildTokens,capacity=session.Defense.ActiveTowerCapacity,binding=Mathf.Max(AbilityBindings.LastChangeFrame,GameActionBindings.LastChangeFrame),selection=session.Builder.SelectedTowerType015;
            string preview=session.Builder.Hint;
            bool changed=!buildSnapshotValid||context!=displayedBuildContext||models!=displayedBuildModels||tokens!=displayedBuildTokens||capacity!=displayedBuildCapacity||binding!=displayedBuildBinding||
                forgeReady!=displayedBuildForgeReady||waiting!=displayedLaneWaiting015||owned!=displayedLaneOwned015||lease!=displayedLaneContext015||feedback!=displayedLaneFeedback015||session.Builder.ModelStatus!=displayedBuildModelStatus||selection!=displayedBuildSelection015||preview!=displayedBuildPreview015;
            for(int type=0;type<4;type++)changed|=displayedBuildTiers[type]!=(forgeReady?session.Defense.GetLocalTowerForgeTier(type):0);
            if(!changed)return;
            for(int type=0;type<4;type++)
            {int tier=forgeReady?session.Defense.GetLocalTowerForgeTier(type):0;towerBuildLabels[type].text=laneBuildLabels015[(owned?16:0)+type*4+Mathf.Clamp(tier,0,3)];
                towerBuildButtons[type].interactable=context&&models&&!waiting;displayedBuildTiers[type]=tier;}
            laneRemoveButton015.gameObject.SetActive(owned);laneUpgradeButton015.gameObject.SetActive(owned);laneRemoveButton015.interactable=context&&!waiting;
            RefreshCompactBuildSelection015(selection,tokens,capacity);
            buildFeedback.text=!models?session.Builder.ModelStatus:waiting?"WAITING FOR HOST CONFIRMATION…":feedback.Length!=0?feedback:!context?"Close and reopen beside an active lane.":
                preview.Length!=0?preview:"Aim at a floor block. Green: buildable. Red: blocked. Host rechecks support and clear space.";
            displayedBuildPreview015=preview;
            displayedBuildContext=context;displayedBuildModels=models;displayedBuildTokens=tokens;displayedBuildCapacity=capacity;displayedBuildBinding=binding;displayedBuildForgeReady=forgeReady;
            displayedBuildModelStatus=session.Builder.ModelStatus;displayedLaneFeedback015=feedback;displayedLaneWaiting015=waiting;displayedLaneOwned015=owned;displayedLaneContext015=lease;buildSnapshotValid=true;
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Existing metre/Y-up/XZ actor space, host Forge ledger and sole combat owners remain.
// Exact composed Refuge/interstage/balance adapter hunks; no new native/save authority.
// # ARCHITECTURE SNAPSHOT
// World metres/Y-up/XZ; existing station mesh/pivots and canonical host roles remain.
// Bounded cached collider/root references only; no new economy/native buffers or quest bits.
// Root owns Unity integration and native acceptance; this is a guarded work candidate.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: existing1240x1024FieldForgepanel,2x2cards enlarged356px within masked480pxscroll;3actual-tier model views each, labels and real request button remain inside cardbounds.
// Array Formats: existing accepted ledger/status caches plus12cold tile views and prebuilt rankstrings; all accepted tier/material updates are presentation only.
// Dependencies/Pointers: existing purchase/charge/host snapshot/optional upgrade authority unchanged. All3tiers visible via scroll independent of purchase availability; notready WAITING, future silhouettes, included tier1 rendered normally when accepted state is ready.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing accepted world metres/Y-up/XZ, exact1m terrain/.25m build roots and locked active-road context.
// Array Formats: Bounded native semantic/preview/body stores, exact64B versioned command and existing2peer/4tower snapshots.
// Dependencies/Pointers: Root integrates exact guarded slices; host economy/readiness/LOS and World geometry remain canonical, prepared/source compile is not native or browser acceptance.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Current authenticated run/epoch/context and monotonic real-time pending request ages; placement/world coordinates unchanged.
// Array Formats: Immutable Frame64, fixed Pending96 scalar state and one cached Frame64 per existing peer; no per-frame heap container or ABI change.
// Dependencies/Pointers: Bounded same-nonce delivery, exact host replay and response-controlled UI only. Root alone imports/native-tests; frozen original packet remains unchanged.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing accepted metre/Y-up/XZ support roots and HUD Canvas; cosmetic floor boxes do not establish build permission.
// Array Formats: Existing fixed8192 preview records, one ray job, four cached original tower choices, unchanged Frame64 pending/replay transaction.
// Dependencies/Pointers: Local picker owns no pause/cursor/economy; accepted host result alone closes successful transaction. Normal input/native/Web rendering remain separate acceptance gates.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing Y-up/XZ canonical placement roots and local active-play input lifetime; no terrain or host permission changes.
// Array Formats: Existing fixed preview records, scalar context/request state and cached HUD controls; no new runtime store or hot allocation.
// Dependencies/Pointers: Local commands require the current play/input owner. Highlight count is invalidated behind its existing job fence before refresh; host acceptance remains authoritative.
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing accepted elemental shop/UI/listener or cosmetic atlas space; no authority or saved-key changes.
// Array Formats: Four bounded clip/image identities and accepted-element bits; existing12models/fourcuevoices and workshop mesh.
// Dependencies/Pointers: Root follow-up imports four retained generated cues; baseline/replay/request failure stays silent, no hot loads/new audio sources or media/provider work here.
