using System;
using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class OdysseyHud
    {
        static readonly Color CampaignPaper020=new Color(.84f,.78f,.62f,.96f),CampaignInk020=new Color(.16f,.135f,.10f,1f),CampaignEdge020=new Color(.32f,.25f,.16f,1f);
        // Five rows of five original square pixels: flame, droplet, diamond/stone and curling air.
        static readonly uint[] CampaignElementBits020={0x067b8c4u,0x0efb884u,0x0477dc4u,0x0f0be1eu};
        RectTransform campaignObjectiveFrame020,nibwickVoxelHeader020;
        UnityEngine.UI.Text nibwickVoxelTitle020,nibwickVoxelObjective020;
        UnityEngine.UI.Button nibwickBack020;
        Color campaignObjectiveInkBefore020;
        string nibwickVoxelObjectiveCache020;
        bool nibwickVoxelTourKnown020,nibwickVoxelTour020;

        void BuildCampaignVoxels020()
        {
            if(campaignHubContinue020!=null)StyleCampaignVoxelButton020(campaignHubContinue020,CampaignInk020);
            if(objectiveText==null||campaignObjectiveFrame020!=null)return;
            var source=objectiveText.rectTransform;campaignObjectiveInkBefore020=objectiveText.color;
            campaignObjectiveFrame020=Frame(source.parent,"CampaignObjectiveVoxelFrame020",source.anchorMin,source.pivot,
                source.anchoredPosition+new Vector2(0,12),source.sizeDelta+new Vector2(28,36),CampaignPaper020,CampaignEdge020);
            campaignObjectiveFrame020.anchorMax=source.anchorMax;campaignObjectiveFrame020.SetSiblingIndex(source.GetSiblingIndex());
            AddCampaignVoxelCorners020(campaignObjectiveFrame020);
            var heading=Label(campaignObjectiveFrame020,"ActualCampaignObjectiveHeading020","CURRENT OBJECTIVE",13,CampaignInk020,TextAnchor.UpperLeft,
                new Vector2(14,-5),new Vector2(240,22),new Vector2(0,1));heading.raycastTarget=false;
            AddCampaignElementRow020(campaignObjectiveFrame020,new Vector2(1,1),new Vector2(-62,-14));
            campaignObjectiveFrame020.gameObject.SetActive(false);
        }
        void SetCampaignVoxelsVisible020(bool visible)
        {
            if(campaignObjectiveFrame020==null)return;
            campaignObjectiveFrame020.gameObject.SetActive(visible);
            if(objectiveText!=null)objectiveText.color=visible?CampaignInk020:campaignObjectiveInkBefore020;
        }
        void BuildNibwickVoxelHeader020()
        {
            if(nibwickVoxelHeader020!=null)return;
            nibwickVoxelHeader020=Frame(loreScroll.content,"NibwickCampaignVoxelHeader020",Vector2.zero,new Vector2(.5f,1),Vector2.zero,new Vector2(0,96),CampaignPaper020,CampaignEdge020);
            Height(nibwickVoxelHeader020,96);nibwickVoxelHeader020.SetAsFirstSibling();AddCampaignVoxelCorners020(nibwickVoxelHeader020);
            nibwickVoxelTitle020=Label(nibwickVoxelHeader020,"NibwickCampaignHeader020","NIBWICK · GUIDE",18,CampaignInk020,TextAnchor.MiddleLeft,Vector2.zero,Vector2.zero,new Vector2(0,1));
            var title=nibwickVoxelTitle020.rectTransform;title.anchorMin=new Vector2(0,1);title.anchorMax=new Vector2(1,1);title.offsetMin=new Vector2(18,-38);title.offsetMax=new Vector2(-132,-8);nibwickVoxelTitle020.raycastTarget=false;
            nibwickVoxelObjective020=Label(nibwickVoxelHeader020,"ActualCampaignObjective020","",16,CampaignInk020,TextAnchor.MiddleLeft,Vector2.zero,Vector2.zero,new Vector2(0,1));
            var body=nibwickVoxelObjective020.rectTransform;body.anchorMin=new Vector2(0,1);body.anchorMax=new Vector2(1,1);body.offsetMin=new Vector2(18,-84);body.offsetMax=new Vector2(-18,-40);nibwickVoxelObjective020.raycastTarget=false;
            AddCampaignElementRow020(nibwickVoxelHeader020,new Vector2(1,1),new Vector2(-64,-22));nibwickVoxelHeader020.gameObject.SetActive(false);
        }
        void ShowNibwickVoxels020(NibwickRuntime020 owner)
        {
            if(nibwickVoxelHeader020==null)return;
            nibwickVoxelHeader020.gameObject.SetActive(true);RefreshNibwickVoxels020(owner);
        }
        void RefreshNibwickVoxels020(NibwickRuntime020 owner)
        {
            if(owner==null||nibwickVoxelHeader020==null)return;
            bool tour=owner.TourStarted020;
            if(!nibwickVoxelTourKnown020||nibwickVoxelTour020!=tour)
            {nibwickVoxelTourKnown020=true;nibwickVoxelTour020=tour;nibwickVoxelTitle020.text=tour?"NIBWICK · OPTIONAL TOUR":"NIBWICK · GUIDE";}
            string value=session!=null?session.Objective:null;if(string.IsNullOrEmpty(value))value="Explore Portal Landing.";
            if(nibwickVoxelObjectiveCache020!=value){nibwickVoxelObjectiveCache020=value;nibwickVoxelObjective020.text=value;}
        }
        void HideNibwickVoxels020(){if(nibwickVoxelHeader020!=null&&nibwickVoxelHeader020.gameObject.activeSelf)nibwickVoxelHeader020.gameObject.SetActive(false);}
        void FocusNibwickVoxels020()
        {
            var events=UnityEngine.EventSystems.EventSystem.current;if(events==null)return;
            var target=nibwickTour020!=null&&nibwickTour020.interactable?nibwickTour020:nibwickBack020;
            if(target!=null)events.SetSelectedGameObject(target.gameObject);
        }
        void StyleCampaignVoxelButton020(UnityEngine.UI.Button button,Color ink)
        {
            StylePaperButton(button,ink);AddCampaignVoxelCorners020(button.transform as RectTransform);
            var navigation=button.navigation;navigation.mode=UnityEngine.UI.Navigation.Mode.Automatic;button.navigation=navigation;
            button.gameObject.AddComponent<OracleCampaignUiCancel020>().CancelRequested=CancelCampaignStyledUi020;
        }
        void CancelCampaignStyledUi020()
        {
            // GameSession already owns physical Escape; UI Cancel also covers controllers.
            if(UnityEngine.InputSystem.Keyboard.current?.escapeKey.wasPressedThisFrame==true)return;
            if(CampaignBookVisible020){EscapeCampaignBook020();return;}
            if(IsNibwickConversationVisible020(nibwickPanelOwner020))BackMenu015();
        }
        void AddCampaignVoxelCorners020(RectTransform parent)
        {
            for(int i=0;i<4;i++)
            {
                float x=(i&1)==0?0f:1f,y=(i&2)==0?0f:1f;
                var block=Rect(parent,"OwnedVoxelCorner020",new Vector2(x,y),new Vector2(x,y),new Vector2(x==0?3:-3,y==0?3:-3),new Vector2(6,6));
                Paint(block,CampaignEdge020,false);
            }
        }
        void AddCampaignElementRow020(Transform parent,Vector2 anchor,Vector2 position)
        {
            var row=Rect(parent,"FourOriginalVoxelElements020",anchor,new Vector2(.5f,.5f),position,new Vector2(104,20));
            for(int element=0;element<4;element++)
            {
                var glyph=Rect(row,"OriginalVoxelElement020",new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2((element-1.5f)*26f,0),new Vector2(18,18));
                uint bits=CampaignElementBits020[element];Color color=TutorialElementColor(element);
                for(int pixel=0;pixel<25;pixel++)if((bits&(1u<<pixel))!=0)
                {
                    var block=Rect(glyph,"OwnedElementPixel020",new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2((pixel%5-2)*3f,(2-pixel/5)*3f),new Vector2(3,3));Paint(block,color,false);
                }
            }
        }
    }

    public sealed class OracleCampaignUiCancel020:MonoBehaviour,UnityEngine.EventSystems.ICancelHandler
    {
        public Action CancelRequested;
        public void OnCancel(UnityEngine.EventSystems.BaseEventData value){if(isActiveAndEnabled)CancelRequested?.Invoke();}
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing Canvas objective rect geometry and Lore scroll; decorations are campaign-only owned descendants.
// Array Formats: Four original25bit element masks, bounded retained UI primitives/control references and cached tour/objective scalars; no new sprite/texture/native asset lifetime.
// Dependencies/Pointers: Cold builders only; refresh compares borrowed exact objective strings and Boolean state. Existing NPC command/voice/Back/Escape/pointer owners retain authority; UI Cancel only requests their existing Back behavior. No map, quest grants, live layout rebuild or per-frame formatting is introduced.
