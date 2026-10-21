using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class OdysseyHud
    {
        private UnityEngine.UI.Button tutorialPortalSkipButton;
        private UnityEngine.UI.Text tutorialSkipHint;
        private bool minimapContextReady, mapContextArena, mapContextCoop, mapContextConnected;

        private static string SelectionCountLabel(int mask)
        {
            int count = SpellIngredientRules.Count(mask);
            return count == 4 ? "ULTIMATE ABILITY" : count == 3 ? "TRIPLE" : count == 2 ? "PAIR" : count == 1 ? "SINGLE" : "CHOOSE";
        }

        private bool CanOfferTutorialSkip => session != null && session.HasStarted && session.Tutorial != null
            && !session.Tutorial.Finished && session.World != null && session.World.Mode020==WorldMode020.TutorialRefuge
            && !session.Victory && !session.IsDead && !session.IsDowned && !IntroActive && !LoadingActive
            && (session.Opening == null || !session.Opening.IsTransitioning);

        private void BuildTutorialPortalSkipPanel()
        {
            var content = PanelContent(JourneyPanel.TutorialPortalSkip, "TutorialPortalSkipPanel", new Vector2(720, 440));
            MenuLabel(content, "TutorialPortalSkipHeading", "THE VIOLET PORTAL", 32, Ivory, 62);
            MenuLabel(content, "TutorialPortalSkipExplanation",
                "Finish your training, or skip the remaining lessons and open the hub portal.\nYour connected party stays in this room.", 19, Muted, 88);
            tutorialPortalSkipButton = MenuButton(content, "SkipTutorialAtPortal", "SKIP & ENTER PORTAL", SkipTutorialFromMenu, 58);
            MenuButton(content, "ContinueTutorialAtPortal", "CONTINUE TRAINING", ResumeJourney, 52);
        }

        // Called by the existing session's portal-approach guard. Opening the modal
        // pauses and releases pointer lock, making the real Skip action clickable.
        public bool ShowTutorialPortalSkip()
        {
            if (!built || !CanOfferTutorialSkip || currentPanel != JourneyPanel.None || ShopVisible
                || session.FieldSmith != null && session.FieldSmith.Visible) return false;
            AbilityBindings.CancelRebind();
            session.Pause();
            SetPanel(JourneyPanel.TutorialPortalSkip);
            return true;
        }

        private void BuildTutorialSkipHint()
        {
            tutorialSkipHint = Label(tutorialPrompt, "TutorialSkipPauseHint", "Esc: pause · Hold Alt: mouse", 12, Muted,
                TextAnchor.UpperCenter, new Vector2(160, -40), new Vector2(310, 20), new Vector2(.5f, 1));
        }

        private void RefreshTutorialSkipAccess()
        {
            bool available = CanOfferTutorialSkip;
            if(tutorialSkipHint!=null)
            {
                string text=session.CursorFree015?"Mouse free · Release Alt to aim · Esc: pause":"Esc: pause · Hold Alt: mouse";
                if(tutorialSkipHint.text!=text)tutorialSkipHint.text=text;
            }
            if (tutorialSkipHint != null && tutorialSkipHint.gameObject.activeSelf != available)
                tutorialSkipHint.gameObject.SetActive(available);
            if (tutorialPortalSkipButton != null) tutorialPortalSkipButton.interactable = available;
            if (currentPanel == JourneyPanel.TutorialPortalSkip && !available && !IntroActive && !LoadingActive
                && !session.IsDead && !session.IsDowned && !session.Victory)
                ResumeJourney();
        }

        private void RefreshMinimapContext(bool force = false)
        {
            if (session == null || session.World == null || minimapTitle == null || mapLegend == null) return;
            bool arena = session.World.IsCoopArena;
            bool connected = session.Rooms != null && session.Rooms.Connected;
            bool coop = connected ? !(session.Rooms.IsHost && session.Rooms.PlayerCount==1) : session.CoopMode;
            if (!force && minimapContextReady && arena == mapContextArena && coop == mapContextCoop && connected == mapContextConnected) return;
            minimapContextReady = true;
            mapContextArena = arena; mapContextCoop = coop; mapContextConnected = connected;
            minimapTitle.text = arena
                ? coop ? connected ? "CO-OP DEFENSE ARENA" : "CO-OP DEFENSE · OFFLINE" : "SOLO DEFENSE ARENA"
                : coop ? connected ? "TUTORIAL ROOM · CO-OP" : "TUTORIAL ROOM · CO-OP OFFLINE" : "TUTORIAL ROOM · SOLO";
            mapLegend.text = arena
                ? "GREEN: YOU · GOLD: TOWERS\nRED: MONSTERS · VIOLET: RIFTS\nNORTH IS UP"
                : CurrentRefugeMapLegend;
            ApplyRefugeMapGeometry(arena);
        }

        private void BuildTutorialZoneMapMarkers()
        {
            if(session==null||session.World==null)return;
            BuildRefugeStationMapOverlay();
        }

        private void TutorialZoneMapMarker(string name, string glyph, Vector3 position, Color color)
        {
            if (!ContainsMapPoint(position)) return;
            var marker = Rect(mapSurface, name, Vector2.zero, new Vector2(.5f, .5f), MapPosition(position), new Vector2(14, 14));
            Paint(marker, new Color(.025f, .035f, .033f, .95f));
            Border(marker, color, 1);
            Label(marker, "ZoneGlyph", glyph, 10, color, TextAnchor.MiddleCenter,
                Vector2.zero, new Vector2(14, 14), new Vector2(.5f, .5f));
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing 1920x1080 Canvas and unchanged +XZ world-to-minimap projection.
// Array Formats: Three fixed cold map markers and cached scalar context flags; no runtime native/GPU array or ownership changes.
// Dependencies/Pointers: Existing session SkipTutorialToHub and pause/resume own state; World owns exact tutorial-zone anchors; no room leave/create call in UI.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing guide/menu Canvas and accepted real tutorial/world/session owners.
// Array Formats: Cold fixed presentation stores and cached labels; no new gameplay/native memory ownership.
// Dependencies/Pointers: Work-only original UI packet; real pointer/audio/practice/reward/network authorities retained. Native/browser acceptance pending.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing accepted world metres/Y-up, input bits and owner simulation seconds.
// Array Formats: Four ingredient deadlines, fixed48 projectile pool, at most three component shots; existing host frame ABIs.
// Dependencies/Pointers: Work-only rapid-casting candidate. Existing host hit/damage/reward owners retained; root owns native/browser acceptance.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing fitted guide/menu Canvas and actual guide view/audio state.
// Array Formats: Existing bounded UI/state stores; cold immutable65status labels, no frame formatting.
// Dependencies/Pointers: Original restart/portal/settings/narration/voice/input/achievement owners remain. Root owns source apply and full compile/native/browser/listening acceptance.
