using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class OdysseyHud
    {
        RectTransform nibwickControls020;
        NibwickRuntime020 nibwickPanelOwner020;
        UnityEngine.UI.Button nibwickTour020,nibwickSkip020;
        UnityEngine.UI.Text nibwickTourLabel020;

        public bool IsNibwickConversationVisible020(NibwickRuntime020 owner)=>built&&owner!=null&&nibwickPanelOwner020==owner&&
            currentPanel==JourneyPanel.Lore&&nibwickControls020!=null&&nibwickControls020.gameObject.activeInHierarchy;
        public bool NibwickMenuBlocks020(NibwickRuntime020 owner)=>built&&currentPanel!=JourneyPanel.None&&!IsNibwickConversationVisible020(owner);
        void EnsureNibwickControls020()
        {
            if(nibwickControls020!=null)return;
            // Reuse the existing book viewport/content; no extra Canvas, scroll
            // view, event system or full-screen panel is built for the NPC.
            var item=new GameObject("NibwickOptionalChoices020",typeof(RectTransform),typeof(UnityEngine.UI.VerticalLayoutGroup));
            nibwickControls020=(RectTransform)item.transform;nibwickControls020.SetParent(loreScroll.content,false);
            var layout=item.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.spacing=8;layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;
            BuildNibwickVoxelHeader020();
            var ink=new Color(.23f,.18f,.115f,1);
            MenuLabel(nibwickControls020,"NibwickTourScope020","Optional tour. End it whenever you like; the tour grants no items or story unlocks.",16,ink,54);
            nibwickTour020=MenuButton(nibwickControls020,"NibwickTakeTour020","TAKE THE TOUR",TakeNibwickTour020,42);
            nibwickTourLabel020=nibwickTour020.GetComponentInChildren<UnityEngine.UI.Text>();StyleCampaignVoxelButton020(nibwickTour020,ink);
            nibwickSkip020=MenuButton(nibwickControls020,"NibwickSkipStop020","SKIP THE NEXT EXPLANATION",SkipNibwickStop020,42);StyleCampaignVoxelButton020(nibwickSkip020,ink);
            StyleCampaignVoxelButton020(MenuButton(nibwickControls020,"NibwickOtherArrivals020","THE FIRST TWO ARRIVALS?",AskNibwickArrivals020,42),ink);
            StyleCampaignVoxelButton020(MenuButton(nibwickControls020,"NibwickEndTour020","NOT NOW / END TOUR",EndNibwickTour020,42),ink);
            nibwickBack020=MenuButton(nibwickControls020,"NibwickReadingBack020","BACK [ESC]",BackMenu015,42);StyleCampaignVoxelButton020(nibwickBack020,ink);
            nibwickControls020.gameObject.SetActive(false);
        }
        public void ShowNibwickConversation020(NibwickRuntime020 owner,string exactLine)
        {
            if(owner==null||!owner.LeaseCurrent020(owner.Generation020)||!Application.isFocused)return;
            ShowLoreBook("Nibwick · Welcome-bell keeper",exactLine);
            if(currentPanel!=JourneyPanel.Lore)return;
            EnsureNibwickControls020();nibwickPanelOwner020=owner;nibwickControls020.gameObject.SetActive(true);
            ShowNibwickVoxels020(owner);RefreshNibwickControls020(owner);Canvas.ForceUpdateCanvases();loreScroll.verticalNormalizedPosition=1;FocusNibwickVoxels020();
        }
        public void RefreshNibwickControls020(NibwickRuntime020 owner)
        {
            if(!IsNibwickConversationVisible020(owner))return;
            bool can=owner.LeaseCurrent020(owner.Generation020);RefreshNibwickVoxels020(owner);
            nibwickTour020.interactable=can&&owner.CanBeginTour020;
            nibwickSkip020.interactable=can&&owner.TourStarted020;
            string label=owner.TourStarted020?"CONTINUE TOUR":"TAKE THE TOUR";
            if(nibwickTourLabel020.text!=label)nibwickTourLabel020.text=label;
        }
        public void SetNibwickReadingLine020(NibwickRuntime020 owner,string exactLine)
        {
            if(!IsNibwickConversationVisible020(owner)||loreBody.text==exactLine)return;
            loreInk.BeginReveal(exactLine,bookInstantText||bookSystemReducedMotion);
            Canvas.ForceUpdateCanvases();loreScroll.verticalNormalizedPosition=1;
        }
        public void HideNibwickControls020(NibwickRuntime020 owner=null)
        {
            if(owner!=null&&nibwickPanelOwner020!=owner)return;
            nibwickPanelOwner020=null;if(nibwickControls020!=null)nibwickControls020.gameObject.SetActive(false);HideNibwickVoxels020();
            // Reading visibility never calls StopNpcGreeting009 or owns a voice.
        }
        void SendNibwickChoice020(NibwickCommand020 value,bool close)
        {
            var owner=nibwickPanelOwner020;if(!IsNibwickConversationVisible020(owner))return;
            owner.Request020(value);
            if(close)ResumeJourney();
        }
        void TakeNibwickTour020(){SendNibwickChoice020(NibwickCommand020.TakeTour,true);}
        void SkipNibwickStop020(){SendNibwickChoice020(NibwickCommand020.SkipStop,true);}
        void EndNibwickTour020(){SendNibwickChoice020(NibwickCommand020.EndTour,true);}
        void AskNibwickArrivals020(){SendNibwickChoice020(NibwickCommand020.OtherArrivals,false);}
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing generic Lore book scroll content and accepted Campaign NPC owner; no new Canvas, pointer capture or game-mode authority.
// Array Formats: One cold four-button optional choice group, cached owner/control refs and constant labels; no frame callbacks/closures, generated strings or queued narration.
// Dependencies/Pointers: Generic lore/achievement and leaving-book hooks hide only owned controls. Current NPC voice/subtitle lease survives reading close. Existing ResumeJourney owns real control/pointer return; root owns native UI/input acceptance.
