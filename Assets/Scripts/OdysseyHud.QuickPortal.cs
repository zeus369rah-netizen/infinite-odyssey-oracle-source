using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class OdysseyHud
    {
        UnityEngine.UI.Button quickPortalButton;
        UnityEngine.UI.Text quickPortalLabel;
        RectTransform topHeading020;
        CanvasGroup topHeadingOpacity020;
        Vector2 topActionsBounds020;
        float topActionsMapWidth020;
        bool topActionsFitKnown020;

        void BindTopHeading020(RectTransform heading)
        {
            topHeading020=heading;topHeadingOpacity020=heading.gameObject.AddComponent<CanvasGroup>();
            topActionsFitKnown020=false;
            topHeadingOpacity020.interactable=false;topHeadingOpacity020.blocksRaycasts=false;
        }
        void FitTopActions020(Vector2 bounds)
        {
            if(topHeading020==null||topHeadingOpacity020==null)return;
            // Reserve the actual minimap right edge and Quick Portal left edge with fixed gutters.
            float mapWidth=minimapRoot!=null?minimapRoot.rect.width*minimapRoot.localScale.x:229;
            if(topActionsFitKnown020&&topActionsBounds020==bounds&&Mathf.Approximately(topActionsMapWidth020,mapWidth))return;
            topActionsFitKnown020=true;topActionsBounds020=bounds;topActionsMapWidth020=mapWidth;
            float left=36+mapWidth+20;
            float right=bounds.x-210-238-16;
            float scale=Mathf.Min(1,Mathf.Max(0,right-left)/630);
            bool readable=scale>=.65f;
            topHeadingOpacity020.alpha=readable?1:0;
            if(!readable)return;
            float half=315*scale;
            float center=Mathf.Clamp(bounds.x*.5f,left+half,right-half);
            topHeading020.localScale=Vector3.one*scale;
            topHeading020.anchoredPosition=new Vector2(center-bounds.x*.5f,-37);
        }

        void BuildQuickPortalButton(Transform parent)
        {
            quickPortalButton = Button(parent, "QuickEnterPortal", "ENTER PORTAL", new Vector2(1, 1),
                new Vector2(1, 1), new Vector2(-210, -35), new Vector2(238, 54), QuickEnterPortal);
            quickPortalLabel = quickPortalButton.GetComponentInChildren<UnityEngine.UI.Text>();
            quickPortalLabel.fontSize = 16;
            quickPortalButton.gameObject.SetActive(false);
        }

        bool QuickPortalAvailable => session != null && session.HasStarted && session.World != null &&
            session.World.Mode020==WorldMode020.TutorialRefuge && !session.IsDead && !session.IsDowned && !session.Victory &&
            !IntroActive && !LoadingActive && !ShopVisible &&
            (session.FieldSmith == null || !session.FieldSmith.Visible) &&
            (currentPanel == JourneyPanel.None || currentPanel == JourneyPanel.Pause || currentPanel == JourneyPanel.TutorialInstruction);

        void RefreshQuickPortalButton()
        {
            if (quickPortalButton == null) return;
            bool visible = QuickPortalAvailable;
            if (quickPortalButton.gameObject.activeSelf != visible) quickPortalButton.gameObject.SetActive(visible);
            if (!visible) return;
            bool busy = session.Rooms != null && session.Rooms.Busy;
            quickPortalButton.interactable = !busy;
            string label = busy ? "CONNECTING…" : session.Tutorial != null && !session.Tutorial.Finished
                ? "SKIP & ENTER PORTAL" : "ENTER PORTAL";
            if (quickPortalLabel.text != label) quickPortalLabel.text = label;
        }

        public void QuickEnterPortal()
        {
            if (!QuickPortalAvailable || session.Rooms != null && session.Rooms.Busy) return;
            session.Audio?.StartAudioFromGesture();
            if (session.Tutorial != null && !session.Tutorial.Finished) session.SkipTutorialToHub();
            if (session.Tutorial == null || !session.Tutorial.Finished) return;
            // The existing authority decides readiness and preparation. This shortcut
            // never invents visits or forces another player to enter the arena.
            if (CampaignUiActive) ActivateJourneyPortal();
            else session.StartCoopFromPortal();
            session.Pause();
            ShowPortalChoices();
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing reference-resolution screen overlay; no direct world teleport.
// Array Formats: One cold uGUI button/text pair and cached literal labels; no recurring allocations.
// Dependencies/Pointers: Existing SkipTutorialToHub, campaign readiness and RoomConnection own all transitions; no quest/economy/party bypass.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing reference Canvas; center heading is fitted between exact minimap and top-right action rectangles only on Canvas bounds change.
// Array Formats: Two retained heading references and scalar geometry; no frame loop, text formatting or dynamic layout objects.
// Dependencies/Pointers: Quick Portal availability/action and canonical Pause/Back/Escape remain untouched. Decorative heading alpha yields on extremely narrow bounds instead of covering actions; external browser logos require separate root observation.
