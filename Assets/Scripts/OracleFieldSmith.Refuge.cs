using System;
using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class OracleFieldSmith
    {
        CoopCampaignDirector forgeCampaign;
        bool preparedInRefuge,campaignSaveFence;

        public CoopCampaignDirector BoundCampaign=>forgeCampaign;
        public bool CampaignForgeActive=>forgeCampaign!=null&&forgeCampaign.Active;
        public bool UpgradesAvailableHere=>session!=null&&session.Defense!=null&&session.Defense.CampaignTowerUpgradePhase;
        // Retain the save boundary if a campaign disconnects in the refuge;
        // a solo restart explicitly clears CoopMode before local town shopping.
        // The initial shared tutorial still uses its original town-shop lesson.
        public bool CampaignSaveFence=>CampaignForgeActive||campaignSaveFence&&session!=null&&session.CoopMode;
        public bool InCampaignRefuge=>initialized&&CampaignForgeActive&&forgeCampaign.InRefuge&&
            session!=null&&session.HasStarted&&session.CoopMode&&session.World!=null&&
            session.World.IsTutorialRefuge&&!session.World.IsCoopArena&&session.Rooms!=null&&
            session.Rooms.Connected&&session.Defense!=null&&!session.Defense.Running&&
            !string.IsNullOrEmpty(session.Defense.ForgeRunId);
        public bool AwaitingRefugeVisit=>InCampaignRefuge&&(forgeCampaign.LocalVisitMask&CampaignQuestState.SmithRole)==0;
        bool RefugeVisitAccepted=>!InCampaignRefuge||!AwaitingRefugeVisit;
        bool ForgeActive=>InCampaignRefuge;

        void RefreshCampaignSaveFence()
        {
            if(CampaignForgeActive)campaignSaveFence=true;
            else if(session!=null&&!session.CoopMode)campaignSaveFence=false;
        }

        public void BindCampaignForge(CoopCampaignDirector campaign)
        {
            if(!initialized||campaign==null||session==null||campaign.gameObject!=session.gameObject)
                throw new ArgumentException("Bind the initialized actual Forge owner to its session campaign.");
            if(forgeCampaign!=null&&forgeCampaign!=campaign)
                throw new InvalidOperationException("One campaign lifetime owns the Forge binding.");
            forgeCampaign=campaign;
        }

        void PrepareRefugeForge()
        {
            // Borrow the existing town Smith. RetireRun destroys actorRoot only;
            // actorRoot remains null here, so the original town art survives.
            var borrowed=session.Shop!=null?session.Shop.Blacksmith:null;
            if(borrowed==null||!borrowed.gameObject.activeInHierarchy)
            {Status="Waiting for the actual refuge Smith.";return;}
            smith=borrowed;
            adapter=session.Defense.CreateCampaignForgeAdapter(CanTransact);
            if(adapter==null||adapter.Mode!=TowerForgeMode.CoopMatch)
            {RetireRun();Status="The refuge Forge requires host-accepted campaign state.";return;}
            preparedRun=session.Defense.ForgeRunId;preparedInRefuge=true;
            sightFrame=lookFrame=-1;
            Status=UpgradesAvailableHere?
                "Between-stage Forge: optional upgrades use carried campaign charge.":
                "Initial refuge: tier-one blueprints are free. Upgrade purchases open after stage one.";
        }

        void RecordRefugeSmithVisit()
        {
            if(!InCampaignRefuge||smith==null||!Visible||!InRange()||!HasLineOfSight())return;
            forgeCampaign.RegisterNpcTarget(CampaignQuestState.SmithRole,smith.transform,smith.TowerAnvilFeet,smith.TowerAnvilAimPoint);
            // Request after a genuine opened Smith interaction. Only the host's
            // accepted snapshot grants the visit; this code sets no quest bits.
            forgeCampaign.RequestNpcVisit(CampaignQuestState.SmithRole);
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Unity world metres/Y-up/XZ; this partial borrows the existing town Smith and HUD.
// Four core types use the existing CoopMatch adapter and host-owned campaign account.
// Legacy actorRoot cleanup remains local; refuge art is borrowed, with no arena spawn, save write or quest grant.

// # ARCHITECTURE SNAPSHOT
// Existing metre/Y-up/XZ actor space, host Forge ledger and sole combat owners remain.
// Exact composed Refuge/interstage/balance adapter hunks; no new native/save authority.
// # ARCHITECTURE SNAPSHOT
// World metres/Y-up/XZ; existing station mesh/pivots and canonical host roles remain.
// Bounded cached collider/root references only; no new economy/native buffers or quest bits.
// Root owns Unity integration and native acceptance; this is a guarded work candidate.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Actual cleared-level monster portal world anchors and refuge-only Smith; stable same-party campaign identity.
// Array Formats: Bounded cold8-point reach job and existing phase/ready/Forge stores; no new visual/actor/wire field.
// Dependencies/Pointers: Existing host phase/epoch/pose/LOS and interlevel visits/readiness own progression; no midwave exit, battlefield shop or SDK session reset.
