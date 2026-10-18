using System;
using UnityEngine;

namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // One cold component install; root private scene preparation is an Action.
    // This bridge owns no profile, transport, world geometry or native buffers.
    public static class CampaignBootstrap
    {
        // Called once by root after Shop/Revival/Loading/Director initialization.
        // prepareArena is the existing private GameSession.PrepareSharedArena.
        public static CoopCampaignDirector Install(GameSession session,Action prepareArena)
        {
            if(session==null||prepareArena==null)throw new ArgumentNullException(nameof(session));
            if(session.GetComponent<CoopCampaignDirector>()!=null)throw new InvalidOperationException("Campaign already installed.");
            var campaign=session.gameObject.AddComponent<CoopCampaignDirector>();
            campaign.Initialize(session,prepareArena);
            if(session.FieldSmith==null)throw new InvalidOperationException("Initialize the actual field Forge owner before the campaign.");
            session.FieldSmith.BindCampaignForge(campaign);return campaign;
        }
        public static bool TrySetReady(GameSession session,bool ready)
        {
            var campaign=session!=null?session.GetComponent<CoopCampaignDirector>():null;
            if(campaign==null||!campaign.Active)return false;
            campaign.SetReady(ready);return true;
        }
        public static bool IsTerminal(GameSession session)
        {
            var campaign=session!=null?session.GetComponent<CoopCampaignDirector>():null;
            return campaign!=null&&(campaign.Phase==CampaignPhase.Complete||campaign.Phase==CampaignPhase.Failed);
        }
        public static bool TryReportPreparationFailure(GameSession session,string reason)
        {
            var campaign=session!=null?session.GetComponent<CoopCampaignDirector>():null;
            if(campaign==null||campaign.Phase!=CampaignPhase.Preparing)return false;
            campaign.ReportPreparationFailure(reason);return true;
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Existing metre/Y-up/XZ actor space, host Forge ledger and sole combat owners remain.
// Exact composed Refuge/interstage/balance adapter hunks; no new native/save authority.
