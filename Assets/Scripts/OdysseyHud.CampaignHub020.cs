using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class OdysseyHud
    {
        UnityEngine.UI.Button campaignHubContinue020,legacyJoinHub020,legacyHostHub020,legacyHowToHub020;
        bool campaignHubHudKnown020,campaignHubHudVisible020;
        string campaignHubHint020="";
        public void SetCampaignHubHint020(string value){campaignHubHint020=value??"";}
        bool CampaignHubHudMode020=>session!=null&&session.World!=null&&session.World.Mode020==WorldMode020.CampaignHub;
        void BuildCampaignHubEntry020()
        {
            campaignHubContinue020=Button(campaignPaper020,"EnterPreparedPortalLanding020","ENTER PORTAL LANDING",new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(0,-218),new Vector2(350,52),ContinueCampaignHub020);
            campaignHubContinue020.gameObject.SetActive(false);BuildCampaignVoxels020();PrepareCampaignHubMinimap020();
        }
        void RefreshCampaignHubGateway020(bool unavailable)
        {
            if(campaignHubContinue020==null)return;
            bool ready=!unavailable&&session!=null&&session.PrepareCampaignHub020()&&session.CanEnterPreparedCampaignHub020;
            campaignHubContinue020.gameObject.SetActive(ready);campaignHubContinue020.interactable=ready;
            if(ready)
            {
                campaignCaption020.text="Explore Portal Landing, or replay the illustrated prologue.\nThe wider campaign is still in development.";
                (campaignPlay020.transform as RectTransform).anchoredPosition=new Vector2(0,-300);
            }
        }
        void ContinueCampaignHub020()
        {
            if(session==null||!session.CanEnterPreparedCampaignHub020||campaignClosing020)return;
            session.Audio?.StartAudioFromGesture();CloseCampaignBookImmediate020();
            if(!session.TryStartCampaignHub020())ShowCampaignGateway020(true);
        }
        void HideCampaignHubEntry020(){if(campaignHubContinue020!=null)campaignHubContinue020.gameObject.SetActive(false);}
        bool UpdateCampaignHubHud020()
        {
            bool hub=CampaignHubHudMode020;
            if(!campaignHubHudKnown020||campaignHubHudVisible020!=hub)
            {
                campaignHubHudKnown020=true;campaignHubHudVisible020=hub;SetCampaignVoxelsVisible020(hub);
                if(minimapRoot!=null)minimapRoot.gameObject.SetActive(!hub);
                if(experienceSlider!=null)experienceSlider.gameObject.SetActive(!hub);
                if(experiencePickupText!=null)experiencePickupText.gameObject.SetActive(!hub);
                if(walletText!=null)walletText.gameObject.SetActive(!hub);
            }
            RefreshCampaignHubMinimap020();
            RefreshCampaignCycleHud020();
            if(!hub)return false;
            if(spellRoot!=null&&spellRoot.gameObject.activeSelf)spellRoot.gameObject.SetActive(false);
            if(tutorialPrompt!=null&&tutorialPrompt.gameObject.activeSelf)tutorialPrompt.gameObject.SetActive(false);
            if(practicePrompt!=null&&practicePrompt.gameObject.activeSelf)practicePrompt.gameObject.SetActive(false);
            if(objectiveCompassRoot!=null&&objectiveCompassRoot.gameObject.activeSelf)objectiveCompassRoot.gameObject.SetActive(false);
            if(readinessHudRoot!=null&&readinessHudRoot.gameObject.activeSelf)readinessHudRoot.gameObject.SetActive(false);
            if(elementLabel!=null)elementLabel.gameObject.SetActive(false);if(chargeText!=null)chargeText.gameObject.SetActive(false);
            if(charges!=null)for(int i=0;i<charges.Length;i++)if(charges[i].gameObject.activeSelf)charges[i].gameObject.SetActive(false);
            if(shrineStatusText!=null&&shrineStatusText.text!="PORTAL LANDING")shrineStatusText.text="PORTAL LANDING";
            if(objectiveText!=null&&objectiveText.text!=session.Objective)objectiveText.text=session.Objective;
            // Nibwick/quest/map are distinct positive-mode owners; old tutorial/TD hints cannot stand in for them.
            if(interactionText!=null&&interactionText.text!=campaignHubHint020)interactionText.text=campaignHubHint020;
            return true;
        }
        void RefreshCampaignHubPauseMenu020()
        {
            bool legacy=!CampaignHubHudMode020;
            if(legacyJoinHub020!=null&&legacyJoinHub020.gameObject.activeSelf!=legacy)legacyJoinHub020.gameObject.SetActive(legacy);
            if(legacyHostHub020!=null&&legacyHostHub020.gameObject.activeSelf!=legacy)legacyHostHub020.gameObject.SetActive(legacy);
            if(legacyHowToHub020!=null&&legacyHowToHub020.gameObject.activeSelf!=legacy)legacyHowToHub020.gameObject.SetActive(legacy);
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing Canvas and exact accepted CampaignHub world mode; no navigation, room, item, quest or bridge authority.
// Array Formats: Four cold button references and two cached HUD visibility scalars; regular updates reuse labels/references.
// Dependencies/Pointers: Continue requires actual prepared hub readiness; Replay stays independent. After the existing minimap refresh, the cycle footer reads the atmosphere's accepted generation/revision snapshot and clears when invalid or hidden. Legacy tutorial/TD map/XP/room/guide affordances hide in the third mode; health/stamina/pause/settings and future real campaign consumers remain distinct.
