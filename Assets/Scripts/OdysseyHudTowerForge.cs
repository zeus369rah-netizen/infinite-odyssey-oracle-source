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
        private UnityEngine.UI.Button[] towerBuildButtons;
        private UnityEngine.UI.Text[] towerBuildLabels;
        private string forgeLocalMessage="",displayedForgeModelStatus,displayedBuildModelStatus;
        private int displayedForgeBalance=-1,displayedBuildTokens=-1,displayedBuildCapacity=-1,displayedBuildBinding=-1;
        private bool forgeBuilt,forgeSnapshotValid,forgeTierBaselineValid,displayedForgeReady,displayedForgePending,displayedForgeContext,displayedForgeModels,displayedForgeCampaign,displayedForgeUpgradePhase;
        private bool buildSnapshotValid,displayedBuildContext,displayedBuildModels,displayedBuildForgeReady;
        private readonly int[] displayedBuildTiers={-1,-1,-1,-1};
        private MatchForgeOutcome displayedForgeOutcome;

        public bool IsFieldTowerForgeVisible=>forgeBuilt&&currentPanel==JourneyPanel.FieldForge&&
            panels[JourneyPanel.FieldForge].gameObject.activeInHierarchy;

        public void InitializeFieldTowerForge()
        {
            if(forgeBuilt)return;
            if(!built||session==null)throw new System.InvalidOperationException("Build the existing HUD before its field forge.");
            var content=PanelContent(JourneyPanel.FieldForge,"MatchTowerForge",new Vector2(1240,1000));
            MenuLabel(content,"FieldForgeTitle","TOWER ANVIL | UPGRADES",32,Ivory,48);
            forgeBalance=MenuLabel(content,"AcceptedMatchForgeBalance","WAITING FOR HOST STATE",23,Gold,32);
            forgeScope=MenuLabel(content,"MatchOnlyForgeScope","Four tier-one blueprints are included. Upgrades are optional.\nForge charge: +1 per accepted XP pickup this defense. Separate from saved Heartstones and run skill points.",17,Muted,62);
            var gridRoot=Rect(content,"FourCoreTowerRankCards",Vector2.zero,new Vector2(.5f,.5f),Vector2.zero,new Vector2(0,552));
            Height(gridRoot,552);
            var grid=gridRoot.gameObject.AddComponent<UnityEngine.UI.GridLayoutGroup>();
            grid.constraint=UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;grid.constraintCount=2;
            grid.cellSize=new Vector2(540,268);grid.spacing=new Vector2(16,16);grid.childAlignment=TextAnchor.MiddleCenter;
            for(int type=0;type<TowerForgeDefinitions.BaseTypes;type++)
            {
                int tower=type;var definition=TowerForgeDefinitions.Get(type);
                Color tint=new Color(definition.Red,definition.Green,definition.Blue);
                var card=Frame(gridRoot,"TowerRanks"+type,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(540,268),Panel,MutedGold);
                Label(card,"TowerName",definition.Name.ToUpperInvariant(),25,tint,TextAnchor.UpperLeft,new Vector2(18,-12),new Vector2(440,34),new Vector2(0,1));
                forgeTierLabels[type]=Label(card,"AcceptedTier","WAITING FOR HOST",18,Gold,TextAnchor.UpperLeft,new Vector2(18,-47),new Vector2(475,25),new Vector2(0,1));
                Label(card,"TargetRole",definition.Role,17,Muted,TextAnchor.UpperLeft,new Vector2(18,-77),new Vector2(500,37),new Vector2(0,1));
                var sprite=OracleElementEmblems.Get(type);
                var emblem=Rect(card,"CoreElementEmblem",new Vector2(1,1),new Vector2(1,1),new Vector2(-14,-10),new Vector2(42,42));
                if(sprite!=null){var image=Paint(emblem,Color.white);image.sprite=sprite;image.preserveAspect=true;}
                else foreach(var graphic in DrawSigil(emblem,type))graphic.color=tint;
                for(int tier=1;tier<=TowerForgeDefinitions.MaximumTier;tier++)
                {
                    forgeRankText[type,tier-1]="T"+tier+"  "+TowerForgeDefinitions.RankName(type,tier)+"  |  "+TowerForgeDefinitions.EffectText(tier);
                    forgeRungs[type,tier-1]=Label(card,"Tier"+tier,forgeRankText[type,tier-1],16,Muted,TextAnchor.UpperLeft,
                        new Vector2(18,-118-(tier-1)*25),new Vector2(504,24),new Vector2(0,1));
                }
                var buy=Button(card,"RequestUpgrade"+type,"WAITING FOR HOST",new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,14),new Vector2(504,48),()=>RequestTowerUpgrade(tower));
                forgeBuyButtons[type]=buy;forgeBuyLabels[type]=buy.GetComponentInChildren<UnityEngine.UI.Text>();forgeBuyLabels[type].fontSize=18;
            }
            forgeUpgradeText[0]="UPGRADE TO TIER 2 | 8 CHARGE";
            forgeUpgradeText[1]="UPGRADE TO TIER 3 | 18 CHARGE";
            forgeUpgradeText[2]="MAXIMUM TIER";
            forgeApplication=MenuLabel(content,"AcceptedUpgradeApplication","Accepted upgrades improve your surviving towers of that type and future builds.\nPlacement uses the build tokens shown in your HUD; visiting the Tower Anvil never requires a purchase.",16,Muted,52);
            forgeStatus=MenuLabel(content,"ActualForgeReceipt","",17,Gold,48);
            MenuButton(content,"ReturnFromFieldForge","RETURN & LOCK MOUSE",()=>session.FieldSmith?.Close(true),50);
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
            int balance=ready?adapter.Balance:0;
            MatchForgeOutcome outcome=smith!=null?smith.LastOutcome:MatchForgeOutcome.None;
            string modelStatus=smith!=null?smith.ModelStatus:"Tower models are unavailable.";
            bool changed=!forgeSnapshotValid||ready!=displayedForgeReady||pending!=displayedForgePending||context!=displayedForgeContext||
                models!=displayedForgeModels||campaign!=displayedForgeCampaign||upgradePhase!=displayedForgeUpgradePhase||balance!=displayedForgeBalance||outcome!=displayedForgeOutcome||modelStatus!=displayedForgeModelStatus;
            bool acceptedIncrease=false;
            for(int type=0;type<4;type++)
            {
                int tier=ready?adapter.GetTier(type):0;
                acceptedIncrease|=forgeTierBaselineValid&&displayedForgeReady&&tier>displayedForgeTiers[type];
                changed|=tier!=displayedForgeTiers[type];
            }
            if(!changed)return;
            if(acceptedIncrease)session.Audio?.PlayProductionCue(ProductionCue.TowerUpgrade);
            forgeScope.text=campaign?
                "Four tier-one blueprints are included. Upgrades open only in the Refuge after stages one and two.\nForge charge and accepted tower tiers carry between stages for this connected party.":
                "Four tier-one blueprints are included. Placement stays in the arena.\nUpgrade purchases are available only in campaign Refuge intermissions.";
            forgeApplication.text=campaign?
                "Accepted upgrades apply to your towers and future builds across this campaign.\nCharge is separate from saved Heartcrystals and skill points. Visiting the Tower Anvil never requires a purchase.":
                "Accepted upgrades improve your surviving towers of that type and future builds.\nPlacement uses the build tokens shown in your HUD; visiting the Tower Anvil never requires a purchase.";
            forgeBalance.text=ready?(campaign?"CAMPAIGN FORGE CHARGE  ":"MATCH FORGE CHARGE  ")+balance:"WAITING FOR HOST STATE";
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
            displayedForgeReady=ready;displayedForgePending=pending;displayedForgeContext=context;displayedForgeModels=models;
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
            if(currentPanel!=JourneyPanel.Build||towerBuildButtons==null||session.Builder==null||session.Defense==null)return;
            bool context=session.Builder.BuildMenuContextValid,models=session.Builder.ModelsReady,forgeReady=session.Defense.ForgeStateReady;
            int tokens=session.Defense.LocalBuildTokens,capacity=session.Defense.ActiveTowerCapacity,binding=AbilityBindings.LastChangeFrame;
            bool changed=!buildSnapshotValid||context!=displayedBuildContext||models!=displayedBuildModels||tokens!=displayedBuildTokens||
                capacity!=displayedBuildCapacity||binding!=displayedBuildBinding||forgeReady!=displayedBuildForgeReady||session.Builder.ModelStatus!=displayedBuildModelStatus;
            for(int type=0;type<4;type++)changed|=displayedBuildTiers[type]!=(forgeReady?session.Defense.GetLocalTowerForgeTier(type):0);
            if(!changed)return;
            for(int type=0;type<4;type++)
            {
                int tier=forgeReady?session.Defense.GetLocalTowerForgeTier(type):0;
                towerBuildLabels[type].text=TowerForgeDefinitions.Get(type).Name.ToUpperInvariant()+(forgeReady?"  |  TIER "+tier:"  |  BASE BLUEPRINT")+"\n"+TowerForgeDefinitions.Get(type).Role;
                towerBuildButtons[type].interactable=context&&models&&tokens>0;displayedBuildTiers[type]=tier;
            }
            buildFeedback.text=!models?session.Builder.ModelStatus:!context?"This pad context is no longer valid. Cancel and approach a highlighted empty pad.":
                "BUILD TOKENS  "+tokens+"  |  "+capacity+" ACTIVE PADS\n"+session.Builder.BuildPads.Length+" SHARED PADS TOTAL | Tier-one choices included; each placement spends one token.";
            displayedBuildContext=context;displayedBuildModels=models;displayedBuildTokens=tokens;displayedBuildCapacity=capacity;displayedBuildBinding=binding;displayedBuildForgeReady=forgeReady;displayedBuildModelStatus=session.Builder.ModelStatus;buildSnapshotValid=true;
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
