using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class OdysseyHud
    {
        // # ARCHITECTURE SNAPSHOT
        // Accepted010 state is presentation only. Bootstrap binds its actual
        // session component once; no quest bits/readiness/ranks/roster are invented.
        // No input poll, event subscription, transport, wallet or scene lookup.
        private CoopCampaignDirector campaignUi;
        private UnityEngine.UI.Text campaignPortalDescription,campaignMapName,campaignMapDescription;
        private UnityEngine.UI.Button campaignOutcomeReplay;
        private bool campaignCopyValid;
        private CampaignPhase campaignShownPhase;
        private int campaignShownStage,campaignShownVisits,campaignShownReady,campaignShownMembers,campaignShownBinding;
        private bool campaignShownLocalReady,campaignShownWaveReady015;
        private int campaignShownWave015,campaignShownAdmitted015,campaignShownQuota015;
        private string campaignShownStatus,campaignObjective="",campaignPauseCopy="",campaignPortalCopy="",campaignMapTitle="";
        public void BindCampaignUi(CoopCampaignDirector campaign)
        {
            if(!built||session==null||campaign==null||campaign.gameObject!=session.gameObject)
                throw new System.ArgumentException("Bind the initialized HUD to its actual session campaign component.");
            if(campaignUi!=null&&campaignUi!=campaign)throw new System.InvalidOperationException("Campaign UI is already bound to another lifetime owner.");
            campaignUi=campaign;campaignCopyValid=false;
        }
        private bool CampaignUiActive=>campaignUi!=null&&campaignUi.Active;
        private bool CampaignUiHasSoloRestartNotice=>campaignUi!=null&&campaignUi.HasSoloRestartNotice;
        private bool CampaignUiInRefuge=>CampaignUiActive&&campaignUi.InRefuge;
        private bool CampaignUiFailed=>CampaignUiActive&&campaignUi.Phase==CampaignPhase.Failed;
        private bool CampaignUiTerminal=>CampaignUiActive&&(campaignUi.Phase==CampaignPhase.Complete||campaignUi.Phase==CampaignPhase.Failed);

        private void RefreshCampaignCopy()
        {
            if(CampaignUiHasSoloRestartNotice){campaignObjective=campaignPauseCopy=campaignUi.SoloRestartNotice;campaignCopyValid=false;return;}
            if(!CampaignUiActive){campaignCopyValid=false;return;}
            var phase=campaignUi.Phase;int stage=campaignUi.StageNumber,visits=campaignUi.LocalVisitMask;
            int ready=campaignUi.ReadyPlayerCount,members=campaignUi.MemberCount,binding=AbilityBindings.LastChangeFrame;
            bool localReady=campaignUi.LocalReady;string status=campaignUi.Status;
            var defense=session.Defense;
            int wave=phase==CampaignPhase.Defense&&defense!=null?defense.WaveNumber:0;
            int admitted=phase==CampaignPhase.Defense&&defense!=null?defense.SpawnedThisWave:0;
            int quota=phase==CampaignPhase.Defense&&defense!=null?defense.WaveQuota:0;
            bool waveReady=phase==CampaignPhase.Defense&&defense!=null&&defense.Running&&defense.ArenaReady&&CampaignWaveLabel015.ValidCampaignWave(stage,wave,admitted,quota);
            if(campaignCopyValid&&campaignShownPhase==phase&&campaignShownStage==stage&&campaignShownVisits==visits&&
                campaignShownReady==ready&&campaignShownMembers==members&&campaignShownLocalReady==localReady&&
                campaignShownBinding==binding&&campaignShownStatus==status&&campaignShownWave015==wave&&
                campaignShownAdmitted015==admitted&&campaignShownQuota015==quota&&campaignShownWaveReady015==waveReady)return;
            campaignCopyValid=true;campaignShownPhase=phase;campaignShownStage=stage;campaignShownVisits=visits;
            campaignShownReady=ready;campaignShownMembers=members;campaignShownLocalReady=localReady;
            campaignShownBinding=binding;campaignShownStatus=status;
            campaignShownWave015=wave;campaignShownAdmitted015=admitted;campaignShownQuota015=quota;campaignShownWaveReady015=waveReady;
            string heading="MILESTONE "+stage+" / "+CampaignQuestState.StageCount;
            campaignMapTitle=heading+" | SHRINE DEFENSE";
            campaignPortalCopy=status+"\nREADY "+ready+" / "+members+" ORACLES";
            switch(phase)
            {
                case CampaignPhase.Refuge:
                    // Recommendation only: either accepted visit order is valid.
                    campaignObjective=(visits&1)==0?(visits&2)==0?"Visit the Tower Anvil (no purchase).\nSpeak to Raykon.":
                        "Visit the Tower Anvil (no purchase).":(visits&2)==0?"Speak to Raykon, the spell mentor.":
                        localReady?"Waiting for party readiness.":"Go to the violet portal and ready for the next stage.";
                    campaignPauseCopy=heading+" | REFUGE\n"+(visits==3?"Review the party at the violet portal.":
                        visits==0?"Visit the Tower Anvil and Raykon in either order. No purchase required.":(visits&1)==0?"Visit the Tower Anvil. No purchase required.":"Speak to Raykon, the spell mentor.");
                    break;
                case CampaignPhase.Preparing:
                    campaignObjective="Preparing the next stage. Wait for the party.";
                    campaignPauseCopy=heading+" | PREPARING\nWaiting for the actual party barrier.";break;
                case CampaignPhase.Defense:
                    string waveCopy=waveReady?"WAVE "+wave+" / "+CampaignBalance015.WaveCount+" · "+admitted+" / "+quota+" ADMITTED":"WAITING FOR ACCEPTED WAVE";
                    campaignObjective=waveCopy+"\nProtect the green shrine · "+heading+".";
                    campaignPauseCopy=heading+" | DEFENSE\n"+waveCopy+"\nClear all five waves, then return through an active monster portal. Forty waves span eight milestones.";break;
                case CampaignPhase.StageClear:
                    campaignObjective="Resume, then walk to any active monster portal to return the party to the refuge.";
                    campaignPauseCopy=heading+" COMPLETE\nResume and walk to an active monster portal. Use ["+GameActionBindings.GetLabel("Interact")+"] to return the party to the refuge.\nVisit the Tower Anvil and Raykon, then every player readies for the next level. Towers are rebuilt; accepted progress and upgrades remain.";break;
                case CampaignPhase.Complete:
                    campaignObjective="All forty waves and eight milestones complete. Thank you for playing this prototype.";
                    campaignPauseCopy=campaignObjective;break;
                case CampaignPhase.Failed:
                    campaignObjective="The campaign ended. Return to the main menu.";
                    campaignPauseCopy=campaignObjective;break;
                default:campaignObjective="";campaignPauseCopy="";break;
            }
        }
        private string CampaignObjectiveOr(string fallback)
        {RefreshCampaignCopy();return CampaignUiHasSoloRestartNotice||CampaignUiActive?campaignObjective:fallback;}
        private void ActivateJourneyPortal()
        {
            if(CampaignUiActive)
            {
                if(campaignUi.InRefuge&&campaignUi.LocalVisitMask==CampaignQuestState.RequiredVisits&&!campaignUi.LocalReady)
                    campaignUi.SetReady(true);
                return;
            }
            session.StartCoopFromPortal();
        }
        private void WithdrawJourneyReadiness()
        {
            if(CampaignUiActive){if(campaignUi.InRefuge)campaignUi.SetReady(false);return;}
            session.Rooms?.SetCoopReady(false);
        }
        private bool RefreshCampaignPortal()
        {
            if(!CampaignUiActive)return false;
            RefreshCampaignCopy();
            bool refuge=campaignUi.InRefuge,ready=campaignUi.LocalReady;
            bool eligible=refuge&&campaignUi.LocalVisitMask==CampaignQuestState.RequiredVisits&&!ready;
            portalCampaignButton.interactable=false;portalCampaignExplanation.text="COMING SOON";
            portalCoopButton.interactable=eligible&&!LoadingActive&&(session.Rooms==null||!session.Rooms.Busy);
            portalReadyLabel.text=ready?"WAITING FOR PARTY":eligible?"READY FOR NEXT STAGE":refuge?"VISIT ANVIL AND RAYKON":"JOURNEY IN PROGRESS";
            portalUnreadyButton.gameObject.SetActive(refuge&&ready);
            portalStatus.text=campaignPortalCopy;
            campaignPortalDescription.text=refuge?"Visit the Tower Anvil and Raykon in either order. Spending is optional.":"Your connected party continues the same campaign.";
            campaignMapName.text=campaignMapTitle;
            campaignMapDescription.text="40 waves · 8 milestones of five.\nReturn to the refuge between milestones; four core tower choices.";
            return true;
        }
        private void RestoreOuterPortalCopy()
        {
            campaignPortalDescription.text="Choose the journey beyond the tutorial hub.";
            campaignMapName.text="SHRINE DEFENSE";
            campaignMapDescription.text="40 waves · 8 milestones of five.\nFire · Water · Earth · Air towers.";
        }
        private void ApplyCampaignOutcome()
        {
            bool terminal=CampaignUiTerminal;
            if(campaignOutcomeReplay!=null)campaignOutcomeReplay.gameObject.SetActive(!terminal);
            if(!terminal)return;
            outcomeHeading.text=campaignUi.Phase==CampaignPhase.Complete?"THE JOURNEY IS COMPLETE":"THE CAMPAIGN ENDS";
            outcomeDescription.text=campaignUi.Phase==CampaignPhase.Complete?
                "All "+CampaignQuestState.StageCount+" milestones and all forty waves are complete.\nThank you for playing this prototype.":campaignUi.Status;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// World metres/Y-up/XZ; existing station mesh/pivots and canonical host roles remain.
// Bounded cached collider/root references only; no new economy/native buffers or quest bits.
// Root owns Unity integration and native acceptance; this is a guarded work candidate.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Stable existing arena road IDs and host-owned eight-level campaign identity.
// Array Formats: Existing two peers/eight lane arrays/four shared pads/48 actors/4096 reward tombstones; no ABI growth.
// Dependencies/Pointers: Level-derived lane prefix, actual host ready/admission/recovery fences; root owns shared protocol and native acceptance.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Actual cleared-level monster portal world anchors and refuge-only Smith; stable same-party campaign identity.
// Array Formats: Bounded cold8-point reach job and existing phase/ready/Forge stores; no new visual/actor/wire field.
// Dependencies/Pointers: Existing host phase/epoch/pose/LOS and interlevel visits/readiness own progression; no midwave exit, battlefield shop or SDK session reset.
