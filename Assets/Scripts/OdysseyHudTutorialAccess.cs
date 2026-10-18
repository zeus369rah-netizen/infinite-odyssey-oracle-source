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
            return count == 4 ? "NOVA" : count == 3 ? "ADD FOURTH" : count == 2 ? "PAIR" : count == 1 ? "SINGLE" : "CHOOSE";
        }

        private bool CanOfferTutorialSkip => session != null && session.HasStarted && session.Tutorial != null
            && !session.Tutorial.Finished && session.World != null && !session.World.IsCoopArena
            && !session.Victory && !session.IsDead && !session.IsDowned && !IntroActive && !LoadingActive
            && (session.Opening == null || !session.Opening.IsTransitioning);

        private void BuildTutorialPortalSkipPanel()
        {
            var content = PanelContent(JourneyPanel.TutorialPortalSkip, "TutorialPortalSkipPanel", new Vector2(720, 440));
            MenuLabel(content, "TutorialPortalSkipHeading", "THE VIOLET PORTAL", 32, Ivory, 62);
            MenuLabel(content, "TutorialPortalSkipExplanation",
                "Finish your training, or skip the remaining lessons and open the hub portal.\nYour connected party stays in this room.", 19, Muted, 88);
            tutorialPortalSkipButton = MenuButton(content, "SkipTutorialAtPortal", "SKIP TUTORIAL", SkipTutorialFromMenu, 58);
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
            tutorialSkipHint = Label(tutorialPrompt, "TutorialSkipPauseHint", "ESC: PAUSE, THEN SKIP TUTORIAL AT ANY TIME", 12, Muted,
                TextAnchor.UpperCenter, new Vector2(0, -122), new Vector2(852, 22), new Vector2(.5f, 1));
        }

        private void RefreshTutorialSkipAccess()
        {
            bool available = CanOfferTutorialSkip;
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
            bool coop = session.CoopMode || connected;
            if (!force && minimapContextReady && arena == mapContextArena && coop == mapContextCoop && connected == mapContextConnected) return;
            minimapContextReady = true;
            mapContextArena = arena; mapContextCoop = coop; mapContextConnected = connected;
            minimapTitle.text = arena
                ? coop ? connected ? "CO-OP DEFENSE ARENA" : "CO-OP DEFENSE · OFFLINE" : "SOLO DEFENSE ARENA"
                : coop ? connected ? "TUTORIAL ROOM · CO-OP" : "TUTORIAL ROOM · CO-OP OFFLINE" : "TUTORIAL ROOM · SOLO";
            mapLegend.text = arena
                ? "GREEN: YOU · GOLD: TOWERS\nRED: MONSTERS · VIOLET: RIFTS\nNORTH IS UP"
                : "GREEN: YOU · VIOLET: PORTAL\nM: MONSTER PRACTICE · R: RAYKON\nT: TOWER TRAINING";
        }

        private void BuildTutorialZoneMapMarkers()
        {
            if (session == null || session.World == null || !session.World.IsTutorialRefuge || session.World.IsCoopArena) return;
            // World owns the actual interaction/spawn anchors. UI only projects them.
            TutorialZoneMapMarker("MonsterPracticeMapZone", "M", session.World.TutorialMonsterPracticePosition, new Color(.87f, .40f, .30f));
            TutorialZoneMapMarker("RaykonMapZone", "R", session.World.TutorialRaykonPosition, new Color(.54f, .78f, .98f));
            TutorialZoneMapMarker("TowerTrainingMapZone", "T", session.World.TutorialTowerPracticePosition, Gold);
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
